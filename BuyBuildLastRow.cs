using System;
using System.Reflection;
using MonoPatcherLib;
using Sims3.UI;

namespace BetterBuildByJojobou
{
    /*
     * Better Build V1 - last-row selection only, TEST15.
     *
     * Base: exact V1 BuyCatalogBottomSpacer.cs and
     * BuildCatalogBottomSpacer.cs (both restored without changes).
     *
     * Only change: the native ItemGrid.OnGridSelectionChanged handler
     * must not quantize scrollbar.TargetValue upward when a player clicks
     * an object in the last visible catalog row while near the bottom.
     *
     * Does not change any scroll metrics, grid row heights, layouts,
     * scrollbar bounds, population, or Normal/Extended transitions.
     *
     * Buy: Room / Function / Collections
     * Build: current BuildCatalogUI.mCatalogGrid only
     * CAS and all other ItemGrids: native EA selection behavior.
     *
     * MonoPatcher: install ONE ItemGrid.OnGridSelectionChanged patch only.
     */
    [TypePatch(typeof(ItemGrid))]
    public class BuyBuildLastRowSelectionOnly_TEST15
    {
        private const uint RoomCatalogID = 0x00002101U;
        private const uint FunctionCatalogID = 0x00002102U;
        private const uint CollectionCatalogID = 0x00000423U;

        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static readonly BindingFlags StaticFlags =
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        private static readonly FieldInfo NativeGridField =
            typeof(ItemGrid).GetField("mGrid", InstanceFlags);

        private static readonly FieldInfo ScrollIncrementField =
            typeof(ItemGrid).GetField("mScrollIncrement", InstanceFlags);

        private static readonly FieldInfo SelectionChangedField =
            typeof(ItemGrid).GetField("SelectionChanged", InstanceFlags);

        // Existing BuildCatalogUI already tracks the active Build catalog.
        // Reading its field leaves the original V1 Build spacer untouched.
        private static readonly FieldInfo ActiveBuildGridField =
            typeof(BuildCatalogUI).GetField("mCatalogGrid", StaticFlags);

        private static bool IsTargetCatalog(ItemGrid itemGrid)
        {
            if (itemGrid == null || itemGrid.HorizontalScrolling)
            {
                return false;
            }

            WindowBase ancestor = itemGrid;
            while (ancestor != null)
            {
                if (ancestor.ID == RoomCatalogID ||
                    ancestor.ID == FunctionCatalogID ||
                    ancestor.ID == CollectionCatalogID)
                {
                    return true;
                }
                ancestor = ancestor.Parent;
            }

            return ActiveBuildGridField != null &&
                object.ReferenceEquals(
                    ActiveBuildGridField.GetValue(null), itemGrid);
        }

        // Signature of the EA UI.dll method replaced by MonoPatcher.
        private void OnGridSelectionChanged(
            WindowBase sender,
            UISelectionChangeEventArgs eventArgs)
        {
            ItemGrid itemGrid = (ItemGrid)(object)this;
            Grid nativeGrid = NativeGridField != null
                ? NativeGridField.GetValue(itemGrid) as Grid
                : null;

            Scrollbar scrollbar = itemGrid.HorizontalScrolling
                ? itemGrid.HScrollbar
                : itemGrid.VScrollbar;

            if (nativeGrid != null &&
                scrollbar != null &&
                ScrollIncrementField != null)
            {
                int firstColumn = 0;
                int firstRow = 0;
                nativeGrid.GetFirstVisibleCell(ref firstColumn, ref firstRow);

                int increment =
                    (int)(uint)ScrollIncrementField.GetValue(itemGrid);

                int firstVisibleIndex = itemGrid.HorizontalScrolling
                    ? firstColumn
                    : firstRow;

                int nativeTarget = firstVisibleIndex * increment;
                bool preventLastRowSnap = false;

                if (increment > 0 && IsTargetCatalog(itemGrid))
                {
                    int currentPosition = scrollbar.Value;
                    int lastPosition =
                        scrollbar.UpperBoundValue - scrollbar.VisibleRange;
                    const int SmallMargin = 4;

                    // Same near-bottom guard previously confirmed in Buy
                    // TEST06, now scoped to the active Build catalog too.
                    // Do NOT write Value OR TargetValue on this specific click.
                    preventLastRowSnap =
                        lastPosition > 0 &&
                        currentPosition > 0 &&
                        currentPosition >=
                            lastPosition - increment - SmallMargin &&
                        nativeTarget < currentPosition &&
                        currentPosition - nativeTarget <=
                            increment + SmallMargin;
                }

                if (!preventLastRowSnap)
                {
                    // Preserve EA logic for every unrelated grid and click.
                    scrollbar.TargetValue = nativeTarget;
                }
            }

            // Always forward the game event. Selection is never cancelled.
            if (SelectionChangedField != null)
            {
                UIEventHandler<UISelectionChangeEventArgs> callback =
                    SelectionChangedField.GetValue(itemGrid)
                    as UIEventHandler<UISelectionChangeEventArgs>;
                if (callback != null)
                {
                    callback(itemGrid, eventArgs);
                }
            }
        }
    }
}
