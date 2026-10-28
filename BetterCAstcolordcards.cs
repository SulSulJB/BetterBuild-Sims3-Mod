using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.UI;
using Sims3.UI.CAS;

namespace BetterBuildByJojobou
{
    /*
     * Better CASt - the color cards of the intro screen.
     *
     * The cards already exist in the CASt layout (they are drawn by the
     * "Better CASt" windows of BetterBuildByJojobouOverridesClassic.package,
     * inside the intro decoration window). They appear and disappear WITH the
     * intro screen: this class never shows or hides anything.
     *
     * What it does, and only this:
     *   - HARMONIES : colors the 10 cards from the current color of the
     *                 game's color picker; a click applies the color.
     *   - MES COULEURS : the pipette saves the current color in one of 6
     *                 slots (kept in CAStColors.txt); a click applies it.
     *   - PREVIEW   : resting the mouse on a harmony card or a saved color
     *                 shows that color on the object / wall / floor (the work
     *                 is done by BetterCASTPatternHoverPreview; this class only
     *                 tells it which card is hovered).
     *   - EYE BUTTON : turns every preview on or off (kept in
     *                 CAStOptions.txt).
     *   - APPLY TO ALL : two buttons copy the color (or the texture) of the
     *                 selected square onto all the other squares of the object.
     *   - texts and tooltips are taken from the language strings; if they are
     *     not installed, the tooltips fall back to English.
     *
     * Integration: ONE line, called from Better CASt's own control binding
     * (never from a patched game method):
     *
     *     BetterCASTColorCards.Attach(controller, materialsBin);
     *
     * Every entry point is wrapped in try/catch: if anything goes wrong the
     * CASt keeps working, the cards simply stay as drawn in the layout.
     */
    public static class BetterCASTColorCards
    {
        // Set to false to turn the feature off completely.
        private static readonly bool Enabled = true;

        // Layout control IDs (must match the CASt layout).
        private const uint CastDecorsID = 0x0012F020U;
        private const uint HarmonyFirstID = 0x0BC5A100U;
        private const int HarmonyCount = 10;
        private const uint PipetteID = 0x0BC5A110U;
        private const uint SlotFirstID = 0x0BC5A120U;
        private const int SlotCount = 6;
        private const uint LabelTopID = 0x0BC5A130U;
        private const uint LabelBottomID = 0x0BC5A131U;
        private const uint HelpTopID = 0x0BC5A150U;
        private const uint HelpBottomID = 0x0BC5A151U;

        // Third section: copy the selected part's color / texture onto all
        // the other parts of the object.
        private const uint LabelApplyID = 0x0BC5A132U;
        private const uint HelpApplyID = 0x0BC5A152U;
        private const uint ApplyColorID = 0x0BC5A180U;
        private const uint ApplyTextureID = 0x0BC5A181U;
        private const uint ApplyDividerID = 0x0BC5A182U;

        // The same two buttons again, in the pattern browser, above the page
        // buttons (they live inside the browser's panel, so they come and
        // go with it).
        private const uint ApplyColorBrowserID = 0x0BC5A183U;
        private const uint ApplyTextureBrowserID = 0x0BC5A184U;

        // The eye button: ONE toggle button. Its first four images are the
        // open eye (preview on), its last four the crossed eye (preview
        // off), shown through the button's "selected" state.
        private const uint PreviewButtonID = 0x0BC5A190U;

        private const string KeyPrefix = "Jojobou/BetterBuild/CAStHarmony:";

        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public;

        private static readonly FieldInfo ColorPickerField =
            typeof(CASCompositorController).GetField(
                "mMaterialsColorPicker", InstanceFlags);

        private static readonly FieldInfo PickerColorField =
            typeof(CASCompositorController).GetField(
                "mColorPickerColor", InstanceFlags);

        private static readonly MethodInfo OnColorChangedMethod =
            typeof(CASCompositorController).GetMethod(
                "OnColorChanged",
                InstanceFlags,
                null,
                new Type[] { typeof(Vector3) },
                null);

        private static readonly MethodInfo OnColorFinalizedMethod =
            typeof(CASCompositorController).GetMethod(
                "OnColorFinalized",
                InstanceFlags,
                null,
                Type.EmptyTypes,
                null);

        // The game's own private members used by "apply to all". Each one
        // may be null (another game version): the feature then stays off.
        private static readonly FieldInfo SkewerWindowsField =
            typeof(CASCompositorController).GetField(
                "mMaterialsSkewerPatternWindow", InstanceFlags);

        private static readonly FieldInfo PresetField =
            typeof(CASCompositorController).GetField(
                "mCurrentPreset", InstanceFlags);

        private static readonly FieldInfo DesignObjectField =
            typeof(CASCompositorController).GetField(
                "mCurrentDesignObject", InstanceFlags);

        private static readonly PropertyInfo SelectedPatternProperty =
            typeof(CASCompositorController).GetProperty(
                "MaterialSkewerSelectedPattern", InstanceFlags);

        // ApplyMaterial(texture | color | colors, row, updateThumbnail,
        // finalize): what the game calls when a texture or a color is
        // dragged onto one of the squares on the left.
        private static readonly MethodInfo ApplyTextureMethod =
            typeof(CASCompositorController).GetMethod(
                "ApplyMaterial",
                InstanceFlags,
                null,
                new Type[]
                {
                    typeof(Sims3.Metadata.Complate),
                    typeof(int),
                    typeof(bool),
                    typeof(bool)
                },
                null);

        private static readonly MethodInfo ApplySingleColorMethod =
            typeof(CASCompositorController).GetMethod(
                "ApplyMaterial",
                InstanceFlags,
                null,
                new Type[]
                {
                    typeof(Sims3.SimIFace.Color),
                    typeof(int),
                    typeof(bool),
                    typeof(bool)
                },
                null);

        private static readonly MethodInfo ApplyColorSetMethod =
            typeof(CASCompositorController).GetMethod(
                "ApplyMaterial",
                InstanceFlags,
                null,
                new Type[]
                {
                    typeof(Sims3.SimIFace.Color[]),
                    typeof(int),
                    typeof(bool),
                    typeof(bool)
                },
                null);

        private static readonly MethodInfo SelectCurrentMaterialMethod =
            typeof(CASCompositorController).GetMethod(
                "SelectCurrentMaterialInBin",
                InstanceFlags,
                null,
                Type.EmptyTypes,
                null);

        private static readonly MethodInfo RepopulateColorsMethod =
            typeof(CASCompositorController).GetMethod(
                "RepopulateColors",
                InstanceFlags,
                null,
                new Type[] { typeof(bool) },
                null);

