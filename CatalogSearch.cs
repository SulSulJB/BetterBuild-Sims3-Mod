using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.SimIFace.BuildBuy;
using Sims3.UI;

namespace BetterBuildByJojobou
{
    public class CatalogSearch
    {
        private const uint RoomWindowID = 0x00002000U;
        private const uint FunctionWindowID = 0x00002001U;
        private const uint CollectionWindowID = 0x00000410U;

        private const uint NormalSearchID = 0x0bc50001U;
        private const uint ExpandedSearchID = 0x00000204U;

        private const uint NormalClearSearchButtonID = 0x0bc50030U;
        private const uint ExpandedClearSearchButtonID = 0x00000208U;

        private const uint NormalAllCatalogButtonID = 0x0bc50002U;
        private const uint ExpandedAllCatalogButtonID = 0x00000205U;

        private static string SearchPlaceholder
        {
            get
            {
                return Localize("SearchPlaceholder");
            }
        }

        private bool mUpdatingSearchBoxes = false;

        // Search state is kept separate from the search engine.
        // The placeholder is intentionally handled as manual text:
        // it never disappears on hover and only changes on focus.
        // No Task, timer, or polling.
        private string mCurrentQuery = "";
        private string mCurrentText = SearchPlaceholder;
        private bool mPlaceholderMode = true;

        private TextEdit mRoomNormalSearch;
        private TextEdit mRoomExpandedSearch;
        private TextEdit mFunctionNormalSearch;
        private TextEdit mFunctionExpandedSearch;
        private TextEdit mCollectionNormalSearch;

        private Button mRoomNormalClearSearchButton;
        private Button mRoomExpandedClearSearchButton;
        private Button mFunctionNormalClearSearchButton;
        private Button mFunctionExpandedClearSearchButton;
        private Button mCollectionNormalClearSearchButton;

        private Button mRoomNormalAllCatalogButton;
        private Button mRoomExpandedAllCatalogButton;
        private Button mFunctionNormalAllCatalogButton;
        private Button mFunctionExpandedAllCatalogButton;

        private WindowBase mRoomWindow;
        private WindowBase mFunctionWindow;
        private WindowBase mCollectionWindow;
        private ExpandableCatalogGrid mRoomGrid;
        private ExpandableCatalogGrid mFunctionGrid;

        private bool mSearchAllCatalog = false;

        // Cache for the complete Buy catalog.
        private List<object> mAllCatalogProducts;

        private BuyController mController;

        private readonly Dictionary<string, FieldInfo> mFieldInfoCache =
            new Dictionary<string, FieldInfo>();

        private readonly Dictionary<string, MethodInfo> mMethodInfoCache =
            new Dictionary<string, MethodInfo>();

        private readonly Dictionary<BuildBuyProduct, string> mSearchableNameCache =
            new Dictionary<BuildBuyProduct, string>();

        private static readonly Dictionary<string, FieldInfo> sStaticFieldInfoCache =
            new Dictionary<string, FieldInfo>();

        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public;

        private static readonly BindingFlags StaticFlags =
            BindingFlags.Static |
            BindingFlags.NonPublic |
            BindingFlags.Public;

        private static CatalogSearch sActiveSearch;
        private static BuyController sActiveController;

        private CatalogSearch()
        {
        }

        private static string Localize(string key)
        {
            return Localization.LocalizeString(
                "Jojobou/BetterBuild/CatalogSearch:" + key,
                new object[0]
            );
        }

        // Called by BuyControllerPatch. No Task or polling.
        public static void Attach(BuyController controller)
        {
            if (controller == null)
            {
                return;
            }

            if (Object.ReferenceEquals(controller, sActiveController))
            {
                // Each ExpandableCatalogGrid owns its own BuyExpandableCatalogGrid.
                // Only missing or replaced UI references are rebound here;
                // no search is triggered.
                if (sActiveSearch != null)
                {
                    sActiveSearch.RefreshUIBindings();
                }
                return;
            }

            Detach();

            sActiveController = controller;
            sActiveSearch = new CatalogSearch();
            sActiveSearch.BindController(controller);
        }

        public static void Reset()
        {
            Detach();
        }

        private static void Detach()
        {
            if (sActiveSearch != null)
            {
                sActiveSearch.Unbind();
            }

            sActiveSearch = null;
            sActiveController = null;
        }

