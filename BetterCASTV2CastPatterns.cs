using Sims3.UI;
using Sims3.UI.CAS;

namespace BetterBuildByJojobou
{
    /*
     * Better Build V2 - CASt pattern layout (3 x 5) and first/last navigation.
     *
     * Pattern cells and grid geometry are defined in the V2 LAYO resources.
     * Pattern indexing, lazy thumbnails, CC filters and page loading remain
     * in BetterCASTHelpers from the official V1 source.
     *
     * NO Task, timer, or continuous UI polling.
     */
    public static class BetterCASTV2CastPatterns
    {
        public const int PatternsPerPage = 15;
        public const int PatternCellSize = 70;
        public const int NavigationButtonSize = 32;

        private const uint FirstPageID = 0x0012F006U;
        private const uint LastPageID = 0x0012F007U;

        private static CASCompositorController sController;
        private static Button sFirstPageButton;
        private static Button sLastPageButton;

        public static void Attach(
            CASCompositorController controller,
            WindowBase materialsBin)
        {
            if (controller == null || materialsBin == null)
            {
                return;
            }

            if (!object.ReferenceEquals(sController, controller))
            {
                Unbind();
                sController = controller;
            }

            Button first = materialsBin.GetChildByID(FirstPageID, true) as Button;
            Button last = materialsBin.GetChildByID(LastPageID, true) as Button;

            if (!object.ReferenceEquals(sFirstPageButton, first))
            {
                if (sFirstPageButton != null)
                {
                    sFirstPageButton.Click -= OnFirstPageClicked;
                }
                sFirstPageButton = first;
            }
            if (!object.ReferenceEquals(sLastPageButton, last))
            {
                if (sLastPageButton != null)
                {
                    sLastPageButton.Click -= OnLastPageClicked;
                }
                sLastPageButton = last;
            }

            if (sFirstPageButton != null)
            {
                sFirstPageButton.TooltipText = "Première page";
                sFirstPageButton.Click -= OnFirstPageClicked;
                sFirstPageButton.Click += OnFirstPageClicked;
            }
            if (sLastPageButton != null)
            {
                sLastPageButton.TooltipText = "Dernière page";
                sLastPageButton.Click -= OnLastPageClicked;
                sLastPageButton.Click += OnLastPageClicked;
            }
        }

        public static void SetVisible(bool visible)
        {
            if (sFirstPageButton != null)
            {
                sFirstPageButton.Visible = visible;
            }
            if (sLastPageButton != null)
            {
                sLastPageButton.Visible = visible;
            }
        }

        public static void UpdatePageState(bool visible, int page, int total)
        {
            // A category switch can rebuild the native UI while the
            // CASCompositorController instance stays alive. Reacquire the
            // current first/last controls before updating their state.
            CASCompositorController current = CASCompositorController.Instance;
            if (current != null)
            {
                WindowBase materialsBin = current.GetChildByID(0x00012000U, false);
                if (materialsBin != null)
                {
                    Attach(current, materialsBin);
                }
            }
            SetVisible(visible);
            if (sFirstPageButton != null)
            {
                sFirstPageButton.Enabled = visible && total > 0 && page > 0;
            }
            if (sLastPageButton != null)
            {
                sLastPageButton.Enabled = visible && total > 0 && page < total - 1;
            }
        }

        private static void OnFirstPageClicked(
            WindowBase sender, UIButtonClickEventArgs eventArgs)
        {
            BetterCASTHelpers.GoToPage(0);
            eventArgs.Handled = true;
        }

        private static void OnLastPageClicked(
            WindowBase sender, UIButtonClickEventArgs eventArgs)
        {
            BetterCASTHelpers.GoToPage(int.MaxValue);
            eventArgs.Handled = true;
        }

        public static void Unbind()
        {
            if (sFirstPageButton != null)
            {
                sFirstPageButton.Click -= OnFirstPageClicked;
                sFirstPageButton = null;
            }
            if (sLastPageButton != null)
            {
                sLastPageButton.Click -= OnLastPageClicked;
                sLastPageButton = null;
            }
            sController = null;
        }
    }
}
