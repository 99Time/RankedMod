using System;

namespace schrader
{
    public sealed class DraftOverlayStateMessage
    {
        public bool IsVisible;
        public bool IsCompleted;
        public string Title;
        public string RedCaptainName;
        public string BlueCaptainName;
        public string CurrentTurnName;
        public ulong CurrentTurnClientId;
        public string CurrentTurnSteamId;
        public string[] AvailablePlayers;
        public string[] RedPlayers;
        public string[] BluePlayers;
        public int PendingLateJoinerCount;
        public string[] PendingLateJoiners;
        public bool DummyModeActive;
        public string FooterText;

        public static DraftOverlayStateMessage Hidden()
        {
            return new DraftOverlayStateMessage
            {
                IsVisible = false,
                CurrentTurnClientId = 0,
                CurrentTurnSteamId = string.Empty,
                AvailablePlayers = Array.Empty<string>(),
                RedPlayers = Array.Empty<string>(),
                BluePlayers = Array.Empty<string>(),
                PendingLateJoiners = Array.Empty<string>()
            };
        }
    }
}
