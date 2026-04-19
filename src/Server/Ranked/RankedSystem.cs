using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using Unity.Netcode;
using Newtonsoft.Json;

namespace schrader.Server
{
    public static partial class RankedSystem
    {
        private static readonly object rankedLock = new object();
        private static readonly object draftLock = new object();
        private static readonly object dummyLock = new object();
        private static bool rankedVoteActive = false;
        private static float rankedVoteStartTime = -999f;
        private static float rankedVoteDuration = 25f;
        private static float rankedVoteCooldown = 60f;
        private static float lastRankedVoteTime = -999f;
        private static string rankedVoteStartedByName = null;
        private static string rankedVoteStartedByKey = null;
        private static ulong rankedVoteStartedByClientId = 0;
        private static Dictionary<ulong, bool> rankedVotes = new Dictionary<ulong, bool>();
        private static bool singleGoalieVoteActive = false;
        private static float singleGoalieVoteStartTime = -999f;
        private static float singleGoalieVoteDuration = 25f;
        private static float singleGoalieVoteCooldown = 60f;
        private static float lastSingleGoalieVoteTime = -999f;
        private static string singleGoalieVoteStartedByName = null;
        private static string singleGoalieVoteStartedByKey = null;
        private static ulong singleGoalieVoteStartedByClientId = 0;
        private static bool singleGoalieVoteDisablesMode = false;
        private static Dictionary<ulong, bool> singleGoalieVotes = new Dictionary<ulong, bool>();
        private static List<RankedParticipant> singleGoalieVoteEligible = null;
        private static bool singleGoalieEnabled = false;
        private static string singleGoaliePlayerKey = null;
        private static ulong singleGoaliePlayerClientId = 0;
        private static string singleGoaliePlayerName = null;
        private static TeamResult singleGoalieAssignedTeam = TeamResult.Unknown;
        private static readonly HashSet<int> singleGoalieHintAnnouncedPeriods = new HashSet<int>();
        private const float SingleGoaliePuckThreshold = 2f;
        private static bool rankedActive = false;
        private static float lastRankedEndTime = -999f;
        private static List<RankedParticipant> rankedParticipants = new List<RankedParticipant>();
        private static bool rankedMatchEndPatched = false;
        // Captured eligible participants at the moment a vote starts; used as fallback
        private static List<RankedParticipant> lastVoteEligible = null;
        private static List<RankedParticipant> forfeitEligibleSnapshot = null;
        private static int manualSpawnDepth = 0;
        private static readonly string[] WinnerKeywords = { "score", "goal", "winner", "winning", "team", "result" };
        private const int MaxLoggedMembers = 12;
        private const bool SyntheticPlayersEnabled = true;
        private const float SyntheticPlayerPurgeIntervalSeconds = 5f;
        private const float InvalidWarmupDummySweepIntervalSeconds = 1f;
        private const float InvalidWarmupDummyGraceSeconds = 4f;
        private const float PracticeFakePlayerSweepIntervalSeconds = 1f;
        private const float ReplayLeftoverSweepIntervalSeconds = 1f;
        private const float ReplayLeftoverGraceSeconds = 1.5f;
        private static bool gameStateHooksPatched = false;
        private static bool teamHooksPatched = false;
        private static bool replayDebugHooksPatched = false;
        private static bool draftUiHooksPatched = false;
        private static float lastSyntheticPlayerPurgeTime = -999f;
        private static float lastInvalidWarmupDummySweepTime = -999f;
        private static float lastPracticeFakePlayerSweepTime = -999f;
        private static float lastReplayLeftoverSweepTime = -999f;
        private static int? lastRedScore = null;
        private static int? lastBlueScore = null;
        private static float lastScoreUpdateTime = -999f;
        private static Dictionary<ulong, float> invalidWarmupDummyFirstSeenTimes = new Dictionary<ulong, float>();
        private static readonly Dictionary<string, float> replayLeftoverFirstSeenTimes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        private static readonly object mmrLock = new object();
        private static MmrFile mmrFile = new MmrFile();
        private static float mmrReloadInterval = 30f;
        private static float lastMmrReloadTime = -999f;
        private static int lastVoteSecondsRemaining = -1;
        private static bool goalHooksPatched = false;
        private static int currentRedGoals = 0;
        private static int currentBlueGoals = 0;
        private static readonly object goalLock = new object();
        private static readonly object playerGoalLock = new object();
        private static Dictionary<string,int> playerGoalCounts = new Dictionary<string,int>();
        private static readonly object playerAssistLock = new object();
        private static Dictionary<string,int> playerPrimaryAssistCounts = new Dictionary<string,int>();
        private static Dictionary<string,int> playerSecondaryAssistCounts = new Dictionary<string,int>();
        private static readonly object matchEndLock = new object();
        private static bool rankedMatchEnding = false;
        private static bool forfeitActive = false;
        private static float forfeitStartTime = -999f;
        private static float forfeitDuration = 30f;
        private static Dictionary<TeamResult, HashSet<ulong>> forfeitVotes = new Dictionary<TeamResult, HashSet<ulong>>();
        private static TeamResult forfeitTeam = TeamResult.Unknown;
        private static Dictionary<TeamResult, HashSet<ulong>> forfeitNoVotes = new Dictionary<TeamResult, HashSet<ulong>>();
        private const float HookRetryInterval = 2f;
        private static float lastHookAttemptTime = -999f;
        private static readonly object teamStateLock = new object();
        private static Dictionary<string, object> lastKnownPlayerTeam = new Dictionary<string, object>();
        private static HashSet<string> teamRevertActive = new HashSet<string>();
        private static readonly object internalTeamAssignmentLock = new object();
        private const float InternalTeamAssignmentGracePeriod = 2f;
        private static Dictionary<string, InternalTeamAssignment> internalTeamAssignments = new Dictionary<string, InternalTeamAssignment>(StringComparer.OrdinalIgnoreCase);

        private static readonly object phaseLock = new object();
        private static string lastGamePhaseName = null;
        private static float lastGamePhaseUpdateTime = -999f;
        private static float rankedNoMatchDetectedTime = -999f;
        private const float RankedStaleResetDelay = 3f;
        private static float lastPracticeExpiryInterceptAt = -999f;
        private const float PracticeExpiryInterceptRetryInterval = 1.5f;
        private const int PracticeExpiryContinuationSeconds = 600;
        private static int intentionalRankedStartPhaseDepth = 0;
        private static bool draftActive = false;
        private static bool draftTeamLockActive = false;
        private const float DraftStatePollInterval = 0.5f;
        private const float DraftAnnouncementMinInterval = 0.75f;
        private static float lastDraftStatePollTime = -999f;
        private static float lastDraftTurnAnnouncementTime = -999f;
        private static string lastAnnouncedTurnId = null;
        private static string lastAnnouncedAvailablePlayersSignature = null;
        private static int lastAnnouncedAvailableCount = -1;
        private static bool controlledTestModeEnabled = false;
        private static string controlledTestModeInitiatorKey = null;
        private static ulong controlledTestModeInitiatorClientId = 0;
        private static string redCaptainId = null;
        private static string blueCaptainId = null;
        private static string currentCaptainTurnId = null;
        private static readonly List<string> captainRotationQueue = new List<string>();
        private static List<string> draftAvailablePlayerIds = new List<string>();
        private static Dictionary<string, TeamResult> draftAssignedTeams = new Dictionary<string, TeamResult>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, RankedParticipant> pendingLateJoiners = new Dictionary<string, RankedParticipant>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> announcedLateJoinerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static int forcedTeamAssignmentDepth = 0;
        private static int nextDummySequence = 1;
        private static Dictionary<string, DummyPlayer> queuedDraftDummies = new Dictionary<string, DummyPlayer>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, DummyPlayer> activeDraftDummies = new Dictionary<string, DummyPlayer>(StringComparer.OrdinalIgnoreCase);

        private sealed class DummyPlayer
        {
            public string dummyId;
            public string displayName;
            public TeamResult team;
            public bool isPendingLateJoiner;
        }

        private sealed class InternalTeamAssignment
        {
            public string expectedTeam;
            public float expiresAt;
        }

        public sealed class DraftOverlayState
        {
            public bool IsVisible { get; set; }
            public bool IsCompleted { get; set; }
            public string Title { get; set; }
            public string RedCaptainName { get; set; }
            public string BlueCaptainName { get; set; }
            public string CurrentTurnName { get; set; }
            public ulong CurrentTurnClientId { get; set; }
            public string CurrentTurnSteamId { get; set; }
            public string[] AvailablePlayers { get; set; }
            public DraftOverlayPlayerEntryMessage[] AvailablePlayerEntries { get; set; }
            public string[] RedPlayers { get; set; }
            public DraftOverlayPlayerEntryMessage[] RedPlayerEntries { get; set; }
            public string[] BluePlayers { get; set; }
            public DraftOverlayPlayerEntryMessage[] BluePlayerEntries { get; set; }
            public int PendingLateJoinerCount { get; set; }
            public string[] PendingLateJoiners { get; set; }
            public DraftOverlayPlayerEntryMessage[] PendingLateJoinerEntries { get; set; }
            public bool DummyModeActive { get; set; }
            public string FooterText { get; set; }
        }

        public static void Initialize()
        {
            SetControlledTestModeEnabled(false);
            try { ClearAllDummies(); } catch { }
            try { StopReplay(null, 0); } catch { }
            invalidWarmupDummyFirstSeenTimes.Clear();
            replayLeftoverFirstSeenTimes.Clear();
            lastInvalidWarmupDummySweepTime = -999f;
            lastReplayLeftoverSweepTime = -999f;
            try { LoadMmr(); LoadReplayMemory(); TryPatchRankedMatchEndHooks(); TryPatchGameStateHooks(); TryPatchGoalHooks(); TryPatchSpawnHooks(); TryPatchTeamChangeHooks(); TryPatchReplayDebugHooks(); TryPatchDraftUiHooks(); } catch { }
            EnforceSyntheticPlayerLockdown(force: true);
        }

        // Public accessor to indicate a ranked match is currently active
        public static bool IsRankedActive()
        {
            try { return rankedActive; } catch { return false; }
        }

        public static bool IsForfeitActive()
        {
            try { return forfeitActive; } catch { return false; }
        }

        public static bool IsSingleGoalieVoteActive()
        {
            try { return singleGoalieVoteActive; } catch { return false; }
        }

        public static bool IsControlledTestModeEnabled()
        {
            try { return controlledTestModeEnabled; } catch { return false; }
        }

        internal static bool AreSyntheticPlayersAllowed()
        {
            return SyntheticPlayersEnabled;
        }

        public static void SetControlledTestModeEnabled(bool enabled)
        {
            controlledTestModeEnabled = enabled && AreSyntheticPlayersAllowed();
            if (!controlledTestModeEnabled)
            {
                controlledTestModeInitiatorKey = null;
                controlledTestModeInitiatorClientId = 0;
            }
        }

        private static void EnforceSyntheticPlayerLockdown(bool force)
        {
            if (AreSyntheticPlayersAllowed())
            {
                return;
            }

            var now = Time.unscaledTime;
            if (!force && lastSyntheticPlayerPurgeTime >= 0f && now - lastSyntheticPlayerPurgeTime < SyntheticPlayerPurgeIntervalSeconds)
            {
                return;
            }

            lastSyntheticPlayerPurgeTime = now;
            controlledTestModeEnabled = false;
            controlledTestModeInitiatorKey = null;
            controlledTestModeInitiatorClientId = 0;

            try { StopReplay(null, 0); } catch { }
            try { ClearAllDummies(); } catch { }
            try { BotManager.RemoveAllBots(); } catch { }
        }

        private static void TryPurgeInvalidWarmupDummies(bool force)
        {
            var trackedPhaseName = GetTrackedPhaseName();
            if (!ShouldRunInvalidWarmupDummyPurge(trackedPhaseName))
            {
                if (force)
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [WARMUP-DUMMY] Skipping purge during phase={trackedPhaseName ?? "unknown"} rankedActive={rankedActive} draftActive={draftActive} voteActive={rankedVoteActive}");
                }
                return;
            }

            var now = Time.unscaledTime;
            if (!force && lastInvalidWarmupDummySweepTime >= 0f && now - lastInvalidWarmupDummySweepTime < InvalidWarmupDummySweepIntervalSeconds)
            {
                return;
            }

            lastInvalidWarmupDummySweepTime = now;
            var activeCandidateClientIds = new HashSet<ulong>();

            try
            {
                var players = GetAllPlayers();
                if (players == null || players.Count == 0)
                {
                    invalidWarmupDummyFirstSeenTimes.Clear();
                    return;
                }

                foreach (var player in players)
                {
                    if (player == null) continue;
                    if (!TryGetClientId(player, out var clientId) || clientId == 0)
                    {
                        continue;
                    }

                    if (BotManager.TryGetBotIdByClientId(clientId, out _)
                        || IsReplayPlayerObject(player, clientId)
                        || IsPracticeModeFakePlayerObject(player, clientId, TryGetPlayerIdNoFallback(player)))
                    {
                        invalidWarmupDummyFirstSeenTimes.Remove(clientId);
                        continue;
                    }

                    if (!IsInvalidWarmupDummyCandidate(player, clientId, out var debugDetails))
                    {
                        invalidWarmupDummyFirstSeenTimes.Remove(clientId);
                        continue;
                    }

                    activeCandidateClientIds.Add(clientId);
                    if (!invalidWarmupDummyFirstSeenTimes.TryGetValue(clientId, out var firstSeenAt))
                    {
                        invalidWarmupDummyFirstSeenTimes[clientId] = now;
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [WARMUP-DUMMY] Detected invalid player candidate. phase={trackedPhaseName ?? "unknown"} {debugDetails} | {DescribePlayerLifecycle(player, clientId, "orphan-candidate")}");
                        continue;
                    }

                    if (!force && now - firstSeenAt < InvalidWarmupDummyGraceSeconds)
                    {
                        continue;
                    }

                    if (TryDespawnInvalidWarmupDummyPlayer(player, "orphan-candidate", "warmup-dummy"))
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [WARMUP-DUMMY] Removed invalid player after {(now - firstSeenAt):0.00}s. phase={trackedPhaseName ?? "unknown"} {debugDetails} | {DescribePlayerLifecycle(player, clientId, "orphan-candidate")}");
                        invalidWarmupDummyFirstSeenTimes.Remove(clientId);
                    }
                    else
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [WARMUP-DUMMY] Failed to remove invalid player candidate. phase={trackedPhaseName ?? "unknown"} {debugDetails} | {DescribePlayerLifecycle(player, clientId, "orphan-candidate")}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Invalid warmup dummy purge failed: {ex.Message}");
            }

            var staleClientIds = invalidWarmupDummyFirstSeenTimes.Keys
                .Where(clientId => !activeCandidateClientIds.Contains(clientId))
                .ToList();
            foreach (var staleClientId in staleClientIds)
            {
                invalidWarmupDummyFirstSeenTimes.Remove(staleClientId);
            }
        }

        private static string GetTrackedPhaseName()
        {
            lock (phaseLock)
            {
                return lastGamePhaseName;
            }
        }

        private static bool ShouldPreserveSingleGoalieDuringTrackedPhase(string trackedPhaseName)
        {
            var normalizedPhase = (trackedPhaseName ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedPhase))
            {
                return false;
            }

