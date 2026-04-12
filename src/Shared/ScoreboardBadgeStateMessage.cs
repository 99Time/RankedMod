using System;

namespace schrader
{
    public sealed class ScoreboardBadgeStateMessage
    {
        public ScoreboardBadgeEntryMessage[] Players;

        public static ScoreboardBadgeStateMessage Empty()
        {
            return new ScoreboardBadgeStateMessage
            {
                Players = Array.Empty<ScoreboardBadgeEntryMessage>()
            };
        }
    }
}