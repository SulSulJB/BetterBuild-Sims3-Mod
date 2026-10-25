using System.Reflection;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.UI;

namespace BetterBuildByJojobou
{
    /*
     * Better Build - Interface Toggle
     *
     * One shared visibility state for Buy + Build.
     *
     * Visible by default when this class is first loaded.
     * Once the player hides Better Build, the state is preserved while
     * switching between normal / expanded and Buy / Build until the player
     * presses the Better Build button again.
     *
     * The toggle button itself is NEVER hidden.
     */
    public static class BetterBuildInterfaceToggle
    {
        private const uint NormalToggleButtonID = 0x0bc50040U;
        private const uint ExpandedToggleButtonID = 0x00000209U;

        private const uint NormalSearchWindowID = 0x0bc50000U;
        private const uint NormalMoveObjectsButtonID = 0x0bc50010U;
        private const uint NormalAllCatalogButtonID = 0x0bc50002U;
        private const uint NormalSaveButtonID = 0x0bc50020U;

        private const uint ExpandedSearchWindowID = 0x00000203U;
        private const uint ExpandedMoveObjectsButtonID = 0x00000206U;
        private const uint ExpandedAllCatalogButtonID = 0x00000205U;
        private const uint ExpandedSaveButtonID = 0x00000207U;

        private const uint BuyRoomWindowID = 0x00002000U;
        private const uint BuyFunctionWindowID = 0x00002001U;
        private const uint BuyCollectionWindowID = 0x00000410U;

        private const uint BuyRoomFilterButtonID = 0x00002201U;
        private const uint BuyFunctionFilterButtonID = 0x00002202U;
        private const uint BuyCollectionFilterButtonID = 0x00000426U;

        private const uint CatalogProductFilterID = 0x0b4ef550U;

        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public;

        private static bool sInterfaceVisible = true;

        // Read-only shared state used by the existing Buy/Build UI classes.
        // The B button remains the only code that changes this value.
        public static bool InterfaceVisible
        {
            get
            {
                return sInterfaceVisible;
            }
        }

        // BUY
        private static BuyController sBuyController;
        private static WindowBase sBuyRoomWindow;
        private static WindowBase sBuyFunctionWindow;
        private static WindowBase sBuyCollectionWindow;
        private static ExpandableCatalogGrid sBuyRoomGrid;
        private static ExpandableCatalogGrid sBuyFunctionGrid;
        private static ExpandableCatalogGrid sBuyCollectionGrid;

        private static Button sBuyRoomNormalToggle;
        private static Button sBuyFunctionNormalToggle;
        private static Button sBuyCollectionNormalToggle;
        private static Button sBuyRoomExpandedToggle;
        private static Button sBuyFunctionExpandedToggle;
        private static Button sBuyCollectionExpandedToggle;

        // BUILD
        private static BuildController sBuildController;
        private static ItemGrid sBuildCatalogGrid;
        private static Window sBuildCatalogWindow;
        private static ExpandableCatalogGrid sBuildExpandableGrid;
        private static Button sBuildNormalToggle;
        private static Button sBuildExpandedToggle;

        private static string Localize(string key)
        {
            return Localization.LocalizeString(
                "Jojobou/BetterBuild/InterfaceToggle:" + key,
                new object[0]
            );
        }

        // ============================================================
        // BUY
        // ============================================================

        public static void Attach(BuyController controller)
        {
            if (controller == null)
            {
                return;
            }

            if (!object.ReferenceEquals(sBuyController, controller))
            {
                DetachBuy();

                sBuyController = controller;
                sBuyController.Detach -= OnBuyControllerDetach;
                sBuyController.Detach += OnBuyControllerDetach;
            }

            RefreshBuyBindings();
            ApplyBuyVisibility();
        }

