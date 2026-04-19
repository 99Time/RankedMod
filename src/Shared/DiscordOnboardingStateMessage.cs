namespace schrader
{
    public sealed class DiscordOnboardingStateMessage
    {
        public bool IsResolved { get; set; }
        public bool IsLinked { get; set; }

        public static DiscordOnboardingStateMessage Unresolved()
        {
            return new DiscordOnboardingStateMessage
            {
                IsResolved = false,
                IsLinked = false
            };
        }
    }
}