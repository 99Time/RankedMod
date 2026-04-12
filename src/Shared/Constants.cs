namespace schrader
{
    public static class Constants
    {
        public const string MOD_NAME = "SchraderRankedSystem";
        // Assembly name for server build
        public const string SERVER_ASSEMBLY = "SchraderRankedSystem.Server";
        public const string DISCORD_INVITE_URL = "https://discord.gg/mKmHjKJBq4";
        public const string SPEEDHOSTING_SITE_URL = "https://speedhosting.site";
        public const string SPEEDHOSTING_PUCK_URL = SPEEDHOSTING_SITE_URL + "/puck";
        public const string HOST_SOURCE_WELCOME = "welcome";
        public const string HOST_SOURCE_POSTMATCH = "postmatch";
        public const string HOST_SOURCE_CHAT = "chat";
        public const string HOST_SOURCE_HOSTCOMMAND = "hostcommand";

        // Default config
        public const int DEFAULT_MMR = 400;

        public static string BuildPuckLandingUrl(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return SPEEDHOSTING_PUCK_URL;
            }

            return SPEEDHOSTING_PUCK_URL + "?src=" + System.Uri.EscapeDataString(source.Trim().ToLowerInvariant());
        }
    }
}
