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
                var matchResult = BuildMatchResultMessage(winner);
                if (matchResult != null && matchResult.IsVisible)
                {
                    Debug.Log($"[{Constants.MOD_NAME}] Match complete -> publishing results for {matchResult.Players?.Length ?? 0} players. Winner={winner}");
                    RankedOverlayNetwork.PublishMatchResult(matchResult);
                    SendSystemChatToAll("<size=14><color=#66ccff>Match complete</color> post-match results are now available.</size>");

                    if (BeginPostMatchLock(matchResult))
                    {
                        return;
                    }
                }

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

        public static bool TryGetMmrValue(string playerId, out int mmr)
        {
            mmr = 0;
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return false;
            }

            lock (mmrLock)
            {
                if (mmrFile.players.TryGetValue(playerId, out var entry))
                {
                    mmr = entry.mmr;
                    return true;
                }

                var resolved = ResolveStoredIdToSteam(playerId);
                if (!string.IsNullOrEmpty(resolved) && mmrFile.players.TryGetValue(resolved, out entry))
                {
                    mmr = entry.mmr;
                    return true;
                }
            }

            return false;
        }
    }
}
