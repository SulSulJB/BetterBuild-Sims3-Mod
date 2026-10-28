using System.Collections.Generic;
using Sims3.SimIFace;
using Sims3.UI;

namespace BetterBuildByJojobou
{
    public static class BetterBuildPlacementControls
    {
        /*
         * Better Build placement controls
         *
         * 8             = move down 0.10
         * 9             = move up 0.10
         * Shift+8       = move down 0.01
         * Shift+9       = move up 0.01
         * Alt+8/9       = same vertical movement while Alt is held
         * Alt+Shift+8/9 = same fine vertical movement while Alt is held
         * Alt           = EA-native behavior through disableSnappingToSlotsOnAlt
         *
         * Better Build does not replace EA's native Alt placement behavior.
         */

        private const uint BetterBuildMoveUp =
            0x0BBD1001U;

        private const uint BetterBuildMoveDown =
            0x0BBD1002U;

        private const uint BetterBuildMoveUpFine =
            0x0BBD1003U;

        private const uint BetterBuildMoveDownFine =
            0x0BBD1004U;

        /*
         * userTools.triggers sends different action IDs while Alt is held.
         * These MUST still be accepted so Alt+8 / Alt+9 keep working.
         * They use the exact same vertical movement code below; Alt itself
         * remains entirely native through disableSnappingToSlotsOnAlt.
         */
        private const uint BetterBuildMoveUpFree =
            0x0BBD1005U;

        private const uint BetterBuildMoveDownFree =
            0x0BBD1006U;

        private const uint BetterBuildMoveUpFreeFine =
            0x0BBD1007U;

        private const uint BetterBuildMoveDownFreeFine =
            0x0BBD1008U;

        // Native EA actions used for wall-mounted objects.
        private const uint NativeMoveUp =
            0x02552006U;

        private const uint NativeMoveDown =
            0x02552007U;

        // Shift state fallback.
        private const uint StateShift =
            0x02551011U;

        private const float NormalStep =
            0.10f;

        private const float FineStep =
            0.01f;

        private const int RepeatInitialDelayMs =
            300;

        private const int RepeatIntervalMs =
            90;

        private static BuyController sController;
        private static SceneMgrWindow sSceneWindow;

        private static bool sShiftHeld;
        private static bool sApplyingVerticalOffset;

        private static ObjectGuid sTrackedObject =
            ObjectGuid.InvalidObjectGuid;

        private static float sTargetHeightAboveFloor;
        private static bool sHasTargetHeight;

        /*
         * Heights changed by Better Build are remembered for this game session.
         * This is the working 8/9 persistence logic, independent from Alt.
         */
        private static readonly Dictionary<ObjectGuid, float> sStoredHeights =
            new Dictionary<ObjectGuid, float>();

        private static bool sMoveKeyHeld;
        private static uint sHeldMoveTrigger;
        private static int sNextRepeatTick;

        private static bool sNativeWallActionHeld;
        private static uint sNativeWallAction;

        public static void Attach(
            BuyController controller)
        {
            if (controller == null)
            {
                return;
            }

            if (!object.ReferenceEquals(
                    sController,
                    controller))
            {
                Detach();

                sController =
                    controller;

                sController.Detach -=
                    OnControllerDetach;

                sController.Detach +=
                    OnControllerDetach;
            }

            SceneMgrWindow scene =
                UIManager.GetSceneWindow();

            if (!object.ReferenceEquals(
                    sSceneWindow,
                    scene))
            {
                if (sSceneWindow != null)
                {
                    sSceneWindow.TriggerDown -=
                        OnTriggerDown;

                    sSceneWindow.TriggerUp -=
                        OnTriggerUp;

                    sSceneWindow.Tick -=
                        OnSceneTick;
                }

                sSceneWindow =
                    scene;
            }

            if (sSceneWindow != null)
            {
                sSceneWindow.TriggerDown -=
                    OnTriggerDown;

                sSceneWindow.TriggerDown +=
                    OnTriggerDown;

                sSceneWindow.TriggerUp -=
                    OnTriggerUp;

                sSceneWindow.TriggerUp +=
                    OnTriggerUp;

                sSceneWindow.Tick -=
                    OnSceneTick;

                sSceneWindow.Tick +=
                    OnSceneTick;
            }

            World.OnHandToolMovementCallback -=
                OnHandToolMovement;

            World.OnHandToolMovementCallback +=
                OnHandToolMovement;

            World.OnHandToolPickupCallback -=
                OnHandToolPickup;

            World.OnHandToolPickupCallback +=
                OnHandToolPickup;

            /*
             * ALT is entirely EA-native.
             * Better Build does not modify the held object when Alt is used.
             */
            CommandSystem.ExecuteCommandString(
                "disableSnappingToSlotsOnAlt on"
            );
        }

