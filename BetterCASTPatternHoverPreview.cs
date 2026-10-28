using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Metadata;
using Sims3.SimIFace;
using Sims3.UI;
using Sims3.UI.CAS;

namespace BetterBuildByJojobou
{
    /*
     * Better CASt - preview on hover (patterns AND colors).
     *
     * While the mouse rests on
     *   - a pattern thumbnail in "Choisir une texture", or
     *   - a harmony card / a saved color of the intro screen,
     * the pattern or color is shown on the object / wall / floor WITHOUT
     * touching the game's current preset or its undo history. As soon as the
     * mouse leaves, the original is sent back. A real click is handled by the
     * game as usual (its preset changes, so nothing is restored).
     *
     * How the preview is applied (v5):
     *
     *   The game has two ways to show a pattern, and the preview uses the
     *   same one the game itself uses for the same kind of target:
     *
     *   - OBJECTS: the live path the game uses while you drag a color
     *     (CompositorUtil.ApplyPreset). It only changes what the design tool
     *     shows. Nothing is committed to the object, so closing the CASt
     *     without clicking leaves the object exactly as it was.
     *
     *   - WALLS, FLOORS and the rest: the game's own "FinalizePresetTask"
     *     (the only path the game has for them, color dragging included).
     *     v5 calls it exactly the way the game does: through a real delegate
     *     queued as a game task, not through a reflection Invoke.
     *
     *   In both cases a TEMPORARY copy of the preset carries the hovered
     *   pattern / color. The game's own preset and its undo list are never
     *   touched. Going back to the original is the same call with the game's
     *   real preset.
     *
     * Integration: ONE line, called from Better CASt's own control binding
     * (never from a patched game method):
     *
     *     BetterCASTPatternHoverPreview.Attach(controller, materialsBin);
     *
     * The color cards (BetterCASTColorCards) feed the color hover with
     * SetColorHover / NotifyCommitted. Nothing loads before something is
     * hovered, so it costs nothing while the mouse is elsewhere.
     */
    public static class BetterCASTPatternHoverPreview
    {
        // Set to false to turn the feature off completely.
        private static readonly bool Enabled = true;

        // The player's switch (the eye button of the CASt). false: nothing is
        // previewed, and whatever was previewed goes back to normal.
        private static bool sUserEnabled = true;

        // The mouse must rest on a thumbnail / card this long (milliseconds)
        // before the preview is applied.
        private static readonly int DwellMs = 300;

        // Safety: a preview task that has not reported back after this long
        // (milliseconds) is considered lost.
        private static readonly int BusyTimeoutMs = 8000;

        // How long the preview waits for the game's own work (a click, a
        // color change) before going ahead anyway (milliseconds).
        private static readonly int GameBusyGraceMs = 1500;

        // After a click on a color card the game needs a moment to commit the
        // color: the preview stays out of its way for this long (milliseconds).
        private static readonly int ClickQuietMs = 700;

        // The color cards report the hovered card on every tick; a report
        // older than this (milliseconds) is ignored.
        private static readonly int ColorReportMaxAgeMs = 300;

        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public;

        private static readonly FieldInfo GridField =
            typeof(CASCompositorController).GetField(
                "mMaterialsWindowGrid", InstanceFlags);

        private static readonly FieldInfo SkewerWindowsField =
            typeof(CASCompositorController).GetField(
                "mMaterialsSkewerPatternWindow", InstanceFlags);

        private static readonly FieldInfo PresetField =
            typeof(CASCompositorController).GetField(
                "mCurrentPreset", InstanceFlags);

        private static readonly FieldInfo DesignObjectField =
            typeof(CASCompositorController).GetField(
                "mCurrentDesignObject", InstanceFlags);

        private static readonly FieldInfo SavedColorsField =
            typeof(CASCompositorController).GetField(
                "mSavedColors", InstanceFlags);

        private static readonly FieldInfo LinkButtonField =
            typeof(CASCompositorController).GetField(
                "mColorsDragButton", InstanceFlags);

        private static readonly PropertyInfo SelectedPatternProperty =
            typeof(CASCompositorController).GetProperty(
                "MaterialSkewerSelectedPattern", InstanceFlags);