        // ------------------------------------------------------------
        // STATE
        // ------------------------------------------------------------

        private static CASCompositorController sController;
        private static ColorPicker sPicker;
        private static WindowBase sBin;
        private static WindowBase sDecors;

        private static readonly Button[] sHarmonyButtons =
            new Button[HarmonyCount];

        private static readonly Button[] sSlotButtons =
            new Button[SlotCount];

        private static Button sPipette;

        private static Button sApplyColorButton;
        private static Button sApplyTextureButton;
        private static Button sApplyColorBrowserButton;
        private static Button sApplyTextureBrowserButton;
        private static Button sPreviewButton;
        private static bool sPreviewEnabled = true;
        private static bool sOptionsLoaded;
        private static int sPreviewTickCount;

        private static bool sApplyAllRunning;
        private static int sApplyAllToken;

        private static readonly Vector3[] sHarmonyColors =
            new Vector3[HarmonyCount];

        // Newest first. Loaded from / saved to CAStColors.txt.
        private static readonly List<Vector3> sSavedColors =
            new List<Vector3>();

        private static bool sSavedLoaded;

        private static Vector3 sLastBase = new Vector3(-1f, -1f, -1f);
        private static int sLastTick;
        private static int sRefreshToken;

        // One stable object per card (harmonies first, then the saved
        // colors): the hover preview recognizes "the same card" by it.
        private static readonly object[] sCardTags = CreateCardTags();

        private static object[] CreateCardTags()
        {
            object[] tags = new object[HarmonyCount + SlotCount];

            for (int i = 0; i < tags.Length; i++)
            {
                tags[i] = new object();
            }

            return tags;
        }

        // ------------------------------------------------------------
        // PUBLIC ENTRY POINT
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
                Button first =
                    materialsBin.GetChildByID(
                        HarmonyFirstID, true) as Button;

                if (first == null)
                {
                    return;
                }

                bool same =
                    object.ReferenceEquals(sBin, materialsBin) &&
                    object.ReferenceEquals(sController, controller) &&
                    object.ReferenceEquals(sHarmonyButtons[0], first);

                if (!same)
                {
                    Unbind();
                    BindAll(controller, materialsBin);
                }

                BindPicker(controller);

                LoadSavedColors();
                UpdateSlots();

                sLastBase = new Vector3(-1f, -1f, -1f);
                Refresh();

