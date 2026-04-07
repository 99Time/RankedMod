using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace schrader.Server
{
    internal static class BotManager
    {
        private sealed class BotRecord
        {
            public string BotId;
            public string DisplayName;
            public ulong OwnerClientId;
            public TeamResult Team;
            public string LockedPositionKey;
            public object PlayerObject;
            public Component PlayerComponent;
            public BotAIController Controller;
            public BotPlayStyle PlayStyle;
            public BotGoalieDifficulty GoalieDifficulty;
        }

        private static readonly object botLock = new object();
        private static readonly Dictionary<string, BotRecord> botsById = new Dictionary<string, BotRecord>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<ulong, string> botIdByClientId = new Dictionary<ulong, string>();
        private static int nextBotSequence = 1;
        private static ulong nextBotOwnerClientId = 7777000;

        internal static bool IsBotKey(string key)
        {
            return !string.IsNullOrEmpty(key) && key.StartsWith("bot:", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryGetBotIdByClientId(ulong clientId, out string botId)
        {
            lock (botLock)
            {
                return botIdByClientId.TryGetValue(clientId, out botId);
            }
        }

        internal static bool TryGetBotParticipant(string botId, out RankedParticipant participant)
        {
            participant = null;
            if (!IsBotKey(botId)) return false;

            BotRecord record;
            lock (botLock)
            {
                if (!botsById.TryGetValue(botId, out record)) return false;
            }

            participant = new RankedParticipant
            {
                clientId = record.OwnerClientId,
                playerId = record.BotId,
                displayName = record.DisplayName,
                team = record.Team,
                isDummy = true
            };

            return true;
        }

        internal static string GetBotDisplayName(string botId)
        {
            if (!IsBotKey(botId)) return null;
            lock (botLock)
            {
                BotRecord record;
                if (!botsById.TryGetValue(botId, out record)) return null;
                return record.DisplayName;
            }
        }

        internal static bool TryGetBotController(string botId, out BotAIController controller)
        {
            controller = null;
            if (!IsBotKey(botId)) return false;

            lock (botLock)
            {
                if (!botsById.TryGetValue(botId, out var record)) return false;
                controller = record.Controller;
                return controller != null;
            }
        }

        internal static bool TryGetAnyBotController(out string botId, out BotAIController controller)
        {
            botId = null;
            controller = null;

            lock (botLock)
            {
                foreach (var entry in botsById)
                {
                    if (entry.Value?.Controller == null) continue;
                    botId = entry.Key;
                    controller = entry.Value.Controller;
                    return true;
                }
            }

            return false;
        }

        internal static RankedParticipant SpawnBot(TeamResult preferredTeam, string requestedName)
        {
            return SpawnBotInternal(preferredTeam, requestedName, BotPlayStyle.Skater, BotGoalieDifficulty.Normal);
        }

        internal static RankedParticipant SpawnGoalieBot(TeamResult team, BotGoalieDifficulty difficulty, string requestedName)
        {
            if (team != TeamResult.Red && team != TeamResult.Blue) return null;
            return SpawnBotInternal(team, requestedName, BotPlayStyle.Goalie, difficulty);
        }

        internal static bool TryGetGoalieBotId(TeamResult team, out string botId)
        {
            botId = null;
            if (team != TeamResult.Red && team != TeamResult.Blue)
            {
                return false;
            }

            lock (botLock)
            {
                foreach (var pair in botsById)
                {
                    var record = pair.Value;
                    if (record == null || record.PlayStyle != BotPlayStyle.Goalie || record.Team != team)
                    {
                        continue;
                    }

                    botId = pair.Key;
                    return true;
                }
            }

            return false;
        }

        private static RankedParticipant SpawnBotInternal(TeamResult preferredTeam, string requestedName, BotPlayStyle playStyle, BotGoalieDifficulty goalieDifficulty)
        {
            try
            {
                if (!RankedSystem.AreSyntheticPlayersAllowed()) return null;
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return null;

                var playerManagerType = SpawnManager.FindTypeByName("PlayerManager", "Puck.PlayerManager");
                if (playerManagerType == null) return null;
                var playerManager = SpawnManager.GetManagerInstance(playerManagerType);
                if (playerManager == null) return null;

                object playerPrefab = null;
                try { playerPrefab = Traverse.Create(playerManager).Field("playerPrefab").GetValue<object>(); } catch { }
                if (playerPrefab == null)
                {
                    var prefabField = playerManagerType.GetField("playerPrefab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (prefabField != null) playerPrefab = prefabField.GetValue(playerManager);
                }
                if (playerPrefab == null) return null;

                Component prefabComponent = playerPrefab as Component;
                if (prefabComponent == null)
                {
                    var prefabGo = playerPrefab as GameObject;
                    if (prefabGo != null) prefabComponent = prefabGo.GetComponent("Player");
                }
                if (prefabComponent == null) return null;

                var spawned = UnityEngine.Object.Instantiate(prefabComponent) as Component;
                if (spawned == null) return null;

                var playerComponent = spawned.GetComponent("Player") ?? spawned;
                var networkObject = playerComponent.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    UnityEngine.Object.Destroy(playerComponent.gameObject);
                    return null;
                }

                string botId;
                string botName;
                ulong ownerClientId;
                lock (botLock)
                {
                    botId = $"bot:{nextBotSequence}";
                    botName = string.IsNullOrWhiteSpace(requestedName) ? $"RankedBot{nextBotSequence}" : requestedName;
                    nextBotSequence++;

                    ownerClientId = nextBotOwnerClientId++;
                    while (botIdByClientId.ContainsKey(ownerClientId)) ownerClientId = nextBotOwnerClientId++;
                }

                networkObject.SpawnWithOwnership(ownerClientId, true);
                Debug.LogWarning($"[{Constants.MOD_NAME}] [ENTITY-SPAWN] Bot player root spawned. reason=SpawnBot botId={botId} requestedName={requestedName ?? "null"} {RankedSystem.DescribePlayerLifecycle(playerComponent, ownerClientId, "bot")}");

                var teamValue = ResolveTeamValue(preferredTeam, allowNeutral: true);
                var roleValue = playStyle == BotPlayStyle.Goalie ? ResolveGoalieRoleValue() : ResolveRoleValue();

                TrySetNetworkVariableValue(playerComponent, "Username", botName);
                TrySetNetworkVariableValue(playerComponent, "Number", 90);
                if (teamValue != null) TrySetNetworkVariableValue(playerComponent, "Team", teamValue);
                if (roleValue != null) TrySetNetworkVariableValue(playerComponent, "Role", roleValue);

                var claimedPositionKey = playStyle == BotPlayStyle.Goalie
                    ? TryClaimOpenPosition(playerComponent, teamValue, roleValue, null, skipGoalie: false, requireGoaliePosition: true)
                    : TryClaimOpenPosition(playerComponent, teamValue, roleValue, null, skipGoalie: true);
                if (string.IsNullOrEmpty(claimedPositionKey))
                {
                    claimedPositionKey = TryClaimOpenPosition(playerComponent, teamValue, roleValue, null, skipGoalie: false);
                }

                if (playStyle == BotPlayStyle.Goalie && roleValue != null)
                {
                    TrySetNetworkVariableValue(playerComponent, "Role", roleValue);
                }

                TrySpawnCharacter(playerComponent, roleValue);
                Debug.LogWarning($"[{Constants.MOD_NAME}] [ENTITY-SPAWN] Bot character spawn invoked. reason=SpawnBot botId={botId} claimedPosition={claimedPositionKey ?? "none"} {RankedSystem.DescribePlayerLifecycle(playerComponent, ownerClientId, "bot")}");
                var botController = AttachBotController(playerComponent);
                if (botController != null)
                {
                    if (playStyle == BotPlayStyle.Goalie) botController.ConfigureGoalkeeper(goalieDifficulty);
                    else botController.ConfigureSkater();
                }

                var participant = new RankedParticipant
                {
                    clientId = ownerClientId,
                    playerId = botId,
                    displayName = botName,
                    team = preferredTeam,
                    isDummy = true
                };

                lock (botLock)
                {
                    botsById[botId] = new BotRecord
                    {
                        BotId = botId,
                        DisplayName = botName,
                        OwnerClientId = ownerClientId,
                        Team = preferredTeam,
                        LockedPositionKey = claimedPositionKey,
                        PlayerObject = playerComponent,
                        PlayerComponent = playerComponent,
                        Controller = botController,
                        PlayStyle = playStyle,
                        GoalieDifficulty = goalieDifficulty
                    };
                    botIdByClientId[ownerClientId] = botId;
                }

                return participant;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] BotManager.SpawnBot failed: {ex.Message}");
            }

            return null;
        }

        internal static bool AssignBotTeam(string botId, TeamResult team)
        {
            if (!IsBotKey(botId)) return false;

            BotRecord record;
            lock (botLock)
            {
                if (!botsById.TryGetValue(botId, out record)) return false;
            }

            try
            {
                var teamValue = ResolveTeamValue(team, allowNeutral: false);
                if (teamValue == null) return false;

                if (TrySetNetworkVariableValue(record.PlayerObject, "Team", teamValue))
                {
                    lock (botLock)
                    {
                        record.Team = team;
                        record.LockedPositionKey = null;
                    }
                    return true;
                }
            }
            catch { }

            return false;
        }

        internal static bool EnsureBotSafePosition(string botId, TeamResult team, HashSet<string> usedPositionKeys)
        {
            if (!IsBotKey(botId)) return false;

            BotRecord record;
            lock (botLock)
            {
                if (!botsById.TryGetValue(botId, out record)) return false;
            }

            try
            {
                var teamValue = ResolveTeamValue(team, allowNeutral: false);
                if (teamValue == null) return false;

                var roleValue = record.PlayStyle == BotPlayStyle.Goalie ? ResolveGoalieRoleValue() : ResolveRoleValue();
                if (roleValue != null) TrySetNetworkVariableValue(record.PlayerObject, "Role", roleValue);
                TrySetNetworkVariableValue(record.PlayerObject, "Team", teamValue);

                if (usedPositionKeys == null)
                {
                    usedPositionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                if (!string.IsNullOrEmpty(record.LockedPositionKey) && IsPositionKeyClaimedByPlayer(record.PlayerObject, teamValue, record.LockedPositionKey))
                {
                    usedPositionKeys.Add(record.LockedPositionKey);
                    return true;
                }

                CollectClaimedPositionKeys(teamValue, usedPositionKeys);

                var claimedKey = record.PlayStyle == BotPlayStyle.Goalie
                    ? TryClaimOpenPosition(record.PlayerObject, teamValue, roleValue, usedPositionKeys, skipGoalie: false, requireGoaliePosition: true)
                    : TryClaimOpenPosition(record.PlayerObject, teamValue, roleValue, usedPositionKeys, skipGoalie: true);
                if (string.IsNullOrEmpty(claimedKey))
                {
                    claimedKey = TryClaimOpenPosition(record.PlayerObject, teamValue, roleValue, usedPositionKeys, skipGoalie: false);
                }

                if (!string.IsNullOrEmpty(claimedKey))
                {
                    if (record.PlayStyle == BotPlayStyle.Goalie && roleValue != null)
                    {
                        TrySetNetworkVariableValue(record.PlayerObject, "Role", roleValue);
                    }

                    lock (botLock)
                    {
                        record.Team = team;
                        record.LockedPositionKey = claimedKey;
                    }

                    if (record.PlayerObject != null) TrySpawnCharacter(record.PlayerObject, roleValue);
                    return true;
                }
            }
            catch { }

            return false;
        }

        internal static bool RemoveBot(string botId)
        {
            if (!IsBotKey(botId)) return false;

            BotRecord record;
            lock (botLock)
            {
                if (!botsById.TryGetValue(botId, out record)) return false;
                botsById.Remove(botId);
                botIdByClientId.Remove(record.OwnerClientId);
            }

            try
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [ENTITY-DESPAWN] Removing bot. reason=RemoveBot botId={botId} {RankedSystem.DescribePlayerLifecycle(record.PlayerObject, record.OwnerClientId, "bot")}");
                TryRemoveBotController(record);
                TryDespawnPlayer(record.PlayerObject);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] BotManager.RemoveBot failed: {ex.Message}");
            }

            return true;
        }

        internal static void RemoveAllBots()
        {
            List<string> botIds;
            lock (botLock)
            {
                botIds = botsById.Keys.ToList();
            }

            foreach (var botId in botIds)
            {
                try { RemoveBot(botId); } catch { }
            }
        }

        private static BotAIController AttachBotController(Component playerComponent)
        {
            try
            {
                if (playerComponent == null) return null;

                var player = playerComponent as Player;
                if (player == null) player = playerComponent.GetComponent<Player>();
                if (player == null) return null;

                var controller = player.gameObject.GetComponent<BotAIController>();
                if (controller == null)
                {
                    controller = player.gameObject.AddComponent<BotAIController>();
                }

                controller.Initialize(player);
                return controller;
            }
            catch
            {
                return null;
            }
        }

        private static void TryRemoveBotController(BotRecord record)
        {
            try
            {
                if (record?.Controller != null)
                {
                    UnityEngine.Object.Destroy(record.Controller);
                    return;
                }

                if (record?.PlayerComponent == null) return;
                var controller = record.PlayerComponent.GetComponent<BotAIController>();
                if (controller != null)
                {
                    UnityEngine.Object.Destroy(controller);
                }
            }
            catch
            {
            }
        }

        private static object ResolveRoleValue()
        {
            var roleType = SpawnManager.FindTypeByName("PlayerRole", "Puck.PlayerRole");
            if (roleType == null || !roleType.IsEnum) return null;

            var names = Enum.GetNames(roleType);
            foreach (var preferred in new[] { "Attacker", "Forward", "Skater", "Center", "Player" })
            {
                var match = names.FirstOrDefault(name => string.Equals(name, preferred, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(match)) return Enum.Parse(roleType, match, true);
            }

            foreach (var name in names)
            {
                if (name.IndexOf("goal", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                return Enum.Parse(roleType, name, true);
            }

            return Enum.Parse(roleType, names.First());
        }

        private static object ResolveGoalieRoleValue()
        {
            var roleType = SpawnManager.FindTypeByName("PlayerRole", "Puck.PlayerRole");
            if (roleType == null || !roleType.IsEnum) return null;

            var names = Enum.GetNames(roleType);
            foreach (var preferred in new[] { "Goalie", "Goalkeeper", "GK", "Goal" })
            {
                var match = names.FirstOrDefault(name => string.Equals(name, preferred, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(match)) return Enum.Parse(roleType, match, true);
            }

            foreach (var name in names)
            {
                if (name.IndexOf("goal", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return Enum.Parse(roleType, name, true);
                }
            }

            return null;
        }

        private static object ResolveTeamValue(TeamResult team, bool allowNeutral)
        {
            var teamType = SpawnManager.FindTypeByName("PlayerTeam", "Puck.PlayerTeam");
            if (teamType == null || !teamType.IsEnum) return null;

            var names = Enum.GetNames(teamType);
            if (team == TeamResult.Red)
            {
                var red = names.FirstOrDefault(name => string.Equals(name, "Red", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(red)) return Enum.Parse(teamType, red, true);
            }
            else if (team == TeamResult.Blue)
            {
                var blue = names.FirstOrDefault(name => string.Equals(name, "Blue", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(blue)) return Enum.Parse(teamType, blue, true);
            }

            if (allowNeutral)
            {
                foreach (var neutralName in new[] { "Spectator", "None", "Unknown", "Unassigned" })
                {
                    var neutral = names.FirstOrDefault(name => string.Equals(name, neutralName, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(neutral)) return Enum.Parse(teamType, neutral, true);
                }
            }

            if (names.Length == 0) return null;
            return Enum.Parse(teamType, names.First());
        }

        private static bool TryClaimOpenPosition(object playerObject, object teamValue, object roleValue)
        {
            return !string.IsNullOrEmpty(TryClaimOpenPosition(playerObject, teamValue, roleValue, null, skipGoalie: true));
        }

        private static string TryClaimOpenPosition(object playerObject, object teamValue, object roleValue, ISet<string> usedPositionKeys, bool skipGoalie, bool requireGoaliePosition = false)
        {
            try
            {
                if (playerObject == null || teamValue == null) return null;

                var ppmType = SpawnManager.FindTypeByName("PlayerPositionManager", "Puck.PlayerPositionManager");
                if (ppmType == null) return null;
                var ppm = SpawnManager.GetManagerInstance(ppmType);
                if (ppm == null) return null;

                string listName = null;
                var teamName = teamValue.ToString();
                if (string.Equals(teamName, "Red", StringComparison.OrdinalIgnoreCase)) listName = "RedPositions";
                else if (string.Equals(teamName, "Blue", StringComparison.OrdinalIgnoreCase)) listName = "BluePositions";
                if (string.IsNullOrEmpty(listName)) return null;

                var listProp = ppmType.GetProperty(listName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var positions = listProp != null ? listProp.GetValue(ppm) as IEnumerable : null;
                if (positions == null) return null;

                var index = 0;
                foreach (var position in positions)
                {
                    var positionKey = $"{listName}:{index++}";
                    if (position == null) continue;

                    if (usedPositionKeys != null && usedPositionKeys.Contains(positionKey)) continue;

                    var posType = position.GetType();

                    var claimedProp = posType.GetProperty("IsClaimed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (claimedProp != null)
                    {
                        var claimedValue = claimedProp.GetValue(position);
                        if (claimedValue is bool claimed && claimed)
                        {
                            usedPositionKeys?.Add(positionKey);
                            continue;
                        }
                    }

                    var isGoaliePosition = IsGoaliePosition(position);
                    if (requireGoaliePosition && !isGoaliePosition) continue;
                    if (skipGoalie && isGoaliePosition) continue;

                    if (roleValue != null)
                    {
                        var roleProp = posType.GetProperty("Role", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                        if (roleProp != null)
                        {
                            var requiredRole = roleProp.GetValue(position);
                            if (requiredRole != null && !string.Equals(requiredRole.ToString(), roleValue.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
                        }
                    }

                    var claimMethod = posType.GetMethod("Server_Claim", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (claimMethod != null)
                    {
                        claimMethod.Invoke(position, new[] { playerObject });
                        usedPositionKeys?.Add(positionKey);
                        return positionKey;
                    }
                }
            }
            catch { }

            return null;
        }

        private static void CollectClaimedPositionKeys(object teamValue, ISet<string> usedPositionKeys)
        {
            if (teamValue == null || usedPositionKeys == null) return;

            try
            {
                var ppmType = SpawnManager.FindTypeByName("PlayerPositionManager", "Puck.PlayerPositionManager");
                if (ppmType == null) return;

                var ppm = SpawnManager.GetManagerInstance(ppmType);
                if (ppm == null) return;

                string listName = null;
                var teamName = teamValue.ToString();
                if (string.Equals(teamName, "Red", StringComparison.OrdinalIgnoreCase)) listName = "RedPositions";
                else if (string.Equals(teamName, "Blue", StringComparison.OrdinalIgnoreCase)) listName = "BluePositions";
                if (string.IsNullOrEmpty(listName)) return;

                var listProp = ppmType.GetProperty(listName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var positions = listProp != null ? listProp.GetValue(ppm) as IEnumerable : null;
                if (positions == null) return;

                var index = 0;
                foreach (var position in positions)
                {
                    var positionKey = $"{listName}:{index++}";
                    if (position == null) continue;
                    if (IsPositionClaimed(position)) usedPositionKeys.Add(positionKey);
                }
            }
            catch { }
        }

        private static bool IsPositionKeyClaimedByPlayer(object playerObject, object teamValue, string expectedPositionKey)
        {
            if (playerObject == null || teamValue == null || string.IsNullOrEmpty(expectedPositionKey)) return false;

            try
            {
                var ppmType = SpawnManager.FindTypeByName("PlayerPositionManager", "Puck.PlayerPositionManager");
                if (ppmType == null) return false;

                var ppm = SpawnManager.GetManagerInstance(ppmType);
                if (ppm == null) return false;

                string listName = null;
                var teamName = teamValue.ToString();
                if (string.Equals(teamName, "Red", StringComparison.OrdinalIgnoreCase)) listName = "RedPositions";
                else if (string.Equals(teamName, "Blue", StringComparison.OrdinalIgnoreCase)) listName = "BluePositions";
                if (string.IsNullOrEmpty(listName)) return false;

                var listProp = ppmType.GetProperty(listName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var positions = listProp != null ? listProp.GetValue(ppm) as IEnumerable : null;
                if (positions == null) return false;

                var index = 0;
                foreach (var position in positions)
                {
                    var positionKey = $"{listName}:{index++}";
                    if (!string.Equals(positionKey, expectedPositionKey, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!IsPositionClaimed(position)) return false;
                    return IsPositionOwnedByPlayer(position, playerObject);
                }
            }
            catch { }

            return false;
        }

        private static bool IsPositionClaimed(object position)
        {
            if (position == null) return false;

            try
            {
                var posType = position.GetType();
                var claimedProp = posType.GetProperty("IsClaimed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (claimedProp == null) return false;

                var claimedValue = claimedProp.GetValue(position);
                return claimedValue is bool claimed && claimed;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPositionOwnedByPlayer(object position, object playerObject)
        {
            if (position == null || playerObject == null) return false;

            try
            {
                var posType = position.GetType();
                object owner = null;

                var ownerProp = posType.GetProperty("Player", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (ownerProp != null) owner = ownerProp.GetValue(position);

                if (owner == null)
                {
                    var ownerField = posType.GetField("player", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                                   ?? posType.GetField("Player", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (ownerField != null) owner = ownerField.GetValue(position);
                }

                if (owner == null) return true;
                if (ReferenceEquals(owner, playerObject)) return true;

                var ownerComponent = owner as Component;
                var playerComponent = playerObject as Component;
                if (ownerComponent != null && playerComponent != null)
                {
                    return ownerComponent == playerComponent;
                }
            }
            catch { }

            return false;
        }

        private static bool IsGoaliePosition(object position)
        {
            if (position == null) return false;

            try
            {
                var posType = position.GetType();

                var roleProp = posType.GetProperty("Role", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (roleProp != null)
                {
                    var roleValue = roleProp.GetValue(position);
                    if (roleValue != null && roleValue.ToString().IndexOf("goal", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }

                var nameProp = posType.GetProperty("Name", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (nameProp != null)
                {
                    var nameValue = nameProp.GetValue(position) as string;
                    if (!string.IsNullOrEmpty(nameValue))
                    {
                        var normalized = nameValue.Trim();
                        if (normalized.Equals("G", StringComparison.OrdinalIgnoreCase)
                            || normalized.Equals("GK", StringComparison.OrdinalIgnoreCase)
                            || normalized.IndexOf("goal", StringComparison.OrdinalIgnoreCase) >= 0
                            || normalized.IndexOf("keeper", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        private static void TrySpawnCharacter(object playerObject, object roleValue)
        {
            if (playerObject == null) return;

            try
            {
                var playerType = playerObject.GetType();

                var isPartiallySpawnedProp = playerType.GetProperty("IsCharacterPartiallySpawned", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var despawnMethod = playerType.GetMethod("Server_DespawnCharacter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (isPartiallySpawnedProp != null)
                {
                    var isPartiallySpawnedValue = isPartiallySpawnedProp.GetValue(playerObject);
                    if (isPartiallySpawnedValue is bool isPartiallySpawned && isPartiallySpawned && despawnMethod != null)
                    {
                        despawnMethod.Invoke(playerObject, null);
                    }
                }

                var spawnMethods = playerType
                    .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    .Where(m => string.Equals(m.Name, "Server_SpawnCharacter", StringComparison.Ordinal))
                    .ToArray();

                foreach (var spawnMethod in spawnMethods)
                {
                    var parameters = spawnMethod.GetParameters();
                    try
                    {
                        if (parameters.Length == 3 && parameters[0].ParameterType == typeof(Vector3) && parameters[1].ParameterType == typeof(Quaternion))
                        {
                            var convertedRole = ConvertValue(roleValue, parameters[2].ParameterType);
                            if (convertedRole == null) convertedRole = GetDefaultValue(parameters[2].ParameterType);
                            spawnMethod.Invoke(playerObject, new object[] { Vector3.zero, Quaternion.identity, convertedRole });
                            return;
                        }

                        if (parameters.Length == 1)
                        {
                            var convertedRole = ConvertValue(roleValue, parameters[0].ParameterType);
                            if (convertedRole == null) convertedRole = GetDefaultValue(parameters[0].ParameterType);
                            spawnMethod.Invoke(playerObject, new[] { convertedRole });
                            return;
                        }

                        if (parameters.Length == 0)
                        {
                            spawnMethod.Invoke(playerObject, null);
                            return;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void TryDespawnPlayer(object playerObject)
        {
            try
            {
                if (playerObject == null) return;
                Debug.LogWarning($"[{Constants.MOD_NAME}] [ENTITY-DESPAWN] Despawning player object. reason=TryDespawnPlayer {RankedSystem.DescribePlayerLifecycle(playerObject, 0, null)}");
                var playerType = playerObject.GetType();

                var despawnCharacter = playerType.GetMethod("Server_DespawnCharacter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (despawnCharacter != null)
                {
                    try { despawnCharacter.Invoke(playerObject, null); } catch { }
                }

                Component playerComponent = playerObject as Component;
                if (playerComponent == null)
                {
                    var componentProp = playerType.GetProperty("transform", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    var transformObj = componentProp != null ? componentProp.GetValue(playerObject) as Transform : null;
                    if (transformObj != null) playerComponent = transformObj.GetComponent("Player");
                }

                if (playerComponent != null)
                {
                    var networkObject = playerComponent.GetComponent<NetworkObject>();
                    if (networkObject != null && networkObject.IsSpawned)
                    {
                        try { networkObject.Despawn(true); } catch { }
                    }

                    try { UnityEngine.Object.Destroy(playerComponent.gameObject); } catch { }
                }

                var playerManagerType = SpawnManager.FindTypeByName("PlayerManager", "Puck.PlayerManager");
                if (playerManagerType != null)
                {
                    var playerManager = SpawnManager.GetManagerInstance(playerManagerType);
                    if (playerManager != null)
                    {
                        var removePlayer = playerManagerType.GetMethod("RemovePlayer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                        if (removePlayer != null)
                        {
                            try { removePlayer.Invoke(playerManager, new[] { playerObject }); } catch { }
                        }
                    }
                }
            }
            catch { }
        }

        private static bool TrySetNetworkVariableValue(object instance, string memberName, object rawValue)
        {
            if (instance == null || string.IsNullOrEmpty(memberName)) return false;

            try
            {
                var type = instance.GetType();
                object memberValue = null;

                var prop = type.GetProperty(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (prop != null)
                {
                    memberValue = prop.GetValue(instance);
                    if (memberValue == null && prop.CanWrite)
                    {
                        var convertedDirect = ConvertValue(rawValue, prop.PropertyType);
                        if (convertedDirect != null)
                        {
                            prop.SetValue(instance, convertedDirect);
                            return true;
                        }
                    }
                }

                if (memberValue == null)
                {
                    var field = type.GetField(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (field != null)
                    {
                        memberValue = field.GetValue(instance);
                        if (memberValue == null)
                        {
                            var convertedField = ConvertValue(rawValue, field.FieldType);
                            if (convertedField != null)
                            {
                                field.SetValue(instance, convertedField);
                                return true;
                            }
                        }
                    }
                }

                if (memberValue == null) return false;

                var valueProp = memberValue.GetType().GetProperty("Value", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (valueProp == null || !valueProp.CanWrite) return false;

                var converted = ConvertValue(rawValue, valueProp.PropertyType);
                if (converted == null) return false;
                valueProp.SetValue(memberValue, converted);
                return true;
            }
            catch { }

            return false;
        }

        private static object ConvertValue(object rawValue, Type targetType)
        {
            try
            {
                if (targetType == null) return null;
                if (rawValue == null)
                {
                    if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null) return null;
                    return Activator.CreateInstance(targetType);
                }

                if (targetType.IsInstanceOfType(rawValue)) return rawValue;

                var rawType = rawValue.GetType();
                if (Nullable.GetUnderlyingType(targetType) != null)
                {
                    targetType = Nullable.GetUnderlyingType(targetType);
                }

                if (targetType.IsEnum)
                {
                    if (rawValue is string enumString)
                    {
                        return Enum.Parse(targetType, enumString, true);
                    }

                    if (rawType.IsEnum)
                    {
                        return Enum.Parse(targetType, rawValue.ToString(), true);
                    }

                    var numeric = Convert.ToInt32(rawValue);
                    return Enum.ToObject(targetType, numeric);
                }

                if (string.Equals(targetType.FullName, "Unity.Collections.FixedString32Bytes", StringComparison.Ordinal))
                {
                    var text = rawValue.ToString() ?? string.Empty;
                    var ctor = targetType.GetConstructor(new[] { typeof(string) });
                    if (ctor != null) return ctor.Invoke(new object[] { text });

                    var implicitOp = targetType.GetMethod("op_Implicit", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, new[] { typeof(string) }, null);
                    if (implicitOp != null) return implicitOp.Invoke(null, new object[] { text });
                }

                return Convert.ChangeType(rawValue, targetType);
            }
            catch { }

            return null;
        }

        private static object GetDefaultValue(Type type)
        {
            if (type == null) return null;
            try
            {
                if (type.IsValueType) return Activator.CreateInstance(type);
            }
            catch { }
            return null;
        }
    }
}
