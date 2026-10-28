using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.SimIFace.BuildBuy;
using Sims3.UI;

namespace BetterBuildByJojobou
{
    /*
     * Better Build - Build mode UI bindings
     *
     * Handles ONLY Build mode:
     * - integrated TextEdit search (Normal + Expanded)
     * - MoveObjects checkbox (Normal + Expanded)
     * - hide the native Build DLC/CC filter while Expanded
     * - hide all NORMAL Better Build controls while Expanded
     *
     * The active Build catalog changes constantly (walls, floors, fences,
     * stairs, roof, terrain, gardening, collections, etc.), so this class
     * binds to the catalog currently passed by BuildControllerPatch.
     *
     * No Task.
     * No timer.
     * No polling.
     */
    public static class BuildCatalogUI
    {
        private const uint NormalSearchWindowID = 0x0bc50000U;
        private const uint NormalSearchTextID = 0x0bc50001U;
        private const uint NormalClearSearchButtonID = 0x0bc50030U;
        private const uint NormalAllCatalogButtonID = 0x0bc50002U;
        private const uint NormalMoveObjectsButtonID = 0x0bc50010U;
        private const uint NormalSaveButtonID = 0x0bc50020U;

        private const uint ExpandedSearchTextID = 0x00000204U;
        private const uint ExpandedClearSearchButtonID = 0x00000208U;
        private const uint ExpandedAllCatalogButtonID = 0x00000205U;
        private const uint ExpandedMoveObjectsButtonID = 0x00000206U;
        private const uint ExpandedSaveButtonID = 0x00000207U;

        // Better Build interface toggle button (B)
        private const uint NormalInterfaceToggleButtonID = 0x0bc50040U;
        private const uint ExpandedInterfaceToggleButtonID = 0x00000209U;

        private static string SearchPlaceholder
        {
            get
            {
                return Localization.LocalizeString(
                    "Jojobou/BetterBuild/CatalogSearch:SearchPlaceholder",
                    new object[0]
                );
            }
        }

        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public;

        private static readonly BindingFlags StaticFlags =
            BindingFlags.Static |
            BindingFlags.NonPublic |
            BindingFlags.Public;

        private static readonly FieldInfo CatalogProductFilterField =
            typeof(BuildController).GetField(
                "mCatalogProductFilter",
                InstanceFlags
            );

        private static readonly FieldInfo CurrentCatalogToolField =
            typeof(BuildController).GetField(
                "mCurrentCatalogTool",
                InstanceFlags
            );

        private static readonly FieldInfo CurrentCatalogProductField =
            typeof(BuildController).GetField(
                "mCurrentCatalogProduct",
                InstanceFlags
            );

        private static readonly FieldInfo CatalogFilterField =
            typeof(BuildController).GetField(
                "mCatalogFilter",
                InstanceFlags
            );

        private static readonly FieldInfo ProductListField =
            typeof(BuildController).GetField(
                "mProductList",
                InstanceFlags
            );

        private static readonly FieldInfo UnfilteredProductListField =
            typeof(BuildController).GetField(
                "mUnfilteredProductList",
                InstanceFlags
            );

        private static readonly FieldInfo PresetListField =
            typeof(BuildController).GetField(
                "mPresetList",
                InstanceFlags
            );

        private static readonly MethodInfo GetProductListMethod =
            typeof(BuildController).GetMethod(
                "GetProductList",
                InstanceFlags
            );

        private static readonly MethodInfo GetPresetListMethod =
            typeof(BuildController).GetMethod(
                "GetPresetList",
                InstanceFlags
            );

        private static readonly MethodInfo PopulateCatalogMethod =
            typeof(BuildController).GetMethod(
                "PopulateCatalog",
                InstanceFlags,
                null,
                new Type[]
                {
                    typeof(ItemGrid),
                    typeof(UserToolUtils.BuildBuyProductType),
                    typeof(uint)
                },
                null
            );

        private static readonly MethodInfo RebuildCatalogMethod =
            typeof(BuildController).GetMethod(
                "RebuildCatalog",
                InstanceFlags,
                null,
                Type.EmptyTypes,
                null
            );

        private static readonly FieldInfo SharedMoveObjectsStateField =
            typeof(MoveObjectsToggle).GetField(
                "sEnabled",
                StaticFlags
            );