        private static void OnTriggerDown(
            WindowBase sender,
            UITriggerEventArgs eventArgs)
        {
            uint trigger =
                eventArgs.TriggerCode;

            if (trigger == StateShift)
            {
                sShiftHeld =
                    true;

                return;
            }

            bool moveUp;
            bool moveDown;
            bool fine;

            if (!DecodeMoveTrigger(
                    trigger,
                    out moveUp,
                    out moveDown,
                    out fine))
            {
                return;
            }

            eventArgs.Handled =
                true;

            bool alreadyHeld =
                sMoveKeyHeld &&
                sHeldMoveTrigger == trigger;

            sMoveKeyHeld =
                true;

            sHeldMoveTrigger =
                trigger;

            if (!alreadyHeld)
            {
                sNextRepeatTick =
                    System.Environment.TickCount +
                    RepeatInitialDelayMs;

                PerformMove(
                    trigger,
                    false
                );
            }
        }

        private static void OnTriggerUp(
            WindowBase sender,
            UITriggerEventArgs eventArgs)
        {
            uint trigger =
                eventArgs.TriggerCode;

            if (trigger == StateShift)
            {
                sShiftHeld =
                    false;

                return;
            }

            bool moveUp;
            bool moveDown;
            bool fine;

            if (!DecodeMoveTrigger(
                    trigger,
                    out moveUp,
                    out moveDown,
                    out fine))
            {
                return;
            }

            eventArgs.Handled =
                true;

            if (sNativeWallActionHeld)
            {
                UserToolUtils.ForwardUserToolTriggerEvent(
                    UserToolUtils.TriggerAction.kTypeTriggerUp,
                    sNativeWallAction
                );

                sNativeWallActionHeld =
                    false;

                sNativeWallAction =
                    0U;
            }

            if (sMoveKeyHeld &&
                sHeldMoveTrigger == trigger)
            {
                sMoveKeyHeld =
                    false;

                sHeldMoveTrigger =
                    0U;

                sNextRepeatTick =
                    0;
            }
        }

        private static bool DecodeMoveTrigger(
            uint trigger,
            out bool moveUp,
            out bool moveDown,
            out bool fine)
        {
            moveUp =
                trigger == BetterBuildMoveUp ||
                trigger == BetterBuildMoveUpFine ||
                trigger == BetterBuildMoveUpFree ||
                trigger == BetterBuildMoveUpFreeFine;

            moveDown =
                trigger == BetterBuildMoveDown ||
                trigger == BetterBuildMoveDownFine ||
                trigger == BetterBuildMoveDownFree ||
                trigger == BetterBuildMoveDownFreeFine;

            fine =
                trigger == BetterBuildMoveUpFine ||
                trigger == BetterBuildMoveDownFine ||
                trigger == BetterBuildMoveUpFreeFine ||
                trigger == BetterBuildMoveDownFreeFine;

            return
                moveUp ||
                moveDown;
        }

