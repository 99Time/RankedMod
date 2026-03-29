using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Unity.Netcode;
using UnityEngine;

namespace schrader.Server
{
    internal sealed class BotAIController : MonoBehaviour
    {
        private enum ReplayPuckZone
        {
            Far = 0,
            Near = 1,
            Control = 2,
            Shoot = 3
        }

        private struct ReplayPuckContext
        {
            public bool IsValid;
            public bool HasPuckControl;
            public float DistanceBotToPuck;
            public Vector3 DirectionBotToPuck;
            public float DistanceStickToPuck;
            public float DistanceBladeToPuck;
            public float RelativeStickPuckAngle;
            public Vector3 BotPosition;
            public Vector3 PuckPosition;
            public Vector3 BladeWorldPosition;
            public float BladeHeightAboveGround;
            public Vector3 RelativePuckVelocity;
            public float RelativePuckSpeed;
            public ReplayPuckZone Zone;
        }

        private struct ReplayLearningSituation
        {
            public ReplayPuckZone Zone;
            public sbyte DistanceBucket;
            public sbyte AngleBucket;
        }

        private struct ReplayLearningAdjustment
        {
            public float PitchDelta;
            public float YawDelta;
            public float BladeDelta;
        }

        private struct ReplayLearningSample
        {
            public bool HasValue;
            public ReplayLearningSituation Situation;
            public ReplayLearningAdjustment Adjustment;
            public float ErrorDistance;
            public float PuckDistance;
            public Vector2 StickAngles;
            public float BladeAngle;
            public Vector2 MoveInput;
            public float RelativeDirection;
            public float CreatedAt;
        }

        private struct ReplayLearningTrial
        {
            public bool Active;
            public ReplayLearningSample Sample;
            public float StartedAt;
        }

        private struct ReplayReinforcementState
        {
            public ReplayPuckZone Zone;
            public sbyte DistanceBucket;
            public sbyte AngleBucket;
            public sbyte SpeedBucket;
            public sbyte StickAngleBucket;
        }

        private struct ReplayReinforcementStep
        {
            public bool HasValue;
            public string StateKey;
            public ReplayReinforcementAction Action;
        }

        private struct ReplayReinforcementValue
        {
            public float Value;
            public int Visits;
        }

        private enum ReplayReinforcementAction
        {
            None = 0,
            StickYawLeft = 1,
            StickYawRight = 2,
            StickLower = 3,
            BladeDown = 4,
            BladeUp = 5,
            MoveLeft = 6,
            MoveRight = 7
        }

        private sealed class ReplayReinforcementEntry
        {
            public string StateKey { get; set; }
            public ReplayReinforcementAction Action { get; set; }
            public float Value { get; set; }
            public int Visits { get; set; }
        }

        private sealed class ReplayReinforcementFile
        {
            public int Version { get; set; } = 1;
            public string SavedUtc { get; set; }
            public List<ReplayReinforcementEntry> Entries { get; set; } = new List<ReplayReinforcementEntry>();
        }

        private enum BotState
        {
            Idle = 0,
            Chase = 1,
            Align = 2,
            Control = 3,
            Dribble = 4,
            Shoot = 5,
            Recover = 6
        }

        private const float GoalZRed = -40.23f;
        private const float GoalZBlue = 40.23f;
        private const float PuckControlDistance = 2.25f;
        private const float PuckCloseDistance = 1.15f;
        private const float StopDistance = 0.45f;
        private const float PuckPredictionTime = 0.32f;
        private const float WallRayHeight = 0.45f;
        private const float WallAvoidDistance = 2.75f;
        private const float WallSideProbeDistance = 2.35f;
        private const float PuckWallDetectDistance = 1.1f;
        private const float PuckWallOpenOffset = 0.62f;
        private const float PuckWallDirectionBlend = 0.68f;
        private const float PuckWallStickPullOffset = 0.16f;
        private const float TurnBrakeAngle = 95f;
        private const float HardTurnAngle = 140f;
        private const float TurnDeadZoneAngle = 7f;
        private const float TurnHysteresisAngle = 13f;
        private const float TurnInputLerpPerTick = 0.22f;
        private const float ForwardAlignMaxAngle = 88f;
        private const float ForwardAlignMinAngle = 10f;
        private const float DefendLerp = 0.45f;
        private const float ControlOffset = 0.85f;
        private const float PuckStickBehindOffset = 0.72f;
        private const float PuckStickSideOffset = 0.18f;
        private const float GoalStickSideOffset = 0.08f;
        private const float StickIceLift = 0.02f;
        private const float StickAttackLift = 0.14f;
        private const float SharpTurnAngle = 58f;
        private const float SlideTurnAngle = 120f;
        private const float SlideSpeedThreshold = 6.5f;
        private const float SlidePulsePeriod = 0.22f;
        private const float SlidePulseDuration = 0.06f;
        private const float SlidePulseTurnAngle = 82f;
        private const float SlidePulseMoveYThreshold = 0.38f;
        private const float ControlPulsePeriod = 0.34f;
        private const float ControlPulseDuration = 0.1f;
        private const float CloseControlDistance = 1.75f;
        private const float ApproachEngageDistance = 1.3f;
        private const float ApproachTargetOffset = 1.05f;
        private const float ApproachTargetDeadzone = 0.2f;
        private const float ApproachTargetLockDuration = 0.45f;
        private const float ApproachSlowDistance = 1.1f;
        private const float ApproachRetargetDistance = 2.4f;
        private const float ApproachPuckMoveThreshold = 0.28f;
        private const float ApproachAlignTurnOnlyAngle = 26f;
        private const float ApproachBodyAlignmentAngleThreshold = 20f;
        private const float ApproachStickAlignmentAngleThreshold = 20f;
        private const float ApproachEntrySpeedThreshold = 2.1f;
        private const float ApproachProgressDistanceThreshold = 0.06f;
        private const float ApproachOrbitTurnAngle = 58f;
        private const float ApproachOrbitFreezeDelay = 0.18f;
        private const float ApproachOrbitResetDuration = 0.6f;
        private const float ApproachAntiSpinFreezeDuration = 0.2f;
        private const float ApproachStickAngleDeadzone = 1.4f;
        private const float ApproachStickTargetDeadzone = 0.06f;
        private const float ChaseTransitionDistance = 1.7f;
        private const float RecoverReleaseDuration = 0.22f;
        private const float RecoverChaseDistance = 1.95f;
        private const float AlignLateralThreshold = 0.24f;
        private const float AlignTightLateralThreshold = 0.12f;
        private const float AlignForwardThreshold = 0.28f;
        private const float AlignTurnScale = 0.55f;
        private const float AlignForwardSpeed = 0.16f;
        private const float AlignMicroAdvanceSpeed = 0.08f;
        private const float ControlStableDuration = 0.38f;
        private const float ControlForwardCap = 0.22f;
        private const float ControlForwardMin = 0.03f;
        private const float ControlRecoverDistance = 0.9f;
        private const float ControlNearPuckDistance = 0.24f;
        private const float ControlFarPuckDistance = 0.6f;
        private const float ControlOvershootFrontThreshold = 0.08f;
        private const float ControlVectorBehindOffset = 0.7f;
        private const float ControlBladeAlignmentAngleThreshold = 24f;
        private const float ContactAnticipationDistance = 0.72f;
        private const float ContactAnticipationForwardCap = 0.11f;
        private const float ContactAnticipationTurnDamping = 0.58f;
        private const float ContactCorridorLateralThreshold = 0.11f;
        private const float ContactCorridorFrontMin = 0.03f;
        private const float ContactCorridorFrontMax = 0.52f;
        private const float ContactCorridorBladeDistance = 0.44f;
        private const float ContactSpeedThreshold = 2.65f;
        private const float ContactHardSpeedThreshold = 4.4f;
        private const float ContactBladePerpendicularOffset = 0.07f;
        private const float BladeOrientationLockDuration = 0.18f;
        private const float StableControlVectorBlend = 0.18f;
        private const float ContactStableControlVectorBlend = 0.08f;
        private const float WallExtractionDirectionBlend = 0.42f;
        private const float WallExtractionSideBias = 0.36f;
        private const float WallExtractionStickPullOffset = 0.26f;
        private const float DribbleStableDuration = 0.28f;
        private const float DribbleForwardMin = 0.08f;
        private const float DribbleForwardCap = 0.24f;
        private const float DribbleTurnScale = 0.42f;
        private const float DribbleRecoverDistance = 1.02f;
        private const float DribbleControlRadius = 0.42f;
        private const float DribbleBladeAlignmentAngleThreshold = 18f;
        private const float DribbleGoalDirectionBlend = 0.7f;
        private const float DribbleLateralBurstThreshold = 0.1f;
        private const float DribbleLateralStrongThreshold = 0.2f;
        private const float DribbleStickSideScale = 0.45f;
        private const float PossessionFrontThreshold = 0.2f;
        private const float PossessionLateralThreshold = 0.14f;
        private const float PossessionRelativeSpeedThreshold = 1.05f;
        private const float PossessionLooseRelativeSpeedThreshold = 1.55f;
        private const float SideSelectionDeadzone = 0.04f;
        private const float WallLockDuration = 0.55f;
        private const float WallLockRelativeSpeedThreshold = 0.45f;
        private const float WallLockLateralThreshold = 0.16f;
        private const float ShotGoalDistanceThreshold = 24f;
        private const float StickTargetNoiseThreshold = 0.03f;
        private const float StickAngleNoiseThreshold = 0.75f;
        private const float StickAngleMaxDeltaPerFrame = 5.5f;
        private const float StickAngleSmoothing = 0.28f;
        private const float ControlStickAngleMaxDeltaPerFrame = 3.2f;
        private const float ControlStickAngleSmoothing = 0.22f;
        private const float LateralPulsePeriod = 0.18f;
        private const float LateralPulseDuration = 0.06f;
        private const float CrouchTurnAngle = 42f;
        private const float CrouchPulsePeriod = 0.2f;
        private const float CrouchPulseDuration = 0.07f;
        private const float DefenseInterceptLerp = 0.42f;
        private const float ShotExecuteDuration = 0.16f;
        private const float ShotFacingAngleThreshold = 18f;
        private const float ShotPuckFrontThreshold = 0.2f;
        private const float BladeBehindPuckForwardThreshold = 0.08f;
        private const float LowStickInteractionDistance = 1.8f;
        private const float ShotDriveBlend = 0.22f;
        private const float ShotMoveForward = 0.62f;
        private const float ShotMoveTurnScale = 0.6f;
        private const float ShotBladeAngle = -4f;
        private const float ReplayCorrectionDistance = 2.4f;
        private const float ReplayCorrectionNearBlend = 0.3f;
        private const float ReplayCorrectionControlBlend = 0.5f;
        private const float ReplayCorrectionMaxAngleDelta = 10f;
        private const float ReplayCorrectionMaxYawOffset = 8f;
        private const float LowStickForwardDistance = 0.8f;
        private const float ReplayPuckFarDistance = 2.8f;
        private const float ReplayPuckNearDistance = 1.45f;
        private const float ReplayStickControlDistance = 0.72f;
        private const float ReplayStickContactDistance = 0.42f;
        private const float ReplayStickAlignmentAngle = 18f;
        private const float ReplayStickOffsetSlew = 1.4f;
        private const float ReplayFrameDistanceThreshold = 0.45f;
        private const float ReplayFrameControlDistanceThreshold = 0.25f;
        private const float ReplayFrameDribbleDistanceThreshold = 0.14f;
        private const float ReplayFrameAngleThreshold = 28f;
        private const float ReplayFrameDribbleAngleThreshold = 12f;
        private const float ReplayMoveControlBlend = 0.14f;
        private const float ReplayMoveShootBlend = 0.22f;
        private const float ReplayMoveTurnDribbleBlend = 0.1f;
        private const float ReplayShootGoalDistance = 18f;
        private const float ReplayShootTurnAngle = 36f;
        private const float ReplayTurnDribbleAngle = 42f;
        private const float ReplayTiltNearFactor = 0.3f;
        private const float ReplayBladeAngleClamp = 42f;
        private const float ReplayBladeForwardClamp = 24f;
        private const float ReplayBladeSmoothing = 0.2f;
        private const float ReplayMoveSmoothing = 0.2f;
        private const float ReplayDribbleMoveXThreshold = 0.2f;
        private const float ReplayDribbleMoveScale = 0.35f;
        private const float ReplayDribbleStickDeltaThreshold = 5.5f;
        private const float ReplayDribbleHoldDuration = 0.45f;
        private const float ReplayDribbleForwardCap = 0.58f;
        private const float ReplayControlForwardCap = 0.72f;
        private const float BladeGroundRaycastDistance = 1.2f;
        private const float BladeGroundHeightThreshold = 0.09f;
        private const float BladeGroundPushScale = 1.15f;
        private const float BladePuckReadyDistance = 0.38f;
        private const float BladePuckNearDistance = 0.8f;
        private const float BladeShootReadyAngle = 18f;
        private const int ReplayLearningBufferSize = 12;
        private const float ReplayLearningErrorDistance = 0.34f;
        private const float ReplayLearningImprovementThreshold = 0.015f;
        private const float ReplayLearningRegressionThreshold = 0.03f;
        private const float ReplayLearningEvaluationWindow = 0.18f;
        private const float ReplayLearningDistanceBucketSize = 0.16f;
        private const float ReplayLearningAngleBucketSize = 10f;
        private const float ReplayLearningPitchStep = 1.2f;
        private const float ReplayLearningYawStep = 1.8f;
        private const float ReplayLearningBladeStep = 2f;
        private const float ReplayLearningMaxPitchDelta = 4f;
        private const float ReplayLearningMaxYawDelta = 5f;
        private const float ReplayLearningMaxBladeDelta = 6f;
        private const float ReplayLearningSmoothing = 0.18f;
        private const float ReplayStickTargetSmoothing = 0.2f;
        private const float ReplayStylePitchClamp = 6f;
        private const float ReplayStyleYawClamp = 8f;
        private const float ReplayStylePitchBlend = 0.2f;
        private const float ReplayStyleYawBlend = 0.28f;
        private const float ReplayStyleBladeBlend = 0.3f;
        private const float ReplayGoalieCloseBehindOffset = 0.26f;
        private const float ReplayGoalieCloseSideOffset = 0.06f;
        private const float ReplayGoalieBehindNetDotThreshold = 0.5f;
        private const float ReplayGoalieNearNetDistance = 2.5f;
        private const float ReplayGoalieFarNetDistance = 6f;
        private const float ReplayGoalieMaxRotationAngle = 85f;
        private const float ReplayReinforcementDistanceBucketSize = 0.16f;
        private const float ReplayReinforcementAngleBucketSize = 12f;
        private const float ReplayReinforcementSpeedBucketSize = 1.35f;
        private const float ReplayReinforcementStickAngleBucketSize = 15f;
        private const float ReplayReinforcementAlpha = 0.2f;
        private const float ReplayReinforcementGamma = 0.85f;
        private const float ReplayReinforcementEpsilon = 0.12f;
        private const float ReplayReinforcementStickYawDelta = 1.5f;
        private const float ReplayReinforcementStickPitchDelta = 1.1f;
        private const float ReplayReinforcementBladeDelta = 1.75f;
        private const float ReplayReinforcementMoveXDelta = 0.12f;
        private const float ReplayReinforcementSmoothing = 0.2f;
        private const float ReplayReinforcementSaveInterval = 5f;
        private const int ReplayReinforcementBinaryVersion = 1;
        private const int ReplayReinforcementBinaryMagic = 0x314C5242;

        private static readonly object replayReinforcementLock = new object();
        private static readonly Dictionary<string, Dictionary<ReplayReinforcementAction, ReplayReinforcementValue>> replayReinforcementTable = new Dictionary<string, Dictionary<ReplayReinforcementAction, ReplayReinforcementValue>>(StringComparer.Ordinal);
        private static bool replayReinforcementLoaded;
        private static bool replayReinforcementDirty;
        private static float replayReinforcementNextSaveTime;

        private Player controlledPlayer;
        private PlayerInput playerInput;
        private PlayerBodyV2 playerBody;
        private Stick stick;
        private StickPositioner stickPositioner;
        private BotState currentState;
        private Vector3 smoothedMoveDirection = Vector3.forward;
        private Vector3 lastPuckDirection = Vector3.forward;
        private float smoothedTurnInput;
        private int lastTurnSign;
        private int avoidSign = 1;
        private float lastControlPulseTime;
        private bool hasApproachTarget;
        private Vector3 cachedApproachPuckPosition = Vector3.zero;
        private Vector3 cachedApproachTarget = Vector3.zero;
        private Vector3 cachedApproachControlVector = Vector3.forward;
        private Vector3 cachedApproachWallDirection = Vector3.zero;
        private int cachedApproachSideSign = 1;
        private Vector2 lastSentStickAngles = Vector2.zero;
        private Vector3 lastSentStickTarget = Vector3.zero;
        private bool hasSentStickAngles;
        private Vector2 smoothedLiveStickAngles = Vector2.zero;
        private bool hasSmoothedLiveStickAngles;
        private int stickSideSign = 1;
        private bool replayMode;
        private float replayStickYawOffset;
        private float replaySmoothedBladeAngle;
        private float replayLastMoveX;
        private Vector2 replayLastRecordedStickAngles = Vector2.zero;
        private bool replayHasRecordedFrame;
        private float replayDribbleUntilTime;
        private float replaySmoothedMoveX;
        private readonly ReplayLearningSample[] replayLearningSamples = new ReplayLearningSample[ReplayLearningBufferSize];
        private int replayLearningSampleCount;
        private int replayLearningNextIndex;
        private ReplayLearningTrial replayLearningTrial;
        private Vector2 replaySmoothedStickAngles = Vector2.zero;
        private bool replayHasSmoothedStickAngles;
        private Vector2 replaySmoothedLearnedStickDelta = Vector2.zero;
        private float replaySmoothedLearnedBladeDelta;
        private bool replayHasPreviousLearningFrame;
        private float replayPreviousBladeDistance;
        private bool replayPreviousExpectedContact;
        private bool replayPreviousShotContext;
        private ReplayReinforcementStep replayPreviousReinforcementStep;
        private Vector2 replaySmoothedReinforcementStickDelta = Vector2.zero;
        private float replaySmoothedReinforcementBladeDelta;
        private float replaySmoothedReinforcementMoveX;
        private bool replayPreviousReinforcementHadControl;
        private float currentStateEnteredAt;
        private float controlStableSince = -1f;
        private float dribbleStableSince = -1f;
        private float lastLateralPulseTime;
        private float lastCrouchPulseTime;
        private float lastSlidePulseTime;
        private float wallLockSince = -1f;
        private float approachTargetLockedUntil = -1f;
        private float approachNoProgressSince = -1f;
        private float lastApproachDistanceToTarget = -1f;
        private float approachFreezeLateralUntil = -1f;
        private bool hasStableControlVector;
        private Vector3 stableControlVector = Vector3.forward;
        private bool hasBladeOrientationLock;
        private Vector3 lockedBladeControlVector = Vector3.forward;
        private Vector3 lockedBladeWallPullOffset = Vector3.zero;
        private int lockedBladeSideSign = 1;
        private float bladeOrientationLockedUntil = -1f;

        internal void Initialize(Player player)
        {
            controlledPlayer = player;
            CacheReferences();
            RankedSystem.EnsureReplayMemoryAvailable();
            EnsureReplayReinforcementLoaded();
            stickSideSign = GetInitialSideSign();
            currentState = BotState.Idle;
            currentStateEnteredAt = Time.time;
            controlStableSince = -1f;
            dribbleStableSince = -1f;
            wallLockSince = -1f;
            hasSmoothedLiveStickAngles = false;
            hasStableControlVector = false;
            hasBladeOrientationLock = false;
        }

        private void OnDisable()
        {
            MaybeSaveReplayReinforcement(force: true);
            TryResetInputs();
        }

        private void OnDestroy()
        {
            RankedSystem.ReleaseAutonomousReplayState(this);
            MaybeSaveReplayReinforcement(force: true);
            TryResetInputs();
        }

