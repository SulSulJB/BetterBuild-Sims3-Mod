using System.Reflection;
using MonoPatcherLib;
using Sims3.SimIFace;
using Sims3.UI;

namespace BetterBuildByJojobou
{
    /*
     * Better Build - BuildController hook
     *
     * FULL-WIDTH PERFORMANCE FIX + optimized Build bottom spacer.
     *
     * No Task.
     * No timer.
     * No polling.
     */
    [TypePatch(typeof(BuildController))]
    public class BuildControllerPatch
    {
        private static readonly BindingFlags Flags =
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public;

        private static readonly FieldInfo CatalogProductFilterField =
            typeof(BuildController).GetField(
                "mCatalogProductFilter",
                Flags
            );

        private static readonly FieldInfo ForceResizeField =
            typeof(BuildController).GetField(
                "mbForceResizeCatalogGrid",
                Flags
            );

        private static readonly FieldInfo CatalogNeedsRefreshField =
            typeof(BuildController).GetField(
                "mCatalogNeedsARefresh",
                Flags
            );


        private static readonly MethodInfo RebuildCatalogMethod =
            typeof(BuildController).GetMethod(
                "RebuildCatalog",
                Flags,
                null,
                System.Type.EmptyTypes,
                null
            );



        /*
         * ItemGrid internals used for the no-repopulate column fix.
         *
         * The public VisibleColumns setter calls RepopulateGrid() when the
         * value changes. On very large Build catalogs that is the expensive
         * path we want to avoid.
         *
         * Build clears the ItemGrid before ResizeCatalogGrid() and populates it
         * immediately afterward, so while ItemRows == 0 we can safely prepare
         * the empty native grid directly.
         */
        private static readonly FieldInfo VisibleColsField =
            typeof(ItemGrid).GetField(
                "mVisibleCols",
                Flags
            );

        private static readonly FieldInfo NativeGridField =
            typeof(ItemGrid).GetField(
                "mGrid",
                Flags
            );

        private static readonly MethodInfo UpdateGridSizeMethod =
            typeof(ItemGrid).GetMethod(
                "UpdateGridSize",
                Flags,
                null,
                System.Type.EmptyTypes,
                null
            );

        private static readonly MethodInfo UpdateScrollbarMethod =
            typeof(ItemGrid).GetMethod(
                "UpdateScrollbar",
                Flags,
                null,
                System.Type.EmptyTypes,
                null
            );

        /*
         * EA's Build filter normally swaps the star icon after filters change.
         *
         * Better Build preserves the important native behavior:
         * - mark catalog dirty
         * - rebuild catalog
         *
         * but skips EA's old star-icon replacement.
         */
        private void CatalogProductFilterChanged()
        {
            BuildController self =
                (BuildController)(object)this;

            if (CatalogNeedsRefreshField != null)
            {
                CatalogNeedsRefreshField.SetValue(
                    self,
                    true
                );
            }

            if (RebuildCatalogMethod != null)
            {
                RebuildCatalogMethod.Invoke(
                    self,
                    null
                );
            }
        }

        private static void SetVisibleColumnsWithoutRepopulate(
            ItemGrid catalogGrid,
            uint columns)
        {
            if (catalogGrid == null ||
                columns < 1U)
            {
                return;
            }

            /*
             * IMPORTANT SAFETY RULE:
             * only touch the native layout while the catalog is EMPTY.
             *
             * PopulateCatalogGrid() clears ItemGrid before calling
             * ResizeCatalogGrid(), so this is the safe window.
             *
             * If a later UI resize happens while items are already present,
             * leave the existing column layout alone.
             */
            if (catalogGrid.ItemRows != 0U)
            {
                return;
            }

            if (VisibleColsField == null ||
                NativeGridField == null)
            {
                return;
            }

            uint currentColumns =
                (uint)VisibleColsField.GetValue(
                    catalogGrid
                );

            if (currentColumns == columns)
            {
                return;
            }

            Grid nativeGrid =
                NativeGridField.GetValue(
                    catalogGrid
                ) as Grid;

            if (nativeGrid == null)
            {
                return;
            }

            /*
             * Mirror the lightweight state that VisibleColumns ultimately
             * needs, but deliberately skip RepopulateGrid() and the
             * VisibleSizeChanged event.
             *
             * The grid is empty here, so there are no cells to rearrange.
             */
            VisibleColsField.SetValue(
                catalogGrid,
                columns
            );

            nativeGrid.ColumnCount =
                (int)columns;

            if (UpdateGridSizeMethod != null)
            {
                UpdateGridSizeMethod.Invoke(
                    catalogGrid,
                    null
                );
            }

            if (UpdateScrollbarMethod != null)
            {
                UpdateScrollbarMethod.Invoke(
                    catalogGrid,
                    null
                );
            }
        }

