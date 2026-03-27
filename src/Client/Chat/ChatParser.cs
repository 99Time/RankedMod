using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace schrader
{
    internal static class ChatParser
    {
        private static List<string> buffer = new List<string>();

        [HarmonyPatch(typeof(UIChat), "AddChatMessage")]
        private static class UIChatAddChatMessagePatch
        {
            [HarmonyPostfix]
            private static void Postfix(string __0)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(__0)) return;

                    // Detect inicio del bloque
                    if (__0.Contains("[DRAFT_STATE]"))
                    {
                        buffer.Clear();
                        buffer.Add(__0);
                        return;
                    }

                    // Si estamos dentro de un bloque
                    if (buffer.Count > 0)
                    {
                        buffer.Add(__0);

                        // Detectamos final del bloque
                        if (__0.StartsWith("footer=") || __0.Contains("dummy="))
                        {
                            string fullMessage = string.Join("\n", buffer);
                            buffer.Clear();

                            var state = ParseDraftState(fullMessage);
                            if (state != null)
                            {
                                Debug.Log("DRAFT STATE PARSED OK");
                                DraftUIController.ApplyState(state);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("CHAT PARSER ERROR: " + e.Message);
                }
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
                if (line.Contains("[DRAFT_STATE]")) continue;

                var splitIndex = line.IndexOf('=');
                if (splitIndex <= 0) continue;

                var key = line.Substring(0, splitIndex).Trim();
                var value = line.Substring(splitIndex + 1).Trim();
                kv[key] = value;
            }

            return new DraftOverlayStateMessage
            {
                IsVisible = !kv.TryGetValue("visible", out var visRaw) || visRaw == "1",
                IsCompleted = kv.TryGetValue("status", out var s) && s == "complete",
                Title = Get(kv, "title", "RANKED MATCH SETUP"),
                RedCaptainName = Get(kv, "captain_red", "Pending"),
                BlueCaptainName = Get(kv, "captain_blue", "Pending"),
                CurrentTurnName = Get(kv, "turn", "Pending"),
                AvailablePlayers = Split(Get(kv, "players", "")),
                RedPlayers = Split(Get(kv, "red_players", "")),
                BluePlayers = Split(Get(kv, "blue_players", "")),
                PendingLateJoinerCount = Int(kv, "pending"),
                DummyModeActive = Get(kv, "dummy", "0") == "1",
                FooterText = Get(kv, "footer", "")
            };
        }

        private static string[] Split(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
            return raw.Split(',');
        }

        private static int Int(Dictionary<string, string> kv, string key)
        {
            return kv.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : 0;
        }

        private static string Get(Dictionary<string, string> kv, string key, string def)
        {
            return kv.TryGetValue(key, out var v) ? v : def;
        }
    }
}