        private static BuildController mController;
        private static ItemGrid mCatalogGrid;
        private static Window mCatalogWindow;
        private static ExpandableCatalogGrid mExpandableGrid;
        private static Button mNativeFilterButton;

        private static WindowBase mNormalSearchWindow;
        private static TextEdit mNormalSearch;
        private static TextEdit mExpandedSearch;

        private static Button mNormalClearSearch;
        private static Button mExpandedClearSearch;

        private static Button mNormalMoveObjects;
        private static Button mExpandedMoveObjects;

        private static Button mNormalAllCatalog;
        private static Button mExpandedAllCatalog;
        private static Button mNormalSave;
        private static Button mExpandedSave;

        private static Button mNormalInterfaceToggle;
        private static Button mExpandedInterfaceToggle;

        private static bool mUpdatingSearchBoxes;
        private static bool mPlaceholderMode = true;
        private static string mCurrentQuery = "";
        private static string mCurrentText = SearchPlaceholder;

        private static readonly Dictionary<BuildBuyProduct, string>
            sSearchableNameCache =
                new Dictionary<BuildBuyProduct, string>();

        public static void Attach(
            BuildController controller,
            ItemGrid catalogGrid,
            Window currentCatalogWindow)
        {
            if (controller == null ||
                catalogGrid == null ||
                currentCatalogWindow == null)
            {
                return;
            }

            ExpandableCatalogGrid expandable =
                FindExpandableParent(
                    catalogGrid
                );

            if (expandable == null)
            {
                return;
            }

            bool sameCatalog =
                object.ReferenceEquals(
                    mController,
                    controller
                ) &&
                object.ReferenceEquals(
                    mCatalogGrid,
                    catalogGrid
                ) &&
                object.ReferenceEquals(
                    mCatalogWindow,
                    currentCatalogWindow
                ) &&
                object.ReferenceEquals(
                    mExpandableGrid,
                    expandable
                );

            if (!sameCatalog)
            {
                DetachCurrentCatalog();

                mController = controller;
                mCatalogGrid = catalogGrid;
                mCatalogWindow = currentCatalogWindow;
                mExpandableGrid = expandable;

                mNativeFilterButton =
                    mCatalogGrid.Tag as Button;

                mExpandableGrid.ExpandChanged -=
                    OnExpandChanged;

                mExpandableGrid.ExpandChanged +=
                    OnExpandChanged;

                mController.Detach -=
                    OnControllerDetach;

                mController.Detach +=
                    OnControllerDetach;

                /*
                 * Build categories are separate catalogs.
                 * Start each newly-selected Build catalog clean.
                 */
                mCurrentQuery = "";
                mCurrentText = SearchPlaceholder;
                mPlaceholderMode = true;
            }
            else
            {
                mNativeFilterButton =
                    mCatalogGrid.Tag as Button;
            }

            RefreshBindings();
            UpdateVisibility();
            SyncMoveObjectsButtons();
        }

        private static ExpandableCatalogGrid FindExpandableParent(
            WindowBase window)
        {
            WindowBase current = window;

            while (current != null)
            {
                ExpandableCatalogGrid expandable =
                    current as ExpandableCatalogGrid;

                if (expandable != null)
                {
                    return expandable;
                }

                current = current.Parent;
            }

            return null;
        }

