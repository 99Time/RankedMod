using System;

namespace schrader
{
    public sealed class ScoreboardStarStateMessage
    {
        public ScoreboardStarEntryMessage[] Players;

        public static ScoreboardStarStateMessage Empty()
        {
            return new ScoreboardStarStateMessage
            {
                Players = Array.Empty<ScoreboardStarEntryMessage>()
            };
        }
    }
}