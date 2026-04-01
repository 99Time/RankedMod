namespace schrader
{
    public sealed class DraftOverlayPlayerEntryMessage
    {
        public string CommandTarget;
        public string DisplayName;
        public int PlayerNumber;
        public bool HasMmr;
        public int Mmr;
        public bool IsCaptain;
        public TeamResult Team;
    }
}