        private static readonly PropertyInfo SelectedColorProperty =
            typeof(CASCompositorController).GetProperty(
                "SelectedColorIndex", InstanceFlags);

        // ------------------------------------------------------------
        // STATE
        // ------------------------------------------------------------

        private static WindowBase sBin;
        private static CASCompositorController sController;

        private static object sHoverTag;
        private static int sHoverSince;

        // The pattern / card currently previewed (null = the game's own state).
        private static object sShownTag;

        // The color currently previewed (only meaningful for a color card).
        private static Vector3 sShownColor;

        // The design object the preview state belongs to.
        private static object sTrackedObject;

        // A preview task has been queued and has not finished yet.
        private static bool sBusy;
        private static int sBusySince;

        // Since when the game itself reports "busy" (0 = it does not).
        private static int sGameBusySince;

        // The preview stays quiet until this time (0 = not quiet).
        private static int sQuietUntil;

        // What the game's preset held when the preview started.
        private static int sRow = -1;
        private static string[] sVars;
        private static Dictionary<string, Complate> sOriginals;
        private static Dictionary<string, Sims3.SimIFace.Color[]> sOriginalColors;

        // The hovered color card, as reported by the color cards.
        private static object sColorTag;
        private static Vector3 sColorValue;
        private static int sColorStamp;

        // ------------------------------------------------------------
        // PUBLIC ENTRY POINTS
        // ------------------------------------------------------------

        public static void Attach(
            CASCompositorController controller,
            WindowBase materialsBin)
        {
            if (!Enabled ||
                controller == null ||
                materialsBin == null)
            {
                return;
            }

            try
            {
                if (!object.ReferenceEquals(sController, controller))
                {
                    ResetState();
                    sController = controller;
                }

                if (!object.ReferenceEquals(sBin, materialsBin))
                {
                    if (sBin != null)
                    {
                        sBin.Tick -= OnBinTick;
                    }

                    ResetState();

                    sBin = materialsBin;
                    sBin.Tick += OnBinTick;
                }
            }
            catch
            {
                // The CASt must keep working even if this feature fails.
            }
        }

        // Called by the color cards on every tick: the card the mouse is on
        // (cardTag, any object that stays the same for the same card) and the
        // color a click on it would apply. cardTag = null: no card hovered.
        public static void SetUserEnabled(bool enabled)
        {
            sUserEnabled = enabled;
        }

        public static void SetColorHover(object cardTag, Vector3 color)
        {
            sColorTag = cardTag;
            sColorValue = color;
            sColorStamp = System.Environment.TickCount;
        }

        // Called by the color cards when a card was clicked: the game is about
        // to commit that color, the preview must not interfere for a moment.
        public static void NotifyCommitted()
        {
            int until = System.Environment.TickCount + ClickQuietMs;
            sQuietUntil = until == 0 ? 1 : until;
        }

        private static void ResetState()
        {
            sHoverTag = null;
            sShownTag = null;
            sTrackedObject = null;
            sBusy = false;
            sGameBusySince = 0;
            sQuietUntil = 0;
            sRow = -1;
            sVars = null;
            sOriginals = null;
            sOriginalColors = null;
            sColorTag = null;
        }

        // ------------------------------------------------------------
        // HOVER DETECTION
        // ------------------------------------------------------------