        private static void RefreshBuyBindings()
        {
            if (sBuyController == null)
            {
                return;
            }

            RebindBuyNormalWindow(
                ref sBuyRoomWindow,
                sBuyController.GetChildByID(
                    BuyRoomWindowID,
                    true
                )
            );

            RebindBuyNormalWindow(
                ref sBuyFunctionWindow,
                sBuyController.GetChildByID(
                    BuyFunctionWindowID,
                    true
                )
            );

            RebindBuyNormalWindow(
                ref sBuyCollectionWindow,
                sBuyController.GetChildByID(
                    BuyCollectionWindowID,
                    true
                )
            );

            RebindBuyGrid(
                ref sBuyRoomGrid,
                GetBuyGrid("mGridSortByRoom")
            );

            RebindBuyGrid(
                ref sBuyFunctionGrid,
                GetBuyGrid("mGridSortByFunction")
            );

            RebindBuyGrid(
                ref sBuyCollectionGrid,
                GetBuyGrid("mGridCollection")
            );

            RebindToggle(
                ref sBuyRoomNormalToggle,
                GetButton(
                    sBuyRoomWindow,
                    NormalToggleButtonID
                )
            );

            RebindToggle(
                ref sBuyFunctionNormalToggle,
                GetButton(
                    sBuyFunctionWindow,
                    NormalToggleButtonID
                )
            );

            RebindToggle(
                ref sBuyCollectionNormalToggle,
                GetButton(
                    sBuyCollectionWindow,
                    NormalToggleButtonID
                )
            );

            RebindToggle(
                ref sBuyRoomExpandedToggle,
                GetButton(
                    sBuyRoomGrid,
                    ExpandedToggleButtonID
                )
            );

            RebindToggle(
                ref sBuyFunctionExpandedToggle,
                GetButton(
                    sBuyFunctionGrid,
                    ExpandedToggleButtonID
                )
            );

            RebindToggle(
                ref sBuyCollectionExpandedToggle,
                GetButton(
                    sBuyCollectionGrid,
                    ExpandedToggleButtonID
                )
            );

            SyncToggleVisuals();
        }

        private static void RebindBuyNormalWindow(
            ref WindowBase current,
            WindowBase next)
        {
            if (!object.ReferenceEquals(current, next))
            {
                if (current != null)
                {
                    current.VisibilityChange -=
                        OnBuyNormalWindowVisibilityChange;
                }

                current = next;
            }

            if (current != null)
            {
                current.VisibilityChange -=
                    OnBuyNormalWindowVisibilityChange;

                current.VisibilityChange +=
                    OnBuyNormalWindowVisibilityChange;
            }
        }

        private static void OnBuyNormalWindowVisibilityChange(
            WindowBase sender,
            UIVisibilityChangeEventArgs eventArgs)
        {
            if (sender == null ||
                !sender.Visible)
            {
                return;
            }

            // EA can rebuild the active Buy tab. Reacquire the actual visible
            // B button so Room / Function / Collection all receive click + tooltip.
            RefreshBuyBindings();
            ApplyBuyVisibility();
        }

        private static ExpandableCatalogGrid GetBuyGrid(string fieldName)
        {
            FieldInfo field =
                typeof(BuyController).GetField(
                    fieldName,
                    InstanceFlags
                );

            if (field == null ||
                sBuyController == null)
            {
                return null;
            }

            return field.GetValue(sBuyController)
                as ExpandableCatalogGrid;
        }

        private static void RebindBuyGrid(
            ref ExpandableCatalogGrid current,
            ExpandableCatalogGrid next)
        {
            if (object.ReferenceEquals(current, next))
            {
                if (current != null)
                {
                    current.ExpandChanged -= OnBuyExpandChanged;
                    current.ExpandChanged += OnBuyExpandChanged;
                }
                return;
            }

            if (current != null)
            {
                current.ExpandChanged -= OnBuyExpandChanged;
            }

            current = next;

            if (current != null)
            {
                current.ExpandChanged -= OnBuyExpandChanged;
                current.ExpandChanged += OnBuyExpandChanged;
            }
        }

