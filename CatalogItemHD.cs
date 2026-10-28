using System;
using System.Reflection;
using MonoPatcherLib;
using Sims3.SimIFace;
using Sims3.SimIFace.BuildBuy;
using Sims3.UI;

namespace BetterBuildByJojobou
{
    /*
     * Better Build - Native HD catalog item injection
     *
     * BUY:
     * - 81x81 catalog cells
     * - native LARGE thumbnails
     *
     * BUILD:
     * - 81x81 catalog cells
     * - EA handles Build thumbnails normally
     *
     * No Task.
     * No timer.
     * No polling.
     */
    [TypePatch(typeof(ItemGrid))]
    public class CatalogItemHD
    {
        private const uint RoomCatalogID =
            0x00002101U;

        private const uint FunctionCatalogID =
            0x00002102U;

        /*
         * BUY Collections:
         * BuyController.mGridCollection = 1059 (0x423)
         */
        private const uint CollectionCatalogGridID =
            0x00000423U;

        /*
         * BUY product cells use 0x05AC1600.
         */
        private const uint BuyProductThumbnailWindowID =
            95163904U;

        /*
         * BUY presets use child 32.
         */
        private const uint BuildThumbnailWindowID =
            32U;

        /*
         * Build HD thumbnails: set BuildHDEnabled to false to turn them off.
         * BuildHDMaxItems = largest catalog (number of items) that still gets
         * HD thumbnails. Raise it if there is no lag, lower it if there is.
         */
        private static readonly bool BuildHDEnabled = true;

        private const int BuildHDMaxItems = 250;

        /*
         * Number of items of the Build catalog being populated.
         * Set by BuildControllerPatch.ResizeCatalogGrid() right before EA
         * fills the grid.
         */
        internal static int BuildItemCount;

        private const float BetterBuildCellSize =
            81f;

        private enum HDTarget
        {
            None,
            Buy,
            Build
        }

        private static readonly BindingFlags Flags =
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public;

        private static readonly FieldInfo GridField =
            typeof(ItemGrid).GetField(
                "mGrid",
                Flags
            );

        private static readonly FieldInfo TempIField =
            typeof(ItemGrid).GetField(
                "mTempEntryI",
                Flags
            );

        private static readonly FieldInfo TempJField =
            typeof(ItemGrid).GetField(
                "mTempEntryJ",
                Flags
            );

        private static readonly FieldInfo LastIField =
            typeof(ItemGrid).GetField(
                "mLastEntryI",
                Flags
            );

        private static readonly FieldInfo LastJField =
            typeof(ItemGrid).GetField(
                "mLastEntryJ",
                Flags
            );

        private static readonly FieldInfo StretchField =
            typeof(ItemGrid).GetField(
                "mbStretchCellWindows",
                Flags
            );

        private static readonly FieldInfo PopulatingField =
            typeof(ItemGrid).GetField(
                "mPopulating",
                Flags
            );

        private static readonly FieldInfo ItemRowsChangedField =
            typeof(ItemGrid).GetField(
                "ItemRowsChanged",
                Flags
            );

        private static readonly MethodInfo UpdateScrollbarMethod =
            typeof(ItemGrid).GetMethod(
                "UpdateScrollbar",
                Flags,
                null,
                Type.EmptyTypes,
                null
            );

        private static readonly FieldInfo BuildControllerField =
            typeof(BuildController).GetField(
                "sController",
                BindingFlags.Static |
                BindingFlags.NonPublic |
                BindingFlags.Public
            );

        private static readonly FieldInfo BuildCurrentGridField =
            typeof(BuildController).GetField(
                "mCurrCatalogGrid",
                Flags
            );

        private static int GetInt(
            FieldInfo field,
            ItemGrid grid)
        {
            if (field == null)
            {
                return -1;
            }

            return
                (int)field.GetValue(grid);
        }

        private static void SetInt(
            FieldInfo field,
            ItemGrid grid,
            int value)
        {
            if (field != null)
            {
                field.SetValue(
                    grid,
                    value
                );
            }
        }

