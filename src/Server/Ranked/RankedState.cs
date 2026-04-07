using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace schrader.Server
{
    public static partial class RankedSystem
    {
        private static bool mmrLoadAttempted;
        private static bool chatStyleLoadAttempted;
        private static bool legacyChatStyleMigrationAttempted;
        private static bool starProgressLoadAttempted;
        private static bool legacyStarProgressMigrationAttempted;
        private static readonly HashSet<string> loggedMissingMmrKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object chatStyleLock = new object();
        private static readonly object starProgressLock = new object();
        private static readonly Dictionary<string, string> ChatColorAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "red", "#ff5555" },
            { "blue", "#55aaff" },
            { "green", "#55dd88" },
            { "yellow", "#ffd966" },
            { "orange", "#ff9966" },
            { "purple", "#b388ff" },
            { "pink", "#ff88cc" },
            { "cyan", "#66e0ff" },
            { "teal", "#4dd0c8" },
            { "white", "#f2f2f2" },
            { "gold", "#f5c451" }
        };
        private static readonly HashSet<string> RainbowColorAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "rgb",
            "rainbow"
        };
        private static ChatStyleFile chatStyleFile = new ChatStyleFile();
        private static StarProgressFile starProgressFile = new StarProgressFile();

        private sealed class ChatStyleEntry
        {
            public string nameColorHex;
            public string messageColorHex;
            public string lastUpdated;
        }

        private sealed class ChatStyleFile
        {
            public int version = 1;
            public Dictionary<string, ChatStyleEntry> players = new Dictionary<string, ChatStyleEntry>();
        }

        private sealed class StarProgressEntry
        {
            public int starPoints;
            public int winStreak;
            public string lastUpdated;
        }

        private sealed class StarProgressFile
        {
            public int version = 1;
            public Dictionary<string, StarProgressEntry> players = new Dictionary<string, StarProgressEntry>();
        }

        private static string GetMmrPath()
        {
            var root = GetGameRootPath();
            var dir = Path.Combine(root, "UserData");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "schrader_ranked_mmr.json");
        }

        private static string GetSharedChatStylePath()
        {
            var root = GetGameRootPath();
            var dir = Path.Combine(root, "UserData");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "schrader_ranked_chat_colors.json");
        }

        private static string GetStarProgressPath()
        {
            var root = GetGameRootPath();
            var dir = Path.Combine(root, "UserData");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "schrader_ranked_stars.json");
        }

        private static void EnsureMmrLoaded()
        {
            if (mmrLoadAttempted)
            {
                return;
            }

            LoadMmr();
        }

        private static void EnsureChatStylesLoaded()
        {
            if (chatStyleLoadAttempted)
            {
                TryMigrateLegacyChatStyles();
                return;
            }

            LoadChatStyles();
        }

        private static void EnsureStarProgressLoaded()
        {
            if (starProgressLoadAttempted)
            {
                TryMigrateLegacyStarProgress();
                return;
            }

            LoadStarProgress();
        }

        private static void LoadChatStyles()
        {
            lock (chatStyleLock)
            {
                if (chatStyleLoadAttempted)
                {
                    return;
                }

                chatStyleLoadAttempted = true;
                var path = GetSharedChatStylePath();
                if (!File.Exists(path))
                {
                    chatStyleFile = new ChatStyleFile();
                }
                else
                {
                    try
                    {
                        var json = File.ReadAllText(path);
                        var loaded = JsonConvert.DeserializeObject<ChatStyleFile>(json);
                        chatStyleFile = loaded != null && loaded.players != null ? loaded : new ChatStyleFile();
                    }
                    catch (Exception ex)
                    {
                        chatStyleFile = new ChatStyleFile();
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [CHAT] Failed to load shared chat color file: {ex.Message}");
                    }
                }
            }

            TryMigrateLegacyChatStyles();
        }

        private static void SaveChatStyles()
        {
            try
            {
                lock (chatStyleLock)
                {
                    var path = GetSharedChatStylePath();
                    var json = JsonConvert.SerializeObject(chatStyleFile, Formatting.Indented);
                    File.WriteAllText(path, json);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Debug.LogError($"[{Constants.MOD_NAME}] [CHAT] Failed to save shared chat colors: {ex.Message}");
                }
                catch { }
            }
        }

        private static void LoadStarProgress()
        {
            lock (starProgressLock)
            {
                if (starProgressLoadAttempted)
                {
                    return;
                }

                starProgressLoadAttempted = true;
                var path = GetStarProgressPath();
                if (!File.Exists(path))
                {
                    starProgressFile = new StarProgressFile();
                }
                else
                {
                    try
                    {
                        var json = File.ReadAllText(path);
                        var loaded = JsonConvert.DeserializeObject<StarProgressFile>(json);
                        starProgressFile = loaded != null && loaded.players != null ? loaded : new StarProgressFile();
                    }
                    catch (Exception ex)
                    {
                        starProgressFile = new StarProgressFile();
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [STARS] Failed to load shared star progress file: {ex.Message}");
                    }
                }
            }

            TryMigrateLegacyStarProgress();
        }

        private static void SaveStarProgress()
        {
            try
            {
                lock (starProgressLock)
                {
                    var path = GetStarProgressPath();
                    var json = JsonConvert.SerializeObject(starProgressFile, Formatting.Indented);
                    File.WriteAllText(path, json);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Debug.LogError($"[{Constants.MOD_NAME}] [STARS] Failed to save star progress: {ex.Message}");
                }
                catch { }
            }
        }

        private static void TryMigrateLegacyChatStyles()
        {
            if (legacyChatStyleMigrationAttempted)
            {
                return;
            }

            Dictionary<string, string> legacyNameColors = null;

            EnsureMmrLoaded();
            lock (mmrLock)
            {
                legacyNameColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (mmrFile?.players != null)
                {
                    foreach (var pair in mmrFile.players)
                    {
                        var normalizedSteamId = NormalizeResolvedPlayerKey(pair.Key);
                        var normalizedNameColor = NormalizeStoredChatColorSpec(pair.Value?.chatColorHex);
                        if (!IsSteamIdentityKey(normalizedSteamId) || string.IsNullOrWhiteSpace(normalizedNameColor))
                        {
                            continue;
                        }

                        legacyNameColors[normalizedSteamId] = normalizedNameColor;
                    }
                }
            }

            var shouldSave = false;
            lock (chatStyleLock)
            {
                if (legacyChatStyleMigrationAttempted)
                {
                    return;
                }

                legacyChatStyleMigrationAttempted = true;
                if (legacyNameColors != null)
                {
                    foreach (var pair in legacyNameColors)
                    {
                        if (!chatStyleFile.players.TryGetValue(pair.Key, out var entry) || entry == null)
                        {
                            entry = new ChatStyleEntry();
                            chatStyleFile.players[pair.Key] = entry;
                        }

                        if (!string.IsNullOrWhiteSpace(entry.nameColorHex))
                        {
                            continue;
                        }

                        entry.nameColorHex = pair.Value;
                        if (string.IsNullOrWhiteSpace(entry.lastUpdated))
                        {
                            entry.lastUpdated = DateTime.UtcNow.ToString("o");
                        }
                        shouldSave = true;
                    }
                }
            }

            if (shouldSave)
            {
                SaveChatStyles();
            }
        }

        private static void TryMigrateLegacyStarProgress()
        {
            if (legacyStarProgressMigrationAttempted)
            {
                return;
            }

            Dictionary<string, StarProgressEntry> legacyProgress = null;

            EnsureMmrLoaded();
            lock (mmrLock)
            {
                legacyProgress = new Dictionary<string, StarProgressEntry>(StringComparer.OrdinalIgnoreCase);
                if (mmrFile?.players != null)
                {
                    foreach (var pair in mmrFile.players)
                    {
                        var normalizedSteamId = NormalizeResolvedPlayerKey(pair.Key);
                        if (!IsSteamIdentityKey(normalizedSteamId))
                        {
                            continue;
                        }

                        var starPoints = Mathf.Max(0, pair.Value?.starPoints ?? 0);
                        var winStreak = Mathf.Max(0, pair.Value?.winStreak ?? 0);
                        if (starPoints <= 0 && winStreak <= 0)
                        {
                            continue;
                        }

                        legacyProgress[normalizedSteamId] = new StarProgressEntry
                        {
                            starPoints = starPoints,
                            winStreak = winStreak,
                            lastUpdated = pair.Value?.lastUpdated
                        };
                    }
                }
            }

            var shouldSave = false;
            lock (starProgressLock)
            {
                if (legacyStarProgressMigrationAttempted)
                {
                    return;
                }

                legacyStarProgressMigrationAttempted = true;
                if (legacyProgress != null)
                {
                    foreach (var pair in legacyProgress)
                    {
                        if (!starProgressFile.players.TryGetValue(pair.Key, out var entry) || entry == null)
                        {
                            starProgressFile.players[pair.Key] = pair.Value;
                            shouldSave = true;
                            continue;
                        }

                        if (entry.starPoints > 0 || entry.winStreak > 0)
                        {
                            continue;
                        }

                        entry.starPoints = pair.Value.starPoints;
                        entry.winStreak = pair.Value.winStreak;
                        entry.lastUpdated = string.IsNullOrWhiteSpace(entry.lastUpdated) ? pair.Value.lastUpdated : entry.lastUpdated;
                        shouldSave = true;
                    }
                }
            }

            if (shouldSave)
            {
                SaveStarProgress();
            }
        }

        private static bool TryGetStoredStarProgressEntry(string playerKey, out StarProgressEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(playerKey))
            {
                return false;
            }

            EnsureStarProgressLoaded();
            lock (starProgressLock)
            {
                if (starProgressFile.players.TryGetValue(playerKey, out entry) && entry != null)
                {
                    return true;
                }

                var resolved = ResolveStoredIdToSteam(playerKey);
                if (!string.IsNullOrWhiteSpace(resolved) && starProgressFile.players.TryGetValue(resolved, out entry) && entry != null)
                {
                    return true;
                }
            }

            entry = null;
            return false;
        }

        private static StarProgressEntry GetOrCreateStarProgressEntry(string playerKey)
        {
            if (string.IsNullOrWhiteSpace(playerKey))
            {
                return null;
            }

            EnsureStarProgressLoaded();
            lock (starProgressLock)
            {
                if (!starProgressFile.players.TryGetValue(playerKey, out var entry) || entry == null)
                {
                    entry = new StarProgressEntry();
                    starProgressFile.players[playerKey] = entry;
                }

                return entry;
            }
        }

        internal static int GetStoredStarPointsForPlayerKey(string playerKey)
        {
            return TryGetStoredStarProgressEntry(playerKey, out var entry) ? Mathf.Max(0, entry.starPoints) : 0;
        }

        internal static int GetStoredWinStreakForPlayerKey(string playerKey)
        {
            return TryGetStoredStarProgressEntry(playerKey, out var entry) ? Mathf.Max(0, entry.winStreak) : 0;
        }

        internal static void UpdateStoredStarsForPlayerKey(string playerKey, bool didWin, int performanceModifier, bool isMvp, bool wasLateJoiner, int maxStarPoints)
        {
            if (string.IsNullOrWhiteSpace(playerKey))
            {
                return;
            }

            EnsureStarProgressLoaded();
            lock (starProgressLock)
            {
                if (!starProgressFile.players.TryGetValue(playerKey, out var entry) || entry == null)
                {
                    entry = new StarProgressEntry();
                    starProgressFile.players[playerKey] = entry;
                }

                if (didWin)
                {
                    entry.winStreak = Mathf.Max(0, entry.winStreak) + 1;
                    var starGain = entry.winStreak >= 2 ? 1 : 0;
                    if (starGain == 0 && (isMvp || performanceModifier >= 2))
                    {
                        starGain = 1;
                    }

                    entry.starPoints = Mathf.Clamp(entry.starPoints + starGain, 0, maxStarPoints);
                }
                else
                {
                    entry.winStreak = 0;
                    if (performanceModifier < 1 && !isMvp && !wasLateJoiner)
                    {
                        entry.starPoints = Mathf.Max(0, entry.starPoints - 1);
                    }
                }

                entry.lastUpdated = DateTime.UtcNow.ToString("o");
            }

            SaveStarProgress();
        }

        public static void LoadMmr()
        {
            lock (mmrLock)
            {
                if (mmrLoadAttempted)
                {
                    return;
                }

                mmrLoadAttempted = true;
                var path = GetMmrPath();
                if (!File.Exists(path))
                {
                    mmrFile = new MmrFile();
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [MMR] File not found: {path}");
                    return;
                }

                var json = File.ReadAllText(path);
                var loaded = JsonConvert.DeserializeObject<MmrFile>(json);
                if (loaded != null && loaded.players != null)
                {
                    mmrFile = loaded;
                    Debug.Log($"[{Constants.MOD_NAME}] [MMR] Loaded cached data from {path}. Entries={mmrFile.players.Count}");
                    return;
                }

                mmrFile = new MmrFile();
                Debug.LogWarning($"[{Constants.MOD_NAME}] [MMR] JSON loaded but no player entries were found in {path}");
            }
        }

        public static void SaveMmr()
        {
            try
            {
                lock (mmrLock)
                {
                    var path = GetMmrPath();
                    var json = JsonConvert.SerializeObject(mmrFile, Formatting.Indented);
                    File.WriteAllText(path, json);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Debug.LogError($"[{Constants.MOD_NAME}] [MMR] Save failed: {ex.Message}");
                }
                catch { }
            }
        }

        public static void ApplyRankedResults(TeamResult winner)
        {
            try
            {
                Debug.Log($"[{Constants.MOD_NAME}] [POSTMATCH] Match ended. Winner={winner}.");
                Debug.Log($"[{Constants.MOD_NAME}] [POSTMATCH] Building result payload.");
                var postMatchLockStarted = false;
                var matchResult = BuildMatchResultMessage(winner);
                if (matchResult != null && matchResult.IsVisible)
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [POSTMATCH] Result payload built for {matchResult.Players?.Length ?? 0} players.");
                    Debug.Log($"[{Constants.MOD_NAME}] [POSTMATCH] Publishing result payload.");
                    postMatchLockStarted = BeginPostMatchLock(matchResult);
                    RankedOverlayNetwork.PublishMatchResult(matchResult);
                    var discordMatchResult = BuildDiscordMatchResultData(matchResult);
                    if (discordMatchResult != null && TryGetCurrentServerName(out var serverName))
                    {
                        _ = SendMatchResultToDiscordAsync(MatchResultDiscordWebhookUrl, discordMatchResult, serverName);
                    }
                    else if (discordMatchResult != null)
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Skipped match result webhook because the server name could not be resolved.");
                    }
                    Debug.Log($"[{Constants.MOD_NAME}] [POSTMATCH] Result payload sent.");
                    SendSystemChatToAll("<size=14><color=#66ccff>Match complete</color> post-match results are now available.</size>");
                }
                else
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [POSTMATCH] Result payload built for 0 players.");
                }

                ResetRankedState(true, true, preservePostMatchLock: postMatchLockStarted);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] ApplyRankedResults failed: {ex.Message}");
            }
        }

        public static int GetMmr(string playerId)
        {
            EnsureMmrLoaded();
            lock (mmrLock)
            {
                if (mmrFile.players.TryGetValue(playerId, out var entry)) return entry.mmr;
                var resolved = ResolveStoredIdToSteam(playerId);
                if (!string.IsNullOrEmpty(resolved) && mmrFile.players.TryGetValue(resolved, out entry)) return entry.mmr;
            }
            return Constants.DEFAULT_MMR;
        }

        public static bool TryGetMmrValue(string playerId, out int mmr)
        {
            mmr = 0;
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return false;
            }

            EnsureMmrLoaded();

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

        private static int GetAuthoritativeMmrOrDefault(RankedParticipant participant, string preferredKey, out string canonicalKey)
        {
            EnsureMmrLoaded();
            canonicalKey = ResolveCanonicalMmrKey(participant, preferredKey, out _);
            if (string.IsNullOrWhiteSpace(canonicalKey))
            {
                lock (mmrLock)
                {
                    var unresolvedMarker = BuildMissingMmrLogKey(participant, preferredKey);
                    if (loggedMissingMmrKeys.Add(unresolvedMarker))
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [MMR] Could not resolve authoritative key for participant: {unresolvedMarker}");
                    }
                }

                return Constants.DEFAULT_MMR;
            }

            if (!string.IsNullOrWhiteSpace(canonicalKey))
            {
                lock (mmrLock)
                {
                    if (mmrFile.players.TryGetValue(canonicalKey, out var entry))
                    {
                        return entry.mmr;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(canonicalKey))
            {
                lock (mmrLock)
                {
                    if (loggedMissingMmrKeys.Add(canonicalKey))
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [MMR] Missing entry for key: {canonicalKey}");
                    }
                }
            }

            return Constants.DEFAULT_MMR;
        }

        private static bool HasStoredMmrEntry(string canonicalKey)
        {
            if (string.IsNullOrWhiteSpace(canonicalKey))
            {
                return false;
            }

            EnsureMmrLoaded();
            lock (mmrLock)
            {
                return mmrFile.players != null && mmrFile.players.ContainsKey(canonicalKey);
            }
        }

        public static string GetStoredNameColorHexForPlayer(object player, ulong fallbackClientId = 0)
        {
            var playerKey = NormalizeResolvedPlayerKey(TryGetPlayerIdNoFallback(player));
            if (!IsSteamIdentityKey(playerKey) && fallbackClientId != 0 && TryGetPlayerByClientId(fallbackClientId, out var livePlayer))
            {
                playerKey = NormalizeResolvedPlayerKey(TryGetPlayerIdNoFallback(livePlayer));
            }

            return GetStoredNameColorHex(playerKey);
        }

        public static string GetStoredMessageColorHexForPlayer(object player, ulong fallbackClientId = 0)
        {
            var playerKey = NormalizeResolvedPlayerKey(TryGetPlayerIdNoFallback(player));
            if (!IsSteamIdentityKey(playerKey) && fallbackClientId != 0 && TryGetPlayerByClientId(fallbackClientId, out var livePlayer))
            {
                playerKey = NormalizeResolvedPlayerKey(TryGetPlayerIdNoFallback(livePlayer));
            }

            return GetStoredMessageColorHex(playerKey);
        }

        public static bool TrySetNameColorForCommand(string rawTarget, string requestedColor, out string resultMessage)
        {
            return TrySetChatColorForCommand(rawTarget, requestedColor, true, "/setnamecolor", "name color", out resultMessage);
        }

        public static bool TrySetMessageColorForCommand(string rawTarget, string requestedColor, out string resultMessage)
        {
            return TrySetChatColorForCommand(rawTarget, requestedColor, false, "/setchatcolor", "chat color", out resultMessage);
        }

        private static bool TrySetChatColorForCommand(string rawTarget, string requestedColor, bool isNameColor, string commandName, string labelText, out string resultMessage)
        {
            resultMessage = null;

            if (!TryResolveChatColorTarget(rawTarget, commandName, out var steamId, out var playerName, out var resolveError))
            {
                resultMessage = resolveError ?? "Could not resolve that player to a SteamID.";
                return false;
            }

            if (!TrySetPersistentChatColor(steamId, requestedColor, isNameColor, commandName, out var normalizedColorHex, out var storageError))
            {
                resultMessage = storageError ?? $"Failed to save the {labelText}.";
                return false;
            }

            var label = string.IsNullOrWhiteSpace(playerName) ? steamId : playerName;
            if (string.IsNullOrWhiteSpace(normalizedColorHex))
            {
                resultMessage = $"cleared {labelText} for <b>{label}</b>.";
                return true;
            }

            if (IsRainbowColorSpec(normalizedColorHex))
            {
                resultMessage = $"set {labelText} for <b>{label}</b> to <b>RGB rainbow</b>.";
                return true;
            }

            resultMessage = $"set {labelText} for <b>{label}</b> to <color={normalizedColorHex}>{normalizedColorHex}</color>.";
            return true;
        }

        private static string GetStoredNameColorHex(string playerKey)
        {
            if (string.IsNullOrWhiteSpace(playerKey))
            {
                return null;
            }

            EnsureChatStylesLoaded();
            lock (chatStyleLock)
            {
                if (chatStyleFile.players.TryGetValue(playerKey, out var entry))
                {
                    return NormalizeStoredChatColorSpec(entry?.nameColorHex);
                }

                var resolved = ResolveStoredIdToSteam(playerKey);
                if (!string.IsNullOrWhiteSpace(resolved) && chatStyleFile.players.TryGetValue(resolved, out entry))
                {
                    return NormalizeStoredChatColorSpec(entry?.nameColorHex);
                }
            }

            return null;
        }

        private static string GetStoredMessageColorHex(string playerKey)
        {
            if (string.IsNullOrWhiteSpace(playerKey))
            {
                return null;
            }

            EnsureChatStylesLoaded();
            lock (chatStyleLock)
            {
                if (chatStyleFile.players.TryGetValue(playerKey, out var entry))
                {
                    return NormalizeStoredChatColorSpec(entry?.messageColorHex);
                }

                var resolved = ResolveStoredIdToSteam(playerKey);
                if (!string.IsNullOrWhiteSpace(resolved) && chatStyleFile.players.TryGetValue(resolved, out entry))
                {
                    return NormalizeStoredChatColorSpec(entry?.messageColorHex);
                }
            }

            return null;
        }

        private static bool TrySetPersistentChatColor(string steamId, string requestedColor, bool isNameColor, string commandName, out string normalizedColorHex, out string error)
        {
            normalizedColorHex = null;
            error = null;

            var normalizedSteamId = NormalizeResolvedPlayerKey(steamId);
            if (!IsSteamIdentityKey(normalizedSteamId))
            {
                error = "Target must resolve to a real SteamID.";
                return false;
            }

            if (!TryNormalizeRequestedChatColor(requestedColor, commandName, out normalizedColorHex, out error))
            {
                return false;
            }

            EnsureChatStylesLoaded();
            lock (chatStyleLock)
            {
                if (!chatStyleFile.players.TryGetValue(normalizedSteamId, out var entry) || entry == null)
                {
                    entry = new ChatStyleEntry();
                    chatStyleFile.players[normalizedSteamId] = entry;
                }

                if (isNameColor)
                {
                    entry.nameColorHex = normalizedColorHex;
                }
                else
                {
                    entry.messageColorHex = normalizedColorHex;
                }

                entry.lastUpdated = DateTime.UtcNow.ToString("o");
            }

            SaveChatStyles();
            return true;
        }

        private static bool TryNormalizeRequestedChatColor(string requestedColor, string commandName, out string normalizedColorHex, out string error)
        {
            normalizedColorHex = null;
            error = null;

            var clean = StripRichTextTags(requestedColor)?.Trim();
            if (string.IsNullOrWhiteSpace(clean))
            {
                error = $"Usage: {commandName} <player|steamId|#number> <color|rgb|#RRGGBB|reset>.";
                return false;
            }

            if (string.Equals(clean, "reset", StringComparison.OrdinalIgnoreCase)
                || string.Equals(clean, "clear", StringComparison.OrdinalIgnoreCase)
                || string.Equals(clean, "default", StringComparison.OrdinalIgnoreCase)
                || string.Equals(clean, "none", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (RainbowColorAliases.Contains(clean))
            {
                normalizedColorHex = "rgb";
                return true;
            }

            if (ChatColorAliases.TryGetValue(clean, out var aliasHex))
            {
                normalizedColorHex = aliasHex;
                return true;
            }

            var candidate = clean.StartsWith("#", StringComparison.Ordinal) ? clean.Substring(1) : clean;
            if (candidate.Length == 6 && candidate.All(Uri.IsHexDigit))
            {
                normalizedColorHex = $"#{candidate.ToLowerInvariant()}";
                return true;
            }

            error = "Unsupported color. Use rgb/rainbow, a named color like red/blue/gold, a #RRGGBB hex value, or reset.";
            return false;
        }

        private static string NormalizeStoredChatColorSpec(string storedColor)
        {
            if (string.IsNullOrWhiteSpace(storedColor))
            {
                return null;
            }

            var candidate = storedColor.Trim();
            if (RainbowColorAliases.Contains(candidate))
            {
                return "rgb";
            }

            if (candidate.StartsWith("#", StringComparison.Ordinal))
            {
                candidate = candidate.Substring(1);
            }

            if (candidate.Length == 6 && candidate.All(Uri.IsHexDigit))
            {
                return $"#{candidate.ToLowerInvariant()}";
            }

            return null;
        }

        public static bool IsRainbowColorSpec(string colorSpec)
        {
            return !string.IsNullOrWhiteSpace(colorSpec) && RainbowColorAliases.Contains(colorSpec.Trim());
        }

        private static bool TryResolveChatColorTarget(string rawTarget, string commandName, out string steamId, out string playerName, out string error)
        {
            steamId = null;
            playerName = null;
            error = null;

            var cleanTarget = StripRichTextTags(rawTarget)?.Trim();
            if (string.IsNullOrWhiteSpace(cleanTarget))
            {
                error = $"Usage: {commandName} <player|steamId|#number> <color|rgb|#RRGGBB|reset>.";
                return false;
            }

            var isExplicitNumberTarget = cleanTarget.StartsWith("#", StringComparison.Ordinal);
            var numericTarget = isExplicitNumberTarget ? cleanTarget.Substring(1).Trim() : cleanTarget;
            if (int.TryParse(numericTarget, out var playerNumber) && playerNumber >= 0)
            {
                if (TryResolveConnectedPlayerByNumber(playerNumber, out var numberedPlayer, out _, out var numberedKey, out var numberedName)
                    && IsSteamIdentityKey(numberedKey))
                {
                    steamId = numberedKey;
                    playerName = numberedName ?? TryGetPlainPlayerName(numberedPlayer) ?? steamId;
                    return true;
                }

                if (isExplicitNumberTarget)
                {
                    error = $"Could not find a live player with number {playerNumber}.";
                    return false;
                }
            }

            if (ulong.TryParse(cleanTarget, out var explicitSteamId) && explicitSteamId != 0)
            {
                steamId = explicitSteamId.ToString();
                if (TryFindConnectedPlayerBySteamId(steamId, out var steamPlayer, out var steamPlayerName))
                {
                    playerName = steamPlayerName ?? TryGetPlainPlayerName(steamPlayer) ?? steamId;
                }
                else
                {
                    playerName = steamId;
                }

                return true;
            }

            var exactMatches = new List<(string SteamId, string Name)>();
            var prefixMatches = new List<(string SteamId, string Name)>();
            foreach (var candidate in GetAllPlayers())
            {
                if (candidate == null)
                {
                    continue;
                }

                ulong candidateClientId = 0;
                TryGetClientId(candidate, out candidateClientId);
                var candidateSteamId = NormalizeResolvedPlayerKey(TryGetPlayerIdNoFallback(candidate));
                if (!IsSteamIdentityKey(candidateSteamId)
                    || IsReplayPlayerObject(candidate, candidateClientId)
                    || IsPracticeModeFakePlayerObject(candidate, candidateClientId, candidateSteamId))
                {
                    continue;
                }

                var candidateName = TryGetPlainPlayerName(candidate) ?? TryGetPlayerName(candidate);
                if (string.IsNullOrWhiteSpace(candidateName))
                {
                    continue;
                }

                if (string.Equals(candidateName, cleanTarget, StringComparison.OrdinalIgnoreCase))
                {
                    exactMatches.Add((candidateSteamId, candidateName));
                    continue;
                }

                if (candidateName.StartsWith(cleanTarget, StringComparison.OrdinalIgnoreCase))
                {
                    prefixMatches.Add((candidateSteamId, candidateName));
                }
            }

            if (exactMatches.Count == 1)
            {
                steamId = exactMatches[0].SteamId;
                playerName = exactMatches[0].Name;
                return true;
            }

            if (exactMatches.Count > 1)
            {
                error = "That player name is ambiguous. Use the SteamID or #playerNumber instead.";
                return false;
            }

            if (prefixMatches.Count == 1)
            {
                steamId = prefixMatches[0].SteamId;
                playerName = prefixMatches[0].Name;
                return true;
            }

            if (prefixMatches.Count > 1)
            {
                error = "That player prefix matches multiple live players. Use the SteamID or #playerNumber instead.";
                return false;
            }

            error = "Could not resolve that player. Use an exact name, SteamID, or #playerNumber.";
            return false;
        }

        private static bool TryFindConnectedPlayerBySteamId(string steamId, out object player, out string playerName)
        {
            player = null;
            playerName = null;

            var normalizedSteamId = NormalizeResolvedPlayerKey(steamId);
            if (!IsSteamIdentityKey(normalizedSteamId))
            {
                return false;
            }

            try
            {
                foreach (var candidate in GetAllPlayers())
                {
                    if (candidate == null)
                    {
                        continue;
                    }

                    ulong candidateClientId = 0;
                    TryGetClientId(candidate, out candidateClientId);
                    var candidateSteamId = NormalizeResolvedPlayerKey(TryGetPlayerIdNoFallback(candidate));
                    if (!string.Equals(candidateSteamId, normalizedSteamId, StringComparison.OrdinalIgnoreCase)
                        || IsReplayPlayerObject(candidate, candidateClientId)
                        || IsPracticeModeFakePlayerObject(candidate, candidateClientId, candidateSteamId))
                    {
                        continue;
                    }

                    player = candidate;
                    playerName = TryGetPlainPlayerName(candidate) ?? TryGetPlayerName(candidate) ?? normalizedSteamId;
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static string ResolveCanonicalMmrKey(RankedParticipant participant, string preferredKey, out bool promotedLegacyKey)
        {
            promotedLegacyKey = false;
            var liveSteamId = TryResolveLiveSteamMmrKey(participant);
            if (!string.IsNullOrWhiteSpace(liveSteamId))
            {
                return liveSteamId;
            }

            var participantKey = NormalizeResolvedPlayerKey(ResolveParticipantIdToKey(participant) ?? participant?.playerId);
            if (IsSteamIdentityKey(participantKey))
            {
                return participantKey;
            }

            var normalizedPreferredKey = NormalizeResolvedPlayerKey(StripRichTextTags(preferredKey)?.Trim());
            if (IsSteamIdentityKey(normalizedPreferredKey))
            {
                return normalizedPreferredKey;
            }

            if (participant != null && participant.clientId != 0)
            {
                promotedLegacyKey = BotManager.IsBotKey(participantKey) || (!string.IsNullOrWhiteSpace(normalizedPreferredKey) && BotManager.IsBotKey(normalizedPreferredKey));
                return $"clientId:{participant.clientId}";
            }

            if (TryResolveFallbackClientIdMmrKey(normalizedPreferredKey, out var fallbackClientKey))
            {
                promotedLegacyKey = !string.Equals(fallbackClientKey, normalizedPreferredKey, StringComparison.OrdinalIgnoreCase);
                return fallbackClientKey;
            }

            if (TryResolveFallbackClientIdMmrKey(participantKey, out fallbackClientKey))
            {
                promotedLegacyKey = true;
                return fallbackClientKey;
            }

            return null;
        }

        private static string TryResolveLiveSteamMmrKey(RankedParticipant participant)
        {
            if (participant == null || participant.clientId == 0 || !TryGetPlayerByClientId(participant.clientId, out var livePlayer))
            {
                return null;
            }

            var liveSteamId = NormalizeResolvedPlayerKey(TryGetPlayerIdNoFallback(livePlayer));
            return IsSteamIdentityKey(liveSteamId) ? liveSteamId : null;
        }

        private static string ResolveParticipantSteamIdForUi(RankedParticipant participant, string preferredResolvedKey = null)
        {
            var liveSteamId = TryResolveLiveSteamMmrKey(participant);
            if (IsSteamIdentityKey(liveSteamId))
            {
                return liveSteamId;
            }

            var participantKey = NormalizeResolvedPlayerKey(ResolveParticipantIdToKey(participant) ?? participant?.playerId);
            if (IsSteamIdentityKey(participantKey))
            {
                return participantKey;
            }

            var preferredKey = NormalizeResolvedPlayerKey(StripRichTextTags(preferredResolvedKey)?.Trim());
            if (IsSteamIdentityKey(preferredKey))
            {
                return preferredKey;
            }

            return null;
        }

        private static bool TryResolveFallbackClientIdMmrKey(string candidateKey, out string clientIdKey)
        {
            clientIdKey = null;
            if (string.IsNullOrWhiteSpace(candidateKey))
            {
                return false;
            }

            if (IsClientIdFallbackKey(candidateKey))
            {
                clientIdKey = candidateKey;
                return true;
            }

            if (BotManager.IsBotKey(candidateKey)
                && BotManager.TryGetBotParticipant(candidateKey, out var botParticipant)
                && botParticipant != null
                && botParticipant.clientId != 0)
            {
                clientIdKey = $"clientId:{botParticipant.clientId}";
                return true;
            }

            return false;
        }

        private static bool IsSteamIdentityKey(string candidateKey)
        {
            return !string.IsNullOrWhiteSpace(candidateKey)
                && LooksLikeIdentityKey(candidateKey)
                && !IsBotOrDummyIdentityKey(candidateKey)
                && !IsClientIdFallbackKey(candidateKey);
        }

        private static string BuildMissingMmrLogKey(RankedParticipant participant, string preferredKey)
        {
            var participantKey = StripRichTextTags(ResolveParticipantIdToKey(participant) ?? participant?.playerId)?.Trim();
            var displayName = StripRichTextTags(participant?.displayName)?.Trim();
            var preferred = StripRichTextTags(preferredKey)?.Trim();
            return string.Join("|", new[]
            {
                "unresolved",
                string.IsNullOrWhiteSpace(participantKey) ? "no-participant-key" : participantKey,
                participant?.clientId.ToString() ?? "0",
                string.IsNullOrWhiteSpace(preferred) ? "no-preferred-key" : preferred,
                string.IsNullOrWhiteSpace(displayName) ? "no-display-name" : displayName
            });
        }

        private static bool IsBotOrDummyIdentityKey(string candidate)
        {
            var clean = StripRichTextTags(candidate)?.Trim();
            if (string.IsNullOrWhiteSpace(clean))
            {
                return false;
            }

            return BotManager.IsBotKey(clean)
                || clean.StartsWith("dummy:", StringComparison.OrdinalIgnoreCase);
        }
    }
}
