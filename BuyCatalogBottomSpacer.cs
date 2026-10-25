using System.Reflection;
using Sims3.SimIFace;
using Sims3.UI;

namespace BetterBuildByJojobou
{
    /*
     * Better Build - Buy catalog bottom spacer
     *
     * Targets:
     * - Sort by Room
     * - Sort by Function
     * - Collections
     * - Normal + Extended
     *
     * Principle:
     * the native grid ignores an empty row when calculating scroll range.
     * Add one real invisible cell in a 51 px spacer row after the last
     * actual catalog row.
     *
     * No Task, timer, or polling.
     * No diagnostic notifications.
     */
    public static class BuyCatalogBottomSpacer
    {
        private const float SpacerHeight = 51f;

        private enum CatalogTarget
        {
            Room,
            Function,
            Collection
        }

        private static readonly BindingFlags Flags =
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public;

        private static BuyController sController;

        private static ExpandableCatalogGrid sRoomExpandable;
        private static ExpandableCatalogGrid sFunctionExpandable;
        private static ExpandableCatalogGrid sCollectionExpandable;

        private static ItemGrid sRoomGrid;
        private static ItemGrid sFunctionGrid;
        private static ItemGrid sCollectionGrid;

        private static WindowBase sRoomSpacerCell;
        private static WindowBase sFunctionSpacerCell;
        private static WindowBase sCollectionSpacerCell;

        private static int sRoomSpacerRow = -1;
        private static int sFunctionSpacerRow = -1;
        private static int sCollectionSpacerRow = -1;

        public static void Attach(BuyController controller)
        {
            if (controller == null)
            {
                return;
            }

            if (object.ReferenceEquals(sController, controller))
            {
                RefreshReferences(controller);
                ApplyAll();
                return;
            }

            Detach();

            sController = controller;

            RefreshReferences(controller);

            sController.Detach -= OnControllerDetach;
            sController.Detach += OnControllerDetach;

            ApplyAll();
        }

        private static void RefreshReferences(
            BuyController controller)
        {
            ExpandableCatalogGrid newRoom =
                GetExpandableGrid(
                    controller,
                    "mGridSortByRoom"
                );

            ItemGrid newRoomGrid =
                newRoom != null
                    ? newRoom.Grid
                    : null;

            ExpandableCatalogGrid newFunction =
                GetExpandableGrid(
                    controller,
                    "mGridSortByFunction"
                );

            ItemGrid newFunctionGrid =
                newFunction != null
                    ? newFunction.Grid
                    : null;

            ExpandableCatalogGrid newCollection =
                GetExpandableGrid(
                    controller,
                    "mGridCollection"
                );

            ItemGrid newCollectionGrid =
                newCollection != null
                    ? newCollection.Grid
                    : null;

            if (!object.ReferenceEquals(
                    sRoomExpandable,
                    newRoom) ||
                !object.ReferenceEquals(
                    sRoomGrid,
                    newRoomGrid))
            {
                UnbindExpandable(
                    sRoomExpandable,
                    sRoomGrid
                );

                sRoomExpandable =
                    newRoom;

                sRoomGrid =
                    newRoomGrid;

                BindExpandable(
                    sRoomExpandable,
                    sRoomGrid
                );
            }

            if (!object.ReferenceEquals(
                    sFunctionExpandable,
                    newFunction) ||
                !object.ReferenceEquals(
                    sFunctionGrid,
                    newFunctionGrid))
            {
                UnbindExpandable(
                    sFunctionExpandable,
                    sFunctionGrid
                );

                sFunctionExpandable =
                    newFunction;

                sFunctionGrid =
                    newFunctionGrid;

                BindExpandable(
                    sFunctionExpandable,
                    sFunctionGrid
                );
            }

            if (!object.ReferenceEquals(
                    sCollectionExpandable,
                    newCollection) ||
                !object.ReferenceEquals(
                    sCollectionGrid,
                    newCollectionGrid))
            {
                UnbindExpandable(
                    sCollectionExpandable,
                    sCollectionGrid
                );

                sCollectionExpandable =
                    newCollection;

                sCollectionGrid =
                    newCollectionGrid;

                BindExpandable(
                    sCollectionExpandable,
                    sCollectionGrid
                );
            }
        }

        private static ExpandableCatalogGrid GetExpandableGrid(
            BuyController controller,
            string fieldName)
        {
            FieldInfo field =
                typeof(BuyController).GetField(
                    fieldName,
                    Flags
                );

            if (field == null)
            {
                return null;
            }

            return field.GetValue(controller)
                as ExpandableCatalogGrid;
        }