        private static void OnBuyExpandChanged()
        {
            /*
             * The expanded LAYO can be instantiated/rebuilt when this event
             * fires, so reacquire the button references before applying state.
             */
            RefreshBuyBindings();
            ApplyBuyVisibility();
        }

        private static void ApplyBuyVisibility()
        {
            ApplyBuyCatalog(
                sBuyRoomWindow,
                sBuyRoomGrid,
                BuyRoomFilterButtonID,
                sBuyRoomNormalToggle,
                sBuyRoomExpandedToggle
            );

            ApplyBuyCatalog(
                sBuyFunctionWindow,
                sBuyFunctionGrid,
                BuyFunctionFilterButtonID,
                sBuyFunctionNormalToggle,
                sBuyFunctionExpandedToggle
            );

            ApplyBuyCatalog(
                sBuyCollectionWindow,
                sBuyCollectionGrid,
                BuyCollectionFilterButtonID,
                sBuyCollectionNormalToggle,
                sBuyCollectionExpandedToggle
            );

            if (!sInterfaceVisible)
            {
                CloseBuyFilterPanel();
            }

            SyncToggleVisuals();
        }

        private static void ApplyBuyCatalog(
            WindowBase normalWindow,
            ExpandableCatalogGrid grid,
            uint normalFilterButtonID,
            Button normalToggle,
            Button expandedToggle)
        {
            if (normalWindow == null &&
                grid == null)
            {
                return;
            }

            bool expanded =
                grid != null &&
                grid.Expanded;

            // Normal B only in normal view; Expanded B only in expanded view.
            SetVisible(
                normalToggle,
                !expanded
            );

            SetVisible(
                expandedToggle,
                expanded
            );

            bool showNormalControls =
                !expanded &&
                sInterfaceVisible;

            bool showExpandedControls =
                expanded &&
                sInterfaceVisible;

            SetVisible(
                GetWindow(normalWindow, normalFilterButtonID),
                showNormalControls
            );

            SetVisible(
                GetWindow(normalWindow, NormalMoveObjectsButtonID),
                showNormalControls
            );

            SetVisible(
                GetWindow(normalWindow, NormalAllCatalogButtonID),
                showNormalControls
            );

            SetVisible(
                GetWindow(normalWindow, NormalSearchWindowID),
                showNormalControls
            );

            SetVisible(
                GetWindow(normalWindow, NormalSaveButtonID),
                showNormalControls
            );

            SetVisible(
                GetWindow(grid, ExpandedMoveObjectsButtonID),
                showExpandedControls
            );

            SetVisible(
                GetWindow(grid, ExpandedAllCatalogButtonID),
                showExpandedControls
            );

            SetVisible(
                GetWindow(grid, ExpandedSearchWindowID),
                showExpandedControls
            );

            SetVisible(
                GetWindow(grid, ExpandedSaveButtonID),
                showExpandedControls
            );

            if (!showNormalControls)
            {
                Button filterButton =
                    GetButton(
                        normalWindow,
                        normalFilterButtonID
                    );

                if (filterButton != null)
                {
                    filterButton.Selected = false;
                }
            }
        }

        private static void CloseBuyFilterPanel()
        {
            if (sBuyController == null)
            {
                return;
            }

            WindowBase filterPanel =
                sBuyController.GetChildByID(
                    CatalogProductFilterID,
                    true
                );

            if (filterPanel != null)
            {
                filterPanel.Visible = false;
            }
        }

        private static void OnBuyControllerDetach(
            WindowBase sender,
            UIEventArgs eventArgs)
        {
            DetachBuy();
        }

