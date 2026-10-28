using MonoPatcherLib;
using Sims3;
using Sims3.Gameplay.Utilities;
using Sims3.Metadata;
using Sims3.SimIFace;
using Sims3.SimIFace.BuildBuy;
using Sims3.SimIFace.CustomContent;
using Sims3.UI;
using Sims3.UI.CAS;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BetterBuildByJojobou
{
    // ============================================================
    // BETTER CAST - CASCompositorController
    // ============================================================
    [TypePatch(typeof(CASCompositorController))]
    public class BetterCASTPatch
    {
        /*
         * Prevent the vanilla catalog from populating automatically.
         *
         * Better CASt now handles:
         * - opening without loading the catalog
         * - category-based loading
         * - 16-pattern pagination
         */
        private void PopulateMaterialsBinGrid(bool changeTemp)
        {
            Simulator.AddObject(
                new OneShotFunctionTask(
                    delegate
                    {
                        BetterCASTHelpers.FinishNoPatternLoad();
                    }
                )
            );

            return;
        }


        /*
         * Category change.
         */
        private void OnMaterialsComboBoxChanged(
            WindowBase sender,
            UISelectionChangeEventArgs eventArgs)
        {
            CASCompositorController controller =
                (CASCompositorController)(object)this;

            BetterCASTHelpers.OnCategoryChanged(
                controller,
                eventArgs
            );
        }


        /*
         * Custom Content filter.
         *
         * Replace the vanilla behavior so Better CASt can rebuild
         * its lightweight index and return to page 1.
         */
        private void OnFilterButtonClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            CASCompositorController controller =
                (CASCompositorController)(object)this;

            BetterCASTHelpers.OnCustomContentFilterChanged(
                controller,
                eventArgs
            );
        }


        /*
         * Better CASt pattern cells do not need to contain
         * a preloaded Complate.
         *
         * The pattern is resolved only when the user clicks it.
         */
        private void MaterialsWindowGridItemClicked(
            WindowBase sender,
            ItemGridCellClickEvent clickedItem)
        {
            CASCompositorController controller =
                (CASCompositorController)(object)this;

            BetterCASTHelpers.OnMaterialGridItemClicked(
                controller,
                clickedItem
            );
        }
    }


    // ============================================================
    // BETTER CAST - PATTERN CACHE
    // ============================================================
    [TypePatch(typeof(Pattern))]
    public class BetterCASTPatternPatch
    {
        public static bool CacheBuilt
        {
            get
            {
                /*
                 * Prevent UpdateMaterialSkewerTask() from automatically
                 * building the Pattern cache when CASt opens.
                 *
                 * The real sCacheBuilt remains untouched.
                 * Pattern.GetPatternCategories() can therefore build
                 * metadata only when the user requests patterns.
                 */
                return true;
            }
        }
    }


    // ============================================================
    // BETTER CAST - LIGHTWEIGHT PATTERN CELL TAG
    // ============================================================
    public sealed class BetterCASTLazyPatternTag
    {
        public readonly string Category;
        public readonly ResourceKey Key;

        public BetterCASTLazyPatternTag(
            string category,
            ResourceKey key)
        {
            Category = category;
            Key = key;
        }
    }


    // ============================================================
    // BETTER CAST - HELPERS
    // ============================================================
    public static class BetterCASTHelpers
    {
        // --------------------------------------------------------
        // CONFIGURATION
        // --------------------------------------------------------

        private const int PatternsPerPage = BetterCASTV2CastPatterns.PatternsPerPage;

        private const uint ShowPatternsButtonID =
            0x0012F001U;

        private const uint PaletteImageID =
            0x0012F002U;

        // Intro-only decorative IMAG: CASTDecors (0x30D00F358A1F6816)
        private const uint CastDecorsID =
            0x0012F020U;

        private const uint PreviousPageButtonID =
            0x0012F003U;

        private const uint NextPageButtonID =
            0x0012F004U;

        private const uint PageTextID =
            0x0012F005U;

        // Small arrow button of the pattern browser: back to the intro
        // screen (palette + "Choisir une texture"). Shown in the browser
        // only.
        private const uint BackToStartButtonID =
            0x0BC5A160U;


        private static string Localize(string key)
        {
            return Localization.LocalizeString(
                "Jojobou/BetterBuild/BetterCAST:" + key,
                new object[0]
            );
        }


        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public;


        private static readonly ResourceKey MaterialsGridItemLayoutKey =
            ResourceKey.CreateUILayoutKey(
                "MaterialsGridItem",
                0U
            );


        // ========================================================
        // LIGHTWEIGHT INDEX ENTRY
        // ========================================================

        private struct PatternEntry
        {
            public string Category;
            public ResourceKey Key;
            public bool IsFavorite;
            public bool IsSavedPreset;

            public PatternEntry(
                string category,
                ResourceKey key)
            {
                Category = category;
                Key = key;
                IsFavorite = false;
                IsSavedPreset = false;
            }

            public static PatternEntry Favorite(string category, ResourceKey key)
            {
                PatternEntry result = new PatternEntry(category, key);
                result.IsFavorite = true;
                return result;
            }

            public static PatternEntry SavedPreset(string category, ResourceKey key)
            {
                PatternEntry result = new PatternEntry(category, key);
                result.IsSavedPreset = true;
                return result;
            }
        }


        // ========================================================
        // BETTER CAST STATE
        // ========================================================

        private static CASCompositorController
            sBoundController = null;

        // Watch the material bin itself, but wait until EA has finished
        // closing before resetting browser state. UI can temporarily hide
        // this window without actually closing CASt (e.g. transitions).
        private static WindowBase sWatchedMaterialsBin = null;

        private static Button
            sShowPatternsButton = null;

        private static Button
            sPreviousPageButton = null;

        private static Button
            sNextPageButton = null;

        private static Button
            sBackToStartButton = null;

        private static Text
            sPageText = null;

        private static bool
            sBrowserOpen = false;

        private static int
            sCurrentPage = 0;

        private static int
            sTotalPages = 0;

        /*
         * Every category/page/filter change increments this value.
         *
         * An older task stops when it sees a different generation.
         */
        private static int
            sLoadGeneration = 0;

        /*
         * IMPORTANT:
         * this list contains only:
         * - category
         * - ResourceKey
         * - CC type
         *
         * NO thumbnail,
         * NO Compositor,
         * NO Complate.
         */
        private static List<PatternEntry>
            sPatternEntries =
                new List<PatternEntry>();


        /*
         * Lightweight cache by category + CC filter state.
         * No thumbnail, Compositor, or Complate is stored here.
         */
        private static readonly Dictionary<string, List<PatternEntry>>
            sPatternIndexCache =
                new Dictionary<string, List<PatternEntry>>();

        /*
         * Cache EA/CC content type by ResourceKey.
         */
        private static readonly Dictionary<ResourceKey, ResourceKeyContentCategory>
            sCustomTypeCache =
                new Dictionary<ResourceKey, ResourceKeyContentCategory>();

        /*
         * Used only for Miscellaneous.
         */
        private static string[]
            sPatternCategoriesCache = null;


        // ========================================================
        // VANILLA PRIVATE FIELDS
        // ========================================================

        private static readonly FieldInfo MaterialComboBoxField =
            typeof(CASCompositorController).GetField(
                "mMaterialComboBox",
                InstanceFlags
            );

        private static readonly FieldInfo MaterialsGridField =
            typeof(CASCompositorController).GetField(
                "mMaterialsWindowGrid",
                InstanceFlags
            );

        private static readonly FieldInfo CurrentFilterField =
            typeof(CASCompositorController).GetField(
                "mCurrentFilter",
                InstanceFlags
            );

        private static readonly FieldInfo FilterButtonField =
            typeof(CASCompositorController).GetField(
                "mFilterButton",
                InstanceFlags
            );


        private static readonly FieldInfo DebugTooltipsField =
            typeof(CASCompositorController).GetField(
                "mDebugTooltips",
                InstanceFlags
            );


        private static readonly FieldInfo MaterialsFavoriteButtonField =
            typeof(CASCompositorController).GetField(
                "mMaterialsFavoriteButton",
                InstanceFlags
            );

        private static readonly FieldInfo MaterialsSaveButtonField =
            typeof(CASCompositorController).GetField(
                "mMaterialsSaveButton",
                InstanceFlags
            );

        private static readonly FieldInfo MaterialsTrashButtonField =
            typeof(CASCompositorController).GetField(
                "mMaterialsTrashButton",
                InstanceFlags
            );

        private static readonly FieldInfo MaterialsShareButtonField =
            typeof(CASCompositorController).GetField(
                "mMaterialsShareButton",
                InstanceFlags
            );

        private static readonly FieldInfo LastSelectedTypeField =
            typeof(CASCompositorController).GetField(
                "mLastSelectedType",
                InstanceFlags
            );

        private static readonly FieldInfo UndoOnDeleteField =
            typeof(CASCompositorController).GetField(
                "mUndoOnDelete",
                InstanceFlags
            );

        private static readonly PropertyInfo MaterialSkewerSelectedPatternProperty =
            typeof(CASCompositorController).GetProperty(
                "MaterialSkewerSelectedPattern",
                InstanceFlags
            );


        // ========================================================
        // VANILLA PRIVATE METHODS
        // ========================================================

        private static readonly MethodInfo PatternFitsFilterMethod =
            typeof(CASCompositorController).GetMethod(
                "PatternFitsFilter",
                InstanceFlags
            );

        private static readonly MethodInfo AddMaterialBinGridItemMethod =
            typeof(CASCompositorController).GetMethod(
                "AddMaterialBinGridItem",
                InstanceFlags
            );


        private static readonly MethodInfo ApplyMaterialComplateMethod =
            typeof(CASCompositorController).GetMethod(
                "ApplyMaterial",
                InstanceFlags,
                null,
                new Type[]
                {
                    typeof(Complate),
                    typeof(int),
                    typeof(bool),
                    typeof(bool)
                },
                null
            );

        private static readonly MethodInfo RepopulateColorsMethod =
            typeof(CASCompositorController).GetMethod(
                "RepopulateColors",
                InstanceFlags,
                null,
                new Type[]
                {
                    typeof(bool)
                },
                null
            );


        // ========================================================
        // UI HELPERS
        // ========================================================

        private static WindowBase GetMaterialsBin(
            CASCompositorController controller)
        {
            if (controller == null)
            {
                return null;
            }

            return controller.GetChildByID(
                73728U,
                false
            );
        }


        private static ItemGrid GetGrid(
            CASCompositorController controller)
        {
            if (controller == null ||
                MaterialsGridField == null)
            {
                return null;
            }

            return
                MaterialsGridField.GetValue(controller)
                as ItemGrid;
        }


        private static ComboBox GetCombo(
            CASCompositorController controller)
        {
            if (controller == null ||
                MaterialComboBoxField == null)
            {
                return null;
            }

            return
                MaterialComboBoxField.GetValue(controller)
                as ComboBox;
        }


        private static Button GetFilterButton(
            CASCompositorController controller)
        {
            if (controller == null ||
                FilterButtonField == null)
            {
                return null;
            }

            return
                FilterButtonField.GetValue(controller)
                as Button;
        }


        private static void ResetPaginationState()
        {
            sLoadGeneration++;

            sCurrentPage = 0;
            sTotalPages = 0;

            sPatternEntries =
                new List<PatternEntry>();
        }


        // A bounded, deferred check: never disable the CASt browser merely
        // because a close button was clicked. EA may still be displaying a
        // confirmation/cancel UI, or temporarily hiding the panel.
        private static int sCloseCheckToken = 0;

        private static void VerifyCAStCloseLater(
            CASCompositorController controller,
            WindowBase materialsBin,
            int token,
            int remainingChecks)
        {
            Simulator.AddObject(new OneShotFunctionTask(
                delegate
                {
                    if (controller == null || materialsBin == null ||
                        token != sCloseCheckToken ||
                        !object.ReferenceEquals(controller, sBoundController) ||
                        !object.ReferenceEquals(materialsBin, sWatchedMaterialsBin))
                    {
                        return;
                    }

                    // Both conditions must indicate an actual close.
                    // Do not reset sBrowserOpen while CASt remains usable:
                    // BetterCASTWheelCAStOnly reads it for wheel pagination.
                    if (!materialsBin.Visible &&
                        !controller.DesignModeToolActiveOrStarting)
                    {
                        if (sBrowserOpen || sTotalPages != 0 ||
                            sPatternEntries.Count != 0)
                        {
                            sBrowserOpen = false;
                            ResetPaginationState();
                        }
                        SetPaginationVisible(false);

                        // No grid.Clear() here: the next explicit click
                        // builds a fresh page while retaining index caches.
                        ApplyBrowserVisibility(controller);
                        return;
                    }

                    // Finite, event-triggered checks: not continuous polling.
                    // Needed because EA clears DesignModeToolActiveOrStarting
                    // asynchronously after hiding the CASt materials bin.
                    if (remainingChecks > 1)
                    {
                        VerifyCAStCloseLater(controller, materialsBin,
                            token, remainingChecks - 1);
                    }
                }
            ));
        }

        private static void RequestCAStCloseVerification(
            CASCompositorController controller,
            WindowBase materialsBin)
        {
            if (controller == null || materialsBin == null)
            {
                return;
            }
            int token = ++sCloseCheckToken;
            VerifyCAStCloseLater(controller, materialsBin, token, 6);
        }

        private static void OnMaterialsBinVisibilityChange(
            WindowBase sender,
            UIVisibilityChangeEventArgs eventArgs)
        {
            if (sender == null ||
                !object.ReferenceEquals(sender, sWatchedMaterialsBin))
            {
                return;
            }


            if (sender.Visible)
            {
                // A temporary hide followed by show must not reset pages.
                // Stop any outstanding close-check chain.
                ++sCloseCheckToken;
                if (sBoundController != null && !sBrowserOpen)
                {
                    ApplyBrowserVisibility(sBoundController);
                }
                return;
            }

            RequestCAStCloseVerification(sBoundController, sender);
        }

        // Do not reset immediately on click: EA may leave CASt open.
        // Both native buttons retain their own original click handlers.
        private static void OnCAStClosing(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            RequestCAStCloseVerification(sBoundController,
                sWatchedMaterialsBin);
        }

        private static void BindBetterCASTControls(
            CASCompositorController controller,
            WindowBase materialsBin)
        {
            if (controller == null ||
                materialsBin == null)
            {
                return;
            }

            if (!object.ReferenceEquals(sWatchedMaterialsBin, materialsBin))
            {
                if (sWatchedMaterialsBin != null)
                {
                    sWatchedMaterialsBin.VisibilityChange -=
                        OnMaterialsBinVisibilityChange;
                }

                sWatchedMaterialsBin = materialsBin;
                sWatchedMaterialsBin.VisibilityChange +=
                    OnMaterialsBinVisibilityChange;
            }

            Button showButton =
                materialsBin.GetChildByID(
                    ShowPatternsButtonID,
                    true
                ) as Button;

            Button previousButton =
                materialsBin.GetChildByID(
                    PreviousPageButtonID,
                    true
                ) as Button;

            Button nextButton =
                materialsBin.GetChildByID(
                    NextPageButtonID,
                    true
                ) as Button;

            Text pageText =
                materialsBin.GetChildByID(
                    PageTextID,
                    true
                ) as Text;


            /*
             * If a new CASCompositorController exists,
             * reset Better CASt to its closed state.
             */
            if (sBoundController != controller)
            {
                if (sShowPatternsButton != null)
                {
                    sShowPatternsButton.Click -=
                        OnShowPatternsClick;
                }

                if (sPreviousPageButton != null)
                {
                    sPreviousPageButton.Click -=
                        OnPreviousPageClick;
                }

                if (sNextPageButton != null)
                {
                    sNextPageButton.Click -=
                        OnNextPageClick;
                }

                if (sBackToStartButton != null)
                {
                    sBackToStartButton.Click -=
                        OnBackToStartClick;
                }

                sBoundController =
                    controller;

                sBrowserOpen = false;

                ResetPaginationState();
            }


            sShowPatternsButton =
                showButton;

            sPreviousPageButton =
                previousButton;

            sNextPageButton =
                nextButton;

            sPageText =
                pageText;


            if (sShowPatternsButton != null)
            {
                string chooseTexture =
                    Localize("ChooseTexture");

                sShowPatternsButton.Caption =
                    chooseTexture;

                sShowPatternsButton.TooltipText =
                    chooseTexture;

                sShowPatternsButton.Click -=
                    OnShowPatternsClick;

                sShowPatternsButton.Click +=
                    OnShowPatternsClick;
            }


            if (sPreviousPageButton != null)
            {
                sPreviousPageButton.TooltipText =
                    Localize("PreviousPage");

                sPreviousPageButton.Click -=
                    OnPreviousPageClick;

                sPreviousPageButton.Click +=
                    OnPreviousPageClick;
            }


            if (sNextPageButton != null)
            {
                sNextPageButton.TooltipText =
                    Localize("NextPage");

                sNextPageButton.Click -=
                    OnNextPageClick;

                sNextPageButton.Click +=
                    OnNextPageClick;
            }

            // Back-to-intro arrow of the pattern browser.
            try
            {
                sBackToStartButton =
                    materialsBin.GetChildByID(
                        BackToStartButtonID,
                        true
                    ) as Button;

                if (sBackToStartButton != null)
                {
                    sBackToStartButton.TooltipText =
                        LocalizeBackTooltip();

                    sBackToStartButton.Click -=
                        OnBackToStartClick;

                    sBackToStartButton.Click +=
                        OnBackToStartClick;
                }
            }
            catch
            {
            }

            // V2: bind only the additional first/last page controls.
            BetterCASTV2CastPatterns.Attach(controller, materialsBin);

            // CASt-only mouse-wheel pagination: no global ItemGrid patch.
            BetterCASTWheelCAStOnly.Attach(controller);

            // Pattern preview on hover (isolated: cannot stop the CASt).
            try
            {
                BetterCASTPatternHoverPreview.Attach(
                    controller, materialsBin);
            }
            catch
            {
            }

            // Color cards of the intro screen: harmonies, saved colors.
            // Isolated as well: the CASt keeps working if this fails.
            try
            {
                BetterCASTColorCards.Attach(
                    controller, materialsBin);
            }
            catch
            {
            }
        }


        private static void SetPaginationVisible(
            bool visible)
        {
            if (sPreviousPageButton != null)
            {
                sPreviousPageButton.Visible =
                    visible;
            }

            if (sNextPageButton != null)
            {
                sNextPageButton.Visible =
                    visible;
            }

            if (sPageText != null)
            {
                sPageText.Visible =
                    visible;
            }

            BetterCASTV2CastPatterns.SetVisible(visible);
        }


        private static void UpdatePaginationControls()
        {
            bool hasPages =
                sBrowserOpen &&
                sTotalPages > 0 &&
                sPatternEntries.Count > 0;

            SetPaginationVisible(
                hasPages
            );


            if (!hasPages)
            {
                if (sPreviousPageButton != null)
                {
                    sPreviousPageButton.Enabled =
                        false;
                }

                if (sNextPageButton != null)
                {
                    sNextPageButton.Enabled =
                        false;
                }

                if (sPageText != null)
                {
                    sPageText.Caption =
                        "";
                }

                BetterCASTV2CastPatterns.UpdatePageState(false, 0, 0);
                return;
            }


            if (sCurrentPage < 0)
            {
                sCurrentPage = 0;
            }

            if (sCurrentPage >= sTotalPages)
            {
                sCurrentPage =
                    sTotalPages - 1;
            }


            if (sPreviousPageButton != null)
            {
                sPreviousPageButton.Enabled =
                    sCurrentPage > 0;
            }


            if (sNextPageButton != null)
            {
                sNextPageButton.Enabled =
                    sCurrentPage <
                    sTotalPages - 1;
            }


            if (sPageText != null)
            {
                sPageText.Caption =
                    (sCurrentPage + 1).ToString()
                    + " / "
                    + sTotalPages.ToString();
            }

            BetterCASTV2CastPatterns.UpdatePageState(true, sCurrentPage, sTotalPages);
        }


        private static void ApplyBrowserVisibility(
            CASCompositorController controller)
        {
            if (controller == null)
            {
                return;
            }


            WindowBase materialsBin =
                GetMaterialsBin(controller);

            if (materialsBin == null)
            {
                return;
            }


            ItemGrid grid =
                GetGrid(controller);

            ComboBox combo =
                GetCombo(controller);

            Button filterButton =
                GetFilterButton(controller);


            WindowBase paletteImage =
                materialsBin.GetChildByID(
                    PaletteImageID,
                    true
                );

            // The decoration is a separate LAYO Window. Keep it in sync
            // with the intro state without rebuilding the pattern grid.
            WindowBase castDecors =
                materialsBin.GetChildByID(
                    CastDecorsID,
                    true
                );

            if (castDecors != null)
            {
                castDecors.Visible = !sBrowserOpen;
            }

            // The back arrow belongs to the pattern browser only.
            WindowBase backToStart =
                materialsBin.GetChildByID(
                    BackToStartButtonID,
                    true
                );

            if (backToStart != null)
            {
                backToStart.Visible = sBrowserOpen;
                backToStart.Enabled = true;
            }


            /*
             * INTRO MODE:
             * palette + Choose a Pattern.
             */
            if (!sBrowserOpen)
            {
                if (sShowPatternsButton != null)
                {
                    sShowPatternsButton.Visible = true;
                    sShowPatternsButton.Enabled = true;
                }

                if (paletteImage != null)
                {
                    paletteImage.Visible = true;
                }

                if (combo != null)
                {
                    combo.Visible = false;
                    combo.Enabled = true;
                }

                if (filterButton != null)
                {
                    filterButton.Visible = false;
                    filterButton.Enabled = true;
                }

                if (grid != null)
                {
                    WindowBase browserPanel =
                        grid.Parent;

                    if (browserPanel != null)
                    {
                        browserPanel.Visible = false;
                    }
                    else
                    {
                        grid.Visible = false;
                    }
                }

                SetPaginationVisible(
                    false
                );

                return;
            }


            /*
             * BROWSER MODE.
             */
            if (sShowPatternsButton != null)
            {
                sShowPatternsButton.Visible = false;
            }

            if (paletteImage != null)
            {
                paletteImage.Visible = false;
            }

            if (combo != null)
            {
                combo.Visible = true;
                combo.Enabled = true;
            }

            if (filterButton != null)
            {
                filterButton.Visible = true;
                filterButton.Enabled = true;
            }

            if (grid != null)
            {
                WindowBase browserPanel =
                    grid.Parent;

                if (browserPanel != null)
                {
                    browserPanel.Visible = true;
                }

                grid.Visible = true;
            }

            UpdatePaginationControls();
        }


        // ========================================================
        // INTRO SAFETY NET (first opening after the game starts)
        // ========================================================

        // True when something that belongs to the pattern browser is shown
        // while the intro screen is supposed to be displayed.
        private static bool IntroLooksWrong(
            CASCompositorController controller)
        {
            WindowBase bin = GetMaterialsBin(controller);
            ItemGrid grid = GetGrid(controller);
            WindowBase panel = grid != null ? grid.Parent : null;
            WindowBase decors =
                bin != null
                    ? bin.GetChildByID(CastDecorsID, true)
                    : null;

            ComboBox combo = GetCombo(controller);
            Button filter = GetFilterButton(controller);

            if (combo != null && combo.Visible)
            {
                return true;
            }

            if (filter != null && filter.Visible)
            {
                return true;
            }

            if (panel != null && panel.Visible)
            {
                return true;
            }

            if (panel == null && grid != null && grid.Visible)
            {
                return true;
            }

            if (sPreviousPageButton != null && sPreviousPageButton.Visible)
            {
                return true;
            }

            if (sNextPageButton != null && sNextPageButton.Visible)
            {
                return true;
            }

            if (sShowPatternsButton != null && !sShowPatternsButton.Visible)
            {
                return true;
            }

            if (decors != null && !decors.Visible)
            {
                return true;
            }

            WindowBase backToStart =
                bin != null
                    ? bin.GetChildByID(BackToStartButtonID, true)
                    : null;

            if (backToStart != null && backToStart.Visible)
            {
                return true;
            }

            return false;
        }

        // Finite check (24 x 250 ms, so about 6 seconds), started once the
        // intro has been applied. It only acts if the game has shown the
        // pattern browser controls again; it ends when "Choisir une
        // texture" is clicked or when the CASt is no longer the same one.
        private static void EnforceIntroLater(
            CASCompositorController controller,
            int remaining)
        {
            if (remaining <= 0 ||
                controller == null)
            {
                return;
            }

            try
            {
                Simulator.AddObject(
                    new OneShotFunctionTask(
                        delegate
                        {
                            EnforceIntroStep(controller, remaining);
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

        private static void EnforceIntroStep(
            CASCompositorController controller,
            int remaining)
        {
            try
            {
                if (sBrowserOpen ||
                    !object.ReferenceEquals(
                        controller,
                        CASCompositorController.Instance) ||
                    !object.ReferenceEquals(
                        controller,
                        sBoundController))
                {
                    return;
                }

                WindowBase bin = GetMaterialsBin(controller);

                if (bin != null &&
                    bin.Visible &&
                    IntroLooksWrong(controller))
                {
                    ApplyBrowserVisibility(controller);
                }
            }
            catch
            {
            }

            EnforceIntroLater(controller, remaining - 1);
        }


        // ========================================================
        // CAST OPENING
        // ========================================================

        public static void FinishNoPatternLoad()
        {

            CASCompositorController controller =
                CASCompositorController.Instance;

            if (controller == null)
            {
                return;
            }


            controller.Enabled =
                true;


            WindowBase materialsBin =
                GetMaterialsBin(controller);

            if (materialsBin == null)
            {
                return;
            }

            materialsBin.Enabled =
                true;


            // An error while binding the extra controls must never stop the
            // intro screen from being applied below.
            try
            {
                BindBetterCASTControls(
                    controller,
                    materialsBin
                );
            }
            catch
            {
            }


            ApplyBrowserVisibility(
                controller
            );


            // If the game shows controls again after this point (first
            // opening), put the intro back for a few seconds. Stops by itself.
            EnforceIntroLater(controller, 24);


            /*
             * X / confirm remain available at all times.
             */
            Button confirm =
                materialsBin.GetChildByID(
                    73738U,
                    true
                ) as Button;

            Button cancel =
                materialsBin.GetChildByID(
                    73749U,
                    true
                ) as Button;


            if (confirm != null)
            {
                confirm.Enabled =
                    true;

                confirm.Click -= OnCAStClosing;
                confirm.Click += OnCAStClosing;
            }


            if (cancel != null)
            {
                cancel.Enabled =
                    true;

                cancel.Click -= OnCAStClosing;
                cancel.Click += OnCAStClosing;
            }
        }


        // ========================================================
        // CLICK: BACK TO THE INTRO SCREEN
        // ========================================================

        // The game's own "Go back" text (already translated in every
        // language); plain English only if the game does not give it.
        private static string LocalizeBackTooltip()
        {
            try
            {
                string key = "Ui/Caption/Global:GoBack";

                string text =
                    Localization.LocalizeString(key, new object[0]);

                if (!string.IsNullOrEmpty(text) &&
                    text != key &&
                    !text.StartsWith("Ui/"))
                {
                    return text;
                }
            }
            catch
            {
            }

            return "Back";
        }

        private static void OnBackToStartClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            try
            {
                CASCompositorController controller =
                    CASCompositorController.Instance;

                if (controller == null ||
                    !sBrowserOpen)
                {
                    return;
                }

                Audio.StartSound(
                    "ui_tertiary_button"
                );

                // Same state as a freshly opened CASt: nothing of the
                // browser is kept, so nothing keeps loading in the
                // background. The next click on "Choisir une texture"
                // builds a fresh page from the selected category.
                sBrowserOpen = false;

                ResetPaginationState();

                ItemGrid grid =
                    GetGrid(controller);

                if (grid != null)
                {
                    grid.Clear();
                }

                SetPaginationVisible(false);

                ApplyBrowserVisibility(controller);

                eventArgs.Handled =
                    true;
            }
            catch
            {
            }
        }


        // ========================================================
        // CLICK: CHOOSE A PATTERN
        // ========================================================

        private static void OnShowPatternsClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            CASCompositorController controller =
                CASCompositorController.Instance;

            if (controller == null)
            {
                return;
            }


            sBrowserOpen =
                true;


            ApplyBrowserVisibility(
                controller
            );


            /*
             * Avoid an empty first page by immediately loading
             * the category already selected in the ComboBox.
             *
             * The ComboBox remains visible so the category can be changed.
             */
            StartCurrentCategoryImmediately(
                controller
            );


            eventArgs.Handled =
                true;
        }


        // ========================================================
        // CATEGORY CHANGE
        // ========================================================

        public static void OnCategoryChanged(
            CASCompositorController controller,
            UISelectionChangeEventArgs eventArgs)
        {
            if (controller == null ||
                MaterialComboBoxField == null ||
                MaterialsGridField == null ||
                CurrentFilterField == null)
            {
                return;
            }


            ComboBox combo =
                GetCombo(controller);

            ItemGrid grid =
                GetGrid(controller);


            if (combo == null ||
                grid == null)
            {
                return;
            }


            int index =
                (int)eventArgs.NewIndex;


            if (index < 0 ||
                index >= combo.ValueList.Count)
            {
                return;
            }


            string selectedFilter =
                combo.EntryTags[index]
                as string;


            if (string.IsNullOrEmpty(
                    selectedFilter))
            {
                return;
            }


            CurrentFilterField.SetValue(
                controller,
                selectedFilter
            );


            sCurrentPage =
                0;

            sTotalPages =
                0;

            sPatternEntries =
                new List<PatternEntry>();


            int generation =
                ++sLoadGeneration;


            grid.Clear();


            /*
             * Patterns are only loaded once "Choose a Pattern" has been
             * clicked. If the game changes the combo while the browser is
             * closed (for example when the CASt opens), only remember the
             * category and stop here: the browser loads it when it opens.
             */
            if (!sBrowserOpen)
            {
                return;
            }

            SetPaginationVisible(
                false
            );




            /*
             * IMPORTANT:
             * do not disable the entire CASt interface.
             *
             * The user can change categories while this task is running,
             * which invalidates the current generation.
             */
            Simulator.AddObject(
                new OneShotFunctionTask(
                    delegate
                    {
                        BuildIndexAndLoadPage(
                            controller,
                            generation
                        );
                    }
                )
            );
        }


        // ========================================================
        // CUSTOM CONTENT FILTER
        // ========================================================

        public static void OnCustomContentFilterChanged(
            CASCompositorController controller,
            UIButtonClickEventArgs eventArgs)
        {
            if (controller == null)
            {
                return;
            }


            if (!sBrowserOpen)
            {
                eventArgs.Handled =
                    true;

                return;
            }


            ItemGrid grid =
                GetGrid(controller);


            if (grid != null)
            {
                grid.Clear();
            }


            sCurrentPage =
                0;

            sTotalPages =
                0;

            sPatternEntries =
                new List<PatternEntry>();


            int generation =
                ++sLoadGeneration;


            SetPaginationVisible(
                false
            );


            string currentFilter =
                CurrentFilterField != null
                    ? CurrentFilterField.GetValue(controller)
                        as string
                    : null;




            Simulator.AddObject(
                new OneShotFunctionTask(
                    delegate
                    {
                        BuildIndexAndLoadPage(
                            controller,
                            generation
                        );
                    }
                )
            );


            eventArgs.Handled =
                true;
        }


        // ========================================================
        // PATTERN CLICK - LAZY COMPLATE LOADING
        // ========================================================

        public static void OnMaterialGridItemClicked(
            CASCompositorController controller,
            ItemGridCellClickEvent clickedItem)
        {
            if (controller == null ||
                clickedItem.mTag == null)
            {
                return;
            }

            try
            {
                Audio.StartSound(
                    "ui_tertiary_button"
                );

                ItemGrid grid =
                    GetGrid(controller);

                if (grid != null)
                {
                    grid.RemoveTempItem();
                }

                object tag =
                    clickedItem.mTag;

                Complate complateToApply =
                    null;

                /*
                 * Pattern.GetComplate() is called only here,
                 * when the user actually clicks this pattern.
                 */
                BetterCASTLazyPatternTag lazyTag =
                    tag as BetterCASTLazyPatternTag;

                if (lazyTag != null)
                {
                    Complate source =
                        Pattern.GetComplate(
                            lazyTag.Key
                        );

                    if (source != null)
                    {
                        complateToApply =
                            source.Clone()
                            as Complate;
                    }
                }
                else
                {
                    /*
                     * Preserve vanilla behavior for other cell types.
                     */
                    Complate complate =
                        tag as Complate;

                    if (complate != null)
                    {
                        complateToApply =
                            complate.Clone()
                            as Complate;
                    }
                    else if (tag is ResourceKey)
                    {
                        ResourceKey resourceKey =
                            (ResourceKey)tag;

                        ColorInfo colorInfo =
                            ColorInfo.FromResourceKey(
                                resourceKey
                            );

                        if (colorInfo != null)
                        {
                            complateToApply =
                                CompositorUtil.ColorInfoToComplate(
                                    colorInfo
                                );
                        }
                    }
                }

                if (complateToApply != null &&
                    complateToApply.Key !=
                        ResourceKey.kInvalidResourceKey &&
                    MaterialSkewerSelectedPatternProperty != null &&
                    ApplyMaterialComplateMethod != null)
                {
                    int selectedPattern =
                        (int)MaterialSkewerSelectedPatternProperty.GetValue(
                            controller,
                            null
                        );

                    if (selectedPattern >= 0)
                    {
                        ApplyMaterialComplateMethod.Invoke(
                            controller,
                            new object[]
                            {
                                complateToApply,
                                selectedPattern,
                                true,
                                true
                            }
                        );
                    }
                }

                if (RepopulateColorsMethod != null)
                {
                    RepopulateColorsMethod.Invoke(
                        controller,
                        new object[]
                        {
                            true
                        }
                    );
                }

                CustomContentIcon customContentIcon =
                    clickedItem.mWin != null
                        ? clickedItem.mWin.GetChildByID(
                            34U,
                            false
                        ) as CustomContentIcon
                        : null;

                string currentFilter =
                    CurrentFilterField != null
                        ? CurrentFilterField.GetValue(
                            controller
                        ) as string
                        : null;

                if (customContentIcon != null)
                {
                    if (LastSelectedTypeField != null)
                    {
                        LastSelectedTypeField.SetValue(
                            controller,
                            (uint)customContentIcon.ContentType
                        );
                    }

                    Button trashButton =
                        MaterialsTrashButtonField != null
                            ? MaterialsTrashButtonField.GetValue(
                                controller
                            ) as Button
                            : null;

                    Button shareButton =
                        MaterialsShareButtonField != null
                            ? MaterialsShareButtonField.GetValue(
                                controller
                            ) as Button
                            : null;

                    if (trashButton != null)
                    {
                        trashButton.Enabled =
                            customContentIcon.IsCustomContent ||
                            currentFilter == "Favorite";
                    }

                    if (shareButton != null)
                    {
                        shareButton.Enabled =
                            customContentIcon.Localuser;
                    }
                }

                Button favoriteButton =
                    MaterialsFavoriteButtonField != null
                        ? MaterialsFavoriteButtonField.GetValue(
                            controller
                        ) as Button
                        : null;

                Button saveButton =
                    MaterialsSaveButtonField != null
                        ? MaterialsSaveButtonField.GetValue(
                            controller
                        ) as Button
                        : null;

                Button shareButton2 =
                    MaterialsShareButtonField != null
                        ? MaterialsShareButtonField.GetValue(
                            controller
                        ) as Button
                        : null;

                if (favoriteButton != null)
                {
                    favoriteButton.Enabled =
                        currentFilter !=
                        "Favorite";
                }

                if (saveButton != null)
                {
                    saveButton.Enabled =
                        false;
                }

                if (complateToApply != null &&
                    complateToApply.Key ==
                        ResourceKey.kInvalidResourceKey &&
                    shareButton2 != null)
                {
                    shareButton2.Enabled =
                        false;
                }

                if (UndoOnDeleteField != null)
                {
                    UndoOnDeleteField.SetValue(
                        controller,
                        true
                    );
                }
            }
            catch
            {
                /*
                 * Safety:
                 * a problematic cell must never crash CASt.
                 */
            }
        }


        // ========================================================
        // PERFORMANCE HELPERS
        // ========================================================

        private static ResourceKeyContentCategory GetCustomTypeCached(
            ResourceKey patternKey)
        {
            ResourceKeyContentCategory customType;

            if (sCustomTypeCache.TryGetValue(
                    patternKey,
                    out customType))
            {
                return customType;
            }

            customType =
                UIUtils.GetCustomContentType(
                    patternKey
                );

            sCustomTypeCache[patternKey] =
                customType;

            return customType;
        }


        private static string BuildIndexCacheKey(
            string currentFilter,
            bool customOnly)
        {
            return
                currentFilter
                + (customOnly
                    ? "|CC"
                    : "|ALL");
        }


        private static bool AppendCategoryEntries(
            string category,
            bool customOnly,
            List<PatternEntry> target,
            int generation,
            ref int scanned)
        {
            ResourceKey[] patterns =
                Pattern.GetPatternsForCategory(
                    category
                );

            foreach (ResourceKey patternKey in patterns)
            {
                if (generation != sLoadGeneration ||
                    CASCompositorController.Instance == null)
                {
                    return false;
                }

                /*
                 * If the CC filter is OFF:
                 * do not call GetCustomContentType here.
                 *
                 * If the CC filter is ON:
                 * the content type is required for correct filtering.
                 */
                if (customOnly)
                {
                    ResourceKeyContentCategory customType =
                        GetCustomTypeCached(
                            patternKey
                        );

                    if (!UIUtils.IsCustomFiltered(
                            customType))
                    {
                        scanned++;

                        if ((scanned % 64) == 0)
                        {
                            Simulator.Sleep(0U);

                            if (generation != sLoadGeneration)
                            {
                                return false;
                            }
                        }

                        continue;
                    }
                }

                target.Add(
                    new PatternEntry(
                        category,
                        patternKey
                    )
                );

                scanned++;

                if ((scanned % 64) == 0)
                {
                    Simulator.Sleep(
                        0U
                    );

                    if (generation != sLoadGeneration)
                    {
                        return false;
                    }
                }
            }

            return true;
        }


        /*
         * EA stores user-saved CASt patterns as ColorInfo presets, not as
         * Pattern resources. The normal category browser must index both.
         *
         * This is intentionally NOT part of sPatternIndexCache:
         * players can save/delete a preset while CASt is open, and the
         * category cache would otherwise continue to serve stale results.
         *
         * Index metadata only. Thumbnails and Complates are generated
         * for at most the 15 entries on the currently displayed page.
         */
        private static bool AppendSavedPresetEntries(
            CASCompositorController controller,
            string currentFilter,
            bool customOnly,
            List<PatternEntry> target,
            int generation)
        {
            List<ResourceKey> savedKeys = ColorInfo.GetColorInfos(
                ResourceTypes.kCASColorInfoResourceID
            );

            if (savedKeys == null)
            {
                return true;
            }

            int scanned = 0;

            foreach (ResourceKey savedKey in savedKeys)
            {
                if (generation != sLoadGeneration ||
                    CASCompositorController.Instance == null)
                {
                    return false;
                }

                if (customOnly &&
                    !UIUtils.IsCustomFiltered(GetCustomTypeCached(savedKey)))
                {
                    continue;
                }

                ColorInfo info = ColorInfo.FromResourceKey(savedKey);
                if (info == null ||
                    info.Favorite ||
                    info.Fabrics == null ||
                    info.Fabrics.Length > 1)
                {
                    continue;
                }

                Complate material = CompositorUtil.ColorInfoToComplate(info);
                if (material == null ||
                    material.Key == ResourceKey.kInvalidResourceKey ||
                    material.Category == "Old")
                {
                    continue;
                }

                bool fits = material.Category == currentFilter;

                // Use EA's own classification for Miscellaneous.
                if (currentFilter == "Miscellaneous")
                {
                    if (PatternFitsFilterMethod == null)
                    {
                        continue;
                    }

                    fits = (bool)PatternFitsFilterMethod.Invoke(
                        controller,
                        new object[] { material.Category, false }
                    );
                }

                if (fits)
                {
                    target.Add(
                        PatternEntry.SavedPreset(material.Category, savedKey)
                    );
                }

                scanned++;
                if ((scanned % 64) == 0)
                {
                    Simulator.Sleep(0U);
                    if (generation != sLoadGeneration)
                    {
                        return false;
                    }
                }
            }

            return true;
        }


        private static void FinishIndexAndLoadPage(
            CASCompositorController controller,
            int generation,
            List<PatternEntry> entries)
        {
            if (controller == null ||
                generation != sLoadGeneration)
            {
                return;
            }

            sPatternEntries =
                entries;

            sTotalPages =
                sPatternEntries.Count == 0
                    ? 0
                    : (
                        sPatternEntries.Count
                        + PatternsPerPage
                        - 1
                    ) / PatternsPerPage;

            if (sCurrentPage >= sTotalPages)
            {
                sCurrentPage =
                    sTotalPages > 0
                        ? sTotalPages - 1
                        : 0;
            }

            UpdatePaginationControls();

            LoadCurrentPage(
                controller,
                generation
            );
        }


        /*
         * When "Choose a Pattern" is clicked, avoid leaving
         * an empty page.
         *
         * Open the browser and immediately load the category
         * already selected in the ComboBox.
         *
         * No other page is preloaded.
         */
        private static void StartCurrentCategoryImmediately(
            CASCompositorController controller)
        {
            if (controller == null)
            {
                return;
            }

            string currentFilter =
                CurrentFilterField != null
                    ? CurrentFilterField.GetValue(controller)
                        as string
                    : null;

            if (string.IsNullOrEmpty(
                    currentFilter))
            {
                return;
            }

            ItemGrid grid =
                GetGrid(controller);

            if (grid != null)
            {
                grid.Clear();
            }

            sCurrentPage = 0;
            sTotalPages = 0;
            sPatternEntries =
                new List<PatternEntry>();

            int generation =
                ++sLoadGeneration;

            SetPaginationVisible(
                false
            );

            Simulator.AddObject(
                new OneShotFunctionTask(
                    delegate
                    {
                        BuildIndexAndLoadPage(
                            controller,
                            generation
                        );
                    }
                )
            );
        }


        // ========================================================
        // LIGHTWEIGHT PATTERN INDEX
        // ========================================================

        private static void BuildIndexAndLoadPage(
            CASCompositorController controller,
            int generation)
        {
            if (controller == null ||
                CASCompositorController.Instance == null ||
                generation != sLoadGeneration)
            {
                return;
            }

            string currentFilter =
                CurrentFilterField != null
                    ? CurrentFilterField.GetValue(controller)
                        as string
                    : null;

            if (string.IsNullOrEmpty(
                    currentFilter))
            {
                return;
            }

            Button filterButton =
                GetFilterButton(controller);

            bool customOnly =
                filterButton != null &&
                filterButton.Selected;

            string cacheKey =
                BuildIndexCacheKey(
                    currentFilter,
                    customOnly
                );

            /*
             * Previously visited category:
             * immediately reuse the lightweight index.
             */
            List<PatternEntry> cachedEntries;

            if (currentFilter != "Favorite" &&
                sPatternIndexCache.TryGetValue(
                    cacheKey,
                    out cachedEntries))
            {
                // Keep EA/CC Pattern keys cached, but re-read saved presets.
                List<PatternEntry> mergedEntries =
                    new List<PatternEntry>(cachedEntries);

                if (!AppendSavedPresetEntries(
                        controller,
                        currentFilter,
                        customOnly,
                        mergedEntries,
                        generation))
                {
                    return;
                }

                FinishIndexAndLoadPage(
                    controller,
                    generation,
                    mergedEntries
                );

                return;
            }

            List<PatternEntry> localEntries =
                new List<PatternEntry>();

            int scanned =
                0;

            /*
             * Woods / Fabrics / Metals / etc.
             *
             * There is no need to scan every category:
             * the vanilla filter accepts the category whose name
             * matches mCurrentFilter directly.
             */
            if (currentFilter == "Favorite")
            {
                // EA stores favorites as ColorInfo presets, not Pattern assets.
                // Index only the keys; render up to 16 entries on the visible page.
                List<ResourceKey> favorites = ColorInfo.GetColorInfos(
                    ResourceTypes.kCASColorInfoResourceID);
                foreach (ResourceKey favoriteKey in favorites)
                {
                    if (generation != sLoadGeneration ||
                        CASCompositorController.Instance == null)
                    {
                        return;
                    }

                    if (customOnly && !UIUtils.IsCustomFiltered(
                            GetCustomTypeCached(favoriteKey)))
                    {
                        continue;
                    }

                    ColorInfo info = ColorInfo.FromResourceKey(favoriteKey);
                    if (info == null || !info.Favorite ||
                        info.Fabrics == null || info.Fabrics.Length > 1)
                    {
                        continue;
                    }

                    Complate material = CompositorUtil.ColorInfoToComplate(info);
                    if (material == null ||
                        material.Key == ResourceKey.kInvalidResourceKey ||
                        material.Category == "Old")
                    {
                        continue;
                    }

                    localEntries.Add(PatternEntry.Favorite(
                        material.Category, favoriteKey));
                }
            }
            else if (currentFilter != "Miscellaneous")
            {
                if (!AppendCategoryEntries(
                        currentFilter,
                        customOnly,
                        localEntries,
                        generation,
                        ref scanned))
                {
                    return;
                }
            }
            else
            {
                /*
                 * Miscellaneous is special:
                 * it groups categories not listed in the ComboBox.
                 * Preserve EA's logic here.
                 */
                if (PatternFitsFilterMethod == null)
                {
                    return;
                }

                if (sPatternCategoriesCache == null)
                {
                    sPatternCategoriesCache =
                        Pattern.GetPatternCategories();
                }

                foreach (string category in sPatternCategoriesCache)
                {
                    if (generation != sLoadGeneration ||
                        CASCompositorController.Instance == null)
                    {
                        return;
                    }

                    bool fits =
                        (bool)PatternFitsFilterMethod.Invoke(
                            controller,
                            new object[]
                            {
                                category,
                                false
                            }
                        );

                    if (!fits)
                    {
                        continue;
                    }

                    if (!AppendCategoryEntries(
                            category,
                            customOnly,
                            localEntries,
                            generation,
                            ref scanned))
                    {
                        return;
                    }
                }
            }

            if (generation != sLoadGeneration)
            {
                return;
            }

            /*
             * Lightweight cache only:
             * Category + ResourceKey.
             */
            if (currentFilter != "Favorite")
            {
                // Cache only static Pattern entries, never ColorInfo presets.
                sPatternIndexCache[cacheKey] =
                    new List<PatternEntry>(localEntries);

                if (!AppendSavedPresetEntries(
                        controller,
                        currentFilter,
                        customOnly,
                        localEntries,
                        generation))
                {
                    return;
                }
            }

            FinishIndexAndLoadPage(
                controller,
                generation,
                localEntries
            );
        }


        // ========================================================
        // V2 FIRST / LAST PAGE BRIDGE
        // ========================================================
        public static void GoToPage(int requestedPage)
        {
            if (!sBrowserOpen || sTotalPages <= 0 ||
                sPatternEntries.Count == 0)
            {
                return;
            }

            int target = Math.Max(0, Math.Min(requestedPage, sTotalPages - 1));
            if (target == sCurrentPage)
            {
                return;
            }

            CASCompositorController controller = CASCompositorController.Instance;
            if (controller == null)
            {
                return;
            }

            sCurrentPage = target;
            StartPageLoad(controller);
        }

        // ========================================================
        // PREVIOUS / NEXT PAGE
        // ========================================================

        private static void OnPreviousPageClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            if (sCurrentPage <= 0 ||
                sPatternEntries.Count == 0)
            {
                eventArgs.Handled =
                    true;

                return;
            }


            CASCompositorController controller =
                CASCompositorController.Instance;

            if (controller == null)
            {
                return;
            }


            sCurrentPage--;

            StartPageLoad(
                controller
            );


            eventArgs.Handled =
                true;
        }


        private static void OnNextPageClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            if (sCurrentPage >=
                    sTotalPages - 1 ||
                sPatternEntries.Count == 0)
            {
                eventArgs.Handled =
                    true;

                return;
            }


            CASCompositorController controller =
                CASCompositorController.Instance;

            if (controller == null)
            {
                return;
            }


            sCurrentPage++;

            StartPageLoad(
                controller
            );


            eventArgs.Handled =
                true;
        }


        private static void StartPageLoad(
            CASCompositorController controller)
        {
            ItemGrid grid =
                GetGrid(controller);


            int generation =
                ++sLoadGeneration;


            if (grid != null)
            {
                /*
                 * Remove the cells from the previous page immediately.
                 */
                grid.Clear();
            }


            UpdatePaginationControls();


            Simulator.AddObject(
                new OneShotFunctionTask(
                    delegate
                    {
                        LoadCurrentPage(
                            controller,
                            generation
                        );
                    }
                )
            );
        }


        // ========================================================
        // LOAD ONLY THE CURRENT PAGE
        // ========================================================

        private static void LoadCurrentPage(
            CASCompositorController controller,
            int generation)
        {
            if (controller == null ||
                CASCompositorController.Instance == null ||
                generation != sLoadGeneration)
            {
                return;
            }

            ItemGrid grid =
                GetGrid(controller);

            if (grid == null ||
                AddMaterialBinGridItemMethod == null)
            {
                return;
            }

            int start =
                sCurrentPage *
                PatternsPerPage;

            int end =
                Math.Min(
                    start + PatternsPerPage,
                    sPatternEntries.Count
                );

            if (start < 0 ||
                start >= sPatternEntries.Count)
            {
                UpdatePaginationControls();
                return;
            }

            bool debugTooltips =
                DebugTooltipsField != null &&
                (bool)DebugTooltipsField.GetValue(
                    controller
                );

            /*
             * LAZY COMPLATE
             *
             * A cell contains:
             * - ResourceKey
             * - category
             * - thumbnail
             *
             * It does NOT load its Complate just to appear
             * in the catalog.
             */
            for (int i = start; i < end; i++)
            {
                if (generation != sLoadGeneration ||
                    CASCompositorController.Instance == null)
                {
                    return;
                }

                PatternEntry entry =
                    sPatternEntries[i];

                // Native EA favorites are ResourceKey-tagged ColorInfo presets.
                // They must remain ResourceKey-tagged for clicks, sharing and
                // removal through the original CASt handlers.
                if (entry.IsFavorite || entry.IsSavedPreset)
                {
                    ColorInfo info = ColorInfo.FromResourceKey(entry.Key);
                    if (info != null &&
                        (entry.IsFavorite ? info.Favorite : !info.Favorite))
                    {
                        Complate material = CompositorUtil.ColorInfoToComplate(info);
                        if (material != null &&
                            material.Key != ResourceKey.kInvalidResourceKey)
                        {
                            uint preview = CompositorUtil.GetPatternThumbnail(
                                material.Category, entry.Key.InstanceId, null);
                            if (preview == 0U)
                            {
                                TextureCompositor compositor =
                                    material.CreateTextureCompositor(null, null, 0U);
                                if (compositor != null)
                                {
                                    byte[] data = compositor.ExportData(null, null, null);
                                    preview = CompositorUtil.GetPatternThumbnail(
                                        material.Category, entry.Key.InstanceId, data);
                                }
                            }

                            ResourceKeyContentCategory presetContentType =
                                entry.IsFavorite
                                    ? (ResourceKeyContentCategory)info.FavoriteContentType
                                    : GetCustomTypeCached(entry.Key);
                            AddMaterialBinGridItemMethod.Invoke(
                                controller,
                                new object[] {
                                    MaterialsGridItemLayoutKey,
                                    entry.Key,
                                    new UIImage(preview),
                                    debugTooltips ? material.Name : string.Empty,
                                    presetContentType
                                });
                        }
                    }

                    Simulator.Sleep(0U);
                    continue;
                }

                ResourceKeyContentCategory customType =
                    GetCustomTypeCached(
                        entry.Key
                    );

                /*
                 * Request an existing thumbnail first.
                 */
                uint thumbnail =
                    CompositorUtil.GetPatternThumbnail(
                        entry.Category,
                        entry.Key.InstanceId,
                        null
                    );

                if (generation != sLoadGeneration)
                {
                    return;
                }

                /*
                 * If the thumbnail is missing, generate the compositor
                 * for THIS pattern so its image can be displayed.
                 *
                 * This is the only case where the Complate may be
                 * built before the user clicks it.
                 */
                if (thumbnail == 0U)
                {
                    thumbnail =
                        CompositorUtil.GetPatternThumbnail(
                            entry.Category,
                            entry.Key.InstanceId,
                            Pattern.GetCompositor(
                                entry.Key
                            )
                        );
                }

                if (generation != sLoadGeneration)
                {
                    return;
                }

                /*
                 * In debug mode only, the tooltip may request
                 * the pattern's full name.
                 * Normal gameplay adds no extra load here.
                 */
                string tooltip =
                    string.Empty;

                if (debugTooltips)
                {
                    Complate debugComplate =
                        Pattern.GetComplate(
                            entry.Key
                        );

                    if (debugComplate != null)
                    {
                        tooltip =
                            debugComplate.Name;
                    }
                }

                BetterCASTLazyPatternTag lazyTag =
                    new BetterCASTLazyPatternTag(
                        entry.Category,
                        entry.Key
                    );

                AddMaterialBinGridItemMethod.Invoke(
                    controller,
                    new object[]
                    {
                        MaterialsGridItemLayoutKey,
                        lazyTag,
                        new UIImage(thumbnail),
                        tooltip,
                        customType
                    }
                );

                Simulator.Sleep(
                    0U
                );

                if (generation != sLoadGeneration)
                {
                    return;
                }
            }

            UpdatePaginationControls();
        }
    }
}