        private static void OnBinTick(
            WindowBase sender,
            UIEventArgs eventArgs)
        {
            CASCompositorController controller = sController;

            if (controller == null ||
                GridField == null)
            {
                return;
            }

            try
            {
                if (sBusy &&
                    System.Environment.TickCount - sBusySince >
                        BusyTimeoutMs)
                {
                    sBusy = false;
                }

                ItemGrid grid =
                    GridField.GetValue(controller) as ItemGrid;

                // Another object / wall / floor was picked in the meantime:
                // whatever was previewed belonged to the previous one.
                FollowDesignObject(controller);

                // The eye button: its state is read right here, on the same
                // tick that uses it.
                try
                {
                    BetterCASTColorCards.PollToggle();
                }
                catch
                {
                }

                // Switched off with the eye button.
                if (!sUserEnabled)
                {
                    sHoverTag = null;
                    Restore(controller);
                    return;
                }

                // An item is being dragged: make sure nothing stays previewed.
                if (controller.Dragging)
                {
                    sHoverTag = null;
                    Restore(controller);
                    return;
                }

                object tag = null;
                bool isColor = false;

                // 1. A pattern thumbnail (only while the grid is shown).
                if (grid != null &&
                    IsShown(grid))
                {
                    Vector2 cursor =
                        UIManager.GetCursorPosition();

                    if (IsInside(
                            UIManager.GetWindowFromPoint(cursor),
                            grid))
                    {
                        ItemGridCellItem cell =
                            new ItemGridCellItem();

                        if (grid.GetItemAtPosition(
                                grid.ScreenToWindow(cursor),
                                ref cell))
                        {
                            tag = cell.mTag;
                        }
                    }
                }

                // 2. A harmony card / saved color of the intro screen.
                if (tag == null)
                {
                    // Ask the color cards right now (same tick, same cursor)
                    // instead of relying on their last report.
                    try
                    {
                        BetterCASTColorCards.PollHover();
                    }
                    catch
                    {
                    }

                    object colorTag = CurrentColorTag();

                    if (colorTag != null)
                    {
                        tag = colorTag;
                        isColor = true;
                    }
                }

                if (tag == null)
                {
                    sHoverTag = null;
                    Restore(controller);
                    return;
                }

                int now = System.Environment.TickCount;

                if (!object.ReferenceEquals(tag, sHoverTag))
                {
                    sHoverTag = tag;
                    sHoverSince = now;
                    return;
                }

                if (object.ReferenceEquals(tag, sShownTag) &&
                    (!isColor || SameColor(sColorValue, sShownColor)))
                {
                    return;
                }

                if (now - sHoverSince < DwellMs)
                {
                    return;
                }

                if (IsQuiet())
                {
                    return;
                }

                Show(controller, tag, isColor);
            }
            catch
            {
                // Never let the preview break the CASt.
                sBusy = false;
            }
        }

        // The color card reported by the color cards, if the report is fresh.
        private static object CurrentColorTag()
        {
            if (sColorTag == null)
            {
                return null;
            }

            if (System.Environment.TickCount - sColorStamp >
                ColorReportMaxAgeMs)
            {
                sColorTag = null;
                return null;
            }

            return sColorTag;
        }

        private static bool IsQuiet()
        {
            if (sQuietUntil == 0)
            {
                return false;
            }

            if (sQuietUntil - System.Environment.TickCount > 0)
            {
                return true;
            }

            sQuietUntil = 0;
            return false;
        }

        // ------------------------------------------------------------
        // SHOW / RESTORE
        // ------------------------------------------------------------