                // The picker may be filled a little after the CASt opens:
                // a few short, bounded refreshes make sure the harmonies
                // are right on the very first opening too.
                ScheduleRefresh(++sRefreshToken, 12);
            }
            catch
            {
            }
        }


        // ------------------------------------------------------------
        // BINDING
        // ------------------------------------------------------------

        private static void BindAll(
            CASCompositorController controller,
            WindowBase materialsBin)
        {
            sController = controller;
            sBin = materialsBin;

            sDecors =
                materialsBin.GetChildByID(CastDecorsID, true);

            for (int i = 0; i < HarmonyCount; i++)
            {
                Button b =
                    materialsBin.GetChildByID(
                        HarmonyFirstID + (uint)i, true) as Button;

                sHarmonyButtons[i] = b;

                if (b != null)
                {
                    b.Click -= OnHarmonyClick;
                    b.Click += OnHarmonyClick;
                    b.MouseMove -= OnCardMouseMove;
                    b.MouseMove += OnCardMouseMove;
                }
            }

            for (int i = 0; i < SlotCount; i++)
            {
                Button b =
                    materialsBin.GetChildByID(
                        SlotFirstID + (uint)i, true) as Button;

                sSlotButtons[i] = b;

                if (b != null)
                {
                    b.Click -= OnSlotClick;
                    b.Click += OnSlotClick;
                    b.MouseMove -= OnCardMouseMove;
                    b.MouseMove += OnCardMouseMove;
                }
            }

            sPipette =
                materialsBin.GetChildByID(PipetteID, true) as Button;

            if (sPipette != null)
            {
                sPipette.Click -= OnPipetteClick;
                sPipette.Click += OnPipetteClick;

                sPipette.TooltipText = TipText("Pipette");
            }

            SetText(LabelTopID, "MyColors", true);
            SetText(LabelBottomID, "Suggestions", true);
            SetHelp(LabelTopID, HelpTopID, "HelpMyColors");
            SetHelp(LabelBottomID, HelpBottomID, "HelpHarmonies");

            SetText(LabelApplyID, "ApplyAllTitle", true);
            SetHelp(LabelApplyID, HelpApplyID, "HelpApplyAll");
            BindApplyAll(materialsBin);
            BindPreviewToggle(materialsBin);

            if (sDecors != null)
            {
                sDecors.VisibilityChange -= OnDecorsVisibilityChange;
                sDecors.VisibilityChange += OnDecorsVisibilityChange;
            }

            materialsBin.Tick -= OnBinTick;
            materialsBin.Tick += OnBinTick;
        }

        private static void Unbind()
        {
            UnbindPicker();

            try
            {
                if (sBin != null)
                {
                    sBin.Tick -= OnBinTick;
                }

                if (sDecors != null)
                {
                    sDecors.VisibilityChange -= OnDecorsVisibilityChange;
                }
            }
            catch
            {
            }

            for (int i = 0; i < HarmonyCount; i++)
            {
                Button b = sHarmonyButtons[i];

                if (b != null)
                {
                    try
                    {
                        b.Click -= OnHarmonyClick;
                        b.MouseMove -= OnCardMouseMove;
                    }
                    catch
                    {
                    }
                }

                sHarmonyButtons[i] = null;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                Button b = sSlotButtons[i];

                if (b != null)
                {
                    try
                    {
                        b.Click -= OnSlotClick;
                        b.MouseMove -= OnCardMouseMove;
                    }
                    catch
                    {
                    }
                }

                sSlotButtons[i] = null;
            }

            if (sPipette != null)
            {
                try
                {
                    sPipette.Click -= OnPipetteClick;
                }
                catch
                {
                }

                sPipette = null;
            }

            UnbindApplyAll();
            UnbindPreviewToggle();

            sBin = null;
            sDecors = null;
            sController = null;
        }

        private static void BindPicker(
            CASCompositorController controller)
        {
            if (object.ReferenceEquals(sController, controller) &&
                sPicker != null)
            {
                return;
            }

            UnbindPicker();

            sController = controller;

            if (ColorPickerField != null)
            {
                sPicker =
                    ColorPickerField.GetValue(controller)
                    as ColorPicker;
            }

            if (sPicker != null)
            {
                sPicker.ColorChanged -= OnPickerColorChanged;
                sPicker.ColorChanged += OnPickerColorChanged;
            }
        }

        private static void UnbindPicker()
        {
            if (sPicker != null)
            {
                try
                {
                    sPicker.ColorChanged -= OnPickerColorChanged;
                }
                catch
                {
                }
            }

            sPicker = null;
        }

        private static void ScheduleRefresh(int token, int attemptsLeft)
        {
            if (attemptsLeft <= 0)
            {
                return;
            }

            try
            {
                Simulator.AddObject(
                    new OneShotFunctionTask(
                        delegate
                        {
                            try
                            {
                                if (token != sRefreshToken ||
                                    sController == null)
                                {
                                    return;
                                }

                                if (sPicker == null)
                                {
                                    BindPicker(sController);
                                }

                                RefreshIfChanged();
                            }
                            catch
                            {
                            }

                            ScheduleRefresh(token, attemptsLeft - 1);
                        },
                        StopWatch.TickStyles.Milliseconds,
                        250f
                    )
                );
            }
            catch
            {
            }
        }

        // ------------------------------------------------------------
        // EVENTS
        // ------------------------------------------------------------

        // The intro screen is shown again (back from the pattern browser).
        private static void OnDecorsVisibilityChange(
            WindowBase sender,
            UIVisibilityChangeEventArgs eventArgs)
        {
            try
            {
                if (sender != null &&
                    sender.Visible)
                {
                    sLastBase = new Vector3(-1f, -1f, -1f);
                    UpdateSlots();
                    RefreshIfChanged();
                }
            }
            catch
            {
            }
        }

        // Cheap: the hover report runs on every tick (one cursor lookup), the
        // refresh at most every 120 ms, and only while the cards are shown.
        private static void OnBinTick(
            WindowBase sender,
            UIEventArgs eventArgs)
        {
            ReportHover();

            PollToggle();

            try
            {
                int now = System.Environment.TickCount;

                if (now >= sLastTick &&
                    now - sLastTick < 120)
                {
                    return;
                }

                sLastTick = now;

                if (!IsShown(sDecors))
                {
                    return;
                }

                RefreshIfChanged();
            }
            catch
            {
            }
        }

        private static void OnCardMouseMove(
            WindowBase sender,
            UIMouseEventArgs eventArgs)
        {
            try
            {
                RefreshIfChanged();
            }
            catch
            {
            }
        }

        // ------------------------------------------------------------
        // HOVER REPORT (the preview itself is BetterCASTPatternHoverPreview)
        // ------------------------------------------------------------

        // Called by the hover preview on its own tick, right before it looks
        // for a hovered card: the report is then made with the very cursor
        // position the preview is working with, whatever the order in which
        // the game calls the two tick handlers.
        public static void PollHover()
        {
            ReportHover();
        }

        // Tells the hover preview which card the mouse is on, and the color
        // a click on it would apply. Nothing hovered (or the intro screen
        // hidden): it is told so, which ends any preview.
        private static void ReportHover()
        {
            try
            {
                int card = -1;

                // The cards are on screen only on the intro screen.
                if (IsShown(sHarmonyButtons[0]))
                {
                    card = CardUnderCursor();
                }

                if (card >= 0 &&
                    card < HarmonyCount)
                {
                    BetterCASTPatternHoverPreview.SetColorHover(
                        sCardTags[card],
                        sHarmonyColors[card]);
                }
                else if (card >= HarmonyCount &&
                         card - HarmonyCount < sSavedColors.Count)
                {
                    BetterCASTPatternHoverPreview.SetColorHover(
                        sCardTags[card],
                        sSavedColors[card - HarmonyCount]);
                }
                else
                {
                    BetterCASTPatternHoverPreview.SetColorHover(
                        null,
                        new Vector3(0f, 0f, 0f));
                }
            }
            catch
            {
            }
        }

        // The card under the mouse: 0..9 = harmonies, 10..15 = saved colors,
        // -1 = none. The mouse position is compared with each card's own
        // rectangle, so it does not depend on which window the game reports
        // under the cursor.
        private static int CardUnderCursor()
        {
            Vector2 cursor = UIManager.GetCursorPosition();

            for (int i = 0; i < HarmonyCount + SlotCount; i++)
            {
                Button button =
                    i < HarmonyCount
                        ? sHarmonyButtons[i]
                        : sSlotButtons[i - HarmonyCount];

                if (button == null ||
                    !button.Visible)
                {
                    continue;
                }

                Vector2 local = button.ScreenToWindow(cursor);
                Rect area = button.Area;

                if (local.x >= 0f &&
                    local.y >= 0f &&
                    local.x < area.Width &&
                    local.y < area.Height)
                {
                    return i;
                }
            }

            return -1;
        }

        // A click commits the color through the game: the preview steps back.
        private static void NotifyClick()
        {
            try
            {
                BetterCASTPatternHoverPreview.NotifyCommitted();
            }
            catch
            {
            }
        }

        // The game's wheel / sliders / hex field moved the color.
        private static void OnPickerColorChanged(Vector3 color)
        {
            try
            {
                if (IsShown(sDecors))
                {
                    Recompute(color);
                }
            }
            catch
            {
            }
        }

        private static void OnHarmonyClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            try
            {
                if (sender == null)
                {
                    return;
                }

                int index = (int)(sender.ID - HarmonyFirstID);

                if (index < 0 ||
                    index >= HarmonyCount)
                {
                    return;
                }

                Audio.StartSound("ui_tertiary_button");

                NotifyClick();

                ApplyColor(sHarmonyColors[index]);
            }
            catch
            {
            }
        }

        private static void OnSlotClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            try
            {
                if (sender == null)
                {
                    return;
                }

                int index = (int)(sender.ID - SlotFirstID);

                if (index < 0 ||
                    index >= sSavedColors.Count)
                {
                    return;
                }

                Audio.StartSound("ui_tertiary_button");

                NotifyClick();

                ApplyColor(sSavedColors[index]);
            }
            catch
            {
            }
        }

        // The pipette saves the current color in "Mes couleurs".
        private static void OnPipetteClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            try
            {
                Vector3 color;

                if (!TryGetCurrentColor(out color))
                {
                    return;
                }

                for (int i = sSavedColors.Count - 1; i >= 0; i--)
                {
                    if (SameColor(sSavedColors[i], color))
                    {
                        sSavedColors.RemoveAt(i);
                    }
                }

                sSavedColors.Insert(0, color);

                while (sSavedColors.Count > SlotCount)
                {
                    sSavedColors.RemoveAt(sSavedColors.Count - 1);
                }

                Audio.StartSound("ui_primary_button");


                UpdateSlots();
                SaveSavedColors();
            }
            catch
            {
            }
        }

        // ------------------------------------------------------------
        // APPLYING A COLOR (the same calls the game's own picker makes)
        // ------------------------------------------------------------

        private static void ApplyColor(Vector3 color)
        {
            if (sController == null)
            {
                return;
            }

            // Make the wheel and sliders follow.
            if (sPicker != null)
            {
                sPicker.Color = color;
            }

            // The game's own picker callbacks: they ignore the call when
            // another selection is in progress, so this is safe.
            if (OnColorChangedMethod != null)
            {
                OnColorChangedMethod.Invoke(
                    sController,
                    new object[] { color });
            }

            if (OnColorFinalizedMethod != null)
            {
                OnColorFinalizedMethod.Invoke(
                    sController,
                    null);
            }

            Recompute(color);
        }

        private static bool TryGetCurrentColor(out Vector3 color)
        {
            color = new Vector3(0.5f, 0.5f, 0.5f);

            try
            {
                if (sPicker != null)
                {
                    color = sPicker.Color;
                    return true;
                }

                if (sController != null &&
                    PickerColorField != null)
                {
                    color =
                        (Vector3)PickerColorField.GetValue(sController);

                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        // ------------------------------------------------------------
        // APPLY TO ALL PARTS ("Meme couleur" / "Meme texture")
        // ------------------------------------------------------------
        //
        // The squares on the left of the CASt are the parts of the object.
        // Dragging the color (or the texture) of the selected square onto
        // another square copies it there. The two buttons do the same for
        // every other square at once, with the game's own call
        // (ApplyMaterial), one square after the other, each time the game
        // is idle again. The buttons always stay shown: with a single
        // square (a wall, a floor) a click simply does nothing.

        private static bool ApplyAllAvailable()
        {
            return
                SkewerWindowsField != null &&
                PresetField != null &&
                DesignObjectField != null &&
                SelectedPatternProperty != null &&
                ApplyTextureMethod != null &&
                ApplySingleColorMethod != null &&
                ApplyColorSetMethod != null;
        }

        private static void BindApplyAll(WindowBase materialsBin)
        {
            try
            {
                sApplyColorButton =
                    materialsBin.GetChildByID(
                        ApplyColorID, true) as Button;

                sApplyTextureButton =
                    materialsBin.GetChildByID(
                        ApplyTextureID, true) as Button;

                sApplyColorBrowserButton =
                    materialsBin.GetChildByID(
                        ApplyColorBrowserID, true) as Button;

                sApplyTextureBrowserButton =
                    materialsBin.GetChildByID(
                        ApplyTextureBrowserID, true) as Button;

                BindApplyButton(sApplyColorButton, false);
                BindApplyButton(sApplyTextureButton, true);
                BindApplyButton(sApplyColorBrowserButton, false);
                BindApplyButton(sApplyTextureBrowserButton, true);
            }
            catch
            {
            }
        }

        private static void BindApplyButton(Button button, bool texture)
        {
            if (button == null)
            {
                return;
            }

            try
            {
                if (texture)
                {
                    button.Click -= OnApplyTextureClick;
                    button.Click += OnApplyTextureClick;
                }
                else
                {
                    button.Click -= OnApplyColorClick;
                    button.Click += OnApplyColorClick;
                }

                SetCaption(
                    button,
                    texture ? "ApplyTextureAll" : "ApplyColorAll");

                button.TooltipText =
                    TipText(
                        texture
                            ? "ApplyTextureAllTip"
                            : "ApplyColorAllTip");
            }
            catch
            {
            }
        }

        private static void UnbindApplyButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            try
            {
                button.Click -= OnApplyColorClick;
                button.Click -= OnApplyTextureClick;
            }
            catch
            {
            }
        }

        private static void UnbindApplyAll()
        {
            // Any run in progress belongs to the CASt that is closing.
            sApplyAllToken++;
            sApplyAllRunning = false;

            UnbindApplyButton(sApplyColorButton);
            UnbindApplyButton(sApplyTextureButton);
            UnbindApplyButton(sApplyColorBrowserButton);
            UnbindApplyButton(sApplyTextureBrowserButton);

            sApplyColorButton = null;
            sApplyTextureButton = null;
            sApplyColorBrowserButton = null;
            sApplyTextureBrowserButton = null;
        }

        private static void SetCaption(Button button, string key)
        {
            string text = LocalizeOrNull(key);

            if (button != null &&
                text != null)
            {
                button.Caption = text;
            }
        }

        // The squares that really hold something: each one carries the list
        // of the object's variables it controls (empty = unused square).
        private static List<int> GetParts(
            CASCompositorController controller)
        {
            List<int> parts = new List<int>();

            if (controller == null ||
                SkewerWindowsField == null)
            {
                return parts;
            }

            Window[] windows =
                SkewerWindowsField.GetValue(controller) as Window[];

            if (windows == null)
            {
                return parts;
            }

            for (int i = 0; i < windows.Length; i++)
            {
                Window window = windows[i];

                if (window == null)
                {
                    continue;
                }

                List<string> variables = window.Tag as List<string>;

                if (variables != null &&
                    variables.Count > 0)
                {
                    parts.Add(i);
                }
            }

            return parts;
        }

        // The texture (with its colors) currently set on a part.
        private static bool TryGetPartComplate(
            CASCompositorController controller,
            int part,
            out Sims3.Metadata.Complate complate)
        {
            complate = null;

            if (PresetField == null ||
                SkewerWindowsField == null)
            {
                return false;
            }

            object boxed = PresetField.GetValue(controller);

            if (!(boxed is KeyValuePair<string,
                    Dictionary<string, Sims3.Metadata.Complate>>))
            {
                return false;
            }

            Dictionary<string, Sims3.Metadata.Complate> preset =
                ((KeyValuePair<string,
                    Dictionary<string, Sims3.Metadata.Complate>>)boxed)
                    .Value;

            Window[] windows =
                SkewerWindowsField.GetValue(controller) as Window[];

            if (preset == null ||
                windows == null ||
                part < 0 ||
                part >= windows.Length ||
                windows[part] == null)
            {
                return false;
            }

            List<string> variables = windows[part].Tag as List<string>;

            if (variables == null ||
                variables.Count == 0)
            {
                return false;
            }

            return
                preset.TryGetValue(variables[0], out complate) &&
                complate != null;
        }

        // The game is still working on the previous change (the same
        // condition under which it ignores a drop on a square).
        private static bool ApplyIsBusy(
            CASCompositorController controller)
        {
            try
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
                        InstanceFlags);

                if (processing == null)
                {
                    return false;
                }

                object value =
                    processing.GetValue(designObject, null);

                return value is bool && (bool)value;
            }
            catch
            {
                return false;
            }
        }

        private static void OnApplyColorClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            StartApplyAll(false);
        }

        private static void OnApplyTextureClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            StartApplyAll(true);
        }

        private static void StartApplyAll(bool texture)
        {
            try
            {
                CASCompositorController controller = sController;

                if (controller == null ||
                    sApplyAllRunning ||
                    !ApplyAllAvailable() ||
                    DesignObjectField.GetValue(controller) == null)
                {
                    return;
                }

                int selected =
                    (int)SelectedPatternProperty.GetValue(
                        controller, null);

                List<int> parts = GetParts(controller);

                if (parts.Count < 2)
                {
                    return;
                }

                // No square highlighted (the game just rebuilt them, after
                // an undo for instance): the first square is the source.
                if (!parts.Contains(selected))
                {
                    selected = parts[0];
                }

                Sims3.Metadata.Complate source;

                if (!TryGetPartComplate(controller, selected, out source))
                {
                    return;
                }

                // Copies taken now: later squares must receive what the
                // selected square holds at this moment.
                Sims3.Metadata.Complate textureCopy = null;
                Sims3.SimIFace.Color[] colors = null;

                if (texture)
                {
                    textureCopy = source.Clone() as Sims3.Metadata.Complate;

                    if (textureCopy == null)
                    {
                        return;
                    }
                }
                else
                {
                    Sims3.SimIFace.Color[] current =
                        CompositorUtil.GetPatternColors(source);

                    if (current == null ||
                        current.Length == 0)
                    {
                        return;
                    }

                    colors = (Sims3.SimIFace.Color[])current.Clone();
                }

                List<int> targets = new List<int>();

                for (int i = 0; i < parts.Count; i++)
                {
                    if (parts[i] != selected)
                    {
                        targets.Add(parts[i]);
                    }
                }

                Audio.StartSound("ui_style_plop");

                // A click commits through the game: the hover preview
                // steps back.
                NotifyClick();

                sApplyAllRunning = true;

                ScheduleApplyStep(
                    ++sApplyAllToken,
                    controller,
                    texture,
                    textureCopy,
                    colors,
                    targets,
                    0,
                    0);
            }
            catch
            {
                sApplyAllRunning = false;
            }
        }

        private static void ScheduleApplyStep(
            int token,
            CASCompositorController controller,
            bool texture,
            Sims3.Metadata.Complate textureCopy,
            Sims3.SimIFace.Color[] colors,
            List<int> targets,
            int index,
            int waited)
        {
            try
            {
                Simulator.AddObject(
                    new OneShotFunctionTask(
                        delegate
                        {
                            try
                            {
                                RunApplyStep(
                                    token,
                                    controller,
                                    texture,
                                    textureCopy,
                                    colors,
                                    targets,
                                    index,
                                    waited);
                            }
                            catch
                            {
                                if (token == sApplyAllToken)
                                {
                                    sApplyAllRunning = false;
                                }
                            }
                        },
                        StopWatch.TickStyles.Milliseconds,
                        120f
                    )
                );
            }
            catch
            {
                if (token == sApplyAllToken)
                {
                    sApplyAllRunning = false;
                }
            }
        }

        // One square per step. The CASt was closed or reopened meanwhile:
        // the run just ends.
        private static void RunApplyStep(
            int token,
            CASCompositorController controller,
            bool texture,
            Sims3.Metadata.Complate textureCopy,
            Sims3.SimIFace.Color[] colors,
            List<int> targets,
            int index,
            int waited)
        {
            if (token != sApplyAllToken)
            {
                return;
            }

            if (!object.ReferenceEquals(controller, sController) ||
                DesignObjectField.GetValue(controller) == null)
            {
                sApplyAllRunning = false;
                return;
            }

            // The game is still busy with the previous square: wait
            // (never for ever, about 5 seconds).
            if (ApplyIsBusy(controller))
            {
                if (waited >= 40)
                {
                    sApplyAllRunning = false;
                    return;
                }

                ScheduleApplyStep(
                    token, controller, texture, textureCopy, colors,
                    targets, index, waited + 1);

                return;
            }

            if (index >= targets.Count)
            {
                FinishApplyAll(controller, texture);
                return;
            }

            ApplyToPart(
                controller, targets[index], texture, textureCopy, colors);

            ScheduleApplyStep(
                token, controller, texture, textureCopy, colors,
                targets, index + 1, 0);
        }

        private static void ApplyToPart(
            CASCompositorController controller,
            int part,
            bool texture,
            Sims3.Metadata.Complate textureCopy,
            Sims3.SimIFace.Color[] colors)
        {
            // updateThumbnail = true, finalize = true: what a drop on the
            // square does.
            if (texture)
            {
                ApplyTextureMethod.Invoke(
                    controller,
                    new object[] { textureCopy, part, true, true });

                return;
            }

            // One color (a plain material): the game's color drop.
            if (colors.Length <= 1)
            {
                ApplySingleColorMethod.Invoke(
                    controller,
                    new object[] { colors[0], part, true, true });

                return;
            }

            // A pattern with several colors: the whole palette, like
            // dragging the palette onto the square.
            Sims3.SimIFace.Color[] palette = new Sims3.SimIFace.Color[4];

            for (int i = 0; i < palette.Length; i++)
            {
                palette[i] =
                    colors[i < colors.Length ? i : colors.Length - 1];
            }

            ApplyColorSetMethod.Invoke(
                controller,
                new object[] { palette, part, true, true });
        }

        // What the game does at the end of a drop on a square.
        private static void FinishApplyAll(
            CASCompositorController controller,
            bool texture)
        {
            sApplyAllRunning = false;

            try
            {
                if (SelectCurrentMaterialMethod != null)
                {
                    SelectCurrentMaterialMethod.Invoke(controller, null);
                }
            }
            catch
            {
            }

            try
            {
                if (RepopulateColorsMethod != null)
                {
                    RepopulateColorsMethod.Invoke(
                        controller,
                        new object[] { texture });
                }
            }
            catch
            {
            }

            try
            {
                sLastBase = new Vector3(-1f, -1f, -1f);
                RefreshIfChanged();
            }
            catch
            {
            }
        }

        // ------------------------------------------------------------
        // PREVIEW ON/OFF (the eye button)
        // ------------------------------------------------------------
        //
        // One switch for every preview of the CASt (patterns of the grid,
        // harmony cards, saved colors). The choice is kept in
        // <Documents>/Electronic Arts/The Sims 3/BetterBuild/CAStOptions.txt

        private static void BindPreviewToggle(WindowBase materialsBin)
        {
            try
            {
                LoadOptions();

                sPreviewButton = null;
                FindPreviewButton(materialsBin);
                ShowPreviewToggle();
            }
            catch
            {
            }
        }

        // Finds the eye: by its ID first, then (if the ID lookup gives
        // nothing) by what it is: the 18x18 toggle button that is a direct
        // child of the window holding the intro decors.
        private static void FindPreviewButton(WindowBase materialsBin)
        {
            Button found = null;

            if (materialsBin != null)
            {
                found =
                    materialsBin.GetChildByID(
                        PreviewButtonID, true) as Button;
            }

            if (found == null)
            {
                found = FindPreviewButtonByShape();
            }

            if (found != null)
            {
                sPreviewButton = found;
            }
        }

        private static bool LooksLikeEye(Button button)
        {
            if (button == null ||
                button.ButtonType != Button.ButtonTypes.Toggle)
            {
                return false;
            }

            Rect area = button.Area;

            return
                System.Math.Abs(area.Width - 18f) < 1.5f &&
                System.Math.Abs(area.Height - 18f) < 1.5f;
        }

        private static Button FindPreviewButtonByShape()
        {
            try
            {
                WindowBase holder =
                    sDecors != null ? sDecors.Parent : null;

                if (holder == null)
                {
                    return null;
                }

                for (uint i = 0; i < 64; i++)
                {
                    WindowBase child = holder.GetChildByIndex(i);

                    if (child == null)
                    {
                        break;
                    }

                    Button button = child as Button;

                    if (LooksLikeEye(button))
                    {
                        return button;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static void UnbindPreviewToggle()
        {
            sPreviewButton = null;
        }

        // Picture and tooltip follow the real switch (blue open eye = on,
        // grey crossed eye = off), and the previews are told.
        private static void ShowPreviewToggle()
        {
            BetterCASTPatternHoverPreview.SetUserEnabled(
                sPreviewEnabled);

            Button button = sPreviewButton;

            if (button == null)
            {
                return;
            }

            button.Enabled = true;
            button.Visible = true;
            button.Selected = !sPreviewEnabled;

            button.TooltipText = PreviewTip();
        }

        private static string PreviewTip()
        {
            return
                TipText(
                    sPreviewEnabled
                        ? "PreviewOnTip"
                        : "PreviewOffTip");
        }

        // The button is a real toggle: the game flips its "selected" state
        // (grey) on every click. That state IS the switch: no click event is
        // needed, it is simply read on every tick (called by the hover
        // preview's tick and by the cards' tick, cheap and harmless to run
        // twice).
        public static void PollToggle()
        {
            try
            {
                sPreviewTickCount++;

                Button button = sPreviewButton;

                if (button == null)
                {
                    // Not found at bind time: look again, about twice a
                    // second, by ID / by shape / under the mouse.
                    if (sPreviewTickCount % 15 != 0)
                    {
                        return;
                    }

                    FindPreviewButton(sBin);

                    if (sPreviewButton == null)
                    {
                        Button under =
                            UIManager.GetWindowFromPoint(
                                UIManager.GetCursorPosition()) as Button;

                        if (LooksLikeEye(under))
                        {
                            sPreviewButton = under;
                        }
                    }

                    if (sPreviewButton != null)
                    {
                        ShowPreviewToggle();
                    }

                    return;
                }

                bool wanted = !button.Selected;

                if (wanted != sPreviewEnabled)
                {
                    sPreviewEnabled = wanted;

                    BetterCASTPatternHoverPreview.SetUserEnabled(
                        sPreviewEnabled);

                    button.TooltipText = PreviewTip();
                    SaveOptions();
                }
                else if (sPreviewTickCount % 30 == 0 &&
                         string.IsNullOrEmpty(button.TooltipText))
                {
                    button.TooltipText = PreviewTip();
                }
            }
            catch
            {
            }
        }

        private static string GetOptionsPath()
        {
            string colors = GetSavePath();

            if (colors == null)
            {
                return null;
            }

            return System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(colors),
                "CAStOptions.txt");
        }

        // One line: PreviewOnHover=1 (or 0). Missing file = on.
        private static void LoadOptions()
        {
            if (sOptionsLoaded)
            {
                return;
            }

            sOptionsLoaded = true;

            try
            {
                string path = GetOptionsPath();

                if (path == null ||
                    !System.IO.File.Exists(path))
                {
                    return;
                }

                string[] lines = System.IO.File.ReadAllLines(path);

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();

                    if (line.StartsWith("PreviewOnHover="))
                    {
                        sPreviewEnabled =
                            line.Substring(
                                "PreviewOnHover=".Length).Trim() != "0";
                    }
                }
            }
            catch
            {
            }
        }

        private static void SaveOptions()
        {
            try
            {
                string path = GetOptionsPath();

                if (path == null)
                {
                    return;
                }

                System.IO.Directory.CreateDirectory(
                    System.IO.Path.GetDirectoryName(path));

                System.IO.File.WriteAllLines(
                    path,
                    new string[]
                    {
                        "PreviewOnHover=" + (sPreviewEnabled ? "1" : "0")
                    });
            }
            catch
            {
            }
        }

        // ------------------------------------------------------------
        // DISPLAY
        // ------------------------------------------------------------

        private static void Refresh()
        {
            Vector3 color;

            if (TryGetCurrentColor(out color))
            {
                Recompute(color);
            }
        }

        private static void RefreshIfChanged()
        {
            Vector3 color;

            if (TryGetCurrentColor(out color) &&
                !SameColor(color, sLastBase))
            {
                Recompute(color);
            }
        }

        private static void Recompute(Vector3 baseColor)
        {
            sLastBase = baseColor;

            // Harmonies are computed in OKLCH (perceptual lightness / chroma /
            // hue), so the suggestions have the same visual weight as the
            // current color instead of being fully saturated primaries.
            float bl, bc, bh;
            RgbToOklch(baseColor, out bl, out bc, out bh);

            // A grey base has no useful hue or chroma: give the suggestions
            // a soft default so they are still pleasant.
            float chroma = bc < 0.035f ? 0.085f : bc;

            // Keep rotated colors in a comfortable lightness range.
            float light = bl < 0.52f ? 0.52f : (bl > 0.80f ? 0.80f : bl);

            float soft = Math.Min(chroma * 0.85f, 0.15f);

            sHarmonyColors[0] = OklchToRgb(light, soft, bh + 180f);
            sHarmonyColors[1] = OklchToRgb(light, soft, bh - 30f);
            sHarmonyColors[2] = OklchToRgb(light, soft, bh + 30f);
            sHarmonyColors[3] = OklchToRgb(light, soft, bh + 120f);
            sHarmonyColors[4] = OklchToRgb(light, soft, bh + 240f);

            // Tints and shades of the current color: light tints are less
            // chromatic, dark shades keep a little more colour.
            float[] shadeL = { 0.93f, 0.81f, 0.66f, 0.50f, 0.34f };
            float[] shadeC = { 0.25f, 0.60f, 0.95f, 0.85f, 0.60f };

            for (int i = 0; i < 5; i++)
            {
                sHarmonyColors[5 + i] =
                    OklchToRgb(
                        shadeL[i],
                        bc * shadeC[i],
                        bh);
            }

            string[] keys =
            {
                "Complementary",
                "Analogous",
                "Analogous",
                "Triad",
                "Triad",
                "Shade",
                "Shade",
                "Shade",
                "Shade",
                "Shade"
            };

            for (int i = 0; i < HarmonyCount; i++)
            {
                Button button = sHarmonyButtons[i];

                if (button == null)
                {
                    continue;
                }

                button.ShadeColor = ToUiColor(sHarmonyColors[i]);

                button.TooltipText =
                    TipText(keys[i]) +
                    "  " +
                    ToHex(sHarmonyColors[i]);
            }
        }

        private static void UpdateSlots()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                Button button = sSlotButtons[i];

                if (button == null)
                {
                    continue;
                }

                if (i < sSavedColors.Count)
                {
                    button.ShadeColor = ToUiColor(sSavedColors[i]);

                    button.TooltipText =
                        TipText("SavedColor") +
                        "  " +
                        ToHex(sSavedColors[i]);
                }
                else
                {
                    // Empty slot: the soft blue-grey drawn in the layout.
                    button.ShadeColor = new Color(150, 190, 225, 255);

                    button.TooltipText = TipText("EmptySlot");
                }
            }
        }

        // ------------------------------------------------------------
        // TEXTS
        // ------------------------------------------------------------

        // The translated text, or null if the language strings are not there
        // (then the text drawn in the layout stays as it is).
        private static string LocalizeOrNull(string key)
        {
            try
            {
                string full = KeyPrefix + key;

                string text =
                    Localization.LocalizeString(full, new object[0]);

                if (string.IsNullOrEmpty(text) ||
                    text == full ||
                    text.StartsWith(KeyPrefix))
                {
                    return null;
                }

                return text;
            }
            catch
            {
                return null;
            }
        }

        private static void SetText(
            uint id,
            string key,
            bool upperCase)
        {
            try
            {
                Text label =
                    sBin.GetChildByID(id, true) as Text;

                string text = LocalizeOrNull(key);

                if (label == null ||
                    text == null)
                {
                    return;
                }

                label.Caption =
                    upperCase
                        ? text.ToUpperInvariant()
                        : text;
            }
            catch
            {
            }
        }

        // The title label sits across the whole row, "?" included, so it
        // used to swallow the mouse before the "?" could show its tooltip.
        // The label no longer reacts to the mouse, the "?" is moved on top,
        // and the title carries the same help text as a safety net.
        private static void SetHelp(
            uint labelId,
            uint helpId,
            string key)
        {
            SetTip(helpId, key);

            try
            {
                WindowBase label = sBin.GetChildByID(labelId, true);

                if (label != null)
                {
                    label.IgnoreMouse = true;
                    label.TooltipText = TipText(key);
                }
            }
            catch
            {
            }

            try
            {
                WindowBase help = sBin.GetChildByID(helpId, true);

                if (help != null)
                {
                    help.MoveToFront();
                }
            }
            catch
            {
            }
        }

        private static void SetTip(uint id, string key)
        {
            try
            {
                WindowBase window = sBin.GetChildByID(id, true);

                if (window != null)
                {
                    window.TooltipText = TipText(key);
                }
            }
            catch
            {
            }
        }

        // A tooltip is always shown: the translated text when the language
        // strings are installed, English otherwise. (Seeing English in a
        // French game means the language strings package is not installed.)
        private static string TipText(string key)
        {
            string text = LocalizeOrNull(key);

            return text != null
                ? text
                : EnglishTip(key);
        }

        private static string EnglishTip(string key)
        {
            switch (key)
            {
                case "Complementary":
                    return "Complementary color";

                case "Analogous":
                    return "Analogous color";

                case "Triad":
                    return "Triadic color";

                case "Shade":
                    return "Shade";

                case "Pipette":
                    return "Eyedropper: save the current color in a slot";

                case "SavedColor":
                    return "Saved color (click to apply)";

                case "EmptySlot":
                    return "Empty slot";

                case "HelpMyColors":
                    return
                        "Click the eyedropper to save the current color " +
                        "in a slot. Click a saved color to apply it; " +
                        "hover it to preview it on the object.";

                case "ApplyColorAll":
                    return "Color";

                case "ApplyTextureAll":
                    return "Texture";

                case "ApplyColorAllTip":
                    return
                        "Applies the color of the selected part to all " +
                        "the other parts of the object (like dragging " +
                        "the color onto each square).";

                case "ApplyTextureAllTip":
                    return
                        "Applies the texture of the selected part, with " +
                        "its colors, to all the other parts of the " +
                        "object (like dragging the texture onto each " +
                        "square).";

                case "PreviewOnTip":
                    return
                        "Preview on hover: ON. Click to turn it off.";

                case "PreviewOffTip":
                    return
                        "Preview on hover: OFF. Click to turn it on.";

                case "HelpApplyAll":
                    return
                        "Copies the color or the texture of the selected " +
                        "part (the square on the left) to all the other " +
                        "parts of the object, instead of dragging it " +
                        "onto each square.";

                case "HelpHarmonies":
                    return
                        "Colors that go well with the current one. " +
                        "Top row: complementary, two analogous and two " +
                        "triadic colors. Bottom row: shades of the same " +
                        "color. Hover to preview, click to apply.";
            }

            return key;
        }

        // ------------------------------------------------------------
        // SAVED COLORS ON DISK
        // ------------------------------------------------------------
        //
        // One "RRGGBB" per line, newest first, in
        // <Documents>/Electronic Arts/The Sims 3/BetterBuild/CAStColors.txt
        // If the file cannot be read or written, the colors simply last
        // for the current game session.

        private static string GetSavePath()
        {
            string documents =
                System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.Personal);

            if (string.IsNullOrEmpty(documents))
            {
                return null;
            }

            return System.IO.Path.Combine(
                System.IO.Path.Combine(
                    System.IO.Path.Combine(
                        System.IO.Path.Combine(
                            documents,
                            "Electronic Arts"),
                        "The Sims 3"),
                    "BetterBuild"),
                "CAStColors.txt");
        }

        private static void LoadSavedColors()
        {
            if (sSavedLoaded)
            {
                return;
            }

            sSavedLoaded = true;

            try
            {
                string path = GetSavePath();

                if (path == null ||
                    !System.IO.File.Exists(path))
                {
                    return;
                }

                string[] lines = System.IO.File.ReadAllLines(path);

                sSavedColors.Clear();

                for (int i = 0;
                     i < lines.Length &&
                     sSavedColors.Count < SlotCount;
                     i++)
                {
                    string line = lines[i].Trim();

                    if (line.Length != 6)
                    {
                        continue;
                    }

                    int value;

                    if (!int.TryParse(
                            line,
                            System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out value))
                    {
                        continue;
                    }

                    sSavedColors.Add(
                        new Vector3(
                            ((value >> 16) & 0xFF) / 255f,
                            ((value >> 8) & 0xFF) / 255f,
                            (value & 0xFF) / 255f));
                }
            }
            catch
            {
            }
        }

        private static void SaveSavedColors()
        {
            try
            {
                string path = GetSavePath();

                if (path == null)
                {
                    return;
                }

                System.IO.Directory.CreateDirectory(
                    System.IO.Path.GetDirectoryName(path));

                string[] lines = new string[sSavedColors.Count];

                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i] = ToHex(sSavedColors[i]).Substring(1);
                }

                System.IO.File.WriteAllLines(path, lines);
            }
            catch
            {
            }
        }

        // ------------------------------------------------------------
        // WINDOW HELPERS
        // ------------------------------------------------------------

        // True when the window and all its parents are visible.
        private static bool IsShown(WindowBase window)
        {
            int guard = 0;

            if (window == null)
            {
                return false;
            }

            while (window != null &&
                   guard++ < 32)
            {
                if (!window.Visible)
                {
                    return false;
                }

                window = window.Parent;
            }

            return true;
        }

        // ------------------------------------------------------------
        // COLOR MATH: OKLCH (Bjorn Ottosson's OKLab) <-> sRGB
        // ------------------------------------------------------------

        private static float SrgbToLinear(float v)
        {
            return v <= 0.04045f
                ? v / 12.92f
                : (float)Math.Pow((v + 0.055f) / 1.055f, 2.4);
        }

        private static float LinearToSrgb(float v)
        {
            if (v <= 0f) { return 0f; }
            if (v >= 1f) { return 1f; }

            return v <= 0.0031308f
                ? 12.92f * v
                : 1.055f * (float)Math.Pow(v, 1.0 / 2.4) - 0.055f;
        }

        private static float Cbrt(float v)
        {
            return v < 0f
                ? -(float)Math.Pow(-v, 1.0 / 3.0)
                : (float)Math.Pow(v, 1.0 / 3.0);
        }

        private static void RgbToOklch(
            Vector3 rgb,
            out float lightness,
            out float chroma,
            out float hue)
        {
            float r = SrgbToLinear(Clamp01(rgb.x));
            float g = SrgbToLinear(Clamp01(rgb.y));
            float b = SrgbToLinear(Clamp01(rgb.z));

            float l = Cbrt(0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b);
            float m = Cbrt(0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b);
            float s = Cbrt(0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b);

            lightness = 0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s;

            float a = 1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s;
            float bb = 0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s;

            chroma = (float)Math.Sqrt(a * a + bb * bb);

            hue = chroma < 0.0001f
                ? 0f
                : (float)(Math.Atan2(bb, a) * 180.0 / Math.PI);

            if (hue < 0f)
            {
                hue += 360f;
            }
        }

        // Reduces chroma until the color fits in sRGB, then clamps.
        private static Vector3 OklchToRgb(
            float lightness,
            float chroma,
            float hue)
        {
            double rad = hue * Math.PI / 180.0;
            float cosH = (float)Math.Cos(rad);
            float sinH = (float)Math.Sin(rad);

            if (chroma < 0f)
            {
                chroma = 0f;
            }

            float r = 0f, g = 0f, b = 0f;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                float a = chroma * cosH;
                float bb = chroma * sinH;

                float l_ = lightness + 0.3963377774f * a + 0.2158037573f * bb;
                float m_ = lightness - 0.1055613458f * a - 0.0638541728f * bb;
                float s_ = lightness - 0.0894841775f * a - 1.2914855480f * bb;

                float l = l_ * l_ * l_;
                float m = m_ * m_ * m_;
                float s = s_ * s_ * s_;

                r = 4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s;
                g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
                b = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;

                if (r >= -0.001f && r <= 1.001f &&
                    g >= -0.001f && g <= 1.001f &&
                    b >= -0.001f && b <= 1.001f)
                {
                    break;
                }

                chroma *= 0.92f;
            }

            return new Vector3(
                LinearToSrgb(r),
                LinearToSrgb(g),
                LinearToSrgb(b));
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) { return 0f; }
            if (value > 1f) { return 1f; }
            return value;
        }

        private static bool SameColor(Vector3 a, Vector3 b)
        {
            return
                Math.Abs(a.x - b.x) < 0.004f &&
                Math.Abs(a.y - b.y) < 0.004f &&
                Math.Abs(a.z - b.z) < 0.004f;
        }

        private static Color ToUiColor(Vector3 c)
        {
            return new Color(
                (int)(Clamp01(c.x) * 255f + 0.5f),
                (int)(Clamp01(c.y) * 255f + 0.5f),
                (int)(Clamp01(c.z) * 255f + 0.5f),
                255);
        }

        private static string ToHex(Vector3 c)
        {
            return string.Format(
                "#{0:X2}{1:X2}{2:X2}",
                (int)(Clamp01(c.x) * 255f + 0.5f),
                (int)(Clamp01(c.y) * 255f + 0.5f),
                (int)(Clamp01(c.z) * 255f + 0.5f));
        }
    }
}