        private void Update()
        {
            try
            {
                if (!IsServer()) return;
                if (!EnsureReady())
                {
                    currentState = BotState.Idle;
                    return;
                }

                if (replayMode)
                {
                    return;
                }

                if (!IsGameplayPhase())
                {
                    currentState = BotState.Idle;
                    ClearApproachTarget();
                    ApplyIdle();
                    return;
                }

                var puckManager = NetworkBehaviourSingleton<PuckManager>.Instance;
                if (puckManager == null)
                {
                    currentState = BotState.Idle;
                    ClearApproachTarget();
                    ApplyIdle();
                    return;
                }

                var puck = GetClosestPuck(puckManager);
                if (puck == null)
                {
                    currentState = BotState.Idle;
                    ClearApproachTarget();
                    ApplyIdle();
                    return;
                }

                var botPosition = GetBotPosition();
                var actualPuckPosition = puck.transform.position;
                var puckPosition = GetFlatPosition(actualPuckPosition, botPosition.y);
                var predictedPuckPosition = PredictPuckPosition(puck, botPosition.y);
                var puckDirection = puckPosition - botPosition;
                puckDirection.y = 0f;
                if (puckDirection.sqrMagnitude > 0.0001f)
                {
                    lastPuckDirection = puckDirection.normalized;
                }
                var puckDistance = Vector3.Distance(botPosition, puckPosition);

                var hasPuck = HasBotPuckControl(puckManager, puck);
                var hasPuckContext = TryBuildReplayPuckContext(out var puckContext);
                var localPuckPosition = GetLocalPuckPosition(actualPuckPosition);
                var nextState = DecideState(
                    puckDistance,
                    hasPuck,
                    hasPuckContext ? puckContext : default,
                    localPuckPosition,
                    actualPuckPosition,
                    predictedPuckPosition);

                if (nextState != currentState)
                {
                    EnterState(nextState);
                }

                switch (currentState)
                {
                    case BotState.Chase:
                        ExecuteChasePuck(predictedPuckPosition, actualPuckPosition, hasPuckContext ? puckContext : default, localPuckPosition);
                        break;
                    case BotState.Align:
                        ExecuteAlignPuck(predictedPuckPosition, actualPuckPosition, hasPuckContext ? puckContext : default, localPuckPosition);
                        break;
                    case BotState.Control:
                        ExecuteControlPuck(predictedPuckPosition, actualPuckPosition, hasPuckContext ? puckContext : default, localPuckPosition);
                        break;
                    case BotState.Dribble:
                        ExecuteDribblePuck(predictedPuckPosition, actualPuckPosition, hasPuckContext ? puckContext : default, localPuckPosition);
                        break;
                    case BotState.Shoot:
                        ExecuteShootPuck(actualPuckPosition, hasPuckContext ? puckContext : default, localPuckPosition);
                        break;
                    case BotState.Recover:
                        ClearApproachTarget();
                        ExecuteRecover(predictedPuckPosition, actualPuckPosition, hasPuckContext ? puckContext : default, localPuckPosition);
                        break;
                    default:
                        ClearApproachTarget();
                        ApplyIdle();
                        break;
                }
            }
            catch
            {
                ApplyIdle();
            }
            finally
            {
                try { RankedSystem.UpdateAutonomousReplayRecording(this); } catch { }
            }
        }

        private static bool IsServer()
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        }

        private bool EnsureReady()
        {
            if (controlledPlayer == null)
            {
                controlledPlayer = GetComponent<Player>();
            }

            CacheReferences();
            return controlledPlayer != null && playerInput != null;
        }

        private void CacheReferences()
        {
            if (controlledPlayer == null) return;
            if (playerInput == null) playerInput = controlledPlayer.PlayerInput;
            if (playerBody == null) playerBody = controlledPlayer.PlayerBody;
            if (stick == null) stick = controlledPlayer.Stick;
            if (stickPositioner == null) stickPositioner = controlledPlayer.StickPositioner;
        }

        private bool IsGameplayPhase()
        {
            var gameManager = NetworkBehaviourSingleton<GameManager>.Instance;
            if (gameManager == null) return true;

            var phase = gameManager.Phase;
            return phase == GamePhase.Warmup || phase == GamePhase.FaceOff || phase == GamePhase.Playing;
        }

        private void EnterState(BotState nextState)
        {
            currentState = nextState;
            currentStateEnteredAt = Time.time;

            if (nextState != BotState.Control)
            {
                controlStableSince = -1f;
            }

            if (nextState != BotState.Dribble)
            {
                dribbleStableSince = -1f;
            }

            wallLockSince = -1f;

            if (nextState != BotState.Chase && nextState != BotState.Align)
            {
                ClearApproachTarget();
            }

            if (nextState != BotState.Align && nextState != BotState.Control && nextState != BotState.Dribble)
            {
                ClearContactPreparation();
            }
        }

        private BotState DecideState(float puckDistance, bool hasPuck, ReplayPuckContext puckContext, Vector3 localPuckPosition, Vector3 stickPuckPosition, Vector3 movePuckPosition)
        {
            var hasValidContext = puckContext.IsValid;
            var approachControlVector = GetApproachControlVector(stickPuckPosition);
            var alignmentReady = IsAlignmentReady(localPuckPosition, puckContext, approachControlVector);
            var controlReady = IsControlReady(localPuckPosition, puckContext, hasPuck, approachControlVector);
            var approachEntryReady = IsApproachEntryReady(localPuckPosition, puckContext, approachControlVector);
            var hasTruePossession = HasTruePuckPossession(localPuckPosition, puckContext);
            var shotReady = IsShotReady(localPuckPosition, puckContext);
            var wallLocked = IsWallLocked(stickPuckPosition, localPuckPosition, puckContext);

            if ((currentState == BotState.Align || currentState == BotState.Control || currentState == BotState.Dribble) && wallLocked)
            {
                return BotState.Recover;
            }

            if (currentState == BotState.Shoot)
            {
                if (!shotReady)
                {
                    return BotState.Recover;
                }

                if (Time.time - currentStateEnteredAt >= ShotExecuteDuration)
                {
                    return BotState.Recover;
                }

                return BotState.Shoot;
            }

            if (currentState == BotState.Dribble)
            {
                if (IsOvershootingPuck(localPuckPosition, puckContext) || !IsPuckInsideControlRadius(localPuckPosition, puckContext))
                {
                    return BotState.Align;
                }

                if (!hasTruePossession || puckContext.DistanceBladeToPuck > DribbleRecoverDistance)
                {
                    return BotState.Align;
                }

                if (dribbleStableSince < 0f)
                {
                    dribbleStableSince = Time.time;
                }

                if (shotReady && Time.time - dribbleStableSince >= DribbleStableDuration)
                {
                    return BotState.Shoot;
                }

                return BotState.Dribble;
            }

            if (currentState == BotState.Recover)
            {
                if (Time.time - currentStateEnteredAt < RecoverReleaseDuration)
                {
                    return BotState.Recover;
                }

                if (!hasValidContext || puckDistance > RecoverChaseDistance)
                {
                    return BotState.Chase;
                }
            }

            if (!hasValidContext)
            {
                return BotState.Chase;
            }

            if (currentState == BotState.Control)
            {
                if (IsOvershootingPuck(localPuckPosition, puckContext))
                {
                    return BotState.Align;
                }

                if (!controlReady || puckContext.DistanceBladeToPuck > ControlRecoverDistance)
                {
                    return BotState.Align;
                }

                if (controlStableSince < 0f)
                {
                    controlStableSince = Time.time;
                }

                if (hasTruePossession && Time.time - controlStableSince >= ControlStableDuration)
                {
                    return BotState.Dribble;
                }

                return BotState.Control;
            }

            controlStableSince = -1f;
            dribbleStableSince = -1f;

            if (currentState == BotState.Align)
            {
                if (approachEntryReady && controlReady)
                {
                    return BotState.Control;
                }

                if (puckDistance > ChaseTransitionDistance * 1.2f)
                {
                    return BotState.Chase;
                }

                return BotState.Align;
            }

            if (alignmentReady || puckDistance <= ChaseTransitionDistance)
            {
                return BotState.Align;
            }

            return BotState.Chase;
        }

        private void ExecuteChasePuck(Vector3 movePuckPosition, Vector3 stickPuckPosition, ReplayPuckContext puckContext, Vector3 localPuckPosition)
        {
            if (TryBuildDefensiveInterceptTarget(out var defensiveMoveTarget, out var defensiveStickTarget))
            {
                DriveTowards(defensiveMoveTarget, sprint: true, stickTarget: defensiveStickTarget, bladeAngle: 0);
                return;
            }

            var moveTarget = GetLockedApproachTarget(stickPuckPosition, localPuckPosition, refreshForOrbit: false);
            var desiredDirection = cachedApproachControlVector;
            var puckSideSign = cachedApproachSideSign;
            var wallOffset = cachedApproachWallDirection * PuckWallStickPullOffset;
            var stickTarget = BuildLowBehindPuckTarget(
                stickPuckPosition,
                desiredDirection,
                0.55f,
                puckSideSign,
                wallOffset);
            if (ShouldForceLowStick(puckContext))
            {
                stickTarget = ForceStickTargetLow(stickTarget, stickPuckPosition.y);
            }

            DriveApproach(moveTarget, movePuckPosition, stickTarget, sprint: true, bladeAngle: 0);
        }

        private void ExecuteAlignPuck(Vector3 movePuckPosition, Vector3 stickPuckPosition, ReplayPuckContext puckContext, Vector3 localPuckPosition)
        {
            var moveTarget = GetLockedApproachTarget(stickPuckPosition, localPuckPosition, refreshForOrbit: true);
            var anticipationActive = IsContactAnticipationActive(localPuckPosition, puckContext);
            var desiredDirection = PrepareContactDirection(cachedApproachControlVector, stickPuckPosition, localPuckPosition, puckContext, allowOrientationLock: true, out var puckSideSign, out var wallPullOffset);
            var stickTarget = BuildControlBladeTarget(stickPuckPosition, desiredDirection, localPuckPosition, puckSideSign, wallPullOffset, anticipationActive);
            if (ShouldForceLowStick(puckContext))
            {
                stickTarget = ForceStickTargetLow(stickTarget, stickPuckPosition.y);
            }

            var botPosition = GetBotPosition();
            var toTarget = moveTarget - botPosition;
            toTarget.y = 0f;
            var distanceToTarget = toTarget.magnitude;
            var desiredMoveDirection = distanceToTarget > 0.0001f ? toTarget / distanceToTarget : desiredDirection;
            desiredMoveDirection = ApplyWallAvoidance(botPosition, desiredMoveDirection);
            desiredMoveDirection = StabilizeMoveDirection(desiredMoveDirection, distanceToTarget);

            var signedTurnAngle = Vector3.SignedAngle(GetFlatForward(), desiredMoveDirection, Vector3.up);
            var turnAngle = Mathf.Abs(signedTurnAngle);
            var turnInput = ComputeTurnInput(signedTurnAngle, turnAngle);
            var distanceFactor = Mathf.Clamp01(Mathf.InverseLerp(ApproachTargetDeadzone, ApproachSlowDistance, distanceToTarget));
            var puckDistanceFactor = Mathf.Clamp01(Mathf.InverseLerp(ControlOvershootFrontThreshold, ApproachSlowDistance, Mathf.Max(localPuckPosition.z, 0f)));
            var bodyAlignment = Mathf.Clamp01(Mathf.InverseLerp(ApproachBodyAlignmentAngleThreshold, TurnDeadZoneAngle, turnAngle));
            var moveY = Mathf.Lerp(0.04f, AlignForwardSpeed, distanceFactor) * puckDistanceFactor * bodyAlignment;

            if (distanceToTarget <= ApproachTargetDeadzone)
            {
                moveY = 0.02f * bodyAlignment;
            }

            var inCorridor = IsPuckInsideContactCorridor(localPuckPosition, puckContext);
            moveY = ApplyContactSpeedControl(moveY, localPuckPosition, puckContext, inCorridor);

            if (Time.time < approachFreezeLateralUntil)
            {
                moveY = Mathf.Min(moveY, 0.06f);
                turnInput *= 0.78f;
            }

            var bodyAligned = IsBodyAlignedWithDirection(desiredDirection, ApproachBodyAlignmentAngleThreshold);
            if (!bodyAligned)
            {
                moveY = Mathf.Min(moveY, 0.05f);
            }

            if (anticipationActive)
            {
                turnInput *= ContactAnticipationTurnDamping;
                if (!inCorridor)
                {
                    moveY = Mathf.Min(moveY, 0.035f);
                }
            }

            var shouldTapCrouch = ShouldTapCrouchForTurn(turnAngle, moveY);
            var shouldSlide = ShouldPulseSlideForTurn(turnAngle, moveY, distanceToTarget);
            ApplyInput(turnInput, moveY, sprint: false, stop: shouldTapCrouch, slide: shouldSlide, lateralLeft: false, lateralRight: false);
            ApplyStickInput(stickTarget, 0);
        }

        private void ExecuteControlPuck(Vector3 movePuckPosition, Vector3 stickPuckPosition, ReplayPuckContext puckContext, Vector3 localPuckPosition)
        {
            ClearApproachTarget();
            var enemyGoal = GetEnemyGoalPosition();
            var controlVector = enemyGoal - stickPuckPosition;
            controlVector.y = 0f;
            if (controlVector.sqrMagnitude <= 0.0001f)
            {
                controlVector = GetFlatForward();
            }

            controlVector.Normalize();
            var anticipationActive = IsContactAnticipationActive(localPuckPosition, puckContext);
            var desiredDirection = PrepareContactDirection(controlVector, stickPuckPosition, localPuckPosition, puckContext, allowOrientationLock: true, out var puckSideSign, out var wallPullOffset);
            GetLateralCorrectionInputs(localPuckPosition.x, AlignLateralThreshold, AlignTightLateralThreshold, out var lateralLeft, out var lateralRight);
            var inCorridor = IsPuckInsideContactCorridor(localPuckPosition, puckContext);
            if (anticipationActive)
            {
                lateralLeft = false;
                lateralRight = false;
            }

            var stickTarget = BuildControlBladeTarget(stickPuckPosition, desiredDirection, localPuckPosition, puckSideSign, wallPullOffset, anticipationActive);
            var signedGoalAngle = Vector3.SignedAngle(GetFlatForward(), desiredDirection, Vector3.up);
            var goalTurnAngle = Mathf.Abs(signedGoalAngle);
            var controlReady = IsBladeControlReady(localPuckPosition, puckContext, desiredDirection, ControlBladeAlignmentAngleThreshold);
            var moveY = GetControlForwardInput(localPuckPosition, puckContext, ControlForwardCap * 0.75f, ControlForwardMin);
            moveY = ApplyContactSpeedControl(moveY, localPuckPosition, puckContext, inCorridor);
            if (!controlReady || !inCorridor)
            {
                moveY = Mathf.Min(moveY, 0.04f);
            }

            var turnInput = Mathf.Clamp(signedGoalAngle / 70f, -0.45f, 0.45f);
            turnInput = Mathf.Clamp(turnInput + Mathf.Clamp(localPuckPosition.x / Mathf.Max(AlignLateralThreshold, 0.01f), -0.18f, 0.18f), -0.55f, 0.55f);
            if (anticipationActive)
            {
                turnInput *= ContactAnticipationTurnDamping;
            }

            var shouldTapCrouch = ShouldTapCrouchForTurn(goalTurnAngle, moveY);
            if (ShouldForceLowStick(puckContext))
            {
                stickTarget = ForceStickTargetLow(stickTarget, stickPuckPosition.y);
            }

            ApplyInput(turnInput, moveY, sprint: false, stop: shouldTapCrouch, slide: false, lateralLeft: lateralLeft, lateralRight: lateralRight);
            ApplyStickInput(stickTarget, 0);
        }

        private void ExecuteDribblePuck(Vector3 movePuckPosition, Vector3 stickPuckPosition, ReplayPuckContext puckContext, Vector3 localPuckPosition)
        {
            ClearApproachTarget();

            var enemyGoal = GetEnemyGoalPosition();
            var controlVector = enemyGoal - stickPuckPosition;
            controlVector.y = 0f;
            if (controlVector.sqrMagnitude <= 0.0001f)
            {
                controlVector = GetFlatForward();
            }

            controlVector.Normalize();
            var forward = GetFlatForward();
            var anticipationActive = IsContactAnticipationActive(localPuckPosition, puckContext);
            var controlDirection = PrepareContactDirection(controlVector, stickPuckPosition, localPuckPosition, puckContext, allowOrientationLock: anticipationActive, out var puckSideSign, out var wallPullOffset);
            var signedGoalAngle = Vector3.SignedAngle(forward, controlDirection, Vector3.up);
            var goalAngle = Mathf.Abs(signedGoalAngle);
            var turnInput = Mathf.Clamp(signedGoalAngle / 70f, -DribbleTurnScale, DribbleTurnScale);
            GetLateralCorrectionInputs(localPuckPosition.x, DribbleLateralStrongThreshold, DribbleLateralBurstThreshold, out var lateralLeft, out var lateralRight);
            if (anticipationActive)
            {
                lateralLeft = false;
                lateralRight = false;
            }

            var stickTarget = BuildControlBladeTarget(stickPuckPosition, controlDirection, localPuckPosition, puckSideSign, wallPullOffset, anticipationActive);
            var controlReady = IsBladeControlReady(localPuckPosition, puckContext, controlDirection, DribbleBladeAlignmentAngleThreshold);
            var inCorridor = IsPuckInsideContactCorridor(localPuckPosition, puckContext);
            var moveY = Mathf.Clamp(GetControlForwardInput(localPuckPosition, puckContext, DribbleForwardCap, DribbleForwardMin), 0.04f, DribbleForwardCap);
            moveY = ApplyContactSpeedControl(moveY, localPuckPosition, puckContext, inCorridor);
            if (!controlReady || !inCorridor)
            {
                moveY = Mathf.Min(moveY, 0.05f);
            }

            turnInput = Mathf.Clamp(turnInput + Mathf.Clamp(localPuckPosition.x / Mathf.Max(DribbleLateralStrongThreshold, 0.01f), -0.15f, 0.15f), -DribbleTurnScale, DribbleTurnScale);
            if (anticipationActive)
            {
                turnInput *= ContactAnticipationTurnDamping;
            }

            var shouldTapCrouch = ShouldTapCrouchForTurn(goalAngle, moveY);

            ApplyInput(turnInput, moveY, sprint: false, stop: shouldTapCrouch, slide: false, lateralLeft: lateralLeft, lateralRight: lateralRight);
            ApplyStickInput(stickTarget, 0);
        }

        private void ExecuteShootPuck(Vector3 stickPuckPosition, ReplayPuckContext puckContext, Vector3 localPuckPosition)
        {
            if (!IsShotReady(localPuckPosition, puckContext))
            {
                EnterState(BotState.Recover);
                ExecuteRecover(stickPuckPosition, stickPuckPosition, puckContext, localPuckPosition);
                return;
            }

            var enemyGoal = GetEnemyGoalPosition();
            var botPosition = GetBotPosition();
            var attackTarget = Vector3.Lerp(enemyGoal, stickPuckPosition, ShotDriveBlend);
            var toGoal = enemyGoal - botPosition;
            toGoal.y = 0f;
            var signedAngle = toGoal.sqrMagnitude > 0.0001f ? Vector3.SignedAngle(GetFlatForward(), toGoal.normalized, Vector3.up) : 0f;
            var moveX = Mathf.Clamp(signedAngle / HardTurnAngle, -ShotMoveTurnScale, ShotMoveTurnScale);
            var stickTarget = BuildGoalStickTarget(enemyGoal, stickPuckPosition, aggressive: false);
            if (ShouldForceLowStick(puckContext))
            {
                stickTarget = ForceStickTargetLow(stickTarget, stickPuckPosition.y);
            }

            DriveTowards(attackTarget, sprint: false, stickTarget: stickTarget, bladeAngle: (sbyte)ShotBladeAngle);
            ApplyInput(moveX, ShotMoveForward, sprint: false, stop: false, slide: false, lateralLeft: false, lateralRight: false);
        }

        private void ExecuteRecover(Vector3 movePuckPosition, Vector3 stickPuckPosition, ReplayPuckContext puckContext, Vector3 localPuckPosition)
        {
            if (TryBuildDefensiveInterceptTarget(out var defensiveMoveTarget, out var defensiveStickTarget))
            {
                DriveTowards(defensiveMoveTarget, sprint: true, stickTarget: defensiveStickTarget, bladeAngle: 0);
                return;
            }

            var desiredDirection = GetDesiredPuckTravelDirection(stickPuckPosition);
            var puckSideSign = GetApproachSideSign(localPuckPosition, desiredDirection);
            var wallPullOffset = Vector3.zero;
            if (TryGetWallOpenDirection(stickPuckPosition, desiredDirection, out var wallOpenDirection, out var wallSideSign))
            {
                desiredDirection = BlendWallAwareDirection(desiredDirection, wallOpenDirection);
                puckSideSign = wallSideSign;
                wallPullOffset = wallOpenDirection * PuckWallStickPullOffset;
            }

            var stickTarget = ForceStickTargetLow(BuildLowBehindPuckTarget(stickPuckPosition, desiredDirection, 0.45f, puckSideSign, wallPullOffset), stickPuckPosition.y);
            var moveTarget = GetFlatPosition(movePuckPosition, GetBotPosition().y);
            DriveTowards(moveTarget, sprint: false, stickTarget: stickTarget, bladeAngle: 0);
        }

        private void ApplyIdle()
        {
            ApplyInput(0f, 0f, sprint: false, stop: true, slide: false);
            ApplyLowStickFallback(0);
        }

