namespace schrader
{
    public sealed class VoteOverlayPlayerEntryMessage
    {
        public ulong ClientId;
        public string PlayerId;
        public string SteamId;
        public string DisplayName;
        public int PlayerNumber;
        public bool HasVoted;
        public bool VoteAccepted;
        public bool IsInitiator;
    }
}