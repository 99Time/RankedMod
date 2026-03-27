using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace schrader.Server
{
    public static partial class RankedSystem
    {
        private static string GetMmrPath()
        {
            var root = GetGameRootPath();
            var dir = Path.Combine(root, "UserData");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "schrader_ranked_mmr.json");
        }

        public static void LoadMmr()
        {
            lock (mmrLock)
            {
                var path = GetMmrPath();
                if (!File.Exists(path)) return;
                var json = File.ReadAllText(path);
                var loaded = JsonConvert.DeserializeObject<MmrFile>(json);
                if (loaded != null && loaded.players != null)
                {
                    mmrFile = loaded;
                }
            }
        }

        public static void SaveMmr()
        {
            lock (mmrLock)
            {
                var path = GetMmrPath();
                var json = JsonConvert.SerializeObject(mmrFile, Formatting.Indented);
                File.WriteAllText(path, json);
            }
        }

        public static void ApplyRankedResults(TeamResult winner)
        {
            try
            {
                RefreshRankedParticipantsFromLiveState();
                var rng = new System.Random();
                foreach (var p in rankedParticipants)
                {
                    if (IsDummyParticipant(p)) continue;
                    var isWin = p.team == winner;
                    var delta = isWin ? rng.Next(24, 32) : -rng.Next(14, 22);

                    lock (mmrLock)
                    {
                        var key = ResolveParticipantIdToKey(p);
                        if (!mmrFile.players.TryGetValue(key, out var entry)) { entry = new MmrEntry(); mmrFile.players[key] = entry; }
                        entry.mmr = Math.Max(0, entry.mmr + delta);
                        if (isWin) entry.wins++; else entry.losses++;
                        entry.lastUpdated = DateTime.UtcNow.ToString("o");
                    }

                    SendSystemChatToClient($"<size=14><color=#00ff00>Ranked</color> {p.displayName}: {delta:+#;-#;0} MMR (now {GetMmr(p.playerId)})</size>", p.clientId);
                }

                string sbSteamId = null; string sbName = null; TeamResult sbTeam = TeamResult.Unknown; int sbGoals = 0; int sbAssists = 0;
                if (TryGetMvpFromScoreboard(out sbSteamId, out sbName, out sbTeam, out sbGoals, out sbAssists))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] MVP scoreboard candidate {sbSteamId ?? sbName} (goals {sbGoals}, assists {sbAssists}, team {sbTeam})");
                    var mvpFound = rankedParticipants.FirstOrDefault(p =>
                        string.Equals(ResolveParticipantIdToKey(p), sbSteamId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(p.playerId, sbSteamId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(p.displayName, sbSteamId, StringComparison.OrdinalIgnoreCase)
                        || (!string.IsNullOrEmpty(sbName) && string.Equals(p.displayName, sbName, StringComparison.OrdinalIgnoreCase)));
                    if (mvpFound != null)
                    {
                        Debug.Log($"[{Constants.MOD_NAME}] MVP scoreboard matched participant {mvpFound.displayName}");
                        var extra = (mvpFound.team == winner) ? rng.Next(8, 13) : rng.Next(3, 6);
                        var key = ResolveParticipantIdToKey(mvpFound);
                        lock (mmrLock)
                        {
                            if (!mmrFile.players.TryGetValue(key, out var entry)) { entry = new MmrEntry(); mmrFile.players[key] = entry; }
                            entry.mmr = Math.Max(0, entry.mmr + extra);
                            entry.lastUpdated = DateTime.UtcNow.ToString("o");
                        }
                        SendSystemChatToAll($"<size=17><color=#66ccff>MVP</color> {mvpFound.displayName}: +{extra} MMR (goals: {sbGoals}, assists: {sbAssists})</size>");
                        SaveMmr();
                        rankedActive = false; rankedParticipants.Clear();
                        return;
                    }
                    if (!string.IsNullOrEmpty(sbSteamId) && sbTeam != TeamResult.Unknown)
                    {
                        var extra = (sbTeam == winner) ? rng.Next(8, 13) : rng.Next(3, 6);
                        lock (mmrLock)
                        {
                            if (!mmrFile.players.TryGetValue(sbSteamId, out var entry)) { entry = new MmrEntry(); mmrFile.players[sbSteamId] = entry; }
                            entry.mmr = Math.Max(0, entry.mmr + extra);
                            entry.lastUpdated = DateTime.UtcNow.ToString("o");
                        }
                        var display = !string.IsNullOrEmpty(sbName) ? sbName : sbSteamId;
                        SendSystemChatToAll($"<size=14><color=#66ccff>MVP</color> {display}: +{extra} MMR (goals: {sbGoals}, assists: {sbAssists})</size>");
                        SaveMmr();
                        rankedActive = false; rankedParticipants.Clear();
                        return;
                    }
                    Debug.Log($"[{Constants.MOD_NAME}] MVP scoreboard candidate {sbSteamId ?? sbName} did not match any ranked participant.");
                }

                string mvpId = null; int mvpGoals = 0;
                lock (playerGoalLock)
                {
                    foreach (var p in rankedParticipants)
                    {
                        if (IsDummyParticipant(p)) continue;
                        if (string.IsNullOrEmpty(p.playerId)) continue;
                        if (playerGoalCounts.TryGetValue(p.playerId, out var g) || playerGoalCounts.TryGetValue(ResolveParticipantIdToKey(p), out g))
                        {
                            if (g > mvpGoals) { mvpGoals = g; mvpId = ResolveParticipantIdToKey(p); }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(mvpId) && mvpGoals > 0)
                {
                    var mvp = rankedParticipants.FirstOrDefault(r => r.playerId == mvpId);
                    if (mvp != null)
                    {
                        var extra = (mvp.team == winner) ? rng.Next(8, 13) : rng.Next(3, 6);
                        lock (mmrLock)
                        {
                            if (!mmrFile.players.TryGetValue(mvpId, out var entry)) { entry = new MmrEntry(); mmrFile.players[mvpId] = entry; }
                            entry.mmr = Math.Max(0, entry.mmr + extra);
                            entry.lastUpdated = DateTime.UtcNow.ToString("o");
                        }
                        SendSystemChatToAll($"<size=14><color=#66ccff>MVP</color> {mvp.displayName}: +{extra} MMR (goals: {mvpGoals})</size>");
                    }
                    else
                    {
                        Debug.Log($"[{Constants.MOD_NAME}] MVP fallback left no candidate (mvpId={mvpId ?? "null"}, goals={mvpGoals})");
                    }
                }

                SaveMmr();
                ResetRankedState(true, true);
            }
            catch { }
        }

        public static int GetMmr(string playerId)
        {
            lock (mmrLock)
            {
                if (mmrFile.players.TryGetValue(playerId, out var entry)) return entry.mmr;
                var resolved = ResolveStoredIdToSteam(playerId);
                if (!string.IsNullOrEmpty(resolved) && mmrFile.players.TryGetValue(resolved, out entry)) return entry.mmr;
            }
            return 350;
        }
    }
}