        private static void RefreshBindings()
        {
            if (mCatalogWindow == null ||
                mExpandableGrid == null)
            {
                return;
            }

            WindowBase normalSearchWindow =
                mCatalogWindow.GetChildByID(
                    NormalSearchWindowID,
                    true
                );

            TextEdit normalSearch =
                mCatalogWindow.GetChildByID(
                    NormalSearchTextID,
                    true
                ) as TextEdit;

            TextEdit expandedSearch =
                mExpandableGrid.GetChildByID(
                    ExpandedSearchTextID,
                    true
                ) as TextEdit;

            Button normalClearSearch =
                mCatalogWindow.GetChildByID(
                    NormalClearSearchButtonID,
                    true
                ) as Button;

            Button expandedClearSearch =
                mExpandableGrid.GetChildByID(
                    ExpandedClearSearchButtonID,
                    true
                ) as Button;

            Button normalMove =
                mCatalogWindow.GetChildByID(
                    NormalMoveObjectsButtonID,
                    true
                ) as Button;

            Button expandedMove =
                mExpandableGrid.GetChildByID(
                    ExpandedMoveObjectsButtonID,
                    true
                ) as Button;

            mNormalSearchWindow =
                normalSearchWindow;

            RebindSearch(
                ref mNormalSearch,
                normalSearch
            );

            RebindSearch(
                ref mExpandedSearch,
                expandedSearch
            );

            RebindClearSearch(
                ref mNormalClearSearch,
                normalClearSearch
            );

            RebindClearSearch(
                ref mExpandedClearSearch,
                expandedClearSearch
            );

            RebindMoveObjects(
                ref mNormalMoveObjects,
                normalMove
            );

            RebindMoveObjects(
                ref mExpandedMoveObjects,
                expandedMove
            );

            mNormalAllCatalog =
                mCatalogWindow.GetChildByID(
                    NormalAllCatalogButtonID,
                    true
                ) as Button;

            mExpandedAllCatalog =
                mExpandableGrid.GetChildByID(
                    ExpandedAllCatalogButtonID,
                    true
                ) as Button;

            string allCatalogTooltip =
                Localization.LocalizeString(
                    "Jojobou/BetterBuild/CatalogSearch:AllCatalogTooltip",
                    new object[0]
                );

            if (mNormalAllCatalog != null)
            {
                mNormalAllCatalog.TooltipText =
                    allCatalogTooltip;
            }

            if (mExpandedAllCatalog != null)
            {
                mExpandedAllCatalog.TooltipText =
                    allCatalogTooltip;
            }

            mNormalSave =
                mCatalogWindow.GetChildByID(
                    NormalSaveButtonID,
                    true
                ) as Button;

            mExpandedSave =
                mExpandableGrid.GetChildByID(
                    ExpandedSaveButtonID,
                    true
                ) as Button;

            string saveTooltip =
                Localization.LocalizeString(
                    "Jojobou/BetterBuild/QuickSaveButton:Tooltip",
                    new object[0]
                );

            if (mNormalSave != null)
            {
                mNormalSave.TooltipText =
                    saveTooltip;
            }

            if (mExpandedSave != null)
            {
                mExpandedSave.TooltipText =
                    saveTooltip;
            }

            // Quick save in Build mode: save in place, stay in Build.
            if (mNormalSave != null)
            {
                mNormalSave.Click -= OnSaveClick;
                mNormalSave.Click += OnSaveClick;
            }

            if (mExpandedSave != null)
            {
                mExpandedSave.Click -= OnSaveClick;
                mExpandedSave.Click += OnSaveClick;
            }

            /*
             * Better Build B button.
             * The Normal and Expanded LAYOs each contain their own copy.
             * Keep references to both so UpdateVisibility() can guarantee
             * that only the copy matching the current catalog state is shown.
             */
            mNormalInterfaceToggle =
                mCatalogWindow.GetChildByID(
                    NormalInterfaceToggleButtonID,
                    true
                ) as Button;

            mExpandedInterfaceToggle =
                mExpandableGrid.GetChildByID(
                    ExpandedInterfaceToggleButtonID,
                    true
                ) as Button;
        }

        private static void RebindSearch(
            ref TextEdit current,
            TextEdit next)
        {
            if (object.ReferenceEquals(
                current,
                next))
            {
                return;
            }

            if (current != null)
            {
                current.TextChange -=
                    OnTextChange;

                current.FocusAcquired -=
                    OnSearchFocusAcquired;

                current.FocusLost -=
                    OnSearchFocusLost;
            }

            current = next;

            if (current == null)
            {
                return;
            }

            current.TextChange -=
                OnTextChange;

            current.FocusAcquired -=
                OnSearchFocusAcquired;

            current.FocusLost -=
                OnSearchFocusLost;

            if (current.Caption != mCurrentText)
            {
                current.Caption =
                    mCurrentText;
            }

            current.TextChange +=
                OnTextChange;

            current.FocusAcquired +=
                OnSearchFocusAcquired;

            current.FocusLost +=
                OnSearchFocusLost;
        }

