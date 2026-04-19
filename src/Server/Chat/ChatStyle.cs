using System;
using System.Text;

namespace schrader.Server
{
    internal enum ChatTone
    {
        Neutral,
        Info,
        Success,
        Warning,
        Error
    }

    internal static class ChatStyle
    {
        internal const string AdminModule = "Admin";
        internal const string DiscordModule = "Discord";
        internal const string DraftModule = "Draft";
        internal const string DraftUiModule = "Draft UI";
        internal const string DummyModule = "Dummy";
        internal const string ForfeitModule = "Forfeit";
        internal const string RankedModule = "Ranked";
        internal const string RecordModule = "Record";
        internal const string ReplayModule = "Replay";
        internal const string SharedGoalieModule = "Shared Goalie";
        internal const string UsageModule = "Usage";

        internal static string Message(string module, string body, ChatTone tone = ChatTone.Neutral, int size = 14)
        {
            var safeModule = Safe(module);
            var safeBody = string.IsNullOrWhiteSpace(body) ? string.Empty : body.Trim();
            return $"<size={size}><b><color={GetModuleColor(module)}>{safeModule}</color></b> <color={GetToneColor(tone)}>{safeBody}</color></size>";
        }

        internal static string Usage(string usage, string detail = null, string module = null)
        {
            var builder = new StringBuilder();
            builder.Append(Command(usage));
            if (!string.IsNullOrWhiteSpace(detail))
            {
                builder.Append(' ');
                builder.Append(Safe(detail));
            }

            return Message(string.IsNullOrWhiteSpace(module) ? UsageModule : module, builder.ToString(), ChatTone.Info, 13);
        }

        internal static string HelpHeading(string title, string colorHex = "#9dc4de")
        {
            return $"<size=12><color={colorHex}>{Safe(title)}</color></size>";
        }

        internal static string HelpCommand(string usage, string description)
        {
            return $"<size=13>{Command(usage)}</size> <size=12>- {Safe(description)}</size>";
        }

        internal static string Command(string value)
        {
            return $"<b><color=#8dd8ff>{Safe(value)}</color></b>";
        }

        internal static string Player(string value)
        {
            return $"<b>{Safe(string.IsNullOrWhiteSpace(value) ? "Player" : value)}</b>";
        }

        internal static string Emphasis(string value)
        {
            return $"<b>{Safe(value)}</b>";
        }

        internal static string Team(TeamResult team)
        {
            switch (team)
            {
                case TeamResult.Red:
                    return "<b><color=#ff7a7a>Red</color></b>";
                case TeamResult.Blue:
                    return "<b><color=#7ab8ff>Blue</color></b>";
                default:
                    return "<b>Unknown</b>";
            }
        }

        internal static string Team(string team)
        {
            if (string.Equals(team, "red", StringComparison.OrdinalIgnoreCase))
            {
                return Team(TeamResult.Red);
            }

            if (string.Equals(team, "blue", StringComparison.OrdinalIgnoreCase))
            {
                return Team(TeamResult.Blue);
            }

            return Emphasis(team);
        }

        internal static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private static string GetModuleColor(string module)
        {
            switch (module)
            {
                case AdminModule:
                    return "#ffd166";
                case DiscordModule:
                    return "#8dd8ff";
                case DraftModule:
                    return "#ffcf70";
                case DraftUiModule:
                    return "#ffcf70";
                case DummyModule:
                    return "#ffcf70";
                case ForfeitModule:
                    return "#ff9f66";
                case RankedModule:
                    return "#ffcf70";
                case RecordModule:
                    return "#9dd8ff";
                case ReplayModule:
                    return "#9dd8ff";
                case SharedGoalieModule:
                    return "#66ccff";
                case UsageModule:
                    return "#9dc4de";
                default:
                    return "#d7e6f2";
            }
        }

        private static string GetToneColor(ChatTone tone)
        {
            switch (tone)
            {
                case ChatTone.Info:
                    return "#d7eef8";
                case ChatTone.Success:
                    return "#d8f2e6";
                case ChatTone.Warning:
                    return "#ffe7a6";
                case ChatTone.Error:
                    return "#ffd1d1";
                default:
                    return "#eef4f8";
            }
        }
    }
}