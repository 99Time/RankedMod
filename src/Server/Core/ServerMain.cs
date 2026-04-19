using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using Unity.Netcode;
using Newtonsoft.Json;
using System.Linq;

namespace schrader
{
    public class CustomMOTD : IPuckMod
    {
        static readonly Harmony harmony = new Harmony(Constants.MOD_NAME);
        private const string OwnerSteamId = "76561199046098825";
        private static GameObject updaterGo;
        private static RankedSystemUpdater updaterInstance;
        private const float ManualPuckSpawnCooldownSeconds = 4.0f;
        private const int ManualPuckSpawnHardCap = 30;
        private static readonly Dictionary<string, float> manualPuckSpawnTimes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        [HarmonyPatch(typeof(UIChatController), "Event_Server_OnSynchronizeComplete")]
        public class UIChatControllerPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(Dictionary<string, object> message)
            {
                ulong clientId = (ulong)message["clientId"];

                Debug.Log($"[{Constants.MOD_NAME}] [JOIN][SERVER] Synchronize complete received for client {clientId}.");
                try
                {
                    UIChat.Instance.Server_SendSystemChatMessage(
                        $"<size=18><b><color=#00ff00>Welcome to SpeedRankeds!</color></b></size>\n" +
                        $"<size=13><color=#d8f2e6>Use <b>/commands</b> to display available server chat commands.</color></size>\n" +
                        $"<size=13><b><color=#78d8ff>Host your own PUCK server</color></b></size>\n" +
                        $"<size=13><color=#ffffff>{Constants.BuildPuckLandingUrl(Constants.HOST_SOURCE_CHAT)}</color> <color=#9fc4db>(or use <b>/host</b>)</color></size>",
                        clientId);
                }
                catch { }
                try { Server.RankedSystem.HandleBackendPlayerSynchronized(clientId); } catch { }
                try { RankedOverlayNetwork.ResyncClient(clientId); } catch { }
                return false;
            }
        }

