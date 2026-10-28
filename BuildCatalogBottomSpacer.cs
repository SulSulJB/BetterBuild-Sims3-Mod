using System.Reflection;
using Sims3.SimIFace;
using Sims3.UI;

namespace BetterBuildByJojobou
{
    /*
     * Better Build - BUILD bottom spacer (optimized)
     *
     * Fixes the last Build catalog row being clipped by adding one
     * invisible 51px native Grid row after the last real ItemGrid row.
     *
     * PERFORMANCE DESIGN:
     * - NO ItemRowsChanged subscription.
     * - NO Task.
     * - NO timer.
     * - NO polling.
     *
     * Why this is safe:
     * BuildController.AddGridItem schedules EA's own GridCleanupTick when
     * population ends. GridCleanupTick calls ResizeCatalogGrid() again.
     * Better Build's BuildControllerPatch calls Attach() from that resize,
     * so the spacer is applied once the catalog has finished populating,
     * instead of refreshing the native Grid every 10-item population chunk.
     *
     * Normal + Expanded supported.
     */
    public static class BuildCatalogBottomSpacer
    {
        private const float SpacerHeight = 51f;

        private static readonly BindingFlags Flags =
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public;

        private static readonly FieldInfo NativeGridField =
            typeof(ItemGrid).GetField(
                "mGrid",
                Flags
            );

        private static BuildController sController;
        private static ExpandableCatalogGrid sExpandable;
        private static ItemGrid sGrid;

        private static WindowBase sSpacerCell;
        private static int sSpacerRow = -1;

