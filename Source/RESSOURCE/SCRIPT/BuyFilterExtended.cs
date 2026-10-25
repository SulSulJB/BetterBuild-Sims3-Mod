using System.Reflection;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.UI;

namespace BetterBuildByJojobou
{
    public static class BuyFilterExtended
    {
        private const uint ExpandedSearchWindowID = 0x00000203;
        private const uint CatalogProductFilterID = 0x0b4ef550;

        private const uint RoomWindowID = 0x00002000;
        private const uint RoomFilterButtonID = 0x00002201;

        private const uint FunctionWindowID = 0x00002001;
        private const uint FunctionFilterButtonID = 0x00002202;

        private const uint CollectionWindowID = 0x00000410;
        private const uint CollectionFilterButtonID = 0x00000426;

        private const uint NormalAllCatalogButtonID = 0x0bc50002;
        private const uint NormalSearchWindowID = 0x0bc50000;

        // Better Build interface toggle button (B)
        private const uint NormalInterfaceToggleButtonID = 0x0bc50040;
        private const uint ExpandedInterfaceToggleButtonID = 0x00000209;

        private const float FilterPanelGap = 5f;
        private const float FilterPanelOffsetX = 15f;
        private const float FilterPanelOffsetY = 64f;

        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public;

        private static BuyController mController;
        private static CatalogProductFilter mFilterPanel;

        private static ExpandableCatalogGrid mRoomGrid;
        private static ExpandableCatalogGrid mFunctionGrid;
        private static ExpandableCatalogGrid mCollectionGrid;

        private static WindowBase mRoomWindow;
        private static Button mRoomNormalFilterButton;
        private static Button mRoomNormalAllCatalogButton;
        private static WindowBase mRoomNormalSearchWindow;
        private static WindowBase mRoomExpandedSearchWindow;
        private static Button mRoomNormalInterfaceToggle;
        private static Button mRoomExpandedInterfaceToggle;

        private static WindowBase mFunctionWindow;
        private static Button mFunctionNormalFilterButton;
        private static Button mFunctionNormalAllCatalogButton;
        private static WindowBase mFunctionNormalSearchWindow;
        private static WindowBase mFunctionExpandedSearchWindow;
        private static Button mFunctionNormalInterfaceToggle;
        private static Button mFunctionExpandedInterfaceToggle;

        private static WindowBase mCollectionWindow;
        private static Button mCollectionNormalFilterButton;
        private static Button mCollectionNormalAllCatalogButton;
        private static WindowBase mCollectionNormalSearchWindow;
        private static Button mCollectionNormalInterfaceToggle;
        private static Button mCollectionExpandedInterfaceToggle;

        private static string Localize(string key)
        {
            return Localization.LocalizeString(
                "Jojobou/BetterBuild/BuyFilterExtended:" + key,
                new object[0]
            );
        }

        public static void Attach(BuyController controller)
        {
            if (controller == null)
            {
                return;
            }

            /*
             * IMPORTANT:
             * EA can recreate parts of the Buy UI while keeping the SAME
             * BuyController instance alive.
             *
             * Therefore we must not assume that our stored Window/Button/Grid
             * references are still the ones currently visible on screen.
             *
             * We only fully Detach() when the controller itself changes.
             * Otherwise, we safely refresh/rebind the current UI instances.
             */
            if (!object.ReferenceEquals(mController, controller))
            {
                Detach();

                mController = controller;

                mController.Detach -= OnControllerDetach;
                mController.Detach += OnControllerDetach;
            }

            RefreshControllerBindings();
            UpdateNormalControlsVisibility();

            // Rebind the interface toggle after EA refreshes/recreates Buy UI.
            // This is especially important when switching to Sort by Room.
            BetterBuildInterfaceToggle.Attach(
                controller
            );
        }

