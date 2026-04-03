using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Unity.Netcode;
using UnityEngine;

namespace schrader.Server
{
    [JsonConverter(typeof(StringEnumConverter))]
    internal enum ReplayPatternType
    {
        Unknown = 0,
        Move = 1,
        Control = 2,
        Shoot = 3,
        TurnDribble = 4
    }

    internal enum ReplayBehaviorIntent : sbyte
    {
        None = 0,
        AcquirePuck = 1,
        ControlPuck = 2,
        PushForward = 3,
        Shoot = 4,
        Recover = 5
    }

    internal struct ReplayLearningSnapshot
    {
        public bool IsValid { get; set; }
        public bool HasPuckContext { get; set; }
        public float PuckDistanceToBlade { get; set; }
        public float PuckDistanceToPlayer { get; set; }
        public float PuckVelocityMagnitude { get; set; }
        public float PuckVelocityAngle { get; set; }
        public float PlayerVelocityMagnitude { get; set; }
        public float PlayerVelocityAngle { get; set; }
        public float RelativeAnglePlayerToPuck { get; set; }
        public float RelativeAnglePuckToGoal { get; set; }
        public sbyte PuckSide { get; set; }
        public sbyte PuckForward { get; set; }
        public bool NearWall { get; set; }
        public float WallNormalAngle { get; set; }
        public sbyte FsmState { get; set; }
        public float ControlVectorAngle { get; set; }
        public bool HasApproachTarget { get; set; }
        public float ApproachTargetDistance { get; set; }
        public float ApproachTargetAngle { get; set; }
        public sbyte SideSelection { get; set; }
        public ReplayBehaviorIntent BehaviorIntent { get; set; }
        public bool HitPuck { get; set; }
        public bool PuckControlled { get; set; }
        public float ControlDurationSeconds { get; set; }
        public bool MissedContact { get; set; }
        public bool Overshoot { get; set; }
        public bool PuckPassedUnderBlade { get; set; }
        public bool BadAngleContact { get; set; }
        public bool LossOfControl { get; set; }
        public float ContactQuality { get; set; }
        public float ApproachQuality { get; set; }
        public float ControlStability { get; set; }
        public float TimingError { get; set; }
        public bool WallExtractionCandidate { get; set; }
        public bool WallExtractionSuccess { get; set; }
    }

    internal struct RecordedInputFrame
    {
        public int Tick { get; set; }
        public float MoveX { get; set; }
        public float MoveY { get; set; }
        public float StickAngleX { get; set; }
        public float StickAngleY { get; set; }
        public sbyte BladeAngle { get; set; }
        public bool Sprint { get; set; }
        public bool Control { get; set; }
        public bool Slide { get; set; }
        public bool LateralLeft { get; set; }
        public bool LateralRight { get; set; }
        public bool HasPuckStickRelation { get; set; }
        public float PuckDistanceToStick { get; set; }
        public float PuckDirectionToStickAngle { get; set; }
        public sbyte PuckRelativeSide { get; set; }
        public bool HasContext { get; set; }
        public float PuckDistanceToBlade { get; set; }
        public float PuckDistanceToPlayer { get; set; }
        public float PuckVelocityMagnitude { get; set; }
        public float PuckVelocityAngle { get; set; }
        public float PlayerVelocityMagnitude { get; set; }
        public float PlayerVelocityAngle { get; set; }
        public float RelativeAnglePlayerToPuck { get; set; }
        public float RelativeAnglePuckToGoal { get; set; }
        public sbyte PuckSide { get; set; }
        public sbyte PuckForward { get; set; }
        public bool NearWall { get; set; }
        public float WallNormalAngle { get; set; }
        public sbyte FsmState { get; set; }
        public float ControlVectorAngle { get; set; }
        public bool HasApproachTarget { get; set; }
        public float ApproachTargetDistance { get; set; }
        public float ApproachTargetAngle { get; set; }
        public sbyte SideSelection { get; set; }
        public ReplayBehaviorIntent BehaviorIntent { get; set; }
        public bool HitPuck { get; set; }
        public bool PuckControlled { get; set; }
        public float ControlDurationSeconds { get; set; }
        public bool MissedContact { get; set; }
        public bool Overshoot { get; set; }
        public bool PuckPassedUnderBlade { get; set; }
        public bool BadAngleContact { get; set; }
        public bool LossOfControl { get; set; }
        public float ContactQuality { get; set; }
        public float ApproachQuality { get; set; }
        public float ControlStability { get; set; }
        public float TimingError { get; set; }
        public bool WallExtractionCandidate { get; set; }
        public bool WallExtractionSuccess { get; set; }
    }

    internal sealed class ReplaySessionSummary
    {
        public int TotalCapturedFrames { get; set; }
        public int SavedFrameCount { get; set; }
        public float DurationSeconds { get; set; }
        public int ContactCount { get; set; }
        public int ControlledFrameCount { get; set; }
        public int MissCount { get; set; }
        public int OvershootCount { get; set; }
        public int UnderBladeCount { get; set; }
        public int BadAngleCount { get; set; }
        public int LossOfControlCount { get; set; }
        public int WallExtractionCount { get; set; }
        public int WallExtractionSuccessCount { get; set; }
        public int SuccessFrameCount { get; set; }
        public int FailureFrameCount { get; set; }
        public float AverageContactQuality { get; set; }
        public float AverageApproachQuality { get; set; }
        public float AverageControlStability { get; set; }
        public bool ContainsSuccess { get; set; }
        public bool ContainsFailure { get; set; }
    }

    internal sealed class RecordedInputSession
    {
        public string RecordingName { get; set; }
        public ReplayPatternType ReplayType { get; set; }
        public string PlayerId { get; set; }
        public string PlayerName { get; set; }
        public ulong ClientId { get; set; }
        public int TickRate { get; set; }
        public DateTime CreatedUtc { get; set; }
        public List<RecordedInputFrame> Frames { get; set; } = new List<RecordedInputFrame>();
        public string SavedFilePath { get; set; }
        public int FrameCount { get; set; }
        public int CapturedFrameCount { get; set; }
        public float DurationSeconds { get; set; }
        public ReplaySessionSummary Summary { get; set; }
        public string MetadataFilePath { get; set; }
        public string FrameDataFilePath { get; set; }
    }

    internal sealed class ReplaySessionDataFile
    {
        public int Version { get; set; } = 2;
        public string RecordingName { get; set; }
        public ReplayPatternType ReplayType { get; set; }
        public string PlayerId { get; set; }
        public string PlayerName { get; set; }
        public ulong ClientId { get; set; }
        public int TickRate { get; set; }
        public DateTime CreatedUtc { get; set; }
        public int CapturedFrameCount { get; set; }
        public int SavedFrameCount { get; set; }
        public float DurationSeconds { get; set; }
        public ReplaySessionSummary Summary { get; set; }
        public List<RecordedInputFrame> Frames { get; set; } = new List<RecordedInputFrame>();
    }

    internal sealed class ReplaySessionMetadataFile
    {
        public int Version { get; set; } = 2;
        public string RecordingName { get; set; }
        public ReplayPatternType ReplayType { get; set; }
        public string PlayerId { get; set; }
        public string PlayerName { get; set; }
        public ulong ClientId { get; set; }
        public int TickRate { get; set; }
        public DateTime CreatedUtc { get; set; }
        public int FrameCount { get; set; }
        public int CapturedFrameCount { get; set; }
        public float DurationSeconds { get; set; }
        public ReplaySessionSummary Summary { get; set; }
        public string FrameDataFileName { get; set; }
    }

    public static partial class RankedSystem
    {
        private static readonly object inputReplayLock = new object();
        private static readonly Dictionary<string, RecordedInputSession> storedReplaySessions = new Dictionary<string, RecordedInputSession>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> storedReplayOrder = new List<string>();
        private static RecordedInputSession activeRecordingSession;
        private static RecordedInputSession lastRecordedSession;
        private static RecordedInputSession activeReplaySession;
        private static ReplayPatternType activeReplayPatternType;
        private static ReplayPatternType replayFilterType;
        private static float recordingAccumulator;
        private static bool replayActive;
        private static string replayBotId;
        private static string replayPinnedRecordingName;
        private static ulong replayRequesterClientId;
        private static int replayFrameIndex;
        private static int replaySegmentStartFrameIndex;
        private static int replaySegmentEndFrameIndex;
        private static int replaySegmentLoopsRemaining;
        private static float replayAccumulator;
        private static Vector3 replayLastBotPosition = Vector3.zero;
        private static float replayStuckDuration;
        private static DateTime lastReplaySaveLogUtc = DateTime.MinValue;
        private static int replaySavesSinceLastLog;
        private static float replayFallbackUntilTime;
        private static readonly Dictionary<int, AutonomousReplayState> autonomousReplayStates = new Dictionary<int, AutonomousReplayState>();

        private const float ReplayStuckDistanceThreshold = 0.18f;
        private const float ReplayStuckTimeThreshold = 0.85f;
        private const float ReplayFallbackDuration = 0.7f;
        private const float AutonomousRecordingIdleFlushTime = 0.45f;
        private const int AutonomousRecordingMinFrames = 8;
        private const int AutonomousRecordingMaxFrames = 28;
        private const float AutonomousRecordingMoveDeltaThreshold = 0.12f;
        private const float AutonomousRecordingStickDeltaThreshold = 1.6f;
        private const float AutonomousRecordingBladeDeltaThreshold = 1f;
        private const float AutonomousRecordingPuckDeltaThreshold = 0.06f;
        private const float AutonomousRecordingPuckAngleThreshold = 5f;
        private const float AutonomousRecordingQualityDeltaThreshold = 0.08f;
        private const float LearningContactQualityGoodThreshold = 0.72f;
        private const float LearningApproachQualityGoodThreshold = 0.68f;
        private const float LearningControlStabilityGoodThreshold = 0.58f;
        private const float LearningFailureQualityThreshold = 0.36f;
        private const float LearningWallExtractionGoodThreshold = 0.62f;
        private const int ReplayMetadataVersion = 2;
        private const int ReplayFrameBinaryVersion = 1;
        private const int ReplayFrameBinaryMagic = 0x31425052;
        private const byte ReplayFrameFlagSprint = 1 << 0;
        private const byte ReplayFrameFlagControl = 1 << 1;
        private const byte ReplayFrameFlagSlide = 1 << 2;
        private const byte ReplayFrameFlagLateralLeft = 1 << 3;
        private const byte ReplayFrameFlagLateralRight = 1 << 4;
        private const byte ReplayFrameFlagHasPuckStickRelation = 1 << 5;

        private sealed class AutonomousReplayState
        {
            public RecordedInputSession ActiveSession;
            public ReplayPatternType ActivePatternType;
            public int FrameIndex;
            public int SegmentStartFrameIndex;
            public int SegmentEndFrameIndex = -1;
            public int SegmentLoopsRemaining;
            public float Accumulator;
            public Vector3 LastBotPosition = Vector3.zero;
            public float StuckDuration;
            public float FallbackUntilTime;
            public RecordedInputSession RecordingSession;
            public ReplayPatternType RecordingPatternType;
            public float RecordingAccumulator;
            public float LastRecordedFrameTime;
        }