        public void ResizeCatalogGrid(
            ItemGrid catalogGrid,
            int numItems,
            Window currentCatalogWindow,
            Window middlePuckWindow)
        {
            BuildController self =
                (BuildController)(object)this;

            CatalogProductFilter filter = null;

            if (CatalogProductFilterField != null)
            {
                filter =
                    CatalogProductFilterField.GetValue(self)
                    as CatalogProductFilter;
            }

            /*
             * Preserve EA's field lifecycle.
             * Better Build handles the actual width before EA calculates
             * VisibleColumns, so we do not need to force a second resize.
             */
            if (ForceResizeField != null)
            {
                ForceResizeField.SetValue(
                    self,
                    false
                );
            }

            /*
             * =====================================================
             * BETTER BUILD PERFORMANCE FIX
             * =====================================================
             *
             * IMPORTANT:
             *
             * Expand BOTH the catalog window and ItemGrid BEFORE
             * EA's native ResizeCatalogGrid().
             *
             * EA will then calculate VisibleColumns ONCE using the
             * final Better Build width.
             *
             * We never assign VisibleColumns after EA has populated
             * the catalog, which avoids the huge Paints/Floors lag.
             */
            SceneMgrWindow sceneWindow =
                UIManager.GetSceneWindow();

            if (sceneWindow != null &&
                currentCatalogWindow != null &&
                catalogGrid != null)
            {
                /*
                 * 1) Extend the BLUE catalog window to the screen edge.
                 */
                Vector2 windowRightEdge =
                    currentCatalogWindow.WindowToScreen(
                        new Vector2(
                            currentCatalogWindow.Area.Width,
                            0f
                        )
                    );

                float windowRemainingWidth =
                    sceneWindow.Area.Width -
                    windowRightEdge.x;

                if (windowRemainingWidth > 0f)
                {
                    Rect windowArea =
                        currentCatalogWindow.Area;

                    windowArea.Width +=
                        windowRemainingWidth;

                    currentCatalogWindow.Area =
                        windowArea;
                }

                /*
                 * 2) Extend the ItemGrid independently.
                 *
                 * The grid starts shorter than its parent window, so adding
                 * the same delta to both preserves the original empty gap.
                 *
                 * Target the inside-right edge of the now-expanded catalog
                 * window instead. Leave a tiny 4px border for the frame.
                 */
                Vector2 desiredGridRightEdge =
                    currentCatalogWindow.WindowToScreen(
                        new Vector2(
                            currentCatalogWindow.Area.Width - 4f,
                            0f
                        )
                    );

                Vector2 gridRightEdge =
                    catalogGrid.WindowToScreen(
                        new Vector2(
                            catalogGrid.Area.Width,
                            0f
                        )
                    );

                float gridRemainingWidth =
                    desiredGridRightEdge.x -
                    gridRightEdge.x;

                if (gridRemainingWidth > 0f)
                {
                    Rect gridArea =
                        catalogGrid.Area;

                    gridArea.Width +=
                        gridRemainingWidth;

                    catalogGrid.Area =
                        gridArea;
                }
            }

            /*
             * Let EA finish the resize and calculate VisibleColumns
             * exactly once from the already-expanded Better Build grid.
             *
             * forceResize = false is intentional:
             * the grid is already at our desired maximum width, so EA
             * must not shrink it back down based on the number of items.
             */
            BuildController.ResizeCatalogGrid(
                catalogGrid,
                numItems,
                currentCatalogWindow,
                middlePuckWindow,
                false
            );

            /*
             * =====================================================
             * FULL-WIDTH COLUMN FIX WITHOUT REPOPULATE
             * =====================================================
             *
             * EA has now completed its own resize.
             *
             * Better Build wants the empty ItemGrid to use all available
             * horizontal space. Calculate the same full-width column count
             * we used in the old version, but DO NOT assign the public
             * VisibleColumns property.
             *
             * Instead, prepare the empty native grid directly so the
             * subsequent BeginPopulating() fills the correct number of
             * columns from the start.
             */
            if (catalogGrid != null &&
                catalogGrid.ItemRows == 0U)
            {
                float paddingWidth =
                    catalogGrid.CellPadding.Width +
                    catalogGrid.CellPadding.TopLeft.x * 2f;

                float cellPitch =
                    paddingWidth +
                    catalogGrid.CellArea.x;

                float usableWidth =
                    catalogGrid.Area.Width -
                    16f;

                usableWidth -=
                    catalogGrid.GridPadding.TopLeft.x +
                    catalogGrid.GridPadding.BottomRight.x;

                if (cellPitch > 0f &&
                    usableWidth > 0f)
                {
                    uint columns =
                        (uint)(
                            (usableWidth - catalogGrid.CellArea.x) /
                            cellPitch +
                            1f
                        );

                    if (columns < 2U)
                    {
                        columns = 2U;
                    }

                    SetVisibleColumnsWithoutRepopulate(
                        catalogGrid,
                        columns
                    );
                }
            }

            /*
             * Keep Better Build's Build-mode controls/search.
             */
            BuildCatalogUI.Attach(
                self,
                catalogGrid,
                currentCatalogWindow
            );
            BetterBuildInterfaceToggle.Attach(
                self,
                catalogGrid,
                currentCatalogWindow
            );

            /*
             * Optimized BUILD bottom spacer.
             *
             * Attach() is cheap before population (ItemRows == 0).
             * EA's own GridCleanupTick calls ResizeCatalogGrid() again after
             * population, which gives the spacer its final real row count.
             *
             * The spacer itself no longer listens to ItemRowsChanged, so it
             * does not Refresh() the native Grid every population chunk.
             */
            BuildCatalogBottomSpacer.Attach(
                self,
                catalogGrid
            );
        }

    }
} 