        private static void PerformMove(
            uint trigger,
            bool isRepeat)
        {
            bool moveUp;
            bool moveDown;
            bool fine;

            if (!DecodeMoveTrigger(
                    trigger,
                    out moveUp,
                    out moveDown,
                    out fine))
            {
                return;
            }

            /*
             * Fallback: if the Hand Tool sends normal 8/9 while Shift is held,
             * still use the fine 0.01 step.
             */
            if (sShiftHeld)
            {
                fine =
                    true;
            }

            ObjectGuid objectId =
                GetCurrentObjectId();

            if (objectId ==
                ObjectGuid.InvalidObjectGuid)
            {
                return;
            }

            /*
             * Wall-mounted objects keep EA's own wall elevation action.
             */
            if (World.ObjectCanBeMovedUpAndDownOnWall(
                    objectId))
            {
                if (!isRepeat &&
                    !sNativeWallActionHeld)
                {
                    sNativeWallAction =
                        moveUp
                            ? NativeMoveUp
                            : NativeMoveDown;

                    UserToolUtils.ForwardUserToolTriggerEvent(
                        UserToolUtils.TriggerAction.kTypeTriggerDown,
                        sNativeWallAction
                    );

                    sNativeWallActionHeld =
                        true;
                }

                return;
            }

            EnsureTrackedObject(
                objectId
            );

            if (!sHasTargetHeight)
            {
                CaptureCurrentHeightAsTarget(
                    objectId
                );
            }
            else
            {
                ApplyTargetHeight(
                    objectId
                );
            }

            float delta =
                moveUp
                    ? (fine ? FineStep : NormalStep)
                    : -(fine ? FineStep : NormalStep);

            Vector3 position =
                Sims3.SimIFace.Objects.GetPosition(
                    objectId
                );

            if (position == Vector3.OutOfWorld ||
                position == Vector3.Empty ||
                position == Vector3.Invalid)
            {
                return;
            }

            position.y +=
                delta;

            sApplyingVerticalOffset =
                true;

            try
            {
                Sims3.SimIFace.Objects.SetPosition(
                    objectId,
                    position
                );
            }
            finally
            {
                sApplyingVerticalOffset =
                    false;
            }

            sTargetHeightAboveFloor =
                sTargetHeightAboveFloor +
                delta;

            sStoredHeights[objectId] =
                sTargetHeightAboveFloor;
        }

        private static void OnSceneTick(
            WindowBase sender,
            UIEventArgs eventArgs)
        {
            ObjectGuid objectId =
                GetCurrentObjectId();

            if (objectId !=
                    ObjectGuid.InvalidObjectGuid &&
                objectId !=
                    sTrackedObject)
            {
                EnsureTrackedObject(
                    objectId
                );
            }

            /*
             * Keep only heights changed by 8/9 sticky while the object is held.
             */
            if (objectId !=
                    ObjectGuid.InvalidObjectGuid &&
                objectId ==
                    sTrackedObject &&
                sHasTargetHeight &&
                !World.ObjectCanBeMovedUpAndDownOnWall(
                    objectId))
            {
                ApplyTargetHeight(
                    objectId
                );
            }

            if (!sMoveKeyHeld)
            {
                return;
            }

            int now =
                System.Environment.TickCount;

            if (unchecked(
                    now -
                    sNextRepeatTick) < 0)
            {
                return;
            }

            sNextRepeatTick =
                now +
                RepeatIntervalMs;

            PerformMove(
                sHeldMoveTrigger,
                true
            );
        }

        private static ObjectGuid GetCurrentObjectId()
        {
            ObjectGuid objectId =
                World.HandToolGetObjectHeld();

            if (objectId !=
                ObjectGuid.InvalidObjectGuid)
            {
                return
                    objectId;
            }

            if (sSceneWindow != null)
            {
                objectId =
                    sSceneWindow.TopDraggedObject;
            }

            return
                objectId;
        }

        private static void EnsureTrackedObject(
            ObjectGuid objectId)
        {
            if (sTrackedObject ==
                objectId)
            {
                return;
            }

            sTrackedObject =
                objectId;

            RestoreStoredHeightForObject(
                objectId
            );
        }

        private static void RestoreStoredHeightForObject(
            ObjectGuid objectId)
        {
            float storedHeight;

            if (sStoredHeights.TryGetValue(
                    objectId,
                    out storedHeight))
            {
                sTargetHeightAboveFloor =
                    storedHeight;

                sHasTargetHeight =
                    true;
            }
            else
            {
                sTargetHeightAboveFloor =
                    0f;

                sHasTargetHeight =
                    false;
            }
        }

