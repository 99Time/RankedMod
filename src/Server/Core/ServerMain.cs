using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using Unity.Netcode;
using Newtonsoft.Json;
using System.Linq;
using System.Globalization;

namespace schrader
{
    public class CustomMOTD : IPuckMod
    {
        static readonly Harmony harmony = new Harmony(Constants.MOD_NAME);
        private const string OwnerSteamId = "76561199046098825";
        private static GameObject updaterGo;
        private static RankedSystemUpdater updaterInstance;
        private static bool serverInstanceRankedActive = true;
        private const float ManualPuckSpawnCooldownSeconds = 4.0f;
        private const int ManualPuckSpawnHardCap = 30;
        private static readonly int TrainingOpenWorldIceLayer = LayerMask.NameToLayer("Ice");
        private static readonly Vector3 TrainingOpenWorldAnchorPosition = Vector3.right * 200f;
        private static readonly Vector3 TrainingOpenWorldFallbackSpawnPosition = TrainingOpenWorldAnchorPosition + Vector3.up * 5f;
        private static readonly Quaternion TrainingOpenWorldSpawnRotation = Quaternion.Euler(0f, 180f, 0f);
        private static readonly Vector3 TrainingOpenWorldFloorSize = new Vector3(128f, 2f, 128f);
        private const float TrainingDebugTargetMatchThreshold = 0.5f;
        private const float TrainingDebugPostMoveShiftThreshold = 0.25f;
        private const float TrainingDebugLateShiftThreshold = 0.5f;
        private static readonly Dictionary<string, float> manualPuckSpawnTimes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<ulong, TrainingOpenWorldState> trainingOpenWorldStateByClient = new Dictionary<ulong, TrainingOpenWorldState>();
        private static GameObject trainingOpenWorldFloorObject;
        private static readonly FieldInfo HoverRaycastOffsetField = typeof(Hover).GetField("raycastOffset", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo HoverRaycastDistanceField = typeof(Hover).GetField("raycastDistance", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo HoverRaycastLayerMaskField = typeof(Hover).GetField("raycastLayerMask", BindingFlags.Instance | BindingFlags.NonPublic);

        private sealed class TrainingOpenWorldState
        {
            public Vector3 ReturnPosition;
            public Quaternion ReturnRotation;
            public bool IsActive;
            public Vector3 OpenWorldPosition;
            public Quaternion OpenWorldRotation;
            public bool LastRespawnRedirectApplied;
            public bool LastTeleportApplied;
            public Vector3 LastRedirectPosition;
            public Quaternion LastRedirectRotation;
        }

        private sealed class TrainingFloorSnapshot
        {
            public bool Exists;
            public string Name;
            public Vector3 Position;
            public string ColliderType;
            public bool HasCollider;
            public bool IsTrigger;
            public Vector3 ColliderBoundsSize;
            public float ColliderTopY;
            public float ColliderBottomY;
            public int Layer;
        }

        private sealed class TrainingBodySnapshot
        {
            public ulong ClientId;
            public string PlayerName;
            public string BodyComponentName;
            public bool HasBody;
            public bool HasRigidbody;
            public Vector3 TransformPosition;
            public Quaternion TransformRotation;
            public Vector3 RigidbodyPosition;
            public Quaternion RigidbodyRotation;
            public bool HasHover;
            public bool IsGrounded;
            public float HoverTargetDistance;
            public Vector3 HoverRaycastOffset;
            public Vector3 HoverRayOrigin;
            public float HoverRaycastDistance;
            public int HoverRaycastLayerMask;
            public bool HoverRaycastHit;
            public string HoverRaycastHitObjectName;
            public int HoverRaycastHitLayer;
            public float HoverRaycastHitDistance;
        }

        [HarmonyPatch(typeof(UIChatController), "Event_Server_OnSynchronizeComplete")]
        public class UIChatControllerPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(Dictionary<string, object> message)
            {
                ulong clientId = (ulong)message["clientId"];

                Debug.Log($"[{Constants.MOD_NAME}] [JOIN][SERVER] Synchronize complete received for client {clientId}.");
                ClearTrainingOpenWorldState(clientId, destroySharedFloorWhenUnused: true);
                try
                {
                    UIChat.Instance.Server_SendSystemChatMessage(
                        $"<size=18><b><color=#00ff00>Welcome to SpeedRankeds!</color></b></size>\n"
                        + $"<size=13><color=#d8f2e6>Use <b>/commands</b> to display available server chat commands.</color></size>\n"
                        + $"<size=13><b><color=#78d8ff>Host your own PUCK server</color></b></size>\n"
                        + $"<size=13><color=#ffffff>{Constants.BuildPuckLandingUrl(Constants.HOST_SOURCE_CHAT)}</color> <color=#9fc4db>(or use <b>/host</b>)</color></size>",
                        clientId);
                }
                catch { }
                try { Server.RankedSystem.HandleBackendPlayerSynchronized(clientId); } catch { }
                try { RankedOverlayNetwork.ResyncClient(clientId); } catch { }
                return false;
            }
        }

        [HarmonyPatch(typeof(Player), "OnNetworkDespawn")]
        public class PlayerOnNetworkDespawnPatch
        {
            [HarmonyPrefix]
            public static void Prefix(Player __instance)
            {
                if (__instance == null)
                {
                    return;
                }

                ClearTrainingOpenWorldState(__instance.OwnerClientId, destroySharedFloorWhenUnused: true);
            }
        }

        [HarmonyPatch(typeof(Player), "Server_RespawnCharacter")]
        public class PlayerServerRespawnCharacterPatch
        {
            [HarmonyPrefix]
            public static void Prefix(Player __instance, ref Vector3 position, ref Quaternion rotation)
            {
                if (!Server.RankedSystem.IsTrainingServerModeActive() || __instance == null || !NetworkManager.Singleton.IsServer)
                {
                    return;
                }

                if (!trainingOpenWorldStateByClient.TryGetValue(__instance.OwnerClientId, out var openWorldState) || openWorldState == null || !openWorldState.IsActive)
                {
                    return;
                }

                openWorldState.LastRespawnRedirectApplied = true;
                openWorldState.LastRedirectPosition = openWorldState.OpenWorldPosition;
                openWorldState.LastRedirectRotation = openWorldState.OpenWorldRotation;
                position = openWorldState.OpenWorldPosition;
                rotation = openWorldState.OpenWorldRotation;
            }

            [HarmonyPostfix]
            public static void Postfix(Player __instance)
            {
                if (!Server.RankedSystem.IsTrainingServerModeActive() || __instance == null || !NetworkManager.Singleton.IsServer)
                {
                    return;
                }

                if (!trainingOpenWorldStateByClient.TryGetValue(__instance.OwnerClientId, out var openWorldState) || openWorldState == null || !openWorldState.IsActive)
                {
                    return;
                }

                openWorldState.LastTeleportApplied = true;
                var playerBody = __instance.PlayerBody;
                if (playerBody == null)
                {
                    return;
                }

                playerBody.Server_Teleport(openWorldState.OpenWorldPosition, openWorldState.OpenWorldRotation);
            }
        }

        [HarmonyPatch(typeof(PlayerBodyV2), "FixedUpdate")]
        public class PlayerBodyV2FixedUpdatePatch
        {
            private const float TrainingOpenWorldManualTeleportDistanceThreshold = 0.05f;
            private const float TrainingOpenWorldManualTeleportRotationThreshold = 0.5f;

            [HarmonyPrefix]
            public static void Prefix(PlayerBodyV2 __instance)
            {
                if (!Server.RankedSystem.IsTrainingServerModeActive() || __instance == null || !NetworkManager.Singleton.IsServer)
                {
                    return;
                }

                var player = __instance.Player;
                if (player == null)
                {
                    return;
                }

                if (!trainingOpenWorldStateByClient.TryGetValue(player.OwnerClientId, out var openWorldState) || openWorldState == null || !openWorldState.IsActive)
                {
                    return;
                }

                if (__instance.Rigidbody == null)
                {
                    return;
                }

                var transformPosition = __instance.transform.position;
                var rigidbodyPosition = __instance.Rigidbody.position;
                var transformRotation = __instance.transform.rotation;
                var rigidbodyRotation = __instance.Rigidbody.rotation;

                if (Vector3.Distance(transformPosition, rigidbodyPosition) < TrainingOpenWorldManualTeleportDistanceThreshold
                    && Quaternion.Angle(transformRotation, rigidbodyRotation) < TrainingOpenWorldManualTeleportRotationThreshold)
                {
                    return;
                }

                __instance.Server_Teleport(transformPosition, transformRotation);
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
                            UIChat.Instance.Server_SendSystemChatMessage(Server.ChatStyle.Message(Server.ChatStyle.RankedModule, "Normal start is disabled here. Redirecting to the ranked ready vote.", Server.ChatTone.Warning, 13), clientId);
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
                            UIChat.Instance.Server_SendSystemChatMessage(Server.ChatStyle.Message(Server.ChatStyle.RankedModule, $"Your current MMR is {Server.ChatStyle.Emphasis(mmr.ToString())}.", Server.ChatTone.Info), clientId);
                        }
                        catch { }
                        return false;
                    }

                    if (trimmed.Equals("/discord", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            UIChat.Instance.Server_SendSystemChatMessage(Server.ChatStyle.Message(Server.ChatStyle.DiscordModule, "Opening the Discord invite in your browser.", Server.ChatTone.Info), clientId);
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
                                SendSystemChatToClient(Server.ChatStyle.Usage("/link CODE"), clientId);
                            return false;
                        }

                        Server.RankedSystem.StartDiscordLinkComplete(clientId, linkCode);
                        return false;
                    }

                    if (trimmed.Equals("/host", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            UIChat.Instance.Server_SendSystemChatMessage(Server.ChatStyle.Message(Server.ChatStyle.DiscordModule, "Opening the hosting page in your browser.", Server.ChatTone.Info), clientId);
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

                    if (Server.RankedSystem.IsTrainingServerModeActive()
                        && (trimmed.Equals("/openworld", StringComparison.OrdinalIgnoreCase)
                            || trimmed.Equals("/return", StringComparison.OrdinalIgnoreCase)))
                    {
                        HandleTrainingOpenWorldCommand(player, clientId, trimmed);
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
                            SendCommandHelpLine(clientId, Server.ChatStyle.Message(Server.ChatStyle.AdminModule, "You must be an admin to use this command.", Server.ChatTone.Error, 13));
                            return false;
                        }

                            var commandArgs = trimmed.Length > 3 ? trimmed.Substring(3).Trim() : string.Empty;
                            var separatorIndex = commandArgs.LastIndexOf(' ');
                            if (separatorIndex <= 0)
                        {
                                SendSystemChatToClient(Server.ChatStyle.Usage("/fc <player|steamId|#number> <red|blue|spectator>"), clientId);
                            return false;
                        }

                            var playerTarget = commandArgs.Substring(0, separatorIndex).Trim();
                            var requestedTeam = commandArgs.Substring(separatorIndex + 1).Trim();

                            if (!Server.RankedSystem.TryForcePlayerTeamByTarget(playerTarget, requestedTeam, out var forceTeamMessage))
                        {
                                SendSystemChatToClient(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, forceTeamMessage, Server.ChatTone.Error), clientId);
                            return false;
                        }

                            SendSystemChatToAll(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, forceTeamMessage, Server.ChatTone.Warning));
                        return false;
                    }

