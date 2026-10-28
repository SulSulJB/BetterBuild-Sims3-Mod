using System;
using MonoPatcherLib;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.UI;

namespace BetterBuildByJojobou
{
    [Plugin]
    public class Main
    {
        private const string StartupAlarmName =
            "BetterBuildByJojobou Startup";

        private const string LoadedNotificationKey =
            "Jojobou/BetterBuild/Main:Loaded";

        private static bool sInitialized;

        public Main()
        {
            if (sInitialized)
            {
                return;
            }

            sInitialized = true;

            // NOTE: no explicit MonoPatcher.PatchAll() here.
            // [Plugin] already applies every [TypePatch] automatically
            // (ApplyPatchesAutomatically defaults to true), so calling it
            // again would patch everything twice.

            // Display the startup notification once the world UI is available.
            World.OnWorldLoadFinishedEventHandler -=
                OnWorldLoadFinished;

            World.OnWorldLoadFinishedEventHandler +=
                OnWorldLoadFinished;
        }

        private static void OnWorldLoadFinished(
            object sender,
            EventArgs e)
        {
            AlarmManager.Global.AddAlarm(
                1f,
                TimeUnit.Seconds,
                new AlarmTimerCallback(OnDelayedStartup),
                StartupAlarmName,
                AlarmType.NeverPersisted,
                null
            );
        }

        private static void OnDelayedStartup()
        {
            string loadedNotification =
                Localization.LocalizeString(
                    LoadedNotificationKey,
                    new object[0]
                );

            StyledNotification.Show(
                new StyledNotification.Format(
                    loadedNotification,
                    StyledNotification.NotificationStyle
                        .kGameMessagePositive
                )
            );
        }
    }
}