        private static void CaptureCurrentHeightAsTarget(
            ObjectGuid objectId)
        {
            sTargetHeightAboveFloor =
                Sims3.SimIFace.Objects.GetHeightAboveFloor(
                    objectId
                );

            sHasTargetHeight =
                true;
        }

        private static void ApplyTargetHeight(
            ObjectGuid objectId)
        {
            if (!sHasTargetHeight ||
                sApplyingVerticalOffset)
            {
                return;
            }

            float currentHeightAboveFloor =
                Sims3.SimIFace.Objects.GetHeightAboveFloor(
                    objectId
                );

            float correction =
                sTargetHeightAboveFloor -
                currentHeightAboveFloor;

            if (correction > -0.0001f &&
                correction < 0.0001f)
            {
                return;
            }

            Vector3 position =
                Sims3.SimIFace.Objects.GetPosition(
                    objectId
                );

            if (position == Vector3.OutOfWorld ||
                position == Vector3.Empty ||
                position == Vector3.Invalid)
            {
                return;
            }

            position.y +=
                correction;

            sApplyingVerticalOffset =
                true;

            try
            {
                Sims3.SimIFace.Objects.SetPosition(
                    objectId,
                    position
                );
            }
            finally
            {
                sApplyingVerticalOffset =
                    false;
            }
        }

        private static void OnHandToolMovement(
            object sender,
            World.HandToolEventArgs eventArgs)
        {
            if (sApplyingVerticalOffset ||
                eventArgs == null ||
                !sHasTargetHeight ||
                eventArgs.ObjectId !=
                    sTrackedObject)
            {
                return;
            }

            if (World.ObjectCanBeMovedUpAndDownOnWall(
                    eventArgs.ObjectId))
            {
                return;
            }

            ApplyTargetHeight(
                eventArgs.ObjectId
            );
        }

        private static void OnHandToolPickup(
            object sender,
            World.HandToolEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                return;
            }

            sTrackedObject =
                eventArgs.ObjectId;

            RestoreStoredHeightForObject(
                eventArgs.ObjectId
            );

            if (sHasTargetHeight &&
                !World.ObjectCanBeMovedUpAndDownOnWall(
                    eventArgs.ObjectId))
            {
                ApplyTargetHeight(
                    eventArgs.ObjectId
                );
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
            /*
             * Never leave EA's native Alt cheat enabled outside Buy mode.
             */
            CommandSystem.ExecuteCommandString(
                "disableSnappingToSlotsOnAlt off"
            );

            if (sSceneWindow != null)
            {
                sSceneWindow.TriggerDown -=
                    OnTriggerDown;

                sSceneWindow.TriggerUp -=
                    OnTriggerUp;

                sSceneWindow.Tick -=
                    OnSceneTick;
            }

            World.OnHandToolMovementCallback -=
                OnHandToolMovement;

            World.OnHandToolPickupCallback -=
                OnHandToolPickup;

            if (sController != null)
            {
                sController.Detach -=
                    OnControllerDetach;
            }

            if (sNativeWallActionHeld)
            {
                UserToolUtils.ForwardUserToolTriggerEvent(
                    UserToolUtils.TriggerAction.kTypeTriggerUp,
                    sNativeWallAction
                );
            }

            sSceneWindow =
                null;

            sController =
                null;

            sShiftHeld =
                false;

            sApplyingVerticalOffset =
                false;

            sMoveKeyHeld =
                false;

            sHeldMoveTrigger =
                0U;

            sNextRepeatTick =
                0;

            sNativeWallActionHeld =
                false;

            sNativeWallAction =
                0U;

            sTrackedObject =
                ObjectGuid.InvalidObjectGuid;

            sTargetHeightAboveFloor =
                0f;

            sHasTargetHeight =
                false;

            /*
             * sStoredHeights intentionally stays alive for this game session.
             */
        }
    }
}