        private static void Show(
            CASCompositorController controller,
            object tag,
            bool isColor)
        {
            if (sBusy ||
                GameIsBusy(controller))
            {
                // Try again on the next tick.
                return;
            }

            if (SelectedPatternProperty == null ||
                SkewerWindowsField == null ||
                PresetField == null ||
                DesignObjectField == null)
            {
                return;
            }

            Complate complate = null;

            if (!isColor)
            {
                complate = Resolve(tag);

                if (complate == null)
                {
                    return;
                }
            }

            int row =
                (int)SelectedPatternProperty.GetValue(
                    controller,
                    null
                );

            Window[] windows =
                SkewerWindowsField.GetValue(controller)
                as Window[];

            if (row < 0 ||
                windows == null ||
                row >= windows.Length ||
                windows[row] == null)
            {
                return;
            }

            List<string> vars =
                windows[row].Tag as List<string>;

            if (vars == null ||
                vars.Count == 0)
            {
                return;
            }

            object boxedPreset =
                PresetField.GetValue(controller);

            if (!(boxedPreset is KeyValuePair<string,
                    Dictionary<string, Complate>>))
            {
                return;
            }

            KeyValuePair<string, Dictionary<string, Complate>> preset =
                (KeyValuePair<string, Dictionary<string, Complate>>)
                    boxedPreset;

            if (preset.Value == null)
            {
                return;
            }

            // The row changed while something was previewed: go back first.
            if (sShownTag != null &&
                sRow != row &&
                !PresetChanged(preset.Value))
            {
                Restore(controller);
                return;
            }

            // First preview of this series, or the user clicked in the
            // meantime: remember what the game holds right now.
            if (sShownTag == null ||
                PresetChanged(preset.Value))
            {
                sOriginals = new Dictionary<string, Complate>();
                sOriginalColors =
                    new Dictionary<string, Sims3.SimIFace.Color[]>();

                for (int i = 0; i < vars.Count; i++)
                {
                    Complate original;

                    if (preset.Value.TryGetValue(vars[i], out original))
                    {
                        sOriginals[vars[i]] = original;
                        sOriginalColors[vars[i]] = ColorsOf(original);
                    }
                }

                sVars = vars.ToArray();
                sRow = row;
            }

            // What each variable of the row shows during the preview.
            Dictionary<string, Complate> shown =
                new Dictionary<string, Complate>();

            if (isColor)
            {
                if (!BuildColorPreview(
                        controller,
                        preset.Value,
                        vars,
                        sColorValue,
                        shown))
                {
                    return;
                }

                sShownColor = sColorValue;
            }
            else
            {
                for (int i = 0; i < vars.Count; i++)
                {
                    Complate copy =
                        complate.Clone() as Complate;

                    if (copy == null)
                    {
                        return;
                    }

                    shown[vars[i]] = copy;
                }
            }

            // Temporary copy of the preset: the game's own stays untouched.
            Dictionary<string, Complate> temporary =
                new Dictionary<string, Complate>(
                    preset.Value,
                    preset.Value.Comparer);

            foreach (KeyValuePair<string, Complate> pair in shown)
            {
                temporary[pair.Key] = pair.Value;
            }

            Queue(
                controller,
                new KeyValuePair<string, Dictionary<string, Complate>>(
                    preset.Key,
                    temporary),
                tag);
        }

        // The same color change the game makes when a color is applied, but
        // on COPIES of the complates (the game's own are never modified).
        private static bool BuildColorPreview(
            CASCompositorController controller,
            Dictionary<string, Complate> real,
            List<string> vars,
            Vector3 color,
            Dictionary<string, Complate> result)
        {
            // "Link colors" mode (the chain button, usually on by default):
            // the game shifts every color of the pattern together, starting
            // from the colors it saved. In that mode the selected color index
            // is -1, so it must be checked first.
            bool link = IsLinkMode(controller);

            int colorIndex = -1;

            if (!link)
            {
                if (SelectedColorProperty == null)
                {
                    return false;
                }

                colorIndex =
                    (int)SelectedColorProperty.GetValue(controller, null);

                // One color expected but none selected: a click would do
                // nothing, so does the preview.
                if (colorIndex < 0)
                {
                    return false;
                }
            }

            Vector3[] shifted = null;

            if (link)
            {
                Vector3[] saved =
                    SavedColorsField != null
                        ? SavedColorsField.GetValue(controller)
                            as Vector3[]
                        : null;

                if (saved == null)
                {
                    return false;
                }

                shifted =
                    CompositorUtil.ColorShifter.HsvShiftColor(
                        saved,
                        color,
                        0);

                if (shifted == null)
                {
                    return false;
                }
            }

            for (int i = 0; i < vars.Count; i++)
            {
                Complate source;

                if (!real.TryGetValue(vars[i], out source) ||
                    source == null)
                {
                    return false;
                }

                Complate copy =
                    source.Clone() as Complate;

                if (copy == null)
                {
                    return false;
                }

                if (shifted == null)
                {
                    CompositorUtil.SetPatternColor(
                        copy,
                        colorIndex,
                        color);
                }
                else
                {
                    Sims3.SimIFace.Color[] colors =
                        CompositorUtil.GetPatternColors(copy);

                    int count =
                        colors != null
                            ? Math.Min(colors.Length, shifted.Length)
                            : 0;

                    for (int c = 0; c < count; c++)
                    {
                        CompositorUtil.SetPatternColor(
                            copy,
                            c,
                            shifted[c]);
                    }
                }

                result[vars[i]] = copy;
            }

            return true;
        }

        private static bool IsLinkMode(CASCompositorController controller)
        {
            if (LinkButtonField == null)
            {
                return false;
            }

            Button button =
                LinkButtonField.GetValue(controller) as Button;

            return button != null && button.Selected;
        }

