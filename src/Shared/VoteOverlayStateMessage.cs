using System;

namespace schrader
{
    public sealed class VoteOverlayStateMessage
    {
        public bool IsVisible;
        public string Title;
        public string PromptText;
        public string InitiatorName;
        public int SecondsRemaining;
        public float SecondsRemainingPrecise;
        public int VoteDurationSeconds;
        public int EligibleCount;
        public int YesVotes;
        public int NoVotes;
        public int RequiredYesVotes;
        public string FooterText;
        public VoteOverlayPlayerEntryMessage[] PlayerEntries;

        public static VoteOverlayStateMessage Hidden()
        {
            return new VoteOverlayStateMessage
            {
                IsVisible = false,
                Title = "RANKED VOTE",
                PlayerEntries = Array.Empty<VoteOverlayPlayerEntryMessage>()
            };
        }
    }
}