        private static void BindExpandable(
            ExpandableCatalogGrid expandable,
            ItemGrid grid)
        {
            if (expandable != null)
            {
                expandable.ExpandChanged -=
                    OnExpandableChanged;

                expandable.ExpandChanged +=
                    OnExpandableChanged;
            }

            BindGrid(grid);
        }

        private static void UnbindExpandable(
            ExpandableCatalogGrid expandable,
            ItemGrid grid)
        {
            if (expandable != null)
            {
                expandable.ExpandChanged -=
                    OnExpandableChanged;
            }

            UnbindGrid(grid);
        }

        private static void BindGrid(
            ItemGrid grid)
        {
            if (grid == null)
            {
                return;
            }

            grid.ItemRowsChanged -=
                OnGridChanged;

            grid.ItemRowsChanged +=
                OnGridChanged;

            grid.VisibleSizeChanged -=
                OnGridChanged;

            grid.VisibleSizeChanged +=
                OnGridChanged;
        }

        private static void UnbindGrid(
            ItemGrid grid)
        {
            if (grid == null)
            {
                return;
            }

            grid.ItemRowsChanged -=
                OnGridChanged;

            grid.VisibleSizeChanged -=
                OnGridChanged;
        }

        private static void OnGridChanged()
        {
            ApplyAll();
        }

        private static void OnExpandableChanged()
        {
            ApplyAll();
        }

        private static void OnControllerDetach(
            WindowBase sender,
            UIEventArgs eventArgs)
        {
            Detach();
        }

        private static void Detach()
        {
            UnbindExpandable(
                sRoomExpandable,
                sRoomGrid
            );

            UnbindExpandable(
                sFunctionExpandable,
                sFunctionGrid
            );

            UnbindExpandable(
                sCollectionExpandable,
                sCollectionGrid
            );

            if (sController != null)
            {
                sController.Detach -=
                    OnControllerDetach;
            }

            sRoomGrid = null;
            sFunctionGrid = null;
            sCollectionGrid = null;

            sRoomExpandable = null;
            sFunctionExpandable = null;
            sCollectionExpandable = null;

            sRoomSpacerCell = null;
            sFunctionSpacerCell = null;
            sCollectionSpacerCell = null;

            sRoomSpacerRow = -1;
            sFunctionSpacerRow = -1;
            sCollectionSpacerRow = -1;

            sController = null;
        }

        private static Grid GetNativeGrid(
            ItemGrid itemGrid)
        {
            if (itemGrid == null)
            {
                return null;
            }

            FieldInfo field =
                typeof(ItemGrid).GetField(
                    "mGrid",
                    Flags
                );

            if (field == null)
            {
                return null;
            }

            return field.GetValue(itemGrid)
                as Grid;
        }

        private static WindowBase CreateSpacerCell()
        {
            ResourceKey key =
                ResourceKey.CreateUILayoutKey(
                    "BuyCatalogItem",
                    0U
                );

            Layout layout =
                UIManager.LoadLayout(key);

            if (layout == null)
            {
                return null;
            }

            WindowBase spacer =
                layout.GetWindowByExportID(1);

            if (spacer == null)
            {
                return null;
            }

            spacer.Area =
                new Rect(
                    0f,
                    0f,
                    0f,
                    0f
                );

            spacer.Enabled = false;
            spacer.Visible = true;
            spacer.DisposeOnDetach = true;

            return spacer;
        }

        private static void ApplyAll()
        {
            ApplySpacer(
                sRoomGrid,
                sRoomExpandable,
                CatalogTarget.Room
            );

            ApplySpacer(
                sFunctionGrid,
                sFunctionExpandable,
                CatalogTarget.Function
            );

            ApplySpacer(
                sCollectionGrid,
                sCollectionExpandable,
                CatalogTarget.Collection
            );
        }

        private static WindowBase GetSpacerCell(
            CatalogTarget target)
        {
            if (target == CatalogTarget.Room)
            {
                return sRoomSpacerCell;
            }

            if (target == CatalogTarget.Function)
            {
                return sFunctionSpacerCell;
            }

            return sCollectionSpacerCell;
        }

        private static void SetSpacerCell(
            CatalogTarget target,
            WindowBase spacer)
        {
            if (target == CatalogTarget.Room)
            {
                sRoomSpacerCell = spacer;
            }
            else if (target == CatalogTarget.Function)
            {
                sFunctionSpacerCell = spacer;
            }
            else
            {
                sCollectionSpacerCell = spacer;
            }
        }

