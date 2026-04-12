namespace schrader
{
    public sealed class ScoreboardBadgeEntryMessage
    {
        public string PlayerId;
        public ulong ClientId;
        public string BadgeText;
        public string ColorHex;
    }
}