            return normalizedPhase.Contains("replay")
                || normalizedPhase.Contains("goal")
                || normalizedPhase.Contains("score")
                || normalizedPhase.Contains("intermission")
                || normalizedPhase.Contains("periodover")
                || normalizedPhase.Contains("faceoff");
        }

        private static bool ShouldUpdateSingleGoalieAssignmentForTrackedPhase(string trackedPhaseName)
        {
            var normalizedPhase = (trackedPhaseName ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedPhase))
            {
                return true;
            }

            return normalizedPhase.Contains("playing");
        }

        private static bool ShouldRunInvalidWarmupDummyPurge(string trackedPhaseName)
        {
            var normalizedPhase = (trackedPhaseName ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedPhase))
            {
                return false;
            }

            return normalizedPhase.Contains("warm")
                || normalizedPhase.Contains("practice")
                || normalizedPhase.Contains("training");
        }

        private static void TryPurgePracticeModeFakePlayers(bool force, string reason)
        {
            if (!force && !ShouldPurgePracticeModeFakePlayers())
            {
                return;
            }

            var now = Time.unscaledTime;
            if (!force && lastPracticeFakePlayerSweepTime >= 0f && now - lastPracticeFakePlayerSweepTime < PracticeFakePlayerSweepIntervalSeconds)
            {
                return;
            }

            lastPracticeFakePlayerSweepTime = now;

            try
            {
                var players = GetAllPlayers();
                if (players == null || players.Count == 0)
                {
                    return;
                }

                foreach (var player in players)
                {
                    if (player == null)
                    {
                        continue;
                    }

                    ulong clientId = 0;
                    TryGetClientId(player, out clientId);
                    var rawIdentity = TryGetPlayerIdNoFallback(player);
                    if (!IsPracticeModeFakePlayerObject(player, clientId, rawIdentity))
                    {
                        continue;
                    }

                    var lifecycle = DescribePlayerLifecycle(player, clientId, "practice-fake");
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [PRACTICE-FAKE] Cleaning transient practice player. reason={reason ?? "unknown"} {lifecycle}");

                    if (!TryDespawnInvalidWarmupDummyPlayer(player, "practice-fake", $"practice-fake:{reason ?? "unknown"}"))
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [PRACTICE-FAKE] Failed to clean transient practice player. reason={reason ?? "unknown"} {lifecycle}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Practice fake player purge failed: {ex.Message}");
            }
        }

        private static bool ShouldPurgePracticeModeFakePlayers()
        {
            return rankedVoteActive || rankedActive || draftActive || draftTeamLockActive;
        }

        private static void TryPurgeReplayLeftovers(bool force, string reason)
        {
            var trackedPhaseName = GetTrackedPhaseName();
            if (!ShouldRunReplayLeftoverPurge(trackedPhaseName))
            {
                if (force)
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [REPLAY-CLEANUP] Skipping transient replay sweep during phase={trackedPhaseName ?? "unknown"} reason={reason ?? "unknown"}");
                }

                replayLeftoverFirstSeenTimes.Clear();
                return;
            }

            var now = Time.unscaledTime;
            if (!force && lastReplayLeftoverSweepTime >= 0f && now - lastReplayLeftoverSweepTime < ReplayLeftoverSweepIntervalSeconds)
            {
                return;
            }

            lastReplayLeftoverSweepTime = now;
            var activeTrackingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var players = GetAllPlayers();
                if (players == null || players.Count == 0)
                {
                    replayLeftoverFirstSeenTimes.Clear();
                    return;
                }

                foreach (var player in players)
                {
                    if (player == null)
                    {
                        continue;
                    }

                    ulong clientId = 0;
                    TryGetClientId(player, out clientId);
                    var rawIdentity = NormalizeResolvedPlayerKey(TryGetPlayerIdNoFallback(player));
                    if (BotManager.TryGetBotIdByClientId(clientId, out _)
                        || BotManager.IsBotKey(rawIdentity)
                        || IsPracticeModeFakePlayerObject(player, clientId, rawIdentity)
                        || IsDummyKey(rawIdentity))
                    {
                        continue;
                    }

                    if (!TryClassifyReplayLeftoverCandidate(player, clientId, trackedPhaseName, out var trackingKey, out var classification, out var debugDetails)
                        || string.IsNullOrWhiteSpace(trackingKey))
                    {
                        continue;
                    }

                    activeTrackingKeys.Add(trackingKey);
                    if (!replayLeftoverFirstSeenTimes.TryGetValue(trackingKey, out var firstSeenAt))
                    {
                        replayLeftoverFirstSeenTimes[trackingKey] = now;
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [REPLAY-CLEANUP] Detected transient candidate. phase={trackedPhaseName ?? "unknown"} reason={reason ?? "unknown"} {debugDetails} | {DescribePlayerLifecycle(player, clientId, classification)}");
                        firstSeenAt = now;
                        if (!force)
                        {
                            continue;
                        }
                    }

                    if (!force && now - firstSeenAt < ReplayLeftoverGraceSeconds)
                    {
                        continue;
                    }

                    if (TryDespawnInvalidWarmupDummyPlayer(player, classification, $"replay-leftover:{reason ?? "unknown"}"))
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [REPLAY-CLEANUP] Removed transient candidate after {(now - firstSeenAt):0.00}s. phase={trackedPhaseName ?? "unknown"} reason={reason ?? "unknown"} {debugDetails} | {DescribePlayerLifecycle(player, clientId, classification)}");
                        replayLeftoverFirstSeenTimes.Remove(trackingKey);
                    }
                    else
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [REPLAY-CLEANUP] Failed to remove transient candidate. phase={trackedPhaseName ?? "unknown"} reason={reason ?? "unknown"} {debugDetails} | {DescribePlayerLifecycle(player, clientId, classification)}");
                    }
                }

                foreach (var component in GetDetachedReplayCleanupComponents())
                {
                    if (!TryClassifyDetachedReplayObjectCandidate(component, trackedPhaseName, out var trackingKey, out var classification, out var debugDetails)
                        || string.IsNullOrWhiteSpace(trackingKey))
                    {
                        continue;
                    }

                    activeTrackingKeys.Add(trackingKey);
                    if (!replayLeftoverFirstSeenTimes.TryGetValue(trackingKey, out var firstSeenAt))
                    {
                        replayLeftoverFirstSeenTimes[trackingKey] = now;
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [REPLAY-CLEANUP] Detected detached transient object. phase={trackedPhaseName ?? "unknown"} reason={reason ?? "unknown"} {debugDetails}");
                        firstSeenAt = now;
                        if (!force)
                        {
                            continue;
                        }
                    }

                    if (!force && now - firstSeenAt < ReplayLeftoverGraceSeconds)
                    {
                        continue;
                    }

                    if (TryDespawnDetachedReplayObject(component, classification, $"replay-leftover:{reason ?? "unknown"}"))
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [REPLAY-CLEANUP] Removed detached transient object after {(now - firstSeenAt):0.00}s. phase={trackedPhaseName ?? "unknown"} reason={reason ?? "unknown"} {debugDetails}");
                        replayLeftoverFirstSeenTimes.Remove(trackingKey);
                    }
                    else
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [REPLAY-CLEANUP] Failed to remove detached transient object. phase={trackedPhaseName ?? "unknown"} reason={reason ?? "unknown"} {debugDetails}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Replay leftover purge failed: {ex.Message}");
            }

            var staleTrackingKeys = replayLeftoverFirstSeenTimes.Keys
                .Where(key => !activeTrackingKeys.Contains(key))
                .ToList();
            foreach (var staleTrackingKey in staleTrackingKeys)
            {
                replayLeftoverFirstSeenTimes.Remove(staleTrackingKey);
            }
        }

        private static bool ShouldRunReplayLeftoverPurge(string trackedPhaseName)
        {
            var normalizedPhase = (trackedPhaseName ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedPhase))
            {
                return false;
            }

            if (normalizedPhase.Contains("replay")
                || normalizedPhase.Contains("warm")
                || normalizedPhase.Contains("practice")
                || normalizedPhase.Contains("training"))
            {
                return false;
            }

            return normalizedPhase.Contains("faceoff")
                || normalizedPhase.Contains("playing")
                || normalizedPhase.Contains("score")
                || normalizedPhase.Contains("periodover");
        }

        private static bool TryClassifyReplayLeftoverCandidate(object player, ulong clientId, string trackedPhaseName, out string trackingKey, out string classification, out string debugDetails)
        {
            trackingKey = null;
            classification = null;
            debugDetails = null;

            if (player == null)
            {
                return false;
            }

            var networkObject = TryGetPlayerLifecycleNetworkObject(player);
            trackingKey = BuildReplayCleanupTrackingKey(player, clientId, networkObject);
            if (string.IsNullOrWhiteSpace(trackingKey))
            {
                return false;
            }

            if (IsReplayPlayerObject(player, clientId))
            {
                classification = "replay-leftover";
                debugDetails = $"trackingKey={trackingKey}, candidate=replay-object";
                return true;
            }

            var normalizedPhase = (trackedPhaseName ?? string.Empty).Trim().ToLowerInvariant();
            if ((normalizedPhase.Contains("faceoff") || normalizedPhase.Contains("playing"))
                && IsInvalidWarmupDummyCandidate(player, clientId, out var invalidDetails))
            {
                classification = "replay-anonymous-leftover";
                debugDetails = $"trackingKey={trackingKey}, {invalidDetails}";
                return true;
            }

            return false;
        }

        private static List<Component> GetDetachedReplayCleanupComponents()
        {
            var result = new List<Component>();

            try
            {
                foreach (var typeName in new[] { "PlayerBodyV2", "Puck.PlayerBodyV2", "Stick", "Puck.Stick", "StickPositioner", "Puck.StickPositioner", "PlayerCamera", "Puck.PlayerCamera" })
                {
                    var componentType = FindTypeByName(typeName);
                    if (componentType == null)
                    {
                        continue;
                    }

                    UnityEngine.Object[] objects;
                    try
                    {
                        objects = Resources.FindObjectsOfTypeAll(componentType);
                    }
                    catch
                    {
                        continue;
                    }

                    if (objects == null || objects.Length == 0)
                    {
                        continue;
                    }

                    foreach (var candidate in objects)
                    {
                        if (candidate is Component component && component != null)
                        {
                            result.Add(component);
                        }
                    }
                }
            }
            catch { }

            return result;
        }

        private static bool TryClassifyDetachedReplayObjectCandidate(Component component, string trackedPhaseName, out string trackingKey, out string classification, out string debugDetails)
        {
            trackingKey = null;
            classification = null;
            debugDetails = null;

            try
            {
                if (component == null)
                {
                    return false;
                }

                var networkObject = component.GetComponent<NetworkObject>();
                trackingKey = BuildDetachedReplayObjectTrackingKey(component, networkObject);
                if (string.IsNullOrWhiteSpace(trackingKey))
                {
                    return false;
                }

                var ownerPlayer = TryResolveTransientOwnerPlayer(component);
                var ownerClientId = GetTransientOwnerClientId(component, networkObject, ownerPlayer);
                var replayFlag = HasReplayFlag(component) || HasReplayFlag(ownerPlayer);
                var replayPlayer = ownerPlayer != null && IsReplayPlayerObject(ownerPlayer, ownerClientId);
                var disconnectedReplayOwner = ownerClientId >= ReplayClientIdOffset && !IsConnectedClientId(ownerClientId);

                if (!replayFlag && !replayPlayer && !disconnectedReplayOwner)
                {
                    return false;
                }

                classification = "replay-detached-object";
                debugDetails = DescribeDetachedReplayObject(component, networkObject, ownerPlayer, ownerClientId, replayFlag, replayPlayer, trackedPhaseName, trackingKey);
                return true;
            }
            catch { }

            return false;
        }

        private static string BuildDetachedReplayObjectTrackingKey(Component component, NetworkObject networkObject)
        {
            try
            {
                if (networkObject != null && networkObject.NetworkObjectId != 0)
                {
                    return $"net:{networkObject.NetworkObjectId}";
                }

                if (component != null)
                {
                    return $"instance:{component.GetInstanceID()}";
                }
            }
            catch { }

            return null;
        }

        private static object TryResolveTransientOwnerPlayer(Component component)
        {
            if (component == null)
            {
                return null;
            }

            try
            {
                foreach (var memberName in new[] { "Player", "player" })
                {
                    var property = component.GetType().GetProperty(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (property != null)
                    {
                        var value = property.GetValue(component);
                        if (value != null)
                        {
                            return value;
                        }
                    }

                    var field = component.GetType().GetField(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (field != null)
                    {
                        var value = field.GetValue(component);
                        if (value != null)
                        {
                            return value;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private static ulong GetTransientOwnerClientId(Component component, NetworkObject networkObject, object ownerPlayer)
        {
            try
            {
                if (networkObject != null && networkObject.OwnerClientId != 0)
                {
                    return networkObject.OwnerClientId;
                }

                if (ownerPlayer != null && TryGetClientId(ownerPlayer, out var ownerClientId) && ownerClientId != 0)
                {
                    return ownerClientId;
                }

                if (component != null && TryGetClientId(component, out var componentClientId) && componentClientId != 0)
                {
                    return componentClientId;
                }
            }
            catch { }

            return 0;
        }

        private static bool IsConnectedClientId(ulong clientId)
        {
            try
            {
                if (clientId == 0)
                {
                    return false;
                }

                var connectedClients = NetworkManager.Singleton?.ConnectedClientsIds;
                return connectedClients != null && connectedClients.Contains(clientId);
            }
            catch { }

            return false;
        }

        private static string DescribeDetachedReplayObject(Component component, NetworkObject networkObject, object ownerPlayer, ulong ownerClientId, bool replayFlag, bool replayPlayer, string trackedPhaseName, string trackingKey)
        {
            try
            {
                var networkObjectId = networkObject != null ? networkObject.NetworkObjectId : 0UL;
                var networkSpawned = networkObject != null && networkObject.IsSpawned;
                var ownerDescription = ownerPlayer != null
                    ? DescribePlayerLifecycle(ownerPlayer, ownerClientId, replayPlayer ? "replay" : "linked-player")
                    : "ownerPlayer=null";
                return $"trackingKey={trackingKey}, phase={trackedPhaseName ?? "unknown"}, type={component.GetType().FullName}, gameObject={component.gameObject.name}, ownerClientId={ownerClientId}, networkObjectId={networkObjectId}, networkSpawned={networkSpawned}, replayFlag={replayFlag}, replayPlayer={replayPlayer}, connectedOwner={IsConnectedClientId(ownerClientId)} | {ownerDescription}";
            }
            catch (Exception ex)
            {
                return $"trackingKey={trackingKey}, ownerClientId={ownerClientId}, describeError={ex.GetType().Name}:{ex.Message}";
            }
        }

        private static bool TryDespawnDetachedReplayObject(Component component, string classificationHint, string reason)
        {
            try
            {
                if (component == null)
                {
                    return false;
                }

                var networkObject = component.GetComponent<NetworkObject>();
                var ownerPlayer = TryResolveTransientOwnerPlayer(component);
                var ownerClientId = GetTransientOwnerClientId(component, networkObject, ownerPlayer);
                Debug.LogWarning($"[{Constants.MOD_NAME}] [ENTITY-DESPAWN] Detached transient cleanup started. phase={GetTrackedPhaseName() ?? "unknown"} reason={reason ?? "unknown"} {DescribeDetachedReplayObject(component, networkObject, ownerPlayer, ownerClientId, HasReplayFlag(component) || HasReplayFlag(ownerPlayer), ownerPlayer != null && IsReplayPlayerObject(ownerPlayer, ownerClientId), GetTrackedPhaseName(), BuildDetachedReplayObjectTrackingKey(component, networkObject))}");

                var removed = false;
                if (ownerPlayer != null && component.GetType().Name.IndexOf("PlayerBody", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var despawnPlayerBody = ownerPlayer.GetType().GetMethod("Server_DespawnPlayerBody", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (despawnPlayerBody != null)
                    {
                        try
                        {
                            despawnPlayerBody.Invoke(ownerPlayer, null);
                            removed = true;
                        }
                        catch { }
                    }
                }
                else if (ownerPlayer != null && component.GetType().Name.IndexOf("StickPositioner", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var despawnStickPositioner = ownerPlayer.GetType().GetMethod("Server_DespawnStickPositioner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (despawnStickPositioner != null)
                    {
                        try
                        {
                            despawnStickPositioner.Invoke(ownerPlayer, null);
                            removed = true;
                        }
                        catch { }
                    }
                }
                else if (ownerPlayer != null && component.GetType().Name.IndexOf("PlayerCamera", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var despawnPlayerCamera = ownerPlayer.GetType().GetMethod("Server_DespawnPlayerCamera", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (despawnPlayerCamera != null)
                    {
                        try
                        {
                            despawnPlayerCamera.Invoke(ownerPlayer, null);
                            removed = true;
                        }
                        catch { }
                    }
                }
                else if (ownerPlayer != null && component.GetType().Name.IndexOf("Stick", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var despawnStick = ownerPlayer.GetType().GetMethod("Server_DespawnStick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (despawnStick != null)
                    {
                        try
                        {
                            despawnStick.Invoke(ownerPlayer, null);
                            removed = true;
                        }
                        catch { }
                    }
                }

                if (networkObject != null && networkObject.IsSpawned)
                {
                    try
                    {
                        networkObject.Despawn(true);
                        removed = true;
                    }
                    catch { }
                }

                try
                {
                    UnityEngine.Object.Destroy(component.gameObject);
                    removed = true;
                }
                catch { }

                return removed;
            }
            catch { }

            return false;
        }

        private static string BuildReplayCleanupTrackingKey(object player, ulong clientId, NetworkObject networkObject)
        {
            try
            {
                if (networkObject != null)
                {
                    return $"net:{networkObject.NetworkObjectId}";
                }

                if (clientId != 0)
                {
                    return $"client:{clientId}";
                }

                var rawIdentity = NormalizeResolvedPlayerKey(TryGetPlayerIdNoFallback(player));
                if (!string.IsNullOrWhiteSpace(rawIdentity))
                {
                    return $"id:{rawIdentity}";
                }

                if (player is UnityEngine.Object unityObject)
                {
                    return $"instance:{unityObject.GetInstanceID()}";
                }
            }
            catch { }

            return null;
        }

        private static bool IsInvalidWarmupDummyCandidate(object player, ulong clientId, out string debugDetails)
        {
            debugDetails = null;

            try
            {
                if (player == null || clientId == 0)
                {
                    return false;
                }

                var rawIdentity = NormalizeResolvedPlayerKey(TryGetPlayerIdNoFallback(player));
                if (BotManager.IsBotKey(rawIdentity)
                    || IsDummyKey(rawIdentity)
                    || IsPracticeModeFakePlayerObject(player, clientId, rawIdentity))
                {
                    return false;
                }

                var resolvedKey = NormalizeResolvedPlayerKey(ResolvePlayerObjectKey(player, clientId));
                var hasFallbackOnlyCommandTarget = string.IsNullOrWhiteSpace(resolvedKey)
                    || resolvedKey.StartsWith("clientId:", StringComparison.OrdinalIgnoreCase);
                var hasValidSteamId = ulong.TryParse(rawIdentity, out var steamId) && steamId != 0;
                var displayName = TryGetPlayerName(player);
                var hasDefaultDisplayName = string.IsNullOrWhiteSpace(displayName)
                    || string.Equals(displayName.Trim(), "Player", StringComparison.OrdinalIgnoreCase);
                var hasValidNumber = TryGetPlayerNumber(player, out var playerNumber) && playerNumber > 0;

                if (!hasFallbackOnlyCommandTarget || hasValidSteamId || !hasDefaultDisplayName || hasValidNumber)
                {
                    return false;
                }

                TeamResult team = TeamResult.Unknown;
                TryGetPlayerTeam(player, out team);
                debugDetails = BuildInvalidWarmupDummyDebugDetails(clientId, resolvedKey, rawIdentity, displayName, playerNumber, team);
                return true;
            }
            catch { }

            return false;
        }

        private static string BuildInvalidWarmupDummyDebugDetails(ulong clientId, string resolvedKey, string rawIdentity, string displayName, int playerNumber, TeamResult team)
        {
            try
            {
                return $"clientId={clientId}, resolvedKey={resolvedKey ?? "null"}, rawIdentity={rawIdentity ?? "null"}, displayName={displayName ?? "null"}, number={playerNumber}, team={team}";
            }
            catch { }

            return $"clientId={clientId}";
        }

        internal static string DescribePlayerLifecycle(object player, ulong fallbackClientId = 0, string classificationHint = null)
        {
            try
            {
                if (player == null)
                {
                    return $"classification={classificationHint ?? "null"}, player=null";
                }

                var clientId = fallbackClientId;
                if (clientId == 0)
                {
                    TryGetClientId(player, out clientId);
                }

                var rawIdentity = NormalizeResolvedPlayerKey(TryGetPlayerIdNoFallback(player));
                var resolvedKey = NormalizeResolvedPlayerKey(ResolvePlayerObjectKey(player, clientId));
                var displayName = TryGetPlayerName(player);
                TryGetPlayerNumber(player, out var playerNumber);
                TeamResult team = TeamResult.Unknown;
                TryGetPlayerTeam(player, out team);

                var networkObject = TryGetPlayerLifecycleNetworkObject(player);
                var networkOwnerId = networkObject != null ? networkObject.OwnerClientId : 0UL;
                var networkSpawned = networkObject != null && networkObject.IsSpawned;
                var hasBody = HasPlayerLifecycleReference(player, "PlayerBody");
                var hasStick = HasPlayerLifecycleReference(player, "Stick");
                var isPartialCharacter = TryGetPlayerLifecycleBool(player, "IsCharacterPartiallySpawned", out var partialCharacter) && partialCharacter;

                var classification = classificationHint;
                if (string.IsNullOrWhiteSpace(classification))
                {
                    if (BotManager.TryGetBotIdByClientId(clientId, out var botId) || BotManager.IsBotKey(rawIdentity))
                    {
                        classification = string.IsNullOrWhiteSpace(botId) ? "bot" : $"bot:{botId}";
                    }
                    else if (IsReplayPlayerObject(player, clientId))
                    {
                        classification = "replay";
                    }
                    else if (IsPracticeModeFakePlayerObject(player, clientId, rawIdentity))
                    {
                        classification = "practice-fake";
                    }
                    else if (IsDummyKey(rawIdentity))
                    {
                        classification = "dummy";
                    }
                    else if (string.IsNullOrWhiteSpace(resolvedKey) || resolvedKey.StartsWith("clientId:", StringComparison.OrdinalIgnoreCase))
                    {
                        classification = "orphan-candidate";
                    }
                    else
                    {
                        classification = "real-player";
                    }
                }

                return $"classification={classification}, clientId={clientId}, ownerId={networkOwnerId}, rawIdentity={rawIdentity ?? "null"}, resolvedKey={resolvedKey ?? "null"}, displayName={displayName ?? "null"}, number={playerNumber}, team={team}, networkSpawned={networkSpawned}, partialCharacter={isPartialCharacter}, hasBody={hasBody}, hasStick={hasStick}, type={player.GetType().FullName}";
            }
            catch (Exception ex)
            {
                return $"classification={classificationHint ?? "unknown"}, clientId={fallbackClientId}, debugError={ex.GetType().Name}:{ex.Message}";
            }
        }

        private static NetworkObject TryGetPlayerLifecycleNetworkObject(object player)
        {
            try
            {
                var component = player as Component;
                if (component == null)
                {
                    var transformProperty = player.GetType().GetProperty("transform", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    var transform = transformProperty != null ? transformProperty.GetValue(player) as Transform : null;
                    if (transform != null)
                    {
                        component = transform.GetComponent("Player");
                    }
                }

                return component != null ? component.GetComponent<NetworkObject>() : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool HasPlayerLifecycleReference(object player, string memberName)
        {
            if (player == null || string.IsNullOrEmpty(memberName))
            {
                return false;
            }

            try
            {
                var type = player.GetType();
                var property = type.GetProperty(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (property != null)
                {
                    return property.GetValue(player) != null;
                }

                var field = type.GetField(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    return field.GetValue(player) != null;
                }
            }
            catch { }

            return false;
        }

        private static bool TryGetPlayerLifecycleBool(object player, string memberName, out bool result)
        {
            result = false;
            if (player == null || string.IsNullOrEmpty(memberName))
            {
                return false;
            }

            try
            {
                var type = player.GetType();
                var property = type.GetProperty(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (property != null)
                {
                    return TryConvertToBool(property.GetValue(player), out result);
                }

                var field = type.GetField(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    return TryConvertToBool(field.GetValue(player), out result);
                }
            }
            catch { }

            return false;
        }

        private static bool TryDespawnInvalidWarmupDummyPlayer(object player, string classificationHint = "orphan-candidate", string reason = null)
        {
            try
            {
                if (player == null)
                {
                    return false;
                }

                Debug.LogWarning($"[{Constants.MOD_NAME}] [ENTITY-DESPAWN] Transient player cleanup started. phase={GetTrackedPhaseName() ?? "unknown"} reason={reason ?? "unknown"} {DescribePlayerLifecycle(player, 0, classificationHint)}");

                var playerType = player.GetType();
                var despawnCharacter = playerType.GetMethod("Server_DespawnCharacter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (despawnCharacter != null)
                {
                    try { despawnCharacter.Invoke(player, null); } catch { }
                }

                Component playerComponent = player as Component;
                if (playerComponent == null)
                {
                    var transformProp = playerType.GetProperty("transform", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    var transformObj = transformProp != null ? transformProp.GetValue(player) as Transform : null;
                    if (transformObj != null)
                    {
                        playerComponent = transformObj.GetComponent("Player");
                    }
                }

                var removed = false;
                if (playerComponent != null)
                {
                    var networkObject = playerComponent.GetComponent<NetworkObject>();
                    if (networkObject != null && networkObject.IsSpawned)
                    {
                        try
                        {
                            networkObject.Despawn(true);
                            removed = true;
                        }
                        catch { }
                    }

                    try
                    {
                        UnityEngine.Object.Destroy(playerComponent.gameObject);
                        removed = true;
                    }
                    catch { }
                }

                if (TryGetPlayerManager(out var playerManager) && playerManager != null)
                {
                    var removePlayer = playerManager.GetType().GetMethod("RemovePlayer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (removePlayer != null)
                    {
                        try
                        {
                            removePlayer.Invoke(playerManager, new[] { player });
                            removed = true;
                        }
                        catch { }
                    }
                }

                return removed;
            }
            catch { }

            return false;
        }

        // Returns true if any game/match appears to be active (ranked or normal)
        public static bool IsMatchActive()
        {
            try
            {
                if (TryGetTrackedMatchActive(out var trackedActive)) return trackedActive;
                if (rankedActive) return true;

                string[] typeNames = { "GameManager", "MatchManager", "MatchController", "GameController", "PuckMatchManager" };
                string[] propNames = { "IsMatchActive", "MatchInProgress", "IsRunning", "GameStarted", "RoundActive", "IsGameActive", "HasStarted", "Running" };
                string[] stateNames = { "GameState", "MatchState", "State", "Phase", "GamePhase", "MatchPhase" };
                string[] methodNames = { "IsMatchActive", "IsGameActive", "IsRunning", "HasStarted", "IsPlaying", "IsInProgress" };

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var tn in typeNames)
                    {
                        var t = asm.GetType(tn) ?? asm.GetType($"Puck.{tn}");
                        if (t == null) continue;

                        var inst = GetManagerInstance(t);
                        if (inst == null) continue;

                        var tt = inst.GetType();
                        foreach (var pn in propNames)
                        {
                            try
                            {
                                var p = tt.GetProperty(pn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                                if (p != null && p.PropertyType == typeof(bool))
                                {
                                    var val = p.GetValue(inst);
                                    if (val is bool b && b) return true;
                                }
                            }
                            catch { }
                        }

                        foreach (var sn in stateNames)
                        {
                            try
                            {
                                var p = tt.GetProperty(sn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                                if (p == null) continue;
                                var val = p.GetValue(inst);
                                if (IsLiveState(val)) return true;
                            }
                            catch { }
                        }

                        foreach (var mn in methodNames)
                        {
                            try
                            {
                                var m = tt.GetMethod(mn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                                if (m != null && m.ReturnType == typeof(bool))
                                {
                                    var res = m.Invoke(inst, null);
                                    if (res is bool b && b) return true;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool PracticePhaseSetPrefix(object __instance, object __0, object __1)
        {
            try
            {
                if (intentionalRankedStartPhaseDepth > 0)
                {
                    return true;
                }

                if (!ShouldInterceptPracticeExpiryTransition(__0))
                {
                    return true;
                }

                if (Time.unscaledTime - lastPracticeExpiryInterceptAt < PracticeExpiryInterceptRetryInterval)
                {
                    return false;
                }

                lastPracticeExpiryInterceptAt = Time.unscaledTime;
                Debug.Log($"[{Constants.MOD_NAME}] [PRACTICE] practice timer expired.");
                TryPurgePracticeModeFakePlayers(force: true, reason: "practice-expiry");
                TryPurgeInvalidWarmupDummies(force: true);

                if (rankedActive || draftActive)
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [PRACTICE] auto-ranked bypassed because ranked flow is already active. rankedActive={rankedActive} draftActive={draftActive}");
                    Debug.Log($"[{Constants.MOD_NAME}] [PRACTICE] native practice expiry transition blocked while ranked flow owns the state.");
                    return false;
                }

                if (rankedVoteActive)
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [PRACTICE] manual ranked vote is active while practice expires. Extending practice instead of auto-starting ranked.");
                }

                if (TryContinuePracticeAfterExpiry(__instance, PracticeExpiryContinuationSeconds))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [PRACTICE] auto-ranked path bypassed. Practice extended for {PracticeExpiryContinuationSeconds}s using native warmup phase.");
                }
                else
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [PRACTICE] auto-ranked path bypassed but practice extension failed. Blocking native faceoff transition to keep ranked manual-only.");
                }

                Debug.Log($"[{Constants.MOD_NAME}] [PRACTICE] repeated auto-ranked attempts disabled for this expiry event.");
                return false;
            }
            catch (Exception ex)
            {
                try { Debug.LogError($"[{Constants.MOD_NAME}] Practice expiry interception failed: {ex.Message}"); } catch { }
            }

            return true;
        }

        private static bool TryGetTrackedMatchActive(out bool isActive)
        {
            isActive = false;
            try
            {
                lock (phaseLock)
                {
                    var phase = lastGamePhaseName;
                    if (string.IsNullOrWhiteSpace(phase)) return false;

                    var p = phase.ToLowerInvariant();

                    if (IsTrackedFinalNonMatchPhase(p) || IsTrackedTransientNonMatchPhase(p))
                    {
                        isActive = false;
                        return true;
                    }

                    if (IsTrackedLiveMatchPhase(p))
                    {
                        isActive = true;
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool IsTrackedFinalNonMatchPhase(string phaseName)
        {
            if (string.IsNullOrWhiteSpace(phaseName)) return false;
            var p = phaseName.ToLowerInvariant();
            return p.Contains("gameover")
                || p.Contains("warm")
                || p.Contains("practice")
                || p.Contains("training")
                || p.Contains("mainmenu")
                || p.Contains("menu");
        }

        private static bool IsTrackedTransientNonMatchPhase(string phaseName)
        {
            if (string.IsNullOrWhiteSpace(phaseName)) return false;
            var p = phaseName.ToLowerInvariant();
            return p.Contains("periodover")
                || p.Contains("intermission")
                || p.Contains("break")
                || p.Contains("pause");
        }

        private static bool IsTrackedLiveMatchPhase(string phaseName)
        {
            if (string.IsNullOrWhiteSpace(phaseName)) return false;
            var p = phaseName.ToLowerInvariant();
            return p.Contains("playing")
                || p.Contains("faceoff")
                || p.Contains("goal")
                || p.Contains("replay")
                || p.Contains("score")
                || p.Contains("live")
                || p.Contains("inprogress")
                || p.Contains("in_progress")
                || p.Contains("running")
                || p.Contains("started")
                || p.Contains("playingphase");
        }

        private static bool ShouldPollDraftPlayerChanges()
        {
            try
            {
                lock (phaseLock)
                {
                    var phase = lastGamePhaseName;
                    if (string.IsNullOrWhiteSpace(phase)) return true;

                    var p = phase.ToLowerInvariant();
                    if (p.Contains("goal") || p.Contains("replay") || p.Contains("score")) return false;
                    if (IsTrackedFinalNonMatchPhase(p) || IsTrackedTransientNonMatchPhase(p)) return false;

                    // Exact GamePhase enum values are unknown from output_all.txt, so preserve existing behavior for unclassified phases.
                    return true;
                }
            }
            catch { }
            return true;
        }

        private static bool TryShouldResetRankedForTrackedPhase(out bool shouldReset)
        {
            shouldReset = false;
            try
            {
                lock (phaseLock)
                {
                    var phase = lastGamePhaseName;
                    if (string.IsNullOrWhiteSpace(phase)) return false;

                    var p = phase.ToLowerInvariant();
                    if (IsTrackedFinalNonMatchPhase(p))
                    {
                        shouldReset = true;
                        return true;
                    }

                    if (IsTrackedTransientNonMatchPhase(p) || IsTrackedLiveMatchPhase(p))
                    {
                        shouldReset = false;
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool IsLiveState(object stateValue)
        {
            if (stateValue == null) return false;
            try
            {
                var name = stateValue.ToString();
                if (string.IsNullOrWhiteSpace(name)) return false;
                var n = name.ToLowerInvariant();

                // Explicit non-live states (warmup, training/practice, intermission, break, pause)
                if (n.Contains("warm") || n.Contains("intermission") || n.Contains("break") || n.Contains("pause") || n.Contains("train") || n.Contains("practice")) return false;

                // Explicit live states (goal/faceoff/replay/score and playing phases should keep manual spawns locked)
                if (n.Contains("goal") || n.Contains("replay") || n.Contains("faceoff") || n.Contains("score") || n.Contains("playing") || n.Equals("play") || n.Contains("live") || n.Contains("inprogress") || n.Contains("in_progress") || n.Contains("running") || n.Contains("started") || n.Contains("playingphase")) return true;
            }
            catch { }
            return false;
        }

        private static bool IsTeamSwitchProtectionActive()
        {
            try
            {
                if (draftTeamLockActive) return true;

                // Prefer a tracked phase from Server_UpdateGameState. This avoids false positives after match end/practice transitions.
                lock (phaseLock)
                {
                    var phase = lastGamePhaseName ?? string.Empty;
                    var p = phase.ToLowerInvariant();

                    if (string.IsNullOrWhiteSpace(p)) return rankedActive || IsMatchActive();

                    // Fully ended / safe phases
                    if (IsTrackedFinalNonMatchPhase(p))
                        return false;

                    // Ranked join protection must stay active for the whole ranked lifecycle,
                    // including goal/replay/intermission windows between live faceoffs.
                    if (rankedActive)
                        return true;

                    // Active non-ranked match phases.
                    if (IsTrackedLiveMatchPhase(p))
                        return true;

                    // Unknown phase: fall back to existing heuristic
                    return IsMatchActive();
                }
            }
            catch { }
            return false;
        }

        public static void Update()
        {
            try { ProcessBackendNotificationQueue(); } catch { }
            try { ProcessPendingBackendSynchronizations(); } catch { }
            try { FlushPendingDiscordOnboardingPublishes(); } catch { }
            try { UpdateBackendDiscordLinkReminders(); } catch { }
            try { RankedOverlayNetwork.EnsureServerHandlers(); } catch { }
            try { TryEnsureHooks(); } catch { }
            try { EnforceSyntheticPlayerLockdown(force: false); } catch { }
            try { TryPurgeInvalidWarmupDummies(force: false); } catch { }
            try { TryPurgePracticeModeFakePlayers(force: false, reason: "periodic-ranked"); } catch { }
            try { TryPurgeReplayLeftovers(force: false, reason: "periodic-ranked"); } catch { }
            try { UpdateSingleGoalieState(); } catch { }
            try { UpdateRankedVote(); } catch { }
            try { ProcessForfeitVotes(); } catch { }
            try { UpdateDraftState(); } catch { }
            try { UpdateRankedWatchdog(); } catch { }
            try { UpdatePostMatchLock(); } catch { }
            try { UpdateInputReplay(); } catch { }
        }

        private static void UpdateRankedWatchdog()
        {
            try
            {
                if (postMatchLockActive)
                {
                    rankedNoMatchDetectedTime = -999f;
                    return;
                }

                if (!rankedActive || draftActive)
                {
                    rankedNoMatchDetectedTime = -999f;
                    return;
                }

                // Only act when we positively know the game is now in a non-match phase.
                if (!TryGetTrackedMatchActive(out var trackedMatchActive))
                {
                    rankedNoMatchDetectedTime = -999f;
                    return;
                }

                if (trackedMatchActive)
                {
                    rankedNoMatchDetectedTime = -999f;
                    return;
                }

                if (!TryShouldResetRankedForTrackedPhase(out var shouldResetRanked))
                {
                    rankedNoMatchDetectedTime = -999f;
                    return;
                }

                if (!shouldResetRanked)
                {
                    rankedNoMatchDetectedTime = -999f;
                    return;
                }

                var now = Time.unscaledTime;
                if (rankedNoMatchDetectedTime < 0f)
                {
                    rankedNoMatchDetectedTime = now;
                    Debug.Log($"[{Constants.MOD_NAME}] Ranked watchdog detected non-match phase while ranked is active. Waiting before reset. phase={lastGamePhaseName ?? "unknown"}");
                    return;
                }

                if (now - rankedNoMatchDetectedTime < RankedStaleResetDelay) return;

                Debug.Log($"[{Constants.MOD_NAME}] Ranked watchdog resetting stale ranked state. phase={lastGamePhaseName ?? "unknown"}");
                ResetRankedState(false, false);
                SendSystemChatToAll("<size=14><color=#ffcc66>Ranked</color> state was cleared because the match ended outside the normal flow.</size>");
            }
            catch { }
        }

        private static void UpdateDraftState()
        {
            try
            {
                if (postMatchLockActive)
                {
                    lastDraftStatePollTime = -999f;
                    return;
                }

                if (!rankedActive)
                {
                    UpdateApprovalRequestState();
                    lastDraftStatePollTime = -999f;
                    lock (draftLock)
                    {
                        pendingLateJoiners.Clear();
                        announcedLateJoinerIds.Clear();
                    }
                    return;
                }

                var now = Time.unscaledTime;
                if (now - lastDraftStatePollTime < DraftStatePollInterval) return;
                lastDraftStatePollTime = now;

                if (!ShouldPollDraftPlayerChanges()) return;

                DetectLateJoiners();
                RemoveDisconnectedDraftCandidates();
                UpdateApprovalRequestState();
            }
            catch { }
        }

        private static void TryEnsureHooks()
        {
            if (rankedMatchEndPatched && gameStateHooksPatched && goalHooksPatched && spawnHooksPatched && teamHooksPatched && replayDebugHooksPatched && draftUiHooksPatched) return;
            var now = Time.unscaledTime;
            if (now - lastHookAttemptTime < HookRetryInterval) return;
            lastHookAttemptTime = now;
            TryPatchRankedMatchEndHooks();
            TryPatchGameStateHooks();
            TryPatchGoalHooks();
            TryPatchSpawnHooks();
            TryPatchTeamChangeHooks();
            TryPatchReplayDebugHooks();
            TryPatchDraftUiHooks();
        }

        private static string GetGameRootPath()
        {
            try
            {
                var dataPath = Application.dataPath;
                if (!string.IsNullOrWhiteSpace(dataPath))
                {
                    var root = Path.GetDirectoryName(dataPath);
                    if (!string.IsNullOrWhiteSpace(root)) return root;
                }
            }
            catch { }
            return Path.GetFullPath(".");
        }

        #if false
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

        #endif

        private static bool ShouldInterceptPracticeExpiryTransition(object requestedPhase)
        {
            try
            {
                if (!TryGetGamePhaseValue(requestedPhase, out var requestedPhaseName))
                {
                    return false;
                }

                var requested = (requestedPhaseName ?? string.Empty).ToLowerInvariant();
                if (!requested.Contains("faceoff"))
                {
                    return false;
                }

                string currentPhaseName;
                lock (phaseLock)
                {
                    currentPhaseName = lastGamePhaseName;
                }

                var current = (currentPhaseName ?? string.Empty).ToLowerInvariant();
                return current.Contains("warm") || current.Contains("practice") || current.Contains("training");
            }
            catch { }

            return false;
        }

        private static bool TryContinuePracticeAfterExpiry(object gameManagerInstance, int continuationSeconds)
        {
            try
            {
                if (gameManagerInstance == null)
                {
                    return false;
                }

                var gameManagerType = gameManagerInstance.GetType();
                return TryInvokeServerSetPhase(gameManagerType, gameManagerInstance, "Warmup", continuationSeconds);
            }
            catch
            {
                return false;
            }
        }

        private static void UpdateRankedVote()
        {
            try
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
                var now = Time.unscaledTime;
                if (now - lastMmrReloadTime >= mmrReloadInterval)
                {
                    try { LoadMmr(); } catch { }
                    lastMmrReloadTime = now;
                }
                if (!rankedVoteActive) return;

                now = Time.unscaledTime;
                if (now - rankedVoteStartTime >= rankedVoteDuration)
                {
                    FinalizeRankedVote();
                    lastVoteSecondsRemaining = -1;
                    return;
                }

                var secondsRemaining = Mathf.CeilToInt(rankedVoteDuration - (now - rankedVoteStartTime));
                if (secondsRemaining < 0) secondsRemaining = 0;
                if (secondsRemaining != lastVoteSecondsRemaining)
                {
                    lastVoteSecondsRemaining = secondsRemaining;
                    PublishVoteOverlayState();
                }

                var eligible = ResolveCurrentVoteEligibleParticipants();
                if (eligible != null)
                {
                    CountSnapshotVotes(eligible, rankedVotes, out var total, out var yes, out var no);
                    var requiredYes = (total / 2) + 1;
                    if (yes >= requiredYes) FinalizeRankedVote();
                    else if (no > total - requiredYes) FinalizeRankedVote();
                }
            }
            catch { }
        }

        private static List<RankedParticipant> ResolveCurrentVoteEligibleParticipants()
        {
            try
            {
                if (lastVoteEligible == null) return null;
                return OrderParticipantsForDeterminism(lastVoteEligible);
            }
            catch { }

            return null;
        }

        public static bool TryGetEligiblePlayersForStartPublic(object player, ulong clientId, out List<RankedParticipant> eligible, out string reason)
        {
            return TryGetEligiblePlayersForStart(player, clientId, out eligible, out reason);
        }

        private static bool TryGetEligiblePlayersForStart(object player, ulong clientId, out List<RankedParticipant> eligible, out string reason)
        {
            eligible = new List<RankedParticipant>();
            reason = "not enough players";

            if (!controlledTestModeEnabled)
            {
                return TryGetEligiblePlayers(out eligible, out reason);
            }

            var unique = new Dictionary<ulong, RankedParticipant>();
            foreach (var candidate in GetAllPlayers())
            {
                if (!TryBuildStartEligibleParticipant(candidate, out var participant, includeBots: true)) continue;
                unique[participant.clientId] = participant;
            }

            if (player != null && TryBuildStartEligibleParticipant(player, out var initiatorParticipant, includeBots: true))
            {
                unique[initiatorParticipant.clientId] = initiatorParticipant;
            }

            eligible = OrderParticipantsForDeterminism(unique.Values);
            if (eligible.Count == 0)
            {
                reason = "need at least one eligible non-goalie player";
                return false;
            }

            if (clientId != 0 && !eligible.Any(participant => participant != null && participant.clientId == clientId))
            {
                reason = "you are not an eligible ranked participant";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool TryBuildStartEligibleParticipant(object player, out RankedParticipant participant, bool includeBots = false)
        {
            participant = null;

            try
            {
                if (!TryBuildConnectedPlayerSnapshot(player, out participant) || participant == null)
                {
                    return false;
                }

                if (participant.clientId == 0 || string.IsNullOrWhiteSpace(participant.playerId))
                {
                    return false;
                }

                if (!includeBots && BotManager.TryGetBotIdByClientId(participant.clientId, out _))
                {
                    return false;
                }

                if (TryIsGoalie(player, out var isGoalie) && isGoalie)
                {
                    return false;
                }

                participant.displayName = participant.displayName ?? $"Player {participant.clientId}";
                return true;
            }
            catch { }

            participant = null;
            return false;
        }

        private static void AutoAcceptControlledTestBotVotes(IEnumerable<RankedParticipant> eligible)
        {
            if (!controlledTestModeEnabled)
            {
                return;
            }

            foreach (var participant in eligible ?? Enumerable.Empty<RankedParticipant>())
            {
                if (participant == null || participant.clientId == 0)
                {
                    continue;
                }

                var participantKey = ResolveParticipantIdToKey(participant) ?? participant.playerId;
                if (!BotManager.IsBotKey(participantKey))
                {
                    continue;
                }

                rankedVotes[participant.clientId] = true;
            }
        }

        private static List<RankedParticipant> OrderParticipantsForDeterminism(IEnumerable<RankedParticipant> participants)
        {
            return (participants ?? Enumerable.Empty<RankedParticipant>())
                .Where(participant => participant != null)
                .Select(CloneParticipant)
                .Where(participant => participant != null)
                .GroupBy(participant =>
                {
                    var participantKey = ResolveParticipantIdToKey(participant);
                    if (!string.IsNullOrWhiteSpace(participantKey)) return participantKey;
                    if (participant.clientId != 0) return $"clientId:{participant.clientId}";
                    return participant.displayName ?? string.Empty;
                }, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(participant => ResolveParticipantIdToKey(participant) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(participant => participant.displayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(participant => participant.clientId)
                .ToList();
        }

        private static List<RankedParticipant> ResolveCurrentForfeitEligibleParticipants()
        {
            try
            {
                if (forfeitEligibleSnapshot == null) return null;
                return OrderParticipantsForDeterminism(forfeitEligibleSnapshot);
            }
            catch { }

            return null;
        }

        private static List<RankedParticipant> CaptureForfeitEligibleParticipants(TeamResult team)
        {
            try
            {
                if (team != TeamResult.Red && team != TeamResult.Blue) return null;

                RefreshRankedParticipantsFromLiveState();

                lock (rankedLock)
                {
                    return OrderParticipantsForDeterminism(rankedParticipants
                        .Where(participant => participant != null)
                        .Where(participant => !IsDummyParticipant(participant))
                        .Where(participant => participant.team == team)
                        .ToList());
                }
            }
            catch { }

            return null;
        }

        private static void CountForfeitVotes(TeamResult team, IEnumerable<RankedParticipant> snapshot, out int total, out int yes, out int no)
        {
            var eligibleClientIds = new HashSet<ulong>((snapshot ?? Enumerable.Empty<RankedParticipant>())
                .Where(participant => participant != null && participant.clientId != 0)
                .Select(participant => participant.clientId));

            total = eligibleClientIds.Count;
            yes = 0;
            no = 0;
            if (eligibleClientIds.Count == 0)
            {
                return;
            }

            lock (forfeitVotes)
            {
                if (forfeitVotes.TryGetValue(team, out var yesSet)) yes = yesSet.Count(clientId => eligibleClientIds.Contains(clientId));
                if (forfeitNoVotes.TryGetValue(team, out var noSet)) no = noSet.Count(clientId => eligibleClientIds.Contains(clientId));
            }
        }

        private static void ClearForfeitVoteState()
        {
            lock (forfeitVotes)
            {
                forfeitVotes.Clear();
                forfeitNoVotes.Clear();
                forfeitActive = false;
                forfeitStartTime = -999f;
                forfeitTeam = TeamResult.Unknown;
                forfeitEligibleSnapshot = null;
            }
        }

        private static bool IsClientInSnapshot(IEnumerable<RankedParticipant> snapshot, ulong clientId)
        {
            return (snapshot ?? Enumerable.Empty<RankedParticipant>())
                .Any(participant => participant != null && participant.clientId == clientId);
        }

        private static void CountSnapshotVotes(IEnumerable<RankedParticipant> snapshot, IReadOnlyDictionary<ulong, bool> votes, out int total, out int yes, out int no)
        {
            var eligibleClientIds = new HashSet<ulong>((snapshot ?? Enumerable.Empty<RankedParticipant>())
                .Where(participant => participant != null && participant.clientId != 0)
                .Select(participant => participant.clientId));

            total = eligibleClientIds.Count;
            yes = 0;
            no = 0;

            if (votes == null || eligibleClientIds.Count == 0)
            {
                return;
            }

            foreach (var vote in votes)
            {
                if (!eligibleClientIds.Contains(vote.Key)) continue;
                if (vote.Value) yes++;
                else no++;
            }
        }

        private static void UpdateSingleGoalieState()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            ProcessSingleGoalieVote();

            if (!singleGoalieEnabled)
            {
                return;
            }

            var trackedPhaseName = GetTrackedPhaseName();
            var preserveDuringPhase = ShouldPreserveSingleGoalieDuringTrackedPhase(trackedPhaseName);

            if (!TryGetSingleGoalieCandidate(out var goalie, out var reason))
            {
                if (preserveDuringPhase)
                {
                    return;
                }

                DisableSingleGoalie(reason ?? "shared goalie disabled because the goalie setup is no longer valid.");
                return;
            }

            if (!IsTrackedSingleGoalie(goalie))
            {
                if (preserveDuringPhase)
                {
                    return;
                }

                DisableSingleGoalie("shared goalie disabled because another goalie is active.");
                return;
            }

            singleGoaliePlayerKey = ResolveParticipantIdToKey(goalie) ?? goalie.playerId;
            singleGoaliePlayerClientId = goalie.clientId;
            singleGoaliePlayerName = goalie.displayName ?? singleGoaliePlayerName ?? "Goalie";

            if (!rankedActive || draftActive)
            {
                return;
            }

            if (!ShouldUpdateSingleGoalieAssignmentForTrackedPhase(trackedPhaseName))
            {
                return;
            }

            TryUpdateSharedGoalieAssignment(goalie);
        }

        private static void ProcessSingleGoalieVote()
        {
            if (!singleGoalieVoteActive)
            {
                return;
            }

            if (!TryGetSingleGoalieCandidate(out _, out var candidateReason))
            {
                CancelSingleGoalieVote(candidateReason ?? "shared goalie vote cancelled because the goalie setup changed.");
                return;
            }

            var now = Time.unscaledTime;
            if (now - singleGoalieVoteStartTime >= singleGoalieVoteDuration)
            {
                FinalizeSingleGoalieVote();
                return;
            }

            var eligible = ResolveCurrentSingleGoalieVoteEligibleParticipants();
            CountSnapshotVotes(eligible, singleGoalieVotes, out var total, out var yes, out var no);
            var requiredYes = total > 0 ? ((total / 2) + 1) : 1;
            if (yes >= requiredYes || no > total - requiredYes)
            {
                FinalizeSingleGoalieVote();
            }
        }

        private static List<RankedParticipant> ResolveCurrentSingleGoalieVoteEligibleParticipants()
        {
            try
            {
                if (singleGoalieVoteEligible == null) return null;
                return OrderParticipantsForDeterminism(singleGoalieVoteEligible);
            }
            catch { }

            return null;
        }

        public static void HandleSingleGoalieVoteStart(object player, ulong clientId)
        {
            try
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

                lock (rankedLock)
                {
                    if (!rankedActive || draftActive)
                    {
                        SendSystemChatToClient("<size=14><color=#ff6666>Shared Goalie</color> can only be used during an active ranked match.</size>", clientId);
                        return;
                    }

                    if (rankedVoteActive)
                    {
                        SendSystemChatToClient("<size=14><color=#ff6666>Shared Goalie</color> wait for the ranked vote to finish first.</size>", clientId);
                        return;
                    }

                    if (singleGoalieVoteActive)
                    {
                        SendSystemChatToClient("<size=14><color=#ffcc66>Shared Goalie</color> vote already in progress.</size>", clientId);
                        return;
                    }

                    if (Time.unscaledTime - lastSingleGoalieVoteTime < singleGoalieVoteCooldown)
                    {
                        SendSystemChatToClient("<size=14><color=#ff6666>Shared Goalie</color> vote is on cooldown.</size>", clientId);
                        return;
                    }

                    if (!TryBuildConnectedPlayerSnapshot(player, out var initiator) || initiator == null)
                    {
                        SendSystemChatToClient("<size=14><color=#ff6666>Shared Goalie</color> could not resolve the requesting player.</size>", clientId);
                        return;
                    }

                    if (!TryIsGoalie(player, out var initiatorIsGoalie) || !initiatorIsGoalie)
                    {
                        SendSystemChatToClient("<size=14><color=#ff6666>Shared Goalie</color> only the active goalie can start this vote.</size>", clientId);
                        return;
                    }

                    if (!TryGetSingleGoalieCandidate(out var goalieCandidate, out var candidateReason))
                    {
                        SendSystemChatToClient($"<size=14><color=#ff6666>Shared Goalie</color> cannot start: {candidateReason}</size>", clientId);
                        return;
                    }

                    if (!IsSameParticipantIdentity(goalieCandidate, initiator))
                    {
                        SendSystemChatToClient("<size=14><color=#ff6666>Shared Goalie</color> only the lone active goalie can start this vote.</size>", clientId);
                        return;
                    }

                    if (!TryGetSingleGoalieVoteEligiblePlayers(out var eligible, out var eligibleReason))
                    {
                        SendSystemChatToClient($"<size=14><color=#ff6666>Shared Goalie</color> cannot start: {eligibleReason}</size>", clientId);
                        return;
                    }

                    var disableVote = singleGoalieEnabled;

                    singleGoalieVoteStartedByName = initiator.displayName ?? TryGetPlayerName(player) ?? "Goalie";
                    singleGoalieVoteStartedByKey = ResolveParticipantIdToKey(initiator) ?? initiator.playerId ?? $"clientId:{clientId}";
                    singleGoalieVoteStartedByClientId = clientId;
                    singleGoalieVoteDisablesMode = disableVote;
                    singleGoalieVoteActive = true;
                    singleGoalieVoteStartTime = Time.unscaledTime;
                    lastSingleGoalieVoteTime = singleGoalieVoteStartTime;
                    singleGoalieVotes.Clear();
                    singleGoalieVotes[clientId] = true;
                    singleGoalieVoteEligible = eligible
                        .Select(CloneParticipant)
                        .Where(participant => participant != null)
                        .ToList();

                    var voteAction = disableVote ? "disable" : "enable";
                    SendSystemChatToAll($"<size=14><color=#00ff00>Shared Goalie {voteAction} vote started</color> by {singleGoalieVoteStartedByName}. Type <b>/y</b> to accept or <b>/n</b> to reject. ({Mathf.CeilToInt(singleGoalieVoteDuration)}s) The goalie starter vote counts automatically.</size>");

                    CountSnapshotVotes(singleGoalieVoteEligible, singleGoalieVotes, out var total, out var yes, out var no);
                    var requiredYes = total > 0 ? ((total / 2) + 1) : 1;
                    if (yes >= requiredYes || no > total - requiredYes)
                    {
                        FinalizeSingleGoalieVote();
                    }
                }
            }
            catch { }
        }

        public static void HandleSingleGoalieVoteResponse(object player, ulong clientId, bool accept)
        {
            try
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

                lock (rankedLock)
                {
                    if (!singleGoalieVoteActive)
                    {
                        SendSystemChatToClient("<size=14><color=#ffcc66>Shared Goalie</color> no vote is currently active.</size>", clientId);
                        return;
                    }

                    var eligible = ResolveCurrentSingleGoalieVoteEligibleParticipants();
                    if (!IsClientInSnapshot(eligible, clientId))
                    {
                        SendSystemChatToClient("<size=14><color=#ff6666>Shared Goalie</color> you are not eligible to vote in this lobby.</size>", clientId);
                        return;
                    }

                    singleGoalieVotes[clientId] = accept;
                    CountSnapshotVotes(eligible, singleGoalieVotes, out var total, out var yes, out var no);
                    var requiredYes = total > 0 ? ((total / 2) + 1) : 1;
                    if (yes >= requiredYes || no > total - requiredYes)
                    {
                        FinalizeSingleGoalieVote();
                    }
                }
            }
            catch { }
        }

        private static void FinalizeSingleGoalieVote()
        {
            try
            {
                var accepted = false;
                var disableVote = false;

                lock (rankedLock)
                {
                    if (!singleGoalieVoteActive)
                    {
                        return;
                    }

                    CountSnapshotVotes(ResolveCurrentSingleGoalieVoteEligibleParticipants(), singleGoalieVotes, out var total, out var yes, out var no);
                    var requiredYes = total > 0 ? ((total / 2) + 1) : 1;
                    accepted = total > 0 && yes >= requiredYes;
                    disableVote = singleGoalieVoteDisablesMode;

                    singleGoalieVoteActive = false;
                    singleGoalieVoteStartTime = -999f;
                    singleGoalieVoteStartedByName = null;
                    singleGoalieVoteStartedByKey = null;
                    singleGoalieVoteStartedByClientId = 0;
                    singleGoalieVoteDisablesMode = false;
                    singleGoalieVotes.Clear();
                    singleGoalieVoteEligible = null;
                }

                if (!accepted)
                {
                    SendSystemChatToAll(disableVote
                        ? "<size=14><color=#ff6666>Shared Goalie</color> disable vote failed.</size>"
                        : "<size=14><color=#ff6666>Shared Goalie</color> enable vote failed.</size>");
                    return;
                }

                if (disableVote)
                {
                    var goalieName = singleGoaliePlayerName ?? "Goalie";
                    singleGoalieEnabled = false;
                    singleGoaliePlayerKey = null;
                    singleGoaliePlayerClientId = 0;
                    singleGoaliePlayerName = null;
                    singleGoalieAssignedTeam = TeamResult.Unknown;
                    SendSystemChatToAll($"<size=14><color=#ff6666>Shared Goalie</color> disabled for <b>{goalieName}</b> by vote.</size>");
                    return;
                }

                if (!TryGetSingleGoalieCandidate(out var goalieCandidate, out var reason))
                {
                    SendSystemChatToAll($"<size=14><color=#ff6666>Shared Goalie</color> vote passed, but activation failed: {reason}</size>");
                    return;
                }

                singleGoalieEnabled = true;
                singleGoaliePlayerKey = ResolveParticipantIdToKey(goalieCandidate) ?? goalieCandidate.playerId;
                singleGoaliePlayerClientId = goalieCandidate.clientId;
                singleGoaliePlayerName = goalieCandidate.displayName ?? "Goalie";
                singleGoalieAssignedTeam = goalieCandidate.team;

                SendSystemChatToAll($"<size=14><color=#00ff00>Shared Goalie</color> enabled for <b>{singleGoaliePlayerName}</b>. This player will appear as <b>SG</b> and will not gain or lose MMR.</size>");
            }
            catch { }
        }

        private static void CancelSingleGoalieVote(string reason)
        {
            var disableVote = singleGoalieVoteDisablesMode;
            lock (rankedLock)
            {
                singleGoalieVoteActive = false;
                singleGoalieVoteStartTime = -999f;
                singleGoalieVoteStartedByName = null;
                singleGoalieVoteStartedByKey = null;
                singleGoalieVoteStartedByClientId = 0;
                singleGoalieVoteDisablesMode = false;
                singleGoalieVotes.Clear();
                singleGoalieVoteEligible = null;
            }

            var voteAction = disableVote ? "disable vote" : "enable vote";
            SendSystemChatToAll($"<size=14><color=#ff6666>Shared Goalie</color> {voteAction} cancelled: {reason}</size>");
        }

        private static void DisableSingleGoalie(string reason)
        {
            if (!singleGoalieEnabled)
            {
                return;
            }

            singleGoalieEnabled = false;
            singleGoaliePlayerKey = null;
            singleGoaliePlayerClientId = 0;
            singleGoaliePlayerName = null;
            singleGoalieAssignedTeam = TeamResult.Unknown;

            SendSystemChatToAll($"<size=14><color=#ff6666>Shared Goalie</color> disabled: {reason}</size>");
        }

        private static bool TryGetSingleGoalieVoteEligiblePlayers(out List<RankedParticipant> eligible, out string reason)
        {
            eligible = new List<RankedParticipant>();
            reason = "no active team players found";

            var unique = new Dictionary<ulong, RankedParticipant>();
            foreach (var player in GetAllPlayers())
            {
                if (!TryBuildConnectedPlayerSnapshot(player, out var participant) || participant == null)
                {
                    continue;
                }

                if (participant.team != TeamResult.Red && participant.team != TeamResult.Blue)
                {
                    continue;
                }

                unique[participant.clientId] = participant;
            }

            eligible = OrderParticipantsForDeterminism(unique.Values);
            if (eligible.Count == 0)
            {
                return false;
            }

            reason = null;
            return true;
        }

        private static bool TryGetSingleGoalieCandidate(out RankedParticipant goalie, out string reason)
        {
            goalie = null;
            reason = "need exactly one active goalie across both teams";

            var goalies = new Dictionary<ulong, RankedParticipant>();
            foreach (var player in GetAllPlayers())
            {
                if (!TryBuildConnectedPlayerSnapshot(player, out var participant) || participant == null)
                {
                    continue;
                }

                if (participant.team != TeamResult.Red && participant.team != TeamResult.Blue)
                {
                    continue;
                }

                if (!TryIsGoalie(player, out var isGoalie) || !isGoalie)
                {
                    continue;
                }

                goalies[participant.clientId] = participant;
            }

            if (goalies.Count == 0)
            {
                reason = "need exactly one active goalie across both teams";
                return false;
            }

            if (goalies.Count > 1)
            {
                reason = "multiple goalies are active";
                return false;
            }

            goalie = OrderParticipantsForDeterminism(goalies.Values).FirstOrDefault();
            reason = goalie == null ? "could not resolve the active goalie" : null;
            return goalie != null;
        }

        private static bool IsTrackedSingleGoalie(RankedParticipant participant)
        {
            if (participant == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(singleGoaliePlayerKey))
            {
                var participantKey = NormalizeResolvedPlayerKey(ResolveParticipantIdToKey(participant) ?? participant.playerId);
                var trackedKey = NormalizeResolvedPlayerKey(singleGoaliePlayerKey);
                if (!string.IsNullOrWhiteSpace(participantKey)
                    && !string.IsNullOrWhiteSpace(trackedKey)
                    && string.Equals(participantKey, trackedKey, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return participant.clientId != 0 && singleGoaliePlayerClientId != 0 && participant.clientId == singleGoaliePlayerClientId;
        }

        private static bool IsSameParticipantIdentity(RankedParticipant left, RankedParticipant right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            var leftKey = NormalizeResolvedPlayerKey(ResolveParticipantIdToKey(left) ?? left.playerId);
            var rightKey = NormalizeResolvedPlayerKey(ResolveParticipantIdToKey(right) ?? right.playerId);
            if (!string.IsNullOrWhiteSpace(leftKey)
                && !string.IsNullOrWhiteSpace(rightKey)
                && string.Equals(leftKey, rightKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return left.clientId != 0 && right.clientId != 0 && left.clientId == right.clientId;
        }

        private static void TryUpdateSharedGoalieAssignment(RankedParticipant goalie)
        {
            try
            {
                if (goalie == null)
                {
                    return;
                }

                if (!TryResolveConnectedPlayer(singleGoaliePlayerKey ?? goalie.playerId, singleGoaliePlayerClientId != 0 ? singleGoaliePlayerClientId : goalie.clientId, out var player, out var resolvedClientId, out var resolvedPlayerKey))
                {
                    return;
                }

                if (!TryGetSinglePuckZ(out var puckZ))
                {
                    return;
                }

                TeamResult targetTeam;
                if (puckZ > SingleGoaliePuckThreshold)
                {
                    targetTeam = TeamResult.Blue;
                }
                else if (puckZ < -SingleGoaliePuckThreshold)
                {
                    targetTeam = TeamResult.Red;
                }
                else
                {
                    return;
                }
                var currentTeam = TeamResult.Unknown;
                if (!TryGetPlayerTeam(player, out currentTeam) || currentTeam == TeamResult.Unknown)
                {
                    TryGetPlayerTeamFromManager(resolvedClientId, out currentTeam);
                }

                if (currentTeam == targetTeam)
                {
                    singleGoalieAssignedTeam = targetTeam;
                    return;
                }

                if (TryApplySharedGoalieTeamSwitch(player, resolvedPlayerKey, resolvedClientId, targetTeam, puckZ))
                {
                    singleGoalieAssignedTeam = targetTeam;
                }
            }
            catch { }
        }

        private static bool TryApplySharedGoalieTeamSwitch(object player, string playerKey, ulong clientId, TeamResult targetTeam, float puckZ)
        {
            if (player == null || targetTeam == TeamResult.Unknown)
            {
                return false;
            }

            try
            {
                var runtimeTeamType = FindTypeByName("PlayerTeam", "Puck.PlayerTeam");
                if (runtimeTeamType == null || !runtimeTeamType.IsEnum)
                {
                    return false;
                }

                var runtimeTeamValue = Enum.Parse(runtimeTeamType, targetTeam == TeamResult.Red ? "Red" : "Blue", true);
                var previousJoinMidMatchDelay = 0f;
                var hasPreviousJoinMidMatchDelay = TryGetJoinMidMatchDelay(out previousJoinMidMatchDelay);
                object goaliePosition = null;

                try
                {
                    if (hasPreviousJoinMidMatchDelay)
                    {
                        TrySetJoinMidMatchDelay(0f);
                    }

                    RegisterInternalTeamAssignment(playerKey, runtimeTeamValue);
                    using (BeginForcedTeamAssignment())
                    {
                        if (!TrySetPlayerTeamOnPlayer(player, runtimeTeamValue))
                        {
                            return false;
                        }
                    }

                    goaliePosition = TryFindSharedGoaliePosition(runtimeTeamValue);
                    if (goaliePosition != null)
                    {
                        var claimMethod = goaliePosition.GetType().GetMethod("Server_Claim", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        claimMethod?.Invoke(goaliePosition, new[] { player });
                    }
                    else
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [SG] No goalie position found on {FormatTeamLabel(targetTeam)}; switch aborted after team change.");
                    }
                }
                finally
                {
                    if (hasPreviousJoinMidMatchDelay)
                    {
                        TrySetJoinMidMatchDelay(previousJoinMidMatchDelay);
                    }
                }

                var goalieLabel = singleGoaliePlayerName ?? playerKey ?? ("clientId:" + clientId);
                Debug.Log($"[{Constants.MOD_NAME}] [SG] moved shared goalie {goalieLabel} to {FormatTeamLabel(targetTeam)} (puck z={puckZ:0.00}).");
                return true;
            }
            catch { }

            return false;
        }

        private static bool TryGetSinglePuckZ(out float puckZ)
        {
            puckZ = 0f;

            try
            {
                var puckManagerType = FindTypeByName("PuckManager", "Puck.PuckManager");
                var puckManager = GetManagerInstance(puckManagerType);
                if (puckManagerType == null || puckManager == null)
                {
                    return false;
                }

                var getPucks = puckManagerType.GetMethod("GetPucks", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (getPucks == null)
                {
                    return false;
                }

                object result;
                var parameters = getPucks.GetParameters();
                if (parameters.Length == 0)
                {
                    result = getPucks.Invoke(puckManager, null);
                }
                else
                {
                    result = getPucks.Invoke(puckManager, new object[] { false });
                }

                var pucks = result as IEnumerable;
                if (pucks == null)
                {
                    return false;
                }

                object singlePuck = null;
                var count = 0;
                foreach (var puck in pucks)
                {
                    if (puck == null) continue;
                    singlePuck = puck;
                    count++;
                    if (count > 1)
                    {
                        return false;
                    }
                }

                if (count != 1 || singlePuck == null)
                {
                    return false;
                }

                if (singlePuck is Component component)
                {
                    puckZ = component.transform.position.z;
                    return true;
                }

                var transformProp = singlePuck.GetType().GetProperty("transform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var transform = transformProp?.GetValue(singlePuck) as Transform;
                if (transform == null)
                {
                    return false;
                }

                puckZ = transform.position.z;
                return true;
            }
            catch { }

            return false;
        }

        private static bool TryResolveGoaliePositionKeyForTeam(TeamResult team, out string positionKey)
        {
            positionKey = null;
            if (team != TeamResult.Red && team != TeamResult.Blue)
            {
                return false;
            }

            try
            {
                var ppmType = FindTypeByName("PlayerPositionManager", "Puck.PlayerPositionManager");
                var ppm = GetManagerInstance(ppmType);
                if (ppmType == null || ppm == null)
                {
                    return false;
                }

                var listName = team == TeamResult.Red ? "RedPositions" : "BluePositions";
                var listProp = ppmType.GetProperty(listName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var positions = listProp?.GetValue(ppm) as IEnumerable;
                if (positions == null)
                {
                    return false;
                }

                var index = 0;
                foreach (var position in positions)
                {
                    var candidateKey = $"{listName}:{index++}";
                    if (!IsGoaliePositionForSharedMode(position))
                    {
                        continue;
                    }

                    positionKey = candidateKey;
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static object TryFindSharedGoaliePosition(object runtimeTeamValue)
        {
            if (runtimeTeamValue == null)
            {
                return null;
            }

            try
            {
                var ppmType = FindTypeByName("PlayerPositionManager", "Puck.PlayerPositionManager");
                var ppm = GetManagerInstance(ppmType);
                if (ppmType == null || ppm == null)
                {
                    return null;
                }

                var allPositionsProperty = ppmType.GetProperty("AllPositions", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var allPositions = allPositionsProperty?.GetValue(ppm) as IEnumerable;
                if (allPositions == null)
                {
                    return null;
                }

                foreach (var position in allPositions)
                {
                    if (position == null)
                    {
                        continue;
                    }

                    var positionType = position.GetType();
                    var roleValue = positionType.GetProperty("Role", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position)
                        ?? positionType.GetField("Role", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position)
                        ?? positionType.GetProperty("role", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position)
                        ?? positionType.GetField("role", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position);
                    var teamValue = positionType.GetProperty("Team", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position)
                        ?? positionType.GetField("Team", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position)
                        ?? positionType.GetProperty("team", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position)
                        ?? positionType.GetField("team", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position);

                    if (roleValue == null || teamValue == null)
                    {
                        continue;
                    }

                    if (roleValue.ToString().IndexOf("goal", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    if (!AreTeamValuesEqual(teamValue, runtimeTeamValue))
                    {
                        continue;
                    }

                    return position;
                }
            }
            catch { }

            return null;
        }

        private static bool TryGetJoinMidMatchDelay(out float delay)
        {
            delay = 0f;

            try
            {
                var serverManagerType = FindTypeByName("ServerManager", "Puck.ServerManager");
                var serverManager = GetManagerInstance(serverManagerType);
                if (serverManager == null)
                {
                    return false;
                }

                var serverManagerResolvedType = serverManager.GetType();
                var configurationManager = serverManagerResolvedType.GetProperty("ServerConfigurationManager", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(serverManager)
                    ?? serverManagerResolvedType.GetField("ServerConfigurationManager", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(serverManager);
                if (configurationManager == null)
                {
                    return false;
                }

                var configurationManagerType = configurationManager.GetType();
                var configuration = configurationManagerType.GetProperty("ServerConfiguration", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(configurationManager)
                    ?? configurationManagerType.GetField("ServerConfiguration", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(configurationManager);
                if (configuration == null)
                {
                    return false;
                }

                var configurationType = configuration.GetType();
                var joinMidMatchDelayProperty = configurationType.GetProperty("joinMidMatchDelay", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (joinMidMatchDelayProperty != null)
                {
                    var value = joinMidMatchDelayProperty.GetValue(configuration);
                    if (value != null)
                    {
                        delay = Convert.ToSingle(value);
                        return true;
                    }
                }

                var joinMidMatchDelayField = configurationType.GetField("joinMidMatchDelay", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (joinMidMatchDelayField != null)
                {
                    var value = joinMidMatchDelayField.GetValue(configuration);
                    if (value != null)
                    {
                        delay = Convert.ToSingle(value);
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private static bool TrySetJoinMidMatchDelay(float delay)
        {
            try
            {
                var serverManagerType = FindTypeByName("ServerManager", "Puck.ServerManager");
                var serverManager = GetManagerInstance(serverManagerType);
                if (serverManager == null)
                {
                    return false;
                }

                var serverManagerResolvedType = serverManager.GetType();
                var configurationManager = serverManagerResolvedType.GetProperty("ServerConfigurationManager", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(serverManager)
                    ?? serverManagerResolvedType.GetField("ServerConfigurationManager", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(serverManager);
                if (configurationManager == null)
                {
                    return false;
                }

                var configurationManagerType = configurationManager.GetType();
                var configuration = configurationManagerType.GetProperty("ServerConfiguration", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(configurationManager)
                    ?? configurationManagerType.GetField("ServerConfiguration", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(configurationManager);
                if (configuration == null)
                {
                    return false;
                }

                var configurationType = configuration.GetType();
                var joinMidMatchDelayProperty = configurationType.GetProperty("joinMidMatchDelay", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (joinMidMatchDelayProperty != null && joinMidMatchDelayProperty.CanWrite)
                {
                    joinMidMatchDelayProperty.SetValue(configuration, delay);
                    return true;
                }

                var joinMidMatchDelayField = configurationType.GetField("joinMidMatchDelay", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (joinMidMatchDelayField != null)
                {
                    joinMidMatchDelayField.SetValue(configuration, delay);
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static bool IsGoaliePositionForSharedMode(object position)
        {
            if (position == null)
            {
                return false;
            }

            try
            {
                var positionType = position.GetType();
                foreach (var memberName in new[] { "Role", "role", "Name", "name", "PositionKey", "positionKey" })
                {
                    var property = positionType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var propertyValue = property?.GetValue(position)?.ToString();
                    if (!string.IsNullOrWhiteSpace(propertyValue)
                        && propertyValue.IndexOf("goal", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }

                    var field = positionType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var fieldValue = field?.GetValue(position)?.ToString();
                    if (!string.IsNullOrWhiteSpace(fieldValue)
                        && fieldValue.IndexOf("goal", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public static void HandleRankedVoteStart(object player, ulong clientId)
        {
            try
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

                lock (rankedLock)
                {
                        var connectedClientCount = NetworkManager.Singleton?.ConnectedClientsIds?.Count ?? 0;
                        Debug.Log($"[{Constants.MOD_NAME}] [VOTE][DEBUG] Vote start command received. clientId={clientId} controlledTest={controlledTestModeEnabled} rankedActive={rankedActive} rankedVoteActive={rankedVoteActive} connectedClients={connectedClientCount}");

                    if (rankedActive) { SendSystemChatToClient("<size=14><color=#ffcc66>Ranked</color> already active.</size>", clientId); return; }
                    if (singleGoalieVoteActive) { SendSystemChatToClient("<size=14><color=#ffcc66>Ranked</color> wait for the shared-goalie vote to finish first.</size>", clientId); return; }
                    if (rankedVoteActive) { SendSystemChatToClient("<size=14><color=#ffcc66>Ranked</color> vote already in progress.</size>", clientId); return; }
                    if (Time.unscaledTime - lastRankedVoteTime < rankedVoteCooldown) { SendSystemChatToClient("<size=14><color=#ff6666>Ranked</color> vote is on cooldown.</size>", clientId); return; }

                    if (!TryGetEligiblePlayersForStart(player, clientId, out var eligible, out var reason))
                    {
                            Debug.Log($"[{Constants.MOD_NAME}] [VOTE][DEBUG] Vote start rejected. clientId={clientId} controlledTest={controlledTestModeEnabled} reason={reason}");
                        SendSystemChatToClient($"<size=14><color=#ff6666>Ranked</color> cannot start: {reason}</size>", clientId);
                        return;
                    }

                    rankedVoteStartedByName = TryGetPlayerName(player) ?? "Player";
                    rankedVoteStartedByKey = ResolvePlayerObjectKey(player, clientId) ?? TryGetPlayerId(player, clientId) ?? $"clientId:{clientId}";
                    rankedVoteStartedByClientId = clientId;
                    Debug.Log($"[{Constants.MOD_NAME}] [PRACTICE] Manual ranked start requested. clientId={clientId} startedBy={rankedVoteStartedByName} startedByKey={rankedVoteStartedByKey}");
                    controlledTestModeInitiatorKey = rankedVoteStartedByKey;
                    controlledTestModeInitiatorClientId = clientId;

                    rankedVoteActive = true;
                    rankedVoteStartTime = Time.unscaledTime;
                    lastVoteSecondsRemaining = Mathf.CeilToInt(rankedVoteDuration);
                    lastRankedVoteTime = rankedVoteStartTime;
                    rankedVotes.Clear();
                    if (!controlledTestModeEnabled)
                    {
                        rankedVotes[clientId] = true;
                    }

                    // Capture eligible at vote start so the vote can be finalized consistently.
                    lastVoteEligible = eligible?
                        .Select(CloneParticipant)
                        .Where(p => p != null)
                        .ToList();

                    var pname = rankedVoteStartedByName;
                    var controlledTestSuffix = controlledTestModeEnabled
                        ? " Test mode active: bots auto-accept after your /y vote."
                        : string.Empty;
                    SendSystemChatToAll($"<size=14><color=#00ff00>Ranked vote started</color> by {pname}. Type <b>/y</b> to accept or <b>/n</b> to reject. ({Mathf.CeilToInt(rankedVoteDuration)}s){controlledTestSuffix}</size>");
                    Debug.Log($"[{Constants.MOD_NAME}] [VOTE] Vote started. by={pname} clientId={clientId} eligible={(lastVoteEligible?.Count ?? 0)} controlledTest={controlledTestModeEnabled} connectedClients={connectedClientCount} teams={string.Join(",", (lastVoteEligible ?? new List<RankedParticipant>()).Where(participant => participant != null).Select(participant => $"{participant.displayName}:{participant.team}:{participant.clientId}"))}");
                    PublishVoteOverlayState();
                }
            }
            catch { }
        }

        private static bool StartRankedFromEligible(List<RankedParticipant> eligible, bool forcedByAdmin)
        {
            try
            {
                if (eligible == null || eligible.Count == 0) return false;

                Debug.Log($"[{Constants.MOD_NAME}] [RANKED] StartRankedFromEligible invoked. path={(forcedByAdmin ? "admin" : "manual-vote")} eligibleCount={eligible.Count}");

                TryPurgePracticeModeFakePlayers(force: true, reason: forcedByAdmin ? "start-ranked-admin" : "start-ranked");
                TryPurgeInvalidWarmupDummies(force: true);

                eligible = OrderParticipantsForDeterminism(eligible);

                rankedVoteActive = false;
                rankedVotes.Clear();
                rankedVoteStartTime = -999f;
                lastVoteSecondsRemaining = -1;
                lastVoteEligible = null;

                PublishVoteOverlayState();

                rankedActive = true;
                rankedParticipants = eligible.Select(CloneParticipant).Where(participant => participant != null).ToList();
                ClearRankedLiveParticipationTracking();
                lock (forfeitVotes)
                {
                    forfeitVotes.Clear();
                    forfeitNoVotes.Clear();
                    forfeitActive = false;
                    forfeitStartTime = -999f;
                    forfeitTeam = TeamResult.Unknown;
                    forfeitEligibleSnapshot = null;
                }

                if (!TryStartCaptainDraft(eligible, forcedByAdmin))
                {
                    SendSystemChatToAll("<size=14><color=#ff6666>Ranked</color> draft could not be started.</size>");
                    ResetRankedState(true, true);
                    return false;
                }

                rankedVoteStartedByName = null;
                rankedVoteStartedByKey = null;
                rankedVoteStartedByClientId = 0;
                PublishScoreboardStarState();

                return true;
            }
            catch { }

            return false;
        }

        public static void HandleRankedVoteResponse(object player, ulong clientId, bool accept)
        {
            try
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
                lock (rankedLock)
                {
                    if (!rankedVoteActive)
                    {
                        SendSystemChatToClient("<size=14><color=#ffcc66>Ranked</color> no vote is currently active.</size>", clientId);
                        return;
                    }

                    var eligible = ResolveCurrentVoteEligibleParticipants();
                    if (!IsClientInSnapshot(eligible, clientId))
                    {
                        SendSystemChatToClient("<size=14><color=#ff6666>Ranked</color> you are not eligible to vote in this lobby.</size>", clientId);
                        return;
                    }

                    rankedVotes[clientId] = accept;
                    if (accept)
                    {
                        AutoAcceptControlledTestBotVotes(eligible);
                    }

                    CountSnapshotVotes(eligible, rankedVotes, out var total, out var yes, out var no);
                    var requiredYes = total > 0 ? ((total / 2) + 1) : 1;
                    if (yes >= requiredYes || no > total - requiredYes)
                    {
                        FinalizeRankedVote();
                        return;
                    }

                    PublishVoteOverlayState();
                }
            }
            catch { }
        }

        private static void FinalizeRankedVote()
        {
            try
            {
                List<RankedParticipant> eligible;
                var accepted = false;

                lock (rankedLock)
                {
                    if (!rankedVoteActive)
                    {
                        return;
                    }

                    eligible = ResolveCurrentVoteEligibleParticipants();
                    CountSnapshotVotes(eligible, rankedVotes, out var total, out var yes, out var no);
                    var requiredYes = total > 0 ? ((total / 2) + 1) : 1;
                    accepted = total > 0 && yes >= requiredYes;

                    rankedVoteActive = false;
                    rankedVotes.Clear();
                    rankedVoteStartTime = -999f;
                    lastVoteSecondsRemaining = -1;
                    lastVoteEligible = null;
                    rankedVoteStartedByName = null;
                    rankedVoteStartedByKey = null;
                    rankedVoteStartedByClientId = 0;
                    controlledTestModeInitiatorKey = null;
                    controlledTestModeInitiatorClientId = 0;

                    PublishVoteOverlayState();
                }

                if (accepted)
                {
                    if (!StartRankedFromEligible(eligible, false))
                    {
                        SendSystemChatToAll("<size=14><color=#ff6666>Ranked</color> vote passed, but the captain draft could not be started.</size>");
                    }

                    return;
                }

                SendSystemChatToAll("<size=14><color=#ff6666>Ranked</color> vote failed.</size>");
            }
            catch { }
        }

        private static VoteOverlayPlayerEntryMessage[] BuildVotePlayerEntries(IEnumerable<RankedParticipant> participants)
        {
            return (participants ?? Enumerable.Empty<RankedParticipant>())
                .Select(BuildVotePlayerEntry)
                .Where(entry => entry != null)
                .ToArray();
        }

        private static VoteOverlayPlayerEntryMessage BuildVotePlayerEntry(RankedParticipant participant)
        {
            if (participant == null)
            {
                return null;
            }

            var participantKey = NormalizeResolvedPlayerKey(ResolveParticipantIdToKey(participant) ?? participant.playerId);
            if (string.IsNullOrWhiteSpace(participantKey) && participant.clientId == 0)
            {
                return null;
            }

            object livePlayer = null;
            if (participant.clientId != 0)
            {
                TryGetPlayerByClientId(participant.clientId, out livePlayer);
            }

            var steamId = ResolveParticipantSteamIdForUi(participant, participantKey)
                ?? NormalizeResolvedPlayerKey((livePlayer != null ? TryGetPlayerIdNoFallback(livePlayer) : null) ?? participant.playerId ?? participantKey);
            var displayName = ResolvePreferredParticipantDisplayName(participant, null, participantKey)
                ?? participant.displayName
                ?? (participant.clientId != 0 ? $"Player {participant.clientId}" : "Player");
            var voteAccepted = false;
            var hasVoted = participant.clientId != 0 && rankedVotes.TryGetValue(participant.clientId, out voteAccepted);

            return new VoteOverlayPlayerEntryMessage
            {
                ClientId = participant.clientId,
                PlayerId = participantKey,
                SteamId = steamId,
                DisplayName = displayName,
                PlayerNumber = ResolveParticipantPlayerNumber(participant),
                HasVoted = hasVoted,
                VoteAccepted = hasVoted && voteAccepted,
                IsInitiator = IsVoteInitiatorParticipant(participant)
            };
        }

        private static bool IsVoteInitiatorParticipant(RankedParticipant participant)
        {
            if (participant == null)
            {
                return false;
            }

            var participantKey = NormalizeResolvedPlayerKey(ResolveParticipantIdToKey(participant) ?? participant.playerId);
            if (!string.IsNullOrWhiteSpace(rankedVoteStartedByKey)
                && !string.IsNullOrWhiteSpace(participantKey)
                && string.Equals(participantKey, rankedVoteStartedByKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return participant.clientId != 0 && participant.clientId == rankedVoteStartedByClientId;
        }

        public static VoteOverlayStateMessage GetVoteOverlayState()
        {
            try
            {
                lock (rankedLock)
                {
                    if (!rankedVoteActive)
                    {
                        return VoteOverlayStateMessage.Hidden();
                    }

                    var eligible = ResolveCurrentVoteEligibleParticipants();

                    CountSnapshotVotes(eligible, rankedVotes, out var total, out var yes, out var no);
                    var requiredYes = total > 0 ? ((total / 2) + 1) : 1;
                    var preciseSecondsRemaining = Mathf.Max(0f, rankedVoteDuration - (Time.unscaledTime - rankedVoteStartTime));
                    var secondsRemaining = Mathf.Max(0, Mathf.CeilToInt(preciseSecondsRemaining));

                    return new VoteOverlayStateMessage
                    {
                        IsVisible = true,
                        Title = "RANKED MATCH FOUND",
                        PromptText = "Accept to start a ranked captain draft for this lobby.",
                        InitiatorName = rankedVoteStartedByName,
                        SecondsRemaining = secondsRemaining,
                        SecondsRemainingPrecise = preciseSecondsRemaining,
                        VoteDurationSeconds = Mathf.Max(1, Mathf.CeilToInt(rankedVoteDuration)),
                        EligibleCount = total,
                        YesVotes = yes,
                        NoVotes = no,
                        RequiredYesVotes = requiredYes,
                        FooterText = "Majority ready starts ranked. You can also vote with /y and /n.",
                        PlayerEntries = BuildVotePlayerEntries(eligible)
                    };
                }
            }
            catch { }

            return VoteOverlayStateMessage.Hidden();
        }

        private static void PublishVoteOverlayState()
        {
            try
            {
                var state = GetVoteOverlayState();
                var targetClientCount = NetworkManager.Singleton?.ConnectedClientsIds?.Count ?? 0;
                Debug.Log($"[{Constants.MOD_NAME}] [VOTE] Publishing vote overlay. Visible={state.IsVisible} Eligible={state.EligibleCount} Yes={state.YesVotes} No={state.NoVotes} Required={state.RequiredYesVotes} controlledTest={controlledTestModeEnabled} targetClients={targetClientCount} playerEntries={(state.PlayerEntries?.Length ?? 0)}");
                RankedOverlayNetwork.PublishVoteState(state);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] PublishVoteOverlayState failed: {ex.Message}");
            }
        }

        #if false
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
                // If it's a NetworkVariable-like wrapper, try to unwrap .Value
                var t = val.GetType();
                var valueProp = t.GetProperty("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (valueProp != null)
                {
                    var inner = valueProp.GetValue(val);
                    if (inner != null) return inner.ToString();
                }
                // Fallback to ToString but filter out type names
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
                if (val is int i) return i == 3 ? TeamResult.Red : (i == 2 ? TeamResult.Blue : TeamResult.Unknown);
                if (val is uint ui) return ui == 3 ? TeamResult.Red : (ui == 2 ? TeamResult.Blue : TeamResult.Unknown);
                if (val is long l) return l == 3L ? TeamResult.Red : (l == 2L ? TeamResult.Blue : TeamResult.Unknown);
                if (val is ulong ul) return ul == 3UL ? TeamResult.Red : (ul == 2UL ? TeamResult.Blue : TeamResult.Unknown);
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

        private static bool IsReplayPlayerObject(object player, ulong fallbackClientId = 0)
        {
            try
            {
                if (player == null)
                {
                    return false;
                }

                var typeName = player.GetType().FullName ?? player.GetType().Name ?? string.Empty;
                if (typeName.IndexOf("replay", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (!TryGetPlayerManager(out var manager) || manager == null)
                {
                    return false;
                }

                var managerType = manager.GetType();
                var getReplayPlayers = managerType.GetMethod("GetReplayPlayers", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (getReplayPlayers != null)
                {
                    var replayPlayers = getReplayPlayers.Invoke(manager, null) as System.Collections.IEnumerable;
                    if (replayPlayers != null)
                    {
                        foreach (var replayPlayer in replayPlayers)
                        {
                            if (ReferenceEquals(replayPlayer, player))
                            {
                                return true;
                            }
                        }
                    }
                }

                var clientId = fallbackClientId;
                if (clientId == 0)
                {
                    TryGetClientId(player, out clientId);
                }

                if (clientId != 0)
                {
                    var getReplayPlayerByClientId = managerType.GetMethod("GetReplayPlayerByClientId", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (getReplayPlayerByClientId != null)
                    {
                        var replayPlayer = getReplayPlayerByClientId.Invoke(manager, new object[] { clientId });
                        if (replayPlayer != null && ReferenceEquals(replayPlayer, player))
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        private static bool ShouldIgnoreTransientTeamHookPlayer(object player, ulong clientId, string steamId)
        {
            try
            {
                if (IsReplayPlayerObject(player, clientId))
                {
                    return true;
                }

                if (clientId == 0 && string.IsNullOrWhiteSpace(steamId))
                {
                    return true;
                }
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

            // Fallback: use PlayerManager.GetPlayersByTeam / GetSpawnedPlayersByTeam and match clientId
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

            if (TryGetClientId(player, out var clientId) && clientId != 0 && TryIsGoalieByClaimedPosition(clientId, out isGoalie))
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

                if (text.IndexOf("goal", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (string.Equals(text.Trim(), "G", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static bool TryIsGoalieByClaimedPosition(ulong clientId, out bool isGoalie)
        {
            isGoalie = false;
            if (clientId == 0)
            {
                return false;
            }

            try
            {
                var ppmType = FindTypeByName("PlayerPositionManager", "Puck.PlayerPositionManager");
                var ppm = GetManagerInstance(ppmType);
                if (ppmType == null || ppm == null)
                {
                    return false;
                }

                var allPositionsProperty = ppmType.GetProperty("AllPositions", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var allPositions = allPositionsProperty?.GetValue(ppm) as IEnumerable;
                if (allPositions == null)
                {
                    return false;
                }

                foreach (var position in allPositions)
                {
                    if (position == null)
                    {
                        continue;
                    }

                    var positionType = position.GetType();
                    var roleValue = positionType.GetProperty("Role", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position)
                        ?? positionType.GetField("Role", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position)
                        ?? positionType.GetProperty("role", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position)
                        ?? positionType.GetField("role", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position);
                    if (!TryValueRepresentsGoalieRole(roleValue))
                    {
                        continue;
                    }

                    var claimedByValue = positionType.GetProperty("ClaimedBy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position)
                        ?? positionType.GetField("ClaimedBy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position)
                        ?? positionType.GetProperty("claimedBy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position)
                        ?? positionType.GetField("claimedBy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position)
                        ?? positionType.GetProperty("Player", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position)
                        ?? positionType.GetField("Player", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position)
                        ?? positionType.GetProperty("player", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position)
                        ?? positionType.GetField("player", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(position);
                    if (claimedByValue == null)
                    {
                        continue;
                    }

                    if (TryGetClientId(claimedByValue, out var claimedClientId) && claimedClientId == clientId)
                    {
                        isGoalie = true;
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        #endif

        #if false
        public static void SendSystemChatToAll(string message)
        {
            try
            {
                var players = GetAllPlayers();
                foreach (var p in players)
                {
                    if (TryGetClientId(p, out var cid))
                    {
                        UIChat.Instance.Server_SendSystemChatMessage(message, cid);
                    }
                }
            }
            catch { }
        }

        public static void SendSystemChatToClient(string message, ulong clientId)
        {
            try { UIChat.Instance.Server_SendSystemChatMessage(message, clientId); } catch { }
        }

        public static bool TryHandleDraftCommand(object player, ulong clientId, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;

            var trimmed = message.Trim();
            if (trimmed.StartsWith("/dummy ", StringComparison.OrdinalIgnoreCase))
            {
                HandleDummyCommand(player, clientId, trimmed.Substring(7).Trim());
                return true;
            }

            if (trimmed.StartsWith("/pick ", StringComparison.OrdinalIgnoreCase))
            {
                HandleDraftPick(player, clientId, trimmed.Substring(6).Trim());
                return true;
            }

            if (trimmed.StartsWith("/accept ", StringComparison.OrdinalIgnoreCase))
            {
                HandleLateJoinAcceptance(player, clientId, trimmed.Substring(8).Trim());
                return true;
            }

            if (trimmed.Equals("/draft", StringComparison.OrdinalIgnoreCase))
            {
                SendDraftStatusToClient(clientId);
                return true;
            }

            if (trimmed.Equals("/draftui", StringComparison.OrdinalIgnoreCase))
            {
                HandleDraftUiCommand(clientId);
                return true;
            }

            return false;
        }

        private static void HandleDraftUiCommand(ulong clientId)
        {
            try
            {
                if (!schrader.DraftStateBridge.CanRenderInCurrentProcess())
                {
                    var reason = schrader.DraftStateBridge.GetUnavailableReason();
                    SendSystemChatToClient($"<size=14><color=#ff6666>Draft UI</color> {reason}</size>", clientId);
                    return;
                }

                var visible = schrader.DraftStateBridge.Toggle();
                var testMode = schrader.DraftStateBridge.IsTestModeEnabled();
                SendSystemChatToClient(visible
                    ? (testMode
                        ? "<size=14><color=#00ff00>Draft UI</color> shown in test mode.</size>"
                        : "<size=14><color=#00ff00>Draft UI</color> shown.</size>")
                    : "<size=14><color=#ffcc66>Draft UI</color> hidden.</size>", clientId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] HandleDraftUiCommand failed: {ex.Message}");
                SendSystemChatToClient("<size=14><color=#ff6666>Draft UI</color> command failed.</size>", clientId);
            }
        }

        private static void HandleDummyCommand(object player, ulong clientId, string rawCount)
        {
            try
            {
                if (!TryIsAdminInternal(player, clientId))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Dummy</color> permission denied.</size>", clientId);
                    return;
                }

                if (!int.TryParse(rawCount, out var count) || count <= 0)
                {
                    SendSystemChatToClient("<size=14>Usage: /dummy <count></size>", clientId);
                    return;
                }

                var created = CreateDummyParticipants(count, rankedActive);
                if (created.Count == 0)
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Dummy</color> no dummies were created.</size>", clientId);
                    return;
                }

                if (!rankedActive)
                {
                    SendSystemChatToAll($"<size=14><color=#ffcc66>Dummy</color> queued {created.Count} logical dummies for the next draft: {string.Join(", ", created.Select(p => p.displayName))}</size>");
                    SendSystemChatToAll("<size=13><color=#ffcc66>Dummy</color> these players only exist inside RankedSystem and never spawn as real players.</size>");
                    return;
                }

                foreach (var participant in created)
                {
                    lock (draftLock)
                    {
                        pendingLateJoiners[participant.playerId] = participant;
                        announcedLateJoinerIds.Remove(participant.playerId);
                    }
                    MergeOrReplaceParticipant(participant, TeamResult.Unknown);
                }

                SendSystemChatToAll($"<size=14><color=#ffcc66>Dummy</color> created {created.Count} logical late joiners: {string.Join(", ", created.Select(p => p.displayName))}</size>");
                SendSystemChatToAll("<size=13><color=#ffcc66>Dummy</color> captains can accept them like any other late joiner, but they will not enter network gameplay.</size>");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] HandleDummyCommand failed: {ex.Message}");
                SendSystemChatToClient("<size=14><color=#ff6666>Dummy</color> command failed.</size>", clientId);
            }
        }

        private static List<RankedParticipant> CreateDummyParticipants(int count, bool asLateJoiners)
        {
            var created = new List<RankedParticipant>();
            lock (dummyLock)
            {
                for (var index = 0; index < count; index++)
                {
                    var dummy = new DummyPlayer
                    {
                        dummyId = $"dummy:{nextDummySequence}",
                        displayName = $"Dummy{nextDummySequence}",
                        team = TeamResult.Unknown,
                        isPendingLateJoiner = asLateJoiners
                    };
                    nextDummySequence++;

                    if (asLateJoiners) activeDraftDummies[dummy.dummyId] = dummy;
                    else queuedDraftDummies[dummy.dummyId] = dummy;

                    created.Add(CreateParticipantFromDummy(dummy));
                }
            }

            return created;
        }

        private static List<RankedParticipant> ConsumeQueuedDummyParticipants()
        {
            lock (dummyLock)
            {
                var consumed = queuedDraftDummies.Values
                    .Select(CreateParticipantFromDummy)
                    .Where(p => p != null)
                    .ToList();

                foreach (var participant in consumed)
                {
                    activeDraftDummies[participant.playerId] = new DummyPlayer
                    {
                        dummyId = participant.playerId,
                        displayName = participant.displayName,
                        team = participant.team,
                        isPendingLateJoiner = false
                    };
                }

                queuedDraftDummies.Clear();
                return consumed;
            }
        }

        private static RankedParticipant CreateParticipantFromDummy(DummyPlayer dummy)
        {
            if (dummy == null || string.IsNullOrEmpty(dummy.dummyId)) return null;
            return new RankedParticipant
            {
                clientId = 0,
                playerId = dummy.dummyId,
                displayName = dummy.displayName,
                team = dummy.team,
                isDummy = true
            };
        }

        private static void ClearAllDummies()
        {
            lock (dummyLock)
            {
                queuedDraftDummies.Clear();
                activeDraftDummies.Clear();
                nextDummySequence = 1;
            }
        }

        private static bool IsDummyParticipant(RankedParticipant participant)
        {
            return participant != null && (participant.isDummy || IsDummyKey(participant.playerId));
        }

        private static bool IsDummyKey(string playerKey)
        {
            return !string.IsNullOrEmpty(playerKey) && playerKey.StartsWith("dummy:", StringComparison.OrdinalIgnoreCase);
        }

        private static void ResetDraftAnnouncementState()
        {
            lastDraftStatePollTime = -999f;
            lastDraftTurnAnnouncementTime = -999f;
            lastAnnouncedTurnId = null;
            lastAnnouncedAvailablePlayersSignature = null;
            lastAnnouncedAvailableCount = -1;
        }

        private static string BuildDraftAvailablePlayersSignatureLocked()
        {
            try
            {
                if (draftAvailablePlayerIds == null || draftAvailablePlayerIds.Count == 0) return string.Empty;
                return string.Join("|", draftAvailablePlayerIds
                    .Where(id => !string.IsNullOrEmpty(id))
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
            }
            catch { }
            return string.Empty;
        }

        private static int GetConnectedRealPlayerCount()
        {
            try
            {
                var count = 0;
                foreach (var player in GetAllPlayers())
                {
                    if (!TryBuildConnectedPlayerSnapshot(player, out var snapshot)) continue;
                    if (snapshot.isDummy) continue;
                    count++;
                }
                return count;
            }
            catch { }
            return 0;
        }

        private static bool CanUseSoloDummyCaptainProxy(object player, ulong clientId)
        {
            if (clientId == 0) return false;
            if (GetConnectedRealPlayerCount() != 1) return false;
            var playerKey = ResolvePlayerObjectKey(player, clientId) ?? $"clientId:{clientId}";
            if (string.IsNullOrEmpty(playerKey)) return false;
            if (TryGetCaptainTeam(playerKey, out var _)) return false;
            lock (draftLock)
            {
                return IsDummyKey(currentCaptainTurnId) || BotManager.IsBotKey(currentCaptainTurnId);
            }
        }

        private static bool TryGetCurrentCaptainTeam(out TeamResult team)
        {
            team = TeamResult.Unknown;
            lock (draftLock)
            {
                if (string.Equals(currentCaptainTurnId, redCaptainId, StringComparison.OrdinalIgnoreCase))
                {
                    team = TeamResult.Red;
                    return true;
                }
                if (string.Equals(currentCaptainTurnId, blueCaptainId, StringComparison.OrdinalIgnoreCase))
                {
                    team = TeamResult.Blue;
                    return true;
                }
            }
            return false;
        }

        #endif

        #if false
        private static void TryPatchDraftUiHooks()
        {
            if (draftUiHooksPatched) return;
            try
            {
                var scoreboardClickPrefix = typeof(RankedSystem).GetMethod(nameof(ScoreboardDraftClickPrefix), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (scoreboardClickPrefix == null)
                {
                    draftUiHooksPatched = true;
                    return;
                }

                string[] typeNames =
                {
                    "SteamIntegrationManagerController",
                    "Puck.SteamIntegrationManagerController"
                };

                foreach (var typeName in typeNames)
                {
                    var type = FindTypeByName(typeName);
                    if (type == null) continue;
                    var method = type.GetMethod("Event_Client_OnScoreboardClickPlayer", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (method == null) continue;

                    var harmony = new Harmony(Constants.MOD_NAME + ".draftui");
                    harmony.Patch(method, prefix: new HarmonyLib.HarmonyMethod(scoreboardClickPrefix));
                    draftUiHooksPatched = true;
                    Debug.Log($"[{Constants.MOD_NAME}] Draft UI hook applied: {type.FullName}.Event_Client_OnScoreboardClickPlayer");
                    return;
                }

                draftUiHooksPatched = true;
                Debug.Log($"[{Constants.MOD_NAME}] Draft UI hook not found; scoreboard interaction remains unavailable.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Draft UI hook failed: {ex.Message}");
                draftUiHooksPatched = true;
            }
        }

        private static bool ScoreboardDraftClickPrefix(object __0)
        {
            try
            {
                if (!rankedActive) return true;

                var dict = __0 as Dictionary<string, object>;
                if (dict == null) return true;

                if (!draftActive)
                {
                    if (!HasPendingLateJoiners()) return true;
                }

                var actorClientId = TryGetClientIdFromDict(dict);
                object actorPlayer = null;
                if (actorClientId != 0) TryGetPlayerByClientId(actorClientId, out actorPlayer);

                if (!TryResolveDraftUiTarget(dict, out var targetParticipant))
                {
                    if (actorClientId != 0)
                    {
                        SendSystemChatToClient("<size=13><color=#ff6666>Draft UI</color> could not resolve the clicked player from the scoreboard event.</size>", actorClientId);
                    }
                    return false;
                }

                var targetKey = ResolveParticipantIdToKey(targetParticipant);
                if (string.IsNullOrEmpty(targetKey)) return false;

                if (draftActive && IsDraftAvailablePlayer(targetKey))
                {
                    HandleDraftPick(actorPlayer, actorClientId, targetParticipant.displayName ?? targetParticipant.playerId ?? actorClientId.ToString());
                    return false;
                }

                if (IsPendingLateJoiner(targetKey))
                {
                    HandleLateJoinAcceptance(actorPlayer, actorClientId, targetParticipant.displayName ?? targetParticipant.playerId ?? actorClientId.ToString());
                    return false;
                }

                if (actorClientId != 0)
                {
                    SendSystemChatToClient("<size=13><color=#ffcc66>Draft UI</color> clicked player is not available for a draft action.</size>", actorClientId);
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] ScoreboardDraftClickPrefix failed: {ex.Message}");
            }
            return true;
        }

        private static void TryPatchRankedMatchEndHooks()
        {
            if (rankedMatchEndPatched) return;
            try
            {
                var postfix = typeof(RankedSystem).GetMethod(nameof(RankedMatchEndPostfix), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (postfix == null) return;

                string[] typeNames = { "GameManager", "MatchManager", "GameController", "GameStateManager", "PuckGameManager" };
                string[] methodNames = { "Server_OnGameOver", "Server_EndMatch", "Server_EndGame", "EndMatch", "EndGame", "Server_GameOver", "GameOver" };

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var tn in typeNames)
                    {
                        var t = asm.GetType(tn) ?? asm.GetType($"Puck.{tn}");
                        if (t == null) continue;

                        foreach (var mn in methodNames)
                        {
                            var method = t.GetMethod(mn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            if (method == null) continue;

                            var h = new Harmony(Constants.MOD_NAME + ".ranked");
                            h.Patch(method, postfix: new HarmonyLib.HarmonyMethod(postfix));
                            rankedMatchEndPatched = true;
                            Debug.Log($"[{Constants.MOD_NAME}] Ranked match end hooked: {t.FullName}.{mn}");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.LogError($"[{Constants.MOD_NAME}] Ranked match end hook failed: {ex.Message}"); }
        }

        private static void TryPatchGameStateHooks()
        {
            if (gameStateHooksPatched) return;
            try
            {
                var postfix = typeof(RankedSystem).GetMethod(nameof(GameStateUpdatePostfix), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (postfix == null) return;

                string[] typeNames = { "GameManager" };
                string[] methodNames = { "Server_UpdateGameState" };

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var tn in typeNames)
                    {
                        var t = asm.GetType(tn) ?? asm.GetType($"Puck.{tn}");
                        if (t == null) continue;

                        foreach (var mn in methodNames)
                        {
                            var method = t.GetMethod(mn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            if (method == null) continue;

                            var h = new Harmony(Constants.MOD_NAME + ".gamestate");
                            h.Patch(method, postfix: new HarmonyLib.HarmonyMethod(postfix));
                            gameStateHooksPatched = true;
                            Debug.Log($"[{Constants.MOD_NAME}] GameState hook aplicado: {t.FullName}.{mn}");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.LogError($"[{Constants.MOD_NAME}] GameState hook failed: {ex.Message}"); }
        }

        private static void TryPatchGoalHooks()
        {
            if (goalHooksPatched) return;
            try
            {
                var postfix = typeof(RankedSystem).GetMethod(nameof(GoalScoredPostfix), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (postfix == null) { goalHooksPatched = true; return; }

                string[] typeNames = { "GameManager" };
                string[] methodNames = { "Server_GoalScored", "Server_GoalScoredRpc" };

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var tn in typeNames)
                    {
                        var t = asm.GetType(tn) ?? asm.GetType($"Puck.{tn}");
                        if (t == null) continue;

                        foreach (var mn in methodNames)
                        {
                            var methods = t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                                .Where(m => string.Equals(m.Name, mn, StringComparison.Ordinal));
                            foreach (var method in methods)
                            {
                                // Use a HarmonyMethod that matches any parameter list by targeting the generic postfix
                                if (method.GetParameters().Length == 0) continue;
                                var h = new Harmony(Constants.MOD_NAME + ".goal");
                                try
                                {
                                    h.Patch(method, postfix: new HarmonyLib.HarmonyMethod(postfix));
                                    goalHooksPatched = true;
                                    Debug.Log($"[{Constants.MOD_NAME}] Goal hook aplicado: {t.FullName}.{mn} ({method.GetParameters().Length} params)");
                                    return;
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogError($"[{Constants.MOD_NAME}] Goal hook patch failed for {t.FullName}.{mn}: {ex.Message}");
                                    continue;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.LogError($"[{Constants.MOD_NAME}] Goal hook failed: {ex.Message}"); goalHooksPatched = true; }
        }

        private static bool spawnHooksPatched = false;

        public static IDisposable BeginManualSpawn()
        {
            Interlocked.Increment(ref manualSpawnDepth);
            return new ManualSpawnScope();
        }

        private class ManualSpawnScope : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                Interlocked.Decrement(ref manualSpawnDepth);
            }
        }
        private static void TryPatchSpawnHooks()
        {
            if (spawnHooksPatched) return;
            try
            {
                string[] methodNames = { "Server_SpawnPuck", "Server_SpawnPucksForPhase", "Server_SpawnPucks", "SpawnPuck", "Server_Spawn" };

                // Only target PuckManager (and common variants) instead of iterating all types
                string[] puckManagerNames = { "PuckManager", "Puck.PuckManager" };
                Type pmType = FindTypeByName(puckManagerNames);
                if (pmType != null)
                {
                    var methods = pmType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
                    foreach (var m in methods)
                    {
                        foreach (var mn in methodNames)
                        {
                            if (!string.Equals(m.Name, mn, StringComparison.Ordinal)) continue;
                            try
                            {
                                var h = new Harmony(Constants.MOD_NAME + ".spawnblock");
                                var prefix = typeof(RankedSystem).GetMethod(nameof(SpawnPrefix), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                                if (prefix != null) h.Patch(m, prefix: new HarmonyLib.HarmonyMethod(prefix));
                            }
                            catch { }
                        }
                    }
                }

                spawnHooksPatched = true;
                Debug.Log($"[{Constants.MOD_NAME}] Spawn hooks patched.");
            }
            catch (Exception ex) { Debug.LogError($"[{Constants.MOD_NAME}] Spawn hooks patch failed: {ex.Message}"); }
        }

        private static void TryPatchTeamChangeHooks()
        {
            if (teamHooksPatched) return;
            try
            {
                var prefix = typeof(RankedSystem).GetMethod(nameof(TeamSelectPrefix), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (prefix == null) { teamHooksPatched = true; return; }

                string[] methodNames =
                {
                    "Event_Client_OnTeamSelectClickTeamRed",
                    "Event_Client_OnTeamSelectClickTeamBlue",
                    "Event_Client_OnTeamSelectClickTeamSpectator"
                };

                string[] targetTypeNames =
                {
                    "UIManagerController",
                    "UIManagerStateController",
                    "Puck.UIManagerController",
                    "Puck.UIManagerStateController"
                };

                foreach (var tn in targetTypeNames)
                {
                    var type = FindTypeByName(tn);
                    if (type == null) continue;

                    foreach (var mn in methodNames)
                    {
                        var method = type.GetMethod(mn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        if (method == null) continue;
                        var h = new Harmony(Constants.MOD_NAME + ".teamlock");
                        try
                        {
                            h.Patch(method, prefix: new HarmonyLib.HarmonyMethod(prefix));
                            teamHooksPatched = true;
                            Debug.Log($"[{Constants.MOD_NAME}] Team change hook applied: {type.FullName}.{mn}");
                        }
                        catch { }
                    }

                    try
                    {
                        var switchPrefix = typeof(RankedSystem).GetMethod(nameof(SwitchTeamMenuPrefix), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                        if (switchPrefix != null)
                        {
                            var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            foreach (var m in methods)
                            {
                                var n = m.Name ?? string.Empty;
                                var ln = n.ToLowerInvariant();
                                if (!ln.Contains("switchteam") && !ln.Contains("teamswitch")) continue;
                                var h = new Harmony(Constants.MOD_NAME + ".teamlock");
                                try
                                {
                                    h.Patch(m, prefix: new HarmonyLib.HarmonyMethod(switchPrefix));
                                    teamHooksPatched = true;
                                    Debug.Log($"[{Constants.MOD_NAME}] Switch Team hook applied: {type.FullName}.{m.Name}");
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }

                string[] playerControllerTypeNames =
                {
                    "PlayerController",
                    "Puck.PlayerController"
                };

                string[] teamChangeMethodNames =
                {
                    "Client_SetPlayerTeamRpc",
                    "Server_SetPlayerTeam",
                    "SetPlayerTeam",
                    "ChangeTeam",
                    "SetTeam"
                };

                var teamChangePrefix = typeof(RankedSystem).GetMethod(nameof(TeamChangePrefix), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (teamChangePrefix != null)
                {
                    foreach (var tn in playerControllerTypeNames)
                    {
                        var type = FindTypeByName(tn);
                        if (type == null) continue;

                        foreach (var mn in teamChangeMethodNames)
                        {
                            var method = type.GetMethod(mn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            if (method == null) continue;
                            var h = new Harmony(Constants.MOD_NAME + ".teamlock");
                            try
                            {
                                h.Patch(method, prefix: new HarmonyLib.HarmonyMethod(teamChangePrefix));
                                teamHooksPatched = true;
                                Debug.Log($"[{Constants.MOD_NAME}] Team change hook applied: {type.FullName}.{mn}");
                            }
                            catch { }
                        }
                    }
                }

                var teamChangedEventPrefix = typeof(RankedSystem).GetMethod(nameof(PlayerTeamChangedEventPrefix), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (teamChangedEventPrefix != null)
                {
                    foreach (var tn in playerControllerTypeNames)
                    {
                        var type = FindTypeByName(tn);
                        if (type == null) continue;

                        var method = type.GetMethod("Event_OnPlayerTeamChanged", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        if (method == null) continue;
                        var h = new Harmony(Constants.MOD_NAME + ".teamlock");
                        try
                        {
                            h.Patch(method, prefix: new HarmonyLib.HarmonyMethod(teamChangedEventPrefix));
                            teamHooksPatched = true;
                            Debug.Log($"[{Constants.MOD_NAME}] Team change hook applied: {type.FullName}.Event_OnPlayerTeamChanged");
                        }
                        catch { }
                    }
                }

                var playerSelectTeamPrefix = typeof(RankedSystem).GetMethod(nameof(PlayerSelectTeamPrefix), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (playerSelectTeamPrefix != null)
                {
                    foreach (var tn in playerControllerTypeNames)
                    {
                        var type = FindTypeByName(tn);
                        if (type == null) continue;

                        var method = type.GetMethod("Event_Client_OnPlayerSelectTeam", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        if (method == null) continue;
                        var h = new Harmony(Constants.MOD_NAME + ".teamlock");
                        try
                        {
                            h.Patch(method, prefix: new HarmonyLib.HarmonyMethod(playerSelectTeamPrefix));
                            teamHooksPatched = true;
                            Debug.Log($"[{Constants.MOD_NAME}] Team change hook applied: {type.FullName}.Event_Client_OnPlayerSelectTeam");
                        }
                        catch { }
                    }
                }

                var pauseSwitchPrefix = typeof(RankedSystem).GetMethod(nameof(SwitchTeamMenuPrefix), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var pauseSwitchPrefixNoArgs = typeof(RankedSystem).GetMethod(nameof(SwitchTeamMenuPrefixNoArgs), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                string[] pauseSwitchTypeNames =
                {
                    "UIPauseMenu",
                    "Puck.UIPauseMenu",
                    "UIManagerStateController",
                    "Puck.UIManagerStateController",
                    "PlayerController",
                    "Puck.PlayerController"
                };

                string[] pauseSwitchMethodNames =
                {
                    "OnClickSwitchTeam",
                    "Event_Client_OnPauseMenuClickSwitchTeam"
                };

                foreach (var tn in pauseSwitchTypeNames)
                {
                    var type = FindTypeByName(tn);
                    if (type == null) continue;

                    foreach (var mn in pauseSwitchMethodNames)
                    {
                        var method = type.GetMethod(mn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        if (method == null) continue;
                        var h = new Harmony(Constants.MOD_NAME + ".teamlock");
                        try
                        {
                            var parameters = method.GetParameters();
                            if (parameters.Length == 0 && pauseSwitchPrefixNoArgs != null)
                            {
                                h.Patch(method, prefix: new HarmonyLib.HarmonyMethod(pauseSwitchPrefixNoArgs));
                            }
                            else if (pauseSwitchPrefix != null)
                            {
                                h.Patch(method, prefix: new HarmonyLib.HarmonyMethod(pauseSwitchPrefix));
                            }
                            teamHooksPatched = true;
                            Debug.Log($"[{Constants.MOD_NAME}] Switch Team hook applied: {type.FullName}.{mn} (params: {method.GetParameters().Length})");
                        }
                        catch { }
                    }
                }

                // Last line of defense: patch Player's actual team setter and RPC.
                var playerType = FindTypeByName("Player", "Puck.Player");
                if (playerType != null)
                {
                    var h = new Harmony(Constants.MOD_NAME + ".teamlock");
                    var playerTeamPrefix = typeof(RankedSystem).GetMethod(nameof(PlayerTeamSetPrefix), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    if (playerTeamPrefix != null)
                    {
                        var setTeam = playerType.GetMethod("set_Team", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        if (setTeam != null)
                        {
                            try { h.Patch(setTeam, prefix: new HarmonyLib.HarmonyMethod(playerTeamPrefix)); teamHooksPatched = true; Debug.Log($"[{Constants.MOD_NAME}] Team change hook applied: {playerType.FullName}.set_Team"); } catch { }
                        }

                        var rpc = playerType.GetMethod("Client_SetPlayerTeamRpc", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        if (rpc != null)
                        {
                            try { h.Patch(rpc, prefix: new HarmonyLib.HarmonyMethod(playerTeamPrefix)); teamHooksPatched = true; Debug.Log($"[{Constants.MOD_NAME}] Team change hook applied: {playerType.FullName}.Client_SetPlayerTeamRpc"); } catch { }
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.LogError($"[{Constants.MOD_NAME}] Team change hook failed: {ex.Message}"); teamHooksPatched = true; }
        }

        private static bool TeamSelectPrefix(object __0)
        {
            try
            {
                var active = IsTeamSwitchProtectionActive();
                Debug.Log($"[{Constants.MOD_NAME}] TeamSelectPrefix called. protectActive={active}");
                return true;
            }
            catch { }
            return true;
        }

        private static bool TeamChangePrefix()
        {
            try
            {
                if (forcedTeamAssignmentDepth > 0) return true;
                var active = IsTeamSwitchProtectionActive();
                Debug.Log($"[{Constants.MOD_NAME}] TeamChangePrefix called. protectActive={active}");
                return true;
            }
            catch { }
            return true;
        }

        private static bool SwitchTeamMenuPrefix(object __0)
        {
            try
            {
                var active = IsTeamSwitchProtectionActive();
                Debug.Log($"[{Constants.MOD_NAME}] SwitchTeamMenuPrefix called. protectActive={active}");
                return true;
            }
            catch { }
            return true;
        }

        private static bool SwitchTeamMenuPrefixNoArgs()
        {
            try
            {
                var active = IsTeamSwitchProtectionActive();
                Debug.Log($"[{Constants.MOD_NAME}] SwitchTeamMenuPrefixNoArgs called. protectActive={active}");
                return true;
            }
            catch { }
            return true;
        }

        private static bool TryHandleSameTeamPositionSelectionRequest(object player, object currentTeam, object requestedTeam)
        {
            try
            {
                if (player == null)
                {
                    return false;
                }

                if (draftActive || draftTeamLockActive)
                {
                    return false;
                }

                var currentResolvedTeam = ConvertTeamValue(currentTeam);
                var requestedResolvedTeam = ConvertTeamValue(requestedTeam);
                if (currentResolvedTeam != TeamResult.Red && currentResolvedTeam != TeamResult.Blue)
                {
                    return false;
                }

                if (currentResolvedTeam != requestedResolvedTeam)
                {
                    return false;
                }

                TryOpenPlayerPositionSelection(player, currentResolvedTeam);
                return true;
            }
            catch { }

            return false;
        }

        private static bool PlayerTeamChangedEventPrefix(object __0)
        {
            try
            {
                if (forcedTeamAssignmentDepth > 0) return true;

                var active = IsTeamSwitchProtectionActive();
                var dict = __0 as Dictionary<string, object>;
                var steamId = TryGetSteamIdFromDict(dict);
                var clientId = TryGetClientIdFromDict(dict);
                var playerObj = TryGetPlayerFromDict(dict);
                if (string.IsNullOrEmpty(steamId) && playerObj != null)
                {
                    steamId = TryGetPlayerId(playerObj, 0UL);
                }
                if (string.IsNullOrEmpty(steamId) && clientId != 0 && TryGetPlayerByClientId(clientId, out var p))
                {
                    steamId = TryGetPlayerId(p, clientId);
                }

                if (ShouldIgnoreTransientTeamHookPlayer(playerObj, clientId, steamId))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] Ignoring transient replay/anonymous team change: steamId={steamId ?? "?"} clientId={clientId}");
                    return true;
                }

                var newTeam = TryGetTeamFromDict(dict);
                var oldTeam = TryGetOldTeamFromDict(dict);
                var keys = dict != null ? string.Join(",", dict.Keys) : "null";
                Debug.Log($"[{Constants.MOD_NAME}] Event_OnPlayerTeamChanged protectActive={active} steamId={steamId ?? "?"} clientId={clientId} playerType={(playerObj != null ? playerObj.GetType().FullName : "null")} oldTeam={FormatTeamValue(oldTeam)} newTeam={FormatTeamValue(newTeam)} keys={keys}");

                // If this isn't a real team change (same team selected), do nothing.
                if (AreTeamValuesEqual(oldTeam, newTeam))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] Ignoring team change event: oldTeam == newTeam");
                    return true;
                }

                if (IsInternalTeamAssignmentAllowed(steamId, clientId, newTeam, true))
                {
                    if (!string.IsNullOrEmpty(steamId) && newTeam != null)
                    {
                        lock (teamStateLock)
                        {
                            lastKnownPlayerTeam[steamId] = newTeam;
                        }
                    }
                    Debug.Log($"[{Constants.MOD_NAME}] Allowing internal team assignment: {steamId ?? (clientId != 0 ? $"clientId:{clientId}" : "unknown")} -> {FormatTeamValue(newTeam)}");
                    return true;
                }

                // If player is joining mid-match (oldTeam None -> newTeam RealTeam), allow it.
                if (active && !draftTeamLockActive && IsTeamNoneLike(oldTeam) && !IsTeamNoneLike(newTeam))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] Allowing initial team assignment during active match: {FormatTeamValue(oldTeam)} -> {FormatTeamValue(newTeam)}");
                    return true;
                }

                if (!string.IsNullOrEmpty(steamId))
                {
                    lock (teamStateLock)
                    {
                        if (!active)
                        {
                            if (newTeam != null) lastKnownPlayerTeam[steamId] = newTeam;
                        }
                        else
                        {
                            if (!lastKnownPlayerTeam.ContainsKey(steamId) && oldTeam != null)
                            {
                                lastKnownPlayerTeam[steamId] = oldTeam;
                            }
                        }
                    }

                    if (active)
                    {
                        object previousTeam = null;
                        lock (teamStateLock)
                        {
                            if (teamRevertActive.Contains(steamId)) return true;
                            if (oldTeam != null) previousTeam = oldTeam;
                            else if (lastKnownPlayerTeam.TryGetValue(steamId, out var prev)) previousTeam = prev;
                        }

                        if (previousTeam != null)
                        {
                            lock (teamStateLock) { teamRevertActive.Add(steamId); }
                            try
                            {
                                if (TrySetPlayerTeamBySteamId(steamId, previousTeam))
                                {
                                    Debug.Log($"[{Constants.MOD_NAME}] Reverted team change for {steamId} to {FormatTeamValue(previousTeam)}");
                                    TryWarnTeamSwitchBlockedBySteamId(steamId);
                                }
                                else
                                {
                                    Debug.Log($"[{Constants.MOD_NAME}] Failed to revert team change for {steamId}");
                                }
                            }
                            finally
                            {
                                lock (teamStateLock) { teamRevertActive.Remove(steamId); }
                            }
                        }
                    }
                }
                else
                {
                    // Fall back: if we have the player object from the event, revert directly.
                    if (active && playerObj != null && oldTeam != null)
                    {
                        if (TrySetPlayerTeamOnPlayer(playerObj, oldTeam))
                        {
                            Debug.Log($"[{Constants.MOD_NAME}] Reverted team change via playerObj to {FormatTeamValue(oldTeam)}");
                            TryWarnTeamSwitchBlocked(playerObj);
                        }
                    }
                }
            }
            catch { }
            return true;
        }

        private static bool PlayerSelectTeamPrefix(object __instance, object __0)
        {
            try
            {
                if (forcedTeamAssignmentDepth > 0) return true;

                var dict = __0 as Dictionary<string, object>;
                var protectActive = IsTeamSwitchProtectionActive();

                // Resolve requested team
                var requestedTeam = TryGetTeamFromDict(dict);

                // Resolve player
                var clientId = TryGetClientIdFromDict(dict);
                object player = null;
                if (clientId != 0 && TryGetPlayerByClientId(clientId, out var pByCid)) player = pByCid;
                if (player == null) player = TryGetPlayerFromController(__instance);
                if (player == null) player = TryGetPlayerFromDict(dict);

                var playerKey = TryGetPlayerIdNoFallback(player);
                if (ShouldIgnoreTransientTeamHookPlayer(player, clientId, playerKey))
                {
                    return true;
                }

                var currentTeam = GetCurrentTeamValue(player);

                Debug.Log($"[{Constants.MOD_NAME}] Event_Client_OnPlayerSelectTeam protectActive={protectActive} clientId={clientId} current={FormatTeamValue(currentTeam)} requested={FormatTeamValue(requestedTeam)}");

                if (IsInternalTeamAssignmentAllowed(player, clientId, requestedTeam, false))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] PlayerSelectTeamPrefix: allowing internal assignment {FormatTeamValue(currentTeam)} -> {FormatTeamValue(requestedTeam)}");
                    return true;
                }

                // Selecting same team: do nothing and avoid side effects.
                if (AreTeamValuesEqual(currentTeam, requestedTeam))
                {
                    TryHandleSameTeamPositionSelectionRequest(player, currentTeam, requestedTeam);
                    Debug.Log($"[{Constants.MOD_NAME}] PlayerSelectTeamPrefix: same team selected, skipping.");
                    return false;
                }

                if (!protectActive) return true;

                // Allow initial assignment when joining mid-match: current is None/Unknown and requested is a real team
                if (!draftTeamLockActive && IsTeamNoneLike(currentTeam) && !IsTeamNoneLike(requestedTeam))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] PlayerSelectTeamPrefix: allowing initial assignment during match.");
                    return true;
                }

                // Block manual team changes during active match
                TryWarnTeamSwitchBlocked(player);
                return false;
            }
            catch { }

            return true;
        }

        private static bool PlayerTeamSetPrefix(object __instance, object __0)
        {
            try
            {
                if (forcedTeamAssignmentDepth > 0) return true;

                var playerKey = TryGetPlayerIdNoFallback(__instance);
                if (ShouldIgnoreTransientTeamHookPlayer(__instance, 0UL, playerKey))
                {
                    return true;
                }

                var protectActive = IsTeamSwitchProtectionActive();
                if (!protectActive) return true;

                // Determine current vs requested team
                var currentTeam = GetCurrentTeamValue(__instance);
                var requestedTeam = __0;

                if (IsInternalTeamAssignmentAllowed(__instance, 0UL, requestedTeam, false))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] PlayerTeamSetPrefix: allowing internal assignment {FormatTeamValue(currentTeam)} -> {FormatTeamValue(requestedTeam)}");
                    return true;
                }

                if (AreTeamValuesEqual(currentTeam, requestedTeam))
                {
                    TryHandleSameTeamPositionSelectionRequest(__instance, currentTeam, requestedTeam);
                    // Same team selected: do nothing (avoid double-switch side effects)
                    Debug.Log($"[{Constants.MOD_NAME}] PlayerTeamSetPrefix: same team selected ({FormatTeamValue(currentTeam)}), skipping.");
                    return false;
                }

                // Allow initial team assignment while match is active (joining mid-game): current == None/Unknown, requested is a real team
                if (!draftTeamLockActive && IsTeamNoneLike(currentTeam) && !IsTeamNoneLike(requestedTeam))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] PlayerTeamSetPrefix: allowing initial assignment {FormatTeamValue(currentTeam)} -> {FormatTeamValue(requestedTeam)}");
                    return true;
                }

                // Block any real manual switch (including intermediate set to None)
                Debug.Log($"[{Constants.MOD_NAME}] PlayerTeamSetPrefix BLOCKED: {FormatTeamValue(currentTeam)} -> {FormatTeamValue(requestedTeam)}");
                TryWarnTeamSwitchBlocked(__instance);
                return false;
            }
            catch { }
            return true;
        }

        // Harmony prefix: returns false to skip original spawn when a match is active
        private static bool SpawnPrefix()
        {
            try
            {
                if (manualSpawnDepth > 0 && IsMatchActive())
                {
                    Debug.Log($"[{Constants.MOD_NAME}] Blocked spawn call because a match is active.");
                    return false; // skip original
                }
            }
            catch { }
            return true; // allow original
        }

        private static void GameStateUpdatePostfix(object __instance, object __0, object __1, object __2, object __3, object __4)
        {
            try
            {
                string previousPhaseName = null;
                string currentPhaseName = null;

                if (TryGetGamePhaseValue(__0, out var phaseStrTracked))
                {
                    currentPhaseName = phaseStrTracked;
                    lock (phaseLock)
                    {
                        previousPhaseName = lastGamePhaseName;
                        lastGamePhaseName = phaseStrTracked;
                        lastGamePhaseUpdateTime = Time.unscaledTime;
                    }

                    if (!string.Equals(previousPhaseName, phaseStrTracked, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log($"[{Constants.MOD_NAME}] [PHASE] {previousPhaseName ?? "unknown"} -> {phaseStrTracked}. rankedActive={rankedActive} draftActive={draftActive} voteActive={rankedVoteActive} manualSpawnDepth={manualSpawnDepth}");
                    }

                    var p = (phaseStrTracked ?? string.Empty).ToLowerInvariant();
                    if (p.Contains("gameover") || p.Contains("periodover") || p.Contains("warm") || p.Contains("practice") || p.Contains("training"))
                    {
                        lock (teamStateLock)
                        {
                            lastKnownPlayerTeam.Clear();
                            teamRevertActive.Clear();
                        }
                    }
                }

                TryAnnounceSingleGoalieCommandHint(__instance, previousPhaseName, currentPhaseName);

                // Check if this is GameOver phase to capture final scores
                if (TryGetGamePhaseValue(__0, out var phaseStr) && phaseStr.Contains("gameover"))
                {
                    if (TryGetScoresFromGameState(__instance, out var red, out var blue))
                    {
                        lastRedScore = red;
                        lastBlueScore = blue;
                        lastScoreUpdateTime = Time.unscaledTime;
                        Debug.Log($"[{Constants.MOD_NAME}] GameOver detected! Final scores -> Red:{red}, Blue:{blue}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                try { Debug.LogError($"[{Constants.MOD_NAME}] GameState update postfix error: {ex.Message}"); } catch { }
            }
        }

        private static void TryAnnounceSingleGoalieCommandHint(object gameManagerInstance, string previousPhaseName, string currentPhaseName)
        {
            try
            {
                if (!rankedActive || draftActive || singleGoalieEnabled)
                {
                    return;
                }

                if (!TryGetPeriodFromGameState(gameManagerInstance, out var period) || period <= 0)
                {
                    return;
                }

                var phase = (currentPhaseName ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(phase))
                {
                    return;
                }

                if (!IsTrackedLiveMatchPhase(phase))
                {
                    return;
                }

                if (!singleGoalieHintAnnouncedPeriods.Add(period))
                {
                    return;
                }

                SendSystemChatToAll($"<size=14><color=#66ccff>Shared Goalie</color> if your lobby has one active goalie, use <b>/votesinglegoalie</b> to start the shared-goalie vote. (Period {period})</size>");
            }
            catch { }
        }

        private static void GoalScoredPostfix(object __instance, object[] __args)
        {
            try
            {
                TeamResult team = TeamResult.Unknown;
                object scorer = null;
                if (__args != null)
                {
                    foreach (var a in __args)
                    {
                        try
                        {
                            var t = ConvertTeamValue(a);
                            if (t != TeamResult.Unknown) team = t;
                        }
                        catch { }

                        if (scorer == null)
                        {
                            try
                            {
                                var pid = TryGetPlayerId(a, 0UL);
                                if (!string.IsNullOrEmpty(pid)) scorer = a;
                            }
                            catch { }
                        }
                    }
                }

                lock (goalLock)
                {
                    if (team == TeamResult.Red) currentRedGoals++;
                    else if (team == TeamResult.Blue) currentBlueGoals++;
                }

                // Track per-player goals when we can identify scorer
                if (scorer != null)
                {
                    try
                    {
                        var pid = TryGetPlayerId(scorer, 0UL) ?? TryGetPlayerName(scorer) ?? "unknown";
                        var key = ResolvePlayerObjectKey(scorer, 0UL);
                        if (string.IsNullOrEmpty(key) || key == "clientId:0")
                        {
                            var name = TryGetPlayerName(scorer) ?? pid;
                            if (TryResolveSteamIdFromScoreboardByName(name, out var sid)) key = sid;
                        }
                        if (string.IsNullOrEmpty(key)) key = pid;
                        lock (playerGoalLock)
                        {
                            if (!playerGoalCounts.TryGetValue(key, out var c)) c = 0;
                            c++;
                            playerGoalCounts[key] = c;
                        }
                        Debug.Log($"[{Constants.MOD_NAME}] Goal detected: {team}. Totals -> Red:{currentRedGoals}, Blue:{currentBlueGoals}. Scorer: {pid} (key: {key}, goals: {playerGoalCounts[key]})");
                    }
                    catch { }
                }
                else
                {
                    Debug.Log($"[{Constants.MOD_NAME}] Goal detected: {team}. Totals -> Red:{currentRedGoals}, Blue:{currentBlueGoals}");
                }
            }
            catch (Exception ex)
            {
                try { Debug.LogError($"[{Constants.MOD_NAME}] GoalScoredPostfix error: {ex.Message}"); } catch { }
            }
        }

        private static void RankedMatchEndPostfix(object __instance)
        {
            try
            {
                if (!rankedActive) return;
                if (Time.unscaledTime - lastRankedEndTime < 1f) return;
                var winner = ResolveBestRankedWinner(__instance, TeamResult.Unknown);
                if (winner == TeamResult.Unknown)
                {
                    Debug.LogError($"[{Constants.MOD_NAME}] RankedMatchEnd could not detect a winner; treating the result as a draw.");
                    LogWinnerCandidateValues("RankedMatchEnd context", __instance);
                    LogManagerWinnerCandidates();
                }

                EndMatch(winner, requestRuntimeEnd: false, forceRequestedWinner: true);
            }
            catch { }
        }

        public static bool EndMatch(TeamResult requestedWinner, bool requestRuntimeEnd, bool forceRequestedWinner)
        {
            try
            {
                lock (matchEndLock)
                {
                    if (!rankedActive || rankedMatchEnding)
                    {
                        return false;
                    }

                    rankedMatchEnding = true;
                    lastRankedEndTime = Time.unscaledTime;
                }

                var resolvedWinner = forceRequestedWinner
                    ? requestedWinner
                    : ResolveBestRankedWinner(null, requestedWinner);
                if (resolvedWinner == TeamResult.Unknown && requestedWinner != TeamResult.Unknown)
                {
                    resolvedWinner = requestedWinner;
                }

                FinalizeRankedMatchEnd(resolvedWinner);

                if (requestRuntimeEnd && !TryEndMatch())
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] EndMatch could not trigger the runtime game-over path after publishing post-match results.");
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] EndMatch failed: {ex.Message}");
                return false;
            }
            finally
            {
                lock (matchEndLock)
                {
                    rankedMatchEnding = false;
                }
            }
        }

        private static TeamResult ResolveBestRankedWinner(object context, TeamResult fallbackWinner)
        {
            try
            {
                if (fallbackWinner != TeamResult.Unknown)
                {
                    return fallbackWinner;
                }

                if (TryGetWinnerTeam(context, out var winner) && winner != TeamResult.Unknown)
                {
                    return winner;
                }
            }
            catch { }

            return TeamResult.Unknown;
        }

        private static void FinalizeRankedMatchEnd(TeamResult winner)
        {
            ClearForfeitVoteState();
            ApplyRankedResults(winner);
        }

        #endif

        public static bool EndMatch(TeamResult requestedWinner, bool requestRuntimeEnd, bool forceRequestedWinner)
        {
            try
            {
                Debug.Log($"[{Constants.MOD_NAME}] EndMatch called. requestedWinner={requestedWinner} requestRuntimeEnd={requestRuntimeEnd} forceRequestedWinner={forceRequestedWinner}");

                lock (matchEndLock)
                {
                    if (!rankedActive || rankedMatchEnding)
                    {
                        return false;
                    }

                    rankedMatchEnding = true;
                    lastRankedEndTime = Time.unscaledTime;
                }

                var resolvedWinner = forceRequestedWinner
                    ? requestedWinner
                    : ResolveBestRankedWinner(requestedWinner);
                if (resolvedWinner == TeamResult.Unknown && requestedWinner != TeamResult.Unknown)
                {
                    resolvedWinner = requestedWinner;
                }

                FinalizeRankedMatchEnd(resolvedWinner);

                if (requestRuntimeEnd && !TryEndMatch())
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] EndMatch could not trigger the runtime game-over path after publishing post-match results.");
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] EndMatch failed: {ex.Message}");
                return false;
            }
            finally
            {
                lock (matchEndLock)
                {
                    rankedMatchEnding = false;
                }
            }
        }

        private static TeamResult ResolveBestRankedWinner(TeamResult fallbackWinner)
        {
            try
            {
                if (fallbackWinner != TeamResult.Unknown)
                {
                    return fallbackWinner;
                }

                if (TryGetWinnerTeam(null, out var winner) && winner != TeamResult.Unknown)
                {
                    return winner;
                }
            }
            catch { }

            return TeamResult.Unknown;
        }

        private static void FinalizeRankedMatchEnd(TeamResult winner)
        {
            ClearForfeitVoteState();
            ApplyRankedResults(winner);
        }

        public static void HandleForfeitVote(object player, ulong clientId)
        {
            try
            {
                HandleForfeitVoteStart(player, clientId);
            }
            catch { }
        }

        public static void HandleForfeitVoteStart(object player, ulong clientId)
        {
            try
            {
                if (!rankedActive)
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Ranked</color> /ff is only available in active ranked matches.</size>", clientId);
                    return;
                }
                if (draftActive)
                {
                    SendSystemChatToClient("<size=14><color=#ffcc66>Ranked</color> /ff is disabled while the captain draft is in progress.</size>", clientId);
                    return;
                }
                if (player == null) return;
                if (!TryResolveForfeitTeam(player, clientId, out var team)) return;

                lock (forfeitVotes)
                {
                    if (!forfeitActive)
                    {
                        var snapshot = CaptureForfeitEligibleParticipants(team);
                        if (snapshot == null || snapshot.Count == 0 || !IsClientInSnapshot(snapshot, clientId))
                        {
                            SendSystemChatToClient("<size=13><color=#ff6666>Forfeit</color> no eligible teammates were found for this vote.</size>", clientId);
                            return;
                        }

                        forfeitActive = true;
                        forfeitStartTime = Time.unscaledTime;
                        forfeitTeam = team;
                        forfeitVotes.Clear();
                        forfeitNoVotes.Clear();
                        forfeitEligibleSnapshot = snapshot;
                    }
                    else if (forfeitTeam != TeamResult.Unknown && team != forfeitTeam)
                    {
                        SendSystemChatToClient("<size=13>Only the team that started the forfeit can vote.</size>", clientId);
                        return;
                    }
                    else
                    {
                        SendSystemChatToClient("<size=13>/ff is already active. Use /y or /n to vote.</size>", clientId);
                        return;
                    }

                    if (!forfeitVotes.TryGetValue(team, out var set)) { set = new HashSet<ulong>(); forfeitVotes[team] = set; }
                    set.Add(clientId);
                    if (forfeitNoVotes.TryGetValue(team, out var noSet)) noSet.Remove(clientId);
                }

                var pname = TryGetPlayerName(player) ?? $"Player {clientId}";
                SendSystemChatToAll($"<size=14><color=#ffcc66>Forfeit</color> vote started by {pname}. Their vote was counted as <b>yes</b>. Use <b>/y</b> or <b>/n</b>. ({Mathf.CeilToInt(forfeitDuration)}s)</size>");
                BroadcastForfeitVoteCount(team);
                TryFinalizeForfeitIfAllVoted(team);
            }
            catch { }
        }

        public static void HandleForfeitVoteResponse(object player, ulong clientId, bool accept)
        {
            try
            {
                if (!rankedActive) return;
                if (!forfeitActive)
                {
                    SendSystemChatToClient("<size=13>No forfeit vote in progress.</size>", clientId);
                    return;
                }
                if (player == null) return;
                if (!TryResolveForfeitTeam(player, clientId, out var team)) return;

                if (forfeitTeam != TeamResult.Unknown && team != forfeitTeam)
                {
                    SendSystemChatToClient("<size=13>Only the team that started the forfeit can vote.</size>", clientId);
                    return;
                }

                var snapshot = ResolveCurrentForfeitEligibleParticipants();
                if (snapshot == null || snapshot.Count == 0)
                {
                    SendSystemChatToClient("<size=13><color=#ff6666>Forfeit</color> vote snapshot is unavailable.</size>", clientId);
                    ClearForfeitVoteState();
                    return;
                }

                if (!IsClientInSnapshot(snapshot, clientId))
                {
                    SendSystemChatToClient("<size=13><color=#ff6666>Forfeit</color> you are not part of this forfeit vote.</size>", clientId);
                    return;
                }

                lock (forfeitVotes)
                {
                    if (!forfeitVotes.TryGetValue(team, out var yesSet)) { yesSet = new HashSet<ulong>(); forfeitVotes[team] = yesSet; }
                    if (!forfeitNoVotes.TryGetValue(team, out var noSet)) { noSet = new HashSet<ulong>(); forfeitNoVotes[team] = noSet; }
                    if (yesSet.Contains(clientId) || noSet.Contains(clientId))
                    {
                        SendSystemChatToClient("<size=13>You already voted for /ff.</size>", clientId);
                        return;
                    }

                    if (accept) yesSet.Add(clientId); else noSet.Add(clientId);
                }

                var pname = TryGetPlayerName(player) ?? $"Player {clientId}";
                var voteText = accept ? "yes" : "no";
                SendSystemChatToAll($"<size=14>{pname} voted {voteText} for /ff.</size>");
                BroadcastForfeitVoteCount(team);
                TryFinalizeForfeitIfAllVoted(team);
            }
            catch { }
        }

        private static bool TryResolveForfeitTeam(object player, ulong clientId, out TeamResult team)
        {
            team = TeamResult.Unknown;
            if (player == null) return false;
            if (TryGetPlayerTeam(player, out team) && team != TeamResult.Unknown) return true;
            if (TryGetPlayerTeamFromManager(clientId, out var managerTeam) && managerTeam != TeamResult.Unknown)
            {
                team = managerTeam;
                Debug.Log($"[{Constants.MOD_NAME}] Forfeit: resolved team {team} for client {clientId} via PlayerManager.");
                return true;
            }
            if (TryGetTeamFromScoreboard(clientId, TryGetPlayerName(player), out var scoreboardTeam) && scoreboardTeam != TeamResult.Unknown)
            {
                team = scoreboardTeam;
                Debug.Log($"[{Constants.MOD_NAME}] Forfeit: resolved team {team} for client {clientId} via Scoreboard.");
                return true;
            }

            // Try to resolve by enumerating players as a fallback
            var allPlayers = GetAllPlayers();
            foreach (var p in allPlayers)
            {
                try
                {
                    if (TryGetClientId(p, out var cid) && cid == clientId)
                    {
                        if (TryGetPlayerTeam(p, out var t) && t != TeamResult.Unknown) { team = t; player = p; break; }
                    }
                }
                catch { }
            }
                if (team == TeamResult.Unknown)
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Ranked</color> cannot determine your team for /ff.</size>", clientId);
                    return false;
                }
            return true;
        }

        private static void BroadcastForfeitVoteCount(TeamResult team)
        {
            var snapshot = ResolveCurrentForfeitEligibleParticipants();
            if (snapshot == null || snapshot.Count == 0)
            {
                SendSystemChatToAll("<size=14><color=#ffcc66>Forfeit</color> vote: snapshot unavailable.</size>");
                return;
            }

            CountForfeitVotes(team, snapshot, out var total, out var yesVotes, out var noVotes);
            SendSystemChatToAll($"<size=14><color=#ffcc66>Forfeit</color> vote: {yesVotes} yes / {noVotes} no (total {yesVotes + noVotes}/{Math.Max(1, total)})</size>");
        }

        private static void TryFinalizeForfeitIfAllVoted(TeamResult team)
        {
            var snapshot = ResolveCurrentForfeitEligibleParticipants();
            if (snapshot == null || snapshot.Count == 0)
            {
                ClearForfeitVoteState();
                return;
            }

            CountForfeitVotes(team, snapshot, out var total, out var yesVotes, out var noVotes);
            if (total <= 0)
            {
                ClearForfeitVoteState();
                return;
            }

            var requiredYes = (total / 2) + 1;
            if (yesVotes >= requiredYes)
            {
                var winner = team == TeamResult.Red ? TeamResult.Blue : TeamResult.Red;
                ClearForfeitVoteState();
                SendSystemChatToAll($"<size=14><color=#ff6666>Forfeit</color> vote passed. {winner} wins the match.</size>");
                EndMatch(winner, requestRuntimeEnd: true, forceRequestedWinner: true);
                return;
            }

            if (yesVotes + noVotes < total && noVotes <= total - requiredYes) return;

            ClearForfeitVoteState();
            SendSystemChatToAll("<size=14><color=#ff6666>Forfeit</color> vote failed.</size>");
        }

        private static void ProcessForfeitVotes()
        {
            try
            {
                if (!forfeitActive) return;
                if (Time.unscaledTime - forfeitStartTime < forfeitDuration) return;

                var team = forfeitTeam;
                var snapshot = ResolveCurrentForfeitEligibleParticipants();
                if (team == TeamResult.Unknown || snapshot == null || snapshot.Count == 0)
                {
                    ClearForfeitVoteState();
                    return;
                }

                CountForfeitVotes(team, snapshot, out var total, out var yesVotes, out var noVotes);
                var requiredYes = (total / 2) + 1;
                if (yesVotes >= requiredYes)
                {
                    var winner = team == TeamResult.Red ? TeamResult.Blue : TeamResult.Red;
                    ClearForfeitVoteState();
                    SendSystemChatToAll($"<size=14><color=#ff6666>Forfeit</color> vote passed. {winner} wins the match.</size>");
                    EndMatch(winner, requestRuntimeEnd: true, forceRequestedWinner: true);
                    return;
                }

                ClearForfeitVoteState();
                SendSystemChatToAll(total > 0 && yesVotes + noVotes < total
                    ? "<size=14><color=#ff6666>Forfeit</color> vote timed out.</size>"
                    : "<size=14><color=#ff6666>Forfeit</color> vote failed.</size>");
            }
            catch { }
        }

        private static readonly string[] ScoreboardEntryEnumerableNames = { "Entries", "Players", "PlayerEntries", "Rows", "entries", "players" };
        private static readonly string[] ScoreboardSteamIdNames = { "SteamId", "steamId", "SteamID", "steamID", "PlayerId", "Id", "id" };
        private static readonly string[] ScoreboardNameFields = { "Name", "PlayerName", "DisplayName", "Username", "NameLabel" };
        private static readonly string[] ScoreboardGoalNames = { "Goals", "goals", "GoalsCount", "GoalsText" };
        private static readonly string[] ScoreboardAssistNames = { "Assists", "assists", "AssistsCount" };
        private static readonly string[] ScoreboardTeamFields = { "Team", "team", "TeamIndex", "teamIndex", "TeamId", "teamId", "TeamSide", "side" };
        private static readonly string[] ScoreboardClientIdFields = { "ClientId", "clientId", "OwnerClientId", "ownerClientId", "PlayerClientId" };

        private static bool TryGetMvpFromScoreboard(out string steamId, out string displayName, out TeamResult team, out int goals, out int assists)
        {
            steamId = null; displayName = null; team = TeamResult.Unknown; goals = 0; assists = 0;
            if (!TryGetScoreboardEntries(out var entries) || entries == null) return false;
            foreach (var entry in entries)
            {
                try
                {
                    if (entry == null) continue;
                    if (!TryGetEntryStringValue(entry, ScoreboardSteamIdNames, out var sid) && !TryGetEntryStringValue(entry, ScoreboardNameFields, out sid)) continue;
                    TryGetEntryStringValue(entry, ScoreboardNameFields, out var name);
                    TryGetEntryTeam(entry, out var entryTeam);
                    TryGetEntryIntValue(entry, ScoreboardGoalNames, out var g);
                    TryGetEntryIntValue(entry, ScoreboardAssistNames, out var a);
                    if (g > goals || (g == goals && a > assists))
                    {
                        goals = g; assists = a; steamId = sid; displayName = name ?? sid; team = entryTeam;
                    }
                }
                catch { }
            }
            return !string.IsNullOrEmpty(steamId) || !string.IsNullOrEmpty(displayName);
        }

        private static bool TryResolveSteamIdFromScoreboardByName(string name, out string steamId)
        {
            steamId = null;
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (!TryGetScoreboardEntries(out var entries) || entries == null) return false;
            foreach (var entry in entries)
            {
                try
                {
                    if (!TryGetEntryStringValue(entry, ScoreboardNameFields, out var entryName)) continue;
                    if (!string.Equals(entryName, name, StringComparison.OrdinalIgnoreCase)) continue;
                    if (TryGetEntryStringValue(entry, ScoreboardSteamIdNames, out var sid) && !string.IsNullOrEmpty(sid))
                    {
                        steamId = sid;
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }

        private static bool TryGetScoreboardEntries(out List<object> entries)
        {
            entries = null;
            string[] typeNames = { "UIScoreboardController", "ScoreboardController", "Scoreboard" };
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var tn in typeNames)
                {
                    var t = asm.GetType(tn) ?? asm.GetType($"Puck.{tn}");
                    if (t == null) continue;
                    var inst = GetManagerInstance(t);
                    if (inst == null) continue;
                    var list = TryGetScoreboardEnumerable(inst);
                    if (list == null) continue;
                    var buffer = new List<object>();
                    foreach (var item in list) { if (item != null) buffer.Add(item); }
                    if (buffer.Count == 0) continue;
                    entries = buffer;
                    return true;
                }
            }
            return false;
        }

        private static System.Collections.IEnumerable TryGetScoreboardEnumerable(object controller)
        {
            if (controller == null) return null;
            var enumerable = TryGetEnumerable(controller, ScoreboardEntryEnumerableNames);
            if (enumerable != null) return enumerable;
            var t = controller.GetType();
            foreach (var prop in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
            {
                try
                {
                    if (typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType))
                    {
                        var val = prop.GetValue(controller) as System.Collections.IEnumerable;
                        if (val != null) return val;
                    }
                }
                catch { }
            }
            foreach (var field in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
            {
                try
                {
                    if (typeof(System.Collections.IEnumerable).IsAssignableFrom(field.FieldType))
                    {
                        var val = field.GetValue(controller) as System.Collections.IEnumerable;
                        if (val != null) return val;
                    }
                }
                catch { }
            }
            return null;
        }

        private static bool TryGetEntryMemberValue(object entry, string name, out object value)
        {
            value = null;
            if (entry == null || string.IsNullOrEmpty(name)) return false;
            var t = entry.GetType();
            try
            {
                var prop = t.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (prop != null)
                {
                    value = prop.GetValue(entry);
                    return true;
                }
            }
            catch { }
            try
            {
                var field = t.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    value = field.GetValue(entry);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool TryGetEntryStringValue(object entry, string[] names, out string value)
        {
            value = null;
            if (entry == null || names == null) return false;
            foreach (var name in names)
            {
                if (TryGetEntryMemberValue(entry, name, out var member))
                {
                    var text = ExtractSimpleValueToString(member);
                    if (!string.IsNullOrEmpty(text))
                    {
                        value = text;
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool TryGetEntryIntValue(object entry, string[] names, out int value)
        {
            value = 0;
            if (entry == null || names == null) return false;
            foreach (var name in names)
            {
                if (TryGetEntryMemberValue(entry, name, out var member))
                {
                    if (TryConvertToInt(member, out var parsed))
                    {
                        value = parsed;
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool TryGetEntryTeam(object entry, out TeamResult team)
        {
            team = TeamResult.Unknown;
            if (entry == null) return false;
            foreach (var name in ScoreboardTeamFields)
            {
                if (TryGetEntryMemberValue(entry, name, out var member))
                {
                    var conv = ConvertTeamValue(member);
                    if (conv != TeamResult.Unknown)
                    {
                        team = conv;
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool TryGetEntryClientId(object entry, out ulong clientId)
        {
            clientId = 0;
            if (entry == null) return false;
            foreach (var name in ScoreboardClientIdFields)
            {
                if (TryGetEntryMemberValue(entry, name, out var member))
                {
                    if (TryConvertToUlong(member, out var parsed))
                    {
                        clientId = parsed;
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool TryMatchScoreboardEntryToClient(object entry, ulong clientId)
        {
            if (entry == null) return false;
            if (TryGetEntryClientId(entry, out var cid) && cid == clientId) return true;
            if (TryGetEntryStringValue(entry, ScoreboardSteamIdNames, out var sid))
            {
                var resolved = ResolveStoredIdToSteam($"clientId:{clientId}");
                if (!string.IsNullOrEmpty(resolved) && string.Equals(resolved, sid, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool TryGetTeamFromScoreboard(ulong clientId, string playerName, out TeamResult team)
        {
            team = TeamResult.Unknown;
            if (!TryGetScoreboardEntries(out var entries) || entries == null) return false;
            foreach (var entry in entries)
            {
                try
                {
                    var matched = TryMatchScoreboardEntryToClient(entry, clientId);
                    if (!matched && !string.IsNullOrWhiteSpace(playerName))
                    {
                        if (TryGetEntryStringValue(entry, ScoreboardNameFields, out var entryName))
                        {
                            matched = string.Equals(entryName, playerName, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                    if (!matched) continue;
                    if (TryGetEntryTeam(entry, out var found) && found != TeamResult.Unknown)
                    {
                        team = found;
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }

        private static bool TryGetWinnerTeam(object context, out TeamResult winner)
        {
            winner = TeamResult.Unknown;
            if (TryGetWinnerTeamFromObject(context, out winner)) return winner != TeamResult.Unknown;
            string[] typeNames = { "GameManager", "MatchManager", "GameController", "GameStateManager", "ScoreManager" };
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var tn in typeNames)
                {
                    var t = asm.GetType(tn) ?? asm.GetType($"Puck.{tn}");
                    if (t == null) continue;
                    var inst = GetManagerInstance(t);
                    if (inst == null) continue;
                    if (TryGetWinnerTeamFromObject(inst, out winner)) return winner != TeamResult.Unknown;
                }
            }
            if (TryGetWinnerFromGoalCounts(out winner)) return winner != TeamResult.Unknown;
            if (TryGetWinnerFromLastScore(out winner)) return winner != TeamResult.Unknown;
            return false;
        }

        private static bool TryGetWinnerTeamFromObject(object obj, out TeamResult winner)
        {
            winner = TeamResult.Unknown;
            if (obj == null) return false;
            var t = obj.GetType();
            string[] names = { "WinningTeam", "WinnerTeam", "Winner", "winningTeam", "winnerTeam", "TeamThatWon", "teamThatWon" };
            foreach (var n in names)
            {
                object val = null;
                var prop = t.GetProperty(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (prop != null) val = prop.GetValue(obj);
                if (val == null)
                {
                    var field = t.GetField(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (field != null) val = field.GetValue(obj);
                }

                if (val != null)
                {
                    winner = ConvertTeamValue(val);
                    if (winner != TeamResult.Unknown) return true;
                }
            }

            // Fallback: try to detect winner by examining score-like properties/fields (e.g., RedScore/BlueScore)
            try
            {
                var scoreCandidates = new Dictionary<string, int>();
                foreach (var prop in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
                {
                    try
                    {
                        var pn = prop.Name.ToLowerInvariant();
                        if (!pn.Contains("score") && !pn.Contains("points")) continue;
                        var val = prop.GetValue(obj);
                        if (val == null) continue;
                        if (val is int iv) scoreCandidates[prop.Name] = iv;
                        else if (val is short sv) scoreCandidates[prop.Name] = sv;
                        else if (val is byte bv) scoreCandidates[prop.Name] = bv;
                        else if (int.TryParse(val.ToString(), out var parsed)) scoreCandidates[prop.Name] = parsed;
                    }
                    catch { }
                }

                foreach (var field in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
                {
                    try
                    {
                        var fn = field.Name.ToLowerInvariant();
                        if (!fn.Contains("score") && !fn.Contains("points")) continue;
                        var val = field.GetValue(obj);
                        if (val == null) continue;
                        if (val is int iv) scoreCandidates[field.Name] = iv;
                        else if (val is short sv) scoreCandidates[field.Name] = sv;
                        else if (val is byte bv) scoreCandidates[field.Name] = bv;
                        else if (int.TryParse(val.ToString(), out var parsed)) scoreCandidates[field.Name] = parsed;
                    }
                    catch { }
                }

                if (scoreCandidates.Count >= 2)
                {
                    // Try to pair Red/Blue names first
                    int? redScore = null, blueScore = null;
                    foreach (var kv in scoreCandidates)
                    {
                        var n = kv.Key.ToLowerInvariant();
                        if (n.Contains("red")) redScore = kv.Value;
                        else if (n.Contains("blue")) blueScore = kv.Value;
                    }
                    if (redScore.HasValue && blueScore.HasValue)
                    {
                        if (redScore.Value > blueScore.Value) { winner = TeamResult.Red; return true; }
                        if (blueScore.Value > redScore.Value) { winner = TeamResult.Blue; return true; }
                        return false;
                    }

                    // If no explicit red/blue fields, compare top two numeric scores
                    var ordered = new List<KeyValuePair<string,int>>(scoreCandidates);
                    ordered.Sort((a,b) => b.Value.CompareTo(a.Value));
                    if (ordered.Count >= 2)
                    {
                        if (ordered[0].Value > ordered[1].Value)
                        {
                            // best-effort: choose team by key name containing red/blue
                            var topName = ordered[0].Key.ToLowerInvariant();
                            if (topName.Contains("red")) { winner = TeamResult.Red; return true; }
                            if (topName.Contains("blue")) { winner = TeamResult.Blue; return true; }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                try { Debug.LogError($"[{Constants.MOD_NAME}] Score-based winner detection error: {ex.Message}"); } catch { }
            }

            // Extra fallback: try to find any property that explicitly says which team won via textual content
            try
            {
                foreach (var prop in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
                {
                    try
                    {
                        var val = prop.GetValue(obj);
                        if (val == null) continue;
                        var s = val.ToString().ToLowerInvariant();
                        if (s.Contains("red")) { winner = TeamResult.Red; return true; }
                        if (s.Contains("blue")) { winner = TeamResult.Blue; return true; }
                    }
                    catch { }
                }
            }
            catch { }
            return false;
        }

        private static bool TryGetWinnerFromLastScore(out TeamResult winner)
        {
            winner = TeamResult.Unknown;
            try
            {
                if (!lastRedScore.HasValue || !lastBlueScore.HasValue) return false;
                if (Time.unscaledTime - lastScoreUpdateTime > 120f) return false;
                if (lastRedScore.Value > lastBlueScore.Value) { winner = TeamResult.Red; return true; }
                if (lastBlueScore.Value > lastRedScore.Value) { winner = TeamResult.Blue; return true; }
            }
            catch { }
            return false;
        }

        private static void ResetGoalCounts()
        {
            lock (goalLock)
            {
                currentRedGoals = 0;
                currentBlueGoals = 0;
            }
            lock (playerGoalLock)
            {
                playerGoalCounts.Clear();
            }
            lock (playerAssistLock)
            {
                playerPrimaryAssistCounts.Clear();
                playerSecondaryAssistCounts.Clear();
            }
            lock (forfeitVotes)
            {
                forfeitVotes.Clear();
                forfeitNoVotes.Clear();
                forfeitActive = false;
                forfeitStartTime = -999f;
                forfeitTeam = TeamResult.Unknown;
                forfeitEligibleSnapshot = null;
            }
            lock (phaseLock)
            {
                if (!rankedActive)
                {
                    lastGamePhaseName = null;
                    lastGamePhaseUpdateTime = -999f;
                }
            }

            singleGoalieHintAnnouncedPeriods.Clear();
        }

        private static void ResetRankedState(bool keepPhaseState, bool keepRankedVoteCooldown, bool preservePostMatchLock = false)
        {
            if (!preservePostMatchLock)
            {
                ClearPostMatchLockState();
            }

            ClearRankedLiveParticipationTracking();

            lock (rankedLock)
            {
                rankedActive = false;
                rankedVoteActive = false;
                rankedVoteStartTime = -999f;
                rankedVoteStartedByName = null;
                rankedVoteStartedByKey = null;
                rankedVoteStartedByClientId = 0;
                rankedParticipants.Clear();
                rankedVotes.Clear();
                lastVoteEligible = null;
                lastVoteSecondsRemaining = -1;
                if (!keepRankedVoteCooldown) lastRankedVoteTime = -999f;

                singleGoalieVoteActive = false;
                singleGoalieVoteStartTime = -999f;
                singleGoalieVoteStartedByName = null;
                singleGoalieVoteStartedByKey = null;
                singleGoalieVoteStartedByClientId = 0;
                singleGoalieVoteDisablesMode = false;
                singleGoalieVotes.Clear();
                singleGoalieVoteEligible = null;
            }

            lock (matchEndLock)
            {
                rankedMatchEnding = false;
            }

            lock (forfeitVotes)
            {
                forfeitVotes.Clear();
                forfeitNoVotes.Clear();
                forfeitActive = false;
                forfeitStartTime = -999f;
                forfeitTeam = TeamResult.Unknown;
                forfeitEligibleSnapshot = null;
            }

            rankedNoMatchDetectedTime = -999f;
            controlledTestModeInitiatorKey = null;
            controlledTestModeInitiatorClientId = 0;

            lock (goalLock)
            {
                currentRedGoals = 0;
                currentBlueGoals = 0;
            }

            lock (playerGoalLock)
            {
                playerGoalCounts.Clear();
            }

            lock (playerAssistLock)
            {
                playerPrimaryAssistCounts.Clear();
                playerSecondaryAssistCounts.Clear();
            }

            lock (teamStateLock)
            {
                lastKnownPlayerTeam.Clear();
                teamRevertActive.Clear();
            }

            lock (internalTeamAssignmentLock)
            {
                internalTeamAssignments.Clear();
            }

            lock (draftLock)
            {
                draftActive = false;
                draftTeamLockActive = false;
                redCaptainId = null;
                blueCaptainId = null;
                currentCaptainTurnId = null;
                draftAvailablePlayerIds.Clear();
                draftAssignedTeams.Clear();
                pendingLateJoiners.Clear();
                announcedLateJoinerIds.Clear();
            }

            singleGoalieHintAnnouncedPeriods.Clear();

            lock (approvalRequestLock)
            {
                pendingTeamApprovalRequests.Clear();
                rejectedRequestCooldownEndsAtByPlayerKey.Clear();
                rejectedLateJoinTeams.Clear();
            }

            lock (joinStateLock)
            {
                joinStateByPlayerKey.Clear();
            }

            PublishHiddenApprovalStateToAllClients();

            ResetDraftAnnouncementState();

            PublishVoteOverlayState();
            PublishDraftOverlayState();

            ClearAllDummies();
            BotManager.RemoveAllBots();

            if (!keepPhaseState)
            {
                lock (phaseLock)
                {
                    lastGamePhaseName = null;
                    lastGamePhaseUpdateTime = -999f;
                }
            }
        }

        private static bool TryGetWinnerFromGoalCounts(out TeamResult winner)
        {
            winner = TeamResult.Unknown;
            try
            {
                lock (goalLock)
                {
                    if (currentRedGoals > currentBlueGoals) { winner = TeamResult.Red; return true; }
                    if (currentBlueGoals > currentRedGoals) { winner = TeamResult.Blue; return true; }
                }
            }
            catch { }
            return false;
        }

        private static int? TryGetNullableInt(object value)
        {
            try
            {
                if (value == null) return null;
                if (value is int iv) return iv;
                if (value is short sv) return sv;
                if (value is byte bv) return bv;
                if (value is long lv) return (int)lv;
                var t = value.GetType();
                if (t.IsGenericType && t.GetGenericTypeDefinition().FullName == "System.Nullable`1")
                {
                    var hasValueProp = t.GetProperty("HasValue");
                    var valueProp = t.GetProperty("Value");
                    if (hasValueProp != null && valueProp != null)
                    {
                        var hasValue = hasValueProp.GetValue(value);
                        if (hasValue is bool b && b)
                        {
                            var inner = valueProp.GetValue(value);
                            return TryGetNullableInt(inner);
                        }
                    }
                }
                if (int.TryParse(value.ToString(), out var parsed)) return parsed;
            }
            catch { }
            return null;
        }

        private static void LogWinnerCandidateValues(string label, object obj)
        {
            try
            {
                if (obj == null) return;
                var t = obj.GetType();
                var entries = new List<string>();
                var looked = new List<string>();

                if (!PopulateWinnerEntriesFromProperties(obj, t, entries, looked) && entries.Count < 10)
                {
                    PopulateWinnerEntriesFromFields(obj, t, entries, looked);
                }

                if (entries.Count < 10 && obj is IDictionary dict)
                {
                    foreach (DictionaryEntry entry in dict)
                    {
                        if (!(entry.Key is string key)) continue;
                        if (!ContainsWinnerKeyword(key)) continue;
                        entries.Add($"{key}={(entry.Value ?? "null")}");
                        looked.Add(key);
                        if (entries.Count >= 10) break;
                    }
                }

                var entryDescription = entries.Count > 0 ? string.Join(", ", entries) : "(ninguna coincidencia)";
                var lookedDescription = looked.Count > 0 ? string.Join(", ", looked) : "(no se encontraron campos relevantes)";
                var sampleProps = t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Select(p => p.Name).Take(MaxLoggedMembers).ToList();
                var sampleFields = t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Select(f => f.Name).Take(MaxLoggedMembers).ToList();
                var propsDesc = sampleProps.Count > 0 ? string.Join(", ", sampleProps) : "(sin propiedades)";
                var fieldsDesc = sampleFields.Count > 0 ? string.Join(", ", sampleFields) : "(sin campos)";
                Debug.LogError($"[{Constants.MOD_NAME}] {label} {t.FullName} candidatos ({entries.Count}): {entryDescription}. Campos inspeccionados: {lookedDescription}. Propiedades disponibles: {propsDesc}. Campos disponibles: {fieldsDesc}");
            }
            catch { }
        }

        private static bool PopulateWinnerEntriesFromProperties(object obj, Type t, List<string> entries, List<string> looked)
        {
            foreach (var prop in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
            {
                try
                {
                    if (prop.GetIndexParameters().Length > 0) continue;
                    var name = prop.Name;
                    if (!ContainsWinnerKeyword(name)) continue;
                    looked.Add(name);
                    var val = prop.GetValue(obj);
                    entries.Add($"{name}={(val ?? "null")}");
                    if (entries.Count >= 10) return true;
                }
                catch { }
            }
            return entries.Count >= 10;
        }

        private static void PopulateWinnerEntriesFromFields(object obj, Type t, List<string> entries, List<string> looked)
        {
            foreach (var field in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
            {
                try
                {
                    var name = field.Name;
                    if (!ContainsWinnerKeyword(name)) continue;
                    looked.Add(name);
                    var val = field.GetValue(obj);
                    entries.Add($"{name}={(val ?? "null")}");
                    if (entries.Count >= 10) return;
                }
                catch { }
            }
        }

        private static bool ContainsWinnerKeyword(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var keyword in WinnerKeywords)
            {
                if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static void LogManagerWinnerCandidates()
        {
            try
            {
                string[] typeNames = { "GameManager", "MatchManager", "GameController", "GameStateManager", "ScoreManager" };
                int logged = 0;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var tn in typeNames)
                    {
                        var t = asm.GetType(tn) ?? asm.GetType($"Puck.{tn}");
                        if (t == null) continue;
                        var inst = GetManagerInstance(t);
                        if (inst == null) continue;
                        LogWinnerCandidateValues($"Manager {t.FullName}", inst);
                        logged++;
                        if (logged >= 3) return;
                    }
                }
            }
            catch { }
        }

        #if false
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

                // MVP detection: choose ranked participant with most goals tracked
                // First, prefer MVP from the scoreboard UI if available
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
                        // check both stored id and resolved steam id
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

        #endif

        #if false
        private static void RefreshRankedParticipantsFromLiveState()
        {
            try
            {
                if (!TryGetEligiblePlayers(out var liveParticipants, out _)) return;
                if (liveParticipants == null || liveParticipants.Count == 0) return;

                var merged = new Dictionary<ulong, RankedParticipant>();
                foreach (var participant in rankedParticipants)
                {
                    if (participant == null) continue;
                    if (participant.clientId == 0) continue;
                    merged[participant.clientId] = participant;
                }

                foreach (var participant in liveParticipants)
                {
                    if (participant == null) continue;
                    if (participant.clientId == 0) continue;
                    merged[participant.clientId] = participant;
                }

                if (merged.Count > 0)
                {
                    rankedParticipants = merged.Values.ToList();
                    lock (draftLock)
                    {
                        foreach (var participant in rankedParticipants)
                        {
                            var participantKey = ResolveParticipantIdToKey(participant);
                            if (!string.IsNullOrEmpty(participantKey) && draftAssignedTeams.TryGetValue(participantKey, out var assignedTeam))
                            {
                                participant.team = assignedTeam;
                            }
                        }
                    }
                    Debug.Log($"[{Constants.MOD_NAME}] Ranked participants refreshed before MMR. Count={rankedParticipants.Count}");
                }

                lock (dummyLock)
                {
                    foreach (var dummy in activeDraftDummies.Values)
                    {
                        if (dummy == null) continue;
                        var dummyParticipant = CreateParticipantFromDummy(dummy);
                        if (dummyParticipant == null) continue;
                        if (rankedParticipants.Any(p => string.Equals(ResolveParticipantIdToKey(p), dummyParticipant.playerId, StringComparison.OrdinalIgnoreCase))) continue;
                        rankedParticipants.Add(dummyParticipant);
                    }
                }
            }
            catch { }
        }

        private static bool TryStartCaptainDraft(List<RankedParticipant> participants, bool forcedByAdmin)
        {
            try
            {
                if (participants == null) participants = new List<RankedParticipant>();

                var combinedParticipants = new List<RankedParticipant>(participants.Select(CloneParticipant).Where(p => p != null));
                combinedParticipants.AddRange(ConsumeQueuedDummyParticipants());
                if (combinedParticipants.Count < 2) return false;
                rankedParticipants = combinedParticipants.Select(CloneParticipant).Where(p => p != null).ToList();

                var pool = combinedParticipants
                    .Where(p => p != null && (p.clientId != 0 || p.isDummy))
                    .Where(p => p != null && !string.IsNullOrEmpty(ResolveParticipantIdToKey(p)))
                    .GroupBy(p => ResolveParticipantIdToKey(p), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                if (pool.Count < 2) return false;

                var realCaptainPool = pool
                    .Where(p => !IsDummyParticipant(p))
                    .ToList();

                if (realCaptainPool.Count < 2) return false;

                var rng = new System.Random();
                var shuffled = realCaptainPool.OrderBy(_ => rng.Next()).ToList();
                var redCaptain = shuffled[0];
                var blueCaptain = shuffled[1];
                var redCaptainKey = ResolveParticipantIdToKey(redCaptain);
                var blueCaptainKey = ResolveParticipantIdToKey(blueCaptain);
                if (string.IsNullOrEmpty(redCaptainKey) || string.IsNullOrEmpty(blueCaptainKey)) return false;

                lock (draftLock)
                {
                    draftActive = true;
                    draftTeamLockActive = true;
                    redCaptainId = redCaptainKey;
                    blueCaptainId = blueCaptainKey;
                    currentCaptainTurnId = redCaptainKey;
                    draftAvailablePlayerIds = pool
                        .Select(ResolveParticipantIdToKey)
                        .Where(pid => !string.IsNullOrEmpty(pid) && !string.Equals(pid, redCaptainKey, StringComparison.OrdinalIgnoreCase) && !string.Equals(pid, blueCaptainKey, StringComparison.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    draftAssignedTeams.Clear();
                    pendingLateJoiners.Clear();
                    announcedLateJoinerIds.Clear();
                    draftAssignedTeams[redCaptainKey] = TeamResult.Red;
                    draftAssignedTeams[blueCaptainKey] = TeamResult.Blue;
                }

                ResetDraftAnnouncementState();
                PublishDraftOverlayState();

                ApplyDraftTeamsToParticipants();
                ForceApplyDraftTeam(redCaptainKey, TeamResult.Red);
                ForceApplyDraftTeam(blueCaptainKey, TeamResult.Blue);

                var intro = forcedByAdmin
                    ? "<size=14><color=#00ff00>Ranked</color> forced by admin. Starting captain draft.</size>"
                    : "<size=14><color=#00ff00>Ranked</color> vote accepted. Starting captain draft.</size>";
                SendSystemChatToAll(intro);
                SendSystemChatToAll(BuildDraftOverlayFallbackMessage(false, true));

                if (!draftAvailablePlayerIds.Any())
                {
                    CompleteDraftAndStartMatch();
                    return true;
                }

                AnnounceDraftTurn();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] TryStartCaptainDraft failed: {ex.Message}");
            }

            return false;
        }

        private static RankedParticipant CloneParticipant(RankedParticipant participant)
        {
            if (participant == null) return null;
            return new RankedParticipant
            {
                clientId = participant.clientId,
                playerId = participant.playerId,
                displayName = participant.displayName,
                team = participant.team,
                isDummy = participant.isDummy
            };
        }

        private static void ApplyDraftTeamsToParticipants()
        {
            lock (draftLock)
            {
                foreach (var participant in rankedParticipants)
                {
                    if (participant == null) continue;
                    var participantKey = ResolveParticipantIdToKey(participant);
                    if (!string.IsNullOrEmpty(participantKey) && draftAssignedTeams.TryGetValue(participantKey, out var assignedTeam))
                    {
                        participant.team = assignedTeam;
                    }
                }
            }
        }

            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] HandleDraftPick failed: {ex.Message}");
                SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> /pick failed.</size>", clientId);
            }
        }

        private static void HandleLateJoinAcceptance(object player, ulong clientId, string rawTarget)
        {
            try
            {
                if (!rankedActive)
                {
                    SendSystemChatToClient("<size=14><color=#ffcc66>Draft</color> there is no active ranked.</size>", clientId);
                    return;
                }

                var actorKey = ResolvePlayerObjectKey(player, clientId) ?? $"clientId:{clientId}";
                if (string.IsNullOrEmpty(actorKey))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> could not identify your player.</size>", clientId);
                    return;
                }

                TeamResult captainTeam;
                var usingSoloDummyProxy = false;
                if (!TryGetCaptainTeam(actorKey, out captainTeam))
                {
                    if (!CanUseSoloDummyCaptainProxy(player, clientId) || !TryGetCurrentCaptainTeam(out captainTeam))
                    {
                        SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> only captains can use /accept.</size>", clientId);
                        return;
                    }
                    usingSoloDummyProxy = true;
                }

                if (string.IsNullOrWhiteSpace(rawTarget))
                {
                    SendSystemChatToClient("<size=14>Usage: /accept <player></size>", clientId);
                    return;
                }

                List<RankedParticipant> pendingPlayers;
                lock (draftLock)
                {
                    pendingPlayers = pendingLateJoiners.Values.Select(CloneParticipant).ToList();
                }

                if (!pendingPlayers.Any())
                {
                    SendSystemChatToClient("<size=14><color=#ffcc66>Draft</color> there are no pending late joiners.</size>", clientId);
                    return;
                }

                if (!TryResolveParticipantFromCommand(rawTarget, pendingPlayers, out var acceptedParticipant))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> pending player not found.</size>", clientId);
                    return;
                }

                var acceptedKey = ResolveParticipantIdToKey(acceptedParticipant);
                if (string.IsNullOrEmpty(acceptedKey))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> could not identify that player.</size>", clientId);
                    return;
                }

                lock (draftLock)
                {
                    if (!pendingLateJoiners.Remove(acceptedKey))
                    {
                        SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> that player is no longer pending.</size>", clientId);
                        return;
                    }

                    draftAssignedTeams[acceptedKey] = captainTeam;
                    announcedLateJoinerIds.Remove(acceptedKey);
                }

                MergeOrReplaceParticipant(acceptedParticipant, captainTeam);
                ForceApplyDraftTeam(acceptedKey, captainTeam);
                PublishDraftOverlayState();
                var acceptingCaptainName = usingSoloDummyProxy ? GetCaptainDisplayNameByKey(string.Equals(captainTeam, TeamResult.Red) ? redCaptainId : blueCaptainId) : GetCaptainDisplayNameByKey(actorKey);
                SendSystemChatToAll($"<size=14><color=#ffcc66>Draft</color> {acceptingCaptainName ?? "Captain"} accepted late joiner <b>{acceptedParticipant.displayName}</b> for {FormatTeamLabel(captainTeam)}.</size>");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] HandleLateJoinAcceptance failed: {ex.Message}");
                SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> /accept failed.</size>", clientId);
            }
        }

        private static void MergeOrReplaceParticipant(RankedParticipant participant, TeamResult team)
        {
            if (participant == null) return;
            participant.team = team;
            var participantKey = ResolveParticipantIdToKey(participant);
            if (string.IsNullOrEmpty(participantKey)) return;

            if (participant.isDummy)
            {
                lock (dummyLock)
                {
                    if (activeDraftDummies.TryGetValue(participantKey, out var dummy)) dummy.team = team;
                }
            }

            var replaced = false;
            for (var i = 0; i < rankedParticipants.Count; i++)
            {
                var existing = rankedParticipants[i];
                if (existing == null) continue;
                var existingKey = ResolveParticipantIdToKey(existing);
                if (!string.Equals(existingKey, participantKey, StringComparison.OrdinalIgnoreCase)) continue;
                rankedParticipants[i] = participant;
                replaced = true;
                break;
            }

            if (!replaced)
            {
                rankedParticipants.Add(participant);
            }
        }

        private static void CompleteDraftIfReadyOrAnnounceTurn()
        {
            lock (draftLock)
            {
                if (!draftActive) return;
                if (draftAvailablePlayerIds.Count == 0)
                {
                    CompleteDraftAndStartMatch();
                    return;
                }
            }

            AnnounceDraftTurn();
        }

        private static void CompleteDraftAndStartMatch()
        {
            string completionPanelMessage;
            lock (draftLock)
            {
                if (!draftActive) return;
                draftActive = false;
                completionPanelMessage = BuildDraftOverlayFallbackMessage(true, false);
            }

            ForceApplyAllDraftTeams();
            ApplyDraftTeamsToParticipants();
            PublishDraftOverlayState();
            SendSystemChatToAll(completionPanelMessage);
            SendDraftTeamSummary();
            SendSystemChatToAll("<size=14><color=#00ff00>Ranked match started.</color></size>");
            TryStartMatch();
        }

        private static void AnnounceDraftTurn()
        {
            string captainName;
            string turnId;
            int remaining;
            string availableSignature;
            lock (draftLock)
            {
                if (!draftActive) return;
                turnId = currentCaptainTurnId ?? string.Empty;
                captainName = GetCaptainDisplayNameByKey(currentCaptainTurnId) ?? "Captain";
                remaining = draftAvailablePlayerIds.Count;
                availableSignature = BuildDraftAvailablePlayersSignatureLocked();
            }

            var now = Time.unscaledTime;
            var sameAnnouncement = string.Equals(lastAnnouncedTurnId, turnId, StringComparison.OrdinalIgnoreCase)
                && lastAnnouncedAvailableCount == remaining
                && string.Equals(lastAnnouncedAvailablePlayersSignature, availableSignature, StringComparison.OrdinalIgnoreCase);
            if (sameAnnouncement && now - lastDraftTurnAnnouncementTime < DraftAnnouncementMinInterval) return;

            lastAnnouncedTurnId = turnId;
            lastAnnouncedAvailableCount = remaining;
            lastAnnouncedAvailablePlayersSignature = availableSignature;
            lastDraftTurnAnnouncementTime = now;

            SendSystemChatToAll(BuildDraftOverlayFallbackMessage(false, true));
        }

        private static void SendDraftStatusToClient(ulong clientId)
        {
            try
            {
                bool isDraftActive;
                string turnName;
                int availableCount;
                int pendingCount;
                lock (draftLock)
                {
                    isDraftActive = draftActive;
                    turnName = GetCaptainDisplayNameByKey(currentCaptainTurnId) ?? "none";
                    availableCount = draftAvailablePlayerIds.Count;
                    pendingCount = pendingLateJoiners.Count;
                }

                if (!rankedActive)
                {
                    SendSystemChatToClient("<size=14><color=#ffcc66>Draft</color> there is no active ranked.</size>", clientId);
                    return;
                }

                if (!isDraftActive)
                {
                    SendSystemChatToClient(BuildDraftOverlayFallbackMessage(true, false), clientId);
                    return;
                }

                SendSystemChatToClient(BuildDraftOverlayFallbackMessage(false, true), clientId);
            }
            catch { }
        }

        private static void DetectLateJoiners()
        {
            try
            {
                if (!ShouldPollDraftPlayerChanges()) return;

                var players = GetAllPlayers();
                if (players == null || players.Count == 0) return;
                var newlyAnnouncedLateJoiners = new List<RankedParticipant>();

                foreach (var player in players)
                {
                    if (!TryBuildConnectedPlayerSnapshot(player, out var snapshot)) continue;
                    var playerKey = ResolveParticipantIdToKey(snapshot);
                    if (string.IsNullOrEmpty(playerKey)) continue;
                    if (IsKnownRankedParticipant(snapshot)) continue;

                    lock (draftLock)
                    {
                        pendingLateJoiners[playerKey] = snapshot;
                        if (announcedLateJoinerIds.Add(playerKey))
                        {
                            TryMovePlayerToNeutralState(snapshot);
                            newlyAnnouncedLateJoiners.Add(CloneParticipant(snapshot));
                        }
                    }
                }

                if (newlyAnnouncedLateJoiners.Count > 0)
                {
                    var names = string.Join(", ", newlyAnnouncedLateJoiners.Select(p => p.displayName).Where(n => !string.IsNullOrEmpty(n)));
                    PublishDraftOverlayState();
                    SendSystemChatToAll($"<size=14><color=#ffcc66>Draft</color> late joiner{(newlyAnnouncedLateJoiners.Count == 1 ? string.Empty : "s")} waiting for captain approval: <b>{names}</b>. Captains can use <b>/accept player</b> or click the scoreboard.</size>");
                }
            }
            catch { }
        }

        private static void RemoveDisconnectedDraftCandidates()
        {
            try
            {
                if (!ShouldPollDraftPlayerChanges()) return;

                var connectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var connectedClientIds = new HashSet<ulong>();
                foreach (var player in GetAllPlayers())
                {
                    if (!TryBuildConnectedPlayerSnapshot(player, out var snapshot)) continue;
                    var connectedKey = ResolveParticipantIdToKey(snapshot);
                    if (!string.IsNullOrEmpty(connectedKey)) connectedIds.Add(connectedKey);
                    if (snapshot.clientId != 0) connectedClientIds.Add(snapshot.clientId);
                }

                if (connectedIds.Count == 0 && connectedClientIds.Count == 0) return;

                List<string> removedNames = null;
                var draftPoolChanged = false;
                var pendingPoolChanged = false;
                lock (draftLock)
                {
                    if (draftActive)
                    {
                        removedNames = new List<string>();
                        for (var i = draftAvailablePlayerIds.Count - 1; i >= 0; i--)
                        {
                            var candidateId = draftAvailablePlayerIds[i];
                            if (IsDummyKey(candidateId)) continue;
                            if (connectedIds.Contains(candidateId)) continue;
                            if (TryGetParticipantByKey(candidateId, out var connectedCandidate) && connectedCandidate.clientId != 0 && connectedClientIds.Contains(connectedCandidate.clientId)) continue;
                            removedNames.Add(GetParticipantDisplayNameByKey(candidateId) ?? candidateId);
                            draftAvailablePlayerIds.RemoveAt(i);
                            draftPoolChanged = true;
                        }
                    }

                    var stalePending = pendingLateJoiners
                        .Where(entry =>
                        {
                            if (IsDummyKey(entry.Key)) return false;
                            if (connectedIds.Contains(entry.Key)) return false;
                            var pendingParticipant = entry.Value;
                            return pendingParticipant == null || pendingParticipant.clientId == 0 || !connectedClientIds.Contains(pendingParticipant.clientId);
                        })
                        .Select(entry => entry.Key)
                        .ToList();
                    foreach (var pendingId in stalePending)
                    {
                        pendingLateJoiners.Remove(pendingId);
                        announcedLateJoinerIds.Remove(pendingId);
                    }
                    pendingPoolChanged = stalePending.Count > 0;
                }

                if (removedNames != null)
                {
                    if (removedNames.Count > 0)
                    {
                        SendSystemChatToAll($"<size=14><color=#ffcc66>Draft</color> removed disconnected player{(removedNames.Count == 1 ? string.Empty : "s")} from the draft pool: <b>{string.Join(", ", removedNames)}</b>.</size>");
                    }
                }

                if (draftPoolChanged || pendingPoolChanged)
                {
                    PublishDraftOverlayState();
                }

                if (draftPoolChanged)
                {
                    CompleteDraftIfReadyOrAnnounceTurn();
                }
            }
            catch { }
        }

        private static bool TryBuildConnectedPlayerSnapshot(object player, out RankedParticipant participant)
        {
            participant = null;
            try
            {
                if (player == null) return false;
                if (!TryGetClientId(player, out var clientId) || clientId == 0) return false;
                if (IsInvalidWarmupDummyCandidate(player, clientId, out _)) return false;

                var playerId = ResolvePlayerObjectKey(player, clientId) ?? TryGetPlayerId(player, clientId);
                if (string.IsNullOrEmpty(playerId)) return false;

                var displayName = TryGetPlayerName(player) ?? $"Player {clientId}";
                var team = TeamResult.Unknown;
                TryGetPlayerTeam(player, out team);
                if (team == TeamResult.Unknown) TryGetPlayerTeamFromManager(clientId, out team);

                participant = new RankedParticipant
                {
                    clientId = clientId,
                    playerId = playerId,
                    displayName = displayName,
                    team = team,
                    isDummy = false
                };
                return true;
            }
            catch { }
            return false;
        }

        private static bool IsKnownRankedPlayer(string playerKey)
        {
            lock (draftLock)
            {
                if (draftAssignedTeams.ContainsKey(playerKey)) return true;
                if (pendingLateJoiners.ContainsKey(playerKey)) return true;
                if (draftAvailablePlayerIds.Contains(playerKey, StringComparer.OrdinalIgnoreCase)) return true;
            }

            lock (dummyLock)
            {
                if (queuedDraftDummies.ContainsKey(playerKey) || activeDraftDummies.ContainsKey(playerKey)) return true;
            }

            foreach (var participant in rankedParticipants)
            {
                if (participant == null) continue;
                var participantKey = ResolveParticipantIdToKey(participant);
                if (string.Equals(participantKey, playerKey, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static bool IsKnownRankedParticipant(RankedParticipant participant)
        {
            if (participant == null) return false;

            var participantKey = ResolveParticipantIdToKey(participant);
            if (!string.IsNullOrEmpty(participantKey) && IsKnownRankedPlayer(participantKey)) return true;

            if (participant.clientId == 0) return false;

            lock (draftLock)
            {
                if (pendingLateJoiners.Values.Any(p => p != null && p.clientId == participant.clientId)) return true;
            }

            foreach (var existingParticipant in rankedParticipants)
            {
                if (existingParticipant != null && existingParticipant.clientId == participant.clientId) return true;
            }

            return false;
        }

        private static bool TryResolveParticipantFromCommand(string rawTarget, List<RankedParticipant> candidates, out RankedParticipant participant)
        {
            participant = null;
            if (string.IsNullOrWhiteSpace(rawTarget) || candidates == null || candidates.Count == 0) return false;

            var normalizedTarget = NormalizeCommandToken(rawTarget);
            RankedParticipant containsMatch = null;

            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;
                var candidateName = NormalizeCommandToken(candidate.displayName);
                var candidateId = NormalizeCommandToken(candidate.playerId);
                var candidateClient = NormalizeCommandToken(candidate.clientId.ToString());
                if (normalizedTarget == candidateName || normalizedTarget == candidateId || normalizedTarget == candidateClient)
                {
                    participant = candidate;
                    return true;
                }

                if (!string.IsNullOrEmpty(candidateName) && candidateName.Contains(normalizedTarget))
                {
                    if (containsMatch != null) return false;
                    containsMatch = candidate;
                }
            }

            participant = containsMatch;
            return participant != null;
        }

        private static bool TryResolveDraftUiTarget(Dictionary<string, object> dict, out RankedParticipant participant)
        {
            participant = null;
            if (dict == null) return false;

            var clickedPlayer = TryGetPlayerFromDict(dict);
            if (TryBuildConnectedPlayerSnapshot(clickedPlayer, out participant)) return true;

            var clickedSteamId = TryGetSteamIdFromDict(dict);
            if (!string.IsNullOrEmpty(clickedSteamId) && TryGetParticipantByKey(clickedSteamId, out participant)) return true;

            var clickedClientId = TryGetClientIdFromDict(dict);
            if (clickedClientId != 0 && TryGetParticipantByClientId(clickedClientId, out participant)) return true;

            foreach (var value in dict.Values)
            {
                if (TryBuildConnectedPlayerSnapshot(value, out participant)) return true;
            }

            return false;
        }

        private static bool TryGetParticipantByKey(string playerKey, out RankedParticipant participant)
        {
            participant = null;
            if (string.IsNullOrEmpty(playerKey)) return false;

            lock (draftLock)
            {
                if (pendingLateJoiners.TryGetValue(playerKey, out var pending))
                {
                    participant = CloneParticipant(pending);
                    return participant != null;
                }
            }

            lock (dummyLock)
            {
                if (activeDraftDummies.TryGetValue(playerKey, out var activeDummy))
                {
                    participant = CreateParticipantFromDummy(activeDummy);
                    return participant != null;
                }

                if (queuedDraftDummies.TryGetValue(playerKey, out var queuedDummy))
                {
                    participant = CreateParticipantFromDummy(queuedDummy);
                    return participant != null;
                }
            }

            foreach (var candidate in rankedParticipants)
            {
                if (candidate == null) continue;
                var candidateKey = ResolveParticipantIdToKey(candidate);
                if (!string.Equals(candidateKey, playerKey, StringComparison.OrdinalIgnoreCase)) continue;
                participant = CloneParticipant(candidate);
                return participant != null;
            }

            foreach (var player in GetAllPlayers())
            {
                if (!TryBuildConnectedPlayerSnapshot(player, out var snapshot)) continue;
                var snapshotKey = ResolveParticipantIdToKey(snapshot);
                if (!string.Equals(snapshotKey, playerKey, StringComparison.OrdinalIgnoreCase)) continue;
                participant = snapshot;
                return true;
            }

            return false;
        }

        private static bool TryGetParticipantByClientId(ulong clientId, out RankedParticipant participant)
        {
            participant = null;
            if (clientId == 0) return false;

            foreach (var candidate in rankedParticipants)
            {
                if (candidate == null || candidate.clientId != clientId) continue;
                participant = CloneParticipant(candidate);
                return participant != null;
            }

            lock (draftLock)
            {
                var pending = pendingLateJoiners.Values.FirstOrDefault(p => p != null && p.clientId == clientId);
                if (pending != null)
                {
                    participant = CloneParticipant(pending);
                    return true;
                }
            }

            if (TryGetPlayerByClientId(clientId, out var player) && TryBuildConnectedPlayerSnapshot(player, out participant)) return true;
            return false;
        }

        private static bool IsDraftAvailablePlayer(string playerKey)
        {
            if (string.IsNullOrEmpty(playerKey)) return false;
            lock (draftLock)
            {
                return draftAvailablePlayerIds.Any(id => string.Equals(id, playerKey, StringComparison.OrdinalIgnoreCase));
            }
        }

        private static bool IsPendingLateJoiner(string playerKey)
        {
            if (string.IsNullOrEmpty(playerKey)) return false;
            lock (draftLock)
            {
                return pendingLateJoiners.ContainsKey(playerKey);
            }
        }

        private static bool HasPendingLateJoiners()
        {
            lock (draftLock)
            {
                return pendingLateJoiners.Count > 0;
            }
        }

        private static string NormalizeCommandToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var stripped = StripRichTextTags(value).Trim();
            return new string(stripped.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        private static bool TryGetCaptainTeam(string playerKey, out TeamResult team)
        {
            team = TeamResult.Unknown;
            if (string.IsNullOrEmpty(playerKey)) return false;

            lock (draftLock)
            {
                if (string.Equals(playerKey, redCaptainId, StringComparison.OrdinalIgnoreCase))
                {
                    team = TeamResult.Red;
                    return true;
                }

                if (string.Equals(playerKey, blueCaptainId, StringComparison.OrdinalIgnoreCase))
                {
                    team = TeamResult.Blue;
                    return true;
                }
            }

            return false;
        }

        private static string GetCaptainDisplayNameByKey(string playerKey)
        {
            if (string.IsNullOrEmpty(playerKey)) return null;
            return GetParticipantDisplayNameByKey(playerKey);
        }

        private static string GetParticipantDisplayNameByKey(string playerKey)
        {
            if (string.IsNullOrEmpty(playerKey)) return null;

            lock (draftLock)
            {
                if (pendingLateJoiners.TryGetValue(playerKey, out var pendingParticipant)) return pendingParticipant.displayName;
            }
            lock (dummyLock)
            {
                if (queuedDraftDummies.TryGetValue(playerKey, out var queuedDummy)) return queuedDummy.displayName;
                if (activeDraftDummies.TryGetValue(playerKey, out var activeDummy)) return activeDummy.displayName;
            }

            foreach (var participant in rankedParticipants)
            {
                if (participant == null) continue;
                var participantKey = ResolveParticipantIdToKey(participant);
                if (string.Equals(participantKey, playerKey, StringComparison.OrdinalIgnoreCase)) return participant.displayName;
            }

            return null;
        }

        private static void ForceApplyAllDraftTeams()
        {
            List<KeyValuePair<string, TeamResult>> assignments;
            lock (draftLock)
            {
                assignments = draftAssignedTeams.ToList();
            }

            foreach (var assignment in assignments)
            {
                ForceApplyDraftTeam(assignment.Key, assignment.Value);
            }
        }

        private static void ForceApplyDraftTeam(string playerKey, TeamResult team)
        {
            if (string.IsNullOrEmpty(playerKey) || team == TeamResult.Unknown) return;
            if (IsDummyKey(playerKey))
            {
                lock (dummyLock)
                {
                    if (activeDraftDummies.TryGetValue(playerKey, out var activeDummy)) activeDummy.team = team;
                    if (queuedDraftDummies.TryGetValue(playerKey, out var queuedDummy)) queuedDummy.team = team;
                }
                return;
            }
            try
            {
                RegisterInternalTeamAssignment(playerKey, team == TeamResult.Red ? "Red" : "Blue");
                using (BeginForcedTeamAssignment())
                {
                    if (TrySetPlayerTeamBySteamId(playerKey, team == TeamResult.Red ? "Red" : "Blue"))
                    {
                        lock (teamStateLock)
                        {
                            lastKnownPlayerTeam[playerKey] = team == TeamResult.Red ? "Red" : "Blue";
                        }
                    }
                }
            }
            catch { }
        }

        private static void TryMovePlayerToNeutralState(RankedParticipant participant)
        {
            if (participant == null) return;
            var participantKey = ResolveParticipantIdToKey(participant);
            if (string.IsNullOrEmpty(participantKey)) return;
            if (IsDummyParticipant(participant)) return;

            var neutralCandidates = new[] { "Spectator", "None", "Unknown", "Unassigned" };
            foreach (var neutralCandidate in neutralCandidates)
            {
                try
                {
                    RegisterInternalTeamAssignment(participantKey, neutralCandidate);
                    using (BeginForcedTeamAssignment())
                    {
                        if (TrySetPlayerTeamBySteamId(participantKey, neutralCandidate)) return;
                    }
                }
                catch { }
            }
        }

        private static IDisposable BeginForcedTeamAssignment()
        {
            Interlocked.Increment(ref forcedTeamAssignmentDepth);
            return new ForcedTeamAssignmentScope();
        }

        private sealed class ForcedTeamAssignmentScope : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                Interlocked.Decrement(ref forcedTeamAssignmentDepth);
            }
        }

        private static string FormatTeamLabel(TeamResult team)
        {
            return team == TeamResult.Red ? "Red" : team == TeamResult.Blue ? "Blue" : "Unknown";
        }

        private static void SendDraftTeamSummary()
        {
            try
            {
                List<string> redPlayers;
                List<string> bluePlayers;
                lock (draftLock)
                {
                    redPlayers = rankedParticipants
                        .Where(p => p != null)
                        .Where(p =>
                        {
                            var participantKey = ResolveParticipantIdToKey(p);
                            return !string.IsNullOrEmpty(participantKey) && draftAssignedTeams.TryGetValue(participantKey, out var team) && team == TeamResult.Red;
                        })
                        .Select(p => p.displayName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    bluePlayers = rankedParticipants
                        .Where(p => p != null)
                        .Where(p =>
                        {
                            var participantKey = ResolveParticipantIdToKey(p);
                            return !string.IsNullOrEmpty(participantKey) && draftAssignedTeams.TryGetValue(participantKey, out var team) && team == TeamResult.Blue;
                        })
                        .Select(p => p.displayName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }

                SendSystemChatToAll($"<size=13><color=#ff6666>Red</color>: {string.Join(", ", redPlayers)}</size>");
                SendSystemChatToAll($"<size=13><color=#66ccff>Blue</color>: {string.Join(", ", bluePlayers)}</size>");
            }
            catch { }
        }

        public static DraftOverlayState GetDraftOverlayState()
        {
            try
            {
                lock (draftLock)
                {
                    var availablePlayers = rankedParticipants
                        .Where(p => p != null)
                        .Where(p =>
                        {
                            var participantKey = ResolveParticipantIdToKey(p);
                            return !string.IsNullOrEmpty(participantKey) && draftAvailablePlayerIds.Contains(participantKey, StringComparer.OrdinalIgnoreCase);
                        })
                        .Select(p => p.displayName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    var redPlayers = rankedParticipants
                        .Where(p => p != null)
                        .Where(p =>
                        {
                            var participantKey = ResolveParticipantIdToKey(p);
                            return !string.IsNullOrEmpty(participantKey) && draftAssignedTeams.TryGetValue(participantKey, out var assignedTeam) && assignedTeam == TeamResult.Red;
                        })
                        .Select(p => p.displayName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    var bluePlayers = rankedParticipants
                        .Where(p => p != null)
                        .Where(p =>
                        {
                            var participantKey = ResolveParticipantIdToKey(p);
                            return !string.IsNullOrEmpty(participantKey) && draftAssignedTeams.TryGetValue(participantKey, out var assignedTeam) && assignedTeam == TeamResult.Blue;
                        })
                        .Select(p => p.displayName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    var isCompleted = rankedActive && !draftActive && (redPlayers.Length > 0 || bluePlayers.Length > 0);
                    var isVisible = draftActive;
                    if (!isVisible)
                    {
                        return new DraftOverlayState { IsVisible = false };
                    }

                    ulong currentTurnClientId = 0;
                    string currentTurnSteamId = null;
                    if (draftActive
                        && !string.IsNullOrWhiteSpace(currentCaptainTurnId)
                        && TryGetParticipantByKey(currentCaptainTurnId, out var currentTurnParticipant)
                        && currentTurnParticipant != null)
                    {
                        var currentTurnEntry = BuildDraftOverlayEntry(currentTurnParticipant, TeamResult.Unknown, currentCaptainTurnId);
                        if (currentTurnEntry != null)
                        {
                            currentTurnClientId = currentTurnEntry.ClientId;
                            currentTurnSteamId = currentTurnEntry.SteamId;
                        }
                    }

                    return new DraftOverlayState
                    {
                        IsVisible = true,
                        IsCompleted = isCompleted,
                        Title = "RANKED MATCH SETUP",
                        RedCaptainName = GetCaptainDisplayNameByKey(redCaptainId) ?? "Pending",
                        BlueCaptainName = GetCaptainDisplayNameByKey(blueCaptainId) ?? "Pending",
                        CurrentTurnName = draftActive ? (GetCaptainDisplayNameByKey(currentCaptainTurnId) ?? "Pending") : string.Empty,
                        CurrentTurnClientId = currentTurnClientId,
                        CurrentTurnSteamId = currentTurnSteamId,
                        AvailablePlayers = availablePlayers,
                        RedPlayers = redPlayers,
                        BluePlayers = bluePlayers,
                        PendingLateJoinerCount = pendingLateJoiners.Count,
                        DummyModeActive = rankedParticipants.Any(IsDummyParticipant),
                        FooterText = "Select a player to add them to your team."
                    };
                }
            }
            catch { }

            return new DraftOverlayState { IsVisible = false };
        }

        private static void PublishDraftOverlayState()
        {
            try
            {
                schrader.DraftStateBridge.PublishState(GetDraftOverlayState());
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] PublishDraftOverlayState failed: {ex.Message}");
            }
        }

        #endif

        private static string BuildDraftOverlayFallbackMessage(bool completed, bool includeAvailablePlayers)
        {
            try
            {
                lock (draftLock)
                {
                    var redCaptainName = GetCaptainDisplayNameByKey(redCaptainId) ?? "Pending";
                    var blueCaptainName = GetCaptainDisplayNameByKey(blueCaptainId) ?? "Pending";
                    var turnName = GetCaptainDisplayNameByKey(currentCaptainTurnId) ?? "Pending";
                    var pendingCount = pendingLateJoiners.Count;
                    var availablePlayers = rankedParticipants
                        .Where(p => p != null)
                        .Where(p =>
                        {
                            var participantKey = ResolveParticipantIdToKey(p);
                            return !string.IsNullOrEmpty(participantKey) && draftAvailablePlayerIds.Contains(participantKey, StringComparer.OrdinalIgnoreCase);
                        })
                        .Select(p => p.displayName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var availableSummary = availablePlayers.Count == 0
                        ? "None"
                        : string.Join(", ", availablePlayers.Take(6));
                    if (availablePlayers.Count > 6)
                    {
                        availableSummary += $" <color=#cccccc>(+{availablePlayers.Count - 6} more)</color>";
                    }

                    var statusLine = completed
                        ? "<color=#55dd88>Status:</color> Draft Complete"
                        : $"<color=#55dd88>Status:</color> Draft Active | <color=#ffef9a>Turn:</color> {turnName}";

                    var availableLine = includeAvailablePlayers
                        ? $"<color=#ffef9a>Available:</color> {availableSummary}"
                        : $"<color=#ffef9a>Available Count:</color> {draftAvailablePlayerIds.Count}";

                    var pendingLine = $"<color=#ffef9a>Late Joiners:</color> {pendingCount}";
                    var guidanceLine = completed
                        ? "<size=12><color=#dddddd>Match is ready to start.</color></size>"
                        : "<size=12><color=#dddddd>Use /pick player. Scoreboard click remains available where supported.</color></size>";

                    return
                        "<size=15><b><color=#f2f2f2>RANKED DRAFT</color></b></size>\n"
                        + "<size=13><color=#bbbbbb>------------------------------</color></size>\n"
                        + $"<size=13><color=#ff6666>RED Captain:</color> <b>{redCaptainName}</b></size>\n"
                        + $"<size=13><color=#66b3ff>BLUE Captain:</color> <b>{blueCaptainName}</b></size>\n"
                        + $"<size=13>{statusLine}</size>\n"
                        + $"<size=13>{availableLine}</size>\n"
                        + $"<size=13>{pendingLine}</size>\n"
                        + guidanceLine;
                }
            }
            catch { }

            return completed
                ? "<size=14><color=#00ff00>Draft complete.</color></size>"
                : "<size=14><color=#ffcc66>Draft</color> active.</size>";
        }

            #if false
        public static int GetMmr(string playerId)
        {
            lock (mmrLock)
            {
                if (mmrFile.players.TryGetValue(playerId, out var entry)) return entry.mmr;
                // try resolving to steamid key if playerId is a clientId fallback
                var resolved = ResolveStoredIdToSteam(playerId);
                if (!string.IsNullOrEmpty(resolved) && mmrFile.players.TryGetValue(resolved, out entry)) return entry.mmr;
            }
            return 350;
        }

        #endif

        private static object FindFirstObjectOfType(Type t)
        {
            try { var objs = Resources.FindObjectsOfTypeAll(t); if (objs != null && objs.Length > 0) return objs[0]; } catch { }
            return null;
        }

        private static object GetManagerInstance(Type managerType)
        {
            if (managerType == null) return null;
            try
            {
                var prop = managerType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop != null) { var val = prop.GetValue(null); if (val != null) return val; }
                return FindFirstObjectOfType(managerType);
            }
            catch { }
            return null;
        }

        private static Type FindTypeByName(params string[] names)
        {
            if (names == null || names.Length == 0) return null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var n in names)
                {
                    var t = asm.GetType(n);
                    if (t != null) return t;
                }
            }
            return null;
        }

        private static string TryGetSteamIdFromDict(Dictionary<string, object> dict)
        {
            if (dict == null) return null;
            string[] keys = { "steamId", "steamid", "steamID", "playerId", "playerID", "playerSteamId", "playerSteamID" };
            foreach (var k in keys)
            {
                if (!dict.TryGetValue(k, out var val) || val == null) continue;
                try { return val.ToString(); } catch { }
            }
            return null;
        }

        private static object TryGetPlayerFromDict(Dictionary<string, object> dict)
        {
            if (dict == null) return null;
            string[] keys = { "player", "Player", "playerObj", "playerObject" };
            foreach (var k in keys)
            {
                if (!dict.TryGetValue(k, out var val) || val == null) continue;
                return val;
            }
            return null;
        }

        private static ulong TryGetClientIdFromDict(Dictionary<string, object> dict)
        {
            if (dict == null) return 0;
            string[] keys = { "clientId", "clientID", "client", "client_id" };
            foreach (var k in keys)
            {
                if (!dict.TryGetValue(k, out var val) || val == null) continue;
                try
                {
                    if (val is ulong ul) return ul;
                    if (val is long l && l >= 0) return (ulong)l;
                    if (val is int i && i >= 0) return (ulong)i;
                    if (ulong.TryParse(val.ToString(), out var parsed)) return parsed;
                }
                catch { }
            }
            return 0;
        }

        private static object TryGetTeamFromDict(Dictionary<string, object> dict)
        {
            if (dict == null) return null;
            string[] keys = { "team", "Team", "newTeam", "NewTeam", "playerTeam", "PlayerTeam" };
            foreach (var k in keys)
            {
                if (!dict.TryGetValue(k, out var val) || val == null) continue;
                return val;
            }
            return null;
        }

        private static object TryGetOldTeamFromDict(Dictionary<string, object> dict)
        {
            if (dict == null) return null;
            string[] keys = { "oldTeam", "OldTeam", "previousTeam", "PreviousTeam" };
            foreach (var k in keys)
            {
                if (!dict.TryGetValue(k, out var val) || val == null) continue;
                return val;
            }
            return null;
        }

        private static bool AreTeamValuesEqual(object a, object b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            try
            {
                if (a.Equals(b)) return true;
            }
            catch { }
            try
            {
                var sa = a.ToString();
                var sb = b.ToString();
                if (!string.IsNullOrEmpty(sa) && !string.IsNullOrEmpty(sb))
                {
                    if (string.Equals(sa, sb, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            catch { }
            try
            {
                // Compare enum underlying values if possible
                var ia = Convert.ToInt32(a);
                var ib = Convert.ToInt32(b);
                return ia == ib;
            }
            catch { }
            return false;
        }

        private static bool IsTeamNoneLike(object teamValue)
        {
            if (teamValue == null) return true;
            try
            {
                var s = teamValue.ToString();
                if (string.IsNullOrWhiteSpace(s)) return true;
                var n = s.ToLowerInvariant();
                if (n == "none" || n == "unknown" || n == "unassigned" || n == "null") return true;
                if (n.Contains("none") && n.Length <= 12) return true;
                if (n.Contains("unknown") && n.Length <= 12) return true;
            }
            catch { }
            try
            {
                // Many enums use 0 as None/Unknown
                var i = Convert.ToInt32(teamValue);
                if (i == 0) return true;
            }
            catch { }
            return false;
        }

        private static object TryGetPlayerFromController(object controller)
        {
            if (controller == null) return null;
            try
            {
                var t = controller.GetType();
                var prop = t.GetProperty("Player", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                           ?? t.GetProperty("player", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (prop != null)
                {
                    var val = prop.GetValue(controller);
                    if (val != null) return val;
                }

                var field = t.GetField("Player", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            ?? t.GetField("player", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    var val = field.GetValue(controller);
                    if (val != null) return val;
                }
            }
            catch { }
            return null;
        }

        private static object GetCurrentTeamValue(object player)
        {
            if (player == null) return null;
            try
            {
                var t = player.GetType();
                var getTeam = t.GetMethod("get_Team", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (getTeam != null) return getTeam.Invoke(player, null);
                var prop = t.GetProperty("Team", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (prop != null) return prop.GetValue(player);
            }
            catch { }
            return null;
        }

        private static void TryWarnTeamSwitchBlocked(object player)
        {
            try
            {
                if (player == null) return;
                TryGetClientId(player, out var cid);
                Debug.Log($"[{Constants.MOD_NAME}] Suppressed blocked team-switch warning for client {cid}.");
            }
            catch { }
        }

        private static void TryWarnTeamSwitchBlockedBySteamId(string steamId)
        {
            try
            {
                if (string.IsNullOrEmpty(steamId)) return;
                var players = GetAllPlayers();
                foreach (var p in players)
                {
                    try
                    {
                        var pid = TryGetPlayerId(p, 0UL);
                        if (!string.Equals(pid, steamId, StringComparison.Ordinal)) continue;
                        TryWarnTeamSwitchBlocked(p);
                        return;
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static string FormatTeamValue(object teamValue)
        {
            if (teamValue == null) return "null";
            try { return teamValue.ToString(); } catch { return "?"; }
        }

        private static void RegisterInternalTeamAssignment(string playerKey, object expectedTeam)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerKey) || expectedTeam == null) return;
                var expectedTeamValue = FormatTeamValue(expectedTeam);
                if (string.IsNullOrWhiteSpace(expectedTeamValue)) return;

                var keys = GetInternalTeamAssignmentKeys(playerKey, 0UL);
                if (keys.Count == 0) return;

                lock (internalTeamAssignmentLock)
                {
                    CleanupExpiredInternalTeamAssignmentsLocked();
                    var expiresAt = Time.unscaledTime + InternalTeamAssignmentGracePeriod;
                    foreach (var key in keys)
                    {
                        internalTeamAssignments[key] = new InternalTeamAssignment
                        {
                            expectedTeam = expectedTeamValue,
                            expiresAt = expiresAt
                        };
                    }
                }
            }
            catch { }
        }

        private static bool IsInternalTeamAssignmentAllowed(object player, ulong fallbackClientId, object requestedTeam, bool consume)
        {
            try
            {
                var playerKey = ResolvePlayerObjectKey(player, fallbackClientId);
                ulong clientId = 0;
                if (player != null) TryGetClientId(player, out clientId);
                if (clientId == 0) clientId = fallbackClientId;
                return IsInternalTeamAssignmentAllowed(playerKey, clientId, requestedTeam, consume);
            }
            catch { }
            return false;
        }

        private static bool IsInternalTeamAssignmentAllowed(string playerKey, ulong clientId, object requestedTeam, bool consume)
        {
            if (requestedTeam == null) return false;

            try
            {
                var keys = GetInternalTeamAssignmentKeys(playerKey, clientId);
                if (keys.Count == 0) return false;

                lock (internalTeamAssignmentLock)
                {
                    CleanupExpiredInternalTeamAssignmentsLocked();
                    foreach (var key in keys)
                    {
                        if (!internalTeamAssignments.TryGetValue(key, out var assignment) || assignment == null) continue;
                        if (!AreTeamValuesEqual(assignment.expectedTeam, requestedTeam)) continue;

                        if (consume)
                        {
                            foreach (var removeKey in keys)
                            {
                                internalTeamAssignments.Remove(removeKey);
                            }
                        }

                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static HashSet<string> GetInternalTeamAssignmentKeys(string playerKey, ulong clientId)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!string.IsNullOrWhiteSpace(playerKey))
                {
                    keys.Add(playerKey);
                    var resolved = ResolveStoredIdToSteam(playerKey);
                    if (!string.IsNullOrWhiteSpace(resolved)) keys.Add(resolved);
                }

                if (clientId != 0)
                {
                    keys.Add($"clientId:{clientId}");
                }

                if (!string.IsNullOrWhiteSpace(playerKey) && TryGetParticipantByKey(playerKey, out var participant) && participant != null && participant.clientId != 0)
                {
                    keys.Add($"clientId:{participant.clientId}");
                    var resolvedParticipantKey = ResolveParticipantIdToKey(participant);
                    if (!string.IsNullOrWhiteSpace(resolvedParticipantKey)) keys.Add(resolvedParticipantKey);
                }

                if (clientId != 0 && TryGetParticipantByClientId(clientId, out var clientParticipant) && clientParticipant != null)
                {
                    var resolvedParticipantKey = ResolveParticipantIdToKey(clientParticipant);
                    if (!string.IsNullOrWhiteSpace(resolvedParticipantKey)) keys.Add(resolvedParticipantKey);
                }
            }
            catch { }
            return keys;
        }

        private static void CleanupExpiredInternalTeamAssignmentsLocked()
        {
            var now = Time.unscaledTime;
            var expiredKeys = internalTeamAssignments
                .Where(entry => entry.Value == null || entry.Value.expiresAt <= now)
                .Select(entry => entry.Key)
                .ToList();
            foreach (var expiredKey in expiredKeys)
            {
                internalTeamAssignments.Remove(expiredKey);
            }
        }

        private static bool TrySetPlayerTeamBySteamId(string steamId, object teamValue)
        {
            try
            {
                if (string.IsNullOrEmpty(steamId)) return false;
                var players = GetAllPlayers();
                foreach (var p in players)
                {
                    try
                    {
                        var pid = TryGetPlayerId(p, 0UL);
                        if (!string.Equals(pid, steamId, StringComparison.Ordinal)) continue;
                        return TrySetPlayerTeamOnPlayer(p, teamValue);
                    }
                    catch { }
                }
            }
            catch { }
            return false;
        }

        private static bool TrySetPlayerTeamOnPlayer(object player, object teamValue)
        {
            if (player == null || teamValue == null) return false;
            try
            {
                var t = player.GetType();

                string[] methodNames = { "Server_SetPlayerTeam", "SetPlayerTeam", "ChangeTeam", "SetTeam", "Client_SetPlayerTeamRpc" };
                foreach (var mn in methodNames)
                {
                    var m = t.GetMethod(mn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (m == null) continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 1)
                    {
                        var arg = ConvertTeamValueForType(teamValue, ps[0].ParameterType);
                        if (arg != null)
                        {
                            m.Invoke(player, new object[] { arg });
                            return true;
                        }
                    }
                }

                var prop = t.GetProperty("Team", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                           ?? t.GetProperty("team", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                           ?? t.GetProperty("PlayerTeam", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (prop != null && prop.CanWrite)
                {
                    var arg = ConvertTeamValueForType(teamValue, prop.PropertyType);
                    if (arg != null)
                    {
                        prop.SetValue(player, arg);
                        return true;
                    }
                }

                var field = t.GetField("Team", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            ?? t.GetField("team", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            ?? t.GetField("PlayerTeam", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    var arg = ConvertTeamValueForType(teamValue, field.FieldType);
                    if (arg != null)
                    {
                        field.SetValue(player, arg);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static object ConvertTeamValueForType(object teamValue, Type targetType)
        {
            if (teamValue == null || targetType == null) return null;
            try
            {
                if (targetType.IsInstanceOfType(teamValue)) return teamValue;

                if (targetType.IsEnum)
                {
                    if (teamValue is string s)
                    {
                        try { return Enum.Parse(targetType, s, true); } catch { }
                    }
                    if (teamValue is int i) return Enum.ToObject(targetType, i);
                    if (teamValue is byte b) return Enum.ToObject(targetType, b);
                    if (teamValue is long l) return Enum.ToObject(targetType, (int)l);
                    if (int.TryParse(teamValue.ToString(), out var parsed)) return Enum.ToObject(targetType, parsed);
                }

                return Convert.ChangeType(teamValue, targetType);
            }
            catch { }
            return null;
        }

        // Resolve a stored id (possibly clientId:123) to a SteamID if the player is connected
        private static string ResolveStoredIdToSteam(string storedId)
        {
            try
            {
                if (string.IsNullOrEmpty(storedId)) return storedId;
                if (!storedId.StartsWith("clientId:")) return storedId;
                if (!ulong.TryParse(storedId.Substring("clientId:".Length), out var cid)) return storedId;
                var players = GetAllPlayers();
                foreach (var p in players)
                {
                    try
                    {
                        if (TryGetClientId(p, out var pcid) && pcid == cid)
                        {
                            var sid = TryGetPlayerId(p, cid);
                            if (!string.IsNullOrEmpty(sid)) return sid;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return storedId;
        }

        private static string GetPlayerKey(object player, ulong clientId = 0)
        {
            try
            {
                var resolvedKey = ResolvePlayerObjectKey(player, clientId);
                if (!string.IsNullOrWhiteSpace(resolvedKey))
                {
                    return ResolveStoredIdToSteam(resolvedKey);
                }

                if (clientId != 0)
                {
                    return $"clientId:{clientId}";
                }
            }
            catch { }

            return null;
        }

        private static string GetPlayerKey(RankedParticipant participant)
        {
            try
            {
                if (participant == null)
                {
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(participant.playerId) && !participant.playerId.StartsWith("clientId:", StringComparison.OrdinalIgnoreCase))
                {
                    return participant.playerId;
                }

                if (participant.clientId != 0)
                {
                    foreach (var player in GetAllPlayers())
                    {
                        try
                        {
                            if (TryGetClientId(player, out var liveClientId) && liveClientId == participant.clientId)
                            {
                                return GetPlayerKey(player, liveClientId) ?? participant.playerId;
                            }
                        }
                        catch { }
                    }
                }

                if (!string.IsNullOrWhiteSpace(participant.playerId))
                {
                    return ResolveStoredIdToSteam(participant.playerId);
                }

                return participant.clientId != 0 ? $"clientId:{participant.clientId}" : null;
            }
            catch
            {
                return participant?.playerId;
            }
        }

        // For a RankedParticipant, prefer SteamID key when available
        private static string ResolveParticipantIdToKey(RankedParticipant p)
        {
            return GetPlayerKey(p);
        }

        public static bool TryGetEligiblePlayers(out List<RankedParticipant> eligible, out string reason)
        {
            eligible = new List<RankedParticipant>(); reason = "not enough players";

            RankedParticipant sharedGoalieCandidate = null;
            if (singleGoalieEnabled && !TryGetSingleGoalieCandidate(out sharedGoalieCandidate, out var sharedGoalieReason))
            {
                if (!ShouldPreserveSingleGoalieDuringTrackedPhase(GetTrackedPhaseName()))
                {
                    DisableSingleGoalie(sharedGoalieReason ?? "shared goalie disabled because the goalie setup changed.");
                }
            }

            var unique = new Dictionary<ulong, RankedParticipant>();
            var players = GetAllPlayers();
            if (players.Count == 0)
            {
                reason = "no players found";
                return false;
            }

            foreach (var p in players)
            {
                if (!TryBuildRankedParticipant(p, out var participant, out var buildReason))
                {
                    if (string.Equals(buildReason, "goalies cannot play ranked", StringComparison.OrdinalIgnoreCase))
                    {
                        if (sharedGoalieCandidate != null
                            && TryBuildConnectedPlayerSnapshot(p, out var goalieSnapshot)
                            && IsSameParticipantIdentity(sharedGoalieCandidate, goalieSnapshot))
                        {
                            continue;
                        }

                        reason = buildReason;
                        return false;
                    }
                    continue;
                }

                unique[participant.clientId] = participant;
            }

            // Fill gaps using PlayerManager team queries when player objects do not expose team reliably.
            TryAddEligiblePlayersFromManager(unique, TeamResult.Red, sharedGoalieCandidate, ref reason);
            if (string.Equals(reason, "goalies cannot play ranked", StringComparison.OrdinalIgnoreCase)) return false;
            TryAddEligiblePlayersFromManager(unique, TeamResult.Blue, sharedGoalieCandidate, ref reason);
            if (string.Equals(reason, "goalies cannot play ranked", StringComparison.OrdinalIgnoreCase)) return false;

            eligible = unique.Values.ToList();
            var redCount = eligible.Count(p => p.team == TeamResult.Red);
            var blueCount = eligible.Count(p => p.team == TeamResult.Blue);

            if (redCount < 1 || blueCount < 1) { reason = "need at least 1 red and 1 blue (non-goalie)"; return false; }
            if (eligible.Count < 2) { reason = "need at least 2 non-goalie players"; return false; }
            reason = null; return true;
        }

        private static bool TryBuildRankedParticipant(object player, out RankedParticipant participant, out string reason)
        {
            participant = null;
            reason = null;

            try
            {
                if (player == null)
                {
                    reason = "player is null";
                    return false;
                }

                if (!TryGetClientId(player, out var clientId) || clientId == 0)
                {
                    reason = "missing clientId";
                    return false;
                }

                var isBotPlayer = BotManager.TryGetBotIdByClientId(clientId, out var botKey);
                if (isBotPlayer)
                {
                    reason = "bots cannot play ranked";
                    return false;
                }

                if (IsInvalidWarmupDummyCandidate(player, clientId, out _))
                {
                    reason = "invalid warmup dummy without identity";
                    return false;
                }

                if (TryIsGoalie(player, out var isGoalie) && isGoalie)
                {
                    reason = "goalies cannot play ranked";
                    return false;
                }

                TeamResult team = TeamResult.Unknown;
                if (!TryGetPlayerTeam(player, out team) || team == TeamResult.Unknown)
                {
                    if (!TryGetPlayerTeamFromManager(clientId, out team) || team == TeamResult.Unknown)
                    {
                        var playerName = TryGetPlayerName(player);
                        TryGetTeamFromScoreboard(clientId, playerName, out team);
                    }
                }

                if (team == TeamResult.Unknown)
                {
                    reason = "team unknown";
                    return false;
                }

                var playerId = isBotPlayer
                    ? botKey
                    : (ResolvePlayerObjectKey(player, clientId) ?? TryGetPlayerId(player, clientId));
                var displayName = isBotPlayer
                    ? (BotManager.GetBotDisplayName(botKey) ?? TryGetPlayerName(player) ?? $"Bot {clientId}")
                    : (TryGetPlayerName(player) ?? $"Player {clientId}");

                participant = new RankedParticipant
                {
                    clientId = clientId,
                    playerId = playerId,
                    displayName = displayName,
                    team = team,
                    isDummy = isBotPlayer
                };
                return true;
            }
            catch { }

            reason = "unknown";
            return false;
        }

        private static void TryAddEligiblePlayersFromManager(Dictionary<ulong, RankedParticipant> unique, TeamResult team, RankedParticipant sharedGoalieCandidate, ref string reason)
        {
            try
            {
                if (!TryGetPlayerManager(out var manager)) return;
                var managerType = manager.GetType();
                var enumType = FindTypeByName("PlayerTeam", "Puck.PlayerTeam");
                if (enumType == null) return;

                var enumValue = Enum.Parse(enumType, team == TeamResult.Red ? "Red" : "Blue", true);
                var getPlayersByTeam = managerType.GetMethod("GetPlayersByTeam", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var getSpawnedPlayersByTeam = managerType.GetMethod("GetSpawnedPlayersByTeam", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                System.Collections.IEnumerable list = null;
                if (getPlayersByTeam != null)
                {
                    try { list = getPlayersByTeam.Invoke(manager, new object[] { enumValue, true }) as System.Collections.IEnumerable; } catch { }
                }
                if (list == null && getSpawnedPlayersByTeam != null)
                {
                    try { list = getSpawnedPlayersByTeam.Invoke(manager, new object[] { enumValue, true }) as System.Collections.IEnumerable; } catch { }
                }
                if (list == null) return;

                foreach (var player in list)
                {
                    if (!TryBuildRankedParticipant(player, out var participant, out var buildReason))
                    {
                        if (string.Equals(buildReason, "goalies cannot play ranked", StringComparison.OrdinalIgnoreCase))
                        {
                            if (sharedGoalieCandidate != null
                                && TryBuildConnectedPlayerSnapshot(player, out var goalieSnapshot)
                                && IsSameParticipantIdentity(sharedGoalieCandidate, goalieSnapshot))
                            {
                                continue;
                            }

                            reason = buildReason;
                            return;
                        }
                        continue;
                    }

                    participant.team = team;
                    unique[participant.clientId] = participant;
                }
            }
            catch { }
        }

        public static string TryGetPlayerName(object player)
        {
            if (player == null) return null;
            try
            {
                try
                {
                    var chat = UIChat.Instance;
                    if (chat != null)
                    {
                        var wrap = chat.GetType().GetMethod("WrapPlayerUsername", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (wrap != null)
                        {
                            var wrapped = wrap.Invoke(chat, new object[] { player }) as string;
                            var clean = NormalizeVisiblePlayerName(wrapped);
                            if (!string.IsNullOrWhiteSpace(clean)) return clean;
                        }
                    }
                }
                catch { }

                var t = player.GetType(); string[] propNames = { "Name", "PlayerName", "playerName", "steamName", "DisplayName", "Username", "username", "UserName", "Nickname", "NickName" };
                foreach (var pn in propNames)
                {
                    var prop = t.GetProperty(pn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (prop == null) continue;
                    var clean = NormalizeVisiblePlayerName(ExtractSimpleValueToString(prop.GetValue(player)));
                    if (!string.IsNullOrWhiteSpace(clean)) return clean;
                }

                foreach (var fn in propNames)
                {
                    var field = t.GetField(fn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (field == null) continue;
                    var clean = NormalizeVisiblePlayerName(ExtractSimpleValueToString(field.GetValue(player)));
                    if (!string.IsNullOrWhiteSpace(clean)) return clean;
                }

                if (player is Component comp)
                {
                    var clean = NormalizeVisiblePlayerName(comp.name);
                    if (!string.IsNullOrWhiteSpace(clean)) return clean;
                }
            }
            catch { }
            return null;
        }

        private static string TryGetPlainPlayerName(object player)
        {
            if (player == null)
            {
                return null;
            }

            try
            {
                var t = player.GetType();
                string[] memberNames = { "Username", "username", "UserName", "DisplayName", "displayName", "Name", "PlayerName", "playerName", "Nickname", "NickName", "steamName" };
                foreach (var memberName in memberNames)
                {
                    var property = t.GetProperty(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (property != null)
                    {
                        var clean = NormalizeVisiblePlayerName(ExtractSimpleValueToString(property.GetValue(player)));
                        if (!string.IsNullOrWhiteSpace(clean)) return clean;
                    }

                    var field = t.GetField(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (field != null)
                    {
                        var clean = NormalizeVisiblePlayerName(ExtractSimpleValueToString(field.GetValue(player)));
                        if (!string.IsNullOrWhiteSpace(clean)) return clean;
                    }
                }
            }
            catch { }

            return null;
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

        private static string NormalizeVisiblePlayerName(string candidate)
        {
            var clean = StripRichTextTags(candidate)?.Trim();
            if (string.IsNullOrWhiteSpace(clean))
            {
                return null;
            }

            return LooksLikeIdentityKey(clean) ? null : clean;
        }

        public static bool TryForcePlayerTeamByNumber(int playerNumber, string requestedTeam, out string resultMessage)
        {
            return TryForcePlayerTeamByTarget(playerNumber.ToString(), requestedTeam, out resultMessage);
        }

        public static bool TryForcePlayerTeamByTarget(string playerTarget, string requestedTeam, out string resultMessage)
        {
            resultMessage = null;

            var cleanTarget = StripRichTextTags(playerTarget)?.Trim();
            if (string.IsNullOrWhiteSpace(cleanTarget))
            {
                resultMessage = "Usage: /fc <player|steamId|#number> <red|blue|spectator>.";
                return false;
            }

            var normalizedTeam = requestedTeam?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(normalizedTeam))
            {
                resultMessage = "Usage: /fc <player|steamId|#number> <red|blue|spectator>.";
                return false;
            }

            if (!TryResolveConnectedPlayerByTarget(cleanTarget, out var player, out var clientId, out var playerKey, out var playerName, out var resolveError))
            {
                resultMessage = resolveError ?? "Could not resolve that player. Use an exact name, SteamID, or #playerNumber.";
                return false;
            }

            var displayName = playerName ?? cleanTarget;
            if (normalizedTeam == "red" || normalizedTeam == "r")
            {
                if (!TryApplyOfficialTeamJoin(playerKey, clientId, TeamResult.Red, openPositionSelection: false))
                {
                    resultMessage = $"Failed to move {displayName} to Red.";
                    return false;
                }

                UpdateTrackedPlayerTeam(playerKey, TeamResult.Red);
                resultMessage = $"moved <b>{displayName}</b> to <b>Red</b>.";
                return true;
            }

            if (normalizedTeam == "blue" || normalizedTeam == "b")
            {
                if (!TryApplyOfficialTeamJoin(playerKey, clientId, TeamResult.Blue, openPositionSelection: false))
                {
                    resultMessage = $"Failed to move {displayName} to Blue.";
                    return false;
                }

                UpdateTrackedPlayerTeam(playerKey, TeamResult.Blue);
                resultMessage = $"moved <b>{displayName}</b> to <b>Blue</b>.";
                return true;
            }

            if (normalizedTeam == "spectator" || normalizedTeam == "spec" || normalizedTeam == "s")
            {
                if (!TryForcePlayerToNeutralState(player, clientId, playerKey))
                {
                    resultMessage = $"Failed to move {displayName} to spectator.";
                    return false;
                }

                UpdateTrackedPlayerTeam(playerKey, TeamResult.Unknown);
                resultMessage = $"moved <b>{displayName}</b> to <b>Spectator</b>.";
                return true;
            }

            resultMessage = "Usage: /fc <player|steamId|#number> <red|blue|spectator>.";
            return false;
        }

        private static bool TryResolveConnectedPlayerByTarget(string rawTarget, out object player, out ulong clientId, out string playerKey, out string playerName, out string error)
        {
            player = null;
            clientId = 0;
            playerKey = null;
            playerName = null;
            error = null;

            var cleanTarget = StripRichTextTags(rawTarget)?.Trim();
            if (string.IsNullOrWhiteSpace(cleanTarget))
            {
                error = "Usage: /fc <player|steamId|#number> <red|blue|spectator>.";
                return false;
            }

            var isExplicitNumberTarget = cleanTarget.StartsWith("#", StringComparison.Ordinal);
            var numericTarget = isExplicitNumberTarget ? cleanTarget.Substring(1).Trim() : cleanTarget;
            if (int.TryParse(numericTarget, out var playerNumber) && playerNumber >= 0)
            {
                if (TryResolveConnectedPlayerByNumber(playerNumber, out player, out clientId, out playerKey, out playerName))
                {
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
                var steamId = explicitSteamId.ToString();
                if (!TryFindConnectedPlayerBySteamId(steamId, out player, out playerName))
                {
                    error = $"Could not find a live player with SteamID {steamId}.";
                    return false;
                }

                TryGetClientId(player, out clientId);
                playerKey = GetPlayerKey(player, clientId) ?? NormalizeResolvedPlayerKey(steamId);
                playerName = playerName ?? TryGetPlainPlayerName(player) ?? steamId;
                return true;
            }

            var exactMatches = new List<(object Player, ulong ClientId, string PlayerKey, string PlayerName)>();
            var prefixMatches = new List<(object Player, ulong ClientId, string PlayerKey, string PlayerName)>();
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
                    var candidateKey = NormalizeResolvedPlayerKey(TryGetPlayerIdNoFallback(candidate));
                    if (IsReplayPlayerObject(candidate, candidateClientId)
                        || IsPracticeModeFakePlayerObject(candidate, candidateClientId, candidateKey))
                    {
                        continue;
                    }

                    var candidateName = TryGetPlainPlayerName(candidate) ?? TryGetPlayerName(candidate);
                    if (string.IsNullOrWhiteSpace(candidateName))
                    {
                        continue;
                    }

                    var resolvedPlayerKey = GetPlayerKey(candidate, candidateClientId) ?? candidateKey ?? TryGetPlayerId(candidate, candidateClientId);
                    var match = (candidate, candidateClientId, resolvedPlayerKey, candidateName);
                    if (string.Equals(candidateName, cleanTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        exactMatches.Add(match);
                        continue;
                    }

                    if (candidateName.StartsWith(cleanTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        prefixMatches.Add(match);
                    }
                }
            }
            catch { }

            if (exactMatches.Count == 1)
            {
                player = exactMatches[0].Player;
                clientId = exactMatches[0].ClientId;
                playerKey = exactMatches[0].PlayerKey;
                playerName = exactMatches[0].PlayerName;
                return true;
            }

            if (exactMatches.Count > 1)
            {
                error = "That player name is ambiguous. Use the SteamID or #playerNumber instead.";
                return false;
            }

            if (prefixMatches.Count == 1)
            {
                player = prefixMatches[0].Player;
                clientId = prefixMatches[0].ClientId;
                playerKey = prefixMatches[0].PlayerKey;
                playerName = prefixMatches[0].PlayerName;
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

        public static bool TryAddScore(string requestedTeam, int amount, out int redScore, out int blueScore, out string error)
        {
            redScore = 0;
            blueScore = 0;
            error = null;

            if (amount == 0)
            {
                error = "Score adjustment amount must not be 0.";
                return false;
            }

            var normalizedTeam = requestedTeam?.Trim().ToLowerInvariant();
            TeamResult team;
            if (normalizedTeam == "red" || normalizedTeam == "r")
            {
                team = TeamResult.Red;
            }
            else if (normalizedTeam == "blue" || normalizedTeam == "b")
            {
                team = TeamResult.Blue;
            }
            else
            {
                error = "Usage: /addscore <amount> <red|blue>.";
                return false;
            }

            if (!TryGetRuntimeGameManager(out var gameManager, out var gameManagerType))
            {
                error = "Could not find the live GameManager.";
                return false;
            }

            if (!TryGetScoresFromGameState(gameManager, out redScore, out blueScore))
            {
                error = "Could not read the current in-game score.";
                return false;
            }

            var nextRedScore = redScore + (team == TeamResult.Red ? amount : 0);
            var nextBlueScore = blueScore + (team == TeamResult.Blue ? amount : 0);
            if (nextRedScore < 0 || nextBlueScore < 0)
            {
                error = "Score adjustment cannot make a team score negative.";
                return false;
            }

            if (!TryPreserveRemainingPlayTime(gameManager))
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] Failed to preserve remaining play time before manual score adjustment.");
            }

            if (!TryInvokeServerUpdateGameState(gameManagerType, gameManager, nextBlueScore, nextRedScore))
            {
                error = "Failed to update the live game state score.";
                return false;
            }

            if (!TryInvokeServerSetPhase(gameManagerType, gameManager, team == TeamResult.Red ? "RedScore" : "BlueScore"))
            {
                error = "Updated the score, but failed to enter the native score phase.";
                return false;
            }

            lock (goalLock)
            {
                currentRedGoals = nextRedScore;
                currentBlueGoals = nextBlueScore;
            }

            lastRedScore = nextRedScore;
            lastBlueScore = nextBlueScore;
            lastScoreUpdateTime = Time.realtimeSinceStartup;

            redScore = nextRedScore;
            blueScore = nextBlueScore;
            return true;
        }

        private static bool TryResolveConnectedPlayerByNumber(int playerNumber, out object player, out ulong clientId, out string playerKey, out string playerName)
        {
            player = null;
            clientId = 0;
            playerKey = null;
            playerName = null;

            try
            {
                foreach (var candidate in GetAllPlayers())
                {
                    if (candidate == null) continue;

                    ulong candidateClientId = 0;
                    TryGetClientId(candidate, out candidateClientId);

                    if (IsReplayPlayerObject(candidate, candidateClientId))
                    {
                        continue;
                    }

                    if (!TryGetPlayerNumber(candidate, out var candidateNumber) || candidateNumber != playerNumber)
                    {
                        continue;
                    }

                    player = candidate;
                    clientId = candidateClientId;
                    playerKey = GetPlayerKey(candidate, candidateClientId) ?? TryGetPlayerId(candidate, candidateClientId);
                    playerName = TryGetPlayerName(candidate) ?? $"#{playerNumber}";
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static bool TryForcePlayerToNeutralState(object player, ulong clientId, string playerKey)
        {
            if (player == null)
            {
                return false;
            }

            var assignmentKey = playerKey ?? $"clientId:{clientId}";
            var neutralCandidates = new[] { "Spectator", "None", "Unknown", "Unassigned" };
            foreach (var neutralCandidate in neutralCandidates)
            {
                try
                {
                    RegisterInternalTeamAssignment(assignmentKey, neutralCandidate);
                    using (BeginForcedTeamAssignment())
                    {
                        if (!TrySetPlayerTeamOnPlayer(player, neutralCandidate))
                        {
                            continue;
                        }
                    }

                    lock (teamStateLock)
                    {
                        lastKnownPlayerTeam[assignmentKey] = neutralCandidate;
                    }

                    return true;
                }
                catch { }
            }

            return false;
        }

        private static void UpdateTrackedPlayerTeam(string playerKey, TeamResult team)
        {
            if (string.IsNullOrEmpty(playerKey))
            {
                return;
            }

            lock (draftLock)
            {
                if (team == TeamResult.Red || team == TeamResult.Blue)
                {
                    draftAssignedTeams[playerKey] = team;
                }
                else
                {
                    draftAssignedTeams.Remove(playerKey);
                }
            }

            for (var index = 0; index < rankedParticipants.Count; index++)
            {
                var participant = rankedParticipants[index];
                if (participant == null) continue;
                var participantKey = ResolveParticipantIdToKey(participant);
                if (!string.Equals(participantKey, playerKey, StringComparison.OrdinalIgnoreCase)) continue;
                participant.team = team;
                break;
            }
        }

        private static bool TryGetRuntimeGameManager(out object gameManager, out Type gameManagerType)
        {
            gameManager = null;
            gameManagerType = FindTypeByName("GameManager", "Puck.GameManager");
            if (gameManagerType == null)
            {
                return false;
            }

            gameManager = GetManagerInstance(gameManagerType);
            return gameManager != null;
        }

        private static bool TryPreserveRemainingPlayTime(object gameManager)
        {
            try
            {
                if (gameManager == null) return false;
                if (!TryGetGameStateTime(gameManager, out var remainingTime)) return false;

                var gameManagerType = gameManager.GetType();
                var remainingField = gameManagerType.GetField("remainingPlayTime", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (remainingField != null)
                {
                    remainingField.SetValue(gameManager, remainingTime);
                    return true;
                }

                var remainingProperty = gameManagerType.GetProperty("remainingPlayTime", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (remainingProperty != null && remainingProperty.CanWrite)
                {
                    remainingProperty.SetValue(gameManager, remainingTime);
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static bool TryGetGameStateTime(object gameManager, out int time)
        {
            time = 0;
            try
            {
                if (gameManager == null) return false;
                var gameManagerType = gameManager.GetType();
                var gameStateProp = gameManagerType.GetProperty("GameState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var gameStateField = gameManagerType.GetField("GameState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var gameStateObj = gameStateProp != null ? gameStateProp.GetValue(gameManager) : gameStateField?.GetValue(gameManager);
                if (gameStateObj == null) return false;

                var valueProp = gameStateObj.GetType().GetProperty("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var target = valueProp != null ? valueProp.GetValue(gameStateObj) : gameStateObj;
                if (target == null) return false;

                var timeProp = target.GetType().GetProperty("Time", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (timeProp != null)
                {
                    var rawTime = timeProp.GetValue(target);
                    return TryConvertToInt(rawTime, out time);
                }

                var timeField = target.GetType().GetField("Time", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (timeField != null)
                {
                    var rawTime = timeField.GetValue(target);
                    return TryConvertToInt(rawTime, out time);
                }
            }
            catch { }

            return false;
        }

        private static bool TryInvokeServerUpdateGameState(Type gameManagerType, object gameManager, int blueScore, int redScore)
        {
            try
            {
                var method = gameManagerType?.GetMethod("Server_UpdateGameState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (method == null) return false;

                var parameters = method.GetParameters();
                if (parameters.Length != 5) return false;

                var args = new object[5];
                args[0] = null;
                args[1] = null;
                args[2] = null;
                args[3] = BoxIntForParameter(blueScore, parameters[3].ParameterType);
                args[4] = BoxIntForParameter(redScore, parameters[4].ParameterType);
                method.Invoke(gameManager, args);
                return true;
            }
            catch { }

            return false;
        }

        private static bool TryInvokeServerSetPhase(Type gameManagerType, object gameManager, string phaseName, int? timeSeconds = null)
        {
            try
            {
                var method = gameManagerType?.GetMethod("Server_SetPhase", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (method == null) return false;

                var parameters = method.GetParameters();
                if (parameters.Length < 1 || parameters.Length > 2 || !parameters[0].ParameterType.IsEnum) return false;

                var phaseValue = Enum.Parse(parameters[0].ParameterType, phaseName, true);
                var args = new object[parameters.Length];
                args[0] = phaseValue;
                if (parameters.Length == 2)
                {
                    var effectiveTime = timeSeconds ?? -1;
                    args[1] = BoxIntForParameter(effectiveTime, parameters[1].ParameterType);
                }

                method.Invoke(gameManager, args);
                return true;
            }
            catch { }

            return false;
        }

        private static object BoxIntForParameter(int value, Type targetType)
        {
            try
            {
                if (targetType == typeof(int)) return value;
                var underlyingType = Nullable.GetUnderlyingType(targetType);
                if (underlyingType == typeof(int)) return value;
            }
            catch { }

            return value;
        }

        
        public static bool TryStartMatch()
        {
            try
            {
                if (draftActive)
                {
                    SendSystemChatToAll("<size=14><color=#ffcc66>Ranked</color> waiting for draft completion before starting the match.</size>");
                    return false;
                }

                if (TryStartRankedMatchWithoutWarmup())
                {
                    SendSystemChatToAll("<size=14><color=#00ff00>Ranked</color> match auto-started.</size>");
                    return true;
                }

                // broaden candidate types/methods
                string[] typeNames = { "GameManager", "MatchManager", "MatchController", "GameController", "PuckMatchManager", "Puck.GameManager", "Puck.MatchManager" };
                string[] methodNames = { "Server_StartMatch", "Server_StartGame", "Server_Start", "StartMatch", "StartGame", "BeginMatch", "BeginGame", "StartRound", "BeginRound", "ForceStartMatch", "ForceStart" };

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var tn in typeNames)
                    {
                        var t = asm.GetType(tn) ?? asm.GetType($"Puck.{tn}"); if (t == null) continue;

                        // try static methods first
                        foreach (var mn in methodNames)
                        {
                            var methods = t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                            foreach (var m in methods)
                            {
                                if (!string.Equals(m.Name, mn, StringComparison.OrdinalIgnoreCase)) continue;
                                var ps = m.GetParameters();
                                try
                                {
                                    if (ps.Length == 0) { m.Invoke(null, null); SendSystemChatToAll("<size=14><color=#00ff00>Ranked</color> match auto-started.</size>"); return true; }
                                    var args = BuildDefaultArgs(ps);
                                    if (args != null) { m.Invoke(null, args); SendSystemChatToAll("<size=14><color=#00ff00>Ranked</color> match auto-started.</size>"); return true; }
                                }
                                catch { }
                            }
                        }

                        var inst = GetManagerInstance(t);
                        if (inst != null)
                        {
                            foreach (var mn in methodNames)
                            {
                                var methods = t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                                foreach (var m in methods)
                                {
                                    if (!string.Equals(m.Name, mn, StringComparison.OrdinalIgnoreCase)) continue;
                                    var ps = m.GetParameters();
                                    try
                                    {
                                        if (ps.Length == 0) { m.Invoke(inst, null); SendSystemChatToAll("<size=14><color=#00ff00>Ranked</color> match auto-started.</size>"); return true; }
                                        var args = BuildDefaultArgs(ps);
                                        if (args != null) { m.Invoke(inst, args); SendSystemChatToAll("<size=14><color=#00ff00>Ranked</color> match auto-started.</size>"); return true; }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }

                SendSystemChatToAll("<size=14><color=#ffcc66>Ranked</color> accepted, but could not auto-start a match. Start the match manually.</size>");
            }
            catch (Exception ex) { Debug.LogError($"[{Constants.MOD_NAME}] TryStartMatch failed: {ex.Message}"); }
            return false;
        }

        private static bool TryStartRankedMatchWithoutWarmup()
        {
            try
            {
                var gameManagerType = FindTypeByName("GameManager", "Puck.GameManager");
                if (gameManagerType == null)
                {
                    return false;
                }

                var gameManager = GetManagerInstance(gameManagerType);
                if (gameManager == null)
                {
                    return false;
                }

                var startMethods = gameManagerType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Where(method => string.Equals(method.Name, "Server_StartGame", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(method.Name, "StartGame", StringComparison.OrdinalIgnoreCase));

                foreach (var method in startMethods)
                {
                    var parameters = method.GetParameters();
                    var args = BuildDirectRankedStartArgs(parameters);
                    if (args == null)
                    {
                        continue;
                    }

                    Debug.Log($"[{Constants.MOD_NAME}] [RANKED] Suppressing intermediate practice/warmup phase.");
                    using (BeginIntentionalRankedStartPhase())
                    {
                        method.Invoke(gameManager, args);
                    }

                    if (TryGetCurrentGamePhaseName(gameManager, out var phaseName))
                    {
                        Debug.Log($"[{Constants.MOD_NAME}] [RANKED] Ranked match start phase = {phaseName}.");
                    }
                    else
                    {
                        Debug.Log($"[{Constants.MOD_NAME}] [RANKED] Ranked match start phase = unknown.");
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Ranked direct start without warmup failed: {ex.Message}");
            }

            return false;
        }

        private static IDisposable BeginIntentionalRankedStartPhase()
        {
            intentionalRankedStartPhaseDepth++;
            return new RankedStartPhaseScope();
        }

        private sealed class RankedStartPhaseScope : IDisposable
        {
            public void Dispose()
            {
                if (intentionalRankedStartPhaseDepth > 0)
                {
                    intentionalRankedStartPhaseDepth--;
                }
            }
        }

        private static bool TryEndMatch()
        {
            try
            {
                var gmType = FindTypeByName("GameManager", "Puck.GameManager");
                if (gmType != null)
                {
                    var gm = GetManagerInstance(gmType);
                    if (gm != null)
                    {
                        var gmMethod = gmType.GetMethod("Server_GameOver", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        if (gmMethod != null && gmMethod.GetParameters().Length == 0)
                        {
                            gmMethod.Invoke(gm, null);
                            return true;
                        }
                    }
                }

                string[] typeNames = { "GameManager", "MatchManager", "MatchController", "GameController", "PuckMatchManager", "Puck.GameManager", "Puck.MatchManager" };
                string[] methodNames = { "Server_OnGameOver", "Server_EndMatch", "Server_EndGame", "EndMatch", "EndGame", "Server_GameOver", "GameOver" };

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var tn in typeNames)
                    {
                        var t = asm.GetType(tn) ?? asm.GetType($"Puck.{tn}");
                        if (t == null) continue;

                        foreach (var mn in methodNames)
                        {
                            var methods = t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                            foreach (var m in methods)
                            {
                                if (!string.Equals(m.Name, mn, StringComparison.OrdinalIgnoreCase)) continue;
                                var ps = m.GetParameters();
                                try
                                {
                                    if (ps.Length == 0) { m.Invoke(null, null); return true; }
                                    var args = BuildDefaultArgs(ps);
                                    if (args != null) { m.Invoke(null, args); return true; }
                                }
                                catch { }
                            }
                        }

                        var inst = GetManagerInstance(t);
                        if (inst != null)
                        {
                            foreach (var mn in methodNames)
                            {
                                var methods = t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                                foreach (var m in methods)
                                {
                                    if (!string.Equals(m.Name, mn, StringComparison.OrdinalIgnoreCase)) continue;
                                    var ps = m.GetParameters();
                                    try
                                    {
                                        if (ps.Length == 0) { m.Invoke(inst, null); return true; }
                                        var args = BuildDefaultArgs(ps);
                                        if (args != null) { m.Invoke(inst, args); return true; }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] TryEndMatch failed: {ex.Message}");
            }
            return false;
        }

        // Build default argument array for parameters if possible (supports basic primitives and strings)
        private static object[] BuildDefaultArgs(System.Reflection.ParameterInfo[] ps)
        {
            if (ps == null) return null;
            var args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                var t = p.ParameterType;
                if (t == typeof(int) || t == typeof(short) || t == typeof(long) || t == typeof(uint) || t == typeof(ushort) || t == typeof(ulong)) args[i] = 0;
                else if (t == typeof(float) || t == typeof(double)) args[i] = 0f;
                else if (t == typeof(bool)) args[i] = true;
                else if (t == typeof(string)) args[i] = string.Empty;
                else if (t.IsEnum) args[i] = Activator.CreateInstance(t);
                else return null; // unsupported parameter type
            }
            return args;
        }

        private static object[] BuildDirectRankedStartArgs(System.Reflection.ParameterInfo[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
            {
                return null;
            }

            var args = BuildDefaultArgs(parameters);
            if (args == null || parameters[0].ParameterType != typeof(bool))
            {
                return null;
            }

            args[0] = false;
            return args;
        }

        private static bool TryGetCurrentGamePhaseName(object gameManager, out string phaseName)
        {
            phaseName = null;

            try
            {
                if (TryExtractCurrentGamePhaseName(gameManager, out phaseName))
                {
                    return true;
                }
            }
            catch { }

            lock (phaseLock)
            {
                if (!string.IsNullOrWhiteSpace(lastGamePhaseName))
                {
                    phaseName = lastGamePhaseName;
                    return true;
                }
            }

            return false;
        }

        private static bool TryExtractCurrentGamePhaseName(object gameManager, out string phaseName)
        {
            phaseName = null;
            if (gameManager == null)
            {
                return false;
            }

            var managerType = gameManager.GetType();
            foreach (var memberName in new[] { "GameState", "MatchState", "State" })
            {
                object stateHolder = null;
                var property = managerType.GetProperty(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (property != null)
                {
                    stateHolder = property.GetValue(gameManager);
                }

                if (stateHolder == null)
                {
                    var field = managerType.GetField(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (field != null)
                    {
                        stateHolder = field.GetValue(gameManager);
                    }
                }

                if (stateHolder == null)
                {
                    continue;
                }

                var resolvedState = stateHolder;
                var holderType = stateHolder.GetType();
                var valueProperty = holderType.GetProperty("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (valueProperty != null)
                {
                    resolvedState = valueProperty.GetValue(stateHolder) ?? stateHolder;
                }
                else
                {
                    var valueField = holderType.GetField("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (valueField != null)
                    {
                        resolvedState = valueField.GetValue(stateHolder) ?? stateHolder;
                    }
                }

                if (resolvedState == null)
                {
                    continue;
                }

                var resolvedType = resolvedState.GetType();
                object phaseValue = null;
                var phaseProperty = resolvedType.GetProperty("Phase", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (phaseProperty != null)
                {
                    phaseValue = phaseProperty.GetValue(resolvedState);
                }
                else
                {
                    var phaseField = resolvedType.GetField("Phase", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (phaseField != null)
                    {
                        phaseValue = phaseField.GetValue(resolvedState);
                    }
                }

                if (TryGetGamePhaseValue(phaseValue, out phaseName))
                {
                    return true;
                }
            }

            return false;
        }

        // Force-start a ranked match with an explicit participant list (admin)
        public static void ForceStart(List<RankedParticipant> participants)
        {
            try
            {
                if (!StartRankedFromEligible(participants, true))
                {
                    SendSystemChatToAll("<size=14><color=#ff6666>Ranked</color> admin start failed: could not initialize the captain draft.</size>");
                }
            }
            catch { }
        }

        // Force-end any active ranked state, clearing votes and participants (admin)
        public static void ForceEnd()
        {
            try
            {
                ResetRankedState(false, false);
                SendSystemChatToAll("<size=14><color=#ff6666>Ranked</color> forcibly ended by admin.</size>");
            }
            catch { }
        }

        public static bool TryIsAdminPublic(object player, ulong clientId)
        {
            return TryIsAdminInternal(player, clientId);
        }

        private static bool TryIsAdminInternal(object player, ulong clientId)
        {
            try
            {
                var resolvedPlayer = player;
                if (resolvedPlayer == null && clientId != 0 && TryGetPlayerByClientId(clientId, out var playerByClientId))
                {
                    resolvedPlayer = playerByClientId;
                }

                if (!TryResolveAdminSteamId(resolvedPlayer, clientId, out var steamId))
                {
                    return false;
                }

                if (TryGetNativeAdminSteamIds(out var nativeAdminSteamIds) && nativeAdminSteamIds.Contains(steamId))
                {
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool TryResolveAdminSteamId(object player, ulong clientId, out string steamId)
        {
            steamId = null;
            try
            {
                if (player != null)
                {
                    steamId = TryGetPlayerIdNoFallback(player);
                    if (!string.IsNullOrWhiteSpace(steamId))
                    {
                        steamId = steamId.Trim();
                        return true;
                    }
                }

                if (clientId != 0 && TryGetPlayerByClientId(clientId, out var playerByClientId))
                {
                    steamId = TryGetPlayerIdNoFallback(playerByClientId);
                    if (!string.IsNullOrWhiteSpace(steamId))
                    {
                        steamId = steamId.Trim();
                        return true;
                    }
                }
            }
            catch { }
            steamId = null;
            return false;
        }

        private static bool TryGetNativeAdminSteamIds(out HashSet<string> adminSteamIds)
        {
            adminSteamIds = null;
            try
            {
                var managerType = FindTypeByName("ServerManager", "Puck.ServerManager");
                var manager = GetManagerInstance(managerType);
                if (manager == null) return false;

                var managerTypeResolved = manager.GetType();
                var adminProp = managerTypeResolved.GetProperty("AdminSteamIds", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?? managerTypeResolved.GetProperty("adminSteamIds", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                object configuredValue = adminProp != null ? adminProp.GetValue(manager) : null;
                if (configuredValue == null)
                {
                    configuredValue = managerTypeResolved.GetField("AdminSteamIds", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(manager)
                        ?? managerTypeResolved.GetField("adminSteamIds", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(manager);
                }

                if (!(configuredValue is IEnumerable enumerable)) return false;

                var values = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in enumerable)
                {
                    var value = ExtractSimpleValueToString(entry);
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    values.Add(value.Trim());
                }

                if (values.Count == 0) return false;
                adminSteamIds = values;
                return true;
            }
            catch { }
            adminSteamIds = null;
            return false;
        }

        private static bool TryGetGamePhaseValue(object phaseObj, out string phaseName)
        {
            phaseName = null;
            try
            {
                if (phaseObj == null) return false;
                var t = phaseObj.GetType();
                if (t.IsGenericType && t.GetGenericTypeDefinition().FullName == "System.Nullable`1")
                {
                    var hasValueProp = t.GetProperty("HasValue");
                    var valueProp = t.GetProperty("Value");
                    if (hasValueProp != null && valueProp != null)
                    {
                        var hasValue = hasValueProp.GetValue(phaseObj);
                        if (hasValue is bool b && b)
                        {
                            var inner = valueProp.GetValue(phaseObj);
                            if (inner != null)
                            {
                                phaseName = inner.ToString().ToLowerInvariant();
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    phaseName = phaseObj.ToString().ToLowerInvariant();
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool TryGetScoresFromGameState(object gameManagerInstance, out int redScore, out int blueScore)
        {
            redScore = 0;
            blueScore = 0;
            try
            {
                if (gameManagerInstance == null) return false;
                var t = gameManagerInstance.GetType();
                var gameStateProp = t.GetProperty("GameState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (gameStateProp == null)
                {
                    var gameStateField = t.GetField("GameState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (gameStateField != null)
                    {
                        var gameStateObj = gameStateField.GetValue(gameManagerInstance);
                        return ExtractScoresFromGameState(gameStateObj, out redScore, out blueScore);
                    }
                    return false;
                }
                else
                {
                    var gameStateObj = gameStateProp.GetValue(gameManagerInstance);
                    return ExtractScoresFromGameState(gameStateObj, out redScore, out blueScore);
                }
            }
            catch { }
            return false;
        }

        private static bool TryGetPeriodFromGameState(object gameManagerInstance, out int period)
        {
            period = 0;
            try
            {
                if (gameManagerInstance == null) return false;
                var t = gameManagerInstance.GetType();
                var gameStateProp = t.GetProperty("GameState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (gameStateProp == null)
                {
                    var gameStateField = t.GetField("GameState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (gameStateField != null)
                    {
                        var gameStateObj = gameStateField.GetValue(gameManagerInstance);
                        return ExtractPeriodFromGameState(gameStateObj, out period);
                    }

                    return false;
                }

                return ExtractPeriodFromGameState(gameStateProp.GetValue(gameManagerInstance), out period);
            }
            catch { }

            return false;
        }

        private static bool ExtractPeriodFromGameState(object gameStateObj, out int period)
        {
            period = 0;
            try
            {
                if (gameStateObj == null) return false;
                var t = gameStateObj.GetType();

                var valueProp = t.GetProperty("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                object targetObj = gameStateObj;
                if (valueProp != null)
                {
                    var valueObj = valueProp.GetValue(gameStateObj);
                    if (valueObj != null) targetObj = valueObj;
                }

                var targetType = targetObj.GetType();
                var periodProp = targetType.GetProperty("Period", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?? targetType.GetProperty("period", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (periodProp != null)
                {
                    var rawValue = periodProp.GetValue(targetObj);
                    if (rawValue is int intValue)
                    {
                        period = intValue;
                        return true;
                    }

                    if (rawValue != null && int.TryParse(rawValue.ToString(), out var parsedValue))
                    {
                        period = parsedValue;
                        return true;
                    }
                }

                var periodField = targetType.GetField("Period", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?? targetType.GetField("period", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (periodField != null)
                {
                    var rawValue = periodField.GetValue(targetObj);
                    if (rawValue is int intValue)
                    {
                        period = intValue;
                        return true;
                    }

                    if (rawValue != null && int.TryParse(rawValue.ToString(), out var parsedValue))
                    {
                        period = parsedValue;
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private static bool ExtractScoresFromGameState(object gameStateObj, out int redScore, out int blueScore)
        {
            redScore = 0;
            blueScore = 0;
            try
            {
                if (gameStateObj == null) return false;
                var t = gameStateObj.GetType();
                
                // Try to get .Value if it's a NetworkVariable
                var valueProp = t.GetProperty("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                object targetObj = gameStateObj;
                if (valueProp != null)
                {
                    var valueObj = valueProp.GetValue(gameStateObj);
                    if (valueObj != null) targetObj = valueObj;
                }

                // Search properties and fields for red/blue numeric candidates
                var candidates = new Dictionary<string,int>();
                var targetType = targetObj.GetType();
                foreach (var prop in targetType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
                {
                    try
                    {
                        var name = prop.Name.ToLowerInvariant();
                        if (!(name.Contains("red") || name.Contains("blue"))) continue;
                        var val = prop.GetValue(targetObj);
                        if (val == null) continue;
                        if (val is int iv) candidates[prop.Name] = iv;
                        else if (int.TryParse(val.ToString(), out var parsed)) candidates[prop.Name] = parsed;
                    }
                    catch { }
                }
                foreach (var field in targetType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
                {
                    try
                    {
                        var name = field.Name.ToLowerInvariant();
                        if (!(name.Contains("red") || name.Contains("blue"))) continue;
                        var val = field.GetValue(targetObj);
                        if (val == null) continue;
                        if (val is int iv) candidates[field.Name] = iv;
                        else if (int.TryParse(val.ToString(), out var parsed)) candidates[field.Name] = parsed;
                    }
                    catch { }
                }

                int? redCandidate = null, blueCandidate = null;
                foreach (var kv in candidates)
                {
                    var n = kv.Key.ToLowerInvariant();
                    if (n.Contains("red")) redCandidate = kv.Value;
                    else if (n.Contains("blue")) blueCandidate = kv.Value;
                }
                if (redCandidate.HasValue && blueCandidate.HasValue)
                {
                    redScore = redCandidate.Value;
                    blueScore = blueCandidate.Value;
                    return true;
                }
            }
            catch { }
            return false;
        }

    }
}