        private void DriveApproach(Vector3 moveTarget, Vector3 puckPosition, Vector3 stickTarget, bool sprint, sbyte bladeAngle)
        {
            var botPosition = GetBotPosition();
            var toTarget = moveTarget - botPosition;
            toTarget.y = 0f;
            var distanceToTarget = toTarget.magnitude;

            if (distanceToTarget <= ApproachTargetDeadzone)
            {
                ApplyInput(0f, 0f, sprint: false, stop: true, slide: false);
                ApplyStickInput(stickTarget, bladeAngle);
                return;
            }

            var desiredDirection = distanceToTarget > 0.0001f ? toTarget / distanceToTarget : GetFlatForward();
            desiredDirection = ApplyWallAvoidance(botPosition, desiredDirection);
            desiredDirection = StabilizeMoveDirection(desiredDirection, distanceToTarget);

            var forward = GetFlatForward();
            var signedTurnAngle = Vector3.SignedAngle(forward, desiredDirection, Vector3.up);
            var turnAngle = Mathf.Abs(signedTurnAngle);

            var moveX = ComputeTurnInput(signedTurnAngle, turnAngle);
            var alignmentFactor = turnAngle >= ApproachAlignTurnOnlyAngle ? 0f : Mathf.InverseLerp(ApproachAlignTurnOnlyAngle, TurnDeadZoneAngle, turnAngle);
            var distanceFactor = Mathf.Clamp01(Mathf.InverseLerp(ApproachTargetDeadzone, ApproachSlowDistance, distanceToTarget));
            var moveY = alignmentFactor * Mathf.Lerp(0.2f, 1f, distanceFactor);

            if (distanceToTarget <= ApproachSlowDistance)
            {
                moveY *= 0.55f + distanceFactor * 0.45f;
            }

            var shouldBrake = turnAngle >= TurnBrakeAngle || (distanceToTarget <= ApproachSlowDistance && turnAngle >= SharpTurnAngle);
            var shouldSlide = ShouldPulseSlideForTurn(turnAngle, moveY, distanceToTarget);
            var controlPulse = !shouldSlide && turnAngle >= SharpTurnAngle && ShouldPulseControlTurn();
            var useSprint = sprint && distanceToTarget > ApproachSlowDistance && !shouldBrake && !shouldSlide;

            ApplyInput(moveX, moveY, useSprint, stop: shouldBrake || controlPulse, slide: shouldSlide);
            ApplyStickInput(stickTarget, bladeAngle);
        }

        private void DriveTowards(Vector3 moveTarget, bool sprint, Vector3 stickTarget, sbyte bladeAngle)
        {
            var botPosition = GetBotPosition();
            var toTarget = moveTarget - botPosition;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= StopDistance * StopDistance)
            {
                smoothedTurnInput = Mathf.MoveTowards(smoothedTurnInput, 0f, TurnInputLerpPerTick);
                ApplyInput(smoothedTurnInput, 0f, sprint: false, stop: true, slide: false);
                ApplyStickInput(stickTarget, bladeAngle);
                return;
            }

            var desiredDirection = StabilizeMoveDirection(ApplyWallAvoidance(botPosition, toTarget.normalized), toTarget.magnitude);

            var forward = GetFlatForward();
            var turnDirection = lastPuckDirection;
            if (turnDirection.sqrMagnitude <= 0.0001f) turnDirection = smoothedMoveDirection;
            turnDirection.y = 0f;
            if (turnDirection.sqrMagnitude <= 0.0001f) turnDirection = forward;
            turnDirection.Normalize();

            var signedTurnAngle = Vector3.SignedAngle(forward, turnDirection, Vector3.up);
            var turnAngle = Mathf.Abs(signedTurnAngle);

            var moveX = ComputeTurnInput(signedTurnAngle, turnAngle);

            var moveY = Mathf.Clamp01(Mathf.InverseLerp(ForwardAlignMaxAngle, ForwardAlignMinAngle, turnAngle));
            if (turnAngle >= HardTurnAngle) moveY = 0f;

            var shouldBrake = turnAngle >= TurnBrakeAngle;
            if (shouldBrake && moveY > 0f) moveY *= 0.35f;

            var shouldControlTurn = turnAngle >= SharpTurnAngle;
            var shouldSlide = ShouldPulseSlideForTurn(turnAngle, moveY, toTarget.magnitude);
            var controlPulse = shouldControlTurn && !shouldSlide && ShouldPulseControlTurn();

