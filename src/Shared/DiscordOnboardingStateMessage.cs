namespace schrader
{
    public sealed class DiscordOnboardingStateMessage
    {
        public bool IsResolved { get; set; }
        public bool IsLinked { get; set; }
        public bool IsPublicServer { get; set; }
        public bool IsTrainingServer { get; set; }

        public static DiscordOnboardingStateMessage Unresolved()
        {
            return new DiscordOnboardingStateMessage
            {
                IsResolved = false,
                IsLinked = false,
                IsPublicServer = false,
                IsTrainingServer = false
            };
        }
    }
}