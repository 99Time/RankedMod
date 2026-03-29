using System;

namespace schrader
{
    public sealed class DraftOverlayExtendedMessage
    {
        public bool IsVisible;
        public DraftOverlayPlayerEntryMessage[] AvailablePlayerEntries;
        public DraftOverlayPlayerEntryMessage[] RedPlayerEntries;
        public DraftOverlayPlayerEntryMessage[] BluePlayerEntries;
        public DraftOverlayPlayerEntryMessage[] PendingLateJoinerEntries;

        public static DraftOverlayExtendedMessage Hidden()
        {
            return new DraftOverlayExtendedMessage
            {
                IsVisible = false,
                AvailablePlayerEntries = Array.Empty<DraftOverlayPlayerEntryMessage>(),
                RedPlayerEntries = Array.Empty<DraftOverlayPlayerEntryMessage>(),
                BluePlayerEntries = Array.Empty<DraftOverlayPlayerEntryMessage>(),
                PendingLateJoinerEntries = Array.Empty<DraftOverlayPlayerEntryMessage>()
            };
        }
    }
}