        private void BindController(BuyController controller)
        {
            mController = controller;

            mController.Detach -= OnControllerDetach;
            mController.Detach += OnControllerDetach;

            mRoomWindow =
                mController.GetChildByID(RoomWindowID, true);

            mFunctionWindow =
                mController.GetChildByID(FunctionWindowID, true);

            mCollectionWindow =
                mController.GetChildByID(CollectionWindowID, true);

            FieldInfo roomGridField =
                typeof(BuyController).GetField("mGridSortByRoom", InstanceFlags);

            if (roomGridField != null)
            {
                mRoomGrid =
                    roomGridField.GetValue(mController) as ExpandableCatalogGrid;
            }

            FieldInfo functionGridField =
                typeof(BuyController).GetField("mGridSortByFunction", InstanceFlags);

            if (functionGridField != null)
            {
                mFunctionGrid =
                    functionGridField.GetValue(mController) as ExpandableCatalogGrid;
            }

            RefreshUIBindings();
        }

        private void RefreshUIBindings()
        {
            if (mController == null)
            {
                return;
            }

            // NORMAL: each control is resolved inside its own window.
            TextEdit roomNormalSearch = null;
            Button roomNormalClearSearch = null;
            Button roomNormalAll = null;
            if (mRoomWindow != null)
            {
                roomNormalSearch =
                    mRoomWindow.GetChildByID(NormalSearchID, true) as TextEdit;
                roomNormalClearSearch =
                    mRoomWindow.GetChildByID(NormalClearSearchButtonID, true) as Button;
                roomNormalAll =
                    mRoomWindow.GetChildByID(NormalAllCatalogButtonID, true) as Button;
            }

            TextEdit functionNormalSearch = null;
            Button functionNormalClearSearch = null;
            Button functionNormalAll = null;
            if (mFunctionWindow != null)
            {
                functionNormalSearch =
                    mFunctionWindow.GetChildByID(NormalSearchID, true) as TextEdit;
                functionNormalClearSearch =
                    mFunctionWindow.GetChildByID(NormalClearSearchButtonID, true) as Button;
                functionNormalAll =
                    mFunctionWindow.GetChildByID(NormalAllCatalogButtonID, true) as Button;
            }

            // Collections use their own normal search controls. Their catalog
            // content is handled separately because EA stores collection items
            // as BuildBuyPreset entries with a different item layout.
            TextEdit collectionNormalSearch = null;
            Button collectionNormalClearSearch = null;
            if (mCollectionWindow != null)
            {
                collectionNormalSearch =
                    mCollectionWindow.GetChildByID(
                        NormalSearchID,
                        true
                    ) as TextEdit;

                collectionNormalClearSearch =
                    mCollectionWindow.GetChildByID(
                        NormalClearSearchButtonID,
                        true
                    ) as Button;
            }

            // EXTENDED: 0x204 / 0x205 exist once per ExpandableCatalogGrid,
            // so Room and Function are resolved separately.
            TextEdit roomExpandedSearch = null;
            Button roomExpandedClearSearch = null;
            Button roomExpandedAll = null;
            if (mRoomGrid != null)
            {
                roomExpandedSearch =
                    mRoomGrid.GetChildByID(ExpandedSearchID, true) as TextEdit;
                roomExpandedClearSearch =
                    mRoomGrid.GetChildByID(ExpandedClearSearchButtonID, true) as Button;
                roomExpandedAll =
                    mRoomGrid.GetChildByID(ExpandedAllCatalogButtonID, true) as Button;
            }

            TextEdit functionExpandedSearch = null;
            Button functionExpandedClearSearch = null;
            Button functionExpandedAll = null;
            if (mFunctionGrid != null)
            {
                functionExpandedSearch =
                    mFunctionGrid.GetChildByID(ExpandedSearchID, true) as TextEdit;
                functionExpandedClearSearch =
                    mFunctionGrid.GetChildByID(ExpandedClearSearchButtonID, true) as Button;
                functionExpandedAll =
                    mFunctionGrid.GetChildByID(ExpandedAllCatalogButtonID, true) as Button;
            }

            RebindTextEdit(ref mRoomNormalSearch, roomNormalSearch);
            RebindTextEdit(ref mRoomExpandedSearch, roomExpandedSearch);
            RebindTextEdit(ref mFunctionNormalSearch, functionNormalSearch);
            RebindTextEdit(ref mFunctionExpandedSearch, functionExpandedSearch);
            RebindTextEdit(
                ref mCollectionNormalSearch,
                collectionNormalSearch
            );

            RebindClearSearchButton(
                ref mRoomNormalClearSearchButton,
                roomNormalClearSearch);
            RebindClearSearchButton(
                ref mRoomExpandedClearSearchButton,
                roomExpandedClearSearch);
            RebindClearSearchButton(
                ref mFunctionNormalClearSearchButton,
                functionNormalClearSearch);
            RebindClearSearchButton(
                ref mFunctionExpandedClearSearchButton,
                functionExpandedClearSearch);
            RebindClearSearchButton(
                ref mCollectionNormalClearSearchButton,
                collectionNormalClearSearch);

            RebindAllCatalogButton(
                ref mRoomNormalAllCatalogButton,
                roomNormalAll);
            RebindAllCatalogButton(
                ref mRoomExpandedAllCatalogButton,
                roomExpandedAll);
            RebindAllCatalogButton(
                ref mFunctionNormalAllCatalogButton,
                functionNormalAll);
            RebindAllCatalogButton(
                ref mFunctionExpandedAllCatalogButton,
                functionExpandedAll);

            SyncAllCatalogButtons();
        }

