namespace schrader
{
    public sealed class VoteOverlayStateMessage
    {
        public bool IsVisible;
        public string Title;
        public string PromptText;
        public string InitiatorName;
        public int SecondsRemaining;
        public int EligibleCount;
        public int YesVotes;
        public int NoVotes;
        public int RequiredYesVotes;
        public string FooterText;

        public static VoteOverlayStateMessage Hidden()
        {
            return new VoteOverlayStateMessage
            {
                IsVisible = false,
                Title = "RANKED VOTE"
            };
        }
    }
}