        private static void RefreshControllerBindings()
        {
            if (mController == null)
            {
                return;
            }

            /*
             * FILTER PANEL
             *
             * Reacquire the native EA panel in case the UI was rebuilt.
             * Rebind only our VisibilityChange handler.
             */
            CatalogProductFilter filterPanel =
                mController.GetChildByID(
                    CatalogProductFilterID,
                    true
                ) as CatalogProductFilter;

            RebindFilterPanel(
                filterPanel
            );

            /*
             * ROOM NORMAL UI
             */
            mRoomWindow =
                mController.GetChildByID(
                    RoomWindowID,
                    true
                );

            Button roomNormalFilterButton = null;
            Button roomNormalAllCatalogButton = null;
            WindowBase roomNormalSearchWindow = null;
            Button roomNormalInterfaceToggle = null;

            if (mRoomWindow != null)
            {
                roomNormalFilterButton =
                    mRoomWindow.GetChildByID(
                        RoomFilterButtonID,
                        true
                    ) as Button;

                roomNormalAllCatalogButton =
                    mRoomWindow.GetChildByID(
                        NormalAllCatalogButtonID,
                        true
                    ) as Button;

                roomNormalSearchWindow =
                    mRoomWindow.GetChildByID(
                        NormalSearchWindowID,
                        true
                    );

                roomNormalInterfaceToggle =
                    mRoomWindow.GetChildByID(
                        NormalInterfaceToggleButtonID,
                        true
                    ) as Button;
            }

            RebindNormalFilterButton(
                ref mRoomNormalFilterButton,
                roomNormalFilterButton
            );

            mRoomNormalAllCatalogButton =
                roomNormalAllCatalogButton;

            mRoomNormalSearchWindow =
                roomNormalSearchWindow;

            mRoomNormalInterfaceToggle =
                roomNormalInterfaceToggle;

            /*
             * FUNCTION NORMAL UI
             */
            mFunctionWindow =
                mController.GetChildByID(
                    FunctionWindowID,
                    true
                );

            Button functionNormalFilterButton = null;
            Button functionNormalAllCatalogButton = null;
            WindowBase functionNormalSearchWindow = null;
            Button functionNormalInterfaceToggle = null;

            if (mFunctionWindow != null)
            {
                functionNormalFilterButton =
                    mFunctionWindow.GetChildByID(
                        FunctionFilterButtonID,
                        true
                    ) as Button;

                functionNormalAllCatalogButton =
                    mFunctionWindow.GetChildByID(
                        NormalAllCatalogButtonID,
                        true
                    ) as Button;

                functionNormalSearchWindow =
                    mFunctionWindow.GetChildByID(
                        NormalSearchWindowID,
                        true
                    );

                functionNormalInterfaceToggle =
                    mFunctionWindow.GetChildByID(
                        NormalInterfaceToggleButtonID,
                        true
                    ) as Button;
            }

            RebindNormalFilterButton(
                ref mFunctionNormalFilterButton,
                functionNormalFilterButton
            );

            mFunctionNormalAllCatalogButton =
                functionNormalAllCatalogButton;

            mFunctionNormalSearchWindow =
                functionNormalSearchWindow;

            mFunctionNormalInterfaceToggle =
                functionNormalInterfaceToggle;

            /*
             * COLLECTION NORMAL UI
             *
             * Collections has its own native filter button (0x00000426).
             * Better Build keeps that native control and only reuses it with
             * the same checkbox visual / popup positioning as Room/Function.
             */
            mCollectionWindow =
                mController.GetChildByID(
                    CollectionWindowID,
                    true
                );

            Button collectionNormalFilterButton = null;
            Button collectionNormalAllCatalogButton = null;
            WindowBase collectionNormalSearchWindow = null;
            Button collectionNormalInterfaceToggle = null;

            if (mCollectionWindow != null)
            {
                collectionNormalFilterButton =
                    mCollectionWindow.GetChildByID(
                        CollectionFilterButtonID,
                        true
                    ) as Button;

                collectionNormalAllCatalogButton =
                    mCollectionWindow.GetChildByID(
                        NormalAllCatalogButtonID,
                        true
                    ) as Button;

                collectionNormalSearchWindow =
                    mCollectionWindow.GetChildByID(
                        NormalSearchWindowID,
                        true
                    );

                collectionNormalInterfaceToggle =
                    mCollectionWindow.GetChildByID(
                        NormalInterfaceToggleButtonID,
                        true
                    ) as Button;
            }

            RebindNormalFilterButton(
                ref mCollectionNormalFilterButton,
                collectionNormalFilterButton
            );

            mCollectionNormalAllCatalogButton =
                collectionNormalAllCatalogButton;

            mCollectionNormalSearchWindow =
                collectionNormalSearchWindow;

            mCollectionNormalInterfaceToggle =
                collectionNormalInterfaceToggle;

            /*
             * EXPANDABLE GRIDS
             *
             * Reacquire the native fields because EA may recreate the grid
             * object while the BuyController itself stays the same.
             */
            FieldInfo roomGridField =
                typeof(BuyController).GetField(
                    "mGridSortByRoom",
                    InstanceFlags
                );

            ExpandableCatalogGrid roomGrid =
                roomGridField != null
                    ? roomGridField.GetValue(mController)
                        as ExpandableCatalogGrid
                    : null;

            RebindRoomGrid(
                roomGrid
            );

            FieldInfo functionGridField =
                typeof(BuyController).GetField(
                    "mGridSortByFunction",
                    InstanceFlags
                );

            ExpandableCatalogGrid functionGrid =
                functionGridField != null
                    ? functionGridField.GetValue(mController)
                        as ExpandableCatalogGrid
                    : null;

            RebindFunctionGrid(
                functionGrid
            );

            FieldInfo collectionGridField =
                typeof(BuyController).GetField(
                    "mGridCollection",
                    InstanceFlags
                );

            ExpandableCatalogGrid collectionGrid =
                collectionGridField != null
                    ? collectionGridField.GetValue(mController)
                        as ExpandableCatalogGrid
                    : null;

            RebindCollectionGrid(
                collectionGrid
            );

            /*
             * The expanded search windows belong to the expandable layouts,
             * so refresh those references after the grids are current.
             */
            RefreshExtendedBindings();
        }