                    if (trimmed.StartsWith("/addscore", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryIsAdmin(player, clientId))
                        {
                            SendCommandHelpLine(clientId, Server.ChatStyle.Message(Server.ChatStyle.AdminModule, "You must be an admin to use this command.", Server.ChatTone.Error, 13));
                            return false;
                        }

                        var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length != 3 || !int.TryParse(parts[1], out var amount))
                        {
                            SendSystemChatToClient(Server.ChatStyle.Usage("/addscore <amount> <red|blue>"), clientId);
                            return false;
                        }

                        if (!Server.RankedSystem.TryAddScore(parts[2], amount, out var redScore, out var blueScore, out var addScoreError))
                        {
                            SendSystemChatToClient(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, addScoreError, Server.ChatTone.Error), clientId);
                            return false;
                        }

                        SendSystemChatToAll(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, $"Adjusted the live score to {Server.ChatStyle.Team(TeamResult.Red)} {Server.ChatStyle.Emphasis(redScore.ToString())} - {Server.ChatStyle.Team(TeamResult.Blue)} {Server.ChatStyle.Emphasis(blueScore.ToString())}.", Server.ChatTone.Warning));
                        return false;
                    }

                    if (trimmed.StartsWith("/setnamecolor", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryIsAdmin(player, clientId))
                        {
                            SendCommandHelpLine(clientId, Server.ChatStyle.Message(Server.ChatStyle.AdminModule, "You must be an admin to use this command.", Server.ChatTone.Error, 13));
                            return false;
                        }

                        var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 3)
                        {
                            SendSystemChatToClient(Server.ChatStyle.Usage("/setnamecolor <player|steamId|#number> <color|rgb|#RRGGBB|reset>"), clientId);
                            return false;
                        }

                        var requestedColor = parts[parts.Length - 1];
                        var rawTarget = string.Join(" ", parts.Skip(1).Take(parts.Length - 2));
                        if (!Server.RankedSystem.TrySetNameColorForCommand(rawTarget, requestedColor, out var resultMessage))
                        {
                            SendSystemChatToClient(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, resultMessage, Server.ChatTone.Error), clientId);
                            return false;
                        }

                        SendSystemChatToAll(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, resultMessage, Server.ChatTone.Warning));
                        return false;
                    }

                    if (trimmed.StartsWith("/setchatcolor", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryIsAdmin(player, clientId))
                        {
                            SendCommandHelpLine(clientId, Server.ChatStyle.Message(Server.ChatStyle.AdminModule, "You must be an admin to use this command.", Server.ChatTone.Error, 13));
                            return false;
                        }

                        var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 3)
                        {
                            SendSystemChatToClient(Server.ChatStyle.Usage("/setchatcolor <player|steamId|#number> <color|rgb|#RRGGBB|reset>"), clientId);
                            return false;
                        }

                        var requestedColor = parts[parts.Length - 1];
                        var rawTarget = string.Join(" ", parts.Skip(1).Take(parts.Length - 2));
                        if (!Server.RankedSystem.TrySetMessageColorForCommand(rawTarget, requestedColor, out var resultMessage))
                        {
                            SendSystemChatToClient(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, resultMessage, Server.ChatTone.Error), clientId);
                            return false;
                        }

                        SendSystemChatToAll(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, resultMessage, Server.ChatTone.Warning));
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
                                        SendCommandHelpLine(clientId, Server.ChatStyle.Message(Server.ChatStyle.AdminModule, "You must be an admin to use this command.", Server.ChatTone.Error, 13));
                                        return false;
                                    }

                                    if (Server.RankedSystem.IsTrainingServerModeActive())
                                    {
                                        SendSystemChatToClient(Server.ChatStyle.Message(Server.ChatStyle.RankedModule, "Ranked cannot be force-started while serverMode=training.", Server.ChatTone.Warning), clientId);
                                        return false;
                                    }

                                    if (!TryGetEligiblePlayersForStart(player, clientId, out var eligible, out var reason))
                                    {
                                        SendSystemChatToClient(Server.ChatStyle.Message(Server.ChatStyle.RankedModule, $"Cannot start: {Server.ChatStyle.Safe(reason)}.", Server.ChatTone.Error), clientId);
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
                                                SendSystemChatToAll(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, $"Ranked was forcibly ended by admin. Winner: {Server.ChatStyle.Team(TeamResult.Red)}.", Server.ChatTone.Warning));
                                                return false;
                                            }
                                            else if (arg == "blue" || arg == "b")
                                            {
                                                EndRankedMatch(TeamResult.Blue, true, true);
                                                SendSystemChatToAll(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, $"Ranked was forcibly ended by admin. Winner: {Server.ChatStyle.Team(TeamResult.Blue)}.", Server.ChatTone.Warning));
                                                return false;
                                            }
                                            else if (arg == "draw")
                                            {
                                                EndRankedMatch(TeamResult.Unknown, true, true);
                                                SendSystemChatToAll(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, "Ranked was forcibly ended by admin as a draw. MMR is unchanged.", Server.ChatTone.Warning));
                                                return false;
                                            }
                                        }

                                        EndRankedMatch(TeamResult.Unknown, true, true);
                                        SendSystemChatToAll(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, "Ranked was forcibly ended by admin as a draw. MMR is unchanged.", Server.ChatTone.Warning));
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
                                        SendSystemChatToClient(Server.ChatStyle.Message(Server.ChatStyle.RankedModule, "Controlled test mode is disabled on this server.", Server.ChatTone.Error), clientId);
                                        return false;
                                    }

                                    var toggle = parts.Length >= 3 ? parts[2].ToLowerInvariant() : "status";
                                    if (toggle == "on")
                                    {
                                        SetControlledTestModeEnabled(true);
                                        SendSystemChatToAll(Server.ChatStyle.Message(Server.ChatStyle.RankedModule, "Controlled test mode enabled.", Server.ChatTone.Warning));
                                        return false;
                                    }

                                    if (toggle == "off")
                                    {
                                        SetControlledTestModeEnabled(false);
                                        SendSystemChatToAll(Server.ChatStyle.Message(Server.ChatStyle.RankedModule, "Controlled test mode disabled.", Server.ChatTone.Warning));
                                        return false;
                                    }

                                    var statusText = IsControlledTestModeEnabled() ? "enabled" : "disabled";
                                    SendSystemChatToClient(Server.ChatStyle.Message(Server.ChatStyle.RankedModule, $"Controlled test mode is currently {Server.ChatStyle.Emphasis(statusText)}.", Server.ChatTone.Info), clientId);
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

                                    SendSystemChatToClient(Server.ChatStyle.Usage("/ranked status publish"), clientId);
                                    return false;
                                }
                            }

                            SendSystemChatToClient(Server.ChatStyle.Usage("/ranked start | /ranked end [red|blue|draw] | /ranked test <on|off|status> | /ranked status publish"), clientId);
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
                                SendSystemChatToClient(Server.ChatStyle.Usage("/link CODE"), clientId);
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
                try { UIChat.Instance.Server_SendSystemChatMessage(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, "All pucks were despawned.", Server.ChatTone.Success), clientId); } catch { }
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
                    try { UIChat.Instance.Server_SendSystemChatMessage(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, "Cannot spawn pucks during an active match.", Server.ChatTone.Error), clientId); } catch { }
                    return;
                }

                if (!TryValidateManualPuckSpawn(requesterKey, out var livePuckCount, out var waitSeconds, out var denialMessage))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [PUCK-SPAWN] Denied spawn for {requesterKey}. livePucks={livePuckCount} wait={waitSeconds:0.00}s");
                    if (!string.IsNullOrWhiteSpace(denialMessage))
                    {
                        try { UIChat.Instance.Server_SendSystemChatMessage(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, denialMessage, Server.ChatTone.Warning), clientId); } catch { }
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
                try { UIChat.Instance.Server_SendSystemChatMessage(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, $"{Server.ChatStyle.Player(requesterName)} spawned a puck.", Server.ChatTone.Success), clientId); } catch { }
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

            serverInstanceRankedActive = Server.RankedServerInstanceActivation.ShouldEnableForCurrentServerInstance();
            if (!serverInstanceRankedActive)
            {
                Debug.Log($"[{Constants.MOD_NAME}] SERVER INIT skipped. RankedMod inactive for this dedicated server instance.");
                return;
            }

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

                if (Application.isBatchMode && serverInstanceRankedActive)
                {
                    PublishServerStatusLifecycle("shutdown", waitForCompletion: true);
                }

                harmony.UnpatchSelf();
                try { ClearAllTrainingOpenWorldState(); } catch { }
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
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/s", "Spawn a puck on the server. Blocked during matches, goals and replays."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpHeading("General"));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/commands", "Show this full command list."));
            if (Server.RankedSystem.IsTrainingServerModeActive())
            {
                SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/vr", "Disabled in training mode."));
                SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/vs", "Disabled in training mode."));
            }
            else
            {
                SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/vr", "Start a ranked ready vote."));
                SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/vs", "Alias for /vr. Normal start is disabled here."));
            }
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/votesinglegoalie", "Start a shared-goalie vote if you are the only active goalie."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/y or /n", "Vote yes or no in ready checks, shared-goalie votes and forfeit votes."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/ff", "Start or vote on a forfeit for your team."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/mmr", "Show your current MMR."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/discord", "Open the Discord invite in your browser."));
                SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/link CODE", "Finish Discord verification using the code generated in Discord."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/host", "Open the dedicated SpeedHosting PUCK page in your browser."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/cs", "Despawn all pucks on the map."));

            if (Server.RankedSystem.IsTrainingServerModeActive())
            {
                SendCommandHelpLine(clientId, Server.ChatStyle.HelpHeading("Training", "#78d8ff"));
                SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/openworld", "Respawn into the dedicated training anchor. The server owns the move, floor and grounded validation."));
                SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/return", "Respawn back to the saved authoritative return pose and leave training open world."));
            }


            

            if (!TryIsAdmin(player, clientId))
            {
                return;
            }

            SendCommandHelpLine(clientId, Server.ChatStyle.HelpHeading("Admin Only", "#ffcc66"));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/ranked start", "Force-start a ranked match with eligible players."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/ranked end [red|blue|draw]", "Force-end the current ranked match."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/ranked test <on|off|status>", "Control synthetic-player test mode when enabled."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/dummygk <red|blue> <easy|normal|hard>", "Spawn or replace a real goalkeeper bot for the chosen team."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/ranked status publish", "Fetch authoritative server activity from SpeedUP and publish the Discord status embed."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/fc <player|steamId|#number> <red|blue|spectator>", "Force a live player onto a team or back to spectator."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/addscore <amount> <red|blue>", "Adjust the real in-game score and trigger the native score phase."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/mute <player|steamId|#number> <duration> <reason...>", "Persist a backend mute and apply it immediately to live chat."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/tempban <player|steamId|#number> <duration> <reason...>", "Persist a backend temporary ban and immediately disconnect the live target."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/unmute <player|steamId|#number> [reason...]", "Clear a backend mute and update live chat permission immediately."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/unban <player|steamId|#number> [reason...]", "Clear a backend ban for a SteamID or live player."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/setnamecolor <player|steamId|#number> <color|rgb|#RRGGBB|reset>", "Persist a visible name color or RGB rainbow for a SteamID in UserData."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/setchatcolor <player|steamId|#number> <color|rgb|#RRGGBB|reset>", "Persist a chat body color or RGB rainbow for a SteamID in UserData."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpHeading("Draft / Captains"));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/draft", "Show the current draft status in chat."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/draftui", "Explain the automatic ranked overlay and text fallback."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/pick <player>", "Captain fallback to draft a player."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/accept <player>", "Captain fallback to accept a late joiner."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/approve <requestId>", "Captain approval for late joins or team switches."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/reject <requestId>", "Captain rejection for late joins or team switches."));

            SendCommandHelpLine(clientId, Server.ChatStyle.HelpHeading("Replay Tools"));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/record start", "Start recording your current input path."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/record stop", "Stop recording and save it into BotMemory."));
            SendCommandHelpLine(clientId, Server.ChatStyle.HelpCommand("/replay", "Start replay behavior mode using the latest library match."));
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

        private static void HandleTrainingOpenWorldCommand(object player, ulong clientId, string trimmed)
        {
            if (!Server.RankedSystem.IsTrainingServerModeActive())
            {
                SendSystemChatToClient(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, "Open-world commands are only available when serverMode=training.", Server.ChatTone.Warning), clientId);
                return;
            }

            if (string.Equals(trimmed, "/openworld", StringComparison.OrdinalIgnoreCase))
            {
                ActivateTrainingOpenWorld(clientId);
                return;
            }

            if (string.Equals(trimmed, "/return", StringComparison.OrdinalIgnoreCase))
            {
                ReturnFromTrainingOpenWorld(clientId);
                return;
            }

            SendSystemChatToClient(Server.ChatStyle.Usage("/openworld or /return"), clientId);
        }

        private static void ActivateTrainingOpenWorld(ulong clientId)
        {
            if (!TryGetTrainingPlayerForClient(clientId, out var player, out var playerBody))
            {
                SendSystemChatToClient(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, "Your player body is not ready yet. Try /openworld again in a moment.", Server.ChatTone.Warning), clientId);
                return;
            }

            if (trainingOpenWorldStateByClient.TryGetValue(clientId, out var existingState) && existingState != null && existingState.IsActive)
            {
                SendSystemChatToClient(Server.ChatStyle.Message(Server.ChatStyle.RankedModule, "You are already in the training open world. Use /return to go back.", Server.ChatTone.Info), clientId);
                return;
            }

            EnsureTrainingOpenWorldFloor();

            var openWorldPosition = ResolveTrainingOpenWorldSpawnPosition(playerBody);

            var beforeSnapshot = CaptureTrainingBodySnapshot(player, playerBody);
            var openWorldState = new TrainingOpenWorldState
            {
                ReturnPosition = playerBody.Rigidbody != null ? playerBody.Rigidbody.position : playerBody.transform.position,
                ReturnRotation = playerBody.Rigidbody != null ? playerBody.Rigidbody.rotation : playerBody.transform.rotation,
                OpenWorldPosition = openWorldPosition,
                OpenWorldRotation = TrainingOpenWorldSpawnRotation,
                IsActive = true
            };

            ResetTrainingOpenWorldDebugFlags(openWorldState);
            trainingOpenWorldStateByClient[clientId] = openWorldState;

            SendTrainingMovementTelemetryStart(
                clientId,
                "/openworld",
                beforeSnapshot,
                $"path=Server_RespawnCharacter request; redirectExpected=yes; teleportExpected=yes; anchor={FormatVector3(TrainingOpenWorldAnchorPosition)}",
                openWorldState.OpenWorldPosition,
                openWorldState.OpenWorldRotation);

            RankedOverlayNetwork.PublishTrainingOpenWorldPoseToClient(clientId, true, openWorldState.OpenWorldPosition, openWorldState.OpenWorldRotation, "openworld");

            ForceTrainingOpenWorldRespawn(player, openWorldState.OpenWorldPosition, openWorldState.OpenWorldRotation, "openworld-command");

            SendTrainingDebugLine(
                clientId,
                $"/openworld touched=Server_RespawnCharacter; redirectApplied={FormatBool(openWorldState.LastRespawnRedirectApplied)}; teleportApplied={FormatBool(openWorldState.LastTeleportApplied)}; redirectTarget={FormatVector3(openWorldState.LastRedirectPosition)} rot={FormatQuaternion(openWorldState.LastRedirectRotation)}");
            ScheduleTrainingMovementTelemetryPostAction(clientId, "/openworld", openWorldState.OpenWorldPosition, openWorldState.OpenWorldRotation);
            SendSystemChatToClient(Server.ChatStyle.Message(Server.ChatStyle.RankedModule, "Respawned into the training open world anchor.", Server.ChatTone.Success), clientId);
        }

        private static void ReturnFromTrainingOpenWorld(ulong clientId)
        {
            if (!trainingOpenWorldStateByClient.TryGetValue(clientId, out var state) || state == null || !state.IsActive)
            {
                SendSystemChatToClient(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, "You are not currently in the training open world.", Server.ChatTone.Warning), clientId);
                return;
            }

            if (!TryGetTrainingPlayerForClient(clientId, out var player, out _))
            {
                ClearTrainingOpenWorldState(clientId, destroySharedFloorWhenUnused: true);
                SendSystemChatToClient(Server.ChatStyle.Message(Server.ChatStyle.AdminModule, "Your player body is not ready yet. Try /return again in a moment.", Server.ChatTone.Warning), clientId);
                return;
            }

            var beforeSnapshot = CaptureTrainingBodySnapshot(player, player.PlayerBody);
            SendTrainingMovementTelemetryStart(
                clientId,
                "/return",
                beforeSnapshot,
                $"path=Server_RespawnCharacter direct; redirectExpected=no; teleportExpected=no; anchor=stored-return-snapshot",
                state.ReturnPosition,
                state.ReturnRotation);

            RankedOverlayNetwork.PublishTrainingOpenWorldPoseToClient(clientId, false, state.ReturnPosition, state.ReturnRotation, "return");

            state.IsActive = false;
            ResetTrainingOpenWorldDebugFlags(state);
            player.Server_RespawnCharacter(state.ReturnPosition, state.ReturnRotation, ResolveTrainingOpenWorldRole(player, player.PlayerPosition));
            SendTrainingDebugLine(clientId, "/return touched=Server_RespawnCharacter; redirectApplied=no; teleportApplied=no");
            ScheduleTrainingMovementTelemetryPostAction(clientId, "/return", state.ReturnPosition, state.ReturnRotation);
            ClearTrainingOpenWorldState(clientId, destroySharedFloorWhenUnused: true);
            SendSystemChatToClient(Server.ChatStyle.Message(Server.ChatStyle.RankedModule, "Returned from the training open world.", Server.ChatTone.Success), clientId);
        }

        private static void EnsureTrainingOpenWorldFloor()
        {
            if (trainingOpenWorldFloorObject != null)
            {
                return;
            }

            var floorObject = new GameObject($"{Constants.MOD_NAME}.TrainingOpenWorldFloor");
            floorObject.transform.position = new Vector3(
                TrainingOpenWorldAnchorPosition.x,
                -(TrainingOpenWorldFloorSize.y * 0.5f),
                TrainingOpenWorldAnchorPosition.z);

            var collider = floorObject.AddComponent<BoxCollider>();
            collider.size = TrainingOpenWorldFloorSize;
            collider.isTrigger = false;

            if (TrainingOpenWorldIceLayer >= 0)
            {
                floorObject.layer = TrainingOpenWorldIceLayer;
            }

            trainingOpenWorldFloorObject = floorObject;
        }

        private static void ClearTrainingOpenWorldState(ulong clientId, bool destroySharedFloorWhenUnused)
        {
            trainingOpenWorldStateByClient.Remove(clientId);

            if (!destroySharedFloorWhenUnused || trainingOpenWorldStateByClient.Count > 0)
            {
                return;
            }

            if (trainingOpenWorldFloorObject != null)
            {
                UnityEngine.Object.Destroy(trainingOpenWorldFloorObject);
                trainingOpenWorldFloorObject = null;
            }
        }

        private static Vector3 ResolveTrainingOpenWorldSpawnPosition(PlayerBodyV2 playerBody)
        {
            var floorSnapshot = CaptureTrainingFloorSnapshot();
            if (floorSnapshot == null || !floorSnapshot.Exists || !floorSnapshot.HasCollider)
            {
                return TrainingOpenWorldFallbackSpawnPosition;
            }

            var targetY = TrainingOpenWorldFallbackSpawnPosition.y;
            if (playerBody != null && TryGetHoverRaycastDebug(playerBody, out var raycastOffset, out var raycastDistance, out _, out var hoverTargetDistance, out _, out _, out _))
            {
                var preferredGroundDistance = hoverTargetDistance > 0f
                    ? Mathf.Min(hoverTargetDistance, Mathf.Max(0.05f, raycastDistance - 0.05f))
                    : 1f;
                targetY = floorSnapshot.ColliderTopY + preferredGroundDistance - raycastOffset.y;
                targetY = Mathf.Max(floorSnapshot.ColliderTopY + 0.01f, targetY);
            }

            return new Vector3(TrainingOpenWorldAnchorPosition.x, targetY, TrainingOpenWorldAnchorPosition.z);
        }

        private static void ForceTrainingOpenWorldRespawn(Player player, Vector3 targetPosition, Quaternion targetRotation, string reason)
        {
            if (player == null)
            {
                return;
            }

            try
            {
                player.Server_RespawnCharacter(targetPosition, targetRotation, ResolveTrainingOpenWorldRole(player, player.PlayerPosition));
                Debug.Log($"[{Constants.MOD_NAME}] [TRAINING][OPENWORLD] Forced respawn to {targetPosition} for client {player.OwnerClientId}. reason={reason}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] [TRAINING][OPENWORLD] Failed to force respawn for client {player.OwnerClientId}. reason={reason} error={ex.Message}");
            }
        }

        private static void ResetTrainingOpenWorldDebugFlags(TrainingOpenWorldState state)
        {
            if (state == null)
            {
                return;
            }

            state.LastRespawnRedirectApplied = false;
            state.LastTeleportApplied = false;
            state.LastRedirectPosition = Vector3.zero;
            state.LastRedirectRotation = Quaternion.identity;
        }

        private static void SendTrainingMovementTelemetryStart(ulong clientId, string actionName, TrainingBodySnapshot snapshot, string pathSummary, Vector3 targetPosition, Quaternion targetRotation)
        {
            if (!Server.RankedSystem.IsTrainingServerModeActive())
            {
                return;
            }

            var floorSnapshot = CaptureTrainingFloorSnapshot();

            SendTrainingDebugLine(clientId, $"{actionName} clientId={clientId} player={snapshot?.PlayerName ?? "n/a"} body={snapshot?.BodyComponentName ?? "null"} rb={FormatBool(snapshot != null && snapshot.HasRigidbody)}");
            SendTrainingDebugLine(clientId, $"{actionName} floor {FormatTrainingFloorSnapshot(floorSnapshot, snapshot)}");

            if (snapshot == null || !snapshot.HasBody)
            {
                SendTrainingDebugLine(clientId, $"{actionName} old body snapshot unavailable");
            }
            else
            {
                SendTrainingDebugLine(clientId, $"{actionName} old tfPos={FormatVector3(snapshot.TransformPosition)} rbPos={FormatSnapshotPosition(snapshot)} drift={FormatDistance(GetSnapshotDriftDistance(snapshot))}");
                SendTrainingDebugLine(clientId, $"{actionName} old tfRot={FormatQuaternion(snapshot.TransformRotation)} rbRot={FormatSnapshotRotation(snapshot)}");
                SendTrainingDebugLine(clientId, $"{actionName} old grounded={FormatBool(snapshot.IsGrounded)} hover={FormatTrainingHoverSnapshot(snapshot)}");
            }

            SendTrainingDebugLine(clientId, $"{actionName} {pathSummary}");
            SendTrainingDebugLine(clientId, $"{actionName} target tfPos={FormatVector3(targetPosition)} rbPos={FormatVector3(targetPosition)} rot={FormatQuaternion(targetRotation)}");
        }

        private static void ScheduleTrainingMovementTelemetryPostAction(ulong clientId, string actionName, Vector3 targetPosition, Quaternion targetRotation)
        {
            if (!Server.RankedSystem.IsTrainingServerModeActive())
            {
                return;
            }

            ScheduleUpdaterAction(0.15f, () =>
            {
                var earlySnapshot = CaptureTrainingBodySnapshot(clientId);
                ScheduleUpdaterAction(0.45f, () => SendTrainingMovementTelemetryFinal(clientId, actionName, targetPosition, targetRotation, earlySnapshot));
                ScheduleUpdaterAction(1.25f, () => SendTrainingMovementTelemetryLate(clientId, actionName, targetPosition));
            });
        }

        private static void SendTrainingMovementTelemetryFinal(ulong clientId, string actionName, Vector3 targetPosition, Quaternion targetRotation, TrainingBodySnapshot earlySnapshot)
        {
            if (!Server.RankedSystem.IsTrainingServerModeActive())
            {
                return;
            }

            var finalSnapshot = CaptureTrainingBodySnapshot(clientId);
            if (finalSnapshot == null || !finalSnapshot.HasBody)
            {
                SendTrainingDebugLine(clientId, $"{actionName} final body snapshot unavailable");
                return;
            }

            var tfDistance = Vector3.Distance(finalSnapshot.TransformPosition, targetPosition);
            var rbDistance = finalSnapshot.HasRigidbody ? Vector3.Distance(finalSnapshot.RigidbodyPosition, targetPosition) : tfDistance;
            var transformMatch = tfDistance <= TrainingDebugTargetMatchThreshold;
            var rigidbodyMatch = rbDistance <= TrainingDebugTargetMatchThreshold;
            var shiftAfterMove = GetMovementDelta(earlySnapshot, finalSnapshot);
            var overwriteDetected = shiftAfterMove > TrainingDebugPostMoveShiftThreshold;

            SendTrainingDebugLine(clientId, $"{actionName} final tfPos={FormatVector3(finalSnapshot.TransformPosition)} rbPos={FormatSnapshotPosition(finalSnapshot)} tfRot={FormatQuaternion(finalSnapshot.TransformRotation)} rbRot={FormatSnapshotRotation(finalSnapshot)}");
            SendTrainingDebugLine(clientId, $"{actionName} final grounded={FormatBool(finalSnapshot.IsGrounded)} hover={FormatTrainingHoverSnapshot(finalSnapshot)}");
            SendTrainingDebugLine(clientId, $"{actionName} match tf={FormatBool(transformMatch)}({FormatDistance(tfDistance)}) rb={FormatBool(rigidbodyMatch)}({FormatDistance(rbDistance)}) drift={FormatDistance(GetSnapshotDriftDistance(finalSnapshot))} shiftAfterMove={FormatDistance(shiftAfterMove)} overwrite={FormatBool(overwriteDetected)} targetRot={FormatQuaternion(targetRotation)}");
        }

        private static void SendTrainingMovementTelemetryLate(ulong clientId, string actionName, Vector3 targetPosition)
        {
            if (!Server.RankedSystem.IsTrainingServerModeActive())
            {
                return;
            }

            var lateSnapshot = CaptureTrainingBodySnapshot(clientId);
            if (lateSnapshot == null || !lateSnapshot.HasBody)
            {
                SendTrainingDebugLine(clientId, $"{actionName} late body snapshot unavailable");
                return;
            }

            var lateDistance = lateSnapshot.HasRigidbody ? Vector3.Distance(lateSnapshot.RigidbodyPosition, targetPosition) : Vector3.Distance(lateSnapshot.TransformPosition, targetPosition);
            var lateOverwriteDetected = lateDistance > TrainingDebugLateShiftThreshold;
            SendTrainingDebugLine(clientId, $"{actionName} late tfPos={FormatVector3(lateSnapshot.TransformPosition)} rbPos={FormatSnapshotPosition(lateSnapshot)} grounded={FormatBool(lateSnapshot.IsGrounded)} lateDistance={FormatDistance(lateDistance)} lateOverwrite={FormatBool(lateOverwriteDetected)} hover={FormatTrainingHoverSnapshot(lateSnapshot)}");
        }

        private static TrainingBodySnapshot CaptureTrainingBodySnapshot(ulong clientId)
        {
            var player = TryResolveTrainingPlayer(clientId);
            if (player == null)
            {
                return null;
            }

            return CaptureTrainingBodySnapshot(player, player.PlayerBody);
        }

        private static TrainingBodySnapshot CaptureTrainingBodySnapshot(Player player, PlayerBodyV2 playerBody)
        {
            if (player == null)
            {
                return null;
            }

            var snapshot = new TrainingBodySnapshot
            {
                ClientId = player.OwnerClientId,
                PlayerName = player.Username.Value.ToString(),
                HasBody = playerBody != null,
                BodyComponentName = playerBody != null ? playerBody.GetType().Name : "null",
                HasRigidbody = playerBody != null && playerBody.Rigidbody != null
            };

            if (playerBody != null)
            {
                snapshot.TransformPosition = playerBody.transform.position;
                snapshot.TransformRotation = playerBody.transform.rotation;
                if (playerBody.Rigidbody != null)
                {
                    snapshot.RigidbodyPosition = playerBody.Rigidbody.position;
                    snapshot.RigidbodyRotation = playerBody.Rigidbody.rotation;
                }

                snapshot.HasHover = playerBody.Hover != null;
                if (snapshot.HasHover)
                {
                    snapshot.IsGrounded = playerBody.Hover.IsGrounded;
                }

                if (TryGetHoverRaycastDebug(playerBody, out var raycastOffset, out var raycastDistance, out var raycastLayerMask, out var hoverTargetDistance, out var raycastOrigin, out var raycastHit, out var raycastHitInfo))
                {
                    snapshot.HoverTargetDistance = hoverTargetDistance;
                    snapshot.HoverRaycastOffset = raycastOffset;
                    snapshot.HoverRayOrigin = raycastOrigin;
                    snapshot.HoverRaycastDistance = raycastDistance;
                    snapshot.HoverRaycastLayerMask = raycastLayerMask.value;
                    snapshot.HoverRaycastHit = raycastHit;
                    if (raycastHit)
                    {
                        snapshot.HoverRaycastHitDistance = raycastHitInfo.distance;
                        snapshot.HoverRaycastHitObjectName = raycastHitInfo.collider != null ? raycastHitInfo.collider.gameObject.name : string.Empty;
                        snapshot.HoverRaycastHitLayer = raycastHitInfo.collider != null ? raycastHitInfo.collider.gameObject.layer : -1;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(snapshot.PlayerName))
            {
                snapshot.PlayerName = "Player";
            }

            return snapshot;
        }

        private static TrainingFloorSnapshot CaptureTrainingFloorSnapshot()
        {
            if (trainingOpenWorldFloorObject == null)
            {
                return null;
            }

            var collider = trainingOpenWorldFloorObject.GetComponent<Collider>();
            var snapshot = new TrainingFloorSnapshot
            {
                Exists = true,
                Name = trainingOpenWorldFloorObject.name,
                Position = trainingOpenWorldFloorObject.transform.position,
                ColliderType = collider != null ? collider.GetType().Name : "none",
                HasCollider = collider != null,
                IsTrigger = collider != null && collider.isTrigger,
                Layer = trainingOpenWorldFloorObject.layer
            };

            if (collider != null)
            {
                snapshot.ColliderBoundsSize = collider.bounds.size;
                snapshot.ColliderTopY = collider.bounds.max.y;
                snapshot.ColliderBottomY = collider.bounds.min.y;
            }

            return snapshot;
        }

        private static bool TryGetHoverRaycastDebug(PlayerBodyV2 playerBody, out Vector3 raycastOffset, out float raycastDistance, out LayerMask raycastLayerMask, out float hoverTargetDistance, out Vector3 raycastOrigin, out bool raycastHit, out RaycastHit raycastHitInfo)
        {
            raycastOffset = Vector3.zero;
            raycastDistance = 0f;
            raycastLayerMask = default(LayerMask);
            hoverTargetDistance = 0f;
            raycastOrigin = Vector3.zero;
            raycastHit = false;
            raycastHitInfo = default(RaycastHit);

            var hover = playerBody != null ? playerBody.Hover : null;
            if (hover == null)
            {
                return false;
            }

            hoverTargetDistance = hover.TargetDistance;

            try
            {
                if (HoverRaycastOffsetField != null)
                {
                    raycastOffset = (Vector3)HoverRaycastOffsetField.GetValue(hover);
                }

                if (HoverRaycastDistanceField != null)
                {
                    raycastDistance = (float)HoverRaycastDistanceField.GetValue(hover);
                }

                if (HoverRaycastLayerMaskField != null)
                {
                    raycastLayerMask = (LayerMask)HoverRaycastLayerMaskField.GetValue(hover);
                }
            }
            catch
            {
                return false;
            }

            if (raycastDistance <= 0f)
            {
                return true;
            }

            raycastOrigin = playerBody.transform.position + raycastOffset;
            raycastHit = Physics.Raycast(raycastOrigin, Vector3.down, out raycastHitInfo, raycastDistance, raycastLayerMask);
            return true;
        }

        private static Player TryResolveTrainingPlayer(ulong clientId)
        {
            try
            {
                var playerManager = PlayerManager.Instance;
                return playerManager != null ? playerManager.GetPlayerByClientId(clientId) : null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Failed to resolve training player for client {clientId}: {ex.Message}");
                return null;
            }
        }

        private static void ClearAllTrainingOpenWorldState()
        {
            trainingOpenWorldStateByClient.Clear();
            if (trainingOpenWorldFloorObject != null)
            {
                UnityEngine.Object.Destroy(trainingOpenWorldFloorObject);
                trainingOpenWorldFloorObject = null;
            }
        }

        private static bool TryGetTrainingPlayerForClient(ulong clientId, out Player player, out PlayerBodyV2 playerBody)
        {
            player = null;
            playerBody = null;

            try
            {
                player = TryResolveTrainingPlayer(clientId);
                playerBody = player != null ? player.PlayerBody : null;
                return player != null && playerBody != null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Failed to resolve training player for client {clientId}: {ex.Message}");
                player = null;
                playerBody = null;
                return false;
            }
        }

        private static PlayerRole ResolveTrainingOpenWorldRole(Player player, PlayerPosition playerPosition)
        {
            if (player != null && player.Role.Value != PlayerRole.None)
            {
                return player.Role.Value;
            }

            return playerPosition != null ? playerPosition.Role : PlayerRole.Attacker;
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

        private static void SendTrainingDebugLine(ulong clientId, string message)
        {
            if (!Server.RankedSystem.IsTrainingServerModeActive() || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            try { Debug.Log($"[{Constants.MOD_NAME}] [TRAINING][DBG] clientId={clientId} {StripRichTextTags(message)}"); } catch { }
            SendCommandHelpLine(clientId, $"<size=12><color=#78d8ff>[training dbg]</color> {message}</size>");
        }

        private static string FormatVector3(Vector3 value) => $"({value.x:0.00},{value.y:0.00},{value.z:0.00})";

        private static string FormatQuaternion(Quaternion value)
        {
            var euler = value.eulerAngles;
            return $"({euler.x:0.0},{euler.y:0.0},{euler.z:0.0})";
        }

        private static string FormatSnapshotPosition(TrainingBodySnapshot snapshot)
        {
            return snapshot != null && snapshot.HasRigidbody ? FormatVector3(snapshot.RigidbodyPosition) : "n/a";
        }

        private static string FormatSnapshotRotation(TrainingBodySnapshot snapshot)
        {
            return snapshot != null && snapshot.HasRigidbody ? FormatQuaternion(snapshot.RigidbodyRotation) : "n/a";
        }

        private static string FormatTrainingFloorSnapshot(TrainingFloorSnapshot floorSnapshot, TrainingBodySnapshot bodySnapshot)
        {
            if (floorSnapshot == null || !floorSnapshot.Exists)
            {
                return "missing";
            }

            var maskMatchesFloor = bodySnapshot != null && bodySnapshot.HasHover && DoesLayerMaskContainLayer(bodySnapshot.HoverRaycastLayerMask, floorSnapshot.Layer);
            return $"name={floorSnapshot.Name} pos={FormatVector3(floorSnapshot.Position)} collider={floorSnapshot.ColliderType} trigger={FormatBool(floorSnapshot.IsTrigger)} layer={FormatLayer(floorSnapshot.Layer)} maskHasFloor={FormatBool(maskMatchesFloor)} topY={FormatDistance(floorSnapshot.ColliderTopY)} size={FormatVector3(floorSnapshot.ColliderBoundsSize)}";
        }

        private static string FormatTrainingHoverSnapshot(TrainingBodySnapshot snapshot)
        {
            if (snapshot == null || !snapshot.HasHover)
            {
                return "n/a";
            }

            var hitText = snapshot.HoverRaycastHit
                ? $"yes obj={snapshot.HoverRaycastHitObjectName ?? "?"} layer={FormatLayer(snapshot.HoverRaycastHitLayer)} dist={FormatDistance(snapshot.HoverRaycastHitDistance)}"
                : "no";
            return $"target={FormatDistance(snapshot.HoverTargetDistance)} rayOrigin={FormatVector3(snapshot.HoverRayOrigin)} rayOffset={FormatVector3(snapshot.HoverRaycastOffset)} rayDist={FormatDistance(snapshot.HoverRaycastDistance)} mask={DescribeLayerMask(snapshot.HoverRaycastLayerMask)} hit={hitText}";
        }

        private static string FormatLayer(int layer)
        {
            if (layer < 0)
            {
                return "n/a";
            }

            var layerName = LayerMask.LayerToName(layer);
            return string.IsNullOrWhiteSpace(layerName) ? layer.ToString(CultureInfo.InvariantCulture) : $"{layer}({layerName})";
        }

        private static string DescribeLayerMask(int layerMaskValue)
        {
            if (layerMaskValue == 0)
            {
                return "0(none)";
            }

            var names = new List<string>();
            for (var layer = 0; layer < 32; layer++)
            {
                if ((layerMaskValue & (1 << layer)) == 0)
                {
                    continue;
                }

                names.Add(FormatLayer(layer));
            }

            return names.Count == 0 ? layerMaskValue.ToString(CultureInfo.InvariantCulture) : string.Join(",", names);
        }

        private static bool DoesLayerMaskContainLayer(int layerMaskValue, int layer)
        {
            if (layer < 0 || layer >= 32)
            {
                return false;
            }

            return (layerMaskValue & (1 << layer)) != 0;
        }

        private static string FormatBool(bool value) => value ? "yes" : "no";

        private static string FormatDistance(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        private static float GetSnapshotDriftDistance(TrainingBodySnapshot snapshot)
        {
            if (snapshot == null || !snapshot.HasRigidbody)
            {
                return 0f;
            }

            return Vector3.Distance(snapshot.TransformPosition, snapshot.RigidbodyPosition);
        }

        private static float GetMovementDelta(TrainingBodySnapshot before, TrainingBodySnapshot after)
        {
            if (before == null || after == null)
            {
                return 0f;
            }

            if (before.HasRigidbody && after.HasRigidbody)
            {
                return Vector3.Distance(before.RigidbodyPosition, after.RigidbodyPosition);
            }

            return Vector3.Distance(before.TransformPosition, after.TransformPosition);
        }

        private static void ScheduleUpdaterAction(float delaySeconds, Action action)
        {
            if (action == null)
            {
                return;
            }

            EnsureUpdater();
            try { updaterInstance?.Schedule(delaySeconds, action); } catch { }
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
            private sealed class ScheduledAction
            {
                public float ExecuteAt;
                public Action Callback;
            }

            private readonly List<ScheduledAction> scheduledActions = new List<ScheduledAction>();

            public void Schedule(float delaySeconds, Action callback)
            {
                if (callback == null)
                {
                    return;
                }

                scheduledActions.Add(new ScheduledAction
                {
                    ExecuteAt = Time.unscaledTime + Mathf.Max(0f, delaySeconds),
                    Callback = callback
                });
            }

            private void Update()
            {
                if (scheduledActions.Count > 0)
                {
                    var now = Time.unscaledTime;
                    for (var index = scheduledActions.Count - 1; index >= 0; index--)
                    {
                        var scheduledAction = scheduledActions[index];
                        if (scheduledAction == null || scheduledAction.ExecuteAt > now)
                        {
                            continue;
                        }

                        scheduledActions.RemoveAt(index);
                        try { scheduledAction.Callback?.Invoke(); } catch { }
                    }
                }

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