        private static void RebindClearSearch(
            ref Button current,
            Button next)
        {
            if (object.ReferenceEquals(
                current,
                next))
            {
                return;
            }

            if (current != null)
            {
                current.Click -=
                    OnClearSearchClick;
            }

            current = next;

            if (current != null)
            {
                current.TooltipText =
                    Localization.LocalizeString(
                        "Jojobou/BetterBuild/CatalogSearch:ClearSearchTooltip",
                        new object[0]
                    );

                current.Click -=
                    OnClearSearchClick;

                current.Click +=
                    OnClearSearchClick;
            }
        }

        private static void OnSaveClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            QuickSaveButton.SaveInPlace(mController);
        }

        private static void OnClearSearchClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            if (mController == null)
            {
                return;
            }

            /*
             * The X clears the current search, restores the placeholder in
             * Normal + Expanded, and restores the complete active catalog.
             *
             * SetSearchTextEverywhere temporarily detaches TextChange, so
             * the localized placeholder is never interpreted as a real search query.
             */
            mCurrentQuery = "";
            mCurrentText = SearchPlaceholder;
            mPlaceholderMode = true;

            SetSearchTextEverywhere(
                SearchPlaceholder
            );

            RestoreCatalog();
        }

        private static void RebindMoveObjects(
            ref Button current,
            Button next)
        {
            if (object.ReferenceEquals(
                current,
                next))
            {
                return;
            }

            if (current != null)
            {
                current.Click -=
                    OnMoveObjectsClick;
            }

            current = next;

            if (current != null)
            {
                current.Click -=
                    OnMoveObjectsClick;

                current.Click +=
                    OnMoveObjectsClick;
            }
        }

        private static void OnMoveObjectsClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            /*
             * Some stable Buy versions keep the MoveObjects helpers private.
             * Build therefore sends the same native cheat command directly,
             * then updates the SAME private static sEnabled field used by Buy.
             *
             * Result:
             * - no dependency on public helper methods
             * - Buy/Build checkbox state stays synchronized
             * - the working Buy MoveObjectsToggle class remains untouched
             */
            bool enabled =
                eventArgs.ButtonSelected;

            CommandSystem.ExecuteCommandString(
                enabled
                    ? "moveObjects on"
                    : "moveObjects off"
            );

            if (SharedMoveObjectsStateField != null)
            {
                SharedMoveObjectsStateField.SetValue(
                    null,
                    enabled
                );
            }

            SyncMoveObjectsButtons();
        }

        private static bool GetSharedMoveObjectsState()
        {
            if (SharedMoveObjectsStateField == null)
            {
                return false;
            }

            object value =
                SharedMoveObjectsStateField.GetValue(
                    null
                );

            return
                value is bool &&
                (bool)value;
        }

        private static void SyncMoveObjectsButtons()
        {
            bool enabled =
                GetSharedMoveObjectsState();

            string tooltip =
                Localization.LocalizeString(
                    "Jojobou/BetterBuild/MoveObjectsToggle:Tooltip",
                    new object[0]
                );

            SyncMoveButton(
                mNormalMoveObjects,
                enabled,
                tooltip
            );

            SyncMoveButton(
                mExpandedMoveObjects,
                enabled,
                tooltip
            );
        }

        private static void SyncMoveButton(
            Button button,
            bool enabled,
            string tooltip)
        {
            if (button == null)
            {
                return;
            }

            button.Selected =
                enabled;

            button.TooltipText =
                tooltip;
        }

        private static void OnExpandChanged()
        {
            RefreshBindings();
            UpdateVisibility();
            SyncMoveObjectsButtons();
        }

        private static void UpdateVisibility()
        {
            if (mExpandableGrid == null)
            {
                return;
            }

            bool expanded =
                mExpandableGrid.Expanded;

            /*
             * The B button follows ONLY the catalog layout.
             * It must stay available even while the Better Build interface
             * itself is hidden.
             */
            // Never show both B buttons at once.
            if (mNormalInterfaceToggle != null)
            {
                mNormalInterfaceToggle.Visible =
                    !expanded;
            }

            if (mExpandedInterfaceToggle != null)
            {
                mExpandedInterfaceToggle.Visible =
                    expanded;
            }

            /*
             * All OTHER normal Better Build controls must respect the shared
             * InterfaceToggle state. This prevents BuildCatalogUI from
             * making them visible again after the player hides the interface
             * and then changes category or Normal / Expanded state.
             */
            bool normalControlsVisible =
                !expanded &&
                BetterBuildInterfaceToggle.InterfaceVisible;

            if (mNativeFilterButton != null)
            {
                mNativeFilterButton.Visible =
                    normalControlsVisible;

                if (!normalControlsVisible)
                {
                    mNativeFilterButton.Selected =
                        false;
                }
            }

            if (mNormalSearchWindow != null)
            {
                mNormalSearchWindow.Visible =
                    normalControlsVisible;
            }

            if (mNormalMoveObjects != null)
            {
                mNormalMoveObjects.Visible =
                    normalControlsVisible;
            }

            if (mNormalAllCatalog != null)
            {
                mNormalAllCatalog.Visible =
                    normalControlsVisible;
            }

            if (mNormalSave != null)
            {
                mNormalSave.Visible =
                    normalControlsVisible;
            }

            /*
             * The DLC/CC popup itself is one native shared panel.
             * Close it whenever its normal-mode button is not available.
             */
            if (!normalControlsVisible &&
                mController != null &&
                CatalogProductFilterField != null)
            {
                CatalogProductFilter panel =
                    CatalogProductFilterField.GetValue(
                        mController
                    ) as CatalogProductFilter;

                if (panel != null)
                {
                    panel.Visible = false;
                }
            }
        }

        private static void OnTextChange(
            WindowBase sender,
            UITextChangeEventArgs eventArgs)
        {
            TextEdit textEdit =
                sender as TextEdit;

            if (textEdit == null ||
                mController == null ||
                mUpdatingSearchBoxes)
            {
                return;
            }

            string rawText =
                textEdit.Caption;

            if (rawText == null)
            {
                rawText = "";
            }

            mCurrentText = rawText;

            SyncSearchBoxes(
                textEdit,
                rawText
            );

            /*
             * Same manual placeholder behavior as the stable Buy search.
             */
            if (mPlaceholderMode)
            {
                if (rawText.Length == 0)
                {
                    mPlaceholderMode = false;
                    mCurrentQuery = "";
                    RestoreCatalog();
                    return;
                }

                if (SearchPlaceholder.StartsWith(
                        rawText,
                        StringComparison.Ordinal))
                {
                    mCurrentQuery = "";
                    return;
                }

                mPlaceholderMode = false;
            }

            string query =
                rawText.Trim();

            mCurrentQuery =
                query;

            if (query.Length == 0)
            {
                RestoreCatalog();
                return;
            }

            SearchCatalog(
                query
            );
        }

        private static void OnSearchFocusAcquired(
            WindowBase sender,
            UIFocusChangeEventArgs eventArgs)
        {
            TextEdit textEdit =
                sender as TextEdit;

            if (textEdit == null ||
                mUpdatingSearchBoxes ||
                textEdit.Caption != SearchPlaceholder)
            {
                return;
            }

            /*
             * The placeholder is visual only. As soon as the user focuses
             * the search box, switch to a real empty input.
             */
            mCurrentText = "";
            mPlaceholderMode = false;

            SetSearchTextEverywhere(
                ""
            );
        }

        private static void OnSearchFocusLost(
            WindowBase sender,
            UIFocusChangeEventArgs eventArgs)
        {
            TextEdit textEdit =
                sender as TextEdit;

            if (textEdit == null ||
                mUpdatingSearchBoxes ||
                !String.IsNullOrEmpty(
                    mCurrentQuery) ||
                !String.IsNullOrEmpty(
                    textEdit.Caption))
            {
                return;
            }

            mCurrentText =
                SearchPlaceholder;

            mPlaceholderMode =
                true;

            SetSearchTextEverywhere(
                SearchPlaceholder
            );
        }

        private static void SyncSearchBoxes(
            TextEdit source,
            string text)
        {
            mUpdatingSearchBoxes =
                true;

            try
            {
                SyncSearchBox(
                    mNormalSearch,
                    source,
                    text
                );

                SyncSearchBox(
                    mExpandedSearch,
                    source,
                    text
                );
            }
            finally
            {
                mUpdatingSearchBoxes =
                    false;
            }
        }

        private static void SyncSearchBox(
            TextEdit edit,
            TextEdit source,
            string text)
        {
            if (edit == null ||
                edit == source ||
                edit.Caption == text)
            {
                return;
            }

            edit.TextChange -=
                OnTextChange;

            edit.Caption = text;

            edit.TextChange +=
                OnTextChange;
        }

        private static void SetSearchTextEverywhere(
            string text)
        {
            mUpdatingSearchBoxes =
                true;

            try
            {
                SetSearchText(
                    mNormalSearch,
                    text
                );

                SetSearchText(
                    mExpandedSearch,
                    text
                );
            }
            finally
            {
                mUpdatingSearchBoxes =
                    false;
            }
        }

        private static void SetSearchText(
            TextEdit edit,
            string text)
        {
            if (edit == null)
            {
                return;
            }

            edit.TextChange -=
                OnTextChange;

            edit.Caption = text;

            edit.TextChange +=
                OnTextChange;
        }

        private static void SearchCatalog(
            string query)
        {
            if (mController == null ||
                mCatalogGrid == null ||
                GetProductListMethod == null ||
                GetPresetListMethod == null ||
                PopulateCatalogMethod == null)
            {
                return;
            }

            try
            {
                object tool =
                    GetFieldValue(
                        CurrentCatalogToolField
                    );

                object productType =
                    GetFieldValue(
                        CurrentCatalogProductField
                    );

                uint category =
                    GetUIntField(
                        CatalogFilterField
                    );

                if (tool == null ||
                    productType == null)
                {
                    return;
                }

                List<string> words =
                    ParseQuery(
                        query
                    );

                List<object> currentPresets =
                    GetListField(
                        PresetListField
                    );

                if (currentPresets != null)
                {
                    List<object> source =
                        GetPresetListMethod.Invoke(
                            mController,
                            new object[]
                            {
                                tool,
                                productType,
                                category
                            }
                        ) as List<object>;

                    List<object> filtered =
                        FilterItems(
                            source,
                            words
                        );

                    SetFieldValue(
                        PresetListField,
                        filtered
                    );

                    SetFieldValue(
                        ProductListField,
                        null
                    );

                    SetFieldValue(
                        UnfilteredProductListField,
                        null
                    );
                }
                else
                {
                    List<object> source =
                        GetProductListMethod.Invoke(
                            mController,
                            new object[]
                            {
                                tool,
                                productType,
                                category
                            }
                        ) as List<object>;

                    List<object> filtered =
                        FilterItems(
                            source,
                            words
                        );

                    SetFieldValue(
                        ProductListField,
                        filtered
                    );

                    SetFieldValue(
                        UnfilteredProductListField,
                        new List<object>(
                            filtered
                        )
                    );

                    SetFieldValue(
                        PresetListField,
                        null
                    );
                }

                mCatalogGrid.SelectedItem =
                    -1;

                PopulateCatalogMethod.Invoke(
                    mController,
                    new object[]
                    {
                        mCatalogGrid,
                        productType,
                        category
                    }
                );
            }
            catch
            {
                /*
                 * Search must never break Build mode.
                 * A failed search simply leaves the current catalog state.
                 */
            }
        }

        private static void RestoreCatalog()
        {
            if (mController == null ||
                RebuildCatalogMethod == null)
            {
                return;
            }

            try
            {
                RebuildCatalogMethod.Invoke(
                    mController,
                    null
                );
            }
            catch
            {
            }
        }

        private static List<object> FilterItems(
            List<object> source,
            List<string> words)
        {
            List<object> result =
                new List<object>();

            if (source == null)
            {
                return result;
            }

            foreach (object item in source)
            {
                BuildBuyProduct product =
                    item as BuildBuyProduct;

                if (product == null)
                {
                    BuildBuyPreset preset =
                        item as BuildBuyPreset;

                    if (preset != null)
                    {
                        product =
                            preset.Product;
                    }
                }

                if (product != null &&
                    ProductMatches(
                        product,
                        words))
                {
                    result.Add(
                        item
                    );
                }
            }

            return result;
        }

        private static bool ProductMatches(
            BuildBuyProduct product,
            List<string> words)
        {
            string searchableName;

            if (!sSearchableNameCache.TryGetValue(
                    product,
                    out searchableName))
            {
                string localizedName =
                    Responder.Instance.LocalizationModel
                        .FillInTokens(
                            false,
                            product.CatalogName,
                            new object[0]
                        );

                searchableName =
                    NormalizeText(
                        localizedName
                    );

                sSearchableNameCache[product] =
                    searchableName;
            }

            foreach (string word in words)
            {
                if (searchableName.IndexOf(
                        word,
                        StringComparison.Ordinal) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static List<string> ParseQuery(
            string query)
        {
            List<string> words =
                new List<string>();

            string[] pieces =
                query.Split(
                    new char[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries
                );

            foreach (string piece in pieces)
            {
                string word =
                    NormalizeText(
                        piece
                    );

                if (word.Length > 0)
                {
                    words.Add(
                        word
                    );
                }
            }

            return words;
        }

        private static string NormalizeText(
            string text)
        {
            if (String.IsNullOrEmpty(
                    text))
            {
                return "";
            }

            return
                text.Trim()
                    .ToLowerInvariant();
        }

        private static object GetFieldValue(
            FieldInfo field)
        {
            if (field == null ||
                mController == null)
            {
                return null;
            }

            return
                field.GetValue(
                    mController
                );
        }

        private static uint GetUIntField(
            FieldInfo field)
        {
            object value =
                GetFieldValue(
                    field
                );

            if (value is uint)
            {
                return
                    (uint)value;
            }

            return 0U;
        }

        private static List<object> GetListField(
            FieldInfo field)
        {
            object value =
                GetFieldValue(
                    field
                );

            return
                value as List<object>;
        }

        private static void SetFieldValue(
            FieldInfo field,
            object value)
        {
            if (field == null ||
                mController == null)
            {
                return;
            }

            field.SetValue(
                mController,
                value
            );
        }

        private static void OnControllerDetach(
            WindowBase sender,
            UIEventArgs eventArgs)
        {
            DetachAll();
        }

        private static void DetachCurrentCatalog()
        {
            UnbindSearch(
                ref mNormalSearch
            );

            UnbindSearch(
                ref mExpandedSearch
            );

            UnbindClearSearch(
                ref mNormalClearSearch
            );

            UnbindClearSearch(
                ref mExpandedClearSearch
            );

            UnbindMoveObjects(
                ref mNormalMoveObjects
            );

            UnbindMoveObjects(
                ref mExpandedMoveObjects
            );

            if (mExpandableGrid != null)
            {
                mExpandableGrid.ExpandChanged -=
                    OnExpandChanged;
            }

            mCatalogGrid = null;
            mCatalogWindow = null;
            mExpandableGrid = null;
            mNativeFilterButton = null;

            mNormalSearchWindow = null;
            mNormalAllCatalog = null;
            mExpandedAllCatalog = null;
            if (mNormalSave != null)
            {
                mNormalSave.Click -= OnSaveClick;
            }

            if (mExpandedSave != null)
            {
                mExpandedSave.Click -= OnSaveClick;
            }

            mNormalSave = null;
            mExpandedSave = null;
            mNormalInterfaceToggle = null;
            mExpandedInterfaceToggle = null;
        }

        private static void DetachAll()
        {
            DetachCurrentCatalog();

            if (mController != null)
            {
                mController.Detach -=
                    OnControllerDetach;
            }

            mController = null;

            mCurrentQuery = "";
            mCurrentText = SearchPlaceholder;
            mPlaceholderMode = true;
            mUpdatingSearchBoxes = false;

            sSearchableNameCache.Clear();
        }

        private static void UnbindSearch(
            ref TextEdit textEdit)
        {
            if (textEdit != null)
            {
                textEdit.TextChange -=
                    OnTextChange;

                textEdit.FocusAcquired -=
                    OnSearchFocusAcquired;

                textEdit.FocusLost -=
                    OnSearchFocusLost;
            }

            textEdit = null;
        }

        private static void UnbindClearSearch(
            ref Button button)
        {
            if (button != null)
            {
                button.Click -=
                    OnClearSearchClick;
            }

            button = null;
        }

        private static void UnbindMoveObjects(
            ref Button button)
        {
            if (button != null)
            {
                button.Click -=
                    OnMoveObjectsClick;
            }

            button = null;
        }
    }
}