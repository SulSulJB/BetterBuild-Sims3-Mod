using System;
using System.Reflection;
using Sims3.UI;
using Sims3.UI.CAS;

namespace BetterBuildByJojobou
{
    /*
     * Better Build V2 - CASt-only wheel pagination.
     *
     * Does NOT patch ItemGrid methods with MonoPatcher.
     * The EA mouse wheel handler is replaced on ONE specific Grid only:
     * CASCompositorController.mMaterialsWindowGrid.mGrid.
     *
     * Other ItemGrids (Buy/Build/CAS clothing/other UI) stay native.
     *
     * REQUIREMENT:
     * Call BetterCASTWheelCAStOnly.Attach(controller) from
     * BetterCASTHelpers.BindBetterCASTControls(), after the controller
     * has been assigned to sBoundController.
     *
     * Remove BetterCASTWheelPagination_TEST04.cs from your project.
     */
    public static class BetterCASTWheelCAStOnly
    {
        private static readonly BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly FieldInfo NativeGridField =
            typeof(ItemGrid).GetField("mGrid", Flags);

        private static readonly FieldInfo MaterialsGridField =
            typeof(CASCompositorController).GetField(
                "mMaterialsWindowGrid", Flags);

        private static readonly FieldInfo BoundControllerField =
            typeof(BetterCASTHelpers).GetField(
                "sBoundController", Flags);

        private static readonly FieldInfo BrowserOpenField =
            typeof(BetterCASTHelpers).GetField(
                "sBrowserOpen", Flags);

        private static readonly FieldInfo CurrentPageField =
            typeof(BetterCASTHelpers).GetField(
                "sCurrentPage", Flags);

        private static readonly FieldInfo TotalPagesField =
            typeof(BetterCASTHelpers).GetField(
                "sTotalPages", Flags);

        private static CASCompositorController sController;
        private static ItemGrid sItemGrid;
        private static Grid sNativeGrid;

        public static void Attach(CASCompositorController controller)
        {
            if (controller == null ||
                MaterialsGridField == null || NativeGridField == null)
            {
                return;
            }

            ItemGrid itemGrid =
                MaterialsGridField.GetValue(controller) as ItemGrid;
            Grid nativeGrid = itemGrid != null
                ? NativeGridField.GetValue(itemGrid) as Grid : null;

            if (itemGrid == null || nativeGrid == null)
            {
                return;
            }

            // Reattachment is idempotent. EA's handler is removed only
            // from the actual CASt materials Grid, never globally.
            if (object.ReferenceEquals(sController, controller) &&
                object.ReferenceEquals(sItemGrid, itemGrid) &&
                object.ReferenceEquals(sNativeGrid, nativeGrid))
            {
                sNativeGrid.MouseWheel -= OnMaterialsMouseWheel;
                sNativeGrid.MouseWheel -= sItemGrid.OnGridMouseWheel;
                sNativeGrid.MouseWheel += OnMaterialsMouseWheel;
                return;
            }

            Detach();
            sController = controller;
            sItemGrid = itemGrid;
            sNativeGrid = nativeGrid;

            sNativeGrid.MouseWheel -= sItemGrid.OnGridMouseWheel;
            sNativeGrid.MouseWheel += OnMaterialsMouseWheel;
        }

        public static void Detach()
        {
            if (sNativeGrid != null)
            {
                sNativeGrid.MouseWheel -= OnMaterialsMouseWheel;

                // Restore EA's exact original wheel handler if this
                // instance is still alive and in use.
                if (sItemGrid != null)
                {
                    sNativeGrid.MouseWheel -= sItemGrid.OnGridMouseWheel;
                    sNativeGrid.MouseWheel += sItemGrid.OnGridMouseWheel;
                }
            }

            sController = null;
            sItemGrid = null;
            sNativeGrid = null;
        }

        private static void OnMaterialsMouseWheel(
            WindowBase sender,
            UIMouseEventArgs eventArgs)
        {
            if (sNativeGrid == null || sItemGrid == null ||
                !object.ReferenceEquals(sender, sNativeGrid))
            {
                return;
            }

            if (!TryPage(eventArgs))
            {
                // CASt's native handler still works while the pattern
                // browser is closed. Nothing else in the game is touched.
                sItemGrid.OnGridMouseWheel(sender, eventArgs);
            }
        }

        private static bool TryPage(UIMouseEventArgs eventArgs)
        {
            if (eventArgs == null || sController == null ||
                BoundControllerField == null ||
                BrowserOpenField == null ||
                CurrentPageField == null ||
                TotalPagesField == null ||
                MaterialsGridField == null ||
                !object.ReferenceEquals(sController,
                    CASCompositorController.Instance) ||
                !object.ReferenceEquals(sController,
                    BoundControllerField.GetValue(null)) ||
                !object.ReferenceEquals(sItemGrid,
                    MaterialsGridField.GetValue(sController)) ||
                !(bool)BrowserOpenField.GetValue(null))
            {
                return false;
            }

            WindowBase materialsBin =
                sController.GetChildByID(73728U, false);

            if (materialsBin == null || !materialsBin.Visible ||
                !sItemGrid.Visible || sItemGrid.Parent == null ||
                !sItemGrid.Parent.Visible)
            {
                return false;
            }

            // While the browser is open, don't let EA scroll its
            // inner 3x5 grid by pixels. One wheel event = one page.
            int count = (int)TotalPagesField.GetValue(null);
            int current = (int)CurrentPageField.GetValue(null);
            if (count > 1 && eventArgs.MouseWheelDelta != 0)
            {
                int next = current +
                    (eventArgs.MouseWheelDelta < 0 ? 1 : -1);
                if (next >= 0 && next < count)
                {
                    BetterCASTHelpers.GoToPage(next);
                }
            }

            return true;
        }
    }
}