        private static void RebindFilterPanel(
            CatalogProductFilter next)
        {
            if (!object.ReferenceEquals(
                    mFilterPanel,
                    next))
            {
                if (mFilterPanel != null)
                {
                    mFilterPanel.VisibilityChange -=
                        OnFilterPanelVisibilityChange;
                }

                mFilterPanel = next;
            }

            if (mFilterPanel != null)
            {
                mFilterPanel.VisibilityChange -=
                    OnFilterPanelVisibilityChange;

                mFilterPanel.VisibilityChange +=
                    OnFilterPanelVisibilityChange;
            }
        }

        private static void RebindNormalFilterButton(
            ref Button current,
            Button next)
        {
            if (!object.ReferenceEquals(
                    current,
                    next))
            {
                if (current != null)
                {
                    current.Click -=
                        OnNormalFilterClick;
                }

                current = next;
            }

            BindNormalFilterButton(
                current
            );
        }

        private static void RebindRoomGrid(
            ExpandableCatalogGrid next)
        {
            if (!object.ReferenceEquals(
                    mRoomGrid,
                    next))
            {
                if (mRoomGrid != null)
                {
                    mRoomGrid.ExpandChanged -=
                        OnRoomExpandChanged;
                }

                mRoomGrid = next;
            }

            if (mRoomGrid != null)
            {
                mRoomGrid.ExpandChanged -=
                    OnRoomExpandChanged;

                mRoomGrid.ExpandChanged +=
                    OnRoomExpandChanged;
            }
        }