        private static void UpdateInputReplay()
        {
            try { UpdateRecording(); } catch { }
            try { UpdateReplay(); } catch { }
        }

        private static void LoadReplayMemory()
        {
            lock (inputReplayLock)
            {
                storedReplaySessions.Clear();
                storedReplayOrder.Clear();
                lastRecordedSession = null;

                try
                {
                    var directory = GetBotMemoryDirectoryPath();
                    MigrateLegacyReplayJsonFiles(directory);

                    var files = Directory.GetFiles(directory, "replay_*.meta.json");
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    var loadedCount = 0;

                    foreach (var filePath in files)
                    {
                        try
                        {
                            var json = File.ReadAllText(filePath);
                            var metadata = JsonConvert.DeserializeObject<ReplaySessionMetadataFile>(json);
                            var loaded = CreateRecordedInputSession(metadata, directory, filePath);
                            if (loaded == null || GetRecordedFrameCount(loaded) <= 0)
                            {
                                Debug.LogWarning($"[{Constants.MOD_NAME}] BotMemory skipped empty replay file: {Path.GetFileName(filePath)}");
                                continue;
                            }

                            var recordingName = NormalizeReplayName(loaded.RecordingName);
                            if (string.IsNullOrWhiteSpace(recordingName)) continue;

                            loaded.RecordingName = recordingName;
                            loaded.FrameCount = Mathf.Max(loaded.FrameCount, GetRecordedFrameCount(loaded));
                            storedReplaySessions[recordingName] = loaded;
                            if (!storedReplayOrder.Contains(recordingName)) storedReplayOrder.Add(recordingName);
                            lastRecordedSession = loaded;
                            loadedCount++;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[{Constants.MOD_NAME}] BotMemory load failed for {Path.GetFileName(filePath)}: {ex.Message}");
                        }
                    }

                    Debug.Log($"[{Constants.MOD_NAME}] BotMemory scan complete. Directory: {directory}. Files found: {files.Length}. Replays loaded: {loadedCount}.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{Constants.MOD_NAME}] BotMemory load failed: {ex.Message}");
                }
            }
        }

        private static void HandleRecordCommand(object player, ulong clientId, bool start)
        {
            if (start)
            {
                if (!TryResolveReplaySourcePlayer(player, clientId, out var resolvedPlayer, out var playerInput))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Record</color> player input source not found.</size>", clientId);
                    return;
                }

                lock (inputReplayLock)
                {
                    if (activeRecordingSession != null)
                    {
                        SendSystemChatToClient("<size=14><color=#ffcc66>Record</color> a recording is already active.</size>", clientId);
                        return;
                    }

                    activeRecordingSession = new RecordedInputSession
                    {
                        PlayerId = TryGetPlayerId(resolvedPlayer, clientId),
                        PlayerName = TryGetPlayerName(resolvedPlayer) ?? $"Player {clientId}",
                        ClientId = clientId,
                        TickRate = Mathf.Max(1, playerInput.TickRate),
                        CreatedUtc = DateTime.UtcNow
                    };
                    activeRecordingSession.Frames.Add(CaptureRecordedFrame(0, GetReplaySourcePlayerComponent(resolvedPlayer), playerInput));
                    recordingAccumulator = 0f;
                }

                Debug.Log($"[{Constants.MOD_NAME}] Record started for client {clientId} at tick rate {playerInput.TickRate}.");
                SendSystemChatToClient("<size=14><color=#00ff00>Record</color> started.</size>", clientId);
                return;
            }

            RecordedInputSession completedSession;
            lock (inputReplayLock)
            {
                if (activeRecordingSession == null)
                {
                    SendSystemChatToClient("<size=14><color=#ffcc66>Record</color> there is no active recording.</size>", clientId);
                    return;
                }

                completedSession = activeRecordingSession;
                activeRecordingSession = null;
                recordingAccumulator = 0f;
                lastRecordedSession = completedSession;
            }

            if ((completedSession.Frames == null || completedSession.Frames.Count == 0)
                && TryResolveReplaySourcePlayer(player, clientId, out var finalResolvedPlayer, out var finalPlayerInput))
            {
                completedSession.Frames = completedSession.Frames ?? new List<RecordedInputFrame>();
                completedSession.Frames.Add(CaptureRecordedFrame(completedSession.Frames.Count, GetReplaySourcePlayerComponent(finalResolvedPlayer), finalPlayerInput));
            }

            if (TrySaveRecordingToBotMemory(completedSession, out var savedFilePath, out var saveFailureReason))
            {
                SendSystemChatToClient($"<size=14><color=#00ff00>Record</color> stopped with <b>{completedSession.Frames.Count}</b> frames. Saved to <b>{Path.GetFileName(savedFilePath)}</b>.</size>", clientId);
                return;
            }

            SendSystemChatToClient($"<size=14><color=#ff6666>Record</color> stopped with <b>{completedSession.Frames?.Count ?? 0}</b> frames, but BotMemory save failed: {saveFailureReason}.</size>", clientId);
        }

        private static void HandleReplayCommand(object player, ulong clientId, string selector)
        {
            StopReplay(null, 0);

            string pinnedRecordingName = null;
            var requestedType = ReplayPatternType.Unknown;
            string selectionDescription;

            if (TryParseReplayPatternType(selector, out var parsedType))
            {
                requestedType = parsedType;
                selectionDescription = parsedType.ToString();
            }
            else if (!string.IsNullOrWhiteSpace(selector) && !selector.Equals("latest", StringComparison.OrdinalIgnoreCase))
            {
                var session = ResolveReplaySession(selector);
                if (session == null || GetRecordedFrameCount(session) <= 0)
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Replay</color> there is no recorded input to replay.</size>", clientId);
                    return;
                }

                EnsureReplayMetadata(session);
                pinnedRecordingName = session.RecordingName;
                selectionDescription = $"{session.RecordingName} ({session.ReplayType})";
            }
            else
            {
                selectionDescription = "BotMemory behavior library";
            }

            if (!HasReplayPatterns(requestedType, pinnedRecordingName))
            {
                SendSystemChatToClient("<size=14><color=#ff6666>Replay</color> there is no matching behavior recording available.</size>", clientId);
                return;
            }

            if (!TryResolveReplayBot(clientId, out var botId, out var controller))
            {
                SendSystemChatToClient("<size=14><color=#ff6666>Replay</color> no replay bot is available.</size>", clientId);
                return;
            }

            controller.SetReplayMode(true);

            lock (inputReplayLock)
            {
                replayActive = true;
                activeReplaySession = null;
                activeReplayPatternType = ReplayPatternType.Unknown;
                replayFilterType = requestedType;
                replayBotId = botId;
                replayPinnedRecordingName = pinnedRecordingName;
                replayRequesterClientId = clientId;
                replayFrameIndex = 0;
                replaySegmentStartFrameIndex = 0;
                replaySegmentEndFrameIndex = -1;
                replaySegmentLoopsRemaining = 0;
                replayAccumulator = 0f;
                replayStuckDuration = 0f;
                replayFallbackUntilTime = 0f;
            }

