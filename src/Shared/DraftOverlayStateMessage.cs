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
        public string[] AvailablePlayers;
        public string[] RedPlayers;
        public string[] BluePlayers;
        public int PendingLateJoinerCount;
        public bool DummyModeActive;
        public string FooterText;

        public static DraftOverlayStateMessage Hidden()
        {
            return new DraftOverlayStateMessage
            {
                IsVisible = false,
                AvailablePlayers = Array.Empty<string>(),
                RedPlayers = Array.Empty<string>(),
                BluePlayers = Array.Empty<string>()
            };
        }
    }
}