            var useSprint = sprint && !shouldBrake && !shouldSlide;
            ApplyInput(moveX, moveY, useSprint, stop: shouldBrake || controlPulse, slide: shouldSlide);
            ApplyStickInput(stickTarget, bladeAngle);
        }

        private Vector3 ApplyWallAvoidance(Vector3 botPosition, Vector3 desiredDirection)
        {
            var origin = botPosition + Vector3.up * WallRayHeight;
            if (!Physics.Raycast(origin, desiredDirection, out var forwardHit, WallAvoidDistance, ~0, QueryTriggerInteraction.Ignore)) return desiredDirection;

            var left = Vector3.Cross(Vector3.up, desiredDirection).normalized;
            var right = -left;
            var wallAway = Vector3.ProjectOnPlane(forwardHit.normal, Vector3.up).normalized;
            if (wallAway.sqrMagnitude <= 0.0001f) wallAway = -desiredDirection;

            var leftProbe = (desiredDirection + left * 0.55f).normalized;
            var rightProbe = (desiredDirection + right * 0.55f).normalized;
            var leftBlocked = Physics.Raycast(origin, leftProbe, WallSideProbeDistance, ~0, QueryTriggerInteraction.Ignore);
            var rightBlocked = Physics.Raycast(origin, rightProbe, WallSideProbeDistance, ~0, QueryTriggerInteraction.Ignore);

            Vector3 side;
            if (leftBlocked && !rightBlocked) side = right;
            else if (rightBlocked && !leftBlocked) side = left;
            else
            {
                avoidSign = -avoidSign;
                side = avoidSign >= 0 ? left : right;
            }

            var steered = desiredDirection + side * 0.9f + wallAway * 1.15f;
            steered.y = 0f;
            if (steered.sqrMagnitude <= 0.0001f) return desiredDirection;
            steered.Normalize();
            return steered;
        }

        private void ApplyInput(float moveX, float moveY, bool sprint, bool stop, bool slide, bool lateralLeft = false, bool lateralRight = false)
        {
            if (playerInput == null) return;

            var x = ToInputInt16(moveX);
            var y = ToInputInt16(moveY);

            playerInput.Client_MoveInputRpc(x, y);
            playerInput.Client_SprintInputRpc(sprint);
            playerInput.Client_StopInputRpc(stop);
            playerInput.Client_TrackInputRpc(false);
            playerInput.Client_SlideInputRpc(slide);
            playerInput.Client_LateralLeftInputRpc(lateralLeft);
            playerInput.Client_LateralRightInputRpc(lateralRight);
        }

        private void ApplyStickInput(Vector3 stickTarget, sbyte bladeAngle)
        {
            if (replayMode) return;
            if (playerInput == null) return;

            if (!TryGetStickAngles(stickTarget, out var stickAngles))
            {
                ApplyBladeInput(bladeAngle);
                return;
            }

            stickAngles = SmoothLiveStickAngles(stickAngles, stickTarget);

            if (hasSentStickAngles)
            {
                var targetShift = Vector3.Distance(lastSentStickTarget, stickTarget);
                var angleShift = Vector2.Distance(lastSentStickAngles, stickAngles);
                if (targetShift <= ApproachStickTargetDeadzone && angleShift <= ApproachStickAngleDeadzone)
                {
                    ApplyBladeInput(bladeAngle);
                    return;
                }
            }

            SendStickAngles(stickAngles, bladeAngle, stickTarget);
        }

        private Vector2 SmoothLiveStickAngles(Vector2 targetStickAngles, Vector3 stickTarget)
        {
            var clampedTargetStickAngles = ClampStickAngles(targetStickAngles);
            if (!hasSmoothedLiveStickAngles)
            {
                smoothedLiveStickAngles = clampedTargetStickAngles;
                hasSmoothedLiveStickAngles = true;
                return smoothedLiveStickAngles;
            }

            var targetShift = Vector3.Distance(lastSentStickTarget, stickTarget);
            var angleShift = Vector2.Distance(smoothedLiveStickAngles, clampedTargetStickAngles);
            if (targetShift <= StickTargetNoiseThreshold && angleShift <= StickAngleNoiseThreshold)
            {
                return smoothedLiveStickAngles;
            }

            var maxAngleDelta = (currentState == BotState.Control || currentState == BotState.Dribble)
                ? ControlStickAngleMaxDeltaPerFrame
                : StickAngleMaxDeltaPerFrame;
            var stickSmoothing = (currentState == BotState.Control || currentState == BotState.Dribble)
                ? ControlStickAngleSmoothing
                : StickAngleSmoothing;

            if (hasBladeOrientationLock && Time.time < bladeOrientationLockedUntil)
            {
                maxAngleDelta = Mathf.Min(maxAngleDelta, 2.2f);
                stickSmoothing = Mathf.Min(stickSmoothing, 0.16f);
            }

            var limitedAngles = new Vector2(
                Mathf.MoveTowardsAngle(smoothedLiveStickAngles.x, clampedTargetStickAngles.x, maxAngleDelta),
                Mathf.MoveTowardsAngle(smoothedLiveStickAngles.y, clampedTargetStickAngles.y, maxAngleDelta));
            smoothedLiveStickAngles = ClampStickAngles(new Vector2(
                Mathf.LerpAngle(smoothedLiveStickAngles.x, limitedAngles.x, stickSmoothing),
                Mathf.LerpAngle(smoothedLiveStickAngles.y, limitedAngles.y, stickSmoothing)));
            return smoothedLiveStickAngles;
        }

        private bool TryGetStickAngles(Vector3 stickTarget, out Vector2 stickAngles)
        {
            stickAngles = Vector2.zero;

            if (stickPositioner == null && controlledPlayer != null)
            {
                stickPositioner = controlledPlayer.StickPositioner;
            }

            if (stickPositioner == null)
            {
                return false;
            }

            var origin = stickPositioner.RaycastOriginPosition;
            var toTarget = stickTarget - origin;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            var localDirection = stickPositioner.transform.InverseTransformDirection(toTarget.normalized);
            if (localDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            var localAngles = WrapEulerAngles(Quaternion.LookRotation(localDirection).eulerAngles);
            stickAngles = ClampStickAngles(new Vector2(localAngles.x, localAngles.y));
            return true;
        }

        private Vector2 ClampStickAngles(Vector2 rawAngles)
        {
            var minimumAngles = playerInput.MinimumStickRaycastOriginAngle;
            var maximumAngles = playerInput.MaximumStickRaycastOriginAngle;
            return new Vector2(
                Mathf.Clamp(rawAngles.x, minimumAngles.x, maximumAngles.x),
                Mathf.Clamp(rawAngles.y, minimumAngles.y, maximumAngles.y));
        }

        private void SendStickAngles(Vector2 stickAngles, sbyte bladeAngle, Vector3 stickTarget)
        {
            playerInput.Client_RaycastOriginAngleInputRpc(ToAngleInt16(stickAngles.x), ToAngleInt16(stickAngles.y));
            lastSentStickAngles = stickAngles;
            lastSentStickTarget = stickTarget;
            hasSentStickAngles = true;
            ApplyBladeInput(bladeAngle);
        }

        private void SendReplayStickAngles(Vector2 stickAngles, sbyte bladeAngle)
        {
            playerInput.Client_RaycastOriginAngleInputRpc(ToAngleInt16(stickAngles.x), ToAngleInt16(stickAngles.y));
            lastSentStickAngles = stickAngles;
            lastSentStickTarget = Vector3.zero;
            hasSentStickAngles = true;
            ApplyBladeInput(bladeAngle);
        }

        private void LogReplayStickFrame(Vector2 replayStickAngles, Vector2 finalStickAngles)
        {
            Debug.Log($"[{Constants.MOD_NAME}] Replay stick frame base=({replayStickAngles.x:F2},{replayStickAngles.y:F2}) final=({finalStickAngles.x:F2},{finalStickAngles.y:F2})");
        }

        private Vector2 ApplyReplayLearningToStickAngles(Vector2 stickAngles)
        {
            return ClampStickAngles(new Vector2(
                stickAngles.x + replaySmoothedLearnedStickDelta.x,
                stickAngles.y + replaySmoothedLearnedStickDelta.y));
        }

        private sbyte ApplyReplayLearningToBladeAngle(sbyte bladeAngle)
        {
            var adjustedBladeAngle = Mathf.Clamp(bladeAngle + replaySmoothedLearnedBladeDelta, -ReplayBladeAngleClamp, ReplayBladeAngleClamp);
            return (sbyte)Mathf.RoundToInt(adjustedBladeAngle);
        }

        private void UpdateReplayLearningAdjustment(ReplayPuckContext puckContext, Vector2 replayStickAngles, sbyte replayBladeAngle, Vector2 moveInput)
        {
            if (!puckContext.IsValid)
            {
                replayLearningTrial = default;
                replaySmoothedLearnedStickDelta = Vector2.Lerp(replaySmoothedLearnedStickDelta, Vector2.zero, ReplayLearningSmoothing);
                replaySmoothedLearnedBladeDelta = Mathf.Lerp(replaySmoothedLearnedBladeDelta, 0f, ReplayLearningSmoothing);
                replayHasPreviousLearningFrame = false;
                return;
            }

            EvaluateReplayLearningTrial(puckContext);

            var targetAdjustment = GetReplayStoredLearningAdjustment(puckContext);
            if (!replayLearningTrial.Active && IsReplayLearningError(puckContext))
            {
                replayLearningTrial = CreateReplayLearningTrial(puckContext, replayStickAngles, replayBladeAngle, moveInput);
            }

            if (replayLearningTrial.Active)
            {
                targetAdjustment = AddReplayLearningAdjustments(targetAdjustment, replayLearningTrial.Sample.Adjustment);
            }

            targetAdjustment = ClampReplayLearningAdjustment(targetAdjustment);
            replaySmoothedLearnedStickDelta = Vector2.Lerp(
                replaySmoothedLearnedStickDelta,
                new Vector2(targetAdjustment.PitchDelta, targetAdjustment.YawDelta),
                ReplayLearningSmoothing);
            replaySmoothedLearnedBladeDelta = Mathf.Lerp(replaySmoothedLearnedBladeDelta, targetAdjustment.BladeDelta, ReplayLearningSmoothing);
        }

        private void EvaluateReplayLearningTrial(ReplayPuckContext puckContext)
        {
            if (!replayLearningTrial.Active)
            {
                return;
            }

            var currentSituation = BuildReplayLearningSituation(puckContext);
            var age = Time.time - replayLearningTrial.StartedAt;
            var isSimilarSituation = IsReplayLearningSituationSimilar(replayLearningTrial.Sample.Situation, currentSituation);
            var improved = isSimilarSituation && puckContext.DistanceBladeToPuck <= replayLearningTrial.Sample.ErrorDistance - ReplayLearningImprovementThreshold;
            var worsened = puckContext.DistanceBladeToPuck >= replayLearningTrial.Sample.ErrorDistance + ReplayLearningRegressionThreshold;

            if (improved)
            {
                StoreReplayLearningSample(replayLearningTrial.Sample);
                replayLearningTrial = default;
                return;
            }

            if (worsened || age >= ReplayLearningEvaluationWindow || !isSimilarSituation)
            {
                replayLearningTrial = default;
            }
        }

        private bool IsReplayLearningError(ReplayPuckContext puckContext)
        {
            if (!puckContext.IsValid)
            {
                return false;
            }

            var expectedContact = IsReplayLearningContactExpected(puckContext);
            var bladeContactMiss = expectedContact && puckContext.DistanceBladeToPuck > ReplayLearningErrorDistance;
            var distanceIncreasedDuringControl = replayHasPreviousLearningFrame
                && replayPreviousExpectedContact
                && expectedContact
                && puckContext.DistanceBladeToPuck > replayPreviousBladeDistance + ReplayLearningRegressionThreshold;
            var shotFailed = replayHasPreviousLearningFrame
                && replayPreviousShotContext
                && !puckContext.HasPuckControl
                && puckContext.DistanceBladeToPuck > ReplayLearningErrorDistance;
            return bladeContactMiss || distanceIncreasedDuringControl || shotFailed;
        }

        private static bool IsReplayLearningContactExpected(ReplayPuckContext puckContext)
        {
            return puckContext.HasPuckControl || puckContext.Zone == ReplayPuckZone.Near || puckContext.Zone == ReplayPuckZone.Control || puckContext.Zone == ReplayPuckZone.Shoot;
        }

        private ReplayLearningTrial CreateReplayLearningTrial(ReplayPuckContext puckContext, Vector2 replayStickAngles, sbyte replayBladeAngle, Vector2 moveInput)
        {
            return new ReplayLearningTrial
            {
                Active = true,
                StartedAt = Time.time,
                Sample = new ReplayLearningSample
                {
                    HasValue = true,
                    Situation = BuildReplayLearningSituation(puckContext),
                    Adjustment = BuildReplayLearningMicroAdjustment(puckContext, replayBladeAngle),
                    ErrorDistance = puckContext.DistanceBladeToPuck,
                    PuckDistance = puckContext.DistanceBotToPuck,
                    StickAngles = replayStickAngles,
                    BladeAngle = replayBladeAngle,
                    MoveInput = moveInput,
                    RelativeDirection = puckContext.RelativeStickPuckAngle,
                    CreatedAt = Time.time,
                }
            };
        }

        private ReplayLearningSituation BuildReplayLearningSituation(ReplayPuckContext puckContext)
        {
            return new ReplayLearningSituation
            {
                Zone = puckContext.Zone,
                DistanceBucket = (sbyte)Mathf.Clamp(Mathf.RoundToInt(puckContext.DistanceBladeToPuck / ReplayLearningDistanceBucketSize), sbyte.MinValue, sbyte.MaxValue),
                AngleBucket = (sbyte)Mathf.Clamp(Mathf.RoundToInt(puckContext.RelativeStickPuckAngle / ReplayLearningAngleBucketSize), sbyte.MinValue, sbyte.MaxValue)
            };
        }

        private ReplayLearningAdjustment BuildReplayLearningMicroAdjustment(ReplayPuckContext puckContext, sbyte replayBladeAngle)
        {
            var yawDelta = Mathf.Clamp(puckContext.RelativeStickPuckAngle * 0.1f, -ReplayLearningYawStep, ReplayLearningYawStep);
            var pitchDelta = -(ReplayLearningPitchStep + Mathf.Clamp(puckContext.BladeHeightAboveGround * 6f, 0f, ReplayLearningPitchStep));
            var bladeDelta = -Mathf.Clamp(replayBladeAngle * 0.15f, -ReplayLearningBladeStep, ReplayLearningBladeStep);
            if (Mathf.Abs(bladeDelta) < 0.1f)
            {
                bladeDelta = -Mathf.Sign(puckContext.RelativeStickPuckAngle) * 0.35f;
            }

            return ClampReplayLearningAdjustment(new ReplayLearningAdjustment
            {
                PitchDelta = pitchDelta,
                YawDelta = yawDelta,
                BladeDelta = bladeDelta
            });
        }

        private ReplayLearningAdjustment GetReplayStoredLearningAdjustment(ReplayPuckContext puckContext)
        {
            if (replayLearningSampleCount <= 0)
            {
                return default;
            }

            var currentSituation = BuildReplayLearningSituation(puckContext);
            var hasBestMatch = false;
            var bestScore = float.MaxValue;
            var bestAdjustment = default(ReplayLearningAdjustment);

            for (var index = 0; index < replayLearningSamples.Length; index++)
            {
                var sample = replayLearningSamples[index];
                if (!sample.HasValue || sample.Situation.Zone != currentSituation.Zone)
                {
                    continue;
                }

                var distanceDelta = Mathf.Abs(sample.Situation.DistanceBucket - currentSituation.DistanceBucket);
                var angleDelta = Mathf.Abs(sample.Situation.AngleBucket - currentSituation.AngleBucket);
                if (distanceDelta > 1 || angleDelta > 1)
                {
                    continue;
                }

                var score = distanceDelta + (angleDelta * 0.75f) + sample.ErrorDistance;
                if (!hasBestMatch || score < bestScore)
                {
                    hasBestMatch = true;
                    bestScore = score;
                    bestAdjustment = sample.Adjustment;
                }
            }

            return hasBestMatch ? bestAdjustment : default;
        }

        private void StoreReplayLearningSample(ReplayLearningSample sample)
        {
            for (var index = 0; index < replayLearningSamples.Length; index++)
            {
                if (!replayLearningSamples[index].HasValue)
                {
                    continue;
                }

                if (IsReplayLearningSituationMatch(replayLearningSamples[index].Situation, sample.Situation))
                {
                    replayLearningSamples[index] = sample;
                    return;
                }
            }

            replayLearningSamples[replayLearningNextIndex] = sample;
            replayLearningNextIndex = (replayLearningNextIndex + 1) % replayLearningSamples.Length;
            replayLearningSampleCount = Mathf.Min(replayLearningSampleCount + 1, replayLearningSamples.Length);
        }

        private static ReplayLearningAdjustment AddReplayLearningAdjustments(ReplayLearningAdjustment left, ReplayLearningAdjustment right)
        {
            return new ReplayLearningAdjustment
            {
                PitchDelta = left.PitchDelta + right.PitchDelta,
                YawDelta = left.YawDelta + right.YawDelta,
                BladeDelta = left.BladeDelta + right.BladeDelta
            };
        }

        private static ReplayLearningAdjustment ClampReplayLearningAdjustment(ReplayLearningAdjustment adjustment)
        {
            adjustment.PitchDelta = Mathf.Clamp(adjustment.PitchDelta, -ReplayLearningMaxPitchDelta, ReplayLearningMaxPitchDelta);
            adjustment.YawDelta = Mathf.Clamp(adjustment.YawDelta, -ReplayLearningMaxYawDelta, ReplayLearningMaxYawDelta);
            adjustment.BladeDelta = Mathf.Clamp(adjustment.BladeDelta, -ReplayLearningMaxBladeDelta, ReplayLearningMaxBladeDelta);
            return adjustment;
        }

        private static bool IsReplayLearningSituationMatch(ReplayLearningSituation left, ReplayLearningSituation right)
        {
            return left.Zone == right.Zone && left.DistanceBucket == right.DistanceBucket && left.AngleBucket == right.AngleBucket;
        }

        private static bool IsReplayLearningSituationSimilar(ReplayLearningSituation left, ReplayLearningSituation right)
        {
            return left.Zone == right.Zone
                && Mathf.Abs(left.DistanceBucket - right.DistanceBucket) <= 1
                && Mathf.Abs(left.AngleBucket - right.AngleBucket) <= 1;
        }

        private void UpdateReplayLearningFrameState(ReplayPuckContext puckContext)
        {
            replayHasPreviousLearningFrame = puckContext.IsValid;
            replayPreviousBladeDistance = puckContext.DistanceBladeToPuck;
            replayPreviousExpectedContact = IsReplayLearningContactExpected(puckContext);
            replayPreviousShotContext = puckContext.Zone == ReplayPuckZone.Shoot;
        }

        private void ResetReplayLearningState()
        {
            replayLearningTrial = default;
            replaySmoothedLearnedStickDelta = Vector2.zero;
            replaySmoothedLearnedBladeDelta = 0f;
            replayHasPreviousLearningFrame = false;
            replayPreviousBladeDistance = 0f;
            replayPreviousExpectedContact = false;
            replayPreviousShotContext = false;
        }

        private void ResetReplayReinforcementState()
        {
            replayPreviousReinforcementStep = default;
            replaySmoothedReinforcementStickDelta = Vector2.zero;
            replaySmoothedReinforcementBladeDelta = 0f;
            replaySmoothedReinforcementMoveX = 0f;
            replayPreviousReinforcementHadControl = false;
        }

        private void ApplyReplayReinforcement(ref Vector2 moveInput, ref Vector2 stickAngles, ref sbyte bladeAngle, ReplayPuckContext puckContext)
        {
            if (!puckContext.IsValid)
            {
                replayPreviousReinforcementStep = default;
                replaySmoothedReinforcementStickDelta = Vector2.Lerp(replaySmoothedReinforcementStickDelta, Vector2.zero, ReplayReinforcementSmoothing);
                replaySmoothedReinforcementBladeDelta = Mathf.Lerp(replaySmoothedReinforcementBladeDelta, 0f, ReplayReinforcementSmoothing);
                replaySmoothedReinforcementMoveX = Mathf.Lerp(replaySmoothedReinforcementMoveX, 0f, ReplayReinforcementSmoothing);
                replayPreviousReinforcementHadControl = false;
                return;
            }

            var currentState = BuildReplayReinforcementState(puckContext, stickAngles);
            var currentStateKey = GetReplayReinforcementStateKey(currentState);
            if (replayPreviousReinforcementStep.HasValue)
            {
                var reward = GetReplayReinforcementReward(puckContext);
                UpdateReplayReinforcementValue(replayPreviousReinforcementStep.StateKey, replayPreviousReinforcementStep.Action, reward, currentStateKey);
            }

            var action = ChooseReplayReinforcementAction(currentStateKey);
            replayPreviousReinforcementStep = new ReplayReinforcementStep
            {
                HasValue = true,
                StateKey = currentStateKey,
                Action = action
            };

            ApplyReplayReinforcementAction(action, ref moveInput, ref stickAngles, ref bladeAngle);
            replayPreviousReinforcementHadControl = puckContext.HasPuckControl;
        }

        private ReplayReinforcementState BuildReplayReinforcementState(ReplayPuckContext puckContext, Vector2 stickAngles)
        {
            var contactDistance = Mathf.Min(puckContext.DistanceStickToPuck, puckContext.DistanceBladeToPuck);
            return new ReplayReinforcementState
            {
                Zone = puckContext.Zone,
                DistanceBucket = QuantizeReplayReinforcementValue(contactDistance, ReplayReinforcementDistanceBucketSize),
                AngleBucket = QuantizeReplayReinforcementValue(puckContext.RelativeStickPuckAngle, ReplayReinforcementAngleBucketSize),
                SpeedBucket = QuantizeReplayReinforcementValue(GetCurrentSpeed(), ReplayReinforcementSpeedBucketSize),
                StickAngleBucket = QuantizeReplayReinforcementValue(stickAngles.y, ReplayReinforcementStickAngleBucketSize)
            };
        }

        private static sbyte QuantizeReplayReinforcementValue(float value, float bucketSize)
        {
            return (sbyte)Mathf.Clamp(Mathf.RoundToInt(value / bucketSize), sbyte.MinValue, sbyte.MaxValue);
        }

        private static string GetReplayReinforcementStateKey(ReplayReinforcementState state)
        {
            return string.Concat(
                ((int)state.Zone).ToString(), "|",
                state.DistanceBucket.ToString(), "|",
                state.AngleBucket.ToString(), "|",
                state.SpeedBucket.ToString(), "|",
                state.StickAngleBucket.ToString());
        }

        private float GetReplayReinforcementReward(ReplayPuckContext puckContext)
        {
            if (replayPreviousReinforcementHadControl && !puckContext.HasPuckControl)
            {
                return -2f;
            }

            if (puckContext.HasPuckControl)
            {
                return 2f;
            }

            if (puckContext.DistanceBladeToPuck <= ReplayStickContactDistance || puckContext.DistanceStickToPuck <= ReplayStickContactDistance)
            {
                return 1f;
            }

            if (IsReplayLearningContactExpected(puckContext) && puckContext.DistanceBladeToPuck > ReplayLearningErrorDistance)
            {
                return -1f;
            }

            return 0f;
        }

        private static ReplayReinforcementAction ChooseReplayReinforcementAction(string stateKey)
        {
            if (UnityEngine.Random.value < ReplayReinforcementEpsilon)
            {
                var actionCount = Enum.GetValues(typeof(ReplayReinforcementAction)).Length;
                return (ReplayReinforcementAction)UnityEngine.Random.Range(0, actionCount);
            }

            lock (replayReinforcementLock)
            {
                if (!replayReinforcementTable.TryGetValue(stateKey, out var stateValues) || stateValues.Count == 0)
                {
                    return ReplayReinforcementAction.None;
                }

                var bestAction = ReplayReinforcementAction.None;
                var bestValue = float.MinValue;
                foreach (var pair in stateValues)
                {
                    if (pair.Value.Value > bestValue)
                    {
                        bestValue = pair.Value.Value;
                        bestAction = pair.Key;
                    }
                }

                return bestAction;
            }
        }

        private static void UpdateReplayReinforcementValue(string stateKey, ReplayReinforcementAction action, float reward, string nextStateKey)
        {
            lock (replayReinforcementLock)
            {
                if (!replayReinforcementTable.TryGetValue(stateKey, out var stateValues))
                {
                    stateValues = new Dictionary<ReplayReinforcementAction, ReplayReinforcementValue>();
                    replayReinforcementTable[stateKey] = stateValues;
                }

                if (!stateValues.TryGetValue(action, out var currentValue))
                {
                    currentValue = default;
                }

                var nextBest = GetReplayReinforcementBestValue(nextStateKey);
                currentValue.Value += ReplayReinforcementAlpha * (reward + (ReplayReinforcementGamma * nextBest) - currentValue.Value);
                currentValue.Visits++;
                stateValues[action] = currentValue;
                replayReinforcementDirty = true;
            }
        }

        private static float GetReplayReinforcementBestValue(string stateKey)
        {
            if (string.IsNullOrWhiteSpace(stateKey))
            {
                return 0f;
            }

            if (!replayReinforcementTable.TryGetValue(stateKey, out var stateValues) || stateValues.Count == 0)
            {
                return 0f;
            }

            var bestValue = float.MinValue;
            foreach (var pair in stateValues)
            {
                if (pair.Value.Value > bestValue)
                {
                    bestValue = pair.Value.Value;
                }
            }

            return bestValue == float.MinValue ? 0f : bestValue;
        }

        private void ApplyReplayReinforcementAction(ReplayReinforcementAction action, ref Vector2 moveInput, ref Vector2 stickAngles, ref sbyte bladeAngle)
        {
            var targetStickDelta = Vector2.zero;
            var targetBladeDelta = 0f;
            var targetMoveXDelta = 0f;

            switch (action)
            {
                case ReplayReinforcementAction.StickYawLeft:
                    targetStickDelta.y = -ReplayReinforcementStickYawDelta;
                    break;
                case ReplayReinforcementAction.StickYawRight:
                    targetStickDelta.y = ReplayReinforcementStickYawDelta;
                    break;
                case ReplayReinforcementAction.StickLower:
                    targetStickDelta.x = -ReplayReinforcementStickPitchDelta;
                    break;
                case ReplayReinforcementAction.BladeDown:
                    targetBladeDelta = -ReplayReinforcementBladeDelta;
                    break;
                case ReplayReinforcementAction.BladeUp:
                    targetBladeDelta = ReplayReinforcementBladeDelta;
                    break;
                case ReplayReinforcementAction.MoveLeft:
                    targetMoveXDelta = -ReplayReinforcementMoveXDelta;
                    break;
                case ReplayReinforcementAction.MoveRight:
                    targetMoveXDelta = ReplayReinforcementMoveXDelta;
                    break;
            }

            replaySmoothedReinforcementStickDelta = Vector2.Lerp(replaySmoothedReinforcementStickDelta, targetStickDelta, ReplayReinforcementSmoothing);
            replaySmoothedReinforcementBladeDelta = Mathf.Lerp(replaySmoothedReinforcementBladeDelta, targetBladeDelta, ReplayReinforcementSmoothing);
            replaySmoothedReinforcementMoveX = Mathf.Lerp(replaySmoothedReinforcementMoveX, targetMoveXDelta, ReplayReinforcementSmoothing);

            moveInput.x = Mathf.Clamp(moveInput.x + replaySmoothedReinforcementMoveX, -1f, 1f);
            stickAngles = ClampStickAngles(new Vector2(
                stickAngles.x + replaySmoothedReinforcementStickDelta.x,
                stickAngles.y + replaySmoothedReinforcementStickDelta.y));

            var adjustedBladeAngle = Mathf.Clamp(bladeAngle + replaySmoothedReinforcementBladeDelta, -ReplayBladeAngleClamp, ReplayBladeAngleClamp);
            bladeAngle = (sbyte)Mathf.RoundToInt(adjustedBladeAngle);
        }

        private static void EnsureReplayReinforcementLoaded()
        {
            lock (replayReinforcementLock)
            {
                if (replayReinforcementLoaded)
                {
                    return;
                }

                replayReinforcementLoaded = true;
                replayReinforcementTable.Clear();

                try
                {
                    var binaryPath = GetReplayReinforcementBinaryPath();
                    if (File.Exists(binaryPath))
                    {
                        if (!TryLoadReplayReinforcementBinary(binaryPath))
                        {
                            Debug.LogError($"[{Constants.MOD_NAME}] Bot learning binary load failed: {Path.GetFileName(binaryPath)}");
                        }

                        return;
                    }

                    var legacyPath = GetReplayReinforcementLegacyJsonPath();
                    if (!File.Exists(legacyPath))
                    {
                        return;
                    }

                    var json = File.ReadAllText(legacyPath);
                    var file = JsonConvert.DeserializeObject<ReplayReinforcementFile>(json);
                    if (file?.Entries == null)
                    {
                        return;
                    }

                    PopulateReplayReinforcementTable(file);
                    TryWriteReplayReinforcementBinary(binaryPath);
                    TryWriteReplayReinforcementDebugJson(GetReplayReinforcementDebugJsonPath());
                    File.Delete(legacyPath);
                    replayReinforcementDirty = false;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{Constants.MOD_NAME}] Bot learning load failed: {ex.Message}");
                }
            }
        }

        private static void MaybeSaveReplayReinforcement(bool force = false)
        {
            lock (replayReinforcementLock)
            {
                if (!replayReinforcementLoaded || !replayReinforcementDirty || (!force && Time.time < replayReinforcementNextSaveTime))
                {
                    return;
                }

                try
                {
                    var binaryPath = GetReplayReinforcementBinaryPath();
                    if (!TryWriteReplayReinforcementBinary(binaryPath))
                    {
                        return;
                    }

                    TryWriteReplayReinforcementDebugJson(GetReplayReinforcementDebugJsonPath());
                    var legacyPath = GetReplayReinforcementLegacyJsonPath();
                    if (File.Exists(legacyPath))
                    {
                        File.Delete(legacyPath);
                    }

                    replayReinforcementDirty = false;
                    replayReinforcementNextSaveTime = Time.time + ReplayReinforcementSaveInterval;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{Constants.MOD_NAME}] Bot learning save failed: {ex.Message}");
                }
            }
        }

        private static string GetReplayReinforcementBinaryPath()
        {
            return Path.Combine(GetReplayReinforcementDirectoryPath(), "Learning.bin");
        }

        private static string GetReplayReinforcementLegacyJsonPath()
        {
            return Path.Combine(GetReplayReinforcementDirectoryPath(), "Learning.json");
        }

        private static string GetReplayReinforcementDebugJsonPath()
        {
            return Path.Combine(GetReplayReinforcementDirectoryPath(), "Learning.debug.json");
        }

        private static string GetReplayReinforcementDirectoryPath()
        {
            var root = GetReplayReinforcementRootPath();
            var userDataDirectory = Path.Combine(root, "UserData");
            Directory.CreateDirectory(userDataDirectory);
            var botMemoryDirectory = Path.Combine(userDataDirectory, "BotMemory");
            Directory.CreateDirectory(botMemoryDirectory);
            return botMemoryDirectory;
        }

        private static bool TryLoadReplayReinforcementBinary(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != ReplayReinforcementBinaryMagic)
                {
                    return false;
                }

                if (reader.ReadInt32() != ReplayReinforcementBinaryVersion)
                {
                    return false;
                }

                var entryCount = reader.ReadInt32();
                if (entryCount < 0)
                {
                    return false;
                }

                for (var index = 0; index < entryCount; index++)
                {
                    var state = new ReplayReinforcementState
                    {
                        Zone = (ReplayPuckZone)reader.ReadByte(),
                        DistanceBucket = reader.ReadSByte(),
                        AngleBucket = reader.ReadSByte(),
                        SpeedBucket = reader.ReadSByte(),
                        StickAngleBucket = reader.ReadSByte()
                    };
                    var action = (ReplayReinforcementAction)reader.ReadByte();
                    var value = reader.ReadSingle();
                    var visits = reader.ReadInt32();
                    var stateKey = GetReplayReinforcementStateKey(state);

                    if (!replayReinforcementTable.TryGetValue(stateKey, out var stateValues))
                    {
                        stateValues = new Dictionary<ReplayReinforcementAction, ReplayReinforcementValue>();
                        replayReinforcementTable[stateKey] = stateValues;
                    }

                    stateValues[action] = new ReplayReinforcementValue
                    {
                        Value = value,
                        Visits = visits
                    };
                }
            }

            return true;
        }

        private static bool TryWriteReplayReinforcementBinary(string filePath)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(ReplayReinforcementBinaryMagic);
                    writer.Write(ReplayReinforcementBinaryVersion);

                    var entryCount = 0;
                    foreach (var statePair in replayReinforcementTable)
                    {
                        if (!TryParseReplayReinforcementStateKey(statePair.Key, out _))
                        {
                            continue;
                        }

                        entryCount += statePair.Value.Count;
                    }

                    writer.Write(entryCount);

                    foreach (var statePair in replayReinforcementTable)
                    {
                        if (!TryParseReplayReinforcementStateKey(statePair.Key, out var state))
                        {
                            continue;
                        }

                        foreach (var actionPair in statePair.Value)
                        {
                            writer.Write((byte)state.Zone);
                            writer.Write(state.DistanceBucket);
                            writer.Write(state.AngleBucket);
                            writer.Write(state.SpeedBucket);
                            writer.Write(state.StickAngleBucket);
                            writer.Write((byte)actionPair.Key);
                            writer.Write(actionPair.Value.Value);
                            writer.Write(actionPair.Value.Visits);
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Bot learning binary save failed: {ex.Message}");
                return false;
            }
        }

        private static void TryWriteReplayReinforcementDebugJson(string filePath)
        {
            try
            {
                var file = new ReplayReinforcementFile
                {
                    SavedUtc = DateTime.UtcNow.ToString("o")
                };

                foreach (var statePair in replayReinforcementTable)
                {
                    foreach (var actionPair in statePair.Value)
                    {
                        file.Entries.Add(new ReplayReinforcementEntry
                        {
                            StateKey = statePair.Key,
                            Action = actionPair.Key,
                            Value = actionPair.Value.Value,
                            Visits = actionPair.Value.Visits
                        });
                    }
                }

                File.WriteAllText(filePath, JsonConvert.SerializeObject(file, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] Bot learning debug JSON save failed: {ex.Message}");
            }
        }

        private static void PopulateReplayReinforcementTable(ReplayReinforcementFile file)
        {
            replayReinforcementTable.Clear();

            if (file?.Entries == null)
            {
                return;
            }

            for (var index = 0; index < file.Entries.Count; index++)
            {
                var entry = file.Entries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.StateKey))
                {
                    continue;
                }

                if (!replayReinforcementTable.TryGetValue(entry.StateKey, out var stateValues))
                {
                    stateValues = new Dictionary<ReplayReinforcementAction, ReplayReinforcementValue>();
                    replayReinforcementTable[entry.StateKey] = stateValues;
                }

                stateValues[entry.Action] = new ReplayReinforcementValue
                {
                    Value = entry.Value,
                    Visits = entry.Visits
                };
            }
        }

        private static bool TryParseReplayReinforcementStateKey(string stateKey, out ReplayReinforcementState state)
        {
            state = default;
            if (string.IsNullOrWhiteSpace(stateKey))
            {
                return false;
            }

            var parts = stateKey.Split('|');
            if (parts.Length != 5)
            {
                return false;
            }

            if (!byte.TryParse(parts[0], out var zone)
                || !sbyte.TryParse(parts[1], out var distanceBucket)
                || !sbyte.TryParse(parts[2], out var angleBucket)
                || !sbyte.TryParse(parts[3], out var speedBucket)
                || !sbyte.TryParse(parts[4], out var stickAngleBucket))
            {
                return false;
            }

            state = new ReplayReinforcementState
            {
                Zone = (ReplayPuckZone)zone,
                DistanceBucket = distanceBucket,
                AngleBucket = angleBucket,
                SpeedBucket = speedBucket,
                StickAngleBucket = stickAngleBucket
            };

            return true;
        }

        private static string GetReplayReinforcementRootPath()
        {
            try
            {
                var dataPath = Application.dataPath;
                if (!string.IsNullOrWhiteSpace(dataPath))
                {
                    var root = Path.GetDirectoryName(dataPath);
                    if (!string.IsNullOrWhiteSpace(root))
                    {
                        return root;
                    }
                }
            }
            catch
            {
            }

            return Path.GetFullPath(".");
        }

        private Vector2 GetReplayCorrectedStickAngles(Vector2 recordedStickAngles, ReplayPuckContext puckContext, Vector2 correctedMoveInput, bool sprint)
        {
            if (!puckContext.IsValid)
            {
                replayStickYawOffset = Mathf.MoveTowards(replayStickYawOffset, 0f, ReplayStickOffsetSlew);
                return SmoothReplayStickAngles(GetLowStickFallbackAngles());
            }

            if (!TryGetReplayGoalieStickAngles(puckContext, out var goalieStickAngles))
            {
                replayStickYawOffset = Mathf.MoveTowards(replayStickYawOffset, 0f, ReplayStickOffsetSlew);
                return SmoothReplayStickAngles(GetLowStickFallbackAngles());
            }

            if (puckContext.Zone == ReplayPuckZone.Far)
            {
                replayStickYawOffset = Mathf.MoveTowards(replayStickYawOffset, 0f, ReplayStickOffsetSlew);
                return SmoothReplayStickAngles(goalieStickAngles);
            }

            var styleBlend = GetReplayStickStyleBlend(puckContext);
            var pitchOffset = Mathf.Clamp(Mathf.DeltaAngle(goalieStickAngles.x, recordedStickAngles.x), -ReplayStylePitchClamp, ReplayStylePitchClamp) * (ReplayStylePitchBlend * styleBlend);
            var targetYawOffset = Mathf.Clamp(Mathf.DeltaAngle(goalieStickAngles.y, recordedStickAngles.y) * (ReplayStyleYawBlend * styleBlend), -ReplayStyleYawClamp, ReplayStyleYawClamp);
            replayStickYawOffset = Mathf.MoveTowards(replayStickYawOffset, targetYawOffset, ReplayStickOffsetSlew);

            var finalPitch = goalieStickAngles.x + pitchOffset;
            if (puckContext.HasPuckControl || puckContext.Zone == ReplayPuckZone.Control || puckContext.Zone == ReplayPuckZone.Shoot || puckContext.Zone == ReplayPuckZone.Near)
            {
                finalPitch = Mathf.Min(finalPitch, GetLowStickFallbackAngles().x);
            }

            var finalYaw = goalieStickAngles.y + replayStickYawOffset;
            if (sprint || correctedMoveInput.y >= 0.75f)
            {
                finalYaw = Mathf.LerpAngle(goalieStickAngles.y, finalYaw, 0.35f);
            }

            return SmoothReplayStickAngles(new Vector2(finalPitch, finalYaw));
        }

        private sbyte GetReplayCorrectedBladeAngle(sbyte replayBladeAngle, ReplayPuckContext puckContext, Vector2 correctedMoveInput, bool sprint, bool dribbleMode)
        {
            var contextFactor = GetReplayTiltContextFactor(puckContext);
            var styleBlend = GetReplayStickStyleBlend(puckContext);
            var targetTilt = Mathf.Clamp(replayBladeAngle * contextFactor * ReplayStyleBladeBlend * styleBlend, -ReplayBladeForwardClamp, ReplayBladeForwardClamp);

            if (puckContext.IsValid && puckContext.Zone == ReplayPuckZone.Shoot)
            {
                targetTilt *= GetReplayShotReadyFactor(puckContext);
            }

            if (sprint || correctedMoveInput.y >= 0.75f)
            {
                targetTilt = Mathf.Clamp(targetTilt, -ReplayBladeForwardClamp, ReplayBladeForwardClamp);
            }

            if (dribbleMode)
            {
                targetTilt = Mathf.Clamp(targetTilt, -ReplayBladeForwardClamp, ReplayBladeForwardClamp);
            }

            replaySmoothedBladeAngle = Mathf.Lerp(replaySmoothedBladeAngle, targetTilt, ReplayBladeSmoothing);
            return (sbyte)Mathf.RoundToInt(replaySmoothedBladeAngle);
        }

        private float GetReplayStickStyleBlend(ReplayPuckContext puckContext)
        {
            if (!puckContext.IsValid)
            {
                return 0f;
            }

            switch (puckContext.Zone)
            {
                case ReplayPuckZone.Shoot:
                    return 0.2f;
                case ReplayPuckZone.Control:
                    return 0.22f;
                case ReplayPuckZone.Near:
                    return 0.28f;
                case ReplayPuckZone.Far:
                    return 0.12f;
                default:
                    return 0.2f;
            }
        }

        private Vector2 SmoothReplayStickAngles(Vector2 targetStickAngles)
        {
            var clampedTargetStickAngles = ClampStickAngles(targetStickAngles);
            if (!replayHasSmoothedStickAngles)
            {
                replaySmoothedStickAngles = clampedTargetStickAngles;
                replayHasSmoothedStickAngles = true;
                return replaySmoothedStickAngles;
            }

            replaySmoothedStickAngles = new Vector2(
                Mathf.LerpAngle(replaySmoothedStickAngles.x, clampedTargetStickAngles.x, ReplayStickTargetSmoothing),
                Mathf.LerpAngle(replaySmoothedStickAngles.y, clampedTargetStickAngles.y, ReplayStickTargetSmoothing));
            return ClampStickAngles(replaySmoothedStickAngles);
        }

        private float GetReplayTiltContextFactor(ReplayPuckContext puckContext)
        {
            if (!puckContext.IsValid || puckContext.Zone == ReplayPuckZone.Far)
            {
                return 0f;
            }

            if (puckContext.HasPuckControl || puckContext.Zone == ReplayPuckZone.Control || puckContext.Zone == ReplayPuckZone.Shoot)
            {
                return 1f;
            }

            return ReplayTiltNearFactor;
        }

        private void ApplyLowStickFallback(sbyte bladeAngle)
        {
            if (playerInput == null)
            {
                return;
            }

            var lowStickAngles = GetLowStickFallbackAngles();
            playerInput.Client_RaycastOriginAngleInputRpc(ToAngleInt16(lowStickAngles.x), ToAngleInt16(lowStickAngles.y));
            lastSentStickAngles = lowStickAngles;
            lastSentStickTarget = Vector3.zero;
            smoothedLiveStickAngles = lowStickAngles;
            hasSmoothedLiveStickAngles = true;
            hasSentStickAngles = true;
            ApplyBladeInput(bladeAngle);
        }

        private Vector2 GetLowStickFallbackAngles()
        {
            if (stickPositioner == null && controlledPlayer != null)
            {
                stickPositioner = controlledPlayer.StickPositioner;
            }

            var botPosition = GetBotPosition();
            var lowStickTarget = botPosition + GetFlatForward() * LowStickForwardDistance;
            lowStickTarget.y = botPosition.y + StickIceLift;
            if (TryGetStickAngles(lowStickTarget, out var lowStickAngles))
            {
                return lowStickAngles;
            }

            var minimumAngles = playerInput.MinimumStickRaycastOriginAngle;
            return new Vector2(minimumAngles.x, 0f);
        }

        private void ApplyBladeInput(sbyte bladeAngle)
        {
            if (playerInput == null) return;
            playerInput.Client_BladeAngleInputRpc(bladeAngle);
        }

        internal void SetReplayMode(bool enabled)
        {
            replayMode = enabled;
            if (!enabled)
            {
                replayStickYawOffset = 0f;
                replaySmoothedBladeAngle = 0f;
                replaySmoothedMoveX = 0f;
                replayLastMoveX = 0f;
                replayLastRecordedStickAngles = Vector2.zero;
                replayHasRecordedFrame = false;
                replayDribbleUntilTime = 0f;
                replaySmoothedStickAngles = Vector2.zero;
                replayHasSmoothedStickAngles = false;
                hasSentStickAngles = false;
                hasSmoothedLiveStickAngles = false;
                controlStableSince = -1f;
                dribbleStableSince = -1f;
                ResetReplayLearningState();
                ResetReplayReinforcementState();
                ClearApproachTarget();
                TryResetInputs();
                return;
            }

            ClearApproachTarget();
            replayStickYawOffset = 0f;
            replaySmoothedBladeAngle = 0f;
            replaySmoothedMoveX = 0f;
            replayLastMoveX = 0f;
            replayLastRecordedStickAngles = Vector2.zero;
            replayHasRecordedFrame = false;
            replayDribbleUntilTime = 0f;
            replaySmoothedStickAngles = Vector2.zero;
            replayHasSmoothedStickAngles = false;
            hasSentStickAngles = false;
            hasSmoothedLiveStickAngles = false;
            controlStableSince = -1f;
            dribbleStableSince = -1f;
            ResetReplayLearningState();
            ResetReplayReinforcementState();
            smoothedTurnInput = 0f;
            lastTurnSign = 0;
        }

        internal bool IsReplayModeEnabled()
        {
            return replayMode;
        }

        internal ReplayPatternType GetReplayPatternType()
        {
            if (!IsServer() || !EnsureReady() || !IsGameplayPhase()) return ReplayPatternType.Unknown;

            if (!TryBuildReplayPuckContext(out var puckContext))
            {
                return ReplayPatternType.Unknown;
            }

            if (puckContext.Zone == ReplayPuckZone.Far || puckContext.Zone == ReplayPuckZone.Near)
            {
                return ReplayPatternType.Unknown;
            }

            if (puckContext.Zone == ReplayPuckZone.Shoot)
            {
                return ReplayPatternType.Shoot;
            }

            if (puckContext.HasPuckControl)
            {
                return GetAttackReplayPatternType(puckContext.BotPosition);
            }

            return ReplayPatternType.Control;
        }

        internal Vector3 GetRuntimeBotPosition()
        {
            return GetBotPosition();
        }

        internal Player GetControlledPlayer()
        {
            return controlledPlayer;
        }

        internal PlayerInput GetControlledPlayerInput()
        {
            return playerInput;
        }

        internal bool TryBuildReplayLearningSnapshot(out ReplayLearningSnapshot snapshot)
        {
            snapshot = default;

            if (!IsServer() || !EnsureReady() || !IsGameplayPhase() || controlledPlayer == null || playerInput == null)
            {
                return false;
            }

            if (!TryBuildReplayPuckContext(out var puckContext))
            {
                return false;
            }

            var localPuckPosition = GetLocalPuckPosition(puckContext.PuckPosition);
            var controlVector = hasStableControlVector
                ? stableControlVector
                : hasApproachTarget
                    ? cachedApproachControlVector
                    : GetDesiredPuckTravelDirection(puckContext.PuckPosition);
            controlVector.y = 0f;
            if (controlVector.sqrMagnitude <= 0.0001f)
            {
                controlVector = GetFlatForward();
            }

            controlVector.Normalize();
            var forward = GetFlatForward();
            var playerVelocity = playerBody != null && playerBody.Rigidbody != null ? playerBody.Rigidbody.linearVelocity : Vector3.zero;
            playerVelocity.y = 0f;

            var puckVelocity = Vector3.zero;
            var puckManager = NetworkBehaviourSingleton<PuckManager>.Instance;
            var puck = puckManager != null ? GetClosestPuck(puckManager) : null;
            if (puck != null && puck.Rigidbody != null)
            {
                puckVelocity = puck.Rigidbody.linearVelocity;
                puckVelocity.y = 0f;
            }

            var nearWall = TryGetWallOpenDirection(puckContext.PuckPosition, controlVector, out var wallOpenDirection, out _);
            var hitPuck = puckContext.DistanceBladeToPuck <= ReplayStickContactDistance;
            var badAngleContact = hitPuck && !IsBladeAlignedWithDirection(puckContext, controlVector, ReplayStickAlignmentAngle);
            var overshoot = IsOvershootingPuck(localPuckPosition, puckContext);
            var puckPassedUnderBlade = puckContext.DistanceBladeToPuck <= BladePuckNearDistance
                && puckContext.BladeHeightAboveGround > BladeGroundHeightThreshold * 1.35f;
            var closeEngagement = puckContext.DistanceBotToPuck <= ReplayPuckNearDistance || puckContext.DistanceBladeToPuck <= BladePuckNearDistance;
            var missedContact = closeEngagement && !hitPuck && !puckContext.HasPuckControl && currentState != BotState.Chase;

            var alignmentScore = 1f - Mathf.Clamp01(Mathf.Abs(Mathf.DeltaAngle(puckContext.RelativeStickPuckAngle, 0f)) / Mathf.Max(ReplayStickAlignmentAngle, 1f));
            var distanceScore = 1f - Mathf.Clamp01(Mathf.InverseLerp(ReplayStickControlDistance, ReplayStickContactDistance, puckContext.DistanceBladeToPuck));
            var velocityScore = 1f - Mathf.Clamp01(Mathf.InverseLerp(0f, PossessionLooseRelativeSpeedThreshold * 1.35f, puckContext.RelativePuckSpeed));
            var contactQuality = Mathf.Clamp01((alignmentScore * 0.4f) + (distanceScore * 0.35f) + (velocityScore * 0.25f));

            var bodyAlignmentScore = 1f - Mathf.Clamp01(Vector3.Angle(forward, controlVector) / Mathf.Max(ApproachBodyAlignmentAngleThreshold * 2f, 1f));
            var behindPuckScore = Mathf.Clamp01(Mathf.InverseLerp(ControlOvershootFrontThreshold, ContactCorridorFrontMax, localPuckPosition.z));
            var lateralScore = 1f - Mathf.Clamp01(Mathf.Abs(localPuckPosition.x) / Mathf.Max(ContactCorridorLateralThreshold * 2f, 0.01f));
            var approachQuality = Mathf.Clamp01((bodyAlignmentScore * 0.35f) + (behindPuckScore * 0.4f) + (lateralScore * 0.25f));

            var controlDurationSeconds = 0f;
            if (currentState == BotState.Control && controlStableSince >= 0f)
            {
                controlDurationSeconds = Mathf.Max(0f, Time.time - controlStableSince);
            }
            else if (currentState == BotState.Dribble && dribbleStableSince >= 0f)
            {
                controlDurationSeconds = Mathf.Max(0f, Time.time - dribbleStableSince);
            }

            var controlStability = Mathf.Clamp01(Mathf.Max(
                controlDurationSeconds / Mathf.Max(DribbleStableDuration, 0.01f),
                puckContext.HasPuckControl ? 0.5f : 0f));
            var timingError = Mathf.Clamp(localPuckPosition.z - ((ContactCorridorFrontMin + ContactCorridorFrontMax) * 0.5f), -1f, 1f);
            var sideSelection = hasApproachTarget ? cachedApproachSideSign : stickSideSign;

            snapshot = new ReplayLearningSnapshot
            {
                IsValid = true,
                HasPuckContext = true,
                PuckDistanceToBlade = puckContext.DistanceBladeToPuck,
                PuckDistanceToPlayer = puckContext.DistanceBotToPuck,
                PuckVelocityMagnitude = puckVelocity.magnitude,
                PuckVelocityAngle = puckVelocity.sqrMagnitude > 0.0001f ? Vector3.SignedAngle(forward, puckVelocity.normalized, Vector3.up) : 0f,
                PlayerVelocityMagnitude = playerVelocity.magnitude,
                PlayerVelocityAngle = playerVelocity.sqrMagnitude > 0.0001f ? Vector3.SignedAngle(forward, playerVelocity.normalized, Vector3.up) : 0f,
                RelativeAnglePlayerToPuck = Vector3.SignedAngle(forward, puckContext.DirectionBotToPuck, Vector3.up),
                RelativeAnglePuckToGoal = Vector3.SignedAngle(forward, controlVector, Vector3.up),
                PuckSide = localPuckPosition.x < -SideSelectionDeadzone ? (sbyte)(-1) : localPuckPosition.x > SideSelectionDeadzone ? (sbyte)1 : (sbyte)0,
                PuckForward = localPuckPosition.z < -ControlOvershootFrontThreshold ? (sbyte)(-1) : localPuckPosition.z > ControlOvershootFrontThreshold ? (sbyte)1 : (sbyte)0,
                NearWall = nearWall,
                WallNormalAngle = nearWall ? Vector3.SignedAngle(forward, wallOpenDirection, Vector3.up) : 0f,
                FsmState = (sbyte)currentState,
                ControlVectorAngle = Vector3.SignedAngle(forward, controlVector, Vector3.up),
                HasApproachTarget = hasApproachTarget,
                ApproachTargetDistance = hasApproachTarget ? Vector3.Distance(GetBotPosition(), cachedApproachTarget) : 0f,
                ApproachTargetAngle = hasApproachTarget
                    ? Vector3.SignedAngle(forward, (cachedApproachTarget - GetBotPosition()).normalized, Vector3.up)
                    : 0f,
                SideSelection = (sbyte)sideSelection,
                BehaviorIntent = GetReplayBehaviorIntent(),
                HitPuck = hitPuck,
                PuckControlled = puckContext.HasPuckControl,
                ControlDurationSeconds = controlDurationSeconds,
                MissedContact = missedContact,
                Overshoot = overshoot,
                PuckPassedUnderBlade = puckPassedUnderBlade,
                BadAngleContact = badAngleContact,
                LossOfControl = false,
                ContactQuality = contactQuality,
                ApproachQuality = approachQuality,
                ControlStability = controlStability,
                TimingError = timingError,
                WallExtractionCandidate = nearWall,
                WallExtractionSuccess = nearWall && puckContext.HasPuckControl && contactQuality >= 0.55f
            };

            return true;
        }

        private ReplayBehaviorIntent GetReplayBehaviorIntent()
        {
            switch (currentState)
            {
                case BotState.Chase:
                case BotState.Align:
                    return ReplayBehaviorIntent.AcquirePuck;
                case BotState.Control:
                    return ReplayBehaviorIntent.ControlPuck;
                case BotState.Dribble:
                    return ReplayBehaviorIntent.PushForward;
                case BotState.Shoot:
                    return ReplayBehaviorIntent.Shoot;
                case BotState.Recover:
                    return ReplayBehaviorIntent.Recover;
                default:
                    return ReplayBehaviorIntent.None;
            }
        }

        internal bool TryGetAutonomousReplayRecordingProfile(out ReplayPatternType patternType, out bool shouldRecord)
        {
            patternType = ReplayPatternType.Unknown;
            shouldRecord = false;

            if (!IsServer() || !EnsureReady() || !IsGameplayPhase() || controlledPlayer == null || playerInput == null)
            {
                return false;
            }

            if (!TryBuildReplayPuckContext(out var puckContext))
            {
                return false;
            }

            patternType = GetReplayPatternType();
            if (patternType == ReplayPatternType.Unknown)
            {
                patternType = ReplayPatternType.Move;
            }

            shouldRecord = true;

            return true;
        }

        internal bool IsReplayStuck(float recordedMoveMagnitude)
        {
            if (recordedMoveMagnitude <= 0.2f) return false;
            return GetCurrentSpeed() <= 0.18f;
        }

        internal bool IsReplayFrameContextMatch(RecordedInputFrame frame, ReplayPatternType patternType)
        {
            if (!frame.HasPuckStickRelation) return true;
            if (!TryGetCurrentPuckStickLocalRelation(out var currentDistanceToStick, out var currentDirectionAngle, out var currentRelativeSide))
            {
                return false;
            }

            var distanceThreshold = GetReplayFrameDistanceThreshold(patternType);
            var angleThreshold = patternType == ReplayPatternType.TurnDribble ? ReplayFrameDribbleAngleThreshold : ReplayFrameAngleThreshold;
            var distanceDelta = Mathf.Abs(currentDistanceToStick - frame.PuckDistanceToStick);
            var angleDelta = Mathf.Abs(Mathf.DeltaAngle(currentDirectionAngle, frame.PuckDirectionToStickAngle));
            var sideMatches = frame.PuckRelativeSide == 0 || currentRelativeSide == 0 || frame.PuckRelativeSide == currentRelativeSide;
            return distanceDelta <= distanceThreshold && angleDelta <= angleThreshold && sideMatches;
        }

        internal void ApplyRecordedFrame(RecordedInputFrame frame)
        {
            ApplyRecordedFrame(frame, ReplayPatternType.Unknown);
        }

        internal void ApplyRecordedFrame(RecordedInputFrame frame, ReplayPatternType patternType)
        {
            if (!IsServer()) return;
            if (!EnsureReady() || playerInput == null) return;

            EnsureReplayReinforcementLoaded();

            var replayStickAngles = ClampStickAngles(new Vector2(frame.StickAngleX, frame.StickAngleY));
            var hasPuckContext = TryBuildReplayPuckContext(out var puckContext);
            var dribbleMode = hasPuckContext && UpdateReplayDribbleMode(new Vector2(frame.MoveX, frame.MoveY), replayStickAngles, puckContext);
            var correctedMoveInput = GetReplayCorrectedMoveInput(new Vector2(frame.MoveX, frame.MoveY), patternType, hasPuckContext ? puckContext : default, dribbleMode);
            UpdateReplayLearningAdjustment(hasPuckContext ? puckContext : default, replayStickAngles, frame.BladeAngle, correctedMoveInput);
            var finalStickAngles = GetReplayCorrectedStickAngles(replayStickAngles, hasPuckContext ? puckContext : default, correctedMoveInput, frame.Sprint);
            finalStickAngles = ApplyReplayLearningToStickAngles(finalStickAngles);
            var finalBladeAngle = GetReplayCorrectedBladeAngle(frame.BladeAngle, hasPuckContext ? puckContext : default, correctedMoveInput, frame.Sprint, dribbleMode);
            finalBladeAngle = ApplyReplayLearningToBladeAngle(finalBladeAngle);
            ApplyReplayReinforcement(ref correctedMoveInput, ref finalStickAngles, ref finalBladeAngle, hasPuckContext ? puckContext : default);
            ApplyReplayPhysicalStickPriority(ref finalStickAngles, hasPuckContext ? puckContext : default);
            UpdateReplayLearningFrameState(hasPuckContext ? puckContext : default);
            var lateralLeft = dribbleMode ? correctedMoveInput.x <= -0.14f : frame.LateralLeft;
            var lateralRight = dribbleMode ? correctedMoveInput.x >= 0.14f : frame.LateralRight;
            var sprintInput = frame.Sprint && (!hasPuckContext || puckContext.Zone == ReplayPuckZone.Far || puckContext.Zone == ReplayPuckZone.Shoot) && !dribbleMode;
            playerInput.Client_MoveInputRpc(ToInputInt16(correctedMoveInput.x), ToInputInt16(correctedMoveInput.y));
            playerInput.Client_SprintInputRpc(sprintInput);
            playerInput.Client_StopInputRpc(frame.Control);
            playerInput.Client_TrackInputRpc(false);
            playerInput.Client_SlideInputRpc(frame.Slide);
            playerInput.Client_LateralLeftInputRpc(lateralLeft);
            playerInput.Client_LateralRightInputRpc(lateralRight);
            SendReplayStickAngles(finalStickAngles, finalBladeAngle);
            MaybeSaveReplayReinforcement();
            LogReplayStickFrame(replayStickAngles, finalStickAngles);
        }

        private void ApplyReplayPhysicalStickPriority(ref Vector2 stickAngles, ReplayPuckContext puckContext)
        {
            if (!puckContext.IsValid)
            {
                stickAngles = GetLowStickFallbackAngles();
                return;
            }

            var physicalPriority = GetReplayPhysicalPriority(puckContext);
            if (physicalPriority > 0.001f)
            {
                var physicalStickTarget = BuildReplayPhysicalStickTarget(puckContext);
                if (TryGetStickAngles(physicalStickTarget, out var physicalStickAngles))
                {
                    stickAngles = BlendStickAngles(stickAngles, physicalStickAngles, physicalPriority);
                }
            }

            if (puckContext.HasPuckControl || puckContext.Zone == ReplayPuckZone.Control || puckContext.Zone == ReplayPuckZone.Shoot || puckContext.Zone == ReplayPuckZone.Near)
            {
                var lowStickAngles = GetLowStickFallbackAngles();
                stickAngles = ClampStickAngles(new Vector2(Mathf.Min(stickAngles.x, lowStickAngles.x), stickAngles.y));
            }
        }

        private Vector2 BlendStickAngles(Vector2 baseAngles, Vector2 priorityAngles, float priority)
        {
            priority = Mathf.Clamp01(priority);
            return ClampStickAngles(new Vector2(
                Mathf.LerpAngle(baseAngles.x, priorityAngles.x, priority),
                Mathf.LerpAngle(baseAngles.y, priorityAngles.y, priority)));
        }

        private bool TryBuildReplayPuckContext(out ReplayPuckContext context)
        {
            context = default;

            var puckManager = NetworkBehaviourSingleton<PuckManager>.Instance;
            if (puckManager == null)
            {
                return false;
            }

            var puck = GetClosestPuck(puckManager);
            if (puck == null)
            {
                return false;
            }

            var botPosition = GetBotPosition();
            var puckPosition = GetFlatPosition(puck.transform.position, botPosition.y);
            var botToPuck = puckPosition - botPosition;
            botToPuck.y = 0f;

            var distanceBotToPuck = botToPuck.magnitude;
            var directionBotToPuck = distanceBotToPuck > 0.0001f ? botToPuck / distanceBotToPuck : GetFlatForward();
            var stickOrigin = stickPositioner != null ? stickPositioner.RaycastOriginPosition : botPosition;
            var stickToPuck = GetFlatPosition(puck.transform.position, stickOrigin.y) - GetFlatPosition(stickOrigin, stickOrigin.y);
            stickToPuck.y = 0f;
            var distanceStickToPuck = stickToPuck.magnitude;
            var bladeWorldPosition = TryGetBladeWorldPosition(out var resolvedBladeWorldPosition) ? resolvedBladeWorldPosition : stickOrigin;
            var bladeToPuck = GetFlatPosition(puck.transform.position, bladeWorldPosition.y) - GetFlatPosition(bladeWorldPosition, bladeWorldPosition.y);
            bladeToPuck.y = 0f;
            var distanceBladeToPuck = bladeToPuck.magnitude;
            var relativePuckVelocity = Vector3.zero;
            if (puck.Rigidbody != null)
            {
                relativePuckVelocity = puck.Rigidbody.linearVelocity;
            }

            if (playerBody != null && playerBody.Rigidbody != null)
            {
                relativePuckVelocity -= playerBody.Rigidbody.linearVelocity;
            }

            relativePuckVelocity.y = 0f;
            var stickDirection = GetReplayReferenceStickDirection();
            var referenceToPuck = bladeToPuck.sqrMagnitude > 0.0001f ? bladeToPuck : stickToPuck;
            var relativeStickPuckAngle = referenceToPuck.sqrMagnitude > 0.0001f
                ? Vector3.SignedAngle(stickDirection, referenceToPuck.normalized, Vector3.up)
                : 0f;
            var controlDistance = Mathf.Min(distanceStickToPuck, distanceBladeToPuck);
            var localPuckPosition = GetLocalPuckPosition(puck.transform.position);

            context.IsValid = true;
            context.DistanceBotToPuck = distanceBotToPuck;
            context.DirectionBotToPuck = directionBotToPuck;
            context.DistanceStickToPuck = distanceStickToPuck;
            context.DistanceBladeToPuck = distanceBladeToPuck;
            context.RelativeStickPuckAngle = relativeStickPuckAngle;
            context.BotPosition = botPosition;
            context.PuckPosition = puck.transform.position;
            context.BladeWorldPosition = bladeWorldPosition;
            context.BladeHeightAboveGround = GetBladeHeightAboveGround(bladeWorldPosition);
            context.RelativePuckVelocity = relativePuckVelocity;
            context.RelativePuckSpeed = relativePuckVelocity.magnitude;
            context.HasPuckControl = HasTruePuckPossession(localPuckPosition, context);
            context.Zone = GetReplayPuckZone(distanceBotToPuck, controlDistance, Mathf.Abs(relativeStickPuckAngle), context.HasPuckControl, botPosition);
            return true;
        }

        private bool TryGetBladeWorldPosition(out Vector3 bladeWorldPosition)
        {
            bladeWorldPosition = Vector3.zero;

            if (stick == null && controlledPlayer != null)
            {
                stick = controlledPlayer.Stick;
            }

            if (stick != null)
            {
                bladeWorldPosition = stick.BladeHandlePosition;
                return true;
            }

            if (stickPositioner == null && controlledPlayer != null)
            {
                stickPositioner = controlledPlayer.StickPositioner;
            }

            if (stickPositioner != null)
            {
                bladeWorldPosition = stickPositioner.RaycastOriginPosition;
                return true;
            }

            return false;
        }

        private float GetBladeHeightAboveGround(Vector3 bladeWorldPosition)
        {
            var rayOrigin = bladeWorldPosition + Vector3.up * 0.05f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, BladeGroundRaycastDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                return Mathf.Max(0f, bladeWorldPosition.y - hit.point.y);
            }

            return Mathf.Max(0f, bladeWorldPosition.y - GetBotPosition().y);
        }

        private Vector3 BuildReplayPhysicalStickTarget(ReplayPuckContext puckContext)
        {
            var liveStickTarget = BuildPuckStickTarget(puckContext.PuckPosition, aggressive: false);
            var heightExcess = Mathf.Max(0f, puckContext.BladeHeightAboveGround - BladeGroundHeightThreshold);
            if (heightExcess > 0.0001f)
            {
                liveStickTarget.y -= heightExcess * BladeGroundPushScale;
            }

            if (puckContext.DistanceBladeToPuck <= BladePuckNearDistance)
            {
                var puckToBlade = GetFlatPosition(puckContext.BladeWorldPosition, puckContext.PuckPosition.y) - GetFlatPosition(puckContext.PuckPosition, puckContext.PuckPosition.y);
                if (puckToBlade.sqrMagnitude > 0.0001f)
                {
                    puckToBlade.Normalize();
                    liveStickTarget = puckContext.PuckPosition + puckToBlade * PuckStickBehindOffset + GetSideBias(puckToBlade) * (PuckStickSideOffset * 0.6f);
                }

                liveStickTarget.y = Mathf.Min(liveStickTarget.y, puckContext.PuckPosition.y + StickIceLift);
            }

            return liveStickTarget;
        }

        private bool TryGetReplayGoalieStickAngles(ReplayPuckContext puckContext, out Vector2 stickAngles)
        {
            stickAngles = GetLowStickFallbackAngles();
            if (!puckContext.IsValid)
            {
                return false;
            }

            var stickTarget = BuildReplayGoalieStickTarget(puckContext);
            if (!TryGetStickAngles(stickTarget, out stickAngles))
            {
                return false;
            }

            return true;
        }

        private Vector3 BuildReplayGoalieStickTarget(ReplayPuckContext puckContext)
        {
            var flatBladePosition = GetFlatPosition(puckContext.BladeWorldPosition, puckContext.PuckPosition.y);
            var flatPuckPosition = GetFlatPosition(puckContext.PuckPosition, puckContext.PuckPosition.y);
            var ownGoalCenter = GetOwnGoalPosition();
            var neutralForward = GetReplayGoalieNeutralForward();
            var goalRight = GetReplayGoalieGoalRight();
            var goalToPuck = flatPuckPosition - GetFlatPosition(ownGoalCenter, flatPuckPosition.y);
            var goalAlignment = goalToPuck.sqrMagnitude > 0.0001f ? Vector3.Dot(neutralForward, goalToPuck.normalized) : 1f;
            var isBehindNet = goalAlignment < ReplayGoalieBehindNetDotThreshold;
            var maxAngle = GetReplayGoalieMaxRotationAngle(goalAlignment, goalToPuck.magnitude);
            var desiredDirection = GetReplayGoalieDesiredDirection(flatBladePosition, flatPuckPosition, ownGoalCenter, neutralForward, goalRight, isBehindNet, maxAngle);
            var stickTarget = flatBladePosition + desiredDirection * Mathf.Max(1f, puckContext.DistanceBladeToPuck + 0.2f);

            if (puckContext.DistanceBladeToPuck <= BladePuckNearDistance)
            {
                stickTarget = flatPuckPosition - desiredDirection * ReplayGoalieCloseBehindOffset + GetSideBias(desiredDirection) * ReplayGoalieCloseSideOffset;
            }

            var heightExcess = Mathf.Max(0f, puckContext.BladeHeightAboveGround - BladeGroundHeightThreshold);
            stickTarget.y = puckContext.PuckPosition.y + StickIceLift - (heightExcess * BladeGroundPushScale);
            return stickTarget;
        }

        private Vector3 GetReplayGoalieDesiredDirection(Vector3 bladePosition, Vector3 puckPosition, Vector3 ownGoalCenter, Vector3 neutralForward, Vector3 goalRight, bool isBehindNet, float maxAngle)
        {
            Vector3 rawDirection;
            if (isBehindNet)
            {
                var goalToPuck = puckPosition - GetFlatPosition(ownGoalCenter, puckPosition.y);
                var lateralDot = Vector3.Dot(goalToPuck, goalRight);
                rawDirection = lateralDot < 0f ? -goalRight : goalRight;
            }
            else
            {
                rawDirection = puckPosition - bladePosition;
                rawDirection.y = 0f;
                if (rawDirection.sqrMagnitude <= 0.0001f)
                {
                    rawDirection = GetReplayReferenceStickDirection();
                }
            }

            rawDirection.y = 0f;
            if (rawDirection.sqrMagnitude <= 0.0001f)
            {
                return neutralForward;
            }

            rawDirection.Normalize();
            var signedAngle = Vector3.SignedAngle(neutralForward, rawDirection, Vector3.up);
            var clampedAngle = Mathf.Clamp(signedAngle, -maxAngle, maxAngle);
            var clampedDirection = Quaternion.AngleAxis(clampedAngle, Vector3.up) * neutralForward;
            clampedDirection.y = 0f;
            return clampedDirection.sqrMagnitude > 0.0001f ? clampedDirection.normalized : neutralForward;
        }

        private float GetReplayGoalieMaxRotationAngle(float goalAlignment, float goalToPuckDistance)
        {
            if (goalAlignment < ReplayGoalieBehindNetDotThreshold)
            {
                return ReplayGoalieMaxRotationAngle;
            }

            var angleFactor = Mathf.InverseLerp(ReplayGoalieNearNetDistance, ReplayGoalieFarNetDistance, goalToPuckDistance);
            return Mathf.Lerp(0f, ReplayGoalieMaxRotationAngle, angleFactor);
        }

        private Vector3 GetReplayGoalieNeutralForward()
        {
            var team = (int)controlledPlayer.Team.Value;
            if (team == 3) return Vector3.forward;
            if (team == 2) return Vector3.back;
            return GetFlatForward();
        }

        private Vector3 GetReplayGoalieGoalRight()
        {
            var team = (int)controlledPlayer.Team.Value;
            if (team == 3) return Vector3.left;
            if (team == 2) return Vector3.right;
            return Vector3.right;
        }

        private float GetReplayPhysicalPriority(ReplayPuckContext puckContext)
        {
            if (!puckContext.IsValid)
            {
                return 0f;
            }

            var groundFactor = Mathf.Clamp01(Mathf.InverseLerp(BladeGroundHeightThreshold, BladeGroundHeightThreshold * 3f, puckContext.BladeHeightAboveGround));
            var puckFactor = Mathf.Clamp01(Mathf.InverseLerp(BladePuckNearDistance, BladePuckReadyDistance, puckContext.DistanceBladeToPuck));
            var shootFactor = puckContext.Zone == ReplayPuckZone.Shoot ? 1f - GetReplayShotReadyFactor(puckContext) : 0f;
            return Mathf.Clamp01(Mathf.Max(groundFactor, puckFactor, shootFactor));
        }

        private float GetReplayShotReadyFactor(ReplayPuckContext puckContext)
        {
            if (!puckContext.IsValid)
            {
                return 0f;
            }

            var bladeDistanceFactor = Mathf.Clamp01(Mathf.InverseLerp(BladePuckNearDistance, BladePuckReadyDistance, puckContext.DistanceBladeToPuck));
            var alignmentFactor = Mathf.Clamp01(Mathf.InverseLerp(ReplayStickAlignmentAngle * 1.5f, BladeShootReadyAngle, Mathf.Abs(puckContext.RelativeStickPuckAngle)));
            var groundFactor = 1f - Mathf.Clamp01(Mathf.InverseLerp(BladeGroundHeightThreshold, BladeGroundHeightThreshold * 3f, puckContext.BladeHeightAboveGround));
            return Mathf.Clamp01(Mathf.Min(bladeDistanceFactor, alignmentFactor, groundFactor));
        }

        private bool TryGetCurrentPuckStickLocalRelation(out float distanceToStick, out float directionAngle, out sbyte relativeSide)
        {
            distanceToStick = 0f;
            directionAngle = 0f;
            relativeSide = 0;

            var puckManager = NetworkBehaviourSingleton<PuckManager>.Instance;
            if (puckManager == null) return false;

            var puck = GetClosestPuck(puckManager);
            if (puck == null) return false;

            if (stickPositioner == null && controlledPlayer != null)
            {
                stickPositioner = controlledPlayer.StickPositioner;
            }

            if (stickPositioner == null) return false;

            var stickOrigin = stickPositioner.RaycastOriginPosition;
            var localPuckOffset = stickPositioner.transform.InverseTransformVector(puck.transform.position - stickOrigin);
            var flatLocalPuckOffset = new Vector2(localPuckOffset.x, localPuckOffset.z);
            distanceToStick = flatLocalPuckOffset.magnitude;
            directionAngle = flatLocalPuckOffset.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(flatLocalPuckOffset.x, flatLocalPuckOffset.y) * Mathf.Rad2Deg
                : 0f;
            relativeSide = flatLocalPuckOffset.x < -0.05f ? (sbyte)(-1) : flatLocalPuckOffset.x > 0.05f ? (sbyte)1 : (sbyte)0;
            return true;
        }

        private float GetReplayFrameDistanceThreshold(ReplayPatternType patternType)
        {
            switch (patternType)
            {
                case ReplayPatternType.TurnDribble:
                    return ReplayFrameDribbleDistanceThreshold;
                case ReplayPatternType.Control:
                case ReplayPatternType.Shoot:
                    return ReplayFrameControlDistanceThreshold;
                default:
                    return ReplayFrameDistanceThreshold;
            }
        }

        private ReplayPuckZone GetReplayPuckZone(float distanceBotToPuck, float distanceStickToPuck, float relativeStickPuckAngle, bool hasPuckControl, Vector3 botPosition)
        {
            if (distanceBotToPuck > ReplayPuckFarDistance)
            {
                return ReplayPuckZone.Far;
            }

            if (hasPuckControl)
            {
                return GetAttackReplayPatternType(botPosition) == ReplayPatternType.Shoot
                    ? ReplayPuckZone.Shoot
                    : ReplayPuckZone.Control;
            }

            if (distanceBotToPuck > ReplayPuckNearDistance)
            {
                return ReplayPuckZone.Far;
            }

            if (distanceStickToPuck <= ReplayStickControlDistance && relativeStickPuckAngle <= ReplayStickAlignmentAngle)
            {
                return ReplayPuckZone.Control;
            }

            return ReplayPuckZone.Near;
        }

        private Vector3 GetReplayReferenceStickDirection()
        {
            if (stickPositioner == null && controlledPlayer != null)
            {
                stickPositioner = controlledPlayer.StickPositioner;
            }

            if (stickPositioner == null)
            {
                return GetFlatForward();
            }

            var referenceAngles = hasSentStickAngles ? lastSentStickAngles : GetLowStickFallbackAngles();
            var localRotation = Quaternion.Euler(referenceAngles.x, referenceAngles.y, 0f);
            var worldDirection = stickPositioner.transform.TransformDirection(localRotation * Vector3.forward);
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude <= 0.0001f)
            {
                return GetFlatForward();
            }

            return worldDirection.normalized;
        }

        private bool UpdateReplayDribbleMode(Vector2 recordedMoveInput, Vector2 replayStickAngles, ReplayPuckContext puckContext)
        {
            if (!puckContext.IsValid || puckContext.Zone == ReplayPuckZone.Far)
            {
                replayDribbleUntilTime = 0f;
            }

            var strongMoveX = Mathf.Abs(recordedMoveInput.x) >= ReplayDribbleMoveXThreshold;
            var signFlip = replayHasRecordedFrame
                && strongMoveX
                && Mathf.Abs(replayLastMoveX) >= ReplayDribbleMoveXThreshold
                && Mathf.Sign(recordedMoveInput.x) != Mathf.Sign(replayLastMoveX);
            var stickDelta = replayHasRecordedFrame ? Vector2.Distance(replayLastRecordedStickAngles, replayStickAngles) : 0f;
            var smallStickMovement = stickDelta <= ReplayDribbleStickDeltaThreshold;

            if ((puckContext.Zone == ReplayPuckZone.Near || puckContext.Zone == ReplayPuckZone.Control)
                && signFlip
                && smallStickMovement)
            {
                replayDribbleUntilTime = Time.time + ReplayDribbleHoldDuration;
            }

            replayLastMoveX = recordedMoveInput.x;
            replayLastRecordedStickAngles = replayStickAngles;
            replayHasRecordedFrame = true;
            return Time.time < replayDribbleUntilTime;
        }

        private ReplayPatternType GetAttackReplayPatternType(Vector3 botPosition)
        {
            var enemyGoal = GetEnemyGoalPosition();
            var toGoal = enemyGoal - botPosition;
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude <= 0.0001f)
            {
                return ReplayPatternType.Control;
            }

            var goalDistance = toGoal.magnitude;
            var turnAngle = Vector3.Angle(GetFlatForward(), toGoal.normalized);
            if (goalDistance <= ReplayShootGoalDistance && turnAngle <= ReplayShootTurnAngle)
            {
                return ReplayPatternType.Shoot;
            }

            if (turnAngle >= ReplayTurnDribbleAngle)
            {
                return ReplayPatternType.TurnDribble;
            }

            return ReplayPatternType.Control;
        }

        private Vector2 GetReplayCorrectedMoveInput(Vector2 recordedMoveInput, ReplayPatternType patternType, ReplayPuckContext puckContext, bool dribbleMode)
        {
            var correctedMoveInput = Vector2.ClampMagnitude(recordedMoveInput, 1f);

            if (puckContext.IsValid && puckContext.Zone != ReplayPuckZone.Far)
            {
                var targetMoveX = dribbleMode
                    ? Mathf.Clamp(correctedMoveInput.x * ReplayDribbleMoveScale, -ReplayDribbleMoveScale, ReplayDribbleMoveScale)
                    : Mathf.Clamp(correctedMoveInput.x, -0.6f, 0.6f);

                replaySmoothedMoveX = Mathf.Lerp(replaySmoothedMoveX, targetMoveX, ReplayMoveSmoothing);
                correctedMoveInput.x = replaySmoothedMoveX;

                if (dribbleMode)
                {
                    correctedMoveInput.y = Mathf.Min(correctedMoveInput.y, ReplayDribbleForwardCap);
                }
                else if (puckContext.Zone == ReplayPuckZone.Control || puckContext.Zone == ReplayPuckZone.Shoot)
                {
                    correctedMoveInput.y = Mathf.Min(correctedMoveInput.y, ReplayControlForwardCap);
                }
            }
            else
            {
                replaySmoothedMoveX = Mathf.Lerp(replaySmoothedMoveX, correctedMoveInput.x, ReplayMoveSmoothing);
                correctedMoveInput.x = replaySmoothedMoveX;
            }

            var correctionBlend = GetReplayMoveCorrectionBlend(patternType);
            if (correctionBlend <= 0.001f)
            {
                return correctedMoveInput;
            }

            var goalDirectedMoveInput = GetGoalDirectedReplayMoveInput();
            if (goalDirectedMoveInput.sqrMagnitude <= 0.0001f)
            {
                return correctedMoveInput;
            }

            return Vector2.ClampMagnitude(correctedMoveInput + goalDirectedMoveInput * correctionBlend, 1f);
        }

        private float GetReplayMoveCorrectionBlend(ReplayPatternType patternType)
        {
            switch (patternType)
            {
                case ReplayPatternType.Shoot:
                    return ReplayMoveShootBlend;
                case ReplayPatternType.Control:
                    return ReplayMoveControlBlend;
                case ReplayPatternType.TurnDribble:
                    return ReplayMoveTurnDribbleBlend;
                default:
                    return 0f;
            }
        }

        private Vector2 GetGoalDirectedReplayMoveInput()
        {
            var botPosition = GetBotPosition();
            var enemyGoal = GetEnemyGoalPosition();
            var desiredDirection = enemyGoal - botPosition;
            desiredDirection.y = 0f;
            if (desiredDirection.sqrMagnitude <= 0.0001f)
            {
                return Vector2.zero;
            }

            desiredDirection.Normalize();
            var forward = GetFlatForward();
            var signedTurnAngle = Vector3.SignedAngle(forward, desiredDirection, Vector3.up);
            var turnAngle = Mathf.Abs(signedTurnAngle);
            var targetTurnInput = Mathf.Clamp(signedTurnAngle / HardTurnAngle, -1f, 1f);
            var targetForwardInput = Mathf.Clamp01(Mathf.InverseLerp(ForwardAlignMaxAngle, ForwardAlignMinAngle, turnAngle));
            if (turnAngle >= HardTurnAngle)
            {
                targetForwardInput = 0f;
            }

            return new Vector2(targetTurnInput, targetForwardInput);
        }

        private Vector3 GetFlatForward()
        {
            var forward = transform.forward;
            if (playerBody != null)
            {
                forward = playerBody.transform.forward;
            }

            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f) return Vector3.forward;
            return forward.normalized;
        }

        private void TryResetInputs()
        {
            try
            {
                if (playerInput == null && controlledPlayer != null)
                {
                    playerInput = controlledPlayer.PlayerInput;
                }

                if (playerInput == null) return;

                playerInput.Client_MoveInputRpc(0, 0);
                playerInput.Client_SprintInputRpc(false);
                playerInput.Client_StopInputRpc(true);
                playerInput.Client_TrackInputRpc(false);
                playerInput.Client_SlideInputRpc(false);
                playerInput.Client_LateralLeftInputRpc(false);
                playerInput.Client_LateralRightInputRpc(false);
                hasSmoothedLiveStickAngles = false;
                ClearApproachTarget();
                ApplyLowStickFallback(0);
            }
            catch
            {
            }
        }

        private static short ToInputInt16(float value)
        {
            var scaled = Mathf.RoundToInt(Mathf.Clamp(value, -1f, 1f) * short.MaxValue);
            if (scaled > short.MaxValue) scaled = short.MaxValue;
            if (scaled < short.MinValue) scaled = short.MinValue;
            return (short)scaled;
        }

        private static short ToAngleInt16(float angle)
        {
            var scaled = Mathf.RoundToInt((angle / 360f) * short.MaxValue);
            if (scaled > short.MaxValue) scaled = short.MaxValue;
            if (scaled < short.MinValue) scaled = short.MinValue;
            return (short)scaled;
        }

        private static Vector3 WrapEulerAngles(Vector3 angles)
        {
            return new Vector3(WrapEulerAngle(angles.x), WrapEulerAngle(angles.y), WrapEulerAngle(angles.z));
        }

        private static float WrapEulerAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }

        private Vector3 BuildPuckStickTarget(Vector3 puckPosition, bool aggressive)
        {
            var flatPuckPosition = GetFlatPosition(puckPosition, puckPosition.y);
            var puckToBot = GetFlatPosition(GetBotPosition(), puckPosition.y) - flatPuckPosition;
            if (puckToBot.sqrMagnitude <= 0.0001f)
            {
                puckToBot = -GetFlatForward();
            }

            puckToBot.Normalize();
            var sideBias = GetSideBias(puckToBot) * PuckStickSideOffset;
            var target = flatPuckPosition - puckToBot * PuckStickBehindOffset + sideBias;
            target.y = puckPosition.y + (aggressive ? StickAttackLift : StickIceLift);
            return target;
        }

        private Vector3 BuildLowBehindPuckTarget(Vector3 puckPosition, Vector3 referenceForward, float sideOffsetScale = 1f)
        {
            return BuildLowBehindPuckTarget(puckPosition, referenceForward, sideOffsetScale, stickSideSign, Vector3.zero);
        }

        private Vector3 BuildLowBehindPuckTarget(Vector3 puckPosition, Vector3 referenceForward, float sideOffsetScale, int sideSign, Vector3 extraOffset)
        {
            referenceForward.y = 0f;
            if (referenceForward.sqrMagnitude <= 0.0001f)
            {
                referenceForward = GetFlatForward();
            }

            referenceForward.Normalize();
            var flatPuckPosition = GetFlatPosition(puckPosition, puckPosition.y);
            var target = flatPuckPosition - referenceForward * PuckStickBehindOffset + GetSideBias(referenceForward, sideSign) * (PuckStickSideOffset * sideOffsetScale) + extraOffset;
            target.y = puckPosition.y + StickIceLift;
            return target;
        }

        private Vector3 BuildControlledStickTarget(Vector3 puckPosition, ReplayPuckContext puckContext, Vector3 preferredForward, Vector3 localPuckPosition, float sideOffsetScale, int sideSign, Vector3 wallPullOffset)
        {
            var referenceForward = GetBehindPuckReferenceDirection(puckContext, preferredForward);
            var target = BuildLowBehindPuckTarget(puckPosition, referenceForward, sideOffsetScale, sideSign, wallPullOffset);
            var localRight = playerBody != null ? playerBody.transform.right : transform.right;
            localRight.y = 0f;
            if (localRight.sqrMagnitude > 0.0001f)
            {
                localRight.Normalize();
                var lateralClamp = Mathf.Clamp(localPuckPosition.x, -DribbleLateralStrongThreshold, DribbleLateralStrongThreshold);
                target -= localRight * (lateralClamp * 0.2f);
            }

            return ForceStickTargetLow(target, puckPosition.y);
        }

        private Vector3 BuildControlBladeTarget(Vector3 puckPosition, Vector3 controlVector, Vector3 localPuckPosition, int sideSign, Vector3 wallPullOffset, bool anticipationActive)
        {
            controlVector.y = 0f;
            if (controlVector.sqrMagnitude <= 0.0001f)
            {
                controlVector = GetFlatForward();
            }

            controlVector.Normalize();
            var perpendicularBias = anticipationActive ? GetSideBias(controlVector, sideSign) * ContactBladePerpendicularOffset : Vector3.zero;
            var target = BuildLowBehindPuckTarget(puckPosition, controlVector, DribbleStickSideScale, sideSign, wallPullOffset + perpendicularBias - controlVector * (ControlVectorBehindOffset - PuckStickBehindOffset));
            var localRight = playerBody != null ? playerBody.transform.right : transform.right;
            localRight.y = 0f;
            if (localRight.sqrMagnitude > 0.0001f)
            {
                localRight.Normalize();
                var lateralClamp = Mathf.Clamp(localPuckPosition.x, -DribbleLateralStrongThreshold, DribbleLateralStrongThreshold);
                target -= localRight * (lateralClamp * 0.18f);
            }

            return ForceStickTargetLow(target, puckPosition.y);
        }

        private Vector3 ForceStickTargetLow(Vector3 stickTarget, float puckY)
        {
            stickTarget.y = Mathf.Min(stickTarget.y, puckY + StickIceLift);
            return stickTarget;
        }

        private Vector3 GetApproachTarget(Vector3 puckPosition, Vector3 desiredDirection, int sideSign, Vector3 wallOffset)
        {
            var flatPuckPosition = GetFlatPosition(puckPosition, GetBotPosition().y);
            var botPosition = GetBotPosition();

            desiredDirection.y = 0f;
            if (desiredDirection.sqrMagnitude <= 0.0001f)
            {
                desiredDirection = flatPuckPosition - botPosition;
                desiredDirection.y = 0f;
            }

            if (desiredDirection.sqrMagnitude <= 0.0001f)
            {
                desiredDirection = GetFlatForward();
            }

            desiredDirection.Normalize();
            var sideBias = GetSideBias(desiredDirection, sideSign) * PuckStickSideOffset;
            var target = flatPuckPosition - desiredDirection * ApproachTargetOffset + sideBias + wallOffset;
            target.y = botPosition.y;
            return target;
        }

        private Vector3 GetLockedApproachTarget(Vector3 puckPosition, Vector3 localPuckPosition, bool refreshForOrbit)
        {
            var botPosition = GetBotPosition();
            if (ShouldRefreshApproachTarget(puckPosition, botPosition, localPuckPosition, refreshForOrbit))
            {
                CacheApproachTarget(puckPosition, localPuckPosition);
            }

            return cachedApproachTarget;
        }

        private void CacheApproachTarget(Vector3 puckPosition, Vector3 localPuckPosition)
        {
            var controlVector = GetDesiredPuckTravelDirection(puckPosition);
            var sideSign = GetApproachSideSign(localPuckPosition, controlVector);
            var wallDirection = Vector3.zero;
            if (TryGetWallOpenDirection(puckPosition, controlVector, out var wallOpenDirection, out var wallSideSign))
            {
                controlVector = BlendWallAwareDirection(controlVector, wallOpenDirection);
                sideSign = wallSideSign;
                wallDirection = wallOpenDirection;
            }

            cachedApproachPuckPosition = puckPosition;
            cachedApproachControlVector = controlVector;
            cachedApproachSideSign = sideSign;
            cachedApproachWallDirection = wallDirection;
            cachedApproachTarget = GetApproachTarget(puckPosition, controlVector, sideSign, wallDirection * PuckWallOpenOffset);
            hasApproachTarget = true;
            approachTargetLockedUntil = Time.time + ApproachTargetLockDuration;
            approachNoProgressSince = -1f;
            approachFreezeLateralUntil = -1f;
            lastApproachDistanceToTarget = Vector3.Distance(GetBotPosition(), cachedApproachTarget);
        }

        private Vector3 BuildGoalStickTarget(Vector3 goalPosition, Vector3 puckPosition, bool aggressive)
        {
            var flatGoalPosition = GetFlatPosition(goalPosition, puckPosition.y);
            var toGoal = flatGoalPosition - GetFlatPosition(puckPosition, puckPosition.y);
            if (toGoal.sqrMagnitude <= 0.0001f)
            {
                toGoal = GetFlatForward();
            }

            toGoal.Normalize();
            var target = flatGoalPosition + GetSideBias(toGoal) * GoalStickSideOffset;
            target.y = puckPosition.y + (aggressive ? StickAttackLift : StickIceLift);
            return target;
        }

        private Vector3 GetSideBias(Vector3 referenceDirection)
        {
            return GetSideBias(referenceDirection, stickSideSign);
        }

        private Vector3 GetSideBias(Vector3 referenceDirection, int sideSign)
        {
            referenceDirection.y = 0f;
            if (referenceDirection.sqrMagnitude <= 0.0001f)
            {
                referenceDirection = GetFlatForward();
            }

            referenceDirection.Normalize();
            var side = Vector3.Cross(Vector3.up, referenceDirection).normalized;
            return side * sideSign;
        }

        private bool ShouldRefreshApproachTarget(Vector3 puckPosition, Vector3 botPosition, Vector3 localPuckPosition, bool refreshForOrbit)
        {
            if (!hasApproachTarget) return true;
            if (Vector3.Distance(cachedApproachPuckPosition, puckPosition) >= ApproachPuckMoveThreshold)
            {
                return true;
            }

            var distanceToTarget = Vector3.Distance(botPosition, cachedApproachTarget);
            var progressing = lastApproachDistanceToTarget < 0f || distanceToTarget <= lastApproachDistanceToTarget - ApproachProgressDistanceThreshold;
            var toTarget = cachedApproachTarget - botPosition;
            toTarget.y = 0f;
            var desiredMoveDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : cachedApproachControlVector;
            var turnAngle = Vector3.Angle(GetFlatForward(), desiredMoveDirection);
            var notReducingDistance = !progressing && distanceToTarget > ApproachTargetDeadzone;
            var orbiting = refreshForOrbit
                && Mathf.Abs(localPuckPosition.x) > AlignLateralThreshold
                && turnAngle >= ApproachOrbitTurnAngle
                && notReducingDistance;

            if (orbiting)
            {
                if (approachNoProgressSince < 0f)
                {
                    approachNoProgressSince = Time.time;
                }

                if (Time.time - approachNoProgressSince >= ApproachOrbitFreezeDelay)
                {
                    approachFreezeLateralUntil = Mathf.Max(approachFreezeLateralUntil, Time.time + ApproachAntiSpinFreezeDuration);
                }

                if (Time.time - approachNoProgressSince >= ApproachOrbitResetDuration)
                {
                    return true;
                }
            }
            else if (progressing || distanceToTarget <= ApproachTargetDeadzone)
            {
                approachNoProgressSince = -1f;
            }

            lastApproachDistanceToTarget = distanceToTarget;
            if (Time.time < approachTargetLockedUntil)
            {
                return false;
            }

            return distanceToTarget >= ApproachRetargetDistance;
        }

        private Vector3 GetApproachControlVector(Vector3 puckPosition)
        {
            if (hasApproachTarget && Time.time < approachTargetLockedUntil)
            {
                return cachedApproachControlVector;
            }

            return GetDesiredPuckTravelDirection(puckPosition);
        }

        private Vector3 GetDesiredPuckTravelDirection(Vector3 puckPosition)
        {
            var enemyGoal = GetEnemyGoalPosition();
            var desiredDirection = enemyGoal - GetFlatPosition(puckPosition, enemyGoal.y);
            desiredDirection.y = 0f;
            if (desiredDirection.sqrMagnitude <= 0.0001f)
            {
                desiredDirection = GetFlatForward();
            }

            return desiredDirection.normalized;
        }

        private int GetApproachSideSign(Vector3 localPuckPosition, Vector3 desiredDirection)
        {
            if (localPuckPosition.x > SideSelectionDeadzone)
            {
                return -1;
            }

            if (localPuckPosition.x < -SideSelectionDeadzone)
            {
                return 1;
            }

            var localDesiredDirection = playerBody != null
                ? playerBody.transform.InverseTransformDirection(desiredDirection)
                : transform.InverseTransformDirection(desiredDirection);
            return localDesiredDirection.x >= 0f ? -1 : 1;
        }

        private Vector3 BlendWallAwareDirection(Vector3 desiredDirection, Vector3 wallOpenDirection)
        {
            desiredDirection.y = 0f;
            wallOpenDirection.y = 0f;

            if (desiredDirection.sqrMagnitude <= 0.0001f)
            {
                desiredDirection = GetFlatForward();
            }

            if (wallOpenDirection.sqrMagnitude <= 0.0001f)
            {
                return desiredDirection.normalized;
            }

            return Vector3.Slerp(desiredDirection.normalized, wallOpenDirection.normalized, PuckWallDirectionBlend).normalized;
        }

        private bool TryGetWallOpenDirection(Vector3 puckPosition, Vector3 desiredDirection, out Vector3 wallOpenDirection, out int sideSign)
        {
            wallOpenDirection = Vector3.zero;
            sideSign = 0;

            desiredDirection.y = 0f;
            if (desiredDirection.sqrMagnitude <= 0.0001f)
            {
                desiredDirection = GetFlatForward();
            }

            desiredDirection.Normalize();
            var origin = GetFlatPosition(puckPosition, puckPosition.y) + Vector3.up * WallRayHeight;
            var right = Vector3.Cross(Vector3.up, desiredDirection).normalized;
            var bestDistance = PuckWallDetectDistance;
            var bestNormal = Vector3.zero;

            void Probe(Vector3 direction)
            {
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    return;
                }

                if (!Physics.Raycast(origin, direction.normalized, out var hit, PuckWallDetectDistance, ~0, QueryTriggerInteraction.Ignore))
                {
                    return;
                }

                var horizontalNormal = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
                if (horizontalNormal.sqrMagnitude <= 0.0001f || hit.distance >= bestDistance)
                {
                    return;
                }

                bestDistance = hit.distance;
                bestNormal = horizontalNormal.normalized;
            }

            Probe(desiredDirection);
            Probe(-desiredDirection);
            Probe(right);
            Probe(-right);
            Probe((desiredDirection + right * 0.65f).normalized);
            Probe((desiredDirection - right * 0.65f).normalized);
            Probe((-desiredDirection + right * 0.65f).normalized);
            Probe((-desiredDirection - right * 0.65f).normalized);

            if (bestNormal.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            wallOpenDirection = bestNormal;
            sideSign = Vector3.Dot(right, wallOpenDirection) >= 0f ? 1 : -1;
            return true;
        }

        private bool IsWallLocked(Vector3 puckPosition, Vector3 localPuckPosition, ReplayPuckContext puckContext)
        {
            if (!puckContext.IsValid || HasTruePuckPossession(localPuckPosition, puckContext))
            {
                wallLockSince = -1f;
                return false;
            }

            if (!TryGetWallOpenDirection(puckPosition, GetDesiredPuckTravelDirection(puckPosition), out _, out _)
                || puckContext.RelativePuckSpeed > WallLockRelativeSpeedThreshold
                || Mathf.Abs(localPuckPosition.x) <= WallLockLateralThreshold)
            {
                wallLockSince = -1f;
                return false;
            }

            if (wallLockSince < 0f)
            {
                wallLockSince = Time.time;
            }

            return Time.time - wallLockSince >= WallLockDuration;
        }

        private bool ShouldPulseSlideForTurn(float turnAngle, float moveY, float distanceToTarget)
        {
            if (turnAngle < SlidePulseTurnAngle || moveY > SlidePulseMoveYThreshold || distanceToTarget <= ApproachSlowDistance)
            {
                return false;
            }

            if (GetCurrentSpeed() < SlideSpeedThreshold)
            {
                return false;
            }

            if (Time.time - lastSlidePulseTime >= SlidePulsePeriod)
            {
                lastSlidePulseTime = Time.time;
            }

            return Time.time - lastSlidePulseTime <= SlidePulseDuration;
        }

        private void ClearApproachTarget()
        {
            hasApproachTarget = false;
            cachedApproachTarget = Vector3.zero;
            cachedApproachPuckPosition = Vector3.zero;
            cachedApproachControlVector = Vector3.forward;
            cachedApproachWallDirection = Vector3.zero;
            cachedApproachSideSign = 1;
            approachTargetLockedUntil = -1f;
            approachNoProgressSince = -1f;
            lastApproachDistanceToTarget = -1f;
            approachFreezeLateralUntil = -1f;
        }

        private void ClearContactPreparation()
        {
            hasStableControlVector = false;
            stableControlVector = Vector3.forward;
            hasBladeOrientationLock = false;
            lockedBladeControlVector = Vector3.forward;
            lockedBladeWallPullOffset = Vector3.zero;
            lockedBladeSideSign = 1;
            bladeOrientationLockedUntil = -1f;
        }

        private Vector3 GetLocalPuckPosition(Vector3 puckPosition)
        {
            if (playerBody != null)
            {
                return playerBody.transform.InverseTransformPoint(puckPosition);
            }

            return transform.InverseTransformPoint(puckPosition);
        }

        private bool ShouldForceLowStick(ReplayPuckContext puckContext)
        {
            return !puckContext.IsValid || puckContext.DistanceBotToPuck <= LowStickInteractionDistance || puckContext.DistanceBladeToPuck <= BladePuckNearDistance;
        }

        private bool IsContactAnticipationActive(Vector3 localPuckPosition, ReplayPuckContext puckContext)
        {
            if (!puckContext.IsValid)
            {
                return false;
            }

            return localPuckPosition.z <= ContactAnticipationDistance || puckContext.DistanceBladeToPuck <= ContactAnticipationDistance;
        }

        private bool IsPuckInsideContactCorridor(Vector3 localPuckPosition, ReplayPuckContext puckContext)
        {
            if (!puckContext.IsValid)
            {
                return false;
            }

            return localPuckPosition.z >= ContactCorridorFrontMin
                && localPuckPosition.z <= ContactCorridorFrontMax
                && Mathf.Abs(localPuckPosition.x) <= ContactCorridorLateralThreshold
                && puckContext.DistanceBladeToPuck <= ContactCorridorBladeDistance;
        }

        private float ApplyContactSpeedControl(float moveY, Vector3 localPuckPosition, ReplayPuckContext puckContext, bool inContactCorridor)
        {
            if (!IsContactAnticipationActive(localPuckPosition, puckContext))
            {
                return moveY;
            }

            var clampedMoveY = Mathf.Min(moveY, ContactAnticipationForwardCap);
            var speed = GetCurrentSpeed();
            if (speed > ContactSpeedThreshold)
            {
                var speedScale = Mathf.Clamp01(Mathf.InverseLerp(ContactHardSpeedThreshold, ContactSpeedThreshold, speed));
                clampedMoveY *= Mathf.Lerp(0.18f, 1f, speedScale);
            }

            if (!inContactCorridor)
            {
                clampedMoveY = Mathf.Min(clampedMoveY, 0.045f);
            }

            return clampedMoveY;
        }

        private Vector3 PrepareContactDirection(Vector3 desiredDirection, Vector3 puckPosition, Vector3 localPuckPosition, ReplayPuckContext puckContext, bool allowOrientationLock, out int sideSign, out Vector3 wallPullOffset)
        {
            desiredDirection = ApplyStableControlVector(desiredDirection, localPuckPosition, puckContext);
            desiredDirection = ApplyWallExtractionDirection(desiredDirection, puckPosition, localPuckPosition, puckContext, out sideSign, out wallPullOffset);

            if (hasBladeOrientationLock && Time.time < bladeOrientationLockedUntil)
            {
                sideSign = lockedBladeSideSign;
                wallPullOffset = lockedBladeWallPullOffset;
                return lockedBladeControlVector;
            }

            var anticipationActive = IsContactAnticipationActive(localPuckPosition, puckContext);
            var corridorReady = IsPuckInsideContactCorridor(localPuckPosition, puckContext);
            if (allowOrientationLock
                && anticipationActive
                && corridorReady
                && IsBodyAlignedWithDirection(desiredDirection, ApproachBodyAlignmentAngleThreshold)
                && IsBladeControlReady(localPuckPosition, puckContext, desiredDirection, ApproachStickAlignmentAngleThreshold + 4f))
            {
                hasBladeOrientationLock = true;
                lockedBladeControlVector = desiredDirection;
                lockedBladeWallPullOffset = wallPullOffset;
                lockedBladeSideSign = sideSign;
                bladeOrientationLockedUntil = Time.time + BladeOrientationLockDuration;
                return lockedBladeControlVector;
            }

            hasBladeOrientationLock = false;
            return desiredDirection;
        }

        private Vector3 ApplyStableControlVector(Vector3 desiredDirection, Vector3 localPuckPosition, ReplayPuckContext puckContext)
        {
            desiredDirection.y = 0f;
            if (desiredDirection.sqrMagnitude <= 0.0001f)
            {
                desiredDirection = GetFlatForward();
            }

            desiredDirection.Normalize();
            if (!hasStableControlVector)
            {
                stableControlVector = desiredDirection;
                hasStableControlVector = true;
                return stableControlVector;
            }

            var blend = IsContactAnticipationActive(localPuckPosition, puckContext)
                ? ContactStableControlVectorBlend
                : StableControlVectorBlend;
            stableControlVector = Vector3.Slerp(stableControlVector, desiredDirection, blend).normalized;
            return stableControlVector;
        }

        private Vector3 ApplyWallExtractionDirection(Vector3 desiredDirection, Vector3 puckPosition, Vector3 localPuckPosition, ReplayPuckContext puckContext, out int sideSign, out Vector3 wallPullOffset)
        {
            sideSign = GetApproachSideSign(localPuckPosition, desiredDirection);
            wallPullOffset = Vector3.zero;
            if (!TryGetWallOpenDirection(puckPosition, desiredDirection, out var wallOpenDirection, out var wallSideSign))
            {
                return desiredDirection;
            }

            sideSign = wallSideSign;
            var openSide = GetSideBias(desiredDirection, wallSideSign);
            var extractionDirection = (wallOpenDirection + openSide * WallExtractionSideBias).normalized;
            var blend = IsContactAnticipationActive(localPuckPosition, puckContext)
                ? WallExtractionDirectionBlend
                : PuckWallDirectionBlend;
            desiredDirection = Vector3.Slerp(desiredDirection.normalized, extractionDirection, blend).normalized;
            var pullScale = IsContactAnticipationActive(localPuckPosition, puckContext)
                ? WallExtractionStickPullOffset
                : PuckWallStickPullOffset;
            wallPullOffset = wallOpenDirection * pullScale + openSide * (pullScale * 0.45f);
            return desiredDirection;
        }

        private Vector3 GetBehindPuckReferenceDirection(ReplayPuckContext puckContext, Vector3 preferredForward)
        {
            preferredForward.y = 0f;
            if (preferredForward.sqrMagnitude <= 0.0001f)
            {
                preferredForward = GetFlatForward();
            }

            preferredForward.Normalize();
            if (puckContext.IsValid && puckContext.RelativePuckSpeed > 0.12f && puckContext.RelativePuckSpeed <= PossessionLooseRelativeSpeedThreshold)
            {
                var relativeDirection = puckContext.RelativePuckVelocity;
                relativeDirection.y = 0f;
                if (relativeDirection.sqrMagnitude > 0.0001f)
                {
                    preferredForward = Vector3.Slerp(preferredForward, relativeDirection.normalized, 0.35f);
                }
            }

            preferredForward.y = 0f;
            if (preferredForward.sqrMagnitude <= 0.0001f)
            {
                return GetFlatForward();
            }

            return preferredForward.normalized;
        }

        private bool IsBladeBehindPuck(ReplayPuckContext puckContext)
        {
            if (!puckContext.IsValid)
            {
                return false;
            }

            var toPuck = GetFlatPosition(puckContext.PuckPosition, puckContext.BladeWorldPosition.y) - GetFlatPosition(puckContext.BladeWorldPosition, puckContext.BladeWorldPosition.y);
            toPuck.y = 0f;
            if (toPuck.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            return Vector3.Dot(toPuck.normalized, GetFlatForward()) >= BladeBehindPuckForwardThreshold;
        }

        private bool IsBladeLow(ReplayPuckContext puckContext)
        {
            return puckContext.IsValid && puckContext.BladeHeightAboveGround <= BladeGroundHeightThreshold;
        }

        private bool IsPuckVelocityStable(ReplayPuckContext puckContext, float threshold)
        {
            return puckContext.IsValid && puckContext.RelativePuckSpeed <= threshold;
        }

        private bool IsBladeAlignedWithDirection(ReplayPuckContext puckContext, Vector3 direction, float angleThreshold)
        {
            if (!puckContext.IsValid)
            {
                return false;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = GetFlatForward();
            }

            var bladeToPuck = GetFlatPosition(puckContext.PuckPosition, puckContext.BladeWorldPosition.y) - GetFlatPosition(puckContext.BladeWorldPosition, puckContext.BladeWorldPosition.y);
            bladeToPuck.y = 0f;
            if (bladeToPuck.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            return Vector3.Angle(bladeToPuck.normalized, direction.normalized) <= angleThreshold;
        }

        private bool IsBladeBehindPuckForDirection(ReplayPuckContext puckContext, Vector3 direction)
        {
            if (!puckContext.IsValid)
            {
                return false;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = GetFlatForward();
            }

            var bladeToPuck = GetFlatPosition(puckContext.PuckPosition, puckContext.BladeWorldPosition.y) - GetFlatPosition(puckContext.BladeWorldPosition, puckContext.BladeWorldPosition.y);
            bladeToPuck.y = 0f;
            if (bladeToPuck.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            return Vector3.Dot(bladeToPuck.normalized, direction.normalized) >= BladeBehindPuckForwardThreshold;
        }

        private bool IsBladeControlReady(Vector3 localPuckPosition, ReplayPuckContext puckContext, Vector3 controlVector, float alignmentThreshold)
        {
            return puckContext.IsValid
                && localPuckPosition.z >= ControlOvershootFrontThreshold
                && puckContext.DistanceBladeToPuck <= BladePuckNearDistance
                && IsBladeLow(puckContext)
                && IsBladeBehindPuckForDirection(puckContext, controlVector)
                && IsBladeAlignedWithDirection(puckContext, controlVector, alignmentThreshold);
        }

        private bool IsBodyAlignedWithDirection(Vector3 direction, float angleThreshold)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            return Vector3.Angle(GetFlatForward(), direction.normalized) <= angleThreshold;
        }

        private bool IsApproachEntryReady(Vector3 localPuckPosition, ReplayPuckContext puckContext, Vector3 controlVector)
        {
            return puckContext.IsValid
                && localPuckPosition.z >= AlignForwardThreshold
                && Mathf.Abs(localPuckPosition.x) <= AlignTightLateralThreshold
                && IsPuckInsideContactCorridor(localPuckPosition, puckContext)
                && IsBodyAlignedWithDirection(controlVector, ApproachBodyAlignmentAngleThreshold)
                && IsBladeControlReady(localPuckPosition, puckContext, controlVector, ApproachStickAlignmentAngleThreshold)
                && GetCurrentSpeed() <= ApproachEntrySpeedThreshold;
        }

        private float GetControlForwardInput(Vector3 localPuckPosition, ReplayPuckContext puckContext, float maxForward, float minForward)
        {
            var distanceMetric = localPuckPosition.z;
            if (puckContext.IsValid)
            {
                distanceMetric = Mathf.Max(distanceMetric, puckContext.DistanceBladeToPuck);
            }

            if (distanceMetric <= ControlOvershootFrontThreshold)
            {
                return 0f;
            }

            var nearScale = Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(Mathf.InverseLerp(ControlNearPuckDistance, ControlFarPuckDistance, distanceMetric)));
            return Mathf.Clamp(maxForward * nearScale, minForward, maxForward);
        }

        private bool IsOvershootingPuck(Vector3 localPuckPosition, ReplayPuckContext puckContext)
        {
            if (!puckContext.IsValid)
            {
                return false;
            }

            return localPuckPosition.z <= ControlOvershootFrontThreshold && puckContext.DistanceBladeToPuck > ControlNearPuckDistance;
        }

        private bool IsPuckInsideControlRadius(Vector3 localPuckPosition, ReplayPuckContext puckContext)
        {
            if (!puckContext.IsValid)
            {
                return false;
            }

            return localPuckPosition.z >= ControlOvershootFrontThreshold
                && Mathf.Abs(localPuckPosition.x) <= DribbleLateralStrongThreshold
                && puckContext.DistanceBladeToPuck <= DribbleControlRadius;
        }

        private bool HasTruePuckPossession(Vector3 localPuckPosition, ReplayPuckContext puckContext)
        {
            return puckContext.IsValid
                && localPuckPosition.z >= PossessionFrontThreshold
                && Mathf.Abs(localPuckPosition.x) <= PossessionLateralThreshold
                && puckContext.DistanceBladeToPuck <= BladePuckNearDistance
                && IsBladeBehindPuck(puckContext)
                && IsBladeLow(puckContext)
                && IsPuckVelocityStable(puckContext, PossessionRelativeSpeedThreshold);
        }

        private bool IsAlignmentReady(Vector3 localPuckPosition, ReplayPuckContext puckContext, Vector3 controlVector)
        {
            return puckContext.IsValid
                && localPuckPosition.z >= AlignForwardThreshold
                && Mathf.Abs(localPuckPosition.x) <= AlignLateralThreshold
            && IsBodyAlignedWithDirection(controlVector, ApproachBodyAlignmentAngleThreshold + 8f)
            && IsBladeControlReady(localPuckPosition, puckContext, controlVector, ApproachStickAlignmentAngleThreshold + 8f);
        }

        private bool IsControlReady(Vector3 localPuckPosition, ReplayPuckContext puckContext, bool hasPuck, Vector3 controlVector)
        {
            return puckContext.IsValid
                && localPuckPosition.z >= AlignForwardThreshold
                && Mathf.Abs(localPuckPosition.x) <= AlignLateralThreshold
                && (hasPuck || IsPuckInsideContactCorridor(localPuckPosition, puckContext))
                && (hasPuck || puckContext.DistanceBladeToPuck <= BladePuckNearDistance || puckContext.DistanceStickToPuck <= ReplayStickControlDistance)
            && IsBodyAlignedWithDirection(controlVector, ApproachBodyAlignmentAngleThreshold)
            && IsBladeControlReady(localPuckPosition, puckContext, controlVector, ControlBladeAlignmentAngleThreshold)
                && IsPuckVelocityStable(puckContext, PossessionLooseRelativeSpeedThreshold);
        }

        private bool IsShotReady(Vector3 localPuckPosition, ReplayPuckContext puckContext)
        {
            if (!puckContext.IsValid)
            {
                return false;
            }

            var enemyGoal = GetEnemyGoalPosition();
            var toGoal = enemyGoal - GetBotPosition();
            toGoal.y = 0f;
            var goalFacingAngle = toGoal.sqrMagnitude > 0.0001f ? Vector3.Angle(GetFlatForward(), toGoal.normalized) : 180f;
            var goalDistance = toGoal.magnitude;

            return HasTruePuckPossession(localPuckPosition, puckContext)
                && localPuckPosition.z >= ShotPuckFrontThreshold
                && Mathf.Abs(localPuckPosition.x) <= PossessionLateralThreshold
                && goalFacingAngle <= ShotFacingAngleThreshold
                && goalDistance <= ShotGoalDistanceThreshold
                && puckContext.DistanceBladeToPuck <= BladePuckReadyDistance
                && IsBladeBehindPuck(puckContext)
                && IsBladeLow(puckContext);
        }

        private void GetLateralCorrectionInputs(float localPuckX, float strongThreshold, float burstThreshold, out bool lateralLeft, out bool lateralRight)
        {
            lateralLeft = false;
            lateralRight = false;

            var absoluteOffset = Mathf.Abs(localPuckX);
            if (absoluteOffset <= burstThreshold)
            {
                return;
            }

            var alwaysCorrect = absoluteOffset >= strongThreshold;
            var pulseActive = alwaysCorrect || ShouldPulseLateralCorrection();
            if (!pulseActive)
            {
                return;
            }

            lateralLeft = localPuckX < 0f;
            lateralRight = localPuckX > 0f;
        }

        private bool ShouldPulseLateralCorrection()
        {
            if (Time.time - lastLateralPulseTime >= LateralPulsePeriod)
            {
                lastLateralPulseTime = Time.time;
            }

            return Time.time - lastLateralPulseTime <= LateralPulseDuration;
        }

        private bool ShouldTapCrouchForTurn(float turnAngle, float moveY)
        {
            if (turnAngle < CrouchTurnAngle || moveY > 0.28f)
            {
                return false;
            }

            if (Time.time - lastCrouchPulseTime >= CrouchPulsePeriod)
            {
                lastCrouchPulseTime = Time.time;
            }

            return Time.time - lastCrouchPulseTime <= CrouchPulseDuration;
        }

        private Vector3 StabilizeMoveDirection(Vector3 desiredDirection, float distanceToTarget)
        {
            if (distanceToTarget <= CloseControlDistance && Vector3.Angle(smoothedMoveDirection, desiredDirection) < 12f)
            {
                desiredDirection = smoothedMoveDirection;
            }

            if (smoothedMoveDirection.sqrMagnitude <= 0.0001f)
            {
                smoothedMoveDirection = desiredDirection;
            }

            var directionLerp = distanceToTarget <= CloseControlDistance ? 0.18f : 0.34f;
            smoothedMoveDirection = Vector3.Slerp(smoothedMoveDirection, desiredDirection, directionLerp);
            smoothedMoveDirection.y = 0f;
            if (smoothedMoveDirection.sqrMagnitude <= 0.0001f) smoothedMoveDirection = desiredDirection;
            smoothedMoveDirection.Normalize();
            return smoothedMoveDirection;
        }

        private float ComputeTurnInput(float signedTurnAngle, float turnAngle)
        {
            var targetTurnSign = 0;
            if (turnAngle > TurnDeadZoneAngle)
            {
                targetTurnSign = signedTurnAngle > 0f ? 1 : -1;

                if (turnAngle < TurnHysteresisAngle && lastTurnSign != 0)
                {
                    targetTurnSign = lastTurnSign;
                }
            }

            if (targetTurnSign != 0)
            {
                lastTurnSign = targetTurnSign;
            }
            else if (turnAngle <= TurnDeadZoneAngle)
            {
                lastTurnSign = 0;
            }

            var targetTurnMagnitude = Mathf.Clamp01(Mathf.InverseLerp(TurnDeadZoneAngle, HardTurnAngle, turnAngle));
            var targetTurnInput = targetTurnSign * targetTurnMagnitude;
            smoothedTurnInput = Mathf.MoveTowards(smoothedTurnInput, targetTurnInput, TurnInputLerpPerTick);

            if (turnAngle <= TurnDeadZoneAngle && Mathf.Abs(smoothedTurnInput) < 0.05f)
            {
                smoothedTurnInput = 0f;
            }

            return Mathf.Clamp(smoothedTurnInput, -1f, 1f);
        }

        private int GetInitialSideSign()
        {
            if (controlledPlayer == null) return 1;
            return (((NetworkBehaviour)controlledPlayer).OwnerClientId & 1UL) == 0UL ? 1 : -1;
        }

        private bool ShouldPulseControlTurn()
        {
            if (Time.time - lastControlPulseTime >= ControlPulsePeriod)
            {
                lastControlPulseTime = Time.time;
            }

            return Time.time - lastControlPulseTime <= ControlPulseDuration;
        }

        private float GetCurrentSpeed()
        {
            if (playerBody == null && controlledPlayer != null)
            {
                playerBody = controlledPlayer.PlayerBody;
            }

            return playerBody != null ? playerBody.Speed : 0f;
        }

        private Puck GetClosestPuck(PuckManager puckManager)
        {
            var pucks = puckManager.GetPucks(false);
            if (pucks == null || pucks.Count == 0)
            {
                return puckManager.GetPuck(false);
            }

            var botPosition = GetBotPosition();
            Puck closest = null;
            var closestDistanceSq = float.MaxValue;

            for (var i = 0; i < pucks.Count; i++)
            {
                var puck = pucks[i];
                if (puck == null) continue;

                var puckPosition = GetFlatPosition(puck.transform.position, botPosition.y);
                var distanceSq = (puckPosition - botPosition).sqrMagnitude;
                if (distanceSq < closestDistanceSq)
                {
                    closestDistanceSq = distanceSq;
                    closest = puck;
                }
            }

            return closest;
        }

        private Vector3 PredictPuckPosition(Puck puck, float y)
        {
            if (puck == null) return new Vector3(0f, y, 0f);

            var current = GetFlatPosition(puck.transform.position, y);
            try
            {
                if (puck.Rigidbody == null) return current;
                var velocity = puck.Rigidbody.linearVelocity;
                velocity.y = 0f;
                var predicted = current + velocity * PuckPredictionTime;
                return new Vector3(predicted.x, y, predicted.z);
            }
            catch
            {
                return current;
            }
        }

        private bool HasBotPuckControl(PuckManager puckManager, Puck referencePuck)
        {
            if (controlledPlayer == null || puckManager == null) return false;

            var ownerClientId = ((NetworkBehaviour)controlledPlayer).OwnerClientId;
            var playerPuck = puckManager.GetPlayerPuck(ownerClientId);
            if (playerPuck != null) return true;

            if (referencePuck != null && referencePuck.IsTouchingStick)
            {
                var stick = referencePuck.TouchingStick;
                if (stick != null && stick.Player == controlledPlayer) return true;
            }

            return false;
        }

        private bool TryGetEnemyPuckCarrier(PuckManager puckManager, out Player enemyCarrier, out Puck enemyPuck)
        {
            enemyCarrier = null;
            enemyPuck = null;

            var playerManager = NetworkBehaviourSingleton<PlayerManager>.Instance;
            if (playerManager == null) return false;

            var players = playerManager.GetPlayers(false);
            if (players == null || players.Count == 0) return false;

            var myTeam = GetTeamValue(controlledPlayer);

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || player == controlledPlayer) continue;

                var team = GetTeamValue(player);
                if (team == myTeam) continue;

                var ownerClientId = ((NetworkBehaviour)player).OwnerClientId;
                var carriedPuck = puckManager.GetPlayerPuck(ownerClientId);
                if (carriedPuck == null) continue;

                enemyCarrier = player;
                enemyPuck = carriedPuck;
                return true;
            }

            return false;
        }

        private bool TryBuildDefensiveInterceptTarget(out Vector3 moveTarget, out Vector3 stickTarget)
        {
            moveTarget = Vector3.zero;
            stickTarget = Vector3.zero;

            var puckManager = NetworkBehaviourSingleton<PuckManager>.Instance;
            if (puckManager == null)
            {
                return false;
            }

            if (!TryGetEnemyPuckCarrier(puckManager, out var enemyCarrier, out var enemyPuck))
            {
                return false;
            }

            var ownGoal = GetOwnGoalPosition();
            var threatPosition = enemyPuck != null
                ? GetFlatPosition(enemyPuck.transform.position, ownGoal.y)
                : GetFlatPosition(enemyCarrier.transform.position, ownGoal.y);
            moveTarget = Vector3.Lerp(ownGoal, threatPosition, DefenseInterceptLerp);
            moveTarget.y = GetBotPosition().y;

            var threatDirection = threatPosition - ownGoal;
            if (threatDirection.sqrMagnitude <= 0.0001f)
            {
                threatDirection = GetFlatForward();
            }

            stickTarget = ForceStickTargetLow(BuildLowBehindPuckTarget(threatPosition, threatDirection.normalized, sideOffsetScale: 0.35f), threatPosition.y);
            return true;
        }

        private Vector3 GetBotPosition()
        {
            if (playerBody == null && controlledPlayer != null)
            {
                playerBody = controlledPlayer.PlayerBody;
            }

            if (playerBody != null)
            {
                var pos = playerBody.transform.position;
                return new Vector3(pos.x, pos.y, pos.z);
            }

            var fallback = transform.position;
            return new Vector3(fallback.x, fallback.y, fallback.z);
        }

        private Vector3 GetOwnGoalPosition()
        {
            var team = GetTeamValue(controlledPlayer);
            var y = GetBotPosition().y;
            if (team == 3) return new Vector3(0f, y, GoalZRed);
            if (team == 2) return new Vector3(0f, y, GoalZBlue);
            return new Vector3(0f, y, 0f);
        }

        private Vector3 GetEnemyGoalPosition()
        {
            var team = GetTeamValue(controlledPlayer);
            var y = GetBotPosition().y;
            if (team == 3) return new Vector3(0f, y, GoalZBlue);
            if (team == 2) return new Vector3(0f, y, GoalZRed);
            return new Vector3(0f, y, 0f);
        }

        private static int GetTeamValue(Player player)
        {
            if (player == null) return 0;

            try
            {
                return (int)player.Team.Value;
            }
            catch
            {
                return 0;
            }
        }

        private static Vector3 GetFlatPosition(Vector3 position, float y)
        {
            return new Vector3(position.x, y, position.z);
        }
    }
}