            Debug.Log($"[{Constants.MOD_NAME}] Replay behavior mode started using {selectionDescription}.");
            SendSystemChatToClient($"<size=14><color=#00ff00>Replay</color> behavior mode started using <b>{selectionDescription}</b>.</size>", clientId);
        }

        private static void HandleReplayListCommand(ulong clientId)
        {
            List<string> names;
            lock (inputReplayLock)
            {
                names = new List<string>(storedReplayOrder.Count);
                for (var i = 0; i < storedReplayOrder.Count; i++)
                {
                    var recordingName = storedReplayOrder[i];
                    if (storedReplaySessions.TryGetValue(recordingName, out var session) && session != null)
                    {
                        EnsureReplayMetadata(session);
                        names.Add($"{recordingName}:{session.ReplayType}");
                    }
                    else
                    {
                        names.Add(recordingName);
                    }
                }
            }

            if (names.Count == 0)
            {
                SendSystemChatToClient("<size=14><color=#ffcc66>Replay</color> there are no saved recordings.</size>", clientId);
                return;
            }

            SendSystemChatToClient($"<size=14><b>BotMemory</b>: {string.Join(", ", names)}</size>", clientId);
        }

        private static void UpdateRecording()
        {
            RecordedInputSession session;
            lock (inputReplayLock)
            {
                session = activeRecordingSession;
            }

            if (session == null) return;
            if (!TryResolveReplaySourcePlayer(null, session.ClientId, out var resolvedPlayer, out var playerInput))
            {
                lock (inputReplayLock)
                {
                    activeRecordingSession = null;
                    recordingAccumulator = 0f;
                    lastRecordedSession = GetRecordedFrameCount(session) > 0 ? session : lastRecordedSession;
                }

                SendSystemChatToClient("<size=14><color=#ffcc66>Record</color> stopped because the source player is no longer available.</size>", session.ClientId);
                return;
            }

            recordingAccumulator += Time.deltaTime * Mathf.Max(1, session.TickRate);
            while (recordingAccumulator >= 1f)
            {
                recordingAccumulator -= 1f;
                session.Frames.Add(CaptureRecordedFrame(session.Frames.Count, GetReplaySourcePlayerComponent(resolvedPlayer), playerInput));
            }
        }

        private static void UpdateReplay()
        {
            string botId;
            ulong requesterClientId;

            lock (inputReplayLock)
            {
                if (!replayActive) return;
                botId = replayBotId;
                requesterClientId = replayRequesterClientId;
            }

            if (!BotManager.TryGetBotController(botId, out var controller) || controller == null)
            {
                StopReplay("<size=14><color=#ff6666>Replay</color> replay bot is no longer available.</size>", requesterClientId);
                return;
            }

            var desiredPatternType = replayFilterType != ReplayPatternType.Unknown
                ? replayFilterType
                : controller.GetReplayPatternType();

            if (desiredPatternType == ReplayPatternType.Unknown || Time.time < replayFallbackUntilTime)
            {
                DeactivateReplayPattern(controller);
                return;
            }

            if (!TryEnsureReplaySegment(controller, desiredPatternType, out var session, out var frameIndex, out var activePatternType))
            {
                DeactivateReplayPattern(controller);
                return;
            }

            if (!controller.IsReplayModeEnabled())
            {
                controller.SetReplayMode(true);
            }

            replayAccumulator += Time.deltaTime * Mathf.Max(1, session.TickRate);
            while (replayAccumulator >= 1f)
            {
                replayAccumulator -= 1f;

                if (!TryEnsureReplaySegment(controller, desiredPatternType, out session, out frameIndex, out activePatternType))
                {
                    DeactivateReplayPattern(controller);
                    return;
                }

                if (!TryResolveReplayFrameContext(session, controller, activePatternType, ref frameIndex))
                {
                    ActivateReplayFallback(controller);
                    return;
                }

                if (frameIndex < 0 || frameIndex >= session.Frames.Count)
                {
                    ActivateReplayFallback(controller);
                    return;
                }

                var frame = session.Frames[frameIndex];
                controller.ApplyRecordedFrame(frame, activePatternType);
                frameIndex++;

                lock (inputReplayLock)
                {
                    replayFrameIndex = frameIndex;
                }

                UpdateReplayFailsafe(controller, frame);
                if (Time.time < replayFallbackUntilTime)
                {
                    DeactivateReplayPattern(controller);
                    return;
                }
            }
        }

        private static void StopReplay(string message, ulong clientId)
        {
            string botId;
            RecordedInputSession releasedSession;
            lock (inputReplayLock)
            {
                if (!replayActive)
                {
                    if (!string.IsNullOrEmpty(message) && clientId != 0) SendSystemChatToClient(message, clientId);
                    return;
                }

                replayActive = false;
                releasedSession = activeReplaySession;
                activeReplaySession = null;
                activeReplayPatternType = ReplayPatternType.Unknown;
                botId = replayBotId;
                replayBotId = null;
                replayPinnedRecordingName = null;
                replayFilterType = ReplayPatternType.Unknown;
                replayRequesterClientId = 0;
                replayFrameIndex = 0;
                replaySegmentStartFrameIndex = 0;
                replaySegmentEndFrameIndex = -1;
                replaySegmentLoopsRemaining = 0;
                replayAccumulator = 0f;
                replayStuckDuration = 0f;
                replayFallbackUntilTime = 0f;
            }

            ReleaseReplayFrames(releasedSession);

            if (!string.IsNullOrEmpty(botId) && BotManager.TryGetBotController(botId, out var controller) && controller != null)
            {
                controller.SetReplayMode(false);
            }

            if (!string.IsNullOrEmpty(message) && clientId != 0)
            {
                SendSystemChatToClient(message, clientId);
            }
        }

        internal static bool TryRunAutonomousReplay(BotAIController controller)
        {
            if (controller == null || controller.IsReplayModeEnabled())
            {
                return false;
            }

            var state = GetOrCreateAutonomousReplayState(controller);
            var desiredPatternType = controller.GetReplayPatternType();
            if (desiredPatternType == ReplayPatternType.Unknown || Time.time < state.FallbackUntilTime)
            {
                DeactivateAutonomousReplayPattern(state);
                return false;
            }

            if (!TryEnsureAutonomousReplaySegment(state, controller, desiredPatternType, out var session, out var frameIndex, out var activePatternType))
            {
                DeactivateAutonomousReplayPattern(state);
                return false;
            }

            state.Accumulator += Time.deltaTime * Mathf.Max(1, session.TickRate);
            var handledReplay = state.ActiveSession != null;

            while (state.Accumulator >= 1f)
            {
                state.Accumulator -= 1f;

                if (!TryEnsureAutonomousReplaySegment(state, controller, desiredPatternType, out session, out frameIndex, out activePatternType))
                {
                    DeactivateAutonomousReplayPattern(state);
                    return handledReplay;
                }

                if (!TryResolveReplayFrameContext(session, controller, activePatternType, ref frameIndex))
                {
                    ActivateAutonomousReplayFallback(state);
                    return handledReplay;
                }

                if (frameIndex < 0 || frameIndex >= session.Frames.Count)
                {
                    ActivateAutonomousReplayFallback(state);
                    return handledReplay;
                }

                controller.ApplyRecordedFrame(session.Frames[frameIndex], activePatternType);
                state.FrameIndex = frameIndex + 1;
                handledReplay = true;

                UpdateAutonomousReplayFailsafe(state, controller, session.Frames[frameIndex]);
                if (Time.time < state.FallbackUntilTime)
                {
                    return true;
                }
            }

            return handledReplay;
        }

        internal static void EnsureReplayMemoryAvailable()
        {
            lock (inputReplayLock)
            {
                if (storedReplaySessions.Count > 0 || lastRecordedSession != null)
                {
                    return;
                }
            }

            LoadReplayMemory();
        }

        internal static void UpdateAutonomousReplayRecording(BotAIController controller)
        {
            if (controller == null)
            {
                return;
            }

            var state = GetOrCreateAutonomousReplayState(controller);
            if (controller.IsReplayModeEnabled())
            {
                FlushAutonomousRecording(state, force: false);
                return;
            }

            if (!controller.TryGetAutonomousReplayRecordingProfile(out var patternType, out var shouldRecord))
            {
                FlushAutonomousRecording(state, force: false);
                return;
            }

            var player = controller.GetControlledPlayer();
            var playerInput = controller.GetControlledPlayerInput();
            if (player == null || playerInput == null)
            {
                FlushAutonomousRecording(state, force: false);
                return;
            }

            state.RecordingAccumulator += Time.deltaTime * Mathf.Max(1, playerInput.TickRate);
            if (!shouldRecord)
            {
                if (state.RecordingSession != null && Time.time - state.LastRecordedFrameTime >= AutonomousRecordingIdleFlushTime)
                {
                    FlushAutonomousRecording(state, force: false);
                }

                return;
            }

            if (state.RecordingSession == null || state.RecordingPatternType != patternType)
            {
                FlushAutonomousRecording(state, force: false);
                StartAutonomousRecording(state, patternType, player, playerInput);
            }

            while (state.RecordingAccumulator >= 1f)
            {
                state.RecordingAccumulator -= 1f;

                if (state.RecordingSession == null)
                {
                    StartAutonomousRecording(state, patternType, player, playerInput);
                }

                var frame = CaptureRecordedFrame(state.RecordingSession.Frames.Count, player, playerInput);
                if (!ShouldAppendAutonomousRecordedFrame(state.RecordingSession, frame))
                {
                    continue;
                }

                state.RecordingSession.Frames.Add(frame);
                state.LastRecordedFrameTime = Time.time;

                if (state.RecordingSession.Frames.Count >= AutonomousRecordingMaxFrames)
                {
                    FlushAutonomousRecording(state, force: true);
                    break;
                }
            }
        }

        internal static void ReleaseAutonomousReplayState(BotAIController controller)
        {
            if (controller == null)
            {
                return;
            }

            AutonomousReplayState state = null;
            lock (inputReplayLock)
            {
                var controllerKey = controller.GetInstanceID();
                if (autonomousReplayStates.TryGetValue(controllerKey, out state))
                {
                    autonomousReplayStates.Remove(controllerKey);
                }
            }

            if (state != null)
            {
                FlushAutonomousRecording(state, force: true);
            }
        }

        private static bool TryResolveReplaySourcePlayer(object player, ulong clientId, out object resolvedPlayer, out PlayerInput playerInput)
        {
            resolvedPlayer = player;
            playerInput = null;

            if (resolvedPlayer == null && clientId != 0 && TryGetPlayerByClientId(clientId, out var playerByClientId))
            {
                resolvedPlayer = playerByClientId;
            }

            var component = resolvedPlayer as Component;
            if (component == null && resolvedPlayer is GameObject gameObject)
            {
                component = gameObject.transform;
            }

            if (component == null) return false;
            var playerComponent = component.GetComponent<Player>();
            if (playerComponent == null) return false;
            playerInput = playerComponent.PlayerInput;
            return playerInput != null;
        }

        private static AutonomousReplayState GetOrCreateAutonomousReplayState(BotAIController controller)
        {
            lock (inputReplayLock)
            {
                var controllerKey = controller.GetInstanceID();
                if (!autonomousReplayStates.TryGetValue(controllerKey, out var state) || state == null)
                {
                    state = new AutonomousReplayState();
                    autonomousReplayStates[controllerKey] = state;
                }

                return state;
            }
        }

        private static bool TryEnsureAutonomousReplaySegment(AutonomousReplayState state, BotAIController controller, ReplayPatternType desiredType, out RecordedInputSession session, out int frameIndex, out ReplayPatternType activePatternType)
        {
            session = null;
            frameIndex = 0;
            activePatternType = ReplayPatternType.Unknown;

            if (state.ActiveSession != null && state.ActivePatternType == desiredType)
            {
                if (state.FrameIndex <= state.SegmentEndFrameIndex)
                {
                    session = state.ActiveSession;
                    frameIndex = state.FrameIndex;
                    activePatternType = state.ActivePatternType;
                    return true;
                }

                if (state.SegmentLoopsRemaining > 0)
                {
                    state.SegmentLoopsRemaining--;
                    state.FrameIndex = GetLoopRestartFrameIndex(state.SegmentStartFrameIndex, state.SegmentEndFrameIndex);
                    session = state.ActiveSession;
                    frameIndex = state.FrameIndex;
                    activePatternType = state.ActivePatternType;
                    return true;
                }
            }

            return TrySelectAutonomousReplaySegment(state, controller, desiredType, out session, out frameIndex, out activePatternType);
        }

        private static bool TrySelectAutonomousReplaySegment(AutonomousReplayState state, BotAIController controller, ReplayPatternType desiredType, out RecordedInputSession session, out int frameIndex, out ReplayPatternType activePatternType)
        {
            session = null;
            frameIndex = 0;
            activePatternType = ReplayPatternType.Unknown;

            var preferredTypes = GetReplayPatternPreference(desiredType);
            lock (inputReplayLock)
            {
                for (var typeIndex = 0; typeIndex < preferredTypes.Length; typeIndex++)
                {
                    var candidateType = preferredTypes[typeIndex];
                    var candidates = BuildReplayCandidates(candidateType, null);
                    if (candidates.Count == 0)
                    {
                        continue;
                    }

                    var selectedSession = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                    if (selectedSession == null || GetRecordedFrameCount(selectedSession) <= 0)
                    {
                        continue;
                    }

                    if (!EnsureReplayFramesLoaded(selectedSession))
                    {
                        continue;
                    }

                    EnsureReplayMetadata(selectedSession);
                    var segmentLength = GetReplaySegmentLength(candidateType, selectedSession.Frames.Count);
                    if (!TryFindReplaySegmentStart(selectedSession, controller, candidateType, segmentLength, out var startIndex, out var endIndex))
                    {
                        continue;
                    }

                    state.ActiveSession = selectedSession;
                    state.ActivePatternType = candidateType;
                    state.SegmentStartFrameIndex = startIndex;
                    state.SegmentEndFrameIndex = endIndex;
                    state.SegmentLoopsRemaining = GetReplaySegmentLoopCount(candidateType);
                    state.FrameIndex = startIndex;
                    state.LastBotPosition = controller.GetRuntimeBotPosition();
                    state.StuckDuration = 0f;
                    state.Accumulator = Mathf.Max(state.Accumulator, 1f);

                    session = state.ActiveSession;
                    frameIndex = state.FrameIndex;
                    activePatternType = state.ActivePatternType;
                    return true;
                }
            }

            return false;
        }

        private static void UpdateAutonomousReplayFailsafe(AutonomousReplayState state, BotAIController controller, RecordedInputFrame frame)
        {
            var currentPosition = controller.GetRuntimeBotPosition();
            var traveledDistance = Vector3.Distance(state.LastBotPosition, currentPosition);
            var moveMagnitude = Mathf.Clamp01(new Vector2(frame.MoveX, frame.MoveY).magnitude);

            if (moveMagnitude >= 0.45f && controller.IsReplayStuck(moveMagnitude) && traveledDistance <= ReplayStuckDistanceThreshold)
            {
                state.StuckDuration += Time.deltaTime;
            }
            else
            {
                state.StuckDuration = 0f;
            }

            state.LastBotPosition = currentPosition;
            if (state.StuckDuration >= ReplayStuckTimeThreshold)
            {
                ActivateAutonomousReplayFallback(state);
            }
        }

        private static void ActivateAutonomousReplayFallback(AutonomousReplayState state)
        {
            state.FallbackUntilTime = Time.time + ReplayFallbackDuration;
            state.StuckDuration = 0f;
            DeactivateAutonomousReplayPattern(state);
        }

        private static void DeactivateAutonomousReplayPattern(AutonomousReplayState state)
        {
            if (state == null)
            {
                return;
            }

            state.ActiveSession = null;
            state.ActivePatternType = ReplayPatternType.Unknown;
            state.SegmentStartFrameIndex = 0;
            state.SegmentEndFrameIndex = -1;
            state.SegmentLoopsRemaining = 0;
            state.FrameIndex = 0;
            state.Accumulator = 0f;
            state.StuckDuration = 0f;
        }

        private static void StartAutonomousRecording(AutonomousReplayState state, ReplayPatternType patternType, Player player, PlayerInput playerInput)
        {
            if (state == null || player == null || playerInput == null)
            {
                return;
            }

            var clientId = ((NetworkBehaviour)player).OwnerClientId;
            state.RecordingSession = new RecordedInputSession
            {
                ReplayType = patternType,
                PlayerId = TryGetPlayerId(player, clientId),
                PlayerName = TryGetPlayerName(player) ?? $"Bot {clientId}",
                ClientId = clientId,
                TickRate = Mathf.Max(1, playerInput.TickRate),
                CreatedUtc = DateTime.UtcNow,
                Frames = new List<RecordedInputFrame>()
            };
            state.RecordingPatternType = patternType;
            state.LastRecordedFrameTime = Time.time;
            state.RecordingAccumulator = Mathf.Max(state.RecordingAccumulator, 1f);
        }

        private static void FlushAutonomousRecording(AutonomousReplayState state, bool force)
        {
            if (state == null)
            {
                return;
            }

            var session = state.RecordingSession;
            state.RecordingSession = null;
            state.RecordingPatternType = ReplayPatternType.Unknown;
            state.RecordingAccumulator = 0f;
            state.LastRecordedFrameTime = 0f;

            if (session == null || session.Frames == null)
            {
                return;
            }

            if (session.Frames.Count < AutonomousRecordingMinFrames)
            {
                return;
            }

            session.FrameCount = session.Frames.Count;
            TrySaveRecordingToBotMemory(session, out _, out _);
        }

        private static bool ShouldAppendAutonomousRecordedFrame(RecordedInputSession session, RecordedInputFrame frame)
        {
            if (session?.Frames == null || session.Frames.Count == 0)
            {
                return true;
            }

            var previous = session.Frames[session.Frames.Count - 1];
            var moveDelta = Mathf.Abs(previous.MoveX - frame.MoveX) + Mathf.Abs(previous.MoveY - frame.MoveY);
            var stickDelta = Mathf.Abs(Mathf.DeltaAngle(previous.StickAngleX, frame.StickAngleX))
                + Mathf.Abs(Mathf.DeltaAngle(previous.StickAngleY, frame.StickAngleY));
            var bladeDelta = Mathf.Abs(previous.BladeAngle - frame.BladeAngle);
            var puckDistanceDelta = Mathf.Abs(previous.PuckDistanceToStick - frame.PuckDistanceToStick);
            var puckAngleDelta = Mathf.Abs(Mathf.DeltaAngle(previous.PuckDirectionToStickAngle, frame.PuckDirectionToStickAngle));
            var qualityDelta = Mathf.Abs(previous.ContactQuality - frame.ContactQuality)
                + Mathf.Abs(previous.ApproachQuality - frame.ApproachQuality)
                + Mathf.Abs(previous.ControlStability - frame.ControlStability);
            var inputFlagsChanged = previous.Sprint != frame.Sprint
                || previous.Control != frame.Control
                || previous.Slide != frame.Slide
                || previous.LateralLeft != frame.LateralLeft
                || previous.LateralRight != frame.LateralRight
                || previous.PuckRelativeSide != frame.PuckRelativeSide
                || previous.HasPuckStickRelation != frame.HasPuckStickRelation
                || previous.FsmState != frame.FsmState
                || previous.BehaviorIntent != frame.BehaviorIntent
                || previous.HitPuck != frame.HitPuck
                || previous.PuckControlled != frame.PuckControlled
                || previous.MissedContact != frame.MissedContact
                || previous.Overshoot != frame.Overshoot
                || previous.PuckPassedUnderBlade != frame.PuckPassedUnderBlade
                || previous.BadAngleContact != frame.BadAngleContact
                || previous.LossOfControl != frame.LossOfControl
                || previous.NearWall != frame.NearWall
                || previous.WallExtractionSuccess != frame.WallExtractionSuccess;

            return inputFlagsChanged
                || moveDelta >= AutonomousRecordingMoveDeltaThreshold
                || stickDelta >= AutonomousRecordingStickDeltaThreshold
                || bladeDelta >= AutonomousRecordingBladeDeltaThreshold
                || puckDistanceDelta >= AutonomousRecordingPuckDeltaThreshold
                || puckAngleDelta >= AutonomousRecordingPuckAngleThreshold
                || qualityDelta >= AutonomousRecordingQualityDeltaThreshold;
        }

        private static bool TryResolveReplayBot(ulong clientId, out string botId, out BotAIController controller)
        {
            botId = null;
            controller = null;

            lock (inputReplayLock)
            {
                if (!string.IsNullOrEmpty(replayBotId) && BotManager.TryGetBotController(replayBotId, out controller) && controller != null)
                {
                    botId = replayBotId;
                    return true;
                }
            }

            if (BotManager.TryGetAnyBotController(out botId, out controller) && controller != null)
            {
                return true;
            }

            TeamResult preferredTeam = TeamResult.Unknown;
            if (TryGetPlayerByClientId(clientId, out var player) && TryGetPlayerTeam(player, out var team))
            {
                preferredTeam = team;
            }

            var participant = BotManager.SpawnBot(preferredTeam, "ReplayBot");
            if (participant == null) return false;

            if (!BotManager.TryGetBotController(participant.playerId, out controller) || controller == null)
            {
                return false;
            }

            botId = participant.playerId;
            return true;
        }

        private static RecordedInputFrame CaptureRecordedFrame(int tick, Player player, PlayerInput playerInput)
        {
            var moveInput = playerInput.MoveInput.ServerValue;
            var stickAngles = playerInput.StickRaycastOriginAngleInput.ServerValue;

            var frame = new RecordedInputFrame
            {
                Tick = tick,
                MoveX = moveInput.x,
                MoveY = moveInput.y,
                StickAngleX = stickAngles.x,
                StickAngleY = stickAngles.y,
                BladeAngle = playerInput.BladeAngleInput.ServerValue,
                Sprint = playerInput.SprintInput.ServerValue,
                Control = playerInput.StopInput.ServerValue,
                Slide = playerInput.SlideInput.ServerValue,
                LateralLeft = playerInput.LateralLeftInput.ServerValue,
                LateralRight = playerInput.LateralRightInput.ServerValue
            };

            PopulateRecordedPuckStickRelation(player, ref frame);
            PopulateRecordedLearningTelemetry(player, ref frame);
            return frame;
        }

        private static void PopulateRecordedLearningTelemetry(Player player, ref RecordedInputFrame frame)
        {
            frame.HasContext = false;
            frame.PuckDistanceToBlade = 0f;
            frame.PuckDistanceToPlayer = 0f;
            frame.PuckVelocityMagnitude = 0f;
            frame.PuckVelocityAngle = 0f;
            frame.PlayerVelocityMagnitude = 0f;
            frame.PlayerVelocityAngle = 0f;
            frame.RelativeAnglePlayerToPuck = 0f;
            frame.RelativeAnglePuckToGoal = 0f;
            frame.PuckSide = 0;
            frame.PuckForward = 0;
            frame.NearWall = false;
            frame.WallNormalAngle = 0f;
            frame.FsmState = 0;
            frame.ControlVectorAngle = 0f;
            frame.HasApproachTarget = false;
            frame.ApproachTargetDistance = 0f;
            frame.ApproachTargetAngle = 0f;
            frame.SideSelection = 0;
            frame.BehaviorIntent = ReplayBehaviorIntent.None;
            frame.HitPuck = false;
            frame.PuckControlled = false;
            frame.ControlDurationSeconds = 0f;
            frame.MissedContact = false;
            frame.Overshoot = false;
            frame.PuckPassedUnderBlade = false;
            frame.BadAngleContact = false;
            frame.LossOfControl = false;
            frame.ContactQuality = 0f;
            frame.ApproachQuality = 0f;
            frame.ControlStability = 0f;
            frame.TimingError = 0f;
            frame.WallExtractionCandidate = false;
            frame.WallExtractionSuccess = false;

            var controller = GetReplaySourceBotController(player);
            if (controller == null || !controller.TryBuildReplayLearningSnapshot(out var snapshot) || !snapshot.IsValid)
            {
                return;
            }

            frame.HasContext = snapshot.HasPuckContext;
            frame.PuckDistanceToBlade = snapshot.PuckDistanceToBlade;
            frame.PuckDistanceToPlayer = snapshot.PuckDistanceToPlayer;
            frame.PuckVelocityMagnitude = snapshot.PuckVelocityMagnitude;
            frame.PuckVelocityAngle = snapshot.PuckVelocityAngle;
            frame.PlayerVelocityMagnitude = snapshot.PlayerVelocityMagnitude;
            frame.PlayerVelocityAngle = snapshot.PlayerVelocityAngle;
            frame.RelativeAnglePlayerToPuck = snapshot.RelativeAnglePlayerToPuck;
            frame.RelativeAnglePuckToGoal = snapshot.RelativeAnglePuckToGoal;
            frame.PuckSide = snapshot.PuckSide;
            frame.PuckForward = snapshot.PuckForward;
            frame.NearWall = snapshot.NearWall;
            frame.WallNormalAngle = snapshot.WallNormalAngle;
            frame.FsmState = snapshot.FsmState;
            frame.ControlVectorAngle = snapshot.ControlVectorAngle;
            frame.HasApproachTarget = snapshot.HasApproachTarget;
            frame.ApproachTargetDistance = snapshot.ApproachTargetDistance;
            frame.ApproachTargetAngle = snapshot.ApproachTargetAngle;
            frame.SideSelection = snapshot.SideSelection;
            frame.BehaviorIntent = snapshot.BehaviorIntent;
            frame.HitPuck = snapshot.HitPuck;
            frame.PuckControlled = snapshot.PuckControlled;
            frame.ControlDurationSeconds = snapshot.ControlDurationSeconds;
            frame.MissedContact = snapshot.MissedContact;
            frame.Overshoot = snapshot.Overshoot;
            frame.PuckPassedUnderBlade = snapshot.PuckPassedUnderBlade;
            frame.BadAngleContact = snapshot.BadAngleContact;
            frame.LossOfControl = snapshot.LossOfControl;
            frame.ContactQuality = snapshot.ContactQuality;
            frame.ApproachQuality = snapshot.ApproachQuality;
            frame.ControlStability = snapshot.ControlStability;
            frame.TimingError = snapshot.TimingError;
            frame.WallExtractionCandidate = snapshot.WallExtractionCandidate;
            frame.WallExtractionSuccess = snapshot.WallExtractionSuccess;
        }

        private static Player GetReplaySourcePlayerComponent(object resolvedPlayer)
        {
            if (resolvedPlayer is Player directPlayer) return directPlayer;

            var component = resolvedPlayer as Component;
            if (component == null && resolvedPlayer is GameObject gameObject)
            {
                component = gameObject.transform;
            }

            return component != null ? component.GetComponent<Player>() : null;
        }

        private static BotAIController GetReplaySourceBotController(Player player)
        {
            return player != null ? player.GetComponent<BotAIController>() : null;
        }

        private static void PopulateRecordedPuckStickRelation(Player player, ref RecordedInputFrame frame)
        {
            frame.HasPuckStickRelation = false;
            frame.PuckDistanceToStick = 0f;
            frame.PuckDirectionToStickAngle = 0f;
            frame.PuckRelativeSide = 0;

            if (player == null) return;

            var puckManager = NetworkBehaviourSingleton<PuckManager>.Instance;
            if (puckManager == null) return;

            var stickPositioner = player.StickPositioner;
            if (stickPositioner == null) return;

            var puck = GetClosestReplayPuck(player, puckManager);
            if (puck == null) return;

            var stickOrigin = stickPositioner.RaycastOriginPosition;
            var localPuckOffset = stickPositioner.transform.InverseTransformVector(puck.transform.position - stickOrigin);
            var flatLocalPuckOffset = new Vector2(localPuckOffset.x, localPuckOffset.z);

            frame.HasPuckStickRelation = true;
            frame.PuckDistanceToStick = flatLocalPuckOffset.magnitude;
            frame.PuckDirectionToStickAngle = flatLocalPuckOffset.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(flatLocalPuckOffset.x, flatLocalPuckOffset.y) * Mathf.Rad2Deg
                : 0f;
            frame.PuckRelativeSide = flatLocalPuckOffset.x < -0.05f ? (sbyte)(-1) : flatLocalPuckOffset.x > 0.05f ? (sbyte)1 : (sbyte)0;
        }

        private static Puck GetClosestReplayPuck(Player player, PuckManager puckManager)
        {
            if (player == null || puckManager == null) return null;

            var ownerClientId = ((NetworkBehaviour)player).OwnerClientId;
            var ownedPuck = puckManager.GetPlayerPuck(ownerClientId);
            if (ownedPuck != null) return ownedPuck;

            var pucks = puckManager.GetPucks(false);
            if (pucks == null || pucks.Count == 0)
            {
                return puckManager.GetPuck(false);
            }

            var playerPosition = player.transform.position;
            Puck closestPuck = null;
            var closestDistanceSq = float.MaxValue;

            for (var i = 0; i < pucks.Count; i++)
            {
                var puck = pucks[i];
                if (puck == null) continue;

                var distanceSq = (puck.transform.position - playerPosition).sqrMagnitude;
                if (distanceSq < closestDistanceSq)
                {
                    closestDistanceSq = distanceSq;
                    closestPuck = puck;
                }
            }

            return closestPuck;
        }

        private static RecordedInputSession ResolveReplaySession(string selector)
        {
            lock (inputReplayLock)
            {
                if (storedReplayOrder.Count == 0) return lastRecordedSession;

                if (string.IsNullOrWhiteSpace(selector) || selector.Equals("latest", StringComparison.OrdinalIgnoreCase))
                {
                    var latestName = storedReplayOrder[storedReplayOrder.Count - 1];
                    return storedReplaySessions.TryGetValue(latestName, out var latestSession) ? latestSession : null;
                }

                var normalized = NormalizeReplayName(selector);
                if (storedReplaySessions.TryGetValue(normalized, out var exact)) return exact;

                foreach (var entry in storedReplaySessions)
                {
                    if (entry.Key.Equals(normalized, StringComparison.OrdinalIgnoreCase)) return entry.Value;
                }
            }

            return null;
        }

        private static List<RecordedInputFrame> BuildLearningFramesForPersistence(List<RecordedInputFrame> capturedFrames, int tickRate, out ReplaySessionSummary summary)
        {
            summary = new ReplaySessionSummary();
            var filteredFrames = new List<RecordedInputFrame>();
            if (capturedFrames == null || capturedFrames.Count == 0)
            {
                return filteredFrames;
            }

            var annotatedFrames = new List<RecordedInputFrame>(capturedFrames.Count);
            var importantIndices = new HashSet<int>();
            var controlDuration = 0f;
            var previousControlled = false;
            var repeatedFailureCount = 0;
            var tickDuration = 1f / Mathf.Max(1, tickRate);
            var metricFrameCount = 0;
            var totalContactQuality = 0f;
            var totalApproachQuality = 0f;
            var totalControlStability = 0f;

            summary.TotalCapturedFrames = capturedFrames.Count;
            summary.DurationSeconds = capturedFrames.Count * tickDuration;

            for (var index = 0; index < capturedFrames.Count; index++)
            {
                var frame = capturedFrames[index];
                if (frame.PuckControlled)
                {
                    controlDuration += tickDuration;
                }
                else
                {
                    controlDuration = 0f;
                }

                frame.ControlDurationSeconds = Mathf.Max(frame.ControlDurationSeconds, controlDuration);
                frame.ControlStability = Mathf.Max(frame.ControlStability, Mathf.Clamp01(controlDuration / Mathf.Max(0.45f, tickDuration)));
                frame.LossOfControl = frame.LossOfControl || (previousControlled && !frame.PuckControlled);
                previousControlled = frame.PuckControlled;

                var goodControl = frame.PuckControlled
                    && frame.ContactQuality >= LearningContactQualityGoodThreshold
                    && frame.ApproachQuality >= LearningApproachQualityGoodThreshold;
                var stableDribble = frame.PuckControlled && frame.ControlStability >= LearningControlStabilityGoodThreshold;
                var goodWallExtraction = frame.NearWall
                    && (frame.WallExtractionSuccess || (frame.PuckControlled && frame.ContactQuality >= LearningWallExtractionGoodThreshold));
                var clearFailure = frame.MissedContact
                    || frame.Overshoot
                    || frame.PuckPassedUnderBlade
                    || frame.BadAngleContact
                    || frame.LossOfControl;

                repeatedFailureCount = clearFailure ? repeatedFailureCount + 1 : 0;
                if (repeatedFailureCount >= 2)
                {
                    clearFailure = true;
                }

                if (frame.HasContext)
                {
                    totalContactQuality += frame.ContactQuality;
                    totalApproachQuality += frame.ApproachQuality;
                    totalControlStability += frame.ControlStability;
                    metricFrameCount++;
                }

                if (frame.HitPuck) summary.ContactCount++;
                if (frame.PuckControlled) summary.ControlledFrameCount++;
                if (frame.MissedContact) summary.MissCount++;
                if (frame.Overshoot) summary.OvershootCount++;
                if (frame.PuckPassedUnderBlade) summary.UnderBladeCount++;
                if (frame.BadAngleContact) summary.BadAngleCount++;
                if (frame.LossOfControl) summary.LossOfControlCount++;
                if (frame.NearWall) summary.WallExtractionCount++;
                if (goodWallExtraction) summary.WallExtractionSuccessCount++;
                if (goodControl || stableDribble || goodWallExtraction) summary.SuccessFrameCount++;
                if (clearFailure || frame.ApproachQuality <= LearningFailureQualityThreshold) summary.FailureFrameCount++;

                var keepFrame = goodControl
                    || stableDribble
                    || goodWallExtraction
                    || clearFailure
                    || (frame.HitPuck && frame.ContactQuality >= LearningContactQualityGoodThreshold)
                    || (frame.HasApproachTarget && (frame.ApproachQuality >= LearningApproachQualityGoodThreshold || frame.ApproachQuality <= LearningFailureQualityThreshold))
                    || (frame.BehaviorIntent == ReplayBehaviorIntent.Shoot && frame.ContactQuality >= 0.55f)
                    || (frame.NearWall && (frame.ContactQuality >= 0.52f || clearFailure));

                if (keepFrame)
                {
                    importantIndices.Add(index);
                    if (index > 0) importantIndices.Add(index - 1);
                    if (index + 1 < capturedFrames.Count) importantIndices.Add(index + 1);
                }

                annotatedFrames.Add(frame);
            }

            summary.ContainsSuccess = summary.SuccessFrameCount > 0;
            summary.ContainsFailure = summary.FailureFrameCount > 0;
            summary.AverageContactQuality = metricFrameCount > 0 ? totalContactQuality / metricFrameCount : 0f;
            summary.AverageApproachQuality = metricFrameCount > 0 ? totalApproachQuality / metricFrameCount : 0f;
            summary.AverageControlStability = metricFrameCount > 0 ? totalControlStability / metricFrameCount : 0f;

            if (importantIndices.Count == 0)
            {
                return filteredFrames;
            }

            for (var index = 0; index < annotatedFrames.Count; index++)
            {
                if (importantIndices.Contains(index))
                {
                    filteredFrames.Add(annotatedFrames[index]);
                }
            }

            summary.SavedFrameCount = filteredFrames.Count;
            return filteredFrames;
        }

        private static bool ShouldPersistLearningSession(ReplaySessionSummary summary)
        {
            return summary != null && (summary.ContainsSuccess || summary.ContainsFailure);
        }

        private static bool TrySaveRecordingToBotMemory(RecordedInputSession session, out string savedFilePath, out string failureReason)
        {
            savedFilePath = null;
            failureReason = null;

            if (session == null)
            {
                failureReason = "session was null";
                Debug.LogWarning($"[{Constants.MOD_NAME}] BotMemory save skipped because the session was null.");
                return false;
            }

            if (session.Frames == null || session.Frames.Count == 0)
            {
                failureReason = "no recorded frames were captured";
                Debug.LogWarning($"[{Constants.MOD_NAME}] BotMemory save skipped for client {session.ClientId} because the recording had no frames.");
                return false;
            }

            try
            {
                var directory = GetBotMemoryDirectoryPath();
                session.RecordingName = NormalizeReplayName($"replay_{DateTime.UtcNow:yyyyMMddHHmmssfff}");
                session.CapturedFrameCount = session.Frames.Count;
                session.DurationSeconds = session.CapturedFrameCount / (float)Mathf.Max(1, session.TickRate);
                session.Frames = BuildLearningFramesForPersistence(session.Frames, session.TickRate, out var summary);
                session.Summary = summary;
                session.FrameCount = session.Frames.Count;

                if (session.FrameCount <= 0)
                {
                    failureReason = "the recording had no meaningful learning frames after filtering";
                    return false;
                }

                if (!ShouldPersistLearningSession(summary))
                {
                    failureReason = "the recording did not contain a successful or failed learning event";
                    return false;
                }

                var filePath = GetReplayFrameDataPath(directory, session.RecordingName);
                var metadataPath = GetReplayMetadataPath(directory, session.RecordingName);
                EnsureReplayMetadata(session);
                WriteReplayFrameDataJson(filePath, session);
                session.FrameDataFilePath = filePath;
                session.MetadataFilePath = metadataPath;
                session.SavedFilePath = filePath;
                WriteReplayMetadata(metadataPath, session);

                if (!File.Exists(filePath) || !File.Exists(metadataPath))
                {
                    failureReason = $"storage files were not created for {session.RecordingName}";
                    Debug.LogError($"[{Constants.MOD_NAME}] BotMemory save failed verification: {failureReason}");
                    return false;
                }

                if (!TryReadReplayStoredFrameCount(filePath, out var frameCount) || frameCount != session.Frames.Count)
                {
                    failureReason = $"saved replay dataset could not be read back from {filePath}";
                    Debug.LogError($"[{Constants.MOD_NAME}] BotMemory save failed verification: {failureReason}");
                    return false;
                }

                savedFilePath = filePath;

                lock (inputReplayLock)
                {
                    if (!string.IsNullOrWhiteSpace(session.RecordingName))
                    {
                        storedReplaySessions[session.RecordingName] = session;
                        storedReplayOrder.RemoveAll(name => name.Equals(session.RecordingName, StringComparison.OrdinalIgnoreCase));
                        storedReplayOrder.Add(session.RecordingName);
                        lastRecordedSession = session;
                    }
                }

                LogReplaySaveSummary(session, filePath);
                return true;
            }
            catch (Exception ex)
            {
                failureReason = ex.Message;
                Debug.LogError($"[{Constants.MOD_NAME}] Input replay save failed: {ex.Message}");
                return false;
            }
        }

        private static void LogReplaySaveSummary(RecordedInputSession session, string filePath)
        {
            replaySavesSinceLastLog++;

            var now = DateTime.UtcNow;
            if ((now - lastReplaySaveLogUtc).TotalMinutes < 1)
            {
                return;
            }

            lastReplaySaveLogUtc = now;
            Debug.Log($"[{Constants.MOD_NAME}] BotMemory replays saved in the last minute: {replaySavesSinceLastLog}. Latest={session?.RecordingName ?? "unknown"} Frames={session?.Frames?.Count ?? 0} Path={filePath}");
            replaySavesSinceLastLog = 0;
        }

        private static void EnsureReplayMetadata(RecordedInputSession session)
        {
            if (session == null) return;
            if (session.ReplayType != ReplayPatternType.Unknown) return;
            if (!EnsureReplayFramesLoaded(session)) return;
            session.ReplayType = ClassifyReplayPatternType(session);
            if (!string.IsNullOrWhiteSpace(session.MetadataFilePath))
            {
                TryWriteReplayMetadata(session.MetadataFilePath, session);
            }
        }

        private static ReplayPatternType ClassifyReplayPatternType(RecordedInputSession session)
        {
            if (session == null || session.Frames == null || session.Frames.Count == 0)
            {
                return ReplayPatternType.Unknown;
            }

            var frameCount = Mathf.Max(1, session.Frames.Count);
            var totalMoveMagnitude = 0f;
            var totalForwardMagnitude = 0f;
            var totalTurnMagnitude = 0f;
            var totalStickDelta = 0f;
            var totalBladeActivity = 0f;
            var sprintFrames = 0;
            var controlFrames = 0;
            var slideFrames = 0;
            var lateralFrames = 0;

            for (var i = 0; i < session.Frames.Count; i++)
            {
                var frame = session.Frames[i];
                totalMoveMagnitude += Mathf.Clamp01(new Vector2(frame.MoveX, frame.MoveY).magnitude);
                totalForwardMagnitude += Mathf.Abs(frame.MoveY);
                totalTurnMagnitude += Mathf.Abs(frame.MoveX);
                totalBladeActivity += Mathf.Abs(frame.BladeAngle) / 127f;
                if (frame.Sprint) sprintFrames++;
                if (frame.Control) controlFrames++;
                if (frame.Slide) slideFrames++;
                if (frame.LateralLeft || frame.LateralRight) lateralFrames++;

                if (i <= 0) continue;

                var previous = session.Frames[i - 1];
                totalStickDelta += Vector2.Distance(
                    new Vector2(previous.StickAngleX, previous.StickAngleY),
                    new Vector2(frame.StickAngleX, frame.StickAngleY));
            }

            var averageMoveMagnitude = totalMoveMagnitude / frameCount;
            var averageForwardMagnitude = totalForwardMagnitude / frameCount;
            var averageTurnMagnitude = totalTurnMagnitude / frameCount;
            var averageStickDelta = totalStickDelta / frameCount;
            var averageBladeActivity = totalBladeActivity / frameCount;
            var sprintRatio = sprintFrames / (float)frameCount;
            var controlRatio = controlFrames / (float)frameCount;
            var slideRatio = slideFrames / (float)frameCount;
            var lateralRatio = lateralFrames / (float)frameCount;

            if (averageForwardMagnitude >= 0.55f && sprintRatio >= 0.2f && averageBladeActivity >= 0.15f && averageTurnMagnitude <= 0.4f)
            {
                return ReplayPatternType.Shoot;
            }

            if (averageTurnMagnitude >= 0.42f && (averageStickDelta >= 3.5f || lateralRatio >= 0.18f || slideRatio >= 0.08f))
            {
                return ReplayPatternType.TurnDribble;
            }

            if (controlRatio >= 0.16f || (averageMoveMagnitude <= 0.58f && averageStickDelta >= 2.25f) || averageBladeActivity >= 0.22f)
            {
                return ReplayPatternType.Control;
            }

            return ReplayPatternType.Move;
        }

        private static bool TryParseReplayPatternType(string selector, out ReplayPatternType patternType)
        {
            patternType = ReplayPatternType.Unknown;
            if (string.IsNullOrWhiteSpace(selector)) return false;

            switch (selector.Trim().ToLowerInvariant())
            {
                case "move":
                    patternType = ReplayPatternType.Move;
                    return true;
                case "control":
                    patternType = ReplayPatternType.Control;
                    return true;
                case "shoot":
                    patternType = ReplayPatternType.Shoot;
                    return true;
                case "turn":
                case "dribble":
                case "turndribble":
                case "turn/dribble":
                    patternType = ReplayPatternType.TurnDribble;
                    return true;
                default:
                    return false;
            }
        }

        private static bool HasReplayPatterns(ReplayPatternType requestedType, string pinnedRecordingName)
        {
            lock (inputReplayLock)
            {
                return BuildReplayCandidates(requestedType, pinnedRecordingName).Count > 0;
            }
        }

        private static List<RecordedInputSession> BuildReplayCandidates(ReplayPatternType requestedType, string pinnedRecordingName)
        {
            var candidates = new List<RecordedInputSession>();

            if (!string.IsNullOrWhiteSpace(pinnedRecordingName))
            {
                if (storedReplaySessions.TryGetValue(pinnedRecordingName, out var pinnedSession) && pinnedSession != null)
                {
                    EnsureReplayMetadata(pinnedSession);
                    if (requestedType == ReplayPatternType.Unknown || pinnedSession.ReplayType == requestedType)
                    {
                        candidates.Add(pinnedSession);
                    }
                }

                return candidates;
            }

            for (var i = 0; i < storedReplayOrder.Count; i++)
            {
                var recordingName = storedReplayOrder[i];
                if (!storedReplaySessions.TryGetValue(recordingName, out var session) || session == null) continue;
                EnsureReplayMetadata(session);
                if (requestedType != ReplayPatternType.Unknown && session.ReplayType != requestedType) continue;
                candidates.Add(session);
            }

            if (candidates.Count == 0 && lastRecordedSession != null)
            {
                EnsureReplayMetadata(lastRecordedSession);
                if (requestedType == ReplayPatternType.Unknown || lastRecordedSession.ReplayType == requestedType)
                {
                    candidates.Add(lastRecordedSession);
                }
            }

            return candidates;
        }

        private static bool TryEnsureReplaySegment(BotAIController controller, ReplayPatternType desiredType, out RecordedInputSession session, out int frameIndex, out ReplayPatternType activePatternType)
        {
            session = null;
            frameIndex = 0;
            activePatternType = ReplayPatternType.Unknown;

            lock (inputReplayLock)
            {
                if (activeReplaySession != null && activeReplayPatternType == desiredType)
                {
                    if (replayFrameIndex <= replaySegmentEndFrameIndex)
                    {
                        session = activeReplaySession;
                        frameIndex = replayFrameIndex;
                        activePatternType = activeReplayPatternType;
                        return true;
                    }

                    if (replaySegmentLoopsRemaining > 0)
                    {
                        replaySegmentLoopsRemaining--;
                        replayFrameIndex = GetLoopRestartFrameIndex(replaySegmentStartFrameIndex, replaySegmentEndFrameIndex);
                        session = activeReplaySession;
                        frameIndex = replayFrameIndex;
                        activePatternType = activeReplayPatternType;
                        return true;
                    }
                }
            }

            return TrySelectReplaySegment(controller, desiredType, out session, out frameIndex, out activePatternType);
        }

        private static bool TrySelectReplaySegment(BotAIController controller, ReplayPatternType desiredType, out RecordedInputSession session, out int frameIndex, out ReplayPatternType activePatternType)
        {
            session = null;
            frameIndex = 0;
            activePatternType = ReplayPatternType.Unknown;

            var preferredTypes = GetReplayPatternPreference(desiredType);
            lock (inputReplayLock)
            {
                for (var typeIndex = 0; typeIndex < preferredTypes.Length; typeIndex++)
                {
                    var candidateType = preferredTypes[typeIndex];
                    var candidates = BuildReplayCandidates(candidateType, replayPinnedRecordingName);
                    if (candidates.Count == 0) continue;

                    var selectedSession = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                    if (selectedSession == null || GetRecordedFrameCount(selectedSession) <= 0) continue;
                    if (!EnsureReplayFramesLoaded(selectedSession)) continue;

                    EnsureReplayMetadata(selectedSession);
                    var segmentLength = GetReplaySegmentLength(candidateType, selectedSession.Frames.Count);
                    if (!TryFindReplaySegmentStart(selectedSession, controller, candidateType, segmentLength, out var startIndex, out var endIndex))
                    {
                        continue;
                    }

                    var previousSession = activeReplaySession;
                    activeReplaySession = selectedSession;
                    activeReplayPatternType = candidateType;
                    replaySegmentStartFrameIndex = startIndex;
                    replaySegmentEndFrameIndex = endIndex;
                    replaySegmentLoopsRemaining = GetReplaySegmentLoopCount(candidateType);
                    replayFrameIndex = startIndex;
                    replayLastBotPosition = controller.GetRuntimeBotPosition();
                    replayStuckDuration = 0f;

                    if (!ReferenceEquals(previousSession, selectedSession))
                    {
                        ReleaseReplayFrames(previousSession);
                    }

                    session = selectedSession;
                    frameIndex = replayFrameIndex;
                    activePatternType = activeReplayPatternType;
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveReplayFrameContext(RecordedInputSession session, BotAIController controller, ReplayPatternType patternType, ref int frameIndex)
        {
            if (session == null || session.Frames == null || controller == null) return false;

            var maxSkip = patternType == ReplayPatternType.TurnDribble ? 2 : 4;
            for (var offset = 0; offset <= maxSkip; offset++)
            {
                var candidateIndex = frameIndex + offset;
                if (candidateIndex < 0 || candidateIndex >= session.Frames.Count) break;
                if (candidateIndex > replaySegmentEndFrameIndex) break;

                if (controller.IsReplayFrameContextMatch(session.Frames[candidateIndex], patternType))
                {
                    frameIndex = candidateIndex;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindReplaySegmentStart(RecordedInputSession session, BotAIController controller, ReplayPatternType patternType, int segmentLength, out int startIndex, out int endIndex)
        {
            startIndex = 0;
            endIndex = -1;

            if (session == null || session.Frames == null || session.Frames.Count == 0 || controller == null)
            {
                return false;
            }

            var maxStartIndex = Mathf.Max(0, session.Frames.Count - segmentLength);
            var attempts = Mathf.Min(maxStartIndex + 1, 12);
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var candidateStartIndex = maxStartIndex > 0 ? UnityEngine.Random.Range(0, maxStartIndex + 1) : 0;
                if (!controller.IsReplayFrameContextMatch(session.Frames[candidateStartIndex], patternType))
                {
                    continue;
                }

                startIndex = candidateStartIndex;
                endIndex = Mathf.Min(session.Frames.Count - 1, startIndex + segmentLength - 1);
                return true;
            }

            return false;
        }

        private static ReplayPatternType[] GetReplayPatternPreference(ReplayPatternType desiredType)
        {
            switch (desiredType)
            {
                case ReplayPatternType.Shoot:
                    return new[] { ReplayPatternType.Shoot, ReplayPatternType.Control, ReplayPatternType.TurnDribble };
                case ReplayPatternType.Control:
                    return new[] { ReplayPatternType.Control, ReplayPatternType.TurnDribble };
                case ReplayPatternType.TurnDribble:
                    return new[] { ReplayPatternType.TurnDribble, ReplayPatternType.Control };
                case ReplayPatternType.Move:
                    return new[] { ReplayPatternType.Move, ReplayPatternType.TurnDribble };
                default:
                    return new[] { ReplayPatternType.Unknown };
            }
        }

        private static int GetReplaySegmentLength(ReplayPatternType patternType, int totalFrames)
        {
            if (totalFrames <= 1) return totalFrames;

            switch (patternType)
            {
                case ReplayPatternType.Shoot:
                    return Mathf.Clamp(UnityEngine.Random.Range(10, 22), 6, totalFrames);
                case ReplayPatternType.Control:
                    return Mathf.Clamp(UnityEngine.Random.Range(14, 32), 8, totalFrames);
                case ReplayPatternType.TurnDribble:
                    return Mathf.Clamp(UnityEngine.Random.Range(12, 26), 8, totalFrames);
                case ReplayPatternType.Move:
                    return Mathf.Clamp(UnityEngine.Random.Range(20, 48), 10, totalFrames);
                default:
                    return Mathf.Clamp(UnityEngine.Random.Range(12, 28), 8, totalFrames);
            }
        }

        private static int GetReplaySegmentLoopCount(ReplayPatternType patternType)
        {
            switch (patternType)
            {
                case ReplayPatternType.Shoot:
                    return UnityEngine.Random.Range(0, 2);
                case ReplayPatternType.Control:
                case ReplayPatternType.TurnDribble:
                    return UnityEngine.Random.Range(1, 3);
                case ReplayPatternType.Move:
                    return UnityEngine.Random.Range(1, 4);
                default:
                    return 0;
            }
        }

        private static int GetLoopRestartFrameIndex(int startFrameIndex, int endFrameIndex)
        {
            if (endFrameIndex <= startFrameIndex) return startFrameIndex;
            var restartWindow = Mathf.Min(3, endFrameIndex - startFrameIndex);
            return startFrameIndex + UnityEngine.Random.Range(0, restartWindow + 1);
        }

        private static void UpdateReplayFailsafe(BotAIController controller, RecordedInputFrame frame)
        {
            var currentPosition = controller.GetRuntimeBotPosition();
            var traveledDistance = Vector3.Distance(replayLastBotPosition, currentPosition);
            var moveMagnitude = Mathf.Clamp01(new Vector2(frame.MoveX, frame.MoveY).magnitude);
            var frameDeltaTime = Time.deltaTime * Mathf.Max(1, activeReplaySession != null ? activeReplaySession.TickRate : 1);

            if (moveMagnitude >= 0.45f && controller.IsReplayStuck(moveMagnitude) && traveledDistance <= ReplayStuckDistanceThreshold)
            {
                replayStuckDuration += Time.deltaTime;
            }
            else
            {
                replayStuckDuration = 0f;
            }

            replayLastBotPosition = currentPosition;

            if (replayStuckDuration < ReplayStuckTimeThreshold) return;

            Debug.LogWarning($"[{Constants.MOD_NAME}] Replay behavior fallback triggered for {activeReplayPatternType} after {replayStuckDuration:F2}s of low progress.");
            ActivateReplayFallback(controller);
        }

        private static void ActivateReplayFallback(BotAIController controller)
        {
            replayFallbackUntilTime = Time.time + ReplayFallbackDuration;
            replayStuckDuration = 0f;
            DeactivateReplayPattern(controller);
        }

        private static void DeactivateReplayPattern(BotAIController controller)
        {
            RecordedInputSession releasedSession;
            lock (inputReplayLock)
            {
                releasedSession = activeReplaySession;
                activeReplaySession = null;
                activeReplayPatternType = ReplayPatternType.Unknown;
                replaySegmentStartFrameIndex = 0;
                replaySegmentEndFrameIndex = -1;
                replaySegmentLoopsRemaining = 0;
                replayFrameIndex = 0;
                replayAccumulator = 0f;
            }

            ReleaseReplayFrames(releasedSession);

            if (controller != null && controller.IsReplayModeEnabled())
            {
                controller.SetReplayMode(false);
            }
        }

        private static int GetRecordedFrameCount(RecordedInputSession session)
        {
            if (session == null)
            {
                return 0;
            }

            if (session.Frames != null && session.Frames.Count > 0)
            {
                return session.Frames.Count;
            }

            return Mathf.Max(0, session.FrameCount);
        }

        private static RecordedInputSession CreateRecordedInputSession(ReplaySessionMetadataFile metadata, string directory, string metadataPath)
        {
            if (metadata == null)
            {
                return null;
            }

            var recordingName = NormalizeReplayName(metadata.RecordingName);
            if (string.IsNullOrWhiteSpace(recordingName))
            {
                var metadataName = Path.GetFileNameWithoutExtension(metadataPath);
                recordingName = NormalizeReplayName(metadataName != null ? metadataName.Replace(".meta", string.Empty) : null);
            }

            if (string.IsNullOrWhiteSpace(recordingName))
            {
                return null;
            }

            var frameDataFileName = metadata.FrameDataFileName;
            if (string.IsNullOrWhiteSpace(frameDataFileName))
            {
                var jsonDataFileName = $"{recordingName}.data.json";
                frameDataFileName = File.Exists(Path.Combine(directory, jsonDataFileName))
                    ? jsonDataFileName
                    : $"{recordingName}.bin";
            }

            var frameDataPath = Path.Combine(directory, frameDataFileName);
            if (!File.Exists(frameDataPath))
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] BotMemory metadata found without frame data: {Path.GetFileName(metadataPath)}");
                return null;
            }

            return new RecordedInputSession
            {
                RecordingName = recordingName,
                ReplayType = metadata.ReplayType,
                PlayerId = metadata.PlayerId,
                PlayerName = metadata.PlayerName,
                ClientId = metadata.ClientId,
                TickRate = Mathf.Max(1, metadata.TickRate),
                CreatedUtc = metadata.CreatedUtc,
                FrameCount = Mathf.Max(0, metadata.FrameCount),
                CapturedFrameCount = Mathf.Max(metadata.CapturedFrameCount, metadata.FrameCount),
                DurationSeconds = metadata.DurationSeconds,
                Summary = metadata.Summary,
                SavedFilePath = frameDataPath,
                MetadataFilePath = metadataPath,
                FrameDataFilePath = frameDataPath,
                Frames = null
            };
        }

        private static string GetReplayFrameDataPath(string directory, string recordingName)
        {
            return Path.Combine(directory, $"{recordingName}.data.json");
        }

        private static string GetReplayMetadataPath(string directory, string recordingName)
        {
            return Path.Combine(directory, $"{recordingName}.meta.json");
        }

        private static void WriteReplayFrameDataJson(string filePath, RecordedInputSession session)
        {
            var data = new ReplaySessionDataFile
            {
                Version = ReplayMetadataVersion,
                RecordingName = session.RecordingName,
                ReplayType = session.ReplayType,
                PlayerId = session.PlayerId,
                PlayerName = session.PlayerName,
                ClientId = session.ClientId,
                TickRate = Mathf.Max(1, session.TickRate),
                CreatedUtc = session.CreatedUtc,
                CapturedFrameCount = Mathf.Max(session.CapturedFrameCount, GetRecordedFrameCount(session)),
                SavedFrameCount = GetRecordedFrameCount(session),
                DurationSeconds = session.DurationSeconds,
                Summary = session.Summary,
                Frames = session.Frames ?? new List<RecordedInputFrame>()
            };

            File.WriteAllText(filePath, JsonConvert.SerializeObject(data, Formatting.Indented));
        }

        private static bool TryReadReplayFrameDataJson(string filePath, out ReplaySessionDataFile data)
        {
            data = null;

            try
            {
                var json = File.ReadAllText(filePath);
                data = JsonConvert.DeserializeObject<ReplaySessionDataFile>(json);
                return data != null && data.Frames != null && data.Frames.Count >= 0;
            }
            catch
            {
                data = null;
                return false;
            }
        }

        private static void WriteReplayFrameDataBinary(string filePath, List<RecordedInputFrame> frames)
        {
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(ReplayFrameBinaryMagic);
                writer.Write(ReplayFrameBinaryVersion);
                writer.Write(frames.Count);

                for (var index = 0; index < frames.Count; index++)
                {
                    var frame = frames[index];
                    var flags = (byte)0;
                    if (frame.Sprint) flags |= ReplayFrameFlagSprint;
                    if (frame.Control) flags |= ReplayFrameFlagControl;
                    if (frame.Slide) flags |= ReplayFrameFlagSlide;
                    if (frame.LateralLeft) flags |= ReplayFrameFlagLateralLeft;
                    if (frame.LateralRight) flags |= ReplayFrameFlagLateralRight;
                    if (frame.HasPuckStickRelation) flags |= ReplayFrameFlagHasPuckStickRelation;

                    writer.Write(frame.MoveX);
                    writer.Write(frame.MoveY);
                    writer.Write(frame.StickAngleX);
                    writer.Write(frame.StickAngleY);
                    writer.Write(frame.BladeAngle);
                    writer.Write(flags);
                    writer.Write(frame.PuckRelativeSide);
                    writer.Write(frame.PuckDistanceToStick);
                    writer.Write(frame.PuckDirectionToStickAngle);
                }
            }
        }

        private static bool TryReadReplayFramesBinary(string filePath, out List<RecordedInputFrame> frames)
        {
            frames = null;

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadInt32() != ReplayFrameBinaryMagic)
                    {
                        return false;
                    }

                    if (reader.ReadInt32() != ReplayFrameBinaryVersion)
                    {
                        return false;
                    }

                    var frameCount = reader.ReadInt32();
                    if (frameCount < 0)
                    {
                        return false;
                    }

                    frames = new List<RecordedInputFrame>(frameCount);
                    for (var index = 0; index < frameCount; index++)
                    {
                        var frame = new RecordedInputFrame
                        {
                            Tick = index,
                            MoveX = reader.ReadSingle(),
                            MoveY = reader.ReadSingle(),
                            StickAngleX = reader.ReadSingle(),
                            StickAngleY = reader.ReadSingle(),
                            BladeAngle = reader.ReadSByte()
                        };

                        var flags = reader.ReadByte();
                        frame.PuckRelativeSide = reader.ReadSByte();
                        frame.PuckDistanceToStick = reader.ReadSingle();
                        frame.PuckDirectionToStickAngle = reader.ReadSingle();
                        frame.Sprint = (flags & ReplayFrameFlagSprint) != 0;
                        frame.Control = (flags & ReplayFrameFlagControl) != 0;
                        frame.Slide = (flags & ReplayFrameFlagSlide) != 0;
                        frame.LateralLeft = (flags & ReplayFrameFlagLateralLeft) != 0;
                        frame.LateralRight = (flags & ReplayFrameFlagLateralRight) != 0;
                        frame.HasPuckStickRelation = (flags & ReplayFrameFlagHasPuckStickRelation) != 0;
                        frames.Add(frame);
                    }

                    return true;
                }
            }
            catch
            {
                frames = null;
                return false;
            }
        }

        private static bool TryReadReplayBinaryFrameCount(string filePath, out int frameCount)
        {
            frameCount = 0;

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadInt32() != ReplayFrameBinaryMagic)
                    {
                        return false;
                    }

                    if (reader.ReadInt32() != ReplayFrameBinaryVersion)
                    {
                        return false;
                    }

                    frameCount = reader.ReadInt32();
                    return frameCount >= 0;
                }
            }
            catch
            {
                frameCount = 0;
                return false;
            }
        }

        private static bool TryReadReplayStoredFrameCount(string filePath, out int frameCount)
        {
            frameCount = 0;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadReplayFrameDataJson(filePath, out var data) || data == null)
                {
                    return false;
                }

                frameCount = data.Frames?.Count ?? 0;
                return frameCount >= 0;
            }

            return TryReadReplayBinaryFrameCount(filePath, out frameCount);
        }

        private static bool EnsureReplayFramesLoaded(RecordedInputSession session)
        {
            if (session == null)
            {
                return false;
            }

            if (session.Frames != null && session.Frames.Count > 0)
            {
                session.FrameCount = session.Frames.Count;
                return true;
            }

            if (string.IsNullOrWhiteSpace(session.FrameDataFilePath) || !File.Exists(session.FrameDataFilePath))
            {
                return false;
            }

            if (session.FrameDataFilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadReplayFrameDataJson(session.FrameDataFilePath, out var data) || data == null || data.Frames == null || data.Frames.Count == 0)
                {
                    Debug.LogError($"[{Constants.MOD_NAME}] BotMemory json load failed for {Path.GetFileName(session.FrameDataFilePath)}.");
                    return false;
                }

                session.Frames = data.Frames;
                session.FrameCount = data.SavedFrameCount > 0 ? data.SavedFrameCount : data.Frames.Count;
                session.CapturedFrameCount = Mathf.Max(data.CapturedFrameCount, session.FrameCount);
                session.DurationSeconds = data.DurationSeconds;
                session.Summary = data.Summary;
                session.SavedFilePath = session.FrameDataFilePath;
                return true;
            }

            if (!TryReadReplayFramesBinary(session.FrameDataFilePath, out var frames) || frames == null || frames.Count == 0)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] BotMemory binary load failed for {Path.GetFileName(session.FrameDataFilePath)}.");
                return false;
            }

            session.Frames = frames;
            session.FrameCount = frames.Count;
            session.CapturedFrameCount = Mathf.Max(session.CapturedFrameCount, session.FrameCount);
            session.SavedFilePath = session.FrameDataFilePath;
            return true;
        }

        private static void ReleaseReplayFrames(RecordedInputSession session)
        {
            if (session == null || session == activeRecordingSession || session == lastRecordedSession)
            {
                return;
            }

            if (session.Frames == null || session.Frames.Count == 0)
            {
                return;
            }

            session.FrameCount = session.Frames.Count;
            session.Frames = null;
        }

        private static void WriteReplayMetadata(string filePath, RecordedInputSession session)
        {
            var metadata = new ReplaySessionMetadataFile
            {
                Version = ReplayMetadataVersion,
                RecordingName = session.RecordingName,
                ReplayType = session.ReplayType,
                PlayerId = session.PlayerId,
                PlayerName = session.PlayerName,
                ClientId = session.ClientId,
                TickRate = Mathf.Max(1, session.TickRate),
                CreatedUtc = session.CreatedUtc,
                FrameCount = GetRecordedFrameCount(session),
                CapturedFrameCount = Mathf.Max(session.CapturedFrameCount, GetRecordedFrameCount(session)),
                DurationSeconds = session.DurationSeconds,
                Summary = session.Summary,
                FrameDataFileName = Path.GetFileName(session.FrameDataFilePath)
            };

            File.WriteAllText(filePath, JsonConvert.SerializeObject(metadata, Formatting.Indented));
        }

        private static void TryWriteReplayMetadata(string filePath, RecordedInputSession session)
        {
            try
            {
                WriteReplayMetadata(filePath, session);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] BotMemory metadata update failed for {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        private static void MigrateLegacyReplayJsonFiles(string directory)
        {
            var legacyFiles = Directory.GetFiles(directory, "replay_*.json");
            Array.Sort(legacyFiles, StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < legacyFiles.Length; index++)
            {
                var legacyFilePath = legacyFiles[index];
                if (legacyFilePath.EndsWith(".meta.json", StringComparison.OrdinalIgnoreCase)
                    || legacyFilePath.EndsWith(".data.json", StringComparison.OrdinalIgnoreCase)
                    || legacyFilePath.EndsWith(".debug.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var json = File.ReadAllText(legacyFilePath);
                    var legacySession = JsonConvert.DeserializeObject<RecordedInputSession>(json);
                    if (legacySession?.Frames == null || legacySession.Frames.Count == 0)
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] BotMemory skipped legacy replay migration for {Path.GetFileName(legacyFilePath)} because it had no frames.");
                        continue;
                    }

                    legacySession.RecordingName = NormalizeReplayName(Path.GetFileNameWithoutExtension(legacyFilePath));
                    legacySession.FrameCount = legacySession.Frames.Count;
                    legacySession.CapturedFrameCount = legacySession.Frames.Count;
                    legacySession.DurationSeconds = legacySession.CapturedFrameCount / (float)Mathf.Max(1, legacySession.TickRate);
                    legacySession.Frames = BuildLearningFramesForPersistence(legacySession.Frames, legacySession.TickRate, out var summary);
                    legacySession.Summary = summary;
                    if (legacySession.Frames.Count == 0 || !ShouldPersistLearningSession(summary))
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] BotMemory skipped legacy replay migration for {Path.GetFileName(legacyFilePath)} because it had no meaningful learning frames.");
                        continue;
                    }

                    legacySession.FrameDataFilePath = GetReplayFrameDataPath(directory, legacySession.RecordingName);
                    legacySession.MetadataFilePath = GetReplayMetadataPath(directory, legacySession.RecordingName);
                    legacySession.SavedFilePath = legacySession.FrameDataFilePath;
                    EnsureReplayMetadata(legacySession);

                    WriteReplayFrameDataJson(legacySession.FrameDataFilePath, legacySession);
                    WriteReplayMetadata(legacySession.MetadataFilePath, legacySession);
                    File.Delete(legacyFilePath);

                    Debug.Log($"[{Constants.MOD_NAME}] BotMemory migrated legacy replay {Path.GetFileName(legacyFilePath)} to {Path.GetFileName(legacySession.FrameDataFilePath)}.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{Constants.MOD_NAME}] BotMemory legacy replay migration failed for {Path.GetFileName(legacyFilePath)}: {ex.Message}");
                }
            }
        }

        private static string GetBotMemoryDirectoryPath()
        {
            var root = GetGameRootPath();
            var userDataDirectory = Path.Combine(root, "UserData");
            Directory.CreateDirectory(userDataDirectory);
            var botMemoryDirectory = Path.Combine(userDataDirectory, "BotMemory");
            Directory.CreateDirectory(botMemoryDirectory);
            return botMemoryDirectory;
        }

        private static string NormalizeReplayName(string replayName)
        {
            if (string.IsNullOrWhiteSpace(replayName)) return null;
            var fileName = Path.GetFileNameWithoutExtension(replayName.Trim());
            return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
        }
    }
}