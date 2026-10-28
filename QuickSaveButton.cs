using System;
using System.Reflection;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.UI;

namespace BetterBuildByJojobou
{
    /*
     * Better Build - Quick Save
     *
     * UI architecture:
     * - Room Normal       : 0x0BC50020
     * - Function Normal   : 0x0BC50020
     * - Collection Normal : 0x0BC50020
     * - Room Extended     : 0x00000207
     * - Function Extended : 0x00000207
     *
     * Save flow:
     * 1) Click the floppy button.
     * 2) Cleanly leave Buy mode through EA's own BuyModel.ExitBuyMode().
     * 3) Wait briefly for the native transition to finish.
     * 4) Ask EA's OptionsModel whether saving is available.
     * 5) Call EA's native OptionsModel.SaveGame().
     *
     * No custom save-file writing.
     * No bypass of EA save restrictions.
     * No continuous polling.
     */
    public static class QuickSaveButton
    {
        private const uint RoomWindowID = 0x00002000U;
        private const uint FunctionWindowID = 0x00002001U;
        private const uint CollectionWindowID = 0x00000410U;

        private const uint NormalSaveButtonID = 0x0bc50020U;
        private const uint ExpandedSaveButtonID = 0x00000207U;

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

        private static bool sSaveSequenceRunning;
        private static bool sPendingSaveAfterBuyExit;

        private static string Localize(string key)
        {
            return Localization.LocalizeString(
                "Jojobou/BetterBuild/QuickSaveButton:" + key,
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
                DetachUI();

                mController = controller;

                mController.Detach -=
                    OnControllerDetach;

                mController.Detach +=
                    OnControllerDetach;
            }

            RefreshControllerBindings();
            RefreshUIBindings();
            UpdateNormalControlsVisibility();
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

            FieldInfo roomGridField =
                typeof(BuyController).GetField(
                    "mGridSortByRoom",
                    InstanceFlags
                );

            ExpandableCatalogGrid roomGrid =
                roomGridField != null
                    ? roomGridField.GetValue(mController)
                        as ExpandableCatalogGrid
                    : null;

            if (!object.ReferenceEquals(
                    mRoomGrid,
                    roomGrid))
            {
                if (mRoomGrid != null)
                {
                    mRoomGrid.ExpandChanged -=
                        OnRoomExpandChanged;
                }

                mRoomGrid = roomGrid;
            }

            if (mRoomGrid != null)
            {
                mRoomGrid.ExpandChanged -=
                    OnRoomExpandChanged;

                mRoomGrid.ExpandChanged +=
                    OnRoomExpandChanged;
            }

            FieldInfo functionGridField =
                typeof(BuyController).GetField(
                    "mGridSortByFunction",
                    InstanceFlags
                );

            ExpandableCatalogGrid functionGrid =
                functionGridField != null
                    ? functionGridField.GetValue(mController)
                        as ExpandableCatalogGrid
                    : null;

            if (!object.ReferenceEquals(
                    mFunctionGrid,
                    functionGrid))
            {
                if (mFunctionGrid != null)
                {
                    mFunctionGrid.ExpandChanged -=
                        OnFunctionExpandChanged;
                }

                mFunctionGrid = functionGrid;
            }

            if (mFunctionGrid != null)
            {
                mFunctionGrid.ExpandChanged -=
                    OnFunctionExpandChanged;

                mFunctionGrid.ExpandChanged +=
                    OnFunctionExpandChanged;
            }

            FieldInfo collectionGridField =
                typeof(BuyController).GetField(
                    "mGridCollection",
                    InstanceFlags
                );

            ExpandableCatalogGrid collectionGrid =
                collectionGridField != null
                    ? collectionGridField.GetValue(mController)
                        as ExpandableCatalogGrid
                    : null;

            if (!object.ReferenceEquals(
                    mCollectionGrid,
                    collectionGrid))
            {
                if (mCollectionGrid != null)
                {
                    mCollectionGrid.ExpandChanged -=
                        OnCollectionExpandChanged;
                }

                mCollectionGrid = collectionGrid;
            }

            if (mCollectionGrid != null)
            {
                mCollectionGrid.ExpandChanged -=
                    OnCollectionExpandChanged;

                mCollectionGrid.ExpandChanged +=
                    OnCollectionExpandChanged;
            }
        }

