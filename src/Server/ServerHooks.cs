using UnityEngine;

namespace schrader
{
    internal static class DraftStateBridge
    {
        public static void EnsureInitialized()
        {
        }

        public static void PublishState(Server.RankedSystem.DraftOverlayState state)
        {
            RankedOverlayNetwork.PublishDraftState(state);
        }

        public static bool Toggle()
        {
            return false;
        }

        public static bool CanRenderInCurrentProcess()
        {
            return !Application.isBatchMode;
        }

        public static string GetUnavailableReason()
        {
            return Application.isBatchMode
                ? "Draft UI is client-only and is not available on a dedicated server."
                : string.Empty;
        }

        public static bool IsTestModeEnabled()
        {
            return false;
        }

        public static void Shutdown()
        {
        }
    }
}