        private static void RebindFunctionGrid(
            ExpandableCatalogGrid next)
        {
            if (!object.ReferenceEquals(
                    mFunctionGrid,
                    next))
            {
                if (mFunctionGrid != null)
                {
                    mFunctionGrid.ExpandChanged -=
                        OnFunctionExpandChanged;
                }

                mFunctionGrid = next;
            }

            if (mFunctionGrid != null)
            {
                mFunctionGrid.ExpandChanged -=
                    OnFunctionExpandChanged;

                mFunctionGrid.ExpandChanged +=
                    OnFunctionExpandChanged;
            }
        }

        private static void RebindCollectionGrid(
            ExpandableCatalogGrid next)
        {
            if (!object.ReferenceEquals(
                    mCollectionGrid,
                    next))
            {
                if (mCollectionGrid != null)
                {
                    mCollectionGrid.ExpandChanged -=
                        OnCollectionExpandChanged;
                }

                mCollectionGrid = next;
            }

            if (mCollectionGrid != null)
            {
                mCollectionGrid.ExpandChanged -=
                    OnCollectionExpandChanged;

                mCollectionGrid.ExpandChanged +=
                    OnCollectionExpandChanged;
            }
        }

        private static void BindNormalFilterButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.TooltipText = Localize("FilterTooltip");
            button.Click -= OnNormalFilterClick;
            button.Click += OnNormalFilterClick;
        }

        private static void RefreshExtendedBindings()
        {
            WindowBase roomSearch = null;
            Button roomExpandedInterfaceToggle = null;

            if (mRoomGrid != null)
            {
                roomSearch =
                    mRoomGrid.GetChildByID(
                        ExpandedSearchWindowID,
                        true
                    );

                roomExpandedInterfaceToggle =
                    mRoomGrid.GetChildByID(
                        ExpandedInterfaceToggleButtonID,
                        true
                    ) as Button;
            }

            WindowBase functionSearch = null;
            Button functionExpandedInterfaceToggle = null;

            if (mFunctionGrid != null)
            {
                functionSearch =
                    mFunctionGrid.GetChildByID(
                        ExpandedSearchWindowID,
                        true
                    );

                functionExpandedInterfaceToggle =
                    mFunctionGrid.GetChildByID(
                        ExpandedInterfaceToggleButtonID,
                        true
                    ) as Button;
            }

            Button collectionExpandedInterfaceToggle = null;

            if (mCollectionGrid != null)
            {
                collectionExpandedInterfaceToggle =
                    mCollectionGrid.GetChildByID(
                        ExpandedInterfaceToggleButtonID,
                        true
                    ) as Button;
            }

            RebindExpandedSearchWindow(
                ref mRoomExpandedSearchWindow,
                roomSearch);

            RebindExpandedSearchWindow(
                ref mFunctionExpandedSearchWindow,
                functionSearch);

            mRoomExpandedInterfaceToggle =
                roomExpandedInterfaceToggle;

            mFunctionExpandedInterfaceToggle =
                functionExpandedInterfaceToggle;

            mCollectionExpandedInterfaceToggle =
                collectionExpandedInterfaceToggle;
        }

        private static void RebindExpandedSearchWindow(
            ref WindowBase current,
            WindowBase next)
        {
            if (object.ReferenceEquals(current, next))
            {
                return;
            }

            if (current != null)
            {
                current.VisibilityChange -=
                    OnExpandedVisibilityChange;
            }

            current = next;

            if (current != null)
            {
                current.VisibilityChange -=
                    OnExpandedVisibilityChange;
                current.VisibilityChange +=
                    OnExpandedVisibilityChange;
            }
        }

        private static void OnRoomExpandChanged()
        {
            if (mRoomGrid != null && mRoomGrid.Expanded)
            {
                CloseNormalFilter(mRoomNormalFilterButton);
            }

            RefreshControllerBindings();
            UpdateNormalControlsVisibility();
        }