        private void RebindTextEdit(ref TextEdit current, TextEdit next)
        {
            if (Object.ReferenceEquals(current, next))
            {
                return;
            }

            if (current != null)
            {
                current.TextChange -= OnTextChange;
                current.FocusAcquired -= OnSearchFocusAcquired;
                current.FocusLost -= OnSearchFocusLost;
            }

            current = next;

            if (current == null)
            {
                return;
            }

            current.TextChange -= OnTextChange;
            current.FocusAcquired -= OnSearchFocusAcquired;
            current.FocusLost -= OnSearchFocusLost;

            /*
             * Preserve the visible text exactly as-is:
             * - the localized search placeholder
             * - "Recherch..."
             * - "R"
             * - ""
             * - or a real search query.
             *
             * This prevents an UI rebuild from restoring the placeholder
             * while the user is deleting it character by character.
             */
            if (current.Caption != mCurrentText)
            {
                current.Caption = mCurrentText;
            }

            current.TextChange += OnTextChange;
            current.FocusAcquired += OnSearchFocusAcquired;
            current.FocusLost += OnSearchFocusLost;
        }

        private void OnSearchFocusAcquired(
            WindowBase sender,
            UIFocusChangeEventArgs eventArgs)
        {
            TextEdit textEdit = sender as TextEdit;

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

            SyncSearchBoxes(textEdit, "");

            textEdit.TextChange -= OnTextChange;
            textEdit.Caption = "";
            textEdit.TextChange += OnTextChange;
        }

        /*
         * The placeholder returns only when an empty search box loses focus.
         * It is never removed by hovering.
         *
         * After the user genuinely clears the field and leaves it,
         * the localized search placeholder is restored automatically.
         */
        private void OnSearchFocusLost(
            WindowBase sender,
            UIFocusChangeEventArgs eventArgs)
        {
            TextEdit textEdit = sender as TextEdit;

            if (textEdit == null ||
                mUpdatingSearchBoxes ||
                !String.IsNullOrEmpty(mCurrentQuery) ||
                !String.IsNullOrEmpty(textEdit.Caption))
            {
                return;
            }

            mCurrentText = SearchPlaceholder;
            mPlaceholderMode = true;

            TextEdit[] edits = new TextEdit[]
            {
                mRoomNormalSearch,
                mRoomExpandedSearch,
                mFunctionNormalSearch,
                mFunctionExpandedSearch,
                mCollectionNormalSearch
            };

            mUpdatingSearchBoxes = true;

            try
            {
                foreach (TextEdit edit in edits)
                {
                    if (edit == null)
                    {
                        continue;
                    }

                    edit.TextChange -= OnTextChange;
                    edit.Caption = SearchPlaceholder;
                    edit.TextChange += OnTextChange;
                }
            }
            finally
            {
                mUpdatingSearchBoxes = false;
            }
        }

        private void RebindClearSearchButton(ref Button current, Button next)
        {
            if (Object.ReferenceEquals(current, next))
            {
                return;
            }

            if (current != null)
            {
                current.Click -= OnClearSearchButtonClick;
            }

            current = next;

            if (current != null)
            {
                current.TooltipText =
                    Localize("ClearSearchTooltip");

                current.Click -= OnClearSearchButtonClick;
                current.Click += OnClearSearchButtonClick;
            }
        }

        private void OnClearSearchButtonClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            if (mController == null)
            {
                return;
            }

            /*
             * The X returns every Buy search box to the normal placeholder.
             * TextChange is detached while the placeholder is written, so
             * the localized search placeholder is never interpreted as a real search query.
             */
            mCurrentQuery = "";
            mCurrentText = SearchPlaceholder;
            mPlaceholderMode = true;

