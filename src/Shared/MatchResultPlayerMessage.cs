namespace schrader
{
    public sealed class MatchResultPlayerMessage
    {
        public string Id;
        public string SteamId;
        public string Username;
        public int PlayerNumber;
        public bool IsSharedGoalie;
        public bool ExcludedFromMmr;
        public TeamResult Team;
        public int Goals;
        public int Assists;
        public int Saves;
        public int Shots;
        public int MmrBefore;
        public int MmrAfter;
        public int MmrDelta;
        public bool IsMVP;
    }
}