        private static void DetachBuy()
        {
            if (sBuyController != null)
            {
                sBuyController.Detach -= OnBuyControllerDetach;
            }

            UnbindBuyGrid(ref sBuyRoomGrid);
            UnbindBuyGrid(ref sBuyFunctionGrid);
            UnbindBuyGrid(ref sBuyCollectionGrid);

            UnbindToggle(ref sBuyRoomNormalToggle);
            UnbindToggle(ref sBuyFunctionNormalToggle);
            UnbindToggle(ref sBuyCollectionNormalToggle);
            UnbindToggle(ref sBuyRoomExpandedToggle);
            UnbindToggle(ref sBuyFunctionExpandedToggle);
            UnbindToggle(ref sBuyCollectionExpandedToggle);

            UnbindBuyNormalWindow(ref sBuyRoomWindow);
            UnbindBuyNormalWindow(ref sBuyFunctionWindow);
            UnbindBuyNormalWindow(ref sBuyCollectionWindow);

            sBuyController = null;
        }

        private static void UnbindBuyNormalWindow(
            ref WindowBase window)
        {
            if (window != null)
            {
                window.VisibilityChange -=
                    OnBuyNormalWindowVisibilityChange;
            }

            window = null;
        }

        private static void UnbindBuyGrid(
            ref ExpandableCatalogGrid grid)
        {
            if (grid != null)
            {
                grid.ExpandChanged -= OnBuyExpandChanged;
            }

            grid = null;
        }

        // ============================================================
        // BUILD
        // ============================================================

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

            bool changed =
                !object.ReferenceEquals(
                    sBuildController,
                    controller
                ) ||
                !object.ReferenceEquals(
                    sBuildCatalogGrid,
                    catalogGrid
                ) ||
                !object.ReferenceEquals(
                    sBuildCatalogWindow,
                    currentCatalogWindow
                ) ||
                !object.ReferenceEquals(
                    sBuildExpandableGrid,
                    expandable
                );

            if (changed)
            {
                DetachBuild();

                sBuildController = controller;
                sBuildCatalogGrid = catalogGrid;
                sBuildCatalogWindow = currentCatalogWindow;
                sBuildExpandableGrid = expandable;

                sBuildController.Detach -= OnBuildControllerDetach;
                sBuildController.Detach += OnBuildControllerDetach;

                sBuildExpandableGrid.ExpandChanged -= OnBuildExpandChanged;
                sBuildExpandableGrid.ExpandChanged += OnBuildExpandChanged;
            }

            RefreshBuildToggleBindings();
            ApplyBuildVisibility();
        }

        private static void RefreshBuildToggleBindings()
        {
            RebindToggle(
                ref sBuildNormalToggle,
                GetButton(
                    sBuildCatalogWindow,
                    NormalToggleButtonID
                )
            );

            RebindToggle(
                ref sBuildExpandedToggle,
                GetButton(
                    sBuildExpandableGrid,
                    ExpandedToggleButtonID
                )
            );

            SyncToggleVisuals();
        }

        private static void OnBuildExpandChanged()
        {
            RefreshBuildToggleBindings();
            ApplyBuildVisibility();
        }

        private static void ApplyBuildVisibility()
        {
            if (sBuildExpandableGrid == null)
            {
                return;
            }

            bool expanded =
                sBuildExpandableGrid.Expanded;

            // Normal B only in normal view; Expanded B only in expanded view.
            SetVisible(
                sBuildNormalToggle,
                !expanded
            );

            SetVisible(
                sBuildExpandedToggle,
                expanded
            );

            bool showNormalControls =
                !expanded &&
                sInterfaceVisible;

            bool showExpandedControls =
                expanded &&
                sInterfaceVisible;

            Button nativeFilter =
                sBuildCatalogGrid != null
                    ? sBuildCatalogGrid.Tag as Button
                    : null;

            SetVisible(
                nativeFilter,
                showNormalControls
            );

            if (nativeFilter != null &&
                !showNormalControls)
            {
                nativeFilter.Selected = false;
            }

            SetVisible(
                GetWindow(sBuildCatalogWindow, NormalMoveObjectsButtonID),
                showNormalControls
            );

            SetVisible(
                GetWindow(sBuildCatalogWindow, NormalAllCatalogButtonID),
                showNormalControls
            );

            SetVisible(
                GetWindow(sBuildCatalogWindow, NormalSearchWindowID),
                showNormalControls
            );

            SetVisible(
                GetWindow(sBuildCatalogWindow, NormalSaveButtonID),
                showNormalControls
            );

            SetVisible(
                GetWindow(sBuildExpandableGrid, ExpandedMoveObjectsButtonID),
                showExpandedControls
            );

            SetVisible(
                GetWindow(sBuildExpandableGrid, ExpandedAllCatalogButtonID),
                showExpandedControls
            );

            SetVisible(
                GetWindow(sBuildExpandableGrid, ExpandedSearchWindowID),
                showExpandedControls
            );

            SetVisible(
                GetWindow(sBuildExpandableGrid, ExpandedSaveButtonID),
                showExpandedControls
            );

            if (!sInterfaceVisible &&
                sBuildController != null)
            {
                WindowBase filterPanel =
                    sBuildController.GetChildByID(
                        CatalogProductFilterID,
                        true
                    );

                if (filterPanel != null)
                {
                    filterPanel.Visible = false;
                }
            }

            SyncToggleVisuals();
        }