        public static void Attach(
            BuildController controller,
            ItemGrid itemGrid)
        {
            if (controller == null ||
                itemGrid == null)
            {
                return;
            }

            ExpandableCatalogGrid expandable =
                FindExpandableParent(
                    itemGrid
                );

            if (expandable == null)
            {
                return;
            }

            bool same =
                object.ReferenceEquals(
                    sController,
                    controller
                ) &&
                object.ReferenceEquals(
                    sGrid,
                    itemGrid
                ) &&
                object.ReferenceEquals(
                    sExpandable,
                    expandable
                );

            if (!same)
            {
                UnbindCurrent();

                sController = controller;
                sGrid = itemGrid;
                sExpandable = expandable;

                BindCurrent();

                sController.Detach -=
                    OnControllerDetach;

                sController.Detach +=
                    OnControllerDetach;
            }

            /*
             * This runs:
             * 1) before BeginPopulating() -> ItemRows == 0, nothing added
             * 2) again from EA GridCleanupTick -> final rows exist, spacer added
             */
            ApplySpacer();
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

        private static void BindCurrent()
        {
            /*
             * Intentionally DO NOT bind ItemRowsChanged.
             *
             * ItemGrid fires ItemRowsChanged after every population chunk.
             * The old spacer refreshed the native Grid on every one of those
             * callbacks. The optimized version waits for EA's final cleanup.
             */
            if (sExpandable != null)
            {
                sExpandable.ExpandChanged -=
                    OnExpandableChanged;

                sExpandable.ExpandChanged +=
                    OnExpandableChanged;
            }
        }

        private static void UnbindCurrent()
        {
            if (sExpandable != null)
            {
                sExpandable.ExpandChanged -=
                    OnExpandableChanged;
            }

            RemoveSpacerIfStillOurs();

            sExpandable = null;
            sGrid = null;
            sSpacerCell = null;
            sSpacerRow = -1;
        }

        private static void OnExpandableChanged()
        {
            /*
             * Expanding/collapsing changes the viewport but not the real
             * catalog content. Re-apply only the final spacer state.
             */
            ApplySpacer();
        }

        private static void OnControllerDetach(
            WindowBase sender,
            UIEventArgs eventArgs)
        {
            Detach();
        }

        private static void Detach()
        {
            UnbindCurrent();

            if (sController != null)
            {
                sController.Detach -=
                    OnControllerDetach;
            }

            sController = null;
        }

        private static Grid GetNativeGrid(
            ItemGrid itemGrid)
        {
            if (itemGrid == null ||
                NativeGridField == null)
            {
                return null;
            }

            return
                NativeGridField.GetValue(
                    itemGrid
                ) as Grid;
        }

        private static WindowBase CreateSpacerCell()
        {
            /*
             * Use EA's Build catalog item layout only as a harmless native
             * Grid cell container. It is resized to 0x0 and disabled.
             */
            ResourceKey key =
                ResourceKey.CreateUILayoutKey(
                    "BuildCatalogItem",
                    0U
                );

            Layout layout =
                UIManager.LoadLayout(
                    key
                );

            if (layout == null)
            {
                return null;
            }

            WindowBase spacer =
                layout.GetWindowByExportID(
                    1
                );

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

        private static void ApplySpacer()
        {
            if (sGrid == null ||
                sExpandable == null)
            {
                return;
            }

            Grid nativeGrid =
                GetNativeGrid(
                    sGrid
                );

            if (nativeGrid == null)
            {
                return;
            }

            int realRows =
                (int)sGrid.ItemRows;

            if (realRows < 0)
            {
                realRows = 0;
            }

            bool useSpacer =
                sGrid.VisibleRows >= 2U &&
                !sGrid.HasTempItem &&
                realRows > 0;

            /*
             * If EA cleared/rebuilt the native grid, our old reference may
             * still exist in C# even though the cell itself is gone.
             */
            if (sSpacerCell != null &&
                sSpacerRow >= 0)
            {
                bool stillOurs =
                    sSpacerRow < nativeGrid.RowCount &&
                    object.ReferenceEquals(
                        nativeGrid.GetCellWindow(
                            0,
                            sSpacerRow
                        ),
                        sSpacerCell
                    );

                if (!stillOurs)
                {
                    sSpacerCell = null;
                    sSpacerRow = -1;
                }
            }

            if (!useSpacer)
            {
                bool changed =
                    RemoveSpacerIfStillOurs();

                if (nativeGrid.RowCount !=
                    realRows)
                {
                    nativeGrid.RowCount =
                        realRows;

                    changed = true;
                }

                sSpacerRow = -1;

                if (changed)
                {
                    nativeGrid.Refresh();
                }

                RefreshScrollbar(
                    sGrid,
                    nativeGrid
                );

                return;
            }

            /*
             * Already correct: do not rewrite the Grid and do not Refresh().
             * We only refresh scrollbar metrics in case the viewport changed.
             */
            bool alreadyInstalled =
                sSpacerCell != null &&
                sSpacerRow == realRows &&
                nativeGrid.RowCount ==
                    realRows + 1 &&
                object.ReferenceEquals(
                    nativeGrid.GetCellWindow(
                        0,
                        realRows
                    ),
                    sSpacerCell
                );

            if (alreadyInstalled)
            {
                RefreshScrollbar(
                    sGrid,
                    nativeGrid
                );

                return;
            }

            /*
             * Move/remove an old spacer if this same ItemGrid was rebuilt.
             */
            RemoveSpacerIfStillOurs();

            if (sSpacerCell == null)
            {
                sSpacerCell =
                    CreateSpacerCell();
            }

            if (sSpacerCell == null)
            {
                return;
            }

            if (nativeGrid.RowCount !=
                realRows + 1)
            {
                nativeGrid.RowCount =
                    realRows + 1;
            }

            nativeGrid.SetRowHeight(
                realRows,
                SpacerHeight
            );

            nativeGrid.SetCellWindow(
                0,
                realRows,
                sSpacerCell,
                false
            );

            sSpacerRow =
                realRows;

            /*
             * Exactly ONE explicit native Grid refresh for a newly-installed
             * spacer. No population-chunk refresh loop.
             */
            nativeGrid.Refresh();

            RefreshScrollbar(
                sGrid,
                nativeGrid
            );
        }

        private static bool RemoveSpacerIfStillOurs()
        {
            if (sGrid == null ||
                sSpacerCell == null ||
                sSpacerRow < 0)
            {
                return false;
            }

            Grid nativeGrid =
                GetNativeGrid(
                    sGrid
                );

            if (nativeGrid == null ||
                sSpacerRow >=
                    nativeGrid.RowCount)
            {
                return false;
            }

            if (!object.ReferenceEquals(
                    nativeGrid.GetCellWindow(
                        0,
                        sSpacerRow
                    ),
                    sSpacerCell
                ))
            {
                return false;
            }

            nativeGrid.ClearCell(
                0,
                sSpacerRow
            );

            return true;
        }

        private static void RefreshScrollbar(
            ItemGrid itemGrid,
            Grid nativeGrid)
        {
            if (itemGrid == null ||
                nativeGrid == null)
            {
                return;
            }

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
                upperBound >
                visibleRange;
        }
    }
}