using System;
using System.Collections.Generic;
using HarmonyLib;

namespace schrader
{
    internal static class ChatParser
    {
        [HarmonyPatch(typeof(UIChat), "AddChatMessage")]
        private static class UIChatAddChatMessagePatch
        {
            [HarmonyPostfix]
            private static void Postfix(string __0)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(__0)) return;
                    if (!__0.Contains("[DRAFT_STATE]")) return;

                    var state = ParseDraftState(__0);
                    if (state == null) return;

                    DraftUIController.ApplyState(state);
                }
                catch { }
            }
        }

        private static DraftOverlayStateMessage ParseDraftState(string message)
        {
            var lines = message.Replace("\r", string.Empty).Split('\n');
            var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var lineRaw in lines)
            {
                var line = (lineRaw ?? string.Empty).Trim();
                if (line.Length == 0) continue;
                if (line.Equals("[DRAFT_STATE]", StringComparison.OrdinalIgnoreCase)) continue;

                var splitIndex = line.IndexOf('=');
                if (splitIndex <= 0) continue;

                var key = line.Substring(0, splitIndex).Trim();
                var value = line.Substring(splitIndex + 1).Trim();
                kv[key] = value;
            }

            var state = new DraftOverlayStateMessage
            {
                IsVisible = !kv.TryGetValue("visible", out var visRaw) || visRaw == "1" || visRaw.Equals("true", StringComparison.OrdinalIgnoreCase),
                IsCompleted = kv.TryGetValue("status", out var statusRaw) && statusRaw.Equals("complete", StringComparison.OrdinalIgnoreCase),
                Title = GetOrDefault(kv, "title", "RANKED MATCH SETUP"),
                RedCaptainName = GetOrDefault(kv, "captain_red", "Pending"),
                BlueCaptainName = GetOrDefault(kv, "captain_blue", "Pending"),
                CurrentTurnName = GetOrDefault(kv, "turn", "Pending"),
                AvailablePlayers = SplitCsv(GetOrDefault(kv, "players", string.Empty)),
                RedPlayers = Array.Empty<string>(),
                BluePlayers = Array.Empty<string>(),
                PendingLateJoinerCount = TryGetInt(kv, "pending"),
                DummyModeActive = GetOrDefault(kv, "dummy", "0") == "1",
                FooterText = GetOrDefault(kv, "footer", "")
            };

            return state;
        }

        private static string[] SplitCsv(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
            var parts = raw.Split(',');
            var list = new List<string>();
            foreach (var part in parts)
            {
                var value = part?.Trim();
                if (!string.IsNullOrWhiteSpace(value)) list.Add(value);
            }

            return list.ToArray();
        }

        private static int TryGetInt(Dictionary<string, string> kv, string key)
        {
            return kv.TryGetValue(key, out var raw) && int.TryParse(raw, out var parsed) ? parsed : 0;
        }

        private static string GetOrDefault(Dictionary<string, string> kv, string key, string fallback)
        {
            return kv.TryGetValue(key, out var value) ? value : fallback;
        }
    }
}