        private static void Restore(CASCompositorController controller)
        {
            if (sShownTag == null ||
                sBusy ||
                controller == null ||
                PresetField == null ||
                IsQuiet() ||
                GameIsBusy(controller))
            {
                return;
            }

            object boxedPreset =
                PresetField.GetValue(controller);

            if (!(boxedPreset is KeyValuePair<string,
                    Dictionary<string, Complate>>))
            {
                sShownTag = null;
                return;
            }

            KeyValuePair<string, Dictionary<string, Complate>> preset =
                (KeyValuePair<string, Dictionary<string, Complate>>)
                    boxedPreset;

            // The user clicked (the game's preset changed): keep what the
            // game applied.
            if (preset.Value == null ||
                PresetChanged(preset.Value))
            {
                sShownTag = null;
                sOriginals = null;
                sOriginalColors = null;
                return;
            }

            // The game's real preset, exactly as it is.
            Queue(controller, preset, null);
        }

        // True when the game's preset no longer holds what it held when the
        // preview started: a pattern click replaces the Complate objects, a
        // color click changes the colors of the same objects in place.
        private static bool PresetChanged(
            Dictionary<string, Complate> current)
        {
            if (sOriginals == null ||
                sVars == null)
            {
                return false;
            }

            for (int i = 0; i < sVars.Length; i++)
            {
                Complate original;
                Complate latest;

                if (sOriginals.TryGetValue(sVars[i], out original) &&
                    current.TryGetValue(sVars[i], out latest))
                {
                    if (!object.ReferenceEquals(original, latest))
                    {
                        return true;
                    }

                    Sims3.SimIFace.Color[] before;

                    if (sOriginalColors != null &&
                        sOriginalColors.TryGetValue(sVars[i], out before) &&
                        ColorsDiffer(before, ColorsOf(latest)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // ------------------------------------------------------------
        // THE GAME'S OWN APPLY TASK
        // ------------------------------------------------------------

        // Applies the given preset to the object being edited, exactly the
        // way the game does it for that kind of target, but without the
        // undo bookkeeping and without touching the game's own preset.
        private static void Queue(
            CASCompositorController controller,
            KeyValuePair<string, Dictionary<string, Complate>> preset,
            object tag)
        {
            object designObject =
                DesignObjectField.GetValue(controller);

            if (designObject == null)
            {
                return;
            }

            // Objects: the live path of the design tool. Everything else
            // (walls, floors...): the game's own finalize task.
            bool live =
                designObject.GetType().Name ==
                    "ObjectCompositorObject";

            MethodInfo finalize = null;
            FunctionWithParam direct = null;

            if (!live)
            {
                finalize =
                    designObject.GetType().GetMethod(
                        "FinalizePresetTask",
                        InstanceFlags
                    );

                if (finalize == null)
                {
                    return;
                }

                // A real delegate on the game's own method: the task then
                // runs it directly, the way the game queues it itself.
                try
                {
                    direct =
                        (FunctionWithParam)Delegate.CreateDelegate(
                            typeof(FunctionWithParam),
                            designObject,
                            finalize
                        );
                }
                catch
                {
                    direct = null;
                }
            }

            object boxed =
                preset;

            sShownTag = tag;
            sBusy = true;
            sBusySince = System.Environment.TickCount;

            FunctionWithParam work =
                delegate (object argument)
                {
                    try
                    {
                        if (CASCompositorController.Instance != null)
                        {
                            if (live)
                            {
                                CompositorUtil.ApplyPreset(preset);
                            }
                            else if (direct != null)
                            {
                                direct(argument);
                            }
                            else
                            {
                                finalize.Invoke(
                                    designObject,
                                    new object[] { argument }
                                );
                            }
                        }
                    }
                    catch
                    {
                    }

                    sBusy = false;
                };

            Simulator.AddObject(
                new OneShotFunctionWithParams(work, boxed)
            );
        }

        // ------------------------------------------------------------
        // HELPERS
        // ------------------------------------------------------------

        // True while the game itself is still applying something (a click,
        // a color change...): the preview waits for it. Never for long: if
        // the game still reports "busy" after GameBusyGraceMs, the preview
        // goes ahead anyway so it cannot stay silent forever.
        private static bool GameIsBusy(CASCompositorController controller)
        {
            bool busy = false;

            try
            {
                busy = ReadGameBusy(controller);
            }
            catch
            {
            }

            if (!busy)
            {
                sGameBusySince = 0;
                return false;
            }

            int now = System.Environment.TickCount;

            if (sGameBusySince == 0)
            {
                sGameBusySince = now == 0 ? 1 : now;
                return true;
            }

            return now - sGameBusySince < GameBusyGraceMs;
        }

        private static bool ReadGameBusy(
            CASCompositorController controller)
        {
            if (ObjectDesigner.IsProcessing)
            {
                return true;
            }

            object designObject =
                DesignObjectField != null
                    ? DesignObjectField.GetValue(controller)
                    : null;

            if (designObject == null)
            {
                return false;
            }

            PropertyInfo processing =
                designObject.GetType().GetProperty(
                    "Processing",
                    InstanceFlags
                );

            if (processing == null)
            {
                return false;
            }

            object value =
                processing.GetValue(designObject, null);

            return value is bool && (bool)value;
        }

        // The preview state belongs to one design object. When the game
        // switches to another one, forget it (nothing to restore there).
        private static void FollowDesignObject(
            CASCompositorController controller)
        {
            if (DesignObjectField == null)
            {
                return;
            }

            object current =
                DesignObjectField.GetValue(controller);

            if (object.ReferenceEquals(current, sTrackedObject))
            {
                return;
            }

            sTrackedObject = current;
            sShownTag = null;
            sHoverTag = null;
            sOriginals = null;
            sOriginalColors = null;
            sVars = null;
            sRow = -1;
        }

        // Same resolution rules as the game / Better CASt use on a click.
        private static Complate Resolve(object tag)
        {
            Complate result = null;

            BetterCASTLazyPatternTag lazy =
                tag as BetterCASTLazyPatternTag;

            if (lazy != null)
            {
                Complate source =
                    Pattern.GetComplate(lazy.Key);

                if (source != null)
                {
                    result = source.Clone() as Complate;
                }
            }
            else
            {
                Complate direct =
                    tag as Complate;

                if (direct != null)
                {
                    result = direct.Clone() as Complate;
                }
                else if (tag is ResourceKey)
                {
                    ColorInfo colorInfo =
                        ColorInfo.FromResourceKey((ResourceKey)tag);

                    if (colorInfo != null)
                    {
                        result =
                            CompositorUtil.ColorInfoToComplate(colorInfo);
                    }
                }
            }

            if (result == null ||
                result.Key == ResourceKey.kInvalidResourceKey)
            {
                return null;
            }

            return result;
        }

        // The colors a complate currently holds (null if it has none).
        private static Sims3.SimIFace.Color[] ColorsOf(Complate complate)
        {
            try
            {
                return complate != null
                    ? CompositorUtil.GetPatternColors(complate)
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool ColorsDiffer(
            Sims3.SimIFace.Color[] a,
            Sims3.SimIFace.Color[] b)
        {
            // Unknown on one side: treat as unchanged.
            if (a == null ||
                b == null)
            {
                return false;
            }

            if (a.Length != b.Length)
            {
                return true;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (!SameColor(
                        CompositorUtil.ColorToVector3(a[i]),
                        CompositorUtil.ColorToVector3(b[i])))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SameColor(Vector3 a, Vector3 b)
        {
            return
                Math.Abs(a.x - b.x) < 0.004f &&
                Math.Abs(a.y - b.y) < 0.004f &&
                Math.Abs(a.z - b.z) < 0.004f;
        }

        private static bool IsShown(WindowBase window)
        {
            int guard = 0;

            while (window != null && guard++ < 32)
            {
                if (!window.Visible)
                {
                    return false;
                }

                window = window.Parent;
            }

            return true;
        }

        private static bool IsInside(
            WindowBase window,
            WindowBase container)
        {
            int guard = 0;

            while (window != null && guard++ < 32)
            {
                if (object.ReferenceEquals(window, container))
                {
                    return true;
                }

                window = window.Parent;
            }

            return false;
        }
    }
}