            TextEdit[] edits = new TextEdit[]
            {
                mRoomNormalSearch,
                mRoomExpandedSearch,
                mFunctionNormalSearch,
                mFunctionExpandedSearch,
                mCollectionNormalSearch
            };

            mUpdatingSearchBoxes = true;

            try
            {
                foreach (TextEdit edit in edits)
                {
                    if (edit == null)
                    {
                        continue;
                    }

                    edit.TextChange -= OnTextChange;
                    edit.Caption = SearchPlaceholder;
                    edit.TextChange += OnTextChange;
                }
            }
            finally
            {
                mUpdatingSearchBoxes = false;
            }

            RestoreCatalog();
        }

        private void RebindAllCatalogButton(ref Button current, Button next)
        {
            if (Object.ReferenceEquals(current, next))
            {
                return;
            }

            if (current != null)
            {
                current.Click -= OnAllCatalogButtonClick;
            }

            current = next;

            if (current != null)
            {
                current.TooltipText = Localize("AllCatalogTooltip");
                current.Click -= OnAllCatalogButtonClick;
                current.Click += OnAllCatalogButtonClick;
            }
        }

        private void OnControllerDetach(
            WindowBase sender,
            UIEventArgs eventArgs)
        {
            Detach();
        }

        private void Unbind()
        {
            if (mController != null)
            {
                mController.Detach -= OnControllerDetach;
            }

            DetachTextEdit(mRoomNormalSearch);
            DetachTextEdit(mRoomExpandedSearch);
            DetachTextEdit(mFunctionNormalSearch);
            DetachTextEdit(mFunctionExpandedSearch);
            DetachTextEdit(mCollectionNormalSearch);

            DetachClearSearchButton(mRoomNormalClearSearchButton);
            DetachClearSearchButton(mRoomExpandedClearSearchButton);
            DetachClearSearchButton(mFunctionNormalClearSearchButton);
            DetachClearSearchButton(mFunctionExpandedClearSearchButton);
            DetachClearSearchButton(mCollectionNormalClearSearchButton);

            DetachAllCatalogButton(mRoomNormalAllCatalogButton);
            DetachAllCatalogButton(mRoomExpandedAllCatalogButton);
            DetachAllCatalogButton(mFunctionNormalAllCatalogButton);
            DetachAllCatalogButton(mFunctionExpandedAllCatalogButton);

            mRoomNormalSearch = null;
            mRoomExpandedSearch = null;
            mFunctionNormalSearch = null;
            mFunctionExpandedSearch = null;
            mCollectionNormalSearch = null;

            mRoomNormalClearSearchButton = null;
            mRoomExpandedClearSearchButton = null;
            mFunctionNormalClearSearchButton = null;
            mFunctionExpandedClearSearchButton = null;
            mCollectionNormalClearSearchButton = null;

            mRoomNormalAllCatalogButton = null;
            mRoomExpandedAllCatalogButton = null;
            mFunctionNormalAllCatalogButton = null;
            mFunctionExpandedAllCatalogButton = null;

            mRoomWindow = null;
            mFunctionWindow = null;
            mCollectionWindow = null;
            mRoomGrid = null;
            mFunctionGrid = null;
            mController = null;

            mCurrentQuery = "";
            mCurrentText = SearchPlaceholder;
            mPlaceholderMode = true;

            if (mAllCatalogProducts != null)
            {
                mAllCatalogProducts.Clear();
                mAllCatalogProducts = null;
            }

            mSearchableNameCache.Clear();
            mFieldInfoCache.Clear();
            mMethodInfoCache.Clear();
        }

        private void DetachTextEdit(TextEdit textEdit)
        {
            if (textEdit != null)
            {
                textEdit.TextChange -= OnTextChange;
                textEdit.FocusAcquired -= OnSearchFocusAcquired;
                textEdit.FocusLost -= OnSearchFocusLost;
            }
        }

        private void DetachClearSearchButton(Button button)
        {
            if (button != null)
            {
                button.Click -= OnClearSearchButtonClick;
            }
        }

        private void DetachAllCatalogButton(Button button)
        {
            if (button != null)
            {
                button.Click -= OnAllCatalogButtonClick;
            }
        }

        private void OnAllCatalogButtonClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            mSearchAllCatalog = eventArgs.ButtonSelected;
            SyncAllCatalogButtons();

            string query = GetCurrentQuery();

            if (query.Length == 0)
            {
                return;
            }

            SearchCatalog(query);
        }

        private void SyncAllCatalogButtons()
        {
            SetButtonSelected(mRoomNormalAllCatalogButton, mSearchAllCatalog);
            SetButtonSelected(mRoomExpandedAllCatalogButton, mSearchAllCatalog);
            SetButtonSelected(mFunctionNormalAllCatalogButton, mSearchAllCatalog);
            SetButtonSelected(mFunctionExpandedAllCatalogButton, mSearchAllCatalog);
        }

        private void SetButtonSelected(Button button, bool selected)
        {
            if (button != null)
            {
                button.Selected = selected;
            }
        }

        private string GetCurrentQuery()
        {
            return mCurrentQuery == null
                ? ""
                : mCurrentQuery.Trim();
        }

        private void OnTextChange(
            WindowBase sender,
            UITextChangeEventArgs eventArgs)
        {
            TextEdit textEdit = sender as TextEdit;

            if (textEdit == null ||
                mController == null ||
                mUpdatingSearchBoxes)
            {
                return;
            }

            string rawText = textEdit.Caption;

            if (rawText == null)
            {
                rawText = "";
            }

            /*
             * Always store exactly what the user currently sees.
             * This value survives Normal/Extended UI rebuilds.
             */
            mCurrentText = rawText;

            SyncSearchBoxes(textEdit, rawText);

            /*
             * MANUAL PLACEHOLDER MODE
             *
             * While the user deletes the localized placeholder from right to left,
             * no search is triggered.
             *
             * Examples ignored as search queries:
             * localized placeholder text and its progressively shortened forms
             *
             * Once the text reaches "", placeholder mode ends.
             * The next "R" typed by the user becomes a real search query.
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

                /*
                 * The user replaced or selected the placeholder
                 * and entered different text directly.
                 */
                mPlaceholderMode = false;
            }

            string query = rawText.Trim();
            mCurrentQuery = query;

            if (query.Length == 0)
            {
                RestoreCatalog();
                return;
            }

            if (object.ReferenceEquals(
                    textEdit,
                    mCollectionNormalSearch))
            {
                // Keep Collections isolated from the normal Buy product path.
                // EA uses BuildBuyPreset entries for collection contents.
                if (mSearchAllCatalog)
                {
                    mSearchAllCatalog = false;
                    SyncAllCatalogButtons();
                }

                SearchCollection(query);
                return;
            }

            SearchCatalog(query);
        }

        private void SyncSearchBoxes(TextEdit source, string text)
        {
            TextEdit[] edits = new TextEdit[]
            {
                mRoomNormalSearch,
                mRoomExpandedSearch,
                mFunctionNormalSearch,
                mFunctionExpandedSearch,
                mCollectionNormalSearch
            };

            mUpdatingSearchBoxes = true;

            try
            {
                foreach (TextEdit edit in edits)
                {
                    if (edit == null ||
                        edit == source ||
                        edit.Caption == text)
                    {
                        continue;
                    }

                    edit.TextChange -= OnTextChange;
                    edit.Caption = text;
                    edit.TextChange += OnTextChange;
                }
            }
            finally
            {
                mUpdatingSearchBoxes = false;
            }
        }

        private void SearchCatalog(string query)
        {
            try
            {
                uint category;
                uint buildCategory;
                ulong subCategory;
                ulong subCategory2;
                ulong roomSubCategory;
                uint roomCategory;
                uint excludedRoomFlags;
                uint categoryShowAllFlag = 0U;

                if (mSearchAllCatalog)
                {
                    // Global search: request every Buy catalog object from EA
                    // without Function or Room filters.
                    category =
                        GetField<uint>(
                            mController,
                            "BUY_CATEGORY_ALL"
                        );

                    buildCategory =
                        GetField<uint>(
                            mController,
                            "BUILD_CATEGORY_ALL"
                        );

                    subCategory =
                        GetField<ulong>(
                            mController,
                            "BUY_SUBCATEGORY_ALL"
                        );

                    subCategory2 =
                        GetField<ulong>(
                            mController,
                            "BUY_SUBCATEGORY2_ALL"
                        );

                    roomSubCategory =
                        GetField<ulong>(
                            mController,
                            "BUY_ROOM_SUBCATEGORY_ALL"
                        );

                    roomCategory =
                        GetField<uint>(
                            mController,
                            "ROOM_CATEGORY_ALL"
                        );

                    // In global mode, no Room category is excluded.
                    excludedRoomFlags = 0U;
                }
                else
                {
                    // Standard behavior: search within the category or Room
                    // currently selected by the player.
                    category =
                        GetField<uint>(
                            mController,
                            "mCurrCategoryFilter"
                        );

                    buildCategory =
                        GetField<uint>(
                            mController,
                            "BUILD_CATEGORY_ALL"
                        );

                    subCategory =
                        GetField<ulong>(
                            mController,
                            "mCurrSubCategoryFilter"
                        );

                    subCategory2 =
                        GetField<ulong>(
                            mController,
                            "mCurrSubCategory2Filter"
                        );

                    roomSubCategory =
                        GetField<ulong>(
                            mController,
                            "mCurrRoomSubCategoryFilter"
                        );

                    roomCategory =
                        GetField<uint>(
                            mController,
                            "mCurrRoomFilter"
                        );

                    excludedRoomFlags =
                        GetField<uint>(
                            mController,
                            "mRoomCategoryFlagsToExclude"
                        );

                    ulong buySubCategoryAll =
                        GetField<ulong>(
                            mController,
                            "BUY_SUBCATEGORY_ALL"
                        );

                    ulong buyRoomSubCategoryAll =
                        GetField<ulong>(
                            mController,
                            "BUY_ROOM_SUBCATEGORY_ALL"
                        );

                    uint roomCategoryAll =
                        GetField<uint>(
                            mController,
                            "ROOM_CATEGORY_ALL"
                        );

                    if (subCategory != buySubCategoryAll &&
                        roomSubCategory != buyRoomSubCategoryAll &&
                        roomCategory != roomCategoryAll)
                    {
                        MethodInfo getCategoryFlagMethod =
                            GetControllerMethod("GetCatFlagForShowAllItemsSubCat");

                        if (getCategoryFlagMethod != null)
                        {
                            categoryShowAllFlag =
                                (uint)getCategoryFlagMethod.Invoke(
                                    mController,
                                    new object[]
                                    {
                                        category,
                                        subCategory,
                                        subCategory2
                                    }
                                );
                        }
                    }
                }

                List<object> products;

                if (mSearchAllCatalog && mAllCatalogProducts != null)
                {
                    // The full catalog has already been cached,
                    // avoiding an expensive rebuild on every keystroke.
                    products = mAllCatalogProducts;
                }
                else if (!mSearchAllCatalog &&
                         subCategory == GetField<ulong>(mController, "BUY_SUBCATEGORY_ALL") &&
                         roomSubCategory == GetField<ulong>(mController, "BUY_ROOM_SUBCATEGORY_ALL"))
                {
                    // Sort by Function has a special case in EA's BuyController:
                    // when the current subcategory is ALL, EA iterates through the
                    // Function tabs instead of treating the mask as a normal category.
                    products = GetFunctionProducts(
                        category,
                        buildCategory,
                        subCategory2,
                        roomSubCategory,
                        roomCategory,
                        excludedRoomFlags,
                        categoryShowAllFlag
                    );
                }
                else
                {
                    products =
                        UserToolUtils.GetObjectProductListFiltered(
                            category,
                            buildCategory,
                            subCategory,
                            subCategory2,
                            roomSubCategory,
                            0UL,
                            roomCategory,
                            excludedRoomFlags,
                            categoryShowAllFlag
                        );

                    if (mSearchAllCatalog && products != null)
                    {
                        mAllCatalogProducts = products;
                    }
                }

                if (products == null)
                {
                    return;
                }

                List<string> words =
                    ParseQuery(query);

                List<object> results =
                    new List<object>();

                foreach (object obj in products)
                {
                    BuildBuyProduct product =
                        obj as BuildBuyProduct;

                    if (product == null)
                    {
                        continue;
                    }

                    if (ProductMatches(product, words))
                    {
                        results.Add(product);
                    }
                }

                DisplayResults(
                    results,
                    false
                );
            }
            catch (Exception exception)
            {
                StyledNotification.Show(
                    new StyledNotification.Format(
                        Localize("SearchErrorPrefix") +
                        exception.Message,
                        StyledNotification.NotificationStyle.kGameMessageNegative
                    )
                );
            }
        }

        private void SearchCollection(string query)
        {
            try
            {
                ulong collectionId =
                    BuyController.CollectionID();

                if (collectionId == 0UL)
                {
                    RestoreCatalog();
                    return;
                }

                List<object> products =
                    UserToolUtils.GetCollectionPresetList(
                        collectionId
                    );

                if (products == null)
                {
                    return;
                }

                // Match EA's Collections path by applying the current
                // DLC/CC product filter before the text search.
                CatalogProductFilter filter =
                    GetField<CatalogProductFilter>(
                        mController,
                        "mCatalogProductFilter"
                    );

                if (filter != null)
                {
                    bool filtered;
                    filter.FilterObjects(
                        products,
                        out filtered
                    );
                }

                List<string> words =
                    ParseQuery(query);

                List<object> results =
                    new List<object>();

                foreach (object obj in products)
                {
                    BuildBuyPreset preset =
                        obj as BuildBuyPreset;

                    if (preset == null ||
                        preset.Product == null)
                    {
                        continue;
                    }

                    if (ProductMatches(
                            preset.Product,
                            words))
                    {
                        results.Add(preset);
                    }
                }

                DisplayResults(
                    results,
                    true
                );
            }
            catch (Exception exception)
            {
                StyledNotification.Show(
                    new StyledNotification.Format(
                        Localize("SearchErrorPrefix") +
                        exception.Message,
                        StyledNotification.NotificationStyle
                            .kGameMessageNegative
                    )
                );
            }
        }

        private List<object> GetFunctionProducts(
            uint category,
            uint buildCategory,
            ulong subCategory2,
            ulong roomSubCategory,
            uint roomCategory,
            uint excludedRoomFlags,
            uint categoryShowAllFlag)
        {
            List<object> products = new List<object>();

            // TabContainer is internal/non-public in Sims3.UI,
            // so retrieve it as WindowBase through reflection.
            WindowBase functionTabs =
                GetField<WindowBase>(
                    mController,
                    "mTabContainerSortByFunction"
                );

            if (functionTabs == null)
            {
                return products;
            }

            byte highestBit =
                GetField<byte>(
                    mController,
                    "BUY_SUBCATEGORY_HIGHEST_BIT"
                );

            ulong buySubCategoryAll =
                GetField<ulong>(
                    mController,
                    "BUY_SUBCATEGORY_ALL"
                );

            uint index = 0U;
            WindowBase child = functionTabs.GetChildByIndex(index);

            while (child != null)
            {
                TabControl tab = child as TabControl;

                if (tab != null && tab.Tag != null)
                {
                    FieldInfo flagBitField =
                        tab.Tag.GetType().GetField(
                            "mFlagBit",
                            InstanceFlags
                        );

                    if (flagBitField != null)
                    {
                        uint flagBit =
                            Convert.ToUInt32(
                                flagBitField.GetValue(tab.Tag)
                            );

                        ulong currentSubCategory = buySubCategoryAll;
                        ulong currentSubCategory2 = subCategory2;

                        if (flagBit <= highestBit)
                        {
                            currentSubCategory =
                                1UL << (int)flagBit;
                        }
                        else
                        {
                            currentSubCategory2 =
                                1UL << (int)(flagBit - highestBit);
                        }

                        List<object> tabProducts =
                            UserToolUtils.GetObjectProductListFiltered(
                                category,
                                buildCategory,
                                currentSubCategory,
                                currentSubCategory2,
                                roomSubCategory,
                                0UL,
                                roomCategory,
                                excludedRoomFlags,
                                categoryShowAllFlag
                            );

                        AddUniqueProducts(products, tabProducts);
                    }
                }

                index++;
                child = functionTabs.GetChildByIndex(index);
            }

            // Safety fallback: if no usable tab is found,
            // fall back to EA's normal query instead of showing zero results.
            if (products.Count == 0)
            {
                products =
                    UserToolUtils.GetObjectProductListFiltered(
                        category,
                        buildCategory,
                        buySubCategoryAll,
                        subCategory2,
                        roomSubCategory,
                        0UL,
                        roomCategory,
                        excludedRoomFlags,
                        categoryShowAllFlag
                    );
            }

            return products;
        }

        private void AddUniqueProducts(
            List<object> destination,
            List<object> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (object item in source)
            {
                if (!destination.Contains(item))
                {
                    destination.Add(item);
                }
            }
        }

        private List<string> ParseQuery(string query)
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
                    NormalizeText(piece);

                if (word.Length > 0)
                {
                    words.Add(word);
                }
            }

            return words;
        }

        private bool ProductMatches(
            BuildBuyProduct product,
            List<string> words)
        {
            string searchableName;

            if (!mSearchableNameCache.TryGetValue(product, out searchableName))
            {
                string localizedName =
                    Responder.Instance.LocalizationModel.FillInTokens(
                        false,
                        product.CatalogName,
                        new object[0]
                    );

                searchableName = NormalizeText(localizedName);
                mSearchableNameCache[product] = searchableName;
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

        private string NormalizeText(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return "";
            }

            return text
                .Trim()
                .ToLowerInvariant();
        }

        private void DisplayResults(
            List<object> results,
            bool collectionMode)
        {
            ItemGrid catalogGrid =
                GetField<ItemGrid>(
                    mController,
                    "mCatalogGrid"
                );

            if (catalogGrid == null)
            {
                return;
            }

            IList cache =
                GetField<object>(
                    mController,
                    collectionMode
                        ? "mBuildCatalogPatternGridItemCache"
                        : "mBuyCatalogGridItemCache"
                ) as IList;

            if (cache != null)
            {
                foreach (
                    ItemGridCellItem item
                    in catalogGrid.Items)
                {
                    // ItemGridCellItem is a struct,
                    // so only its window needs to be checked.
                    if (item.mWin != null)
                    {
                        cache.Add(item.mWin);
                    }
                }
            }

            SetField<uint>(
                mController,
                "mNumObjectsInGrid",
                0U
            );

            catalogGrid.Clear();

            ResourceKey layoutKey =
                GetStaticField<ResourceKey>(
                    typeof(BuyController),
                    collectionMode
                        ? "kBuildCatalogPatternItemKey"
                        : "kBuyCatalogItemKey"
                );

            MethodInfo resizeMethod =
                GetControllerMethod("ResizeCatalogGrid");

            if (resizeMethod != null)
            {
                resizeMethod.Invoke(
                    mController,
                    new object[]
                    {
                        catalogGrid,
                        results.Count
                    }
                );
            }

            MethodInfo populateMethod =
                GetControllerMethod("PopulateGrid");

            if (populateMethod != null)
            {
                populateMethod.Invoke(
                    mController,
                    new object[]
                    {
                        results,
                        layoutKey
                    }
                );
            }
        }

        private void RestoreCatalog()
        {
            MethodInfo method =
                GetControllerMethod("RepopulateCatalogGrid");

            if (method != null)
            {
                method.Invoke(
                    mController,
                    null
                );
            }
        }

        private FieldInfo GetInstanceFieldInfo(
            object instance,
            string fieldName)
        {
            FieldInfo field;

            if (mFieldInfoCache.TryGetValue(fieldName, out field))
            {
                return field;
            }

            field =
                instance.GetType().GetField(
                    fieldName,
                    InstanceFlags
                );

            if (field == null)
            {
                throw new Exception(
                    Localize("FieldNotFound") +
                    fieldName
                );
            }

            mFieldInfoCache[fieldName] = field;
            return field;
        }

        private MethodInfo GetControllerMethod(string methodName)
        {
            MethodInfo method;

            if (mMethodInfoCache.TryGetValue(methodName, out method))
            {
                return method;
            }

            // BuyController has two overloads of ResizeCatalogGrid
            // (the 6-parameter one patched by BuyControllerPatch, and the
            // private (ItemGrid, int) one called from here). Select the
            // exact signature so GetMethod can never be ambiguous.
            if (methodName == "ResizeCatalogGrid")
            {
                method =
                    typeof(BuyController).GetMethod(
                        methodName,
                        InstanceFlags,
                        null,
                        new Type[] { typeof(ItemGrid), typeof(int) },
                        null
                    );
            }
            else
            {
                method =
                    typeof(BuyController).GetMethod(
                        methodName,
                        InstanceFlags
                    );
            }

            if (method != null)
            {
                mMethodInfoCache[methodName] = method;
            }

            return method;
        }

        private T GetField<T>(
            object instance,
            string fieldName)
        {
            return (T)GetInstanceFieldInfo(
                instance,
                fieldName
            ).GetValue(instance);
        }

        private void SetField<T>(
            object instance,
            string fieldName,
            T value)
        {
            GetInstanceFieldInfo(
                instance,
                fieldName
            ).SetValue(
                instance,
                value
            );
        }

        private T GetStaticField<T>(
            Type type,
            string fieldName)
        {
            string cacheKey =
                type.FullName + "|" + fieldName;

            FieldInfo field;

            if (!sStaticFieldInfoCache.TryGetValue(
                    cacheKey,
                    out field))
            {
                field =
                    type.GetField(
                        fieldName,
                        StaticFlags
                    );

                if (field == null)
                {
                    throw new Exception(
                        Localize("StaticFieldNotFound") +
                        fieldName
                    );
                }

                sStaticFieldInfoCache[cacheKey] = field;
            }

            return (T)field.GetValue(null);
        }

    }
}