        private static void RefreshUIBindings()
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
                        NormalSaveButtonID,
                        true
                    ) as Button;
            }

            if (mFunctionWindow != null)
            {
                functionNormal =
                    mFunctionWindow.GetChildByID(
                        NormalSaveButtonID,
                        true
                    ) as Button;
            }

            if (mCollectionWindow != null)
            {
                collectionNormal =
                    mCollectionWindow.GetChildByID(
                        NormalSaveButtonID,
                        true
                    ) as Button;
            }

            if (mRoomGrid != null)
            {
                roomExpanded =
                    mRoomGrid.GetChildByID(
                        ExpandedSaveButtonID,
                        true
                    ) as Button;
            }

            if (mFunctionGrid != null)
            {
                functionExpanded =
                    mFunctionGrid.GetChildByID(
                        ExpandedSaveButtonID,
                        true
                    ) as Button;
            }

            RebindButton(
                ref mRoomNormalButton,
                roomNormal
            );

            RebindButton(
                ref mFunctionNormalButton,
                functionNormal
            );

            RebindButton(
                ref mCollectionNormalButton,
                collectionNormal
            );

            RebindButton(
                ref mRoomExpandedButton,
                roomExpanded
            );

            RebindButton(
                ref mFunctionExpandedButton,
                functionExpanded
            );
        }

        private static void RebindButton(
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
                    OnSaveButtonClick;
            }

            current = next;

            if (current != null)
            {
                current.TooltipText =
                    Localize("Tooltip");

                current.Click -=
                    OnSaveButtonClick;

                current.Click +=
                    OnSaveButtonClick;
            }
        }

        private static void SyncButtons()
        {
            SyncButton(mRoomNormalButton);
            SyncButton(mFunctionNormalButton);
            SyncButton(mCollectionNormalButton);
            SyncButton(mRoomExpandedButton);
            SyncButton(mFunctionExpandedButton);
        }

        private static void SyncButton(
            Button button)
        {
            if (button == null)
            {
                return;
            }

            // Prevent a second click while Buy -> Live -> Save is running.
            button.Enabled =
                !sSaveSequenceRunning;
        }

        private static void OnSaveButtonClick(
            WindowBase sender,
            UIButtonClickEventArgs eventArgs)
        {
            if (sSaveSequenceRunning)
            {
                return;
            }

            // A save is already running (EA's save can be slow in Edit Town).
            if (IsNativeSaveInProgress())
            {
                return;
            }

            sSaveSequenceRunning = true;
            sPendingSaveAfterBuyExit = ExitBuyModeBeforeSave;

            SyncButtons();

            Audio.StartSound(
                "ui_primary_button"
            );

            /*
             * Clean up any active UI drag before saving.
             */
            UIManager.CancelAnyDragDropNow();

            if (!ExitBuyModeBeforeSave)
            {
                /*
                 * Default: stay in Buy/Build (or Edit Town lot) and let EA's
                 * own SaveGame() run from where the player is. The mode is
                 * not changed, so the player is not sent back to Live mode
                 * or to the map view.
                 */
                DebugNotify("save requested (staying in Buy/Build)");
                StartNativeSave();
                ScheduleHandToolRestore(mController, 1000f);
                return;
            }

            if (!ExitBuyModeNative())
            {
                sPendingSaveAfterBuyExit = false;
                FinishSaveSequence();

                ShowSystemNotification(
                    Localize("ExitBuyModeFailed")
                );

                return;
            }

            /*
             * IMPORTANT:
             * We do NOT start a custom timer here.
             *
             * BuyController.Detach is raised when the Buy UI is actually
             * being destroyed as the game leaves Buy mode. On that native
             * lifecycle event we launch EA's own asynchronous SaveGame().
             */
        }

        private static bool ExitBuyModeNative()
        {
            if (mController == null)
            {
                return false;
            }

            try
            {
                /*
                 * BuyController's own private ExitBuyMode() simply calls
                 * mBuyModel.ExitBuyMode(). Invoking that exact controller
                 * method keeps the transition on EA's native path.
                 */
                MethodInfo exitBuyModeMethod =
                    typeof(BuyController).GetMethod(
                        "ExitBuyMode",
                        InstanceFlags,
                        null,
                        Type.EmptyTypes,
                        null
                    );

                if (exitBuyModeMethod == null)
                {
                    return false;
                }

                exitBuyModeMethod.Invoke(
                    mController,
                    null
                );

                return true;
            }
            catch
            {
                return false;
            }
        }

        /*
         * Used by the Build mode catalog: save in place, stay in Build.
         */
        /*
         * EA's SaveGameSynchronous calls UserToolUtils.OnClose(), which closes
         * the active user tool (the hand tool). Saving from the menu works
         * because leaving the menu re-selects it; saving in place does not.
         *
         * Once the save is finished we call the SAME controller method the H
         * key / the hand button runs:
         *   Buy   : BuyController.OnHandTool(sender, UIButtonClickEventArgs)
         *   Build : BuildController.OnHandClick(sender, UIButtonClickEventArgs)
         */
        private static int sRestoreChecks;

        private static object sToolController;

        private static void ScheduleHandToolRestore(
            object controller,
            float delayMilliseconds)
        {
            sToolController = controller;

            try
            {
                Simulator.AddObject(
                    new Sims3.Gameplay.OneShotFunctionTask(
                        new Sims3.Gameplay.Function(RestoreHandToolWhenSaved),
                        StopWatch.TickStyles.Milliseconds,
                        delayMilliseconds
                    )
                );
            }
            catch
            {
            }
        }

        private static void RestoreHandToolWhenSaved()
        {
            // Wait (up to ~30 s) until EA's save has really finished.
            if (IsNativeSaveInProgress() && sRestoreChecks < 60)
            {
                sRestoreChecks++;
                ScheduleHandToolRestore(sToolController, 500f);
                return;
            }

            sRestoreChecks = 0;

            object controller = sToolController;
            sToolController = null;

            if (controller == null)
            {
                return;
            }

            try
            {
                string methodName =
                    controller is BuyController
                        ? "OnHandTool"
                        : "OnHandClick";

                MethodInfo handMethod =
                    controller.GetType().GetMethod(
                        methodName,
                        InstanceFlags,
                        null,
                        new Type[]
                        {
                            typeof(WindowBase),
                            typeof(UIButtonClickEventArgs)
                        },
                        null
                    );

                if (handMethod == null)
                {
                    DebugNotify("hand tool method not found");
                    return;
                }

                handMethod.Invoke(
                    controller,
                    new object[]
                    {
                        null,
                        new UIButtonClickEventArgs()
                    }
                );

                DebugNotify("hand tool restored");
            }
            catch
            {
                DebugNotify("hand tool restore failed");
            }
        }

        public static void SaveInPlace(BuildController controller)
        {
            if (IsNativeSaveInProgress())
            {
                return;
            }

            Audio.StartSound(
                "ui_primary_button"
            );

            UIManager.CancelAnyDragDropNow();

            DebugNotify("save requested (staying in Build)");

            StartNativeSave();
            ScheduleHandToolRestore(controller, 1000f);
        }

        private static void StartNativeSave()
        {
            /*
             * Do not pre-empt EA's own save restrictions.
             *
             * OptionsModel.SaveGame() is asynchronous: EA schedules its own
             * SaveGameTask, which performs the normal IsSaveDisabled/modal/
             * screen-grab/etc. checks and displays EA's native messages.
             */
            object optionsModel =
                Responder.Instance != null
                    ? (object)Responder.Instance.OptionsModel
                    : null;

            if (optionsModel == null)
            {
                FinishSaveSequence();

                ShowSystemNotification(
                    Localize("SaveSystemNotFound")
                );

                return;
            }

            try
            {
                MethodInfo saveMethod =
                    optionsModel.GetType().GetMethod(
                        "SaveGame",
                        InstanceFlags,
                        null,
                        Type.EmptyTypes,
                        null
                    );

                if (saveMethod == null)
                {
                    FinishSaveSequence();

                    ShowSystemNotification(
                        Localize("SaveCommandNotFound")
                    );

                    return;
                }

                saveMethod.Invoke(
                    optionsModel,
                    null
                );

                FinishSaveSequence();
            }
            catch
            {
                FinishSaveSequence();

                ShowSystemNotification(
                    Localize("SaveStartFailed")
                );
            }
        }

        private static void FinishSaveSequence()
        {
            sSaveSequenceRunning = false;
            sPendingSaveAfterBuyExit = false;

            // If Buy UI still exists for any reason, re-enable its buttons.
            SyncButtons();
        }

        private static void ShowSystemNotification(
            string text)
        {
            StyledNotification.Show(
                new StyledNotification.Format(
                    text,
                    StyledNotification.NotificationStyle.kSystemMessage
                )
            );
        }

        private static void OnRoomExpandChanged()
        {
            RefreshControllerBindings();
            RefreshUIBindings();
            UpdateNormalControlsVisibility();
            SyncButtons();
        }

        private static void OnFunctionExpandChanged()
        {
            RefreshControllerBindings();
            RefreshUIBindings();
            UpdateNormalControlsVisibility();
            SyncButtons();
        }

        private static void OnCollectionExpandChanged()
        {
            RefreshControllerBindings();
            RefreshUIBindings();
            UpdateNormalControlsVisibility();
            SyncButtons();
        }

        private static void UpdateNormalControlsVisibility()
        {
            if (mRoomGrid != null &&
                mRoomNormalButton != null)
            {
                mRoomNormalButton.Visible =
                    !mRoomGrid.Expanded;
            }

            if (mFunctionGrid != null &&
                mFunctionNormalButton != null)
            {
                mFunctionNormalButton.Visible =
                    !mFunctionGrid.Expanded;
            }

            if (mCollectionGrid != null &&
                mCollectionNormalButton != null)
            {
                mCollectionNormalButton.Visible =
                    !mCollectionGrid.Expanded;
            }
        }

        private static void OnControllerDetach(
            WindowBase sender,
            UIEventArgs eventArgs)
        {
            /*
             * Native lifecycle hinge:
             * only a click on our floppy sets this flag.
             * A normal BuyController detach must never cause a save.
             */
            bool startSave =
                sPendingSaveAfterBuyExit;

            DetachUI();

            if (startSave)
            {
                sPendingSaveAfterBuyExit = false;
                DebugNotify("1/3 Buy UI detached");
                ScheduleNativeSave();
            }
        }

        /*
         * Set to false once the quick save is confirmed working in game.
         * While true, short on-screen messages show how far the sequence got.
         */
        private static readonly bool DebugQuickSave = false;

        /*
         * false = save in place, stay in Buy/Build (recommended).
         * true  = old behaviour: leave Buy mode first, then save.
         */
        private static readonly bool ExitBuyModeBeforeSave = false;

        private static bool IsNativeSaveInProgress()
        {
            try
            {
                object optionsModel =
                    Responder.Instance != null
                        ? (object)Responder.Instance.OptionsModel
                        : null;

                if (optionsModel == null)
                {
                    return false;
                }

                PropertyInfo property =
                    optionsModel.GetType().GetProperty(
                        "SaveGameInProgress",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic
                    );

                if (property == null)
                {
                    return false;
                }

                return (bool)property.GetValue(optionsModel, null);
            }
            catch
            {
                return false;
            }
        }

        // Delay between leaving Buy mode and asking EA to save.
        private const float SaveDelayMilliseconds = 750f;

        private static void DebugNotify(string text)
        {
            if (DebugQuickSave)
            {
                ShowSystemNotification("[QuickSave] " + text);
            }
        }

        /*
         * BuyController.Detach fires while the game is still leaving Buy
         * mode. Calling SaveGame() right there can run before the world
         * state is back to Live. Run it from a simulator task a moment later.
         */
        private static void ScheduleNativeSave()
        {
            try
            {
                Simulator.AddObject(
                    new Sims3.Gameplay.OneShotFunctionTask(
                        new Sims3.Gameplay.Function(StartNativeSaveDelayed),
                        StopWatch.TickStyles.Milliseconds,
                        SaveDelayMilliseconds
                    )
                );
                DebugNotify("2/3 save scheduled");
            }
            catch
            {
                // Fallback: previous behaviour.
                StartNativeSave();
            }
        }

        private static void StartNativeSaveDelayed()
        {
            DebugNotify("3/3 calling SaveGame()");
            StartNativeSave();
        }

        private static void DetachUI()
        {
            UnbindButton(
                ref mRoomNormalButton
            );

            UnbindButton(
                ref mFunctionNormalButton
            );

            UnbindButton(
                ref mCollectionNormalButton
            );

            UnbindButton(
                ref mRoomExpandedButton
            );

            UnbindButton(
                ref mFunctionExpandedButton
            );

            if (mRoomGrid != null)
            {
                mRoomGrid.ExpandChanged -=
                    OnRoomExpandChanged;
            }

            if (mFunctionGrid != null)
            {
                mFunctionGrid.ExpandChanged -=
                    OnFunctionExpandChanged;
            }

            if (mCollectionGrid != null)
            {
                mCollectionGrid.ExpandChanged -=
                    OnCollectionExpandChanged;
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

        private static void UnbindButton(
            ref Button button)
        {
            if (button != null)
            {
                button.Click -=
                    OnSaveButtonClick;
            }

            button = null;
        }
    }
}