        private static void OnBuildControllerDetach(
            WindowBase sender,
            UIEventArgs eventArgs)
        {
            DetachBuild();
        }

        private static void DetachBuild()
        {
            if (sBuildController != null)
            {
                sBuildController.Detach -= OnBuildControllerDetach;
            }

            if (sBuildExpandableGrid != null)
            {
                sBuildExpandableGrid.ExpandChanged -= OnBuildExpandChanged;
            }

            UnbindToggle(ref sBuildNormalToggle);
            UnbindToggle(ref sBuildExpandedToggle);

            sBuildController = null;
            sBuildCatalogGrid = null;
            sBuildCatalogWindow = null;
            sBuildExpandableGrid = null;
        }

        // ============================================================
        // SHARED BUTTON LOGIC
        // ============================================================

        private static void OnToggleClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            sInterfaceVisible =
                !sInterfaceVisible;

            ApplyBuyVisibility();
            ApplyBuildVisibility();
            SyncToggleVisuals();
        }

        private static void RebindToggle(
            ref Button current,
            Button next)
        {
            if (object.ReferenceEquals(
                    current,
                    next))
            {
                if (current != null)
                {
                    ApplyTogglePresentation(current);
                }
                return;
            }

            if (current != null)
            {
                current.Click -= OnToggleClick;
            }

            current = next;

            if (current != null)
            {
                current.Click -= OnToggleClick;
                current.Click += OnToggleClick;
                ApplyTogglePresentation(current);
            }
        }

        private static void UnbindToggle(
            ref Button button)
        {
            if (button != null)
            {
                button.Click -= OnToggleClick;
            }

            button = null;
        }

        private static void ApplyTogglePresentation(
            Button button)
        {
            if (button == null)
            {
                return;
            }

            button.Selected =
                sInterfaceVisible;

            button.TooltipText =
                sInterfaceVisible
                    ? Localize("HideTooltip")
                    : Localize("ShowTooltip");
        }

        private static void SyncToggleVisuals()
        {
            ApplyTogglePresentation(sBuyRoomNormalToggle);
            ApplyTogglePresentation(sBuyFunctionNormalToggle);
            ApplyTogglePresentation(sBuyCollectionNormalToggle);
            ApplyTogglePresentation(sBuyRoomExpandedToggle);
            ApplyTogglePresentation(sBuyFunctionExpandedToggle);
            ApplyTogglePresentation(sBuyCollectionExpandedToggle);
            ApplyTogglePresentation(sBuildNormalToggle);
            ApplyTogglePresentation(sBuildExpandedToggle);
        }

        private static WindowBase GetWindow(
            WindowBase parent,
            uint id)
        {
            if (parent == null)
            {
                return null;
            }

            return parent.GetChildByID(
                id,
                true
            );
        }

        private static Button GetButton(
            WindowBase parent,
            uint id)
        {
            return GetWindow(parent, id)
                as Button;
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
    }
}