        private static void OnFunctionExpandChanged()
        {
            if (mFunctionGrid != null && mFunctionGrid.Expanded)
            {
                CloseNormalFilter(mFunctionNormalFilterButton);
            }

            RefreshControllerBindings();
            UpdateNormalControlsVisibility();
        }

        private static void OnCollectionExpandChanged()
        {
            if (mCollectionGrid != null &&
                mCollectionGrid.Expanded)
            {
                CloseNormalFilter(
                    mCollectionNormalFilterButton
                );
            }

            RefreshControllerBindings();
            UpdateNormalControlsVisibility();
        }

        private static void UpdateNormalControlsVisibility()
        {
            if (mRoomGrid != null)
            {
                bool roomExpanded =
                    mRoomGrid.Expanded;

                bool showRoomNormalControls =
                    !roomExpanded &&
                    BetterBuildInterfaceToggle.InterfaceVisible;

                SetVisible(
                    mRoomNormalFilterButton,
                    showRoomNormalControls);

                SetVisible(
                    mRoomNormalAllCatalogButton,
                    showRoomNormalControls);

                SetVisible(
                    mRoomNormalSearchWindow,
                    showRoomNormalControls);

                // Never show both B buttons at once.
                SetVisible(
                    mRoomNormalInterfaceToggle,
                    !roomExpanded);

                SetVisible(
                    mRoomExpandedInterfaceToggle,
                    roomExpanded);

                if (!showRoomNormalControls &&
                    mRoomNormalFilterButton != null &&
                    mRoomNormalFilterButton.Selected)
                {
                    CloseNormalFilter(
                        mRoomNormalFilterButton
                    );
                }
            }

            if (mFunctionGrid != null)
            {
                bool functionExpanded =
                    mFunctionGrid.Expanded;

                bool showFunctionNormalControls =
                    !functionExpanded &&
                    BetterBuildInterfaceToggle.InterfaceVisible;

                SetVisible(
                    mFunctionNormalFilterButton,
                    showFunctionNormalControls);

                SetVisible(
                    mFunctionNormalAllCatalogButton,
                    showFunctionNormalControls);

                SetVisible(
                    mFunctionNormalSearchWindow,
                    showFunctionNormalControls);

                // Never show both B buttons at once.
                SetVisible(
                    mFunctionNormalInterfaceToggle,
                    !functionExpanded);

                SetVisible(
                    mFunctionExpandedInterfaceToggle,
                    functionExpanded);

                if (!showFunctionNormalControls &&
                    mFunctionNormalFilterButton != null &&
                    mFunctionNormalFilterButton.Selected)
                {
                    CloseNormalFilter(
                        mFunctionNormalFilterButton
                    );
                }
            }

            if (mCollectionGrid != null)
            {
                bool collectionExpanded =
                    mCollectionGrid.Expanded;

                bool showCollectionNormalControls =
                    !collectionExpanded &&
                    BetterBuildInterfaceToggle.InterfaceVisible;

                SetVisible(
                    mCollectionNormalFilterButton,
                    showCollectionNormalControls);

                SetVisible(
                    mCollectionNormalAllCatalogButton,
                    showCollectionNormalControls);

                SetVisible(
                    mCollectionNormalSearchWindow,
                    showCollectionNormalControls);

                // Never show both B buttons at once.
                SetVisible(
                    mCollectionNormalInterfaceToggle,
                    !collectionExpanded);

                SetVisible(
                    mCollectionExpandedInterfaceToggle,
                    collectionExpanded);

                if (!showCollectionNormalControls &&
                    mCollectionNormalFilterButton != null &&
                    mCollectionNormalFilterButton.Selected)
                {
                    CloseNormalFilter(
                        mCollectionNormalFilterButton
                    );
                }
            }
        }

