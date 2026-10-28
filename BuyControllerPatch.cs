using MonoPatcherLib;
using Sims3.SimIFace;
using Sims3.UI;
using System;

namespace BetterBuildByJojobou
{
    [TypePatch(typeof(BuyController))]
    public class BuyControllerPatch
    {
        private const uint RoomWindowID = 0x00002000U;
        private const uint CollectionCatalogWindowID = 0x00000420U;

        /*
         * Right-hand breathing room between the last column and the
         * scrollbar (pixels). Tune these by eye in game.
         *
         * Sort by ROOM used to keep ALL the leftover width, which left more
         * space on the right than on the left: RoomRightTrim removes part
         * of it. Raise it to tighten the right side, lower it to loosen.
         *
         * Sort by FUNCTION (and categories) kept only whole columns, so the
         * last column touched the scrollbar: FunctionRightPadding adds a
         * little room (never more than the width that is really free).
         */
        private const float RoomRightTrim = 8f;
        private const float FunctionRightPadding = 4f;

        private void ResizeCatalogGrid(
            WindowBase window,
            ExpandableCatalogGrid exGrid,
            float defaultWidth,
            uint defaultColumns,
            int gridCount,
            bool forceResize)
        {
            /*
             * Placement controls must be attached independently of the catalog
             * resize calculation below. ResizeCatalogGrid can return early
             * (for example if the grid is not ready yet), and we must not
             * lose the Better Build placement handlers because of that.
             */
            BetterBuildPlacementControls.Attach(
                (BuyController)(object)this
            );

            gridCount = Math.Max(4, gridCount);

            SceneMgrWindow sceneWindow =
                UIManager.GetSceneWindow();

            if (sceneWindow == null)
            {
                return;
            }

            Vector2 defaultRightEdge =
                window.WindowToScreen(
                    new Vector2(
                        defaultWidth,
                        0f
                    )
                );

            float availableWidth =
                sceneWindow.Area.Width -
                defaultRightEdge.x;

            float columnWidth =
                exGrid.CellArea.x +
                exGrid.CellPadding.Width;

            if (columnWidth <= 0f)
            {
                return;
            }

            int extraColumns =
                (int)Math.Floor(
                    (double)(
                        availableWidth /
                        columnWidth
                    )
                );

            if (extraColumns < 0)
            {
                extraColumns = 0;
            }

            float usedByColumns =
                (float)extraColumns *
                columnWidth;

            float remainingWidth =
                availableWidth -
                usedByColumns;

            if (remainingWidth < 0f)
            {
                remainingWidth = 0f;
            }

            float newWidth =
                defaultWidth +
                usedByColumns;

            bool isRoom =
                window.ID ==
                RoomWindowID;

            bool isCollection =
                window.ID ==
                CollectionCatalogWindowID;

            if (isRoom)
            {
                float roomExtra =
                    remainingWidth - RoomRightTrim;

                if (roomExtra < 0f)
                {
                    roomExtra = 0f;
                }

                newWidth +=
                    roomExtra;
            }
            else if (!isCollection)
            {
                float functionExtra =
                    Math.Min(
                        FunctionRightPadding,
                        remainingWidth
                    );

                if (functionExtra > 0f)
                {
                    newWidth +=
                        functionExtra;
                }
            }

            Rect area =
                window.Area;

            area.Width =
                newWidth;

            window.Area =
                area;

            uint visibleColumns =
                defaultColumns +
                (uint)extraColumns;

            if (isRoom &&
                remainingWidth > 0f)
            {
                visibleColumns++;
            }

            /*
             * COLLECTION only:
             *
             * BuildExpandableCatalogGrid keeps a different margin / scrollbar
             * behavior than BuyExpandableCatalogGrid. With the standard
             * calculation, one extra column is reported as visible and
             * overflows on the right.
             *
             * Keep the blue background at the same width, but remove only
             * that extra logical column.
             */
            if (isCollection &&
                visibleColumns >
                    defaultColumns)
            {
                visibleColumns--;
            }

            exGrid.VisibleColumns =
                visibleColumns;

            CatalogSearch.Attach(
                (BuyController)(object)this
            );

            BuyFilterExtended.Attach(
                (BuyController)(object)this
            );

            MoveObjectsToggle.Attach(
                (BuyController)(object)this
            );

            QuickSaveButton.Attach(
                (BuyController)(object)this
            );

            BuyCatalogBottomSpacer.Attach(
                (BuyController)(object)this
            );
            BetterBuildInterfaceToggle.Attach(
                (BuyController)(object)this
            );

            /*
             * IMPORTANT:
             * No CatalogThumbnailQuality.Attach() here anymore.
             *
             * CatalogItemHD patches ItemGrid.AddItem() and replaces
             * the thumbnail BEFORE the cell enters the grid.
             */
        }
    }
}