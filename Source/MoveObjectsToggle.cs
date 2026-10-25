using System.Reflection;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.UI;

namespace BetterBuildByJojobou
{
    public static class MoveObjectsToggle
    {
        private const uint RoomWindowID = 0x00002000U;
        private const uint FunctionWindowID = 0x00002001U;
        private const uint CollectionWindowID = 0x00000410U;

        private const uint NormalButtonID = 0x0BC50010U;
        private const uint ExpandedButtonID = 0x00000206U;

        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public;

        private static BuyController mController;

        private static WindowBase mRoomWindow;
        private static WindowBase mFunctionWindow;
        private static WindowBase mCollectionWindow;

        private static ExpandableCatalogGrid mRoomGrid;
        private static ExpandableCatalogGrid mFunctionGrid;
        private static ExpandableCatalogGrid mCollectionGrid;

        private static Button mRoomNormalButton;
        private static Button mFunctionNormalButton;
        private static Button mCollectionNormalButton;
        private static Button mRoomExpandedButton;
        private static Button mFunctionExpandedButton;

        private static bool sEnabled;

        private static string Localize(string key)
        {
            return Localization.LocalizeString(
                "Jojobou/BetterBuild/MoveObjectsToggle:" + key,
                new object[0]
            );
        }

        public static void Attach(BuyController controller)
        {
            if (controller == null)
            {
                return;
            }

            if (!object.ReferenceEquals(
                    mController,
                    controller))
            {
                Detach();

                mController = controller;

                mController.Detach -=
                    OnControllerDetach;

                mController.Detach +=
                    OnControllerDetach;
            }

            RefreshControllerBindings();
            RefreshButtons();
            UpdateVisibility();
            SyncButtons();
        }

        private static void RefreshControllerBindings()
        {
            if (mController == null)
            {
                return;
            }

            mRoomWindow =
                mController.GetChildByID(
                    RoomWindowID,
                    true
                );

            mFunctionWindow =
                mController.GetChildByID(
                    FunctionWindowID,
                    true
                );

            mCollectionWindow =
                mController.GetChildByID(
                    CollectionWindowID,
                    true
                );

            FieldInfo roomField =
                typeof(BuyController).GetField(
                    "mGridSortByRoom",
                    InstanceFlags
                );

            ExpandableCatalogGrid roomGrid =
                roomField != null
                    ? roomField.GetValue(mController)
                        as ExpandableCatalogGrid
                    : null;

            if (!object.ReferenceEquals(
                    mRoomGrid,
                    roomGrid))
            {
                if (mRoomGrid != null)
                {
                    mRoomGrid.ExpandChanged -=
                        OnExpandChanged;
                }

                mRoomGrid = roomGrid;
            }

            if (mRoomGrid != null)
            {
                mRoomGrid.ExpandChanged -=
                    OnExpandChanged;

                mRoomGrid.ExpandChanged +=
                    OnExpandChanged;
            }

            FieldInfo functionField =
                typeof(BuyController).GetField(
                    "mGridSortByFunction",
                    InstanceFlags
                );

            ExpandableCatalogGrid functionGrid =
                functionField != null
                    ? functionField.GetValue(mController)
                        as ExpandableCatalogGrid
                    : null;

            if (!object.ReferenceEquals(
                    mFunctionGrid,
                    functionGrid))
            {
                if (mFunctionGrid != null)
                {
                    mFunctionGrid.ExpandChanged -=
                        OnExpandChanged;
                }

                mFunctionGrid = functionGrid;
            }

            if (mFunctionGrid != null)
            {
                mFunctionGrid.ExpandChanged -=
                    OnExpandChanged;

                mFunctionGrid.ExpandChanged +=
                    OnExpandChanged;
            }

            FieldInfo collectionField =
                typeof(BuyController).GetField(
                    "mGridCollection",
                    InstanceFlags
                );

            ExpandableCatalogGrid collectionGrid =
                collectionField != null
                    ? collectionField.GetValue(mController)
                        as ExpandableCatalogGrid
                    : null;

            if (!object.ReferenceEquals(
                    mCollectionGrid,
                    collectionGrid))
            {
                if (mCollectionGrid != null)
                {
                    mCollectionGrid.ExpandChanged -=
                        OnExpandChanged;
                }

                mCollectionGrid = collectionGrid;
            }

            if (mCollectionGrid != null)
            {
                mCollectionGrid.ExpandChanged -=
                    OnExpandChanged;

                mCollectionGrid.ExpandChanged +=
                    OnExpandChanged;
            }
        }

        private static void RefreshButtons()
        {
            Button roomNormal = null;
            Button functionNormal = null;
            Button collectionNormal = null;
            Button roomExpanded = null;
            Button functionExpanded = null;

            if (mRoomWindow != null)
            {
                roomNormal =
                    mRoomWindow.GetChildByID(
                        NormalButtonID,
                        true
                    ) as Button;
            }

            if (mFunctionWindow != null)
            {
                functionNormal =
                    mFunctionWindow.GetChildByID(
                        NormalButtonID,
                        true
                    ) as Button;
            }

            if (mCollectionWindow != null)
            {
                collectionNormal =
                    mCollectionWindow.GetChildByID(
                        NormalButtonID,
                        true
                    ) as Button;
            }

            if (mRoomGrid != null)
            {
                roomExpanded =
                    mRoomGrid.GetChildByID(
                        ExpandedButtonID,
                        true
                    ) as Button;
            }

            if (mFunctionGrid != null)
            {
                functionExpanded =
                    mFunctionGrid.GetChildByID(
                        ExpandedButtonID,
                        true
                    ) as Button;
            }

            Rebind(
                ref mRoomNormalButton,
                roomNormal
            );

            Rebind(
                ref mFunctionNormalButton,
                functionNormal
            );

            Rebind(
                ref mCollectionNormalButton,
                collectionNormal
            );

            Rebind(
                ref mRoomExpandedButton,
                roomExpanded
            );

            Rebind(
                ref mFunctionExpandedButton,
                functionExpanded
            );
        }

        private static void Rebind(
            ref Button current,
            Button next)
        {
            if (object.ReferenceEquals(
                    current,
                    next))
            {
                return;
            }

            if (current != null)
            {
                current.Click -=
                    OnClick;
            }

            current = next;

            if (current != null)
            {
                current.Click -=
                    OnClick;

                current.Click +=
                    OnClick;
            }
        }

        private static void OnClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            sEnabled =
                eventArgs.ButtonSelected;

            CommandSystem.ExecuteCommandString(
                sEnabled
                    ? "moveObjects on"
                    : "moveObjects off"
            );

            SyncButtons();
        }

        private static void SyncButtons()
        {
            string tooltip =
                Localize("Tooltip");

            SyncButton(
                mRoomNormalButton,
                tooltip
            );

            SyncButton(
                mFunctionNormalButton,
                tooltip
            );

            SyncButton(
                mCollectionNormalButton,
                tooltip
            );

            SyncButton(
                mRoomExpandedButton,
                tooltip
            );

            SyncButton(
                mFunctionExpandedButton,
                tooltip
            );
        }

        private static void SyncButton(
            Button button,
            string tooltip)
        {
            if (button == null)
            {
                return;
            }

            button.Selected =
                sEnabled;

            button.TooltipText =
                tooltip;
        }

        private static void OnExpandChanged()
        {
            RefreshControllerBindings();
            RefreshButtons();
            UpdateVisibility();
            SyncButtons();
        }

        private static void UpdateVisibility()
        {
            if (mRoomNormalButton != null &&
                mRoomGrid != null)
            {
                mRoomNormalButton.Visible =
                    !mRoomGrid.Expanded;
            }

            if (mFunctionNormalButton != null &&
                mFunctionGrid != null)
            {
                mFunctionNormalButton.Visible =
                    !mFunctionGrid.Expanded;
            }

            if (mCollectionNormalButton != null &&
                mCollectionGrid != null)
            {
                mCollectionNormalButton.Visible =
                    !mCollectionGrid.Expanded;
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
            Unbind(
                ref mRoomNormalButton
            );

            Unbind(
                ref mFunctionNormalButton
            );

            Unbind(
                ref mCollectionNormalButton
            );

            Unbind(
                ref mRoomExpandedButton
            );

            Unbind(
                ref mFunctionExpandedButton
            );

            if (mRoomGrid != null)
            {
                mRoomGrid.ExpandChanged -=
                    OnExpandChanged;
            }

            if (mFunctionGrid != null)
            {
                mFunctionGrid.ExpandChanged -=
                    OnExpandChanged;
            }

            if (mCollectionGrid != null)
            {
                mCollectionGrid.ExpandChanged -=
                    OnExpandChanged;
            }

            if (mController != null)
            {
                mController.Detach -=
                    OnControllerDetach;
            }

            mRoomWindow = null;
            mFunctionWindow = null;
            mCollectionWindow = null;

            mRoomGrid = null;
            mFunctionGrid = null;
            mCollectionGrid = null;

            mController = null;
        }

        private static void Unbind(
            ref Button button)
        {
            if (button != null)
            {
                button.Click -=
                    OnClick;
            }

            button = null;
        }
    }
}