        [HarmonyPatch(typeof(UIChat), "WrapPlayerUsername")]
        public class UIChatWrapPlayerUsernamePatch
        {
            [HarmonyPostfix]
            public static void Postfix(Player player, ref string __result)
            {
                try
                {
                    var nameColorSpec = Server.RankedSystem.GetStoredNameColorHexForPlayer(player, player != null ? player.OwnerClientId : 0UL);
                    if (!string.IsNullOrWhiteSpace(nameColorSpec) && player != null)
                    {
                        var username = player.Username.Value.ToString();
                        if (string.IsNullOrWhiteSpace(username))
                        {
                            username = "Player";
                        }
                        var wrappedName = $"#{player.Number.Value} {username}";
                        __result = BuildStyledNameLabel(wrappedName, nameColorSpec);
                    }

                    if (player != null && !string.IsNullOrEmpty(__result))
                    {
                        if (IsOwnerPlayer(player))
                        {
                            __result = BuildOwnerNamePrefix() + __result;
                        }
                        else if (TryIsAdmin(player, player.OwnerClientId))
                        {
                            __result = BuildAdminNamePrefix() + __result;
                        }
                    }

                    var starPrefix = Server.RankedSystem.BuildChatStarPrefix(player);
                    var backendPrefix = Server.RankedSystem.BuildBackendChatPrefix(player, player != null ? player.OwnerClientId : 0UL);
                    if (string.IsNullOrEmpty(__result))
                    {
                        return;
                    }

                    __result = (starPrefix ?? string.Empty) + (backendPrefix ?? string.Empty) + __result;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{Constants.MOD_NAME}] [CHAT][STAR] Failed to format player username: {ex.Message}");
                }
            }
        }

        // Server-side only: use chat command `/s` to spawn a networked puck.

        [HarmonyPatch(typeof(UIChat), "Server_ProcessPlayerChatMessage")]
        public class UIChatServerProcessChatPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(UIChat __instance, Player player, ref string message, ulong clientId, bool useTeamChat, bool isMuted)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(message)) return true;
                    var trimmed = message.Trim();

                    if (Server.RankedSystem.TryHandleDraftCommand(player, clientId, trimmed))
                    {
                        return false;
                    }

                    if (trimmed.Equals("/ff", StringComparison.OrdinalIgnoreCase))
                    {
                        try { Server.RankedSystem.HandleForfeitVote(player, clientId); } catch { }
                        return false;
                    }

                    if (trimmed.Equals("/vr", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleRankedVoteStart(player, clientId);
                        return false;
                    }

                    if (trimmed.Equals("/votesinglegoalie", StringComparison.OrdinalIgnoreCase))
                    {
                        Server.RankedSystem.HandleSingleGoalieVoteStart(player, clientId);
                        return false;
                    }

                    if (trimmed.Equals("/vs", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            UIChat.Instance.Server_SendSystemChatMessage("<size=13><color=#ffcc66>Ranked</color> Normal start disabled. Redirecting to ranked vote...</size>", clientId);
                        }
                        catch { }

                        HandleRankedVoteStart(player, clientId);
                        return false;
                    }

                    if (trimmed.Equals("/y", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Server.RankedSystem.IsSingleGoalieVoteActive())
                            Server.RankedSystem.HandleSingleGoalieVoteResponse(player, clientId, true);
                        else if (Server.RankedSystem.IsForfeitActive())
                            Server.RankedSystem.HandleForfeitVoteResponse(player, clientId, true);
                        else
                            HandleRankedVoteResponse(player, clientId, true);
                        return false;
                    }

                    if (trimmed.Equals("/n", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Server.RankedSystem.IsSingleGoalieVoteActive())
                            Server.RankedSystem.HandleSingleGoalieVoteResponse(player, clientId, false);
                        else if (Server.RankedSystem.IsForfeitActive())
                            Server.RankedSystem.HandleForfeitVoteResponse(player, clientId, false);
                        else
                            HandleRankedVoteResponse(player, clientId, false);
                        return false;
                    }

                    if (trimmed.Equals("/mmr", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var pid = TryGetPlayerId(player, clientId);
                            var mmr = GetMmr(pid);
                            UIChat.Instance.Server_SendSystemChatMessage($"<size=14>Your MMR: <b>{mmr}</b></size>", clientId);
                        }
                        catch { }
                        return false;
                    }

                    if (trimmed.Equals("/discord", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            UIChat.Instance.Server_SendSystemChatMessage("<size=14><color=#66ccff>Opening Discord invite...</color></size>", clientId);
                        }
                        catch { }

                        try
                        {
                            RankedOverlayNetwork.PublishDiscordInviteOpenToClient(clientId);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[{Constants.MOD_NAME}] /discord failed: {ex.Message}");
                        }

                        return false;
                    }

                    if (trimmed.Equals("/link", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("/link ", StringComparison.OrdinalIgnoreCase))
                    {
                        var linkCode = trimmed.Length > 5 ? trimmed.Substring(5).Trim() : string.Empty;
                        if (string.IsNullOrWhiteSpace(linkCode))
                        {
                            SendSystemChatToClient("<size=14>Usage: /link <code></size>", clientId);
                            return false;
                        }

                        Server.RankedSystem.StartDiscordLinkComplete(clientId, linkCode);
                        return false;
                    }

                    if (trimmed.Equals("/host", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            UIChat.Instance.Server_SendSystemChatMessage("<size=14><color=#66ccff>Opening the hosting page in your browser...</color></size>", clientId);
                        }
                        catch { }

                        try
                        {
                            RankedOverlayNetwork.PublishExternalUrlOpenToClient(clientId, Constants.BuildPuckLandingUrl(Constants.HOST_SOURCE_HOSTCOMMAND));
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[{Constants.MOD_NAME}] /host failed: {ex.Message}");
                        }

                        return false;
                    }

                    if (trimmed.Equals("/commands", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            SendCommandsOverview(player, clientId);
                        }
                        catch { }
                        return false;
                    }

                    if (TryHandleBackendModerationCommand(player, clientId, trimmed))
                    {
                        return false;
                    }

                    if (trimmed.Equals("/cs", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleServerDespawnPucksCommand(clientId);
                        return false;
                    }

                    if (trimmed.StartsWith("/fc", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryIsAdmin(player, clientId))
                        {
                            SendCommandHelpLine(clientId, "<color=#ff6666>You must be an admin to use this command</color>");
                            return false;
                        }

                            var commandArgs = trimmed.Length > 3 ? trimmed.Substring(3).Trim() : string.Empty;
                            var separatorIndex = commandArgs.LastIndexOf(' ');
                            if (separatorIndex <= 0)
                        {
                                SendSystemChatToClient("<size=14>Usage: /fc <player|steamId|#number> <red|blue|spectator></size>", clientId);
                            return false;
                        }

                            var playerTarget = commandArgs.Substring(0, separatorIndex).Trim();
                            var requestedTeam = commandArgs.Substring(separatorIndex + 1).Trim();

                            if (!Server.RankedSystem.TryForcePlayerTeamByTarget(playerTarget, requestedTeam, out var forceTeamMessage))
                        {
                            SendSystemChatToClient($"<size=14><color=#ff6666>{forceTeamMessage}</color></size>", clientId);
                            return false;
                        }

                        SendSystemChatToAll($"<size=14><color=#ffcc66>Admin</color> {forceTeamMessage}</size>");
                        return false;
                    }

                    if (trimmed.StartsWith("/addscore", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryIsAdmin(player, clientId))
                        {
                            SendCommandHelpLine(clientId, "<color=#ff6666>You must be an admin to use this command</color>");
                            return false;
                        }

                        var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length != 3 || !int.TryParse(parts[1], out var amount))
                        {
                            SendSystemChatToClient("<size=14>Usage: /addscore <amount> <red|blue></size>", clientId);
                            return false;
                        }

                        if (!Server.RankedSystem.TryAddScore(parts[2], amount, out var redScore, out var blueScore, out var addScoreError))
                        {
                            SendSystemChatToClient($"<size=14><color=#ff6666>{addScoreError}</color></size>", clientId);
                            return false;
                        }

                        SendSystemChatToAll($"<size=14><color=#ffcc66>Admin</color> adjusted score to <b>Red {redScore} - Blue {blueScore}</b>.</size>");
                        return false;
                    }

                    if (trimmed.StartsWith("/setnamecolor", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryIsAdmin(player, clientId))
                        {
                            SendCommandHelpLine(clientId, "<color=#ff6666>You must be an admin to use this command</color>");
                            return false;
                        }

                        var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 3)
                        {
                            SendSystemChatToClient("<size=14>Usage: /setnamecolor <player|steamId|#number> <color|rgb|#RRGGBB|reset></size>", clientId);
                            return false;
                        }

                        var requestedColor = parts[parts.Length - 1];
                        var rawTarget = string.Join(" ", parts.Skip(1).Take(parts.Length - 2));
                        if (!Server.RankedSystem.TrySetNameColorForCommand(rawTarget, requestedColor, out var resultMessage))
                        {
                            SendSystemChatToClient($"<size=14><color=#ff6666>{resultMessage}</color></size>", clientId);
                            return false;
                        }

                        SendSystemChatToAll($"<size=14><color=#ffcc66>Admin</color> {resultMessage}</size>");
                        return false;
                    }

                    if (trimmed.StartsWith("/setchatcolor", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryIsAdmin(player, clientId))
                        {
                            SendCommandHelpLine(clientId, "<color=#ff6666>You must be an admin to use this command</color>");
                            return false;
                        }

                        var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 3)
                        {
                            SendSystemChatToClient("<size=14>Usage: /setchatcolor <player|steamId|#number> <color|rgb|#RRGGBB|reset></size>", clientId);
                            return false;
                        }

                        var requestedColor = parts[parts.Length - 1];
                        var rawTarget = string.Join(" ", parts.Skip(1).Take(parts.Length - 2));
                        if (!Server.RankedSystem.TrySetMessageColorForCommand(rawTarget, requestedColor, out var resultMessage))
                        {
                            SendSystemChatToClient($"<size=14><color=#ff6666>{resultMessage}</color></size>", clientId);
                            return false;
                        }

                        SendSystemChatToAll($"<size=14><color=#ffcc66>Admin</color> {resultMessage}</size>");
                        return false;
                    }

                        // Admin-style ranked controls: /ranked start | /ranked end [red|blue|draw] | /ranked test <on|off|status> | /ranked status publish
                        if (trimmed.StartsWith("/ranked", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = trimmed.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                var sub = parts[1].ToLowerInvariant();
                                if (sub == "start")
                                {
                                    if (!TryIsAdmin(player, clientId))
                                    {
                                        SendCommandHelpLine(clientId, "<color=#ff6666>You must be an admin to use this command</color>");
                                        return false;
                                    }

                                    if (!TryGetEligiblePlayersForStart(player, clientId, out var eligible, out var reason))
                                    {
                                        SendSystemChatToClient($"<size=14><color=#ff6666>Ranked</color> cannot start: {reason}</size>", clientId);
                                        return false;
                                    }

                                    Server.RankedSystem.ForceStart(eligible);
                                    return false;
                                }
                                else if (sub == "end")
                                {
                                    if (!TryIsAdmin(player, clientId))
                                    {
                                        SendCommandHelpLine(clientId, "<color=#ff6666>You must be an admin to use this command</color>");
                                        return false;
                                    }

                                        // optional winner argument
                                        if (parts.Length >= 3)
                                        {
                                            var arg = parts[2].ToLowerInvariant();
                                            if (arg == "red" || arg == "r")
                                            {
                                                EndRankedMatch(TeamResult.Red, true, true);
                                                SendSystemChatToAll("<size=14><color=#ffcc66>Ranked</color> forcibly ended by admin. Winner: Red.</size>");
                                                return false;
                                            }
                                            else if (arg == "blue" || arg == "b")
                                            {
                                                EndRankedMatch(TeamResult.Blue, true, true);
                                                SendSystemChatToAll("<size=14><color=#ffcc66>Ranked</color> forcibly ended by admin. Winner: Blue.</size>");
                                                return false;
                                            }
                                            else if (arg == "draw")
                                            {
                                                EndRankedMatch(TeamResult.Unknown, true, true);
                                                SendSystemChatToAll("<size=14><color=#ffcc66>Ranked</color> forcibly ended by admin: draw. MMR unchanged.</size>");
                                                return false;
                                            }
                                        }

                                        EndRankedMatch(TeamResult.Unknown, true, true);
                                        SendSystemChatToAll("<size=14><color=#ffcc66>Ranked</color> forcibly ended by admin: draw. MMR unchanged.</size>");
                                        return false;
                                }
                                else if (sub == "test")
                                {
                                    if (!TryIsAdmin(player, clientId))
                                    {
                                        SendCommandHelpLine(clientId, "<color=#ff6666>You must be an admin to use this command</color>");
                                        return false;
                                    }

                                    if (!Server.RankedSystem.AreSyntheticPlayersAllowed())
                                    {
                                        SendSystemChatToClient("<size=14><color=#ff6666>Ranked</color> controlled test mode is disabled on this server.</size>", clientId);
                                        return false;
                                    }

                                    var toggle = parts.Length >= 3 ? parts[2].ToLowerInvariant() : "status";
                                    if (toggle == "on")
                                    {
                                        SetControlledTestModeEnabled(true);
                                        SendSystemChatToAll("<size=14><color=#ffcc66>Ranked</color> controlled test mode enabled.</size>");
                                        return false;
                                    }

                                    if (toggle == "off")
                                    {
                                        SetControlledTestModeEnabled(false);
                                        SendSystemChatToAll("<size=14><color=#ffcc66>Ranked</color> controlled test mode disabled.</size>");
                                        return false;
                                    }

                                    var statusText = IsControlledTestModeEnabled() ? "enabled" : "disabled";
                                    SendSystemChatToClient($"<size=14><color=#ffcc66>Ranked</color> controlled test mode is currently <b>{statusText}</b>.</size>", clientId);
                                    return false;
                                }
                                else if (sub == "status")
                                {
                                    if (!TryIsAdmin(player, clientId))
                                    {
                                        SendCommandHelpLine(clientId, "<color=#ff6666>You must be an admin to use this command</color>");
                                        return false;
                                    }

                                    var action = parts.Length >= 3 ? parts[2].ToLowerInvariant() : "publish";
                                    if (action == "publish")
                                    {
                                        _ = Server.RankedSystem.PublishServerStatusFromSpeedupAsync(debugMessage => DebugToClient(clientId, debugMessage));
                                        DebugToClient(clientId, "Publishing Discord server status from SpeedUP API...");
                                        return false;
                                    }

                                    SendSystemChatToClient("<size=14>Usage: /ranked status publish</size>", clientId);
                                    return false;
                                }
                            }

                            SendSystemChatToClient("<size=14>Usage: /ranked start | /ranked end [red|blue|draw] | /ranked test <on|off|status> | /ranked status publish</size>", clientId);
                            return false;
                        }

                    if (!trimmed.StartsWith("/", StringComparison.Ordinal))
                    {
                        if (Server.RankedSystem.IsBackendMuted(player, clientId, out var muteReason))
                        {
                            SendSystemChatToClient($"<size=14><color=#ff6666>{muteReason}</color></size>", clientId);
                            return false;
                        }

                        try
                        {
                            var starPrefix = Server.RankedSystem.BuildChatStarPrefix(player);
                            if (!string.IsNullOrEmpty(starPrefix))
                            {
                                Debug.Log($"[{Constants.MOD_NAME}] [CHAT][STAR] Applying ranked star prefix for clientId={clientId} player={(player as Player)?.Username.Value ?? "?"}.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[{Constants.MOD_NAME}] [CHAT][STAR] Preflight failed: {ex.Message}");
                        }

                        try
                        {
                            if (TryHandleStyledPlayerChat(__instance, player, message, clientId, useTeamChat, isMuted))
                            {
                                return false;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[{Constants.MOD_NAME}] [CHAT] Failed to apply stored chat message color: {ex.Message}");
                        }
                    }

                    // Only handle exact "/s" or "/s <args>". Avoid capturing commands like "/start".
                    if (!(trimmed.Equals("/s", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("/s ", StringComparison.OrdinalIgnoreCase))) return true;

                    // Server-side: spawn puck at player's stick position
                    Vector3 spawnPos = Vector3.zero;
                    Quaternion rot = Quaternion.identity;
                    Vector3 vel = Vector3.zero;

                    var playerComp = player as Component;
                    if (playerComp != null)
                    {
                        if (!TryGetBladeSpawn(playerComp, out spawnPos, out rot, out vel))
                        {
                            spawnPos = playerComp.transform.position + playerComp.transform.forward * 1.5f + Vector3.up * 0.05f;
                            rot = Quaternion.LookRotation(playerComp.transform.forward);
                            vel = playerComp.transform.forward * 5f;
                        }
                    }
                    else
                    {
                        var cam = Camera.main;
                        if (cam != null)
                        {
                            spawnPos = cam.transform.position + cam.transform.forward * 2f;
                            rot = cam.transform.rotation;
                            vel = cam.transform.forward * 5f;
                        }
                    }

                    HandleServerSpawnPuckCommand(player, clientId, spawnPos, rot, vel);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{Constants.MOD_NAME}] Server_ProcessPlayerChatMessage patch error: {ex.Message}");
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(UIChatController), "Event_Server_OnChatCommand")]
        public class UIChatControllerChatCommandPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(Dictionary<string, object> message)
            {
                try
                {
                    if (message == null) return true;

                    // extract command and clientId
                    string cmd = null;
                    ulong clientId = 0;
                    if (message.ContainsKey("clientId")) { try { clientId = Convert.ToUInt64(message["clientId"]); } catch { } }

                    string[] possibleKeys = new[] { "message", "text", "chatMessage", "command", "msg" };
                    foreach (var k in possibleKeys)
                    {
                        if (message.ContainsKey(k) && message[k] is string s) { cmd = s.Trim(); break; }
                    }
                    if (cmd == null)
                    {
                        foreach (var val in message.Values) { if (val is string sv && sv.TrimStart().StartsWith("/")) { cmd = sv.Trim(); break; } }
                    }
                    if (string.IsNullOrEmpty(cmd)) return true;

                    if (Server.RankedSystem.TryHandleDraftCommand(null, clientId, cmd))
                    {
                        return false;
                    }

                    // /cs -> despawn all pucks
                    if (cmd.Equals("/cs", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleServerDespawnPucksCommand(clientId);
                        return false;
                    }

                    if (cmd.Equals("/host", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            RankedOverlayNetwork.PublishExternalUrlOpenToClient(clientId, Constants.BuildPuckLandingUrl(Constants.HOST_SOURCE_HOSTCOMMAND));
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[{Constants.MOD_NAME}] /host failed in chat command event: {ex.Message}");
                        }

                        return false;
                    }

                    if (cmd.Equals("/link", StringComparison.OrdinalIgnoreCase) || cmd.StartsWith("/link ", StringComparison.OrdinalIgnoreCase))
                    {
                        var linkCode = cmd.Length > 5 ? cmd.Substring(5).Trim() : string.Empty;
                        if (string.IsNullOrWhiteSpace(linkCode))
                        {
                            SendSystemChatToClient("<size=14>Usage: /link <code></size>", clientId);
                            return false;
                        }

                        Server.RankedSystem.StartDiscordLinkComplete(clientId, linkCode);
                        return false;
                    }

                    if (TryHandleBackendModerationCommand(null, clientId, cmd))
                    {
                        return false;
                    }

                    // /s -> spawn puck
                    if (cmd.Equals("/s", StringComparison.OrdinalIgnoreCase) || cmd.StartsWith("/s ", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleServerSpawnPuckCommand(null, clientId);
                        return false;
                    }

                }
                catch (Exception e)
                {
                    Debug.LogError($"[{Constants.MOD_NAME}] ChatCommand patch error: {e.Message}");
                }

                return true;
            }
        }

        private static void HandleServerDespawnPucksCommand(ulong clientId)
        {
            try
            {
                var pmType = FindTypeByName("PuckManager", "Puck.PuckManager");
                if (pmType == null)
                {
                    return;
                }

                var pmInstance = GetManagerInstance(pmType);
                if (pmInstance == null)
                {
                    return;
                }

                var method = pmType.GetMethod("Server_DespawnPucks", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null)
                {
                    return;
                }

                method.Invoke(pmInstance, new object[] { true });
                try { UIChat.Instance.Server_SendSystemChatMessage("<size=14><color=#00ff00>All pucks despawned.</color></size>", clientId); } catch { }
                Debug.Log($"[{Constants.MOD_NAME}] [PUCK-SPAWN] Cleared all pucks via /cs from clientId={clientId}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] /cs failed: {ex.Message}");
            }
        }

        private static void HandleServerSpawnPuckCommand(object player, ulong clientId)
        {
            var playerInstance = ResolveChatCommandPlayer(player, clientId);
            Vector3 spawnPos = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            Vector3 vel = Vector3.zero;

            if (playerInstance is Component playerComp)
            {
                if (!TryGetBladeSpawn(playerComp, out spawnPos, out rot, out vel))
                {
                    spawnPos = playerComp.transform.position + playerComp.transform.forward * 1.5f + Vector3.up * 0.05f;
                    rot = Quaternion.LookRotation(playerComp.transform.forward);
                    vel = playerComp.transform.forward * 5f;
                }
            }
            else
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    spawnPos = cam.transform.position + cam.transform.forward * 2f;
                    rot = cam.transform.rotation;
                    vel = cam.transform.forward * 5f;
                }
            }

            HandleServerSpawnPuckCommand(playerInstance, clientId, spawnPos, rot, vel);
        }

        private static void HandleServerSpawnPuckCommand(object player, ulong clientId, Vector3 spawnPos, Quaternion rot, Vector3 vel)
        {
            try
            {
                var requesterKey = ResolveManualPuckSpawnRequesterKey(player, clientId);
                var requesterName = TryGetPlayerName(player) ?? "Player";

                if (Server.RankedSystem.IsMatchActive())
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [PUCK-SPAWN] Denied spawn for {requesterKey} during active match.");
                    try { UIChat.Instance.Server_SendSystemChatMessage("<size=14><color=#ff6666>Cannot spawn pucks during an active match.</color></size>", clientId); } catch { }
                    return;
                }

                if (!TryValidateManualPuckSpawn(requesterKey, out var livePuckCount, out var waitSeconds, out var denialMessage))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [PUCK-SPAWN] Denied spawn for {requesterKey}. livePucks={livePuckCount} wait={waitSeconds:0.00}s");
                    if (!string.IsNullOrWhiteSpace(denialMessage))
                    {
                        try { UIChat.Instance.Server_SendSystemChatMessage(denialMessage, clientId); } catch { }
                    }
                    return;
                }

                var pmType = FindTypeByName("PuckManager", "Puck.PuckManager");
                if (pmType == null)
                {
                    return;
                }

                var pmInstance = GetManagerInstance(pmType);
                if (pmInstance == null)
                {
                    return;
                }

                var method = pmType.GetMethod("Server_SpawnPuck", BindingFlags.Public | BindingFlags.Instance);
                if (method == null)
                {
                    return;
                }

                using (Server.RankedSystem.BeginManualSpawn())
                {
                    var spawned = method.Invoke(pmInstance, new object[] { spawnPos, rot, vel, false });
                    if (spawned is Component spawnedComp)
                    {
                        try
                        {
                            var unfreeze = spawnedComp.GetType().GetMethod("Server_Unfreeze", BindingFlags.Public | BindingFlags.Instance);
                            if (unfreeze != null)
                            {
                                unfreeze.Invoke(spawnedComp, null);
                            }
                        }
                        catch { }

                        try
                        {
                            var rb = spawnedComp.GetComponent<Rigidbody>();
                            if (rb != null)
                            {
                                rb.linearVelocity = vel;
                                rb.WakeUp();
                            }
                        }
                        catch { }
                    }
                }

                RecordManualPuckSpawn(requesterKey);
                Debug.Log($"[{Constants.MOD_NAME}] [PUCK-SPAWN] Allowed spawn for {requesterKey} at {spawnPos}. livePucksBefore={livePuckCount}");
                try { UIChat.Instance.Server_SendSystemChatMessage($"<size=14><b><color=#00ff00>{requesterName}</color></b> has spawned a puck.</size>", clientId); } catch { }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] chat spawn failed: {ex.Message}");
            }
        }

        private static object ResolveChatCommandPlayer(object player, ulong clientId)
        {
            if (player != null)
            {
                return player;
            }

            try
            {
                var playerManagerType = FindTypeByName("PlayerManager", "Puck.PlayerManager");
                if (playerManagerType == null)
                {
                    return null;
                }

                var playerManager = GetManagerInstance(playerManagerType);
                if (playerManager == null)
                {
                    return null;
                }

                var getPlayer = playerManagerType.GetMethod("GetPlayerByClientId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                return getPlayer != null ? getPlayer.Invoke(playerManager, new object[] { clientId }) : null;
            }
            catch { }

            return null;
        }

        private static string ResolveManualPuckSpawnRequesterKey(object player, ulong clientId)
        {
            try
            {
                var playerKey = TryGetPlayerId(player, clientId);
                if (!string.IsNullOrWhiteSpace(playerKey))
                {
                    return playerKey;
                }
            }
            catch { }

            return clientId != 0 ? $"clientId:{clientId}" : "clientId:0";
        }

        private static bool TryValidateManualPuckSpawn(string requesterKey, out int livePuckCount, out float waitSeconds, out string denialMessage)
        {
            livePuckCount = GetLiveNonReplayPuckCount();
            waitSeconds = 0f;
            denialMessage = null;

            if (livePuckCount >= ManualPuckSpawnHardCap)
            {
                denialMessage = $"<size=14><color=#ff6666>Puck</color> spawn limit reached ({livePuckCount}/{ManualPuckSpawnHardCap}). Use <b>/cs</b> or wait for pucks to clear.</size>";
                return false;
            }

            if (string.IsNullOrWhiteSpace(requesterKey))
            {
                return true;
            }

            var now = Time.unscaledTime;
            lock (manualPuckSpawnTimes)
            {
                TrimManualPuckSpawnCooldowns_NoLock(now);

                if (manualPuckSpawnTimes.TryGetValue(requesterKey, out var lastSpawnTime))
                {
                    var elapsed = now - lastSpawnTime;
                    if (elapsed < ManualPuckSpawnCooldownSeconds)
                    {
                        waitSeconds = Mathf.Max(0f, ManualPuckSpawnCooldownSeconds - elapsed);
                        denialMessage = $"<size=14><color=#ff6666>Puck</color> wait {waitSeconds:0.0}s before spawning another puck.</size>";
                        return false;
                    }
                }
            }

            return true;
        }

        private static void RecordManualPuckSpawn(string requesterKey)
        {
            if (string.IsNullOrWhiteSpace(requesterKey))
            {
                return;
            }

            lock (manualPuckSpawnTimes)
            {
                manualPuckSpawnTimes[requesterKey] = Time.unscaledTime;
            }
        }

        private static void TrimManualPuckSpawnCooldowns_NoLock(float now)
        {
            var expiredKeys = manualPuckSpawnTimes
                .Where(entry => now - entry.Value >= ManualPuckSpawnCooldownSeconds)
                .Select (entry => entry.Key)
                .ToList();

            foreach (var expiredKey in expiredKeys)
            {
                manualPuckSpawnTimes.Remove(expiredKey);
            }
        }

        private static int GetLiveNonReplayPuckCount()
        {
            try
            {
                var pmType = FindTypeByName("PuckManager", "Puck.PuckManager");
                if (pmType == null)
                {
                    return 0;
                }

                var pmInstance = GetManagerInstance(pmType);
                if (pmInstance == null)
                {
                    return 0;
                }

                var getPucks = pmType.GetMethod("GetPucks", BindingFlags.Public | BindingFlags.Instance);
                if (getPucks == null)
                {
                    return 0;
                }

                object result;
                var parameters = getPucks.GetParameters();
                if (parameters.Length == 0)
                {
                    result = getPucks.Invoke(pmInstance, null);
                }
                else
                {
                    result = getPucks.Invoke(pmInstance, new object[] { false });
                }

                if (result is System.Collections.ICollection collection)
                {
                    return collection.Count;
                }

                if (result is System.Collections.IEnumerable enumerable)
                {
                    var count = 0;
                    foreach (var puck in enumerable)
                    {
                        if (puck != null)
                        {
                            count++;
                        }
                    }

                    return count;
                }
            }
            catch { }

            return 0;
        }

        public bool OnEnable()
        {
            try
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Enabling...");

                if (Application.isBatchMode)
                {
                    InitializeServer();
                }
                else
                {
                    InitializeClient();
                }

                Debug.Log($"[{Constants.MOD_NAME}] Enabled");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] failed to enable: {e}");
                return false;
            }
        }

        private static void InitializeServer()
        {
            Debug.Log($"[{Constants.MOD_NAME}] SERVER INIT");

            try
            {
                LoadMmr();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Failed to load MMR: {ex.Message}");
            }

            try
            {
                harmony.PatchAll();
                Server.RankedSystem.Initialize();
                EnsureUpdater();
                PublishServerStatusLifecycle("startup", waitForCompletion: false);
            }
            catch (Exception hex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Server initialization failed: {hex}");
                throw;
            }
        }

        private static void InitializeClient()
        {
            Debug.Log($"[{Constants.MOD_NAME}] CLIENT INIT");
            InitializeClientUI();
        }

        private static void InitializeClientUI()
        {
            if (Application.isBatchMode)
            {
                Debug.Log($"[{Constants.MOD_NAME}] Skipping UI: running on server (batch mode)");
                return;
            }

            DraftStateBridge.EnsureInitialized();
            Debug.Log($"[{Constants.MOD_NAME}] UI INIT");
            Debug.Log($"[{Constants.MOD_NAME}] CLIENT UI INITIALIZED");
        }

        public static void SpawnPuck()
        {
            try
            {
                Camera cam = Camera.main;
                Vector3 spawnPos = Vector3.zero;
                Vector3 forward = Vector3.forward;

                if (cam != null)
                {
                    spawnPos = cam.transform.position + cam.transform.forward * 2f;
                    forward = cam.transform.forward;
                }

                // If we're the server, ask the game's PuckManager to spawn a networked puck
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    try
                    {
                        // Try to get the PuckManager type and instance
                        Type pmType = null;
                        object pmInstance = null;

                        try { pmType = Type.GetType("PuckManager"); } catch { pmType = null; }
                        if (pmType == null)
                        {
                            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                pmType = asm.GetType("PuckManager") ?? asm.GetType("Puck.PuckManager");
                                if (pmType != null) break;
                            }
                        }

                            if (pmType != null)
                            {
                                // Try static Instance property first, otherwise find any object of that type
                                var prop = pmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                                if (prop != null)
                                    pmInstance = prop.GetValue(null);

                                if (pmInstance == null)
                                    pmInstance = FindFirstObjectOfType(pmType);

                                if (pmInstance != null)
                                {
                                    var method = pmType.GetMethod("Server_SpawnPuck", BindingFlags.Public | BindingFlags.Instance);
                                    if (method != null)
                                    {
                                        if (Server.RankedSystem.IsMatchActive())
                                        {
                                            Debug.Log($"[{Constants.MOD_NAME}] Manual spawn aborted because a match is already active.");
                                            return;
                                        }
                                        // signature: Puck Server_SpawnPuck(Vector3, Quaternion, Vector3, Boolean)
                                        var rot = Quaternion.LookRotation(forward);
                                        using (Server.RankedSystem.BeginManualSpawn())
                                        {
                                            method.Invoke(pmInstance, new object[] { spawnPos, rot, forward * 5f, false });
                                        }
                                        Debug.Log($"[{Constants.MOD_NAME}] Requested server spawn puck at {spawnPos}");
                                        return;
                                    }
                                }
                        }

                        Debug.LogError($"[{Constants.MOD_NAME}] PuckManager not found or spawn method missing");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[{Constants.MOD_NAME}] Server spawn failed: {ex.Message}");
                    }
                }
                // Fallback: spawn locally for testing when not server
                GameObject puck = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puck.name = "Puck";
                puck.transform.position = spawnPos;
                puck.transform.localScale = Vector3.one * 0.3f;

                var rb = puck.AddComponent<Rigidbody>();
                rb.mass = 0.17f;
                rb.linearVelocity = forward * 5f;
                rb.WakeUp();

                Debug.Log($"[{Constants.MOD_NAME}] Spawned local puck at {spawnPos}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] SpawnPuck failed: {e.Message}");
            }
        }

        public bool OnDisable()
        {
            try
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Disabling...");

                if (Application.isBatchMode)
                {
                    PublishServerStatusLifecycle("shutdown", waitForCompletion: true);
                }

                harmony.UnpatchSelf();
                try
                {
                    if (updaterGo != null)
                    {
                        UnityEngine.Object.Destroy(updaterGo);
                        updaterGo = null;
                        updaterInstance = null;
                    }
                }
                catch { }

                try { DraftStateBridge.Shutdown(); } catch { }

                Debug.Log($"[{Constants.MOD_NAME}] Disabled");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] failed to disable: {e.Message}");
                return false;
            }
        }

        private static void PublishServerStatusLifecycle(string trigger, bool waitForCompletion)
        {
            if (!Application.isBatchMode)
            {
                return;
            }

            try
            {
                Debug.Log($"[{Constants.MOD_NAME}] [DISCORD] Auto-publishing server status on {trigger}.");
                var publishTask = Server.RankedSystem.PublishServerStatusFromSpeedupAsync(debugMessage =>
                {
                    try
                    {
                        Debug.Log($"[{Constants.MOD_NAME}] [DISCORD] [{trigger}] {debugMessage}");
                    }
                    catch { }
                });

                if (waitForCompletion)
                {
                    publishTask.GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Auto server status publish failed on {trigger}: {ex.Message}");
            }
        }

        // Helper: find a live object of the given type without using the obsolete API.
        private static object FindFirstObjectOfType(Type t)
        {
            try
            {
                var objs = Resources.FindObjectsOfTypeAll(t);
                if (objs != null && objs.Length > 0)
                    return objs[0];
            }
            catch { }
            return null;
        }

        // Helper: get manager instance by checking static Instance property or finding in scene
        private static object GetManagerInstance(Type managerType)
        {
            try { return Server.ReflectionUtils.GetManagerInstance(managerType); } catch { return null; }
        }

        private static bool TryGetBladeSpawn(Component playerComp, out Vector3 spawnPos, out Quaternion rot, out Vector3 vel)
        {
            try { return Server.ReflectionUtils.TryGetBladeSpawn(playerComp, out spawnPos, out rot, out vel); } catch { spawnPos = Vector3.zero; rot = Quaternion.identity; vel = Vector3.zero; return false; }
        }

        private static void SendCommandsOverview(object player, ulong clientId)
        {
            SendCommandHelpLine(clientId, "<size=15><b>Server Commands</b></size>");
            SendCommandHelpLine(clientId, "<size=13>/s</size> <size=12>- Spawn a puck (server). Blocked during matches, goals and replays.</size>");
            SendCommandHelpLine(clientId, "<size=12><color=#9dc4de>General</color></size>");
            SendCommandHelpLine(clientId, "<size=13>/commands</size> <size=12>- Show this full command list.</size>");
            SendCommandHelpLine(clientId, "<size=13>/vr</size> <size=12>- Start a ranked ready vote.</size>");
            SendCommandHelpLine(clientId, "<size=13>/vs</size> <size=12>- Alias for /vr. Normal start is disabled here.</size>");
            SendCommandHelpLine(clientId, "<size=13>/votesinglegoalie</size> <size=12>- Start a shared-goalie vote if you are the only active goalie.</size>");
            SendCommandHelpLine(clientId, "<size=13>/y | /n</size> <size=12>- Vote yes or no in ready checks, shared-goalie votes and forfeit votes.</size>");
            SendCommandHelpLine(clientId, "<size=13>/ff</size> <size=12>- Start or vote on a forfeit for your team.</size>");
            SendCommandHelpLine(clientId, "<size=13>/mmr</size> <size=12>- Show your current MMR.</size>");
            SendCommandHelpLine(clientId, "<size=13>/discord</size> <size=12>- Open the Discord invite in your browser.</size>");
            SendCommandHelpLine(clientId, "<size=13>/link &lt;code&gt;</size> <size=12>- Finish Discord verification using the code generated in Discord.</size>");
            SendCommandHelpLine(clientId, "<size=13>/host</size> <size=12>- Open the dedicated SpeedHosting PUCK page in your browser.</size>");
            SendCommandHelpLine(clientId, "<size=13>/cs</size> <size=12>- Despawn all pucks on the map.</size>");


            

            if (!TryIsAdmin(player, clientId))
            {
                return;
            }

            SendCommandHelpLine(clientId, "<size=12><color=#ffcc66>Admin only</color></size>");
            SendCommandHelpLine(clientId, "<size=13>/ranked start</size> <size=12>- Force-start a ranked match with eligible players.</size>");
            SendCommandHelpLine(clientId, "<size=13>/ranked end [red|blue|draw]</size> <size=12>- Force-end the current ranked match.</size>");
            SendCommandHelpLine(clientId, "<size=13>/ranked test <on|off|status></size> <size=12>- Control synthetic-player test mode when enabled.</size>");
            SendCommandHelpLine(clientId, "<size=13>/dummygk <red|blue> <easy|normal|hard></size> <size=12>- Spawn or replace a real goalkeeper bot for the chosen team.</size>");
            SendCommandHelpLine(clientId, "<size=13>/ranked status publish</size> <size=12>- Fetch authoritative server activity from SpeedUP and publish the Discord status embed.</size>");
            SendCommandHelpLine(clientId, "<size=13>/fc <player|steamId|#number> <red|blue|spectator></size> <size=12>- Force a live player onto a team or back to spectator.</size>");
            SendCommandHelpLine(clientId, "<size=13>/addscore <amount> <red|blue></size> <size=12>- Adjust the real in-game score and trigger the native score phase.</size>");
            SendCommandHelpLine(clientId, "<size=13>/mute <player|steamId|#number> <duration> <reason...></size> <size=12>- Persist a backend mute and apply it immediately to live chat.</size>");
            SendCommandHelpLine(clientId, "<size=13>/tempban <player|steamId|#number> <duration> <reason...></size> <size=12>- Persist a backend temporary ban and immediately disconnect the live target.</size>");
            SendCommandHelpLine(clientId, "<size=13>/unmute <player|steamId|#number> [reason...]</size> <size=12>- Clear a backend mute and update live chat permission immediately.</size>");
            SendCommandHelpLine(clientId, "<size=13>/unban <player|steamId|#number> [reason...]</size> <size=12>- Clear a backend ban for a SteamID or live player.</size>");
            SendCommandHelpLine(clientId, "<size=13>/setnamecolor <player|steamId|#number> <color|rgb|#RRGGBB|reset></size> <size=12>- Persist a visible name color or RGB rainbow for a SteamID in UserData.</size>");
            SendCommandHelpLine(clientId, "<size=13>/setchatcolor <player|steamId|#number> <color|rgb|#RRGGBB|reset></size> <size=12>- Persist a chat body color or RGB rainbow for a SteamID in UserData.</size>");


            SendCommandHelpLine(clientId, "<size=12><color=#9dc4de>Draft / captains</color></size>");
            SendCommandHelpLine(clientId, "<size=13>/draft</size> <size=12>- Show the current draft status in chat.</size>");
            SendCommandHelpLine(clientId, "<size=13>/draftui</size> <size=12>- Explain the automatic ranked overlay and text fallback.</size>");
            SendCommandHelpLine(clientId, "<size=13>/pick <player></size> <size=12>- Captain fallback to draft a player.</size>");
            SendCommandHelpLine(clientId, "<size=13>/accept <player></size> <size=12>- Captain fallback to accept a late joiner.</size>");
            SendCommandHelpLine(clientId, "<size=13>/approve <requestId></size> <size=12>- Captain approval for late joins or team switches.</size>");
            SendCommandHelpLine(clientId, "<size=13>/reject <requestId></size> <size=12>- Captain rejection for late joins or team switches.</size>");

            SendCommandHelpLine(clientId, "<size=12><color=#9dc4de>Replay tools</color></size>");
            SendCommandHelpLine(clientId, "<size=13>/record start</size> <size=12>- Start recording your current input path.</size>");
            SendCommandHelpLine(clientId, "<size=13>/record stop</size> <size=12>- Stop recording and save it into BotMemory.</size>");
            SendCommandHelpLine(clientId, "<size=13>/replay</size> <size=12>- Start replay behavior mode using the latest library match.</size>");
            SendCommandHelpLine(clientId, "<size=13>/replay list</size> <size=12>- List saved BotMemory recordings and replay types.</size>");
            SendCommandHelpLine(clientId, "<size=13>/replay <name|type></size> <size=12>- Replay a specific recording or pattern type.</size>");

            
        }

        private static bool TryHandleBackendModerationCommand(object player, ulong clientId, string trimmed)
        {
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return false;
            }

            var isTempBan = trimmed.StartsWith("/tempban", StringComparison.OrdinalIgnoreCase);
            var isUnmute = !isTempBan && trimmed.StartsWith("/unmute", StringComparison.OrdinalIgnoreCase);
            var isUnban = !isTempBan && !isUnmute && trimmed.StartsWith("/unban", StringComparison.OrdinalIgnoreCase);
            var isMute = !isTempBan && !isUnmute && !isUnban && trimmed.StartsWith("/mute", StringComparison.OrdinalIgnoreCase);
            if (!isMute && !isTempBan && !isUnmute && !isUnban)
            {
                return false;
            }

            var commandLength = isTempBan ? 8 : (isUnmute ? 7 : (isUnban ? 6 : 5));
            if (trimmed.Length > commandLength && !char.IsWhiteSpace(trimmed[commandLength]))
            {
                return false;
            }

            if (!CanUseBackendModerationCommand(player, clientId))
            {
                SendCommandHelpLine(clientId, "<color=#ff6666>You must be an admin to use this command</color>");
                return true;
            }

            string errorMessage;
            var commandArgs = trimmed.Length > commandLength ? trimmed.Substring(commandLength).Trim() : string.Empty;
            var started = isTempBan
                ? Server.RankedSystem.TryStartBackendTempBan(player, clientId, commandArgs, out errorMessage)
                : isUnmute
                    ? Server.RankedSystem.TryStartBackendUnmute(player, clientId, commandArgs, out errorMessage)
                    : isUnban
                        ? Server.RankedSystem.TryStartBackendUnban(player, clientId, commandArgs, out errorMessage)
                        : Server.RankedSystem.TryStartBackendMute(player, clientId, commandArgs, out errorMessage);
            if (!started)
            {
                SendSystemChatToClient($"<size=14><color=#ff6666>{errorMessage}</color></size>", clientId);
            }

            return true;
        }

        private static bool CanUseBackendModerationCommand(object player, ulong clientId)
        {
            if (TryIsAdmin(player, clientId))
            {
                return true;
            }

            try
            {
                return string.Equals(TryGetPlayerId(player, clientId), OwnerSteamId, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryHandleStyledPlayerChat(UIChat chat, Player player, string message, ulong clientId, bool useTeamChat, bool isMuted)
        {
            if (chat == null || player == null)
            {
                return false;
            }

            var messageColorSpec = Server.RankedSystem.GetStoredMessageColorHexForPlayer(player, clientId);
            if (string.IsNullOrWhiteSpace(messageColorSpec))
            {
                return false;
            }

            if (isMuted)
            {
                return true;
            }

            if (!TryConsumeChatRateLimit(chat, player, message, out var shouldSuppressMessage))
            {
                return shouldSuppressMessage;
            }

            var body = BuildStyledChatBodyForBroadcast(message, messageColorSpec);
            if (useTeamChat)
            {
                var recipients = new List<Player>();
                if (player.Team.Value == PlayerTeam.None || player.Team.Value == PlayerTeam.Spectator)
                {
                    recipients.AddRange(NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayersByTeam(PlayerTeam.None));
                    recipients.AddRange(NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayersByTeam(PlayerTeam.Spectator));
                }
                else
                {
                    recipients.AddRange(NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayersByTeam(player.Team.Value));
                }

                var clientIds = recipients.Select(candidate => candidate.OwnerClientId).ToArray();
                chat.Server_ChatMessageRpc($"[TEAM] {chat.WrapPlayerUsername(player)}: {body}", chat.RpcTarget.Group(clientIds, RpcTargetUse.Temp));
                return true;
            }

            chat.Server_ChatMessageRpc($"{chat.WrapPlayerUsername(player)}: {body}", chat.RpcTarget.ClientsAndHost);
            return true;
        }

        private static bool TryConsumeChatRateLimit(UIChat chat, Player player, string message, out bool shouldSuppressMessage)
        {
            shouldSuppressMessage = false;
            try
            {
                var rateLimitsField = typeof(UIChat).GetField("playerRateLimits", BindingFlags.Instance | BindingFlags.NonPublic);
                var rateLimitField = typeof(UIChat).GetField("messageRateLimit", BindingFlags.Instance | BindingFlags.NonPublic);
                if (rateLimitsField == null || rateLimitField == null)
                {
                    return false;
                }

                if (!(rateLimitsField.GetValue(chat) is Dictionary<Player, float> playerRateLimits))
                {
                    return false;
                }

                var messageRateLimit = (float)rateLimitField.GetValue(chat);
                if (!playerRateLimits.ContainsKey(player))
                {
                    playerRateLimits.Add(player, 0f);
                }

                if (playerRateLimits[player] + 1f >= messageRateLimit)
                {
                    Debug.LogWarning($"[UIChat] {player.Username.Value} ({player.OwnerClientId}) [{player.SteamId.Value}] is rate limited. Ignoring message: {message}");
                    shouldSuppressMessage = true;
                    return false;
                }

                playerRateLimits[player] += 1f;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildStyledChatBodyForBroadcast(string message, string messageColorSpec)
        {
            if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(messageColorSpec))
            {
                return $"<noparse>{message}</noparse>";
            }

            if (Server.RankedSystem.IsRainbowColorSpec(messageColorSpec))
            {
                return BuildRainbowRichText(message);
            }

            return $"<color={messageColorSpec}><noparse>{message}</noparse></color>";
        }

        private static string BuildStyledNameLabel(string message, string colorSpec)
        {
            if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(colorSpec))
            {
                return $"<b><noparse>{message}</noparse></b>";
            }

            if (Server.RankedSystem.IsRainbowColorSpec(colorSpec))
            {
                return $"<b>{BuildRainbowRichText(message)}</b>";
            }

            return $"<b><color={colorSpec}><noparse>{message}</noparse></color></b>";
        }

        private static string BuildAdminNamePrefix()
        {
            return "<b><color=#00ff00>Admin</color></b> ";
        }

        private static string BuildOwnerNamePrefix()
        {
            return $"<b>{BuildRainbowRichText("Owner")}</b> ";
        }

        private static bool IsOwnerPlayer(Player player)
        {
            if (player == null)
            {
                return false;
            }

            try
            {
                return string.Equals(player.SteamId.Value.ToString(), OwnerSteamId, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static string BuildRainbowRichText(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(message.Length * 24);
            var visibleIndex = 0;
            for (var index = 0; index < message.Length; index++)
            {
                var current = message[index];
                if (char.IsWhiteSpace(current))
                {
                    builder.Append(current);
                    continue;
                }

                var hue = (visibleIndex % 24) / 24f;
                var rgb = Color.HSVToRGB(hue, 1f, 1f);
                var hex = $"#{ColorUtility.ToHtmlStringRGB(rgb)}";
                builder.Append("<color=");
                builder.Append(hex);
                builder.Append('>');
                builder.Append(EscapeRichTextChar(current));
                builder.Append("</color>");
                visibleIndex++;
            }

            return builder.ToString();
        }

        private static string EscapeRichTextChar(char value)
        {
            switch (value)
            {
                case '<':
                    return "&lt;";
                case '>':
                    return "&gt;";
                case '&':
                    return "&amp;";
                default:
                    return value.ToString();
            }
        }

        private static void SendCommandHelpLine(ulong clientId, string message)
        {
            try { UIChat.Instance.Server_SendSystemChatMessage(message, clientId); } catch { }
        }

        private static void DebugToClient(ulong clientId, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            SendCommandHelpLine(clientId, $"<size=12><color=#9dc4de>[ranked status]</color> {message}</size>");
        }

        private static Type FindTypeByName(params string[] names)
        {
            try { return Server.ReflectionUtils.FindTypeByName(names); } catch { return null; }
        }
        
        private static string StripRichTextTags(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new System.Text.StringBuilder(input.Length);
            bool inside = false;
            foreach (var c in input)
            {
                if (c == '<') { inside = true; continue; }
                if (c == '>') { inside = false; continue; }
                if (!inside) sb.Append(c);
            }
            return sb.ToString();
        }

        private static void EnsureUpdater()
        {
            if (updaterInstance != null) return;
            try
            {
                updaterGo = new GameObject($"{Constants.MOD_NAME}.Updater");
                UnityEngine.Object.DontDestroyOnLoad(updaterGo);
                updaterInstance = updaterGo.AddComponent<RankedSystemUpdater>();
            }
            catch { }
        }

        private class RankedSystemUpdater : MonoBehaviour
        {
            private void Update()
            {
                try { Server.RankedSystem.Update(); } catch { }
            }
        }

        // Delegates to the server-side RankedSystem implementation
        private static void LoadMmr() => Server.RankedSystem.LoadMmr();
        private static void SaveMmr() => Server.RankedSystem.SaveMmr();
        private static void UpdateRankedVote() => Server.RankedSystem.Update();
        private static void HandleRankedVoteStart(object player, ulong clientId) => Server.RankedSystem.HandleRankedVoteStart(player, clientId);
        private static void HandleRankedVoteResponse(object player, ulong clientId, bool accept) => Server.RankedSystem.HandleRankedVoteResponse(player, clientId, accept);
        private static void FinalizeRankedVote() { /* handled by RankedSystem */ }
        private static bool TryHandleDraftCommand(object player, ulong clientId, string message) => Server.RankedSystem.TryHandleDraftCommand(player, clientId, message);
        private static bool TryGetEligiblePlayers(out List<RankedParticipant> eligible, out string reason) => Server.RankedSystem.TryGetEligiblePlayers(out eligible, out reason);
        private static bool TryGetEligiblePlayersForStart(object player, ulong clientId, out List<RankedParticipant> eligible, out string reason) => Server.RankedSystem.TryGetEligiblePlayersForStartPublic(player, clientId, out eligible, out reason);
        private static string TryGetPlayerId(object player, ulong fallbackClientId) => Server.RankedSystem.TryGetPlayerId(player, fallbackClientId);
        private static string TryGetPlayerName(object player) => Server.RankedSystem.TryGetPlayerName(player);
        private static int GetMmr(string playerId) => Server.RankedSystem.GetMmr(playerId);
        private static bool TryIsAdmin(object player, ulong clientId) => Server.RankedSystem.TryIsAdminPublic(player, clientId);
        private static void SendSystemChatToAll(string message) => Server.RankedSystem.SendSystemChatToAll(message);
        private static void SendSystemChatToClient(string message, ulong clientId) => Server.RankedSystem.SendSystemChatToClient(message, clientId);
        private static void TryStartMatch() => Server.RankedSystem.TryStartMatch();
        private static void ApplyRankedResults(TeamResult winner) => Server.RankedSystem.ApplyRankedResults(winner);
        private static bool EndRankedMatch(TeamResult winner, bool requestRuntimeEnd, bool forceRequestedWinner) => Server.RankedSystem.EndMatch(winner, requestRuntimeEnd, forceRequestedWinner);
        private static bool IsControlledTestModeEnabled() => Server.RankedSystem.IsControlledTestModeEnabled();
        private static void SetControlledTestModeEnabled(bool enabled) => Server.RankedSystem.SetControlledTestModeEnabled(enabled);

        // Diagnostic Harmony patch: intercept NetworkVariableBase.CanSend to detect null NetworkBehaviour and avoid crashing the network tick
        [HarmonyPatch]
        public class NetworkVariableCanSendPatch
        {
            static System.Reflection.MethodBase TargetMethod()
            {
                var t = FindTypeByName("Unity.Netcode.NetworkVariableBase", "Unity.Netcode.NetworkVariableBase");
                if (t == null) return null;
                return t.GetMethod("CanSend", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            }

            [HarmonyPrefix]
            public static bool Prefix(object __instance, ref bool __result)
            {
                try
                {
                    if (__instance == null) return true;

                    var nb = __instance;
                    var baseType = nb.GetType();

                    var field = typeof(NetworkVariableBase).GetField("m_NetworkBehaviour", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field == null)
                    {
                        // field not found on this runtime version � don't interfere
                        return true;
                    }

                    var val = field.GetValue(nb);
                    if (val == null)
                    {
                        try
                        {
                            Debug.LogError($"[{Constants.MOD_NAME}] NetworkVariable detected with null m_NetworkBehaviour. Variable type: {baseType.FullName}. Stack:\n{Environment.StackTrace}");
                        }
                        catch { }
                        // prevent original CanSend from running (which would throw); indicate cannot send
                        __result = false;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    try { Debug.LogError($"[{Constants.MOD_NAME}] NetworkVariableCanSendPatch error: {ex}"); } catch { }
                }
                return true;
            }
        }
    }
}
