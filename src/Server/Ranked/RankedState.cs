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
        private static readonly HashSet<string> loggedMissingMmrKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static string GetMmrPath()
        {
            var root = GetGameRootPath();
            var dir = Path.Combine(root, "UserData");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "schrader_ranked_mmr.json");
        }

        private static void EnsureMmrLoaded()
        {
            if (mmrLoadAttempted)
            {
                return;
            }

            LoadMmr();
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
                var postMatchLockStarted = false;
                var matchResult = BuildMatchResultMessage(winner);
                if (matchResult != null && matchResult.IsVisible)
                {
                    Debug.Log($"[{Constants.MOD_NAME}] Match complete -> publishing results for {matchResult.Players?.Length ?? 0} players. Winner={winner}");
                    postMatchLockStarted = BeginPostMatchLock(matchResult);
                    RankedOverlayNetwork.PublishMatchResult(matchResult);
                    Debug.Log($"[{Constants.MOD_NAME}] MatchResultMessage sent. Players={matchResult.Players?.Length ?? 0} Winner={winner}");
                    SendSystemChatToAll("<size=14><color=#66ccff>Match complete</color> post-match results are now available.</size>");
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