        private static int GetSpacerRow(
            CatalogTarget target)
        {
            if (target == CatalogTarget.Room)
            {
                return sRoomSpacerRow;
            }

            if (target == CatalogTarget.Function)
            {
                return sFunctionSpacerRow;
            }

            return sCollectionSpacerRow;
        }

        private static void SetSpacerRow(
            CatalogTarget target,
            int row)
        {
            if (target == CatalogTarget.Room)
            {
                sRoomSpacerRow = row;
            }
            else if (target == CatalogTarget.Function)
            {
                sFunctionSpacerRow = row;
            }
            else
            {
                sCollectionSpacerRow = row;
            }
        }

        private static void ApplySpacer(
            ItemGrid itemGrid,
            ExpandableCatalogGrid expandable,
            CatalogTarget target)
        {
            if (itemGrid == null ||
                expandable == null)
            {
                return;
            }

            Grid nativeGrid =
                GetNativeGrid(itemGrid);

            if (nativeGrid == null)
            {
                return;
            }

            int realRows =
                (int)itemGrid.ItemRows;

            if (realRows < 0)
            {
                realRows = 0;
            }

            /*
             * Use the same spacer rule for:
             * Normal / Extended / Collections.
             */
            bool useSpacer =
                itemGrid.VisibleRows >= 2U &&
                !itemGrid.HasTempItem &&
                realRows > 0;

            int oldSpacerRow =
                GetSpacerRow(target);

            WindowBase spacer =
                GetSpacerCell(target);

            /*
             * If EA replaced the previous spacer cell with a real catalog
             * item while populating the grid, never touch that real item.
             *
             * Drop the stale spacer reference and create a new spacer at
             * the new end of the catalog.
             */
            if (oldSpacerRow >= 0 &&
                oldSpacerRow < nativeGrid.RowCount &&
                spacer != null &&
                !object.ReferenceEquals(
                    nativeGrid.GetCellWindow(
                        0,
                        oldSpacerRow
                    ),
                    spacer
                ))
            {
                spacer = null;
                SetSpacerCell(
                    target,
                    null
                );
            }

            /*
             * Remove the old spacer only if the cell still contains
             * the exact spacer window created by Better Build.
             * This prevents holes in the catalog.
             */
            if (oldSpacerRow >= 0 &&
                oldSpacerRow < nativeGrid.RowCount &&
                oldSpacerRow != realRows &&
                spacer != null &&
                object.ReferenceEquals(
                    nativeGrid.GetCellWindow(
                        0,
                        oldSpacerRow
                    ),
                    spacer
                ))
            {
                nativeGrid.ClearCell(
                    0,
                    oldSpacerRow
                );
            }

            if (useSpacer)
            {
                if (spacer == null)
                {
                    spacer =
                        CreateSpacerCell();

                    SetSpacerCell(
                        target,
                        spacer
                    );
                }

                if (spacer == null)
                {
                    return;
                }

                nativeGrid.RowCount =
                    realRows + 1;

                nativeGrid.SetRowHeight(
                    realRows,
                    SpacerHeight
                );

                nativeGrid.SetCellWindow(
                    0,
                    realRows,
                    spacer,
                    false
                );

                SetSpacerRow(
                    target,
                    realRows
                );
            }
            else
            {
                if (oldSpacerRow >= 0 &&
                    oldSpacerRow < nativeGrid.RowCount &&
                    spacer != null &&
                    object.ReferenceEquals(
                        nativeGrid.GetCellWindow(
                            0,
                            oldSpacerRow
                        ),
                        spacer
                    ))
                {
                    nativeGrid.ClearCell(
                        0,
                        oldSpacerRow
                    );
                }

                if (nativeGrid.RowCount != realRows)
                {
                    nativeGrid.RowCount =
                        realRows;
                }

                SetSpacerRow(
                    target,
                    -1
                );
            }

            nativeGrid.Refresh();

            RefreshScrollbar(
                itemGrid,
                nativeGrid
            );
        }

        private static void RefreshScrollbar(
            ItemGrid itemGrid,
            Grid nativeGrid)
        {
            int visibleRange = 0;
            int upperBound = 0;
            uint increment = 0U;

            nativeGrid.GetScrollMetrics(
                false,
                ref visibleRange,
                ref upperBound,
                ref increment
            );

            Scrollbar scrollbar =
                itemGrid.VScrollbar;

            if (scrollbar == null)
            {
                return;
            }

            scrollbar.UpperBoundValue =
                upperBound;

            scrollbar.VisibleRange =
                visibleRange;

            scrollbar.ArrowDelta =
                (int)increment;

            scrollbar.Visible =
                upperBound > visibleRange;
        }
    }
}
