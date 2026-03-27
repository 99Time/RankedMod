using System;
using System.Collections.Generic;
using UnityEngine;

namespace schrader.Server
{
    public static partial class RankedSystem
    {
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
                string[] names = { "SteamId", "steamId", "SteamID", "steamID", "SteamId64", "Steam64Id", "steamID64", "AccountId", "m_SteamId", "Id", "id" };
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

        private static bool TryGetPlayerManager(out object manager)
        {
            manager = null;
            var managerType = FindTypeByName("PlayerManager", "Puck.PlayerManager");
            if (managerType == null) return false;
            manager = GetManagerInstance(managerType);
            return manager != null;
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
                if (val is string s && s.IndexOf("goal", StringComparison.OrdinalIgnoreCase) >= 0) { isGoalie = true; return true; }
            }

            return false;
        }
    }
}
