using System;

namespace schrader
{
    public sealed class MatchResultMessage
    {
        public bool IsVisible;
        public TeamResult WinningTeam;
        public bool UsePublicPresentation;
        public MatchResultPlayerMessage[] Players;

        public static MatchResultMessage Hidden()
        {
            return new MatchResultMessage
            {
                IsVisible = false,
                UsePublicPresentation = false,
                Players = Array.Empty<MatchResultPlayerMessage>()
            };
        }
    }
}