        private static bool GetBool(
            FieldInfo field,
            ItemGrid grid)
        {
            return
                field != null &&
                (bool)field.GetValue(grid);
        }

        private static HDTarget GetHDTarget(
            ItemGrid itemGrid)
        {
            if (itemGrid == null)
            {
                return HDTarget.None;
            }

            /*
             * BUY:
             * - Sort by Room
             * - Sort by Function
             * - Collections
             */
            WindowBase current =
                itemGrid.Parent;

            while (current != null)
            {
                if (current.ID == RoomCatalogID ||
                    current.ID == FunctionCatalogID ||
                    current.ID == CollectionCatalogGridID)
                {
                    return HDTarget.Buy;
                }

                current =
                    current.Parent;
            }

            /*
             * BUILD:
             * EA swaps mCurrCatalogGrid whenever
             * the Build category changes.
             */
            if (BuildControllerField != null &&
                BuildCurrentGridField != null)
            {
                BuildController controller =
                    BuildControllerField.GetValue(null)
                    as BuildController;

                if (controller != null)
                {
                    ItemGrid currentBuildGrid =
                        BuildCurrentGridField.GetValue(controller)
                        as ItemGrid;

                    if (object.ReferenceEquals(
                            itemGrid,
                            currentBuildGrid))
                    {
                        return HDTarget.Build;
                    }
                }
            }

            return HDTarget.None;
        }

        private static void PrepareBetterBuildItem(
            ItemGridCellItem item,
            HDTarget target)
        {
            if (item.mWin == null)
            {
                return;
            }

            /*
             * Keep Better Build's 81x81 root cell.
             */
            Rect cellArea =
                item.mWin.Area;

            item.mWin.Area =
                new Rect(
                    cellArea.TopLeft.x,
                    cellArea.TopLeft.y,
                    cellArea.TopLeft.x +
                        BetterBuildCellSize,
                    cellArea.TopLeft.y +
                        BetterBuildCellSize
                );

            /*
             * BUILD:
             * HD thumbnails are only applied to SMALL catalogs (doors,
             * windows, stairs, fences...). Huge pattern catalogs (walls,
             * floors, terrain paints) keep EA's native thumbnails, because
             * loading a LARGE thumbnail for every cell made the game lag.
             */
            if (target == HDTarget.Build &&
                (!BuildHDEnabled ||
                 BuildItemCount > BuildHDMaxItems))
            {
                return;
            }

            /*
             * From here on, only BUY should continue.
             */
            if (target != HDTarget.Buy)
            {
                return;
            }

            ResourceKey thumbnailKey =
                ResourceKey.kInvalidResourceKey;

            uint thumbnailWindowID =
                0U;

            BuildBuyProduct product =
                item.mTag as BuildBuyProduct;

            if (product != null)
            {
                thumbnailKey =
                    product.ProductResourceKey;

                thumbnailWindowID =
                    target == HDTarget.Build
                        ? BuildThumbnailWindowID
                        : BuyProductThumbnailWindowID;
            }
            else
            {
                BuildBuyPreset preset =
                    item.mTag as BuildBuyPreset;

                if (preset == null)
                {
                    return;
                }

                thumbnailKey =
                    preset.KeyForThumbnail;

                thumbnailWindowID =
                    BuildThumbnailWindowID;
            }

            Window thumbnailWindow =
                item.mWin.GetChildByID(
                    thumbnailWindowID,
                    true
                ) as Window;

            if (thumbnailWindow == null)
            {
                return;
            }

            ImageDrawable drawable =
                thumbnailWindow.Drawable
                as ImageDrawable;

            if (drawable == null)
            {
                return;
            }

            /*
             * BUY:
             * use EA's native LARGE thumbnail.
             */
            UIImage image =
                UIManager.GetThumbnailImage(
                    new ThumbnailKey(
                        thumbnailKey,
                        ThumbnailSize.Large
                    )
                );

            /*
             * Fallback to Medium if Large doesn't exist.
             */
            if (image == null)
            {
                image =
                    UIManager.GetThumbnailImage(
                        new ThumbnailKey(
                            thumbnailKey,
                            ThumbnailSize.Medium
                        )
                    );
            }

            /*
             * IMPORTANT:
             *
             * Keep the original behavior that previously
             * produced the visible HD result.
             *
             * No ReferenceEquals optimization here.
             */
            if (image != null)
            {
                drawable.Image =
                    image;

                thumbnailWindow.Invalidate();
            }
        }

        /*
         * Replacement for ItemGrid.AddItem().
         *
         * EA logic is preserved.
         */
        public void AddItem(
            ItemGridCellItem item)
        {
            ItemGrid self =
                (ItemGrid)(object)this;

            if (self.VisibleRows == 0U ||
                self.VisibleColumns == 0U)
            {
                return;
            }

            Grid grid =
                GridField == null
                    ? null
                    : GridField.GetValue(self)
                        as Grid;

            if (grid == null)
            {
                return;
            }

            int tempI =
                GetInt(
                    TempIField,
                    self
                );

            int tempJ =
                GetInt(
                    TempJField,
                    self
                );

            int lastI =
                GetInt(
                    LastIField,
                    self
                );

            int lastJ =
                GetInt(
                    LastJField,
                    self
                );

            bool horizontal =
                self.HorizontalScrolling;

            int entriesCountI =
                horizontal
                    ? grid.RowCount
                    : grid.ColumnCount;

            if (entriesCountI <= 0)
            {
                return;
            }

            int column;
            int row;

            if (tempI == -1)
            {
                lastI =
                    (lastI + 1) %
                    entriesCountI;

                if (lastI == 0)
                {
                    lastJ++;

                    if (horizontal)
                    {
                        grid.ColumnCount =
                            lastJ + 1;

                        grid.SetColumnWidth(
                            lastJ,
                            grid.DefaultColumnWidth
                        );
                    }
                    else
                    {
                        grid.RowCount =
                            lastJ + 1;

                        grid.SetRowHeight(
                            lastJ,
                            grid.DefaultRowHeight
                        );
                    }
                }

                column =
                    horizontal
                        ? lastJ
                        : lastI;

                row =
                    horizontal
                        ? lastI
                        : lastJ;
            }
            else
            {
                lastJ =
                    tempJ;

                lastI =
                    tempI;

                column =
                    horizontal
                        ? lastJ
                        : lastI;

                row =
                    horizontal
                        ? lastI
                        : lastJ;

                grid.ClearCell(
                    column,
                    row
                );

                tempI =
                    -1;

                tempJ =
                    -1;
            }

            SetInt(
                LastIField,
                self,
                lastI
            );

            SetInt(
                LastJField,
                self,
                lastJ
            );

            SetInt(
                TempIField,
                self,
                tempI
            );

            SetInt(
                TempJField,
                self,
                tempJ
            );

            /*
             * Better Build preparation.
             */
            HDTarget hdTarget =
                GetHDTarget(
                    self
                );

            if (hdTarget != HDTarget.None)
            {
                PrepareBetterBuildItem(
                    item,
                    hdTarget
                );
            }

            if (item.mWin == null)
            {
                return;
            }

            item.mWin.Visible =
                false;

            bool stretchCellWindows =
                GetBool(
                    StretchField,
                    self
                );

            grid.SetCellWindow(
                column,
                row,
                item.mWin,
                stretchCellWindows
            );

            grid.CellTags[
                column,
                row
            ] = item.mTag;

            bool populating =
                GetBool(
                    PopulatingField,
                    self
                );

            if (!populating)
            {
                grid.Refresh();

                if (UpdateScrollbarMethod != null)
                {
                    UpdateScrollbarMethod.Invoke(
                        self,
                        null
                    );
                }

                if (ItemRowsChangedField != null)
                {
                    Delegate callback =
                        ItemRowsChangedField.GetValue(
                            self
                        ) as Delegate;

                    if (callback != null)
                    {
                        callback.DynamicInvoke(
                            new object[0]
                        );
                    }
                }
            }
        }
    }
}