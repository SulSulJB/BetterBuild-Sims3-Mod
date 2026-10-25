using System;
using System.Collections.Generic;
using System.Reflection;
using MonoPatcherLib;
using Sims3.Gameplay.Utilities;
using Sims3.Metadata;
using Sims3.SimIFace;
using Sims3.SimIFace.CustomContent;
using Sims3.UI;
using Sims3.UI.CAS;

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
         * - 12-pattern pagination
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

        private const int PatternsPerPage = 12;

        private const uint ShowPatternsButtonID =
            0x0012F001U;

        private const uint PaletteImageID =
            0x0012F002U;

        private const uint PreviousPageButtonID =
            0x0012F003U;

        private const uint NextPageButtonID =
            0x0012F004U;

        private const uint PageTextID =
            0x0012F005U;


        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public;


        // Match the localized CASt button captions and tooltips present in the
        // published assembly (verified against ILSpy output).
        private static string Localize(string key)
        {
            return Localization.LocalizeString(
                "Jojobou/BetterBuild/BetterCAST:" + key,
                new object[0]
            );
        }


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

            public PatternEntry(
                string category,
                ResourceKey key)
            {
                Category = category;
                Key = key;
            }
        }


        // ========================================================
        // BETTER CAST STATE
        // ========================================================

        private static CASCompositorController
            sBoundController = null;

        private static Button
            sShowPatternsButton = null;

        private static Button
            sPreviousPageButton = null;

        private static Button
            sNextPageButton = null;

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


        private static void BindBetterCASTControls(
            CASCompositorController controller,
            WindowBase materialsBin)
        {
            if (controller == null ||
                materialsBin == null)
            {
                return;
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
                string chooseTexture = Localize("ChooseTexture");
                sShowPatternsButton.Caption = chooseTexture;
                sShowPatternsButton.TooltipText = chooseTexture;

                sShowPatternsButton.Click -=
                    OnShowPatternsClick;

                sShowPatternsButton.Click +=
                    OnShowPatternsClick;
            }


            if (sPreviousPageButton != null)
            {
                sPreviousPageButton.TooltipText = Localize("PreviousPage");

                sPreviousPageButton.Click -=
                    OnPreviousPageClick;

                sPreviousPageButton.Click +=
                    OnPreviousPageClick;
            }


            if (sNextPageButton != null)
            {
                sNextPageButton.TooltipText = Localize("NextPage");

                sNextPageButton.Click -=
                    OnNextPageClick;

                sNextPageButton.Click +=
                    OnNextPageClick;
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


            BindBetterCASTControls(
                controller,
                materialsBin
            );


            ApplyBrowserVisibility(
                controller
            );


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

                /*
                 * This handler does NOT block EA's handler.
                 * It only resets Better CASt state.
                 */
                confirm.Click -=
                    OnCAStClosing;

                confirm.Click +=
                    OnCAStClosing;
            }


            if (cancel != null)
            {
                cancel.Enabled =
                    true;

                cancel.Click -=
                    OnCAStClosing;

                cancel.Click +=
                    OnCAStClosing;
            }
        }


        private static void OnCAStClosing(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            sBrowserOpen =
                false;

            ResetPaginationState();
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

            SetPaginationVisible(
                false
            );


            /*
             * Favorites uses ColorInfo.
             * Keep the empty behavior for now.
             */
            if (selectedFilter == "Favorite")
            {
                UpdatePaginationControls();
                return;
            }


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


            if (currentFilter == "Favorite")
            {
                eventArgs.Handled =
                    true;

                return;
            }


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
                    currentFilter) ||
                currentFilter == "Favorite")
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

            if (sPatternIndexCache.TryGetValue(
                    cacheKey,
                    out cachedEntries))
            {
                FinishIndexAndLoadPage(
                    controller,
                    generation,
                    cachedEntries
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
            if (currentFilter != "Miscellaneous")
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
            sPatternIndexCache[cacheKey] =
                localEntries;

            FinishIndexAndLoadPage(
                controller,
                generation,
                localEntries
            );
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
                 * Remove the 12 cells from the previous page immediately.
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
