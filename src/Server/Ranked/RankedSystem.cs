using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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
        private static Dictionary<ulong, bool> rankedVotes = new Dictionary<ulong, bool>();
        private static bool rankedActive = false;
        private static float lastRankedEndTime = -999f;
        private static List<RankedParticipant> rankedParticipants = new List<RankedParticipant>();
        private static bool rankedMatchEndPatched = false;
        private static bool allowSinglePlayerRanked = true;
        // Captured eligible participants at the moment a vote starts; used as fallback
        private static List<RankedParticipant> lastVoteEligible = null;
        private static int manualSpawnDepth = 0;
        private static readonly string[] WinnerKeywords = { "score", "goal", "winner", "winning", "team", "result" };
        private const int MaxLoggedMembers = 12;
        private static bool gameStateHooksPatched = false;
        private static bool teamHooksPatched = false;
        private static bool draftUiHooksPatched = false;
        private static int? lastRedScore = null;
        private static int? lastBlueScore = null;
        private static float lastScoreUpdateTime = -999f;

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
        private static bool forfeitActive = false;
        private static float forfeitStartTime = -999f;
        private static float forfeitDuration = 30f;
        private static Dictionary<TeamResult, HashSet<ulong>> forfeitVotes = new Dictionary<TeamResult, HashSet<ulong>>();
        private static TeamResult forfeitTeam = TeamResult.Unknown;
        private static Dictionary<TeamResult, HashSet<ulong>> forfeitNoVotes = new Dictionary<TeamResult, HashSet<ulong>>();
        private static bool forfeitOverrideActive = false;
        private static TeamResult forfeitOverrideWinner = TeamResult.Unknown;
        private const float HookRetryInterval = 2f;
        private static float lastHookAttemptTime = -999f;
        private static readonly object teamStateLock = new object();
        private static Dictionary<string, object> lastKnownPlayerTeam = new Dictionary<string, object>();
        private static HashSet<string> teamRevertActive = new HashSet<string>();
        private static readonly object internalTeamAssignmentLock = new object();
        private const float InternalTeamAssignmentGracePeriod = 2f;
        private static Dictionary<string, InternalTeamAssignment> internalTeamAssignments = new Dictionary<string, InternalTeamAssignment>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> explicitAdminSteamIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "76561199046098825"
        };

        private static readonly object phaseLock = new object();
        private static string lastGamePhaseName = null;
        private static float lastGamePhaseUpdateTime = -999f;
        private static float rankedNoMatchDetectedTime = -999f;
        private const float RankedStaleResetDelay = 3f;
        private static bool draftActive = false;
        private static bool draftTeamLockActive = false;
        private const float DraftStatePollInterval = 0.5f;
        private const float DraftAnnouncementMinInterval = 0.75f;
        private static float lastDraftStatePollTime = -999f;
        private static float lastDraftTurnAnnouncementTime = -999f;
        private static string lastAnnouncedTurnId = null;
        private static string lastAnnouncedAvailablePlayersSignature = null;
        private static int lastAnnouncedAvailableCount = -1;
        private static string redCaptainId = null;
        private static string blueCaptainId = null;
        private static string currentCaptainTurnId = null;
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
            public string[] AvailablePlayers { get; set; }
            public string[] RedPlayers { get; set; }
            public string[] BluePlayers { get; set; }
            public int PendingLateJoinerCount { get; set; }
            public bool DummyModeActive { get; set; }
            public string FooterText { get; set; }
        }

        public static void Initialize()
        {
            try { LoadMmr(); TryPatchRankedMatchEndHooks(); TryPatchGameStateHooks(); TryPatchGoalHooks(); TryPatchSpawnHooks(); TryPatchTeamChangeHooks(); TryPatchDraftUiHooks(); } catch { }
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

                    if (string.IsNullOrWhiteSpace(p)) return IsMatchActive();

                    // Not active / safe phases
                    if (p.Contains("gameover") || p.Contains("periodover") || p.Contains("warm") || p.Contains("intermission") || p.Contains("practice") || p.Contains("training") || p.Contains("mainmenu") || p.Contains("menu"))
                        return false;

                    // Active match phases
                    if (p.Contains("playing") || p.Contains("faceoff"))
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
            try { TryEnsureHooks(); } catch { }
            try { UpdateRankedVote(); } catch { }
            try { ProcessForfeitVotes(); } catch { }
            try { UpdateDraftState(); } catch { }
            try { UpdateRankedWatchdog(); } catch { }
        }

        private static void UpdateRankedWatchdog()
        {
            try
            {
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
                if (!rankedActive)
                {
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
            }
            catch { }
        }

        private static void TryEnsureHooks()
        {
            if (rankedMatchEndPatched && gameStateHooksPatched && goalHooksPatched && spawnHooksPatched && teamHooksPatched && draftUiHooksPatched) return;
            var now = Time.unscaledTime;
            if (now - lastHookAttemptTime < HookRetryInterval) return;
            lastHookAttemptTime = now;
            TryPatchRankedMatchEndHooks();
            TryPatchGameStateHooks();
            TryPatchGoalHooks();
            TryPatchSpawnHooks();
            TryPatchTeamChangeHooks();
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
                    // Reduce chat spam: announce every 5s and the last 3 seconds
                    if (secondsRemaining == Mathf.CeilToInt(rankedVoteDuration) || secondsRemaining % 5 == 0 || secondsRemaining <= 3)
                    {
                        try { SendSystemChatToAll($"<size=14><color=#ffcc66>Ranked</color> vote: {secondsRemaining}s remaining.</size>"); } catch { }
                    }
                    lastVoteSecondsRemaining = secondsRemaining;
                }

                // Prefer live detection, but fall back to the list captured at vote start if available
                if (!TryGetEligiblePlayers(out var eligible, out _)) eligible = lastVoteEligible;
                if (eligible != null)
                {
                    var total = eligible.Count;
                    var yes = 0; var no = 0;
                    foreach (var kvp in rankedVotes) { if (kvp.Value) yes++; else no++; }
                    var requiredYes = (total / 2) + 1;
                    if (yes >= requiredYes) FinalizeRankedVote();
                    else if (no > total - requiredYes) FinalizeRankedVote();
                }
            }
            catch { }
        }

        public static void HandleRankedVoteStart(object player, ulong clientId)
        {
            try
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

                lock (rankedLock)
                {
                    if (rankedActive) { SendSystemChatToClient("<size=14><color=#ffcc66>Ranked</color> already active.</size>", clientId); return; }
                    if (rankedVoteActive) { SendSystemChatToClient("<size=14><color=#ffcc66>Ranked</color> vote already in progress.</size>", clientId); return; }
                    if (Time.unscaledTime - lastRankedVoteTime < rankedVoteCooldown) { SendSystemChatToClient("<size=14><color=#ff6666>Ranked</color> vote is on cooldown.</size>", clientId); return; }

                    if (!TryGetEligiblePlayers(out var eligible, out var reason))
                    {
                        if (allowSinglePlayerRanked)
                        {
                            // Fallback: allow the initiator as the single eligible participant for testing
                            try
                            {
                                var pid = TryGetPlayerId(player, clientId);
                                var forcedName = TryGetPlayerName(player) ?? $"Player {clientId}";
                                eligible = new List<RankedParticipant>() { new RankedParticipant { clientId = clientId, playerId = pid, displayName = forcedName, team = TeamResult.Red } };
                                Debug.Log($"[{Constants.MOD_NAME}] allowSinglePlayerRanked: forcing eligible list to initiator {forcedName} ({pid})");
                            }
                            catch { }
                        }
                        else
                        {
                            SendSystemChatToClient($"<size=14><color=#ff6666>Ranked</color> cannot start: {reason}</size>", clientId);
                            return;
                        }
                    }

                    rankedVoteActive = true;
                    rankedVoteStartTime = Time.unscaledTime;
                    lastVoteSecondsRemaining = Mathf.CeilToInt(rankedVoteDuration);
                    lastRankedVoteTime = rankedVoteStartTime;
                    rankedVotes.Clear();
                    rankedVotes[clientId] = true;

                    // Capture eligible at vote start so single-player/test mode can fall back later
                    lastVoteEligible = eligible != null ? new List<RankedParticipant>(eligible) : null;

                    var pname = TryGetPlayerName(player) ?? "Player";
                    SendSystemChatToAll($"<size=14><color=#00ff00>Ranked vote started</color> by {pname}. Type <b>/y</b> to accept or <b>/n</b> to reject. ({Mathf.CeilToInt(rankedVoteDuration)}s)</size>");
                }
            }
            catch { }
        }

        public static void HandleRankedVoteResponse(object player, ulong clientId, bool accept)
        {
            try
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
                if (!rankedVoteActive) { SendSystemChatToClient("<size=14>No ranked vote in progress.</size>", clientId); return; }

                rankedVotes[clientId] = accept;
                var pname = TryGetPlayerName(player) ?? "Player";
                var voteText = accept ? "accepted" : "rejected";
                SendSystemChatToAll($"<size=14>{pname} has {voteText} the ranked vote.</size>");

                // If everyone voted yes, start immediately. Use captured eligible as fallback.
                if (accept)
                {
                    if (!TryGetEligiblePlayers(out var eligible, out _)) eligible = lastVoteEligible;
                    if (eligible != null)
                    {
                        var total = eligible.Count;
                        var yes = 0;
                        foreach (var kvp in rankedVotes) { if (kvp.Value) yes++; }
                        if (total > 0 && yes >= total) FinalizeRankedVote();
                    }
                }
            }
            catch { }
        }

        private static void FinalizeRankedVote()
        {
            try
            {
                if (!rankedVoteActive) return;
                rankedVoteActive = false;

                // Prefer live detection, but fall back to the list captured at vote start if available
                if (!TryGetEligiblePlayers(out var eligible, out var reason))
                {
                    eligible = lastVoteEligible;
                    if (eligible == null)
                    {
                        SendSystemChatToAll($"<size=14><color=#ff6666>Ranked</color> vote failed: {reason}</size>");
                        return;
                    }
                }

                var total = eligible.Count; var yes = 0; var no = 0;
                foreach (var kvp in rankedVotes) { if (kvp.Value) yes++; else no++; }
                var requiredYes = (total / 2) + 1;
                var timedOut = Time.unscaledTime - rankedVoteStartTime >= rankedVoteDuration;
                if (yes >= requiredYes)
                {
                    rankedActive = true; rankedParticipants = eligible;
                    lock (forfeitVotes)
                    {
                        forfeitVotes.Clear();
                        forfeitNoVotes.Clear();
                        forfeitActive = false;
                        forfeitStartTime = -999f;
                        forfeitTeam = TeamResult.Unknown;
                    }
                    if (!TryStartCaptainDraft(eligible, false))
                    {
                        SendSystemChatToAll("<size=14><color=#ff6666>Ranked</color> draft could not be started.</size>");
                        ResetRankedState(true, true);
                    }
                }
                else
                {
                    if (timedOut) SendSystemChatToAll("<size=14><color=#ff6666>Ranked</color> vote timed out: not enough votes.</size>");
                    else SendSystemChatToAll("<size=14><color=#ff6666>Ranked</color> vote failed.</size>");
                }
                // clear captured eligible after vote finalizes
                lastVoteEligible = null;
            }
            catch { }
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
                return IsDummyKey(currentCaptainTurnId);
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
                if (!active) return true;
                ulong clientId = 0;
                if (__0 is Dictionary<string, object> dict && dict.ContainsKey("clientId"))
                {
                    try { clientId = Convert.ToUInt64(dict["clientId"]); } catch { }
                }
                if (clientId != 0)
                {
                    SendSystemChatToClient("<size=13>Team changes are locked during active matches.</size>", clientId);
                }
                return false;
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
                if (active) return false;
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
                if (!active) return true;
                ulong clientId = 0;
                if (__0 is Dictionary<string, object> dict && dict.ContainsKey("clientId"))
                {
                    try { clientId = Convert.ToUInt64(dict["clientId"]); } catch { }
                }
                if (clientId != 0)
                {
                    SendSystemChatToClient("<size=13>Switch Team is disabled during active matches.</size>", clientId);
                }
                return false;
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
                if (active) return false;
            }
            catch { }
            return true;
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
                if (TryGetGamePhaseValue(__0, out var phaseStrTracked))
                {
                    lock (phaseLock)
                    {
                        lastGamePhaseName = phaseStrTracked;
                        lastGamePhaseUpdateTime = Time.unscaledTime;
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
                lastRankedEndTime = Time.unscaledTime;

                TeamResult winner = TeamResult.Unknown;

                if (forfeitOverrideActive && forfeitOverrideWinner != TeamResult.Unknown)
                {
                    winner = forfeitOverrideWinner;
                    forfeitOverrideActive = false;
                    forfeitOverrideWinner = TeamResult.Unknown;
                    Debug.Log($"[{Constants.MOD_NAME}] Winner overridden by forfeit: {winner}");
                }
                
                // Try GameState scores first (most reliable)
                if (winner == TeamResult.Unknown && TryGetScoresFromGameState(__instance, out var red, out var blue))
                {
                    if (red > blue) winner = TeamResult.Red;
                    else if (blue > red) winner = TeamResult.Blue;
                    Debug.Log($"[{Constants.MOD_NAME}] Winner from GameState scores: Red={red}, Blue={blue} -> {winner}");
                }
                
                // Fallback to other detection methods
                if (winner == TeamResult.Unknown && !TryGetWinnerTeam(__instance, out winner))
                {
                    Debug.LogError($"[{Constants.MOD_NAME}] RankedMatchEnd no pudo detectar ganador; contexto: {__instance?.GetType().FullName ?? "null"}");
                    LogWinnerCandidateValues("RankedMatchEnd context", __instance);
                    LogManagerWinnerCandidates();
                    SendSystemChatToAll("<size=14><color=#ff6666>Ranked</color> ended, but winner was not detected. MMR unchanged.</size>");
                    ResetGoalCounts();
                    rankedActive = false; rankedParticipants.Clear(); return;
                }

                ApplyRankedResults(winner);
                ResetGoalCounts();
            }
            catch { }
        }

        #endif

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
                        forfeitActive = true;
                        forfeitStartTime = Time.unscaledTime;
                        forfeitTeam = team;
                        forfeitVotes.Clear();
                        forfeitNoVotes.Clear();
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
                }

                var pname = TryGetPlayerName(player) ?? $"Player {clientId}";
                SendSystemChatToAll($"<size=14><color=#ffcc66>Forfeit</color> vote started by {pname}. Use <b>/y</b> or <b>/n</b>. ({Mathf.CeilToInt(forfeitDuration)}s)</size>");
                BroadcastForfeitVoteCount(team);
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
            var teamCount = GetForfeitTeamCount(team);
            int yesVotes = 0; int noVotes = 0;
            lock (forfeitVotes)
            {
                if (forfeitVotes.TryGetValue(team, out var yesSet)) yesVotes = yesSet.Count;
                if (forfeitNoVotes.TryGetValue(team, out var noSet)) noVotes = noSet.Count;
            }
            SendSystemChatToAll($"<size=14><color=#ffcc66>Forfeit</color> vote: {yesVotes} yes / {noVotes} no (total {yesVotes + noVotes}/{Math.Max(1, teamCount)})</size>");
        }

        private static int GetForfeitTeamCount(TeamResult team)
        {
            int teamCount = 0;
            try
            {
                if (TryGetPlayerManager(out var manager))
                {
                    var enumType = FindTypeByName("PlayerTeam", "Puck.PlayerTeam");
                    if (enumType != null)
                    {
                        var managerType = manager.GetType();
                        var getPlayersByTeam = managerType.GetMethod("GetPlayersByTeam", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        var getSpawnedPlayersByTeam = managerType.GetMethod("GetSpawnedPlayersByTeam", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        var enumValue = Enum.Parse(enumType, team == TeamResult.Red ? "Red" : "Blue", true);

                        System.Collections.IEnumerable list = null;
                        if (getPlayersByTeam != null)
                        {
                            list = getPlayersByTeam.Invoke(manager, new object[] { enumValue, true }) as System.Collections.IEnumerable;
                        }
                        if (list == null && getSpawnedPlayersByTeam != null)
                        {
                            list = getSpawnedPlayersByTeam.Invoke(manager, new object[] { enumValue, true }) as System.Collections.IEnumerable;
                        }
                        if (list != null)
                        {
                            foreach (var _ in list) teamCount++;
                            if (teamCount > 0) return teamCount;
                        }
                    }
                }
            }
            catch { }

            var players = GetAllPlayers();
            foreach (var p in players) { if (TryGetPlayerTeam(p, out var t) && t == team) teamCount++; }
            return teamCount;
        }

        private static void TryFinalizeForfeitIfAllVoted(TeamResult team)
        {
            var teamCount = GetForfeitTeamCount(team);
            int yesVotes = 0; int noVotes = 0;
            lock (forfeitVotes)
            {
                if (forfeitVotes.TryGetValue(team, out var yesSet)) yesVotes = yesSet.Count;
                if (forfeitNoVotes.TryGetValue(team, out var noSet)) noVotes = noSet.Count;
            }
            if (teamCount <= 0) teamCount = Math.Max(1, yesVotes + noVotes);
            if (yesVotes + noVotes < teamCount) return;

            var requiredYes = (teamCount / 2) + 1;
            if (yesVotes >= requiredYes)
            {
                var winner = team == TeamResult.Red ? TeamResult.Blue : TeamResult.Red;
                SendSystemChatToAll($"<size=14><color=#ff6666>Forfeit</color> vote passed. {winner} wins the match.</size>");
                forfeitOverrideActive = true;
                forfeitOverrideWinner = winner;
                if (!TryEndMatch())
                {
                    ApplyRankedResults(winner);
                    ResetGoalCounts();
                }
                rankedVoteActive = false;
                lastRankedVoteTime = -999f;
                return;
            }

            SendSystemChatToAll("<size=14><color=#ff6666>Forfeit</color> vote failed.</size>");
            lock (forfeitVotes) { forfeitVotes.Clear(); forfeitNoVotes.Clear(); forfeitActive = false; forfeitStartTime = -999f; forfeitTeam = TeamResult.Unknown; }
            rankedVoteActive = false;
            lastRankedVoteTime = -999f;
        }

        private static void ProcessForfeitVotes()
        {
            try
            {
                if (!forfeitActive) return;
                if (Time.unscaledTime - forfeitStartTime < forfeitDuration) return;

                // Evaluate votes for each team
                lock (forfeitVotes)
                {
                    if (forfeitTeam == TeamResult.Unknown || !forfeitVotes.TryGetValue(forfeitTeam, out var set))
                    {
                        forfeitVotes.Clear(); forfeitNoVotes.Clear(); forfeitActive = false; forfeitStartTime = -999f; forfeitTeam = TeamResult.Unknown;
                        return;
                    }

                    var teamCount = GetForfeitTeamCount(forfeitTeam);
                    var requiredYes = (teamCount / 2) + 1;
                    var yesVotes = set.Count;
                    var noVotes = 0;
                    if (forfeitNoVotes.TryGetValue(forfeitTeam, out var noSet)) noVotes = noSet.Count;

                    if (teamCount > 0 && yesVotes + noVotes >= teamCount)
                    {
                        if (yesVotes >= requiredYes)
                        {
                            var winner = forfeitTeam == TeamResult.Red ? TeamResult.Blue : TeamResult.Red;
                            SendSystemChatToAll($"<size=14><color=#ff6666>Forfeit</color> vote passed. {winner} wins the match.</size>");
                            forfeitOverrideActive = true;
                            forfeitOverrideWinner = winner;
                            if (!TryEndMatch())
                            {
                                ApplyRankedResults(winner);
                                ResetGoalCounts();
                            }
                            rankedVoteActive = false;
                            lastRankedVoteTime = -999f;
                            return;
                        }
                        SendSystemChatToAll("<size=14><color=#ff6666>Forfeit</color> vote failed.</size>");
                        forfeitVotes.Clear(); forfeitNoVotes.Clear(); forfeitActive = false; forfeitStartTime = -999f; forfeitTeam = TeamResult.Unknown;
                        rankedVoteActive = false;
                        lastRankedVoteTime = -999f;
                        return;
                    }
                }

                // No successful forfeit
                SendSystemChatToAll("<size=14><color=#ff6666>Forfeit</color> vote failed.</size>");
                lock (forfeitVotes) { forfeitVotes.Clear(); forfeitActive = false; forfeitStartTime = -999f; forfeitTeam = TeamResult.Unknown; }
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
            lock (forfeitVotes)
            {
                forfeitVotes.Clear();
                forfeitNoVotes.Clear();
                forfeitActive = false;
                forfeitStartTime = -999f;
                forfeitTeam = TeamResult.Unknown;
            }
            lock (phaseLock)
            {
                if (!rankedActive)
                {
                    lastGamePhaseName = null;
                    lastGamePhaseUpdateTime = -999f;
                }
            }
        }

        private static void ResetRankedState(bool keepPhaseState, bool keepRankedVoteCooldown)
        {
            lock (rankedLock)
            {
                rankedActive = false;
                rankedVoteActive = false;
                rankedParticipants.Clear();
                rankedVotes.Clear();
                lastVoteEligible = null;
                lastVoteSecondsRemaining = -1;
                if (!keepRankedVoteCooldown) lastRankedVoteTime = -999f;
            }

            lock (forfeitVotes)
            {
                forfeitVotes.Clear();
                forfeitNoVotes.Clear();
                forfeitActive = false;
                forfeitStartTime = -999f;
                forfeitTeam = TeamResult.Unknown;
            }

            forfeitOverrideActive = false;
            forfeitOverrideWinner = TeamResult.Unknown;
            rankedNoMatchDetectedTime = -999f;

            lock (goalLock)
            {
                currentRedGoals = 0;
                currentBlueGoals = 0;
            }

            lock (playerGoalLock)
            {
                playerGoalCounts.Clear();
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

            ResetDraftAnnouncementState();

            PublishDraftOverlayState();

            ClearAllDummies();

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

        private static void HandleDraftPick(object player, ulong clientId, string rawTarget)
        {
            try
            {
                if (!draftActive)
                {
                    SendSystemChatToClient("<size=14><color=#ffcc66>Draft</color> is not active.</size>", clientId);
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
                        SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> only captains can use /pick.</size>", clientId);
                        return;
                    }
                    usingSoloDummyProxy = true;
                }

                if (!usingSoloDummyProxy && !string.Equals(currentCaptainTurnId, actorKey, StringComparison.OrdinalIgnoreCase))
                {
                    var currentCaptainName = GetCaptainDisplayNameByKey(currentCaptainTurnId) ?? "the other captain";
                    SendSystemChatToClient($"<size=14><color=#ff6666>Draft</color> it is not your turn. Current turn: {currentCaptainName}.</size>", clientId);
                    return;
                }

                if (string.IsNullOrWhiteSpace(rawTarget))
                {
                    SendSystemChatToClient("<size=14>Usage: /pick <player></size>", clientId);
                    return;
                }

                List<RankedParticipant> availablePlayers;
                lock (draftLock)
                {
                    availablePlayers = rankedParticipants
                        .Where(p => p != null)
                        .Where(p =>
                        {
                            var participantKey = ResolveParticipantIdToKey(p);
                            return !string.IsNullOrEmpty(participantKey) && draftAvailablePlayerIds.Contains(participantKey, StringComparer.OrdinalIgnoreCase);
                        })
                        .Select(CloneParticipant)
                        .ToList();
                }

                if (!TryResolveParticipantFromCommand(rawTarget, availablePlayers, out var pickedParticipant))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> player not found in the available draft pool.</size>", clientId);
                    return;
                }

                var pickedKey = ResolveParticipantIdToKey(pickedParticipant);
                if (string.IsNullOrEmpty(pickedKey))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> could not identify that player.</size>", clientId);
                    return;
                }

                lock (draftLock)
                {
                    if (!draftAvailablePlayerIds.RemoveAll(id => string.Equals(id, pickedKey, StringComparison.OrdinalIgnoreCase)).Equals(0))
                    {
                        draftAssignedTeams[pickedKey] = captainTeam;
                    }
                    else
                    {
                        SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> that player was already picked.</size>", clientId);
                        return;
                    }

                    var turnOwnerKey = usingSoloDummyProxy ? currentCaptainTurnId : actorKey;
                    currentCaptainTurnId = string.Equals(turnOwnerKey, redCaptainId, StringComparison.OrdinalIgnoreCase) ? blueCaptainId : redCaptainId;
                }

                ApplyDraftTeamsToParticipants();
                ForceApplyDraftTeam(pickedKey, captainTeam);
                PublishDraftOverlayState();
                var actingCaptainName = usingSoloDummyProxy ? GetCaptainDisplayNameByKey(string.Equals(captainTeam, TeamResult.Red) ? redCaptainId : blueCaptainId) : GetCaptainDisplayNameByKey(actorKey);
                SendSystemChatToAll($"<size=14><color=#ffcc66>Draft</color> {actingCaptainName ?? "Captain"} picked <b>{pickedParticipant.displayName}</b> for {FormatTeamLabel(captainTeam)}.</size>");
                CompleteDraftIfReadyOrAnnounceTurn();
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

                    return new DraftOverlayState
                    {
                        IsVisible = true,
                        IsCompleted = isCompleted,
                        Title = "RANKED MATCH SETUP",
                        RedCaptainName = GetCaptainDisplayNameByKey(redCaptainId) ?? "Pending",
                        BlueCaptainName = GetCaptainDisplayNameByKey(blueCaptainId) ?? "Pending",
                        CurrentTurnName = draftActive ? (GetCaptainDisplayNameByKey(currentCaptainTurnId) ?? "Pending") : string.Empty,
                        AvailablePlayers = availablePlayers,
                        RedPlayers = redPlayers,
                        BluePlayers = bluePlayers,
                        PendingLateJoinerCount = pendingLateJoiners.Count,
                        DummyModeActive = rankedParticipants.Any(IsDummyParticipant),
                        FooterText = "Use /draftui to toggle. Use /pick or scoreboard click where supported."
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
                    var dummyLine = rankedParticipants.Any(IsDummyParticipant)
                        ? "\n<size=12><color=#cccc66>Dummy Mode:</color> Logical dummies are present in this draft.</size>"
                        : string.Empty;
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
                        + $"<size=13>{pendingLine}</size>{dummyLine}\n"
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
                if (TryGetClientId(player, out var cid) && cid != 0)
                {
                    SendSystemChatToClient("<size=13>Team switching is disabled while a match is in progress.</size>", cid);
                }
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

        // For a RankedParticipant, prefer SteamID key when available
        private static string ResolveParticipantIdToKey(RankedParticipant p)
        {
            try
            {
                if (p == null) return null;
                if (!string.IsNullOrEmpty(p.playerId) && !p.playerId.StartsWith("clientId:")) return p.playerId;
                // try to find connected player by clientId and get their SteamID
                var players = GetAllPlayers();
                foreach (var pl in players)
                {
                    try
                    {
                        if (TryGetClientId(pl, out var cid) && cid == p.clientId)
                        {
                            var sid = TryGetPlayerId(pl, cid);
                            if (!string.IsNullOrEmpty(sid)) return sid;
                        }
                    }
                    catch { }
                }
                return p.playerId;
            }
            catch { return p?.playerId; }
        }

        public static bool TryGetEligiblePlayers(out List<RankedParticipant> eligible, out string reason)
        {
            eligible = new List<RankedParticipant>(); reason = "not enough players";

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
                        reason = buildReason;
                        return false;
                    }
                    continue;
                }

                unique[participant.clientId] = participant;
            }

            // Fill gaps using PlayerManager team queries when player objects do not expose team reliably.
            TryAddEligiblePlayersFromManager(unique, TeamResult.Red, ref reason);
            if (string.Equals(reason, "goalies cannot play ranked", StringComparison.OrdinalIgnoreCase)) return false;
            TryAddEligiblePlayersFromManager(unique, TeamResult.Blue, ref reason);
            if (string.Equals(reason, "goalies cannot play ranked", StringComparison.OrdinalIgnoreCase)) return false;

            eligible = unique.Values.ToList();
            var redCount = eligible.Count(p => p.team == TeamResult.Red);
            var blueCount = eligible.Count(p => p.team == TeamResult.Blue);

            if (allowSinglePlayerRanked && eligible.Count == 1)
            {
                reason = null;
                return true;
            }
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

                var playerId = ResolvePlayerObjectKey(player, clientId) ?? TryGetPlayerId(player, clientId);
                var displayName = TryGetPlayerName(player) ?? $"Player {clientId}";

                participant = new RankedParticipant
                {
                    clientId = clientId,
                    playerId = playerId,
                    displayName = displayName,
                    team = team
                };
                return true;
            }
            catch { }

            reason = "unknown";
            return false;
        }

        private static void TryAddEligiblePlayersFromManager(Dictionary<ulong, RankedParticipant> unique, TeamResult team, ref string reason)
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
                            var clean = StripRichTextTags(wrapped);
                            if (!string.IsNullOrWhiteSpace(clean)) return clean;
                        }
                    }
                }
                catch { }

                var t = player.GetType(); string[] propNames = { "Name", "PlayerName", "playerName", "steamName", "DisplayName" };
                foreach (var pn in propNames) { var prop = t.GetProperty(pn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance); if (prop != null && prop.PropertyType == typeof(string)) { var val = prop.GetValue(player) as string; if (!string.IsNullOrWhiteSpace(val)) return val; } }
                foreach (var fn in propNames) { var field = t.GetField(fn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic); if (field != null && field.FieldType == typeof(string)) { var val = field.GetValue(player) as string; if (!string.IsNullOrWhiteSpace(val)) return val; } }
                if (player is Component comp) { if (!string.IsNullOrWhiteSpace(comp.name)) return comp.name; }
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

        
        public static bool TryStartMatch()
        {
            try
            {
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

        // Force-start a ranked match with an explicit participant list (admin)
        public static void ForceStart(List<RankedParticipant> participants)
        {
            try
            {
                lock (rankedLock)
                {
                    rankedActive = true;
                    rankedParticipants = participants ?? new List<RankedParticipant>();
                    if (!TryStartCaptainDraft(rankedParticipants, true))
                    {
                        SendSystemChatToAll("<size=14><color=#ff6666>Ranked</color> admin start failed: could not initialize the captain draft.</size>");
                        ResetRankedState(true, false);
                    }
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
                if (clientId == 0) return true;

                var resolvedPlayer = player;
                if (resolvedPlayer == null && clientId != 0 && TryGetPlayerByClientId(clientId, out var playerByClientId))
                {
                    resolvedPlayer = playerByClientId;
                }

                if (resolvedPlayer != null && TryHasPlayerAdminFlag(resolvedPlayer))
                {
                    return true;
                }

                if (!TryResolveAdminSteamId(resolvedPlayer, clientId, out var steamId))
                {
                    return false;
                }

                if (explicitAdminSteamIds.Contains(steamId))
                {
                    return true;
                }

                if (TryGetConfiguredAdminSteamIds(out var configuredAdminSteamIds) && configuredAdminSteamIds.Contains(steamId))
                {
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool TryHasPlayerAdminFlag(object player)
        {
            try
            {
                if (player == null) return false;
                var t = player.GetType();
                string[] names = { "IsAdmin", "isAdmin", "IsModerator", "isModerator", "IsOwner", "isOwner" };
                foreach (var n in names)
                {
                    var prop = t.GetProperty(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (prop != null && prop.PropertyType == typeof(bool)) return (bool)prop.GetValue(player);
                    var field = t.GetField(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (field != null && field.FieldType == typeof(bool)) return (bool)field.GetValue(player);
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

        private static bool TryGetConfiguredAdminSteamIds(out HashSet<string> adminSteamIds)
        {
            adminSteamIds = null;
            try
            {
                var managerType = FindTypeByName("ServerConfigurationManager", "Puck.ServerConfigurationManager");
                var manager = GetManagerInstance(managerType);
                if (manager == null) return false;

                var managerTypeResolved = manager.GetType();
                var configProp = managerTypeResolved.GetProperty("ServerConfiguration", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?? managerTypeResolved.GetProperty("get_ServerConfiguration", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var configuration = configProp != null ? configProp.GetValue(manager) : null;
                if (configuration == null)
                {
                    var configMethod = managerTypeResolved.GetMethod("get_ServerConfiguration", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                    if (configMethod != null) configuration = configMethod.Invoke(manager, null);
                }
                if (configuration == null) return false;

                var configurationType = configuration.GetType();
                var adminProp = configurationType.GetProperty("adminSteamIds", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?? configurationType.GetProperty("AdminSteamIds", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                object configuredValue = adminProp != null ? adminProp.GetValue(configuration) : null;
                if (configuredValue == null)
                {
                    var adminMethod = configurationType.GetMethod("get_adminSteamIds", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                    if (adminMethod != null) configuredValue = adminMethod.Invoke(configuration, null);
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