        private static void OnNormalFilterClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            /*
             * Fallback seulement.
             * Le vrai repositionnement se fait dans VisibilityChange,
             * après qu'EA ait ouvert et reparenté le panneau.
             */
            if (mFilterPanel != null &&
                mFilterPanel.Visible)
            {
                PositionVisibleFilterPanel();
            }
        }

        private static void OnFilterPanelVisibilityChange(
            WindowBase sender,
            UIVisibilityChangeEventArgs eventArgs)
        {
            if (mFilterPanel == null ||
                !mFilterPanel.Visible)
            {
                return;
            }

            /*
             * EA vient de rendre le CatalogProductFilter visible.
             * À ce moment-ci son Parent est déjà le bouton EA actif.
             */
            PositionVisibleFilterPanel();
        }

        private static void PositionVisibleFilterPanel()
        {
            if (mFilterPanel == null ||
                !mFilterPanel.Visible)
            {
                return;
            }

            if (mRoomNormalFilterButton != null &&
                mRoomNormalFilterButton.Selected &&
                mRoomGrid != null &&
                !mRoomGrid.Expanded)
            {
                PositionFilterPanel(
                    mRoomNormalFilterButton,
                    mRoomNormalAllCatalogButton);
                return;
            }

            if (mFunctionNormalFilterButton != null &&
                mFunctionNormalFilterButton.Selected &&
                mFunctionGrid != null &&
                !mFunctionGrid.Expanded)
            {
                PositionFilterPanel(
                    mFunctionNormalFilterButton,
                    mFunctionNormalAllCatalogButton);
                return;
            }

            if (mCollectionNormalFilterButton != null &&
                mCollectionNormalFilterButton.Selected &&
                mCollectionGrid != null &&
                !mCollectionGrid.Expanded)
            {
                PositionFilterPanel(
                    mCollectionNormalFilterButton,
                    mCollectionNormalAllCatalogButton);
            }
        }

        private static void PositionFilterPanel(
            Button filterButton,
            Button allCatalogButton)
        {
            if (mFilterPanel == null ||
                mFilterPanel.Parent == null ||
                filterButton == null ||
                allCatalogButton == null)
            {
                return;
            }

            /*
             * Better Build:
             * on centre le popup sur LA case DLC/CC cochée.
             *
             * Le visuel du bouton est légèrement décalé par rapport
             * à son rectangle logique : +15 px donne l'alignement
             * observé sur le centre réel de la case cochée.
             */
            Vector2 filterTopLeft =
                filterButton.WindowToScreen(
                    new Vector2(0f, 0f)
                );

            float filterCenterX =
                filterTopLeft.x +
                (filterButton.Area.Width / 2f);

            float buttonsTop =
                filterTopLeft.y;

            float panelWidth =
                mFilterPanel.Area.Width;

            float panelHeight =
                mFilterPanel.Area.Height;

            Vector2 targetScreen =
                new Vector2(
                    filterCenterX -
                    (panelWidth / 2f) +
                    FilterPanelOffsetX,
                    buttonsTop -
                    panelHeight -
                    FilterPanelGap +
                    FilterPanelOffsetY
                );

            /*
             * Area est en coordonnées LOCALES du parent.
             * On convertit donc correctement Screen -> Parent avant Set().
             */
            Vector2 targetLocal =
                mFilterPanel.Parent.ScreenToWindow(
                    targetScreen
                );

            Rect area = mFilterPanel.Area;

            area.Set(
                targetLocal.x,
                targetLocal.y,
                targetLocal.x + panelWidth,
                targetLocal.y + panelHeight
            );

            mFilterPanel.Area = area;
        }

        private static void OnExpandedVisibilityChange(
            WindowBase sender,
            UIVisibilityChangeEventArgs eventArgs)
        {
            UpdateNormalControlsVisibility();
        }

        private static void CloseNormalFilter(
            Button button)
        {
            if (mFilterPanel != null)
            {
                mFilterPanel.Visible = false;
            }

            if (button != null)
            {
                button.Selected = false;
            }
        }

        private static void OnControllerDetach(
            WindowBase sender,
            UIEventArgs eventArgs)
        {
            Detach();
        }

        private static void Detach()
        {
            SetVisible(
                mRoomNormalFilterButton,
                true);

            SetVisible(
                mRoomNormalAllCatalogButton,
                true);

            SetVisible(
                mRoomNormalSearchWindow,
                true);

            SetVisible(
                mRoomNormalInterfaceToggle,
                true);

            SetVisible(
                mRoomExpandedInterfaceToggle,
                false);

            SetVisible(
                mFunctionNormalFilterButton,
                true);

            SetVisible(
                mFunctionNormalAllCatalogButton,
                true);

            SetVisible(
                mFunctionNormalSearchWindow,
                true);

            SetVisible(
                mFunctionNormalInterfaceToggle,
                true);

            SetVisible(
                mFunctionExpandedInterfaceToggle,
                false);

            SetVisible(
                mCollectionNormalFilterButton,
                true);

            SetVisible(
                mCollectionNormalAllCatalogButton,
                true);

            SetVisible(
                mCollectionNormalSearchWindow,
                true);

            SetVisible(
                mCollectionNormalInterfaceToggle,
                true);

            SetVisible(
                mCollectionExpandedInterfaceToggle,
                false);

            if (mFilterPanel != null)
            {
                mFilterPanel.VisibilityChange -=
                    OnFilterPanelVisibilityChange;
            }


            if (mRoomNormalFilterButton != null)
            {
                mRoomNormalFilterButton.Click -=
                    OnNormalFilterClick;
            }

            if (mFunctionNormalFilterButton != null)
            {
                mFunctionNormalFilterButton.Click -=
                    OnNormalFilterClick;
            }

            if (mCollectionNormalFilterButton != null)
            {
                mCollectionNormalFilterButton.Click -=
                    OnNormalFilterClick;
            }

            if (mRoomExpandedSearchWindow != null)
            {
                mRoomExpandedSearchWindow.VisibilityChange -=
                    OnExpandedVisibilityChange;
            }

            if (mFunctionExpandedSearchWindow != null)
            {
                mFunctionExpandedSearchWindow.VisibilityChange -=
                    OnExpandedVisibilityChange;
            }

            if (mRoomGrid != null)
            {
                mRoomGrid.ExpandChanged -=
                    OnRoomExpandChanged;
            }

            if (mFunctionGrid != null)
            {
                mFunctionGrid.ExpandChanged -=
                    OnFunctionExpandChanged;
            }

            if (mCollectionGrid != null)
            {
                mCollectionGrid.ExpandChanged -=
                    OnCollectionExpandChanged;
            }

            if (mController != null)
            {
                mController.Detach -=
                    OnControllerDetach;
            }

            mController = null;
            mFilterPanel = null;

            mRoomGrid = null;
            mRoomWindow = null;
            mRoomNormalFilterButton = null;
            mRoomNormalAllCatalogButton = null;
            mRoomNormalSearchWindow = null;
            mRoomExpandedSearchWindow = null;
            mRoomNormalInterfaceToggle = null;
            mRoomExpandedInterfaceToggle = null;

            mFunctionGrid = null;
            mFunctionWindow = null;
            mFunctionNormalFilterButton = null;
            mFunctionNormalAllCatalogButton = null;
            mFunctionNormalSearchWindow = null;
            mFunctionExpandedSearchWindow = null;
            mFunctionNormalInterfaceToggle = null;
            mFunctionExpandedInterfaceToggle = null;

            mCollectionGrid = null;
            mCollectionWindow = null;
            mCollectionNormalFilterButton = null;
            mCollectionNormalAllCatalogButton = null;
            mCollectionNormalSearchWindow = null;
            mCollectionNormalInterfaceToggle = null;
            mCollectionExpandedInterfaceToggle = null;
        }

        private static void SetVisible(
            WindowBase window,
            bool visible)
        {
            if (window != null)
            {
                window.Visible = visible;
            }
        }
    }
}
