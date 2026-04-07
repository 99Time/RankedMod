using System;
using System.Collections.Generic;
using UnityEngine;

namespace schrader.Server
{
    public static partial class RankedSystem
    {
        private const ulong ReplayClientIdOffset = 1337UL;

        private static List<object> GetAllPlayers()
        {
            var result = new List<object>();
            try
            {
                var pmType = FindTypeByName("PlayerManager", "Puck.PlayerManager");
                if (pmType == null) return result;
                var pm = GetManagerInstance(pmType);
                if (pm == null) return result;

                var list = TryGetEnumerable(pm, new[] { "Players", "players", "PlayerList", "AllPlayers", "m_Players" });
                if (list != null) { foreach (var obj in list) result.Add(obj); return result; }

                var method = pmType.GetMethod("GetPlayers", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                             ?? pmType.GetMethod("GetAllPlayers", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    var val = method.Invoke(pm, null) as System.Collections.IEnumerable;
                    if (val != null) foreach (var obj in val) result.Add(obj);
                }
            }
            catch { }
            return result;
        }

        private static System.Collections.IEnumerable TryGetEnumerable(object obj, string[] names)
        {
            if (obj == null || names == null) return null;
            var t = obj.GetType();
            foreach (var n in names)
            {
                var prop = t.GetProperty(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (prop != null) { var val = prop.GetValue(obj) as System.Collections.IEnumerable; if (val != null) return val; }
                var field = t.GetField(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null) { var val = field.GetValue(obj) as System.Collections.IEnumerable; if (val != null) return val; }
            }
            return null;
        }

        private static bool TryGetClientId(object player, out ulong clientId)
        {
            clientId = 0;
            if (player == null) return false;
            var t = player.GetType();
            string[] names = { "ClientId", "clientId", "OwnerClientId", "ownerClientId", "m_ClientId" };
            foreach (var n in names)
            {
                var prop = t.GetProperty(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (prop != null)
                {
                    var val = prop.GetValue(player);
                    if (TryConvertToUlong(val, out clientId)) return true;
                }
                var field = t.GetField(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    var val = field.GetValue(player);
                    if (TryConvertToUlong(val, out clientId)) return true;
                }
            }
            return false;
        }

        private static bool TryConvertToUlong(object val, out ulong result)
        {
            result = 0;
            if (val == null) return false;
            try
            {
                if (val is ulong u) { result = u; return true; }
                if (val is long l) { result = (ulong)l; return true; }
                if (val is int i) { result = (ulong)i; return true; }
                if (val is uint ui) { result = ui; return true; }
                if (ulong.TryParse(val.ToString(), out var parsed)) { result = parsed; return true; }
            }
            catch { }
            return false;
        }

        private static bool TryConvertToInt(object val, out int result)
        {
            result = 0;
            if (val == null) return false;
            try
            {
                if (val is int i) { result = i; return true; }
                if (val is long l && l >= int.MinValue && l <= int.MaxValue) { result = (int)l; return true; }
                if (val is uint ui && ui <= int.MaxValue) { result = (int)ui; return true; }
                if (val is string s && int.TryParse(s, out var parsed)) { result = parsed; return true; }
                if (val is double d) { result = (int)d; return true; }
                var valueProp = val.GetType().GetProperty("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (valueProp != null)
                {
                    var inner = valueProp.GetValue(val);
                    if (!ReferenceEquals(inner, val) && TryConvertToInt(inner, out result)) return true;
                }
            }
            catch { }
            return false;
        }

        private static bool TryConvertToBool(object val, out bool result)
        {
            result = false;
            if (val == null) return false;
            try
            {
                if (val is bool b) { result = b; return true; }
                if (val is int i) { result = i != 0; return true; }
                if (val is long l) { result = l != 0; return true; }
                if (val is uint ui) { result = ui != 0; return true; }
                if (val is ulong ul) { result = ul != 0; return true; }
                if (val is string s)
                {
                    if (bool.TryParse(s, out var parsedBool))
                    {
                        result = parsedBool;
                        return true;
                    }

                    if (int.TryParse(s, out var parsedInt))
                    {
                        result = parsedInt != 0;
                        return true;
                    }
                }

                var valueProp = val.GetType().GetProperty("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (valueProp != null)
                {
                    var inner = valueProp.GetValue(val);
                    if (!ReferenceEquals(inner, val) && TryConvertToBool(inner, out result)) return true;
                }
            }
            catch { }

            return false;
        }

        private static bool HasReplayFlag(object obj, int depth = 0)
        {
            if (obj == null || depth > 2)
            {
                return false;
            }

            try
            {
                var type = obj.GetType();
                foreach (var memberName in new[] { "IsReplay", "isReplay", "m_IsReplay" })
                {
                    var property = type.GetProperty(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (property != null)
                    {
                        var value = property.GetValue(obj);
                        if (TryConvertToBool(value, out var replayFlag) && replayFlag)
                        {
                            return true;
                        }
                    }

                    var field = type.GetField(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (field != null)
                    {
                        var value = field.GetValue(obj);
                        if (TryConvertToBool(value, out var replayFlag) && replayFlag)
                        {
                            return true;
                        }
                    }
                }

                foreach (var ownerName in new[] { "Player", "player" })
                {
                    var property = type.GetProperty(ownerName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (property != null)
                    {
                        var nested = property.GetValue(obj);
                        if (nested != null && !ReferenceEquals(nested, obj) && HasReplayFlag(nested, depth + 1))
                        {
                            return true;
                        }
                    }

                    var field = type.GetField(ownerName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (field != null)
                    {
                        var nested = field.GetValue(obj);
                        if (nested != null && !ReferenceEquals(nested, obj) && HasReplayFlag(nested, depth + 1))
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        private static bool AreEquivalentPlayerObjects(object left, object right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            if (ReferenceEquals(left, right))
            {
                return true;
            }

            try
            {
                if (TryGetClientId(left, out var leftClientId)
                    && TryGetClientId(right, out var rightClientId)
                    && leftClientId != 0
                    && leftClientId == rightClientId)
                {
                    return true;
                }

                var leftId = TryGetPlayerIdNoFallback(left);
                var rightId = TryGetPlayerIdNoFallback(right);
                if (!string.IsNullOrWhiteSpace(leftId) && string.Equals(leftId, rightId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static bool IsReplayClientId(ulong clientId)
        {
            if (clientId == 0 || !TryGetPlayerManager(out var manager) || manager == null)
            {
                return false;
            }

            try
            {
                var method = manager.GetType().GetMethod("GetReplayPlayerByClientId", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (method == null)
                {
                    return false;
                }

                if (method.Invoke(manager, new object[] { clientId }) != null)
                {
                    return true;
                }

                if (clientId >= ReplayClientIdOffset)
                {
                    var sourceClientId = clientId - ReplayClientIdOffset;
                    if (method.Invoke(manager, new object[] { sourceClientId }) != null)
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public static string TryGetPlayerId(object player, ulong fallbackClientId)
        {
            var found = TryGetPlayerIdNoFallback(player);
            if (!string.IsNullOrEmpty(found)) return found;
            return fallbackClientId == 0 ? null : $"clientId:{fallbackClientId}";
        }

        private static string TryGetPlayerIdNoFallback(object player)
        {
            try
            {
                if (player == null) return null;
                var t = player.GetType();
                string[] names = { "SteamId", "steamId", "SteamID", "steamID", "SteamId64", "Steam64Id", "steamID64", "m_SteamId" };
                foreach (var n in names)
                {
                    var prop = t.GetProperty(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (prop != null)
                    {
                        var val = prop.GetValue(player);
                        var s = ExtractSimpleValueToString(val);
                        if (!string.IsNullOrEmpty(s)) return s;
                    }
                    var field = t.GetField(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (field != null)
                    {
                        var val = field.GetValue(player);
                        var s = ExtractSimpleValueToString(val);
                        if (!string.IsNullOrEmpty(s)) return s;
                    }
                }
            }
            catch { }
            return null;
        }

        private static string ExtractSimpleValueToString(object val)
        {
            try
            {
                if (val == null) return null;
                if (val is string s) return s;
                if (val is ulong ul) return ul.ToString();
                if (val is long l) return l.ToString();
                if (val is int i) return i.ToString();
                if (val is uint ui) return ui.ToString();
                var t = val.GetType();
                var valueProp = t.GetProperty("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (valueProp != null)
                {
                    var inner = valueProp.GetValue(val);
                    if (inner != null) return inner.ToString();
                }
                var ts = val.ToString();
                if (!string.IsNullOrWhiteSpace(ts) && !ts.StartsWith(t.FullName)) return ts;
            }
            catch { }
            return null;
        }

        private static bool TryGetPlayerTeam(object player, out TeamResult team)
        {
            team = TeamResult.Unknown;
            if (player == null) return false;
            var t = player.GetType();
            string[] names = { "Team", "team", "TeamIndex", "teamIndex", "TeamId", "teamId", "TeamSide", "side" };
            foreach (var n in names)
            {
                object val = null;
                var prop = t.GetProperty(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (prop != null) val = prop.GetValue(player);
                if (val == null)
                {
                    var field = t.GetField(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (field != null) val = field.GetValue(player);
                }

                if (val != null)
                {
                    team = ConvertTeamValue(val);
                    if (team != TeamResult.Unknown) return true;
                }
            }
            return false;
        }

        private static TeamResult ConvertTeamValue(object val)
        {
            if (val == null) return TeamResult.Unknown;
            try
            {
                if (val is int i) return i == 0 ? TeamResult.Red : (i == 1 ? TeamResult.Blue : TeamResult.Unknown);
                if (val is uint ui) return ui == 0 ? TeamResult.Red : (ui == 1 ? TeamResult.Blue : TeamResult.Unknown);
                if (val is Enum) { var name = val.ToString().ToLowerInvariant(); if (name.Contains("red")) return TeamResult.Red; if (name.Contains("blue")) return TeamResult.Blue; }
                var s = val.ToString().ToLowerInvariant(); if (s.Contains("red")) return TeamResult.Red; if (s.Contains("blue")) return TeamResult.Blue;
            }
            catch { }
            return TeamResult.Unknown;
        }

        private static string ResolvePlayerObjectKey(object player, ulong fallbackClientId)
        {
            try
            {
                if (player != null)
                {
                    var pid = TryGetPlayerIdNoFallback(player);
                    if (!string.IsNullOrEmpty(pid))
                    {
                        var resolved = ResolveStoredIdToSteam(pid);
                        if (!string.IsNullOrEmpty(resolved)) return resolved;
                        return pid;
                    }
                    if (TryGetClientId(player, out var cid))
                    {
                        if (cid == 0 && fallbackClientId == 0) return null;
                        var clientKey = $"clientId:{cid}";
                        var resolved = ResolveStoredIdToSteam(clientKey);
                        if (!string.IsNullOrEmpty(resolved)) return resolved;
                        return clientKey;
                    }
                }
            }
            catch { }
            return fallbackClientId == 0 ? null : $"clientId:{fallbackClientId}";
        }

        private static bool TryGetPlayerNumber(object player, out int playerNumber)
        {
            playerNumber = 0;
            if (player == null) return false;

            var type = player.GetType();
            string[] names = { "Number", "number", "PlayerNumber", "playerNumber", "JerseyNumber", "jerseyNumber", "UniformNumber", "uniformNumber" };
            foreach (var name in names)
            {
                var property = type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (property != null)
                {
                    var value = property.GetValue(player);
                    if (TryConvertToInt(value, out playerNumber)) return true;
                }

                var field = type.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    var value = field.GetValue(player);
                    if (TryConvertToInt(value, out playerNumber)) return true;
                }
            }

            return false;
        }

        private static int ResolveParticipantPlayerNumber(RankedParticipant participant)
        {
            if (participant == null || participant.clientId == 0)
            {
                return 0;
            }

            if (TryGetPlayerByClientId(participant.clientId, out var player) && TryGetPlayerNumber(player, out var playerNumber))
            {
                return playerNumber;
            }

            return 0;
        }

        private static bool TryGetPlayerManager(out object manager)
        {
            manager = null;
            var managerType = FindTypeByName("PlayerManager", "Puck.PlayerManager");
            if (managerType == null) return false;
            manager = GetManagerInstance(managerType);
            return manager != null;
        }

        private static bool TryGetLocalPlayer(out object player, out ulong clientId)
        {
            player = null;
            clientId = 0;

            if (!TryGetPlayerManager(out var manager)) return false;
            try
            {
                var method = manager.GetType().GetMethod("GetLocalPlayer", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (method == null) return false;

                player = method.Invoke(manager, null);
                if (player == null) return false;

                TryGetClientId(player, out clientId);
                return true;
            }
            catch
            {
                player = null;
                clientId = 0;
                return false;
            }
        }

        private static bool TryGetPlayerByClientId(ulong clientId, out object player)
        {
            player = null;
            if (!TryGetPlayerManager(out var manager)) return false;
            try
            {
                var method = manager.GetType().GetMethod("GetPlayerByClientId", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (method == null) return false;
                player = method.Invoke(manager, new object[] { clientId });
                return player != null;
            }
            catch { }
            return false;
        }

        private static bool TryGetPlayerTeamFromManager(ulong clientId, out TeamResult team)
        {
            team = TeamResult.Unknown;
            if (!TryGetPlayerManager(out var manager)) return false;

            if (TryGetPlayerByClientId(clientId, out var player))
            {
                if (TryGetPlayerTeam(player, out team) && team != TeamResult.Unknown) return true;
            }

            var enumType = FindTypeByName("PlayerTeam", "Puck.PlayerTeam");
            if (enumType == null) return false;
            var managerType = manager.GetType();
            var getPlayersByTeam = managerType.GetMethod("GetPlayersByTeam", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var getSpawnedPlayersByTeam = managerType.GetMethod("GetSpawnedPlayersByTeam", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var candidate in new[] { TeamResult.Red, TeamResult.Blue })
            {
                try
                {
                    var enumValue = Enum.Parse(enumType, candidate == TeamResult.Red ? "Red" : "Blue", true);
                    System.Collections.IEnumerable list = null;
                    if (getPlayersByTeam != null)
                    {
                        list = getPlayersByTeam.Invoke(manager, new object[] { enumValue, true }) as System.Collections.IEnumerable;
                    }
                    if (list == null && getSpawnedPlayersByTeam != null)
                    {
                        list = getSpawnedPlayersByTeam.Invoke(manager, new object[] { enumValue, true }) as System.Collections.IEnumerable;
                    }
                    if (list == null) continue;
                    foreach (var p in list)
                    {
                        if (TryGetClientId(p, out var cid) && cid == clientId)
                        {
                            team = candidate;
                            return true;
                        }
                    }
                }
                catch { }
            }
            return false;
        }

        private static bool TryIsGoalie(object player, out bool isGoalie)
        {
            isGoalie = false;
            if (player == null) return false;
            var t = player.GetType();
            string[] names = { "IsGoalie", "isGoalie", "IsGoalkeeper", "isGoalkeeper", "IsGk", "isGk", "IsGoal", "isGoal", "Goalie", "goalie" };
            foreach (var n in names)
            {
                object val = null;
                var prop = t.GetProperty(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (prop != null) val = prop.GetValue(player);
                if (val == null)
                {
                    var field = t.GetField(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (field != null) val = field.GetValue(player);
                }

                if (val is bool b) { isGoalie = b; return true; }
                if (TryConvertToBool(val, out b)) { isGoalie = b; return true; }
            }

            string[] roleNames = { "Role", "role", "Position", "position" };
            foreach (var n in roleNames)
            {
                object val = null;
                var prop = t.GetProperty(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (prop != null) val = prop.GetValue(player);
                if (val == null)
                {
                    var field = t.GetField(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (field != null) val = field.GetValue(player);
                }

                if (TryValueRepresentsGoalieRole(val))
                {
                    isGoalie = true;
                    return true;
                }
            }

            if (TryIsGoalieByClaimedPosition(player, out isGoalie))
            {
                return true;
            }

            return false;
        }

        private static bool TryValueRepresentsGoalieRole(object value)
        {
            if (value == null)
            {
                return false;
            }

            try
            {
                if (TryConvertToInt(value, out var numericRole))
                {
                    return numericRole == 2;
                }

                var text = ExtractSimpleValueToString(value) ?? value.ToString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return false;
                }

                var normalized = text.Trim();
                if (normalized.Equals("G", StringComparison.OrdinalIgnoreCase)
                    || normalized.Equals("GK", StringComparison.OrdinalIgnoreCase)
                    || normalized.IndexOf("goal", StringComparison.OrdinalIgnoreCase) >= 0
                    || normalized.IndexOf("keeper", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static bool TryIsGoalieByClaimedPosition(object player, out bool isGoalie)
        {
            isGoalie = false;
            if (player == null)
            {
                return false;
            }

            try
            {
                var ppmType = FindTypeByName("PlayerPositionManager", "Puck.PlayerPositionManager");
                if (ppmType == null)
                {
                    return false;
                }

                var ppm = GetManagerInstance(ppmType);
                if (ppm == null)
                {
                    return false;
                }

                var allPositionsProperty = ppmType.GetProperty("AllPositions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var allPositions = allPositionsProperty?.GetValue(ppm) as System.Collections.IEnumerable;
                if (allPositions == null)
                {
                    return false;
                }

                foreach (var position in allPositions)
                {
                    if (!IsGoaliePosition(position) || !IsPositionClaimedByPlayer(position, player))
                    {
                        continue;
                    }

                    isGoalie = true;
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static bool IsGoaliePosition(object position)
        {
            if (position == null)
            {
                return false;
            }

            try
            {
                var posType = position.GetType();
                var roleValue = posType.GetProperty("Role", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(position)
                    ?? posType.GetField("Role", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(position)
                    ?? posType.GetProperty("role", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(position)
                    ?? posType.GetField("role", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(position);
                if (TryValueRepresentsGoalieRole(roleValue))
                {
                    return true;
                }

                var nameValue = posType.GetProperty("Name", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(position)
                    ?? posType.GetField("Name", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(position)
                    ?? posType.GetProperty("name", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(position)
                    ?? posType.GetField("name", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(position);
                return TryValueRepresentsGoalieRole(nameValue);
            }
            catch { }

            return false;
        }

        private static bool IsPositionClaimedByPlayer(object position, object player)
        {
            if (position == null || player == null)
            {
                return false;
            }

            try
            {
                var posType = position.GetType();
                object owner = posType.GetProperty("ClaimedBy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(position)
                    ?? posType.GetField("ClaimedBy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(position)
                    ?? posType.GetProperty("claimedBy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(position)
                    ?? posType.GetField("claimedBy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(position)
                    ?? posType.GetProperty("Player", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(position)
                    ?? posType.GetField("Player", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(position)
                    ?? posType.GetProperty("player", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(position)
                    ?? posType.GetField("player", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(position);

                if (owner == null)
                {
                    return false;
                }

                if (ReferenceEquals(owner, player))
                {
                    return true;
                }

                if (TryGetClientId(owner, out var ownerClientId) && TryGetClientId(player, out var playerClientId) && ownerClientId != 0 && ownerClientId == playerClientId)
                {
                    return true;
                }

                if (owner is Component ownerComponent && player is Component playerComponent)
                {
                    return ownerComponent == playerComponent;
                }

                return AreEquivalentPlayerObjects(owner, player);
            }
            catch { }

            return false;
        }
    }
}
