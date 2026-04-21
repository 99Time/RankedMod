using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace schrader.Server
{
    public static partial class RankedSystem
    {
        private const string BackendConfigFileName = "schrader_backend_config.json";
        private const string DefaultServerConfigurationRelativePath = "./server_configuration.json";
        private const string PuckServerConfigurationCommandLineArg = "--serverConfigurationPath";
        private const string PuckServerConfigurationEnvVar = "PUCK_SERVER_CONFIGURATION";
        private const string BackendBaseUrlEnvVar = "SCHRADER_BACKEND_BASE_URL";
        private const string BackendApiKeyEnvVar = "SCHRADER_BACKEND_API_KEY";
        private const string BackendPlayerStatePathEnvVar = "SCHRADER_BACKEND_PLAYER_STATE_PATH";
        private const string BackendMatchResultPathEnvVar = "SCHRADER_BACKEND_MATCH_RESULT_PATH";
        private const string BackendLinkCompletePathEnvVar = "SCHRADER_BACKEND_LINK_COMPLETE_PATH";
        private const string BackendMutePathEnvVar = "SCHRADER_BACKEND_MUTE_PATH";
        private const string BackendTempBanPathEnvVar = "SCHRADER_BACKEND_TEMPBAN_PATH";
        private const string BackendUnmutePathEnvVar = "SCHRADER_BACKEND_UNMUTE_PATH";
        private const string BackendUnbanPathEnvVar = "SCHRADER_BACKEND_UNBAN_PATH";
        private const string BackendServerModeEnvVar = "SCHRADER_RANKED_SERVER_MODE";
        private const string BackendTimeoutMsEnvVar = "SCHRADER_BACKEND_TIMEOUT_MS";
        private const string BackendModerationCacheSecondsEnvVar = "SCHRADER_BACKEND_MODERATION_CACHE_SECONDS";
        private const string BackendBadgeCacheSecondsEnvVar = "SCHRADER_BACKEND_BADGE_CACHE_SECONDS";
        private const string DefaultBackendPlayerStatePath = "/api/puck/players/{steamId}";
        private const string DefaultBackendMatchResultPath = "/api/puck/matches";
        private const string DefaultBackendLinkCompletePath = "/api/ranked/link/complete";
        private const string DefaultBackendMutePath = "/api/puck/moderation/mute";
        private const string DefaultBackendTempBanPath = "/api/puck/moderation/tempban";
        private const string DefaultBackendUnmutePath = "/api/puck/moderation/unmute";
        private const string DefaultBackendUnbanPath = "/api/puck/moderation/unban";
        private const int DefaultBackendTimeoutMs = 5000;
        private const int DefaultBackendModerationCacheSeconds = 60;
        private const int DefaultBackendBadgeCacheSeconds = 300;
        private const string CompetitiveServerMode = "competitive";
        private const string PublicServerMode = "public";
        private const string TrainingServerMode = "training";
        private static readonly TimeSpan BackendDiscordReminderInitialDelay = TimeSpan.Zero;
        private static readonly TimeSpan BackendDiscordReminderInterval = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan BackendDiscordLinkConsistencyGrace = TimeSpan.FromSeconds(15);
        private const float BackendDiscordReminderSweepIntervalSeconds = 1f;
        private const string DefaultBadgeColorHex = "#f7c66b";
        private const string DefaultTitleColorHex = "#cfe6ff";
        private static readonly HttpClient backendHttpClient = new HttpClient();
        private static readonly object backendStateLock = new object();
        private static readonly object backendNotificationLock = new object();
        private static readonly Dictionary<string, BackendPlayerState> backendPlayerStateBySteamId = new Dictionary<string, BackendPlayerState>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> backendPlayerFetchesInFlight = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<ulong> backendPendingBootstrapClientIds = new HashSet<ulong>();
        private static readonly Dictionary<string, DateTime> backendDiscordReminderScheduleBySteamId = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, DateTime> backendRecentDiscordLinkSuccessBySteamId = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ulong> backendPendingOnboardingClientBySteamId = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
        private static readonly Queue<BackendClientNotification> pendingBackendNotifications = new Queue<BackendClientNotification>();
        private static float lastBackendDiscordReminderSweepAt = -999f;
        private static BackendConfig backendConfig;
        private static bool backendConfigLoaded;

        private sealed class BackendConfig
        {
            public bool Enabled = true;
            public string ServerMode = CompetitiveServerMode;
            public string BaseUrl;
            public string ApiKey;
            public string PlayerStatePath = DefaultBackendPlayerStatePath;
            public string MatchResultPath = DefaultBackendMatchResultPath;
            public string LinkCompletePath = DefaultBackendLinkCompletePath;
            public string MutePath = DefaultBackendMutePath;
            public string TempBanPath = DefaultBackendTempBanPath;
            public string UnmutePath = DefaultBackendUnmutePath;
            public string UnbanPath = DefaultBackendUnbanPath;
            public int TimeoutMs = DefaultBackendTimeoutMs;
            public int ModerationCacheSeconds = DefaultBackendModerationCacheSeconds;
            public int BadgeCacheSeconds = DefaultBackendBadgeCacheSeconds;
        }

        private sealed class BackendClientNotification
        {
            public ulong ClientId;
            public string Message;
        }

        private sealed class BackendLinkCompleteRequest
        {
            public string SteamId;
            public string Code;
            public string GameDisplayName;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string GamePlayerNumber;
        }

        private sealed class BackendLinkCompleteResponse
        {
            public bool Ok;
            public bool? Success;
            public bool? Linked;
            public bool? Verified;
            public string DiscordId;
            public string SteamId;
            public string Error;
            public string Message;
            public string Code;
        }

        private sealed class BackendModerationCommandRequest
        {
            public string SteamId;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? DurationSeconds;
            public string Reason;
            public string IssuedBy;
            public string IssuedBySteamId;
            public string IssuerDisplayName;
            public string GameDisplayName;
            public string Source;
        }

        private enum BackendModerationActionType
        {
            Mute,
            TempBan,
            Unmute,
            Unban
        }

        private sealed class BackendModerationCommandResponse
        {
            public bool? Ok;
            public bool? Success;
            public string Error;
            public string Message;
            public string Code;
        }

        private sealed class BackendModerationTarget
        {
            public object Player;
            public ulong ClientId;
            public string SteamId;
            public string PlayerKey;
            public string DisplayName;
            public bool IsConnected;
        }

        private sealed class BackendPlayerState
        {
            public string SteamId;
            public bool IsMuted;
            public bool IsBanned;
            public bool IsDiscordLinked;
            public string DiscordId;
            public string TierKey;
            public string TierName;
            public string TierTag;
            public string TierColorHex;
            public string MuteReason;
            public string BanReason;
            public string TagText;
            public string TagColorHex;
            public string TitleText;
            public string TitleColorHex;
            public DateTime CachedAtUtc;
            public DateTime ModerationExpiresAtUtc;
            public DateTime BadgeExpiresAtUtc;
        }

        private sealed class BackendMatchResultReport
        {
            public string ServerName;
            public string CompletedAtUtc;
            public string WinningTeam;
            public int RedScore;
            public int BlueScore;
            public BackendMatchResultPlayerReport[] Players;
        }

        private sealed class BackendMatchResultPlayerReport
        {
            public string Id;
            public string SteamId;
            public string Username;
            public string Team;
            public int Goals;
            public int Assists;
            public int Saves;
            public int Shots;
            public int MmrBefore;
            public int MmrAfter;
            public int MmrDelta;
            public bool IsMvp;
            public bool ExcludedFromMmr;
            public bool IsSharedGoalie;
        }

        public static void HandleBackendPlayerSynchronized(ulong clientId)
        {
            try
            {
                if (clientId == 0)
                {
                    return;
                }

                if (!TryBootstrapBackendForClient(clientId))
                {
                    lock (backendStateLock)
                    {
                        backendPendingBootstrapClientIds.Add(clientId);
                    }

                    Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Delaying backend bootstrap until player exists. clientId={clientId}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] [BACKEND] Failed to handle synchronize-complete backend bootstrap: {ex.Message}");
            }
        }

        public static void ProcessPendingBackendSynchronizations()
        {
            List<ulong> pendingClientIds;
            lock (backendStateLock)
            {
                if (backendPendingBootstrapClientIds.Count == 0)
                {
                    return;
                }

                pendingClientIds = backendPendingBootstrapClientIds.ToList();
            }

            foreach (var clientId in pendingClientIds)
            {
                if (!IsConnectedClientId(clientId))
                {
                    lock (backendStateLock)
                    {
                        backendPendingBootstrapClientIds.Remove(clientId);
                    }

                    continue;
                }

                if (!TryBootstrapBackendForClient(clientId))
                {
                    continue;
                }

                lock (backendStateLock)
                {
                    backendPendingBootstrapClientIds.Remove(clientId);
                }
            }
        }

        public static string BuildBackendChatPrefix(object player, ulong fallbackClientId)
        {
            try
            {
                var steamId = NormalizeResolvedPlayerKey(TryGetPlayerId(player, fallbackClientId));
                if (!IsSteamIdentityKey(steamId))
                {
                    return string.Empty;
                }

                if (!TryGetBackendPlayerState(steamId, out var state, out var moderationFresh, out var badgeFresh))
                {
                    EnsureBackendPlayerStateQueued(steamId, "chat-prefix", forceRefresh: false);
                    return string.Empty;
                }

                if (!moderationFresh || !badgeFresh)
                {
                    EnsureBackendPlayerStateQueued(steamId, "chat-prefix-stale", forceRefresh: false);
                }

                var tierText = ResolveTierDisplayText(state, "chat-tier", steamId);
                var tagText = NormalizeVisibleBadgeText(state.TagText, "chat-tag", steamId);
                var titleText = NormalizeVisibleBadgeText(state.TitleText, "chat-title", steamId);

                var builder = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(tierText))
                {
                    AppendChatBadgeSegment(builder, tierText, NormalizeColorHex(state.TierColorHex, DefaultBadgeColorHex));
                }

                if (!string.IsNullOrWhiteSpace(tagText)
                    && !string.Equals(tagText, tierText, StringComparison.OrdinalIgnoreCase))
                {
                    AppendChatBadgeSegment(builder, tagText, NormalizeColorHex(state.TagColorHex, DefaultBadgeColorHex));
                }

                if (!string.IsNullOrWhiteSpace(titleText)
                    && !string.Equals(titleText, tierText, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(titleText, tagText, StringComparison.OrdinalIgnoreCase))
                {
                    AppendChatTitleSegment(builder, titleText, NormalizeColorHex(state.TitleColorHex, DefaultTitleColorHex));
                }

                var prefix = builder.ToString();
                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    prefix += " ";
                    Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Chat tier visual built. format=clean-inline steamId={steamId} tierKey={state.TierKey ?? "none"} tierName={state.TierName ?? "none"} tierTag={state.TierTag ?? "none"} tierColor={state.TierColorHex ?? "none"} tag={tagText ?? "none"} title={titleText ?? "none"} prefix={prefix}");
                }

                return prefix;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool IsBackendMuted(object player, ulong fallbackClientId, out string reason)
        {
            reason = null;
            try
            {
                var steamId = NormalizeResolvedPlayerKey(TryGetPlayerId(player, fallbackClientId));
                if (!IsSteamIdentityKey(steamId))
                {
                    return false;
                }

                if (!TryGetBackendPlayerState(steamId, out var state, out var moderationFresh, out _))
                {
                    EnsureBackendPlayerStateQueued(steamId, "chat-moderation", forceRefresh: false);
                    return false;
                }

                if (!moderationFresh)
                {
                    EnsureBackendPlayerStateQueued(steamId, "chat-moderation-stale", forceRefresh: false);
                }

                if (!state.IsMuted)
                {
                    return false;
                }

                reason = string.IsNullOrWhiteSpace(state.MuteReason)
                    ? "Muted by SpeedRanked moderation."
                    : state.MuteReason.Trim();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static ScoreboardBadgeStateMessage GetScoreboardBadgeStateForClient(ulong clientId)
        {
            return BuildScoreboardBadgeState();
        }

        public static DiscordOnboardingStateMessage GetDiscordOnboardingStateForClient(ulong clientId)
        {
            try
            {
                var config = GetBackendConfig();
                if (IsOnboardingBypassedServerMode(config))
                {
                    return new DiscordOnboardingStateMessage
                    {
                        IsResolved = true,
                        IsLinked = true,
                        IsPublicServer = IsPublicServerMode(config),
                        IsTrainingServer = IsTrainingServerMode(config)
                    };
                }

                if (clientId == 0 || !TryGetPlayerByClientId(clientId, out var player) || player == null)
                {
                    return DiscordOnboardingStateMessage.Unresolved();
                }

                var steamId = NormalizeResolvedPlayerKey(TryGetPlayerId(player, clientId));
                if (!IsSteamIdentityKey(steamId))
                {
                    return DiscordOnboardingStateMessage.Unresolved();
                }

                TryResolveDiscordVerificationStateCore(steamId, clientId, out var isResolved, out var isLinked, out var resolutionReason);
                if (!isResolved)
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Discord onboarding state unresolved. clientId={clientId} steamId={steamId} reason={resolutionReason}");
                    return DiscordOnboardingStateMessage.Unresolved();
                }

                TryGetBackendPlayerState(steamId, out var resolvedState, out _, out _);
                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Discord onboarding state resolved from backend cache. clientId={clientId} steamId={steamId} linked={isLinked} discordId={resolvedState?.DiscordId ?? "null"}");
                return new DiscordOnboardingStateMessage
                {
                    IsResolved = isResolved,
                    IsLinked = isLinked,
                    IsPublicServer = false,
                    IsTrainingServer = false
                };
            }
            catch
            {
                return DiscordOnboardingStateMessage.Unresolved();
            }
        }

        public static void HandleMandatoryVerificationDeclined(ulong clientId, string action)
        {
            try
            {
                var config = GetBackendConfig();
                if (IsOnboardingBypassedServerMode(config))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [BACKEND][VERIFY] Ignoring verification refusal because serverMode={config?.ServerMode ?? CompetitiveServerMode}. clientId={clientId} action={action ?? "unknown"}");
                    return;
                }

                object player = null;
                if (clientId != 0)
                {
                    TryGetPlayerByClientId(clientId, out player);
                }

                var hasState = TryResolveDiscordVerificationStateForPlayer(player, clientId, out var resolvedClientId, out var steamId, out var isResolved, out var isLinked, out var resolutionReason);
                var effectiveClientId = resolvedClientId != 0 ? resolvedClientId : clientId;
                var effectiveAction = string.IsNullOrWhiteSpace(action) ? "unknown" : action.Trim();
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND][VERIFY] Verification refusal received. clientId={effectiveClientId} steamId={steamId ?? "unknown"} linked={isLinked} resolved={isResolved} reason={resolutionReason ?? "unknown"} action={effectiveAction}");

                if (hasState && isResolved && isLinked)
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [BACKEND][VERIFY] Ignoring verification refusal because backend already confirmed linked=True. clientId={effectiveClientId} steamId={steamId ?? "unknown"} action={effectiveAction}");
                    return;
                }

                if (effectiveClientId != 0)
                {
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.DiscordModule, "Discord verification is required on this server. Disconnecting now.", ChatTone.Error), effectiveClientId);
                }

                var kicked = TryKickPlayer(steamId, effectiveClientId);
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND][VERIFY] Disconnect path executed. clientId={effectiveClientId} steamId={steamId ?? "unknown"} action={effectiveAction} kicked={kicked}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] [BACKEND][VERIFY] Failed to process verification refusal for clientId={clientId}: {ex.Message}");
            }
        }

        public static void ReportMatchResultToBackend(MatchResultMessage matchResult)
        {
            try
            {
                var config = GetBackendConfig();
                if (!IsBackendConfigured(config) || matchResult == null || !matchResult.IsVisible)
                {
                    return;
                }

                var report = BuildBackendMatchResultReport(matchResult);
                if (report == null)
                {
                    return;
                }

                LogBackendMatchResultAudit(report);
                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Match result queued for backend delivery. winner={matchResult.WinningTeam} players={matchResult.Players?.Length ?? 0}");
                _ = SendMatchResultToBackendAsync(config, report);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] [BACKEND] Failed to queue match result report: {ex.Message}");
            }
        }

        public static void StartDiscordLinkComplete(ulong clientId, string code)
        {
            try
            {
                var trimmedCode = string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim();
                if (clientId == 0)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(trimmedCode))
                {
                    SendSystemChatToClient(ChatStyle.Usage("/link CODE"), clientId);
                    return;
                }

                var config = GetBackendConfig();
                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] /link requested. clientId={clientId} configLoaded={(config != null)} baseUrl={config?.BaseUrl ?? "null"} linkCompletePath={config?.LinkCompletePath ?? "null"} authConfigured={!string.IsNullOrWhiteSpace(config?.ApiKey)}");
                if (!IsBackendConfigured(config))
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] /link aborted because backend config is not valid. clientId={clientId} configPath={GetBackendConfigPath()}");
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.DiscordModule, "Linking service is currently unavailable. Please try again later.", ChatTone.Error), clientId);
                    return;
                }

                if (!TryGetPlayerByClientId(clientId, out var player) || player == null)
                {
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.DiscordModule, "Could not identify your player for Discord linking.", ChatTone.Error), clientId);
                    return;
                }

                var steamId = NormalizeResolvedPlayerKey(TryGetPlayerId(player, clientId));
                if (!IsSteamIdentityKey(steamId))
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] /link aborted because authoritative SteamID could not be resolved. clientId={clientId} resolved={steamId ?? "null"}");
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.DiscordModule, "Could not resolve your SteamID for Discord linking.", ChatTone.Error), clientId);
                    return;
                }

                var gameDisplayName = StripRichTextTags(TryGetPlainPlayerName(player) ?? TryGetPlayerName(player))?.Trim();
                if (string.IsNullOrWhiteSpace(gameDisplayName))
                {
                    gameDisplayName = null;
                }

                var gamePlayerNumber = TryGetPlayerNumber(player, out var resolvedPlayerNumber) && resolvedPlayerNumber > 0
                    ? resolvedPlayerNumber.ToString()
                    : null;

                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] /link resolved authoritative SteamID. clientId={clientId} steamId={steamId} gameDisplayName={gameDisplayName ?? "null"} gamePlayerNumber={gamePlayerNumber ?? "null"} code={trimmedCode}");
                SendSystemChatToClient(ChatStyle.Message(ChatStyle.DiscordModule, "Submitting your Discord link code.", ChatTone.Info), clientId);
                _ = CompleteDiscordLinkAsync(config, clientId, steamId, trimmedCode, gameDisplayName, gamePlayerNumber);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] [BACKEND] Failed to start Discord link completion: {ex.Message}");
                SendSystemChatToClient(ChatStyle.Message(ChatStyle.DiscordModule, "Linking service is currently unavailable. Please try again later.", ChatTone.Error), clientId);
            }
        }

        public static bool TryStartBackendMute(object issuerPlayer, ulong issuerClientId, string commandArgs, out string errorMessage)
        {
            return TryStartBackendModerationCommand(issuerPlayer, issuerClientId, commandArgs, BackendModerationActionType.Mute, out errorMessage);
        }

        public static bool TryStartBackendTempBan(object issuerPlayer, ulong issuerClientId, string commandArgs, out string errorMessage)
        {
            return TryStartBackendModerationCommand(issuerPlayer, issuerClientId, commandArgs, BackendModerationActionType.TempBan, out errorMessage);
        }

        public static bool TryStartBackendUnmute(object issuerPlayer, ulong issuerClientId, string commandArgs, out string errorMessage)
        {
            return TryStartBackendModerationCommand(issuerPlayer, issuerClientId, commandArgs, BackendModerationActionType.Unmute, out errorMessage);
        }

        public static bool TryStartBackendUnban(object issuerPlayer, ulong issuerClientId, string commandArgs, out string errorMessage)
        {
            return TryStartBackendModerationCommand(issuerPlayer, issuerClientId, commandArgs, BackendModerationActionType.Unban, out errorMessage);
        }

        public static void ProcessBackendNotificationQueue()
        {
            while (true)
            {
                BackendClientNotification notification = null;
                lock (backendNotificationLock)
                {
                    if (pendingBackendNotifications.Count == 0)
                    {
                        break;
                    }

                    notification = pendingBackendNotifications.Dequeue();
                }

                if (notification == null || notification.ClientId == 0 || string.IsNullOrWhiteSpace(notification.Message))
                {
                    continue;
                }

                try
                {
                    SendSystemChatToClient(notification.Message, notification.ClientId);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Failed to deliver queued client notification: {ex.Message}");
                }
            }
        }

        public static void FlushPendingDiscordOnboardingPublishes()
        {
            List<KeyValuePair<string, ulong>> pendingPublishes;
            lock (backendStateLock)
            {
                if (backendPendingOnboardingClientBySteamId.Count == 0)
                {
                    return;
                }

                pendingPublishes = backendPendingOnboardingClientBySteamId.ToList();
            }

            foreach (var pendingPublish in pendingPublishes)
            {
                if (!TryGetBackendPlayerState(pendingPublish.Key, out var state, out _, out _) || state == null)
                {
                    continue;
                }

                if (!PublishDiscordOnboardingState(pendingPublish.Key, state, pendingPublish.Value))
                {
                    continue;
                }

                lock (backendStateLock)
                {
                    if (backendPendingOnboardingClientBySteamId.TryGetValue(pendingPublish.Key, out var mappedClientId)
                        && mappedClientId == pendingPublish.Value)
                    {
                        backendPendingOnboardingClientBySteamId.Remove(pendingPublish.Key);
                    }
                }
            }
        }

        private static bool TryBootstrapBackendForClient(ulong clientId)
        {
            if (clientId == 0 || !TryGetPlayerByClientId(clientId, out var player) || player == null)
            {
                return false;
            }

            var steamId = NormalizeResolvedPlayerKey(TryGetPlayerId(player, clientId));
            if (!IsSteamIdentityKey(steamId))
            {
                return false;
            }

            lock (backendStateLock)
            {
                backendDiscordReminderScheduleBySteamId[steamId] = DateTime.UtcNow.Add(BackendDiscordReminderInitialDelay);
            }

            RankedOverlayNetwork.PublishScoreboardBadgesToClient(clientId, GetScoreboardBadgeStateForClient(clientId));
            var config = GetBackendConfig();
            if (IsOnboardingBypassedServerMode(config))
            {
                RankedOverlayNetwork.PublishDiscordOnboardingStateToClient(clientId, new DiscordOnboardingStateMessage
                {
                    IsResolved = true,
                    IsLinked = true,
                    IsPublicServer = IsPublicServerMode(config),
                    IsTrainingServer = IsTrainingServerMode(config)
                });
                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Backend bootstrap skipped onboarding because serverMode={config?.ServerMode ?? CompetitiveServerMode}. clientId={clientId} steamId={steamId}");
            }
            else if (TryGetBackendPlayerState(steamId, out var cachedState, out _, out _)
                && cachedState != null
                && (cachedState.IsDiscordLinked || !string.IsNullOrWhiteSpace(cachedState.DiscordId)))
            {
                RankedOverlayNetwork.PublishDiscordOnboardingStateToClient(clientId, new DiscordOnboardingStateMessage
                {
                    IsResolved = true,
                    IsLinked = true,
                    IsPublicServer = false,
                    IsTrainingServer = false
                });
                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Backend bootstrap skipped onboarding because cached backend state is already linked. clientId={clientId} steamId={steamId}");
            }
            else
            {
                RankedOverlayNetwork.PublishDiscordOnboardingStateToClient(clientId, DiscordOnboardingStateMessage.Unresolved());
                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Backend bootstrap waiting for fresh onboarding decision. clientId={clientId} steamId={steamId}");
            }

            EnsureBackendPlayerStateQueued(steamId, "synchronize-complete", forceRefresh: true, onboardingClientId: clientId);
            Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Backend bootstrap armed for client {clientId}. steamId={steamId}");
            return true;
        }

        private static bool TryStartBackendModerationCommand(object issuerPlayer, ulong issuerClientId, string commandArgs, BackendModerationActionType actionType, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                var commandName = GetBackendModerationCommandName(actionType);
                if (!TryParseBackendModerationCommandArgs(commandArgs, actionType, out var rawTarget, out var durationSeconds, out var durationText, out var reason, out errorMessage))
                {
                    return false;
                }

                var config = GetBackendConfig();
                if (!IsBackendConfigured(config))
                {
                    errorMessage = "Moderation service is currently unavailable. Please try again later.";
                    return false;
                }

                var configuredPath = GetConfiguredModerationPath(config, actionType);
                if (string.IsNullOrWhiteSpace(configuredPath))
                {
                    errorMessage = "Moderation service is currently unavailable. Please try again later.";
                    return false;
                }

                if (!TryResolveBackendModerationIssuer(issuerPlayer, issuerClientId, out var issuerSteamId, out var issuerDisplayName))
                {
                    errorMessage = "Could not resolve your authoritative SteamID for moderation.";
                    return false;
                }

                if (!TryResolveBackendModerationTarget(rawTarget, out var target, out errorMessage))
                {
                    return false;
                }

                SendSystemChatToClient($"<size=14>Submitting /{commandName} request...</size>", issuerClientId);
                _ = SendBackendModerationCommandAsync(config, configuredPath, actionType, issuerClientId, issuerSteamId, issuerDisplayName, target, durationSeconds, durationText, reason);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] [BACKEND] Failed to start backend moderation command: {ex.Message}");
                errorMessage = "Moderation service is currently unavailable. Please try again later.";
                return false;
            }
        }

        private static bool TryParseBackendModerationCommandArgs(string commandArgs, BackendModerationActionType actionType, out string rawTarget, out int? durationSeconds, out string durationText, out string reason, out string errorMessage)
        {
            rawTarget = null;
            durationSeconds = null;
            durationText = null;
            reason = null;
            errorMessage = null;

            var commandName = GetBackendModerationCommandName(actionType);

            var trimmedArgs = StripRichTextTags(commandArgs)?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedArgs))
            {
                errorMessage = BuildBackendModerationUsage(commandName, BackendModerationActionRequiresDuration(actionType));
                return false;
            }

            var parts = trimmedArgs.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (BackendModerationActionRequiresDuration(actionType))
            {
                if (parts.Length < 3)
                {
                    errorMessage = BuildBackendModerationUsage(commandName, true);
                    return false;
                }

                var parsedDurationSeconds = 0;
                var durationIndex = -1;
                for (var index = 1; index < parts.Length - 1; index++)
                {
                    if (TryParseBackendModerationDuration(parts[index], out parsedDurationSeconds, out durationText))
                    {
                        durationIndex = index;
                        durationSeconds = parsedDurationSeconds;
                        break;
                    }
                }

                if (durationIndex <= 0)
                {
                    errorMessage = $"Duration must use s, m, h, d, or w. Example: /{commandName} #4 30m spam.";
                    return false;
                }

                rawTarget = string.Join(" ", parts.Take(durationIndex)).Trim();
                reason = StripRichTextTags(string.Join(" ", parts.Skip(durationIndex + 1)))?.Trim();
                if (string.IsNullOrWhiteSpace(rawTarget) || string.IsNullOrWhiteSpace(reason))
                {
                    errorMessage = BuildBackendModerationUsage(commandName, true);
                    return false;
                }
            }
            else
            {
                rawTarget = parts[0];
                reason = parts.Length > 1
                    ? StripRichTextTags(string.Join(" ", parts.Skip(1)))?.Trim()
                    : null;
                if (string.IsNullOrWhiteSpace(rawTarget))
                {
                    errorMessage = BuildBackendModerationUsage(commandName, false);
                    return false;
                }
            }

            return true;
        }

        private static string BuildBackendModerationUsage(string commandName, bool requiresDuration)
        {
            return requiresDuration
                ? $"Usage: /{commandName} <player|steamId|#number> <duration> <reason...>"
                : $"Usage: /{commandName} <player|steamId|#number> [reason...]";
        }

        private static bool BackendModerationActionRequiresDuration(BackendModerationActionType actionType)
        {
            return actionType == BackendModerationActionType.Mute || actionType == BackendModerationActionType.TempBan;
        }

        private static string GetBackendModerationCommandName(BackendModerationActionType actionType)
        {
            switch (actionType)
            {
                case BackendModerationActionType.TempBan:
                    return "tempban";
                case BackendModerationActionType.Unmute:
                    return "unmute";
                case BackendModerationActionType.Unban:
                    return "unban";
                default:
                    return "mute";
            }
        }

        private static string GetConfiguredModerationPath(BackendConfig config, BackendModerationActionType actionType)
        {
            if (config == null)
            {
                return null;
            }

            switch (actionType)
            {
                case BackendModerationActionType.TempBan:
                    return config.TempBanPath;
                case BackendModerationActionType.Unmute:
                    return config.UnmutePath;
                case BackendModerationActionType.Unban:
                    return config.UnbanPath;
                default:
                    return config.MutePath;
            }
        }

        private static bool TryParseBackendModerationDuration(string durationToken, out int durationSeconds, out string durationText)
        {
            durationSeconds = 0;
            durationText = null;

            var trimmed = StripRichTextTags(durationToken)?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length < 2)
            {
                return false;
            }

            var unit = char.ToLowerInvariant(trimmed[trimmed.Length - 1]);
            var amountText = trimmed.Substring(0, trimmed.Length - 1);
            if (!long.TryParse(amountText, out var amount) || amount <= 0)
            {
                return false;
            }

            long multiplier;
            switch (unit)
            {
                case 's': multiplier = 1L; break;
                case 'm': multiplier = 60L; break;
                case 'h': multiplier = 60L * 60L; break;
                case 'd': multiplier = 60L * 60L * 24L; break;
                case 'w': multiplier = 60L * 60L * 24L * 7L; break;
                default: return false;
            }

            var totalSeconds = amount * multiplier;
            if (totalSeconds <= 0 || totalSeconds > int.MaxValue)
            {
                return false;
            }

            durationSeconds = (int)totalSeconds;
            durationText = $"{amount}{unit}";
            return true;
        }

        private static bool TryResolveBackendModerationIssuer(object issuerPlayer, ulong issuerClientId, out string issuerSteamId, out string issuerDisplayName)
        {
            issuerSteamId = null;
            issuerDisplayName = null;

            var resolvedIssuerPlayer = issuerPlayer;
            if (resolvedIssuerPlayer == null && issuerClientId != 0)
            {
                TryGetPlayerByClientId(issuerClientId, out resolvedIssuerPlayer);
            }

            issuerSteamId = NormalizeResolvedPlayerKey(TryGetPlayerId(resolvedIssuerPlayer, issuerClientId));
            if (!IsSteamIdentityKey(issuerSteamId))
            {
                return false;
            }

            issuerDisplayName = StripRichTextTags(TryGetPlainPlayerName(resolvedIssuerPlayer) ?? TryGetPlayerName(resolvedIssuerPlayer))?.Trim();
            if (string.IsNullOrWhiteSpace(issuerDisplayName))
            {
                issuerDisplayName = issuerSteamId;
            }

            return true;
        }

        private static bool TryResolveBackendModerationTarget(string rawTarget, out BackendModerationTarget target, out string errorMessage)
        {
            target = null;
            errorMessage = null;

            var cleanTarget = StripRichTextTags(rawTarget)?.Trim();
            if (string.IsNullOrWhiteSpace(cleanTarget))
            {
                errorMessage = "Could not resolve that player. Use an exact name, SteamID, or #playerNumber.";
                return false;
            }

            if (ulong.TryParse(cleanTarget, out var explicitSteamId) && explicitSteamId != 0)
            {
                var normalizedSteamId = NormalizeResolvedPlayerKey(explicitSteamId.ToString());
                if (!IsSteamIdentityKey(normalizedSteamId))
                {
                    errorMessage = "Could not resolve an authoritative SteamID for that player.";
                    return false;
                }

                var resolvedTarget = new BackendModerationTarget
                {
                    SteamId = normalizedSteamId,
                    PlayerKey = normalizedSteamId,
                    DisplayName = normalizedSteamId,
                    IsConnected = false
                };

                if (TryFindConnectedPlayerBySteamId(normalizedSteamId, out var livePlayer, out var livePlayerName))
                {
                    TryGetClientId(livePlayer, out var liveClientId);
                    resolvedTarget.Player = livePlayer;
                    resolvedTarget.ClientId = liveClientId;
                    resolvedTarget.DisplayName = StripRichTextTags(livePlayerName ?? TryGetPlainPlayerName(livePlayer) ?? TryGetPlayerName(livePlayer))?.Trim() ?? normalizedSteamId;
                    resolvedTarget.IsConnected = true;
                }

                target = resolvedTarget;
                return true;
            }

            if (!TryResolveConnectedPlayerByTarget(cleanTarget, out var player, out var clientId, out var playerKey, out var playerName, out errorMessage))
            {
                return false;
            }

            var steamId = NormalizeResolvedPlayerKey(TryGetPlayerId(player, clientId));
            if (!IsSteamIdentityKey(steamId))
            {
                errorMessage = "Could not resolve an authoritative SteamID for that player.";
                return false;
            }

            target = new BackendModerationTarget
            {
                Player = player,
                ClientId = clientId,
                SteamId = steamId,
                PlayerKey = playerKey ?? steamId,
                DisplayName = StripRichTextTags(playerName ?? TryGetPlainPlayerName(player) ?? TryGetPlayerName(player))?.Trim() ?? steamId,
                IsConnected = true
            };
            return true;
        }

        private static async Task SendBackendModerationCommandAsync(BackendConfig config, string configuredPath, BackendModerationActionType actionType, ulong issuerClientId, string issuerSteamId, string issuerDisplayName, BackendModerationTarget target, int? durationSeconds, string durationText, string reason)
        {
            var commandName = GetBackendModerationCommandName(actionType);

            try
            {
                var requestUrl = BuildConfiguredUrl(config.BaseUrl, configuredPath, target?.SteamId);
                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] /{commandName} prepared request. issuerClientId={issuerClientId} issuerSteamId={issuerSteamId} issuerDisplayName={issuerDisplayName ?? "null"} targetSteamId={target?.SteamId ?? "null"} targetDisplayName={target?.DisplayName ?? "null"} targetOnline={(target != null && target.IsConnected)} durationSeconds={(durationSeconds.HasValue ? durationSeconds.Value.ToString() : "null")} url={requestUrl ?? "null"} authConfigured={!string.IsNullOrWhiteSpace(config?.ApiKey)}");
                if (string.IsNullOrWhiteSpace(requestUrl) || target == null || !IsSteamIdentityKey(target.SteamId))
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] /{commandName} aborted because request URL or target SteamID was invalid.");
                    EnqueueBackendNotification(issuerClientId, "<size=14><color=#ff6666>Moderation service is currently unavailable. Please try again later.</color></size>");
                    return;
                }

                var payload = new BackendModerationCommandRequest
                {
                    SteamId = target.SteamId,
                    DurationSeconds = durationSeconds,
                    Reason = reason,
                    IssuedBy = issuerSteamId,
                    IssuedBySteamId = issuerSteamId,
                    IssuerDisplayName = issuerDisplayName,
                    GameDisplayName = target.DisplayName,
                    Source = "puck_mod"
                };

                var json = JsonConvert.SerializeObject(payload, Formatting.None);
                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] /{commandName} payload={json}");
                using (var request = new HttpRequestMessage(HttpMethod.Post, requestUrl))
                {
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    if (!string.IsNullOrWhiteSpace(config.ApiKey))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey.Trim());
                    }

                    using (var timeoutCts = new CancellationTokenSource(Math.Max(1000, config.TimeoutMs)))
                    using (var response = await backendHttpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false))
                    {
                        Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] /{commandName} response received. issuerSteamId={issuerSteamId} targetSteamId={target.SteamId} status={(int)response.StatusCode} reason={response.ReasonPhrase}");

                        string responseText;
                        try
                        {
                            responseText = await AwaitWithTimeoutAsync(response.Content.ReadAsStringAsync(), Math.Max(1000, config.TimeoutMs), $"Backend {commandName} response body timed out").ConfigureAwait(false);
                        }
                        catch (TimeoutException ex)
                        {
                            Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] /{commandName} body read timed out for {target.SteamId}: {ex.Message}");
                            EnqueueBackendNotification(issuerClientId, "<size=14><color=#ff6666>Moderation service is currently unavailable. Please try again later.</color></size>");
                            return;
                        }

                        var moderationResponse = ParseBackendModerationCommandResponse(responseText);
                        if (response.IsSuccessStatusCode && IsSuccessfulBackendModerationResponse(moderationResponse, responseText))
                        {
                            ApplySuccessfulBackendModeration(config, target, actionType, reason, $"/{commandName}-success");
                            Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] /{commandName} branch=success issuerSteamId={issuerSteamId} targetSteamId={target.SteamId} durationSeconds={(durationSeconds.HasValue ? durationSeconds.Value.ToString() : "null")} effect={BuildBackendModerationSuccessEffect(actionType)} response={responseText}");
                            EnqueueBackendNotification(issuerClientId, BuildBackendModerationIssuerSuccessMessage(actionType, target.DisplayName ?? target.SteamId, durationText, reason, target.IsConnected));
                            if (actionType == BackendModerationActionType.Mute && target.IsConnected && target.ClientId != 0)
                            {
                                EnqueueBackendNotification(target.ClientId, BuildBackendModerationTargetMuteMessage(durationText, reason));
                            }
                            else if (actionType == BackendModerationActionType.Unmute && target.IsConnected && target.ClientId != 0)
                            {
                                EnqueueBackendNotification(target.ClientId, BuildBackendModerationTargetUnmuteMessage(reason));
                            }

                            return;
                        }

                        var failureMessage = BuildBackendModerationFailureMessage(commandName, (int)response.StatusCode, responseText, out var failureBranch);
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] /{commandName} branch={failureBranch} issuerSteamId={issuerSteamId} targetSteamId={target.SteamId} durationSeconds={(durationSeconds.HasValue ? durationSeconds.Value.ToString() : "null")} status={(int)response.StatusCode} reason={response.ReasonPhrase} response={responseText}");
                        EnqueueBackendNotification(issuerClientId, failureMessage);
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] /{commandName} branch=unavailable-timeout issuerSteamId={issuerSteamId} targetSteamId={target?.SteamId ?? "null"} error={ex.Message}");
                EnqueueBackendNotification(issuerClientId, "<size=14><color=#ff6666>Moderation service is currently unavailable. Please try again later.</color></size>");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] /{commandName} branch=unavailable-exception issuerSteamId={issuerSteamId} targetSteamId={target?.SteamId ?? "null"} error={ex.Message}");
                EnqueueBackendNotification(issuerClientId, "<size=14><color=#ff6666>Moderation service is currently unavailable. Please try again later.</color></size>");
            }
        }

        private static BackendModerationCommandResponse ParseBackendModerationCommandResponse(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return null;
            }

            try
            {
                var root = JObject.Parse(responseText);
                var payload = SelectPrimaryPayload(root);
                return new BackendModerationCommandResponse
                {
                    Ok = FirstBoolean(payload, "ok", "isOk") ?? FirstBoolean(root, "ok", "isOk"),
                    Success = FirstBoolean(payload, "success", "isSuccess") ?? FirstBoolean(root, "success", "isSuccess"),
                    Error = FirstNonEmptyString(payload, "error", "errorMessage") ?? FirstNonEmptyString(root, "error", "errorMessage"),
                    Message = FirstNonEmptyString(payload, "message", "detail") ?? FirstNonEmptyString(root, "message", "detail"),
                    Code = FirstNonEmptyString(payload, "code") ?? FirstNonEmptyString(root, "code")
                };
            }
            catch
            {
                return null;
            }
        }

        private static bool IsSuccessfulBackendModerationResponse(BackendModerationCommandResponse response, string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return true;
            }

            if (response == null)
            {
                return true;
            }

            if (response.Ok == false || response.Success == false)
            {
                return false;
            }

            if (response.Ok == true || response.Success == true)
            {
                return true;
            }

            return string.IsNullOrWhiteSpace(response.Error);
        }

        private static string BuildBackendModerationFailureMessage(string commandName, int statusCode, string responseText, out string failureBranch)
        {
            failureBranch = "rejected";
            var response = ParseBackendModerationCommandResponse(responseText);
            var detail = response?.Error;
            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = response?.Message;
            }

            if (statusCode == 400 || statusCode == 404 || statusCode == 409 || statusCode == 422)
            {
                failureBranch = "validation";
                if (!string.IsNullOrWhiteSpace(detail))
                {
                    return $"<size=14><color=#ff6666>/{commandName} failed:</color> {EscapeRichText(detail)}</size>";
                }

                return $"<size=14><color=#ff6666>/{commandName} failed because the backend rejected the request.</color></size>";
            }

            if (statusCode == 401 || statusCode == 403)
            {
                failureBranch = "forbidden";
                return $"<size=14><color=#ff6666>/{commandName} failed because the moderation service rejected the request.</color></size>";
            }

            failureBranch = "unavailable";
            return $"<size=14><color=#ff6666>/{commandName} service is currently unavailable. Please try again later.</color></size>";
        }

        private static string BuildBackendModerationIssuerSuccessMessage(BackendModerationActionType actionType, string targetDisplayName, string durationText, string reason, bool targetWasConnected)
        {
            var builder = new StringBuilder();
            builder.Append("<size=14><color=#00ff99>");
            switch (actionType)
            {
                case BackendModerationActionType.TempBan:
                    builder.Append("Temporary ban applied to <b>");
                    break;
                case BackendModerationActionType.Unmute:
                    builder.Append("Unmuted <b>");
                    break;
                case BackendModerationActionType.Unban:
                    builder.Append("Unbanned <b>");
                    break;
                default:
                    builder.Append("Muted <b>");
                    break;
            }

            builder.Append(EscapeRichText(targetDisplayName ?? "player"));
            builder.Append("</b>");
            if (BackendModerationActionRequiresDuration(actionType))
            {
                builder.Append(" for <b>");
                builder.Append(EscapeRichText(durationText ?? "unknown"));
                builder.Append("</b>");
            }

            builder.Append(".</color>");
            if (!string.IsNullOrWhiteSpace(reason))
            {
                builder.Append(" <color=#d7e7f3>Reason: ");
                builder.Append(EscapeRichText(reason));
                builder.Append(".</color>");
            }

            if (actionType == BackendModerationActionType.TempBan)
            {
                builder.Append(targetWasConnected
                    ? " <color=#ffcc66>Live player disconnected immediately.</color>"
                    : " <color=#9dc4de>Target was not connected.</color>");
            }

            builder.Append("</size>");
            return builder.ToString();
        }

        private static string BuildBackendModerationTargetMuteMessage(string durationText, string reason)
        {
            var builder = new StringBuilder();
            builder.Append("<size=14><color=#ffcc66>You have been muted for <b>");
            builder.Append(EscapeRichText(durationText ?? "unknown"));
            builder.Append("</b>.</color>");
            if (!string.IsNullOrWhiteSpace(reason))
            {
                builder.Append(" <color=#d7e7f3>Reason: ");
                builder.Append(EscapeRichText(reason));
                builder.Append(".</color>");
            }

            builder.Append("</size>");
            return builder.ToString();
        }

        private static string BuildBackendModerationTargetUnmuteMessage(string reason)
        {
            var builder = new StringBuilder();
            builder.Append("<size=14><color=#00ff99>Your mute has been lifted.</color>");
            if (!string.IsNullOrWhiteSpace(reason))
            {
                builder.Append(" <color=#d7e7f3>Reason: ");
                builder.Append(EscapeRichText(reason));
                builder.Append(".</color>");
            }

            builder.Append("</size>");
            return builder.ToString();
        }

        private static string BuildBackendModerationSuccessEffect(BackendModerationActionType actionType)
        {
            switch (actionType)
            {
                case BackendModerationActionType.TempBan:
                    return "tempban-applied";
                case BackendModerationActionType.Unmute:
                    return "mute-cleared";
                case BackendModerationActionType.Unban:
                    return "ban-cleared";
                default:
                    return "mute-applied";
            }
        }

        private static void ApplySuccessfulBackendModeration(BackendConfig config, BackendModerationTarget target, BackendModerationActionType actionType, string reason, string source)
        {
            if (target == null || !IsSteamIdentityKey(target.SteamId))
            {
                return;
            }

            lock (backendStateLock)
            {
                if (!backendPlayerStateBySteamId.TryGetValue(target.SteamId, out var state) || state == null)
                {
                    state = new BackendPlayerState
                    {
                        SteamId = target.SteamId,
                        BadgeExpiresAtUtc = DateTime.UtcNow
                    };
                    backendPlayerStateBySteamId[target.SteamId] = state;
                }

                var nowUtc = DateTime.UtcNow;
                state.SteamId = target.SteamId;
                state.CachedAtUtc = nowUtc;
                state.ModerationExpiresAtUtc = nowUtc.AddSeconds(Math.Max(15, config != null ? config.ModerationCacheSeconds : DefaultBackendModerationCacheSeconds));
                if (state.BadgeExpiresAtUtc < nowUtc)
                {
                    state.BadgeExpiresAtUtc = nowUtc;
                }

                if (actionType == BackendModerationActionType.TempBan || actionType == BackendModerationActionType.Unban)
                {
                    state.IsBanned = actionType == BackendModerationActionType.TempBan;
                    state.BanReason = actionType == BackendModerationActionType.TempBan
                        ? (string.IsNullOrWhiteSpace(reason) ? state.BanReason : reason.Trim())
                        : null;
                }
                else
                {
                    state.IsMuted = actionType == BackendModerationActionType.Mute;
                    state.MuteReason = actionType == BackendModerationActionType.Mute
                        ? (string.IsNullOrWhiteSpace(reason) ? state.MuteReason : reason.Trim())
                        : null;
                }
            }

            if (actionType == BackendModerationActionType.TempBan)
            {
                EnforceConnectedBackendModeration(target.SteamId, new BackendPlayerState
                {
                    SteamId = target.SteamId,
                    IsBanned = true,
                    BanReason = reason
                }, source);
            }

            EnsureBackendPlayerStateQueued(target.SteamId, source, forceRefresh: true);
        }

        public static void UpdateBackendDiscordLinkReminders()
        {
            try
            {
                var config = GetBackendConfig();
                if (!IsBackendConfigured(config))
                {
                    return;
                }

                var nowUnscaledTime = Time.unscaledTime;
                if (nowUnscaledTime - lastBackendDiscordReminderSweepAt < BackendDiscordReminderSweepIntervalSeconds)
                {
                    return;
                }

                lastBackendDiscordReminderSweepAt = nowUnscaledTime;

                var nowUtc = DateTime.UtcNow;
                var connectedSteamIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var player in GetAllPlayers() ?? new List<object>())
                {
                    if (!TryBuildConnectedPlayerSnapshot(player, out var snapshot) || snapshot == null || snapshot.clientId == 0)
                    {
                        continue;
                    }

                    var steamId = NormalizeResolvedPlayerKey(ResolveParticipantIdToKey(snapshot) ?? snapshot.playerId);
                    if (!IsSteamIdentityKey(steamId))
                    {
                        continue;
                    }

                    connectedSteamIds.Add(steamId);
                    if (!TryGetBackendPlayerState(steamId, out var state, out var moderationFresh, out var badgeFresh) || state == null)
                    {
                        continue;
                    }

                    if (!moderationFresh || !badgeFresh)
                    {
                        EnsureBackendPlayerStateQueued(steamId, "discord-link-reminder-stale", forceRefresh: false);
                    }

                    if (state.IsDiscordLinked || !string.IsNullOrWhiteSpace(state.DiscordId))
                    {
                        lock (backendStateLock)
                        {
                            if (backendDiscordReminderScheduleBySteamId.Remove(steamId))
                            {
                                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Discord link reminder suppressed because player is already linked. steamId={steamId} clientId={snapshot.clientId}");
                            }
                        }

                        continue;
                    }

                    var shouldSendReminder = false;
                    lock (backendStateLock)
                    {
                        if (!backendDiscordReminderScheduleBySteamId.TryGetValue(steamId, out var nextReminderUtc))
                        {
                            backendDiscordReminderScheduleBySteamId[steamId] = nowUtc.Add(BackendDiscordReminderInitialDelay);
                        }
                        else if (nextReminderUtc <= nowUtc)
                        {
                            backendDiscordReminderScheduleBySteamId[steamId] = nowUtc.Add(BackendDiscordReminderInterval);
                            shouldSendReminder = true;
                        }
                    }

                    if (shouldSendReminder)
                    {
                            SendSystemChatToClient(ChatStyle.Message(ChatStyle.DiscordModule, $"Join the Discord and complete verification to publish your stats. Use {ChatStyle.Command("/discord")} and then {ChatStyle.Command("/link CODE")}.", ChatTone.Info), snapshot.clientId);
                    }
                }

                lock (backendStateLock)
                {
                    var staleKeys = backendDiscordReminderScheduleBySteamId.Keys
                        .Where(key => !connectedSteamIds.Contains(key))
                        .ToList();
                    foreach (var staleKey in staleKeys)
                    {
                        backendDiscordReminderScheduleBySteamId.Remove(staleKey);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Failed to update Discord link reminders: {ex.Message}");
            }
        }

        private static ScoreboardBadgeStateMessage BuildScoreboardBadgeState()
        {
            var entryMap = new Dictionary<string, ScoreboardBadgeEntryMessage>(StringComparer.OrdinalIgnoreCase);
            foreach (var player in GetAllPlayers() ?? new List<object>())
            {
                if (!TryBuildConnectedPlayerSnapshot(player, out var snapshot) || snapshot == null)
                {
                    continue;
                }

                var steamId = NormalizeResolvedPlayerKey(ResolveParticipantIdToKey(snapshot) ?? snapshot.playerId);
                if (!IsSteamIdentityKey(steamId))
                {
                    continue;
                }

                if (!TryGetBackendPlayerState(steamId, out var state, out _, out _))
                {
                    continue;
                }

                var badgeText = BuildCompactScoreboardBadgeText(state);
                if (string.IsNullOrWhiteSpace(badgeText))
                {
                    continue;
                }

                var badgeColorHex = ResolveCompactBadgeColor(state);
                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Scoreboard tier visual built. format=clean-inline steamId={steamId} tierKey={state.TierKey ?? "none"} tierName={state.TierName ?? "none"} tierTag={state.TierTag ?? "none"} badgeText={badgeText} color={badgeColorHex}");

                entryMap[steamId] = new ScoreboardBadgeEntryMessage
                {
                    PlayerId = steamId,
                    ClientId = snapshot.clientId,
                    BadgeText = badgeText,
                    ColorHex = badgeColorHex
                };
            }

            return new ScoreboardBadgeStateMessage
            {
                Players = entryMap.Values
                    .OrderBy(entry => entry.BadgeText ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.PlayerId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.ClientId)
                    .ToArray()
            };
        }

        private static void PublishScoreboardBadgeState()
        {
            RankedOverlayNetwork.PublishScoreboardBadges(BuildScoreboardBadgeState());
        }

        private static void EnsureBackendPlayerStateQueued(string steamId, string reason, bool forceRefresh, ulong onboardingClientId = 0)
        {
            var normalizedSteamId = NormalizeResolvedPlayerKey(steamId);
            if (!IsSteamIdentityKey(normalizedSteamId))
            {
                return;
            }

            var config = GetBackendConfig();
            if (!IsBackendConfigured(config))
            {
                return;
            }

            if (!forceRefresh && TryGetBackendPlayerState(normalizedSteamId, out _, out var moderationFresh, out var badgeFresh) && moderationFresh && badgeFresh)
            {
                return;
            }

            lock (backendStateLock)
            {
                if (onboardingClientId != 0)
                {
                    backendPendingOnboardingClientBySteamId[normalizedSteamId] = onboardingClientId;
                }

                if (backendPlayerFetchesInFlight.Contains(normalizedSteamId))
                {
                    return;
                }

                backendPlayerFetchesInFlight.Add(normalizedSteamId);
            }

            _ = RefreshBackendPlayerStateAsync(config, normalizedSteamId, reason ?? "unknown");
        }

        private static async Task RefreshBackendPlayerStateAsync(BackendConfig config, string steamId, string reason)
        {
            try
            {
                var requestUrl = BuildConfiguredUrl(config.BaseUrl, config.PlayerStatePath, steamId);
                if (string.IsNullOrWhiteSpace(requestUrl))
                {
                    return;
                }

                using (var request = new HttpRequestMessage(HttpMethod.Get, requestUrl))
                {
                    if (!string.IsNullOrWhiteSpace(config.ApiKey))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey.Trim());
                    }

                    using (var timeoutCts = new CancellationTokenSource(Math.Max(1000, config.TimeoutMs)))
                    using (var response = await backendHttpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false))
                    {
                        var responseText = string.Empty;
                        try
                        {
                            responseText = await AwaitWithTimeoutAsync(response.Content.ReadAsStringAsync(), Math.Max(1000, config.TimeoutMs), "Backend player state response body timed out").ConfigureAwait(false);
                        }
                        catch (TimeoutException ex)
                        {
                            Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Player state body read timed out for {steamId}: {ex.Message}");
                            return;
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Player state fetch failed for {steamId}: {(int)response.StatusCode} {response.ReasonPhrase}. Response={responseText}");
                            return;
                        }

                        var state = ParseBackendPlayerState(steamId, responseText, config);
                        if (state == null)
                        {
                            return;
                        }

                        var nowUtc = DateTime.UtcNow;
                        var preservedRecentLinkSuccess = false;
                        ulong pendingOnboardingClientId = 0;
                        lock (backendStateLock)
                        {
                            if (backendPlayerStateBySteamId.TryGetValue(steamId, out var previousState)
                                && previousState != null
                                && backendRecentDiscordLinkSuccessBySteamId.TryGetValue(steamId, out var graceUntilUtc)
                                && graceUntilUtc > nowUtc
                                && (previousState.IsDiscordLinked || !string.IsNullOrWhiteSpace(previousState.DiscordId))
                                && !(state.IsDiscordLinked || !string.IsNullOrWhiteSpace(state.DiscordId)))
                            {
                                state.IsDiscordLinked = true;
                                if (string.IsNullOrWhiteSpace(state.DiscordId))
                                {
                                    state.DiscordId = previousState.DiscordId;
                                }

                                preservedRecentLinkSuccess = true;
                            }

                            backendPlayerStateBySteamId[steamId] = state;
                            backendPendingOnboardingClientBySteamId.TryGetValue(steamId, out pendingOnboardingClientId);

                            if (state.IsDiscordLinked || !string.IsNullOrWhiteSpace(state.DiscordId))
                            {
                                backendRecentDiscordLinkSuccessBySteamId.Remove(steamId);
                            }
                            else if (backendRecentDiscordLinkSuccessBySteamId.TryGetValue(steamId, out var graceExpiryUtc)
                                && graceExpiryUtc <= nowUtc)
                            {
                                backendRecentDiscordLinkSuccessBySteamId.Remove(steamId);
                            }
                        }

                        if (preservedRecentLinkSuccess)
                        {
                            Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Preserved linked=true after /link success because player-state refresh had not caught up yet. steamId={steamId} reason={reason}");
                        }

                        Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Player state refreshed. steamId={steamId} linked={state.IsDiscordLinked || !string.IsNullOrWhiteSpace(state.DiscordId)} discordId={state.DiscordId ?? "null"} muted={state.IsMuted} banned={state.IsBanned} tierKey={state.TierKey ?? "none"} tierName={state.TierName ?? "none"} tierTag={state.TierTag ?? "none"} tierColor={state.TierColorHex ?? "none"} tag={state.TagText ?? "none"} title={state.TitleText ?? "none"} reason={reason}");
                        EnforceConnectedBackendModeration(steamId, state, reason);
                        PublishScoreboardBadgeState();
                        var publishedOnboarding = false;
                        if (pendingOnboardingClientId != 0)
                        {
                            Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Queued Discord onboarding publish for main-thread flush. steamId={steamId} clientId={pendingOnboardingClientId} linked={state.IsDiscordLinked || !string.IsNullOrWhiteSpace(state.DiscordId)} reason={reason}");
                        }
                        else
                        {
                            publishedOnboarding = PublishDiscordOnboardingState(steamId, state, 0UL);
                        }

                        if (publishedOnboarding && pendingOnboardingClientId != 0)
                        {
                            lock (backendStateLock)
                            {
                                if (backendPendingOnboardingClientBySteamId.TryGetValue(steamId, out var mappedClientId)
                                    && mappedClientId == pendingOnboardingClientId)
                                {
                                    backendPendingOnboardingClientBySteamId.Remove(steamId);
                                }
                            }
                        }
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Player state fetch timed out for {steamId}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Player state refresh failed for {steamId}: {ex.Message}");
            }
            finally
            {
                lock (backendStateLock)
                {
                    backendPlayerFetchesInFlight.Remove(steamId);
                }
            }
        }

        private static async Task CompleteDiscordLinkAsync(BackendConfig config, ulong clientId, string steamId, string code, string gameDisplayName, string gamePlayerNumber)
        {
            try
            {
                var requestUrl = BuildConfiguredUrl(config.BaseUrl, config.LinkCompletePath, null);
                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] /link prepared request. clientId={clientId} steamId={steamId} gameDisplayName={gameDisplayName ?? "null"} gamePlayerNumber={gamePlayerNumber ?? "null"} effectiveBaseUrl={config?.BaseUrl ?? "null"} effectiveLinkCompletePath={config?.LinkCompletePath ?? "null"} finalUrl={requestUrl ?? "null"} authConfigured={!string.IsNullOrWhiteSpace(config?.ApiKey)} timeoutMs={config?.TimeoutMs ?? 0}");
                if (string.IsNullOrWhiteSpace(requestUrl))
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] /link aborted because request URL could not be built. baseUrl={config?.BaseUrl ?? "null"} linkCompletePath={config?.LinkCompletePath ?? "null"}");
                    EnqueueBackendNotification(clientId, "<size=14><color=#ff6666>Linking service is currently unavailable. Please try again later.</color></size>");
                    return;
                }

                var payload = new BackendLinkCompleteRequest
                {
                    SteamId = steamId,
                    Code = code,
                    GameDisplayName = gameDisplayName,
                    GamePlayerNumber = gamePlayerNumber
                };

                var json = JsonConvert.SerializeObject(payload, Formatting.None);
                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] /link payload audit. steamId={steamId} code={code} gameDisplayName={gameDisplayName ?? "null"} gamePlayerNumber={gamePlayerNumber ?? "null"} payload={json}");
                using (var request = new HttpRequestMessage(HttpMethod.Post, requestUrl))
                {
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    if (!string.IsNullOrWhiteSpace(config.ApiKey))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey.Trim());
                    }

                    Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] /link request dispatch. clientId={clientId} steamId={steamId} finalUrl={requestUrl} authHeaderPresent={(request.Headers.Authorization != null)}");

                    using (var timeoutCts = new CancellationTokenSource(Math.Max(1000, config.TimeoutMs)))
                    using (var response = await backendHttpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false))
                    {
                        Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] /link response received. clientId={clientId} steamId={steamId} status={(int)response.StatusCode} reason={response.ReasonPhrase}");
                        string responseText;
                        try
                        {
                            responseText = await AwaitWithTimeoutAsync(response.Content.ReadAsStringAsync(), Math.Max(1000, config.TimeoutMs), "Backend link response body timed out").ConfigureAwait(false);
                        }
                        catch (TimeoutException ex)
                        {
                            Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Discord link body read timed out for {steamId}: {ex.Message}");
                            EnqueueBackendNotification(clientId, "<size=14><color=#ff6666>Linking service is currently unavailable. Please try again later.</color></size>");
                            return;
                        }

                        if (response.IsSuccessStatusCode)
                        {
                            var success = ParseBackendLinkCompleteResponse(responseText);
                            var isDefinitiveSuccess = IsSuccessfulLinkCompleteResponse(response.StatusCode, success, responseText);
                            Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] /link response audit. steamId={steamId} status={(int)response.StatusCode} definitiveSuccess={isDefinitiveSuccess} ok={(success != null ? success.Ok.ToString() : "null")} success={(success?.Success.HasValue == true ? success.Success.Value.ToString() : "null")} linked={(success?.Linked.HasValue == true ? success.Linked.Value.ToString() : "null")} verified={(success?.Verified.HasValue == true ? success.Verified.Value.ToString() : "null")} discordId={success?.DiscordId ?? "null"} returnedSteamId={success?.SteamId ?? "null"} error={success?.Error ?? "null"} message={success?.Message ?? "null"} code={success?.Code ?? "null"} responseBody={responseText ?? string.Empty}");
                            if (!isDefinitiveSuccess)
                            {
                                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] /link branch=ambiguous-success-response steamId={steamId} status={(int)response.StatusCode} effectiveBaseUrl={config?.BaseUrl ?? "null"} effectiveLinkCompletePath={config?.LinkCompletePath ?? "null"} finalUrl={requestUrl ?? "null"} response={responseText}");
                                EnqueueBackendNotification(clientId, "<size=14><color=#ff6666>Linking service is currently unavailable. Please try again later.</color></size>");
                                return;
                            }

                            success = success ?? new BackendLinkCompleteResponse { Ok = true, SteamId = steamId };
                            Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] /link branch=success steamId={steamId} discordId={success.DiscordId ?? "unknown"} status={(int)response.StatusCode} response={responseText}");
                            ApplySuccessfulDiscordLinkState(config, steamId, success, clientId);
                            EnqueueBackendNotification(clientId, "<size=14><color=#00ff99>Your Discord account has been linked successfully.</color></size>");
                            EnsureBackendPlayerStateQueued(steamId, "discord-link-success", forceRefresh: true);
                            return;
                        }

                        var failureMessage = BuildDiscordLinkFailureMessage(response.StatusCode, responseText, out var failureBranch);
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] /link branch={failureBranch} steamId={steamId} status={(int)response.StatusCode} reason={response.ReasonPhrase} response={responseText}");
                        EnqueueBackendNotification(clientId, failureMessage);
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] /link branch=unavailable-timeout steamId={steamId} error={ex.Message}");
                EnqueueBackendNotification(clientId, "<size=14><color=#ff6666>Linking service is currently unavailable. Please try again later.</color></size>");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] /link branch=unavailable-exception steamId={steamId} error={ex.Message}");
                EnqueueBackendNotification(clientId, "<size=14><color=#ff6666>Linking service is currently unavailable. Please try again later.</color></size>");
            }
        }

        private static void EnforceConnectedBackendModeration(string steamId, BackendPlayerState state, string reason)
        {
            if (state == null || !state.IsBanned || !TryFindConnectedPlayerBySteamId(steamId, out _, out _))
            {
                return;
            }

            Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Disconnecting banned player {steamId}. reason={state.BanReason ?? "backend ban"} source={reason}");
            TryKickPlayer(steamId, 0UL);
        }

        private static void ApplySuccessfulDiscordLinkState(BackendConfig config, string steamId, BackendLinkCompleteResponse success, ulong clientId)
        {
            if (!IsSteamIdentityKey(steamId))
            {
                return;
            }

            BackendPlayerState state;
            var nowUtc = DateTime.UtcNow;
            lock (backendStateLock)
            {
                if (!backendPlayerStateBySteamId.TryGetValue(steamId, out state) || state == null)
                {
                    state = new BackendPlayerState
                    {
                        SteamId = steamId
                    };
                    backendPlayerStateBySteamId[steamId] = state;
                }

                state.SteamId = steamId;
                state.IsDiscordLinked = true;
                if (!string.IsNullOrWhiteSpace(success?.DiscordId))
                {
                    state.DiscordId = success.DiscordId.Trim();
                }

                state.CachedAtUtc = nowUtc;
                if (state.ModerationExpiresAtUtc < nowUtc)
                {
                    state.ModerationExpiresAtUtc = nowUtc.AddSeconds(Math.Max(15, config != null ? config.ModerationCacheSeconds : DefaultBackendModerationCacheSeconds));
                }

                if (state.BadgeExpiresAtUtc < nowUtc)
                {
                    state.BadgeExpiresAtUtc = nowUtc.AddSeconds(Math.Max(30, config != null ? config.BadgeCacheSeconds : DefaultBackendBadgeCacheSeconds));
                }

                if (clientId != 0)
                {
                    backendPendingOnboardingClientBySteamId[steamId] = clientId;
                }

                backendRecentDiscordLinkSuccessBySteamId[steamId] = nowUtc.Add(BackendDiscordLinkConsistencyGrace);
            }

            Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Applied immediate Discord link success to cache and queued onboarding publish. steamId={steamId} clientId={clientId} discordId={success?.DiscordId ?? "null"}");
        }

        private static bool PublishDiscordOnboardingState(string steamId, BackendPlayerState state, ulong preferredClientId = 0)
        {
            try
            {
                if (state == null || !IsSteamIdentityKey(steamId))
                {
                    return false;
                }

                ulong clientId = 0;
                if (preferredClientId != 0)
                {
                    clientId = preferredClientId;
                }
                else
                {
                    if (!TryFindConnectedPlayerBySteamId(steamId, out var player, out _)
                        || !TryGetClientId(player, out clientId)
                        || clientId == 0)
                    {
                        return false;
                    }
                }

                RankedOverlayNetwork.PublishDiscordOnboardingStateToClient(clientId, new DiscordOnboardingStateMessage
                {
                    IsResolved = true,
                    IsLinked = state.IsDiscordLinked || !string.IsNullOrWhiteSpace(state.DiscordId)
                });
                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Published Discord onboarding state. steamId={steamId} clientId={clientId} linked={(state.IsDiscordLinked || !string.IsNullOrWhiteSpace(state.DiscordId))}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Failed to publish Discord onboarding state: {ex.Message}");
                return false;
            }
        }

        private static bool TryResolveDiscordVerificationStateForPlayer(object player, ulong fallbackClientId, out ulong clientId, out string steamId, out bool isResolved, out bool isLinked, out string resolutionReason)
        {
            clientId = fallbackClientId;
            steamId = null;
            isResolved = false;
            isLinked = false;
            resolutionReason = "player-unresolved";

            try
            {
                if (clientId == 0 && player != null)
                {
                    TryGetClientId(player, out clientId);
                }

                steamId = NormalizeResolvedPlayerKey(TryGetPlayerId(player, clientId));
                if (!IsSteamIdentityKey(steamId))
                {
                    resolutionReason = "steamid-invalid";
                    return false;
                }

                TryResolveDiscordVerificationStateCore(steamId, clientId, out isResolved, out isLinked, out resolutionReason);
                return true;
            }
            catch
            {
                resolutionReason = "exception";
                return false;
            }
        }

        private static void TryResolveDiscordVerificationStateCore(string steamId, ulong clientId, out bool isResolved, out bool isLinked, out string resolutionReason)
        {
            isResolved = false;
            isLinked = false;
            resolutionReason = "steamid-invalid";

            if (IsOnboardingBypassedServerMode(GetBackendConfig()))
            {
                isResolved = true;
                isLinked = true;
                resolutionReason = "non-competitive-mode";
                return;
            }

            var normalizedSteamId = NormalizeResolvedPlayerKey(steamId);
            if (!IsSteamIdentityKey(normalizedSteamId))
            {
                return;
            }

            bool bootstrapPending;
            bool fetchInFlight;
            lock (backendStateLock)
            {
                bootstrapPending = clientId != 0 && backendPendingBootstrapClientIds.Contains(clientId);
                fetchInFlight = backendPlayerFetchesInFlight.Contains(normalizedSteamId);
            }

            if (bootstrapPending)
            {
                resolutionReason = "bootstrap-pending";
                return;
            }

            if (!TryGetBackendPlayerState(normalizedSteamId, out var state, out var moderationFresh, out var badgeFresh) || state == null)
            {
                EnsureBackendPlayerStateQueued(normalizedSteamId, "discord-onboarding-state-miss", forceRefresh: false, onboardingClientId: clientId);
                resolutionReason = "state-miss";
                return;
            }

            if (state.IsDiscordLinked || !string.IsNullOrWhiteSpace(state.DiscordId))
            {
                isResolved = true;
                isLinked = true;
                resolutionReason = "linked";
                return;
            }

            if (!moderationFresh || !badgeFresh)
            {
                EnsureBackendPlayerStateQueued(normalizedSteamId, "discord-onboarding-state-stale", forceRefresh: false, onboardingClientId: clientId);
                resolutionReason = "state-stale";
                return;
            }

            if (fetchInFlight)
            {
                resolutionReason = "fetch-in-flight";
                return;
            }

            isResolved = true;
            isLinked = false;
            resolutionReason = "unlinked";
        }

        private static bool IsMandatoryVerificationBlockedState(object state)
        {
            if (IsOnboardingBypassedServerMode(GetBackendConfig()))
            {
                return false;
            }

            var stateName = state?.ToString() ?? string.Empty;
            return string.Equals(stateName, "TeamSelect", StringComparison.Ordinal)
                || string.Equals(stateName, "PositionSelectBlue", StringComparison.Ordinal)
                || string.Equals(stateName, "PositionSelectRed", StringComparison.Ordinal)
                || string.Equals(stateName, "Play", StringComparison.Ordinal);
        }

        internal static bool TryBlockMandatoryVerificationState(object player, object state)
        {
            try
            {
                if (!IsMandatoryVerificationBlockedState(state))
                {
                    return false;
                }

                if (!TryResolveDiscordVerificationStateForPlayer(player, 0UL, out var clientId, out var steamId, out var isResolved, out var isLinked, out var resolutionReason))
                {
                    return false;
                }

                if (isResolved && isLinked)
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [BACKEND][VERIFY] Verified player allowed through normally. clientId={clientId} steamId={steamId} targetState={state} source=state-rpc");
                    return false;
                }

                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND][VERIFY] Mandatory verification gate triggered. clientId={clientId} steamId={steamId} linked={isLinked} resolved={isResolved} reason={resolutionReason} targetState={state} source=state-rpc");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] [BACKEND][VERIFY] Failed while enforcing mandatory verification state block: {ex.Message}");
                return false;
            }
        }

        internal static bool TryRejectTeamSelectionForMandatoryVerification(object player, ulong clientId, object requestedTeam, string source)
        {
            try
            {
                if (IsOnboardingBypassedServerMode(GetBackendConfig()))
                {
                    return false;
                }

                if (!TryResolveDiscordVerificationStateForPlayer(player, clientId, out var resolvedClientId, out var steamId, out var isResolved, out var isLinked, out var resolutionReason))
                {
                    return false;
                }

                if (isResolved && isLinked)
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [BACKEND][VERIFY] Verified player allowed through normally. clientId={resolvedClientId} steamId={steamId} requestedTeam={requestedTeam} source={source}");
                    return false;
                }

                if (resolvedClientId != 0)
                {
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.DiscordModule, $"Discord verification is required to join a team on this server. Use {ChatStyle.Command("/discord")} and then {ChatStyle.Command("/link CODE")} to continue.", ChatTone.Error), resolvedClientId);
                }

                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND][VERIFY] Mandatory verification gate triggered. clientId={resolvedClientId} steamId={steamId} linked={isLinked} resolved={isResolved} reason={resolutionReason} requestedTeam={requestedTeam} source={source}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] [BACKEND][VERIFY] Failed while rejecting team selection for mandatory verification: {ex.Message}");
                return false;
            }
        }

        [HarmonyPatch(typeof(Player), "Client_SetPlayerStateRpc")]
        private static class PlayerClientSetPlayerStateRpcVerificationPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(object __instance, object state)
            {
                return !TryBlockMandatoryVerificationState(__instance, state);
            }
        }

        private static bool TryGetBackendPlayerState(string steamId, out BackendPlayerState state, out bool moderationFresh, out bool badgeFresh)
        {
            state = null;
            moderationFresh = false;
            badgeFresh = false;

            var normalizedSteamId = NormalizeResolvedPlayerKey(steamId);
            if (!IsSteamIdentityKey(normalizedSteamId))
            {
                return false;
            }

            lock (backendStateLock)
            {
                if (!backendPlayerStateBySteamId.TryGetValue(normalizedSteamId, out state) || state == null)
                {
                    return false;
                }

                var nowUtc = DateTime.UtcNow;
                moderationFresh = state.ModerationExpiresAtUtc > nowUtc;
                badgeFresh = state.BadgeExpiresAtUtc > nowUtc;
                return true;
            }
        }

        private static BackendConfig GetBackendConfig()
        {
            lock (backendStateLock)
            {
                if (backendConfigLoaded)
                {
                    return backendConfig;
                }

                backendConfigLoaded = true;
                backendConfig = LoadBackendConfig();
                return backendConfig;
            }
        }

        private static BackendConfig LoadBackendConfig()
        {
            var config = new BackendConfig();
            var rawBackendFileServerMode = (string)null;
            var backendFileDeclaredServerMode = false;
            var rawBackendEnvServerMode = GetEnvironmentOverride(BackendServerModeEnvVar, null);
            try
            {
                var configPath = GetBackendConfigPath();
                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Loading backend config from {configPath}");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    try
                    {
                        var rawFileRoot = JObject.Parse(json);
                        if (TryGetDynamicMemberValue(rawFileRoot, "serverMode", out var rawBackendServerModeValue))
                        {
                            backendFileDeclaredServerMode = true;
                            rawBackendFileServerMode = ExtractDynamicMemberValueToString(rawBackendServerModeValue);
                        }
                    }
                    catch (Exception rawServerModeEx)
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Failed to inspect raw backend serverMode field: {rawServerModeEx.Message}");
                    }

                    var fileConfig = JsonConvert.DeserializeObject<BackendConfig>(json);
                    if (fileConfig != null)
                    {
                        config.Enabled = fileConfig.Enabled;
                        config.BaseUrl = fileConfig.BaseUrl;
                        config.ApiKey = fileConfig.ApiKey;
                        config.PlayerStatePath = string.IsNullOrWhiteSpace(fileConfig.PlayerStatePath) ? config.PlayerStatePath : fileConfig.PlayerStatePath;
                        config.MatchResultPath = string.IsNullOrWhiteSpace(fileConfig.MatchResultPath) ? config.MatchResultPath : fileConfig.MatchResultPath;
                        config.LinkCompletePath = string.IsNullOrWhiteSpace(fileConfig.LinkCompletePath) ? config.LinkCompletePath : fileConfig.LinkCompletePath;
                        config.MutePath = string.IsNullOrWhiteSpace(fileConfig.MutePath) ? config.MutePath : fileConfig.MutePath;
                        config.TempBanPath = string.IsNullOrWhiteSpace(fileConfig.TempBanPath) ? config.TempBanPath : fileConfig.TempBanPath;
                        config.UnmutePath = string.IsNullOrWhiteSpace(fileConfig.UnmutePath) ? config.UnmutePath : fileConfig.UnmutePath;
                        config.UnbanPath = string.IsNullOrWhiteSpace(fileConfig.UnbanPath) ? config.UnbanPath : fileConfig.UnbanPath;
                        config.TimeoutMs = fileConfig.TimeoutMs > 0 ? fileConfig.TimeoutMs : config.TimeoutMs;
                        config.ModerationCacheSeconds = fileConfig.ModerationCacheSeconds > 0 ? fileConfig.ModerationCacheSeconds : config.ModerationCacheSeconds;
                        config.BadgeCacheSeconds = fileConfig.BadgeCacheSeconds > 0 ? fileConfig.BadgeCacheSeconds : config.BadgeCacheSeconds;
                    }

                    Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Backend config file loaded. rawServerMode={(backendFileDeclaredServerMode ? DescribeServerModeValue(rawBackendFileServerMode) : "missing")} baseUrl={config.BaseUrl ?? "null"} playerStatePath={config.PlayerStatePath ?? "null"} matchResultPath={config.MatchResultPath ?? "null"} linkCompletePath={config.LinkCompletePath ?? "null"} mutePath={config.MutePath ?? "null"} tempBanPath={config.TempBanPath ?? "null"} unmutePath={config.UnmutePath ?? "null"} unbanPath={config.UnbanPath ?? "null"} authConfigured={!string.IsNullOrWhiteSpace(config.ApiKey)}");
                }
                else
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Backend config file not found at {configPath}. Defaults/env overrides will be used.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Failed to read backend config file: {ex.Message}");
            }

            config.BaseUrl = GetEnvironmentOverride(BackendBaseUrlEnvVar, config.BaseUrl);
            config.ApiKey = GetEnvironmentOverride(BackendApiKeyEnvVar, config.ApiKey);
            config.PlayerStatePath = GetEnvironmentOverride(BackendPlayerStatePathEnvVar, config.PlayerStatePath);
            config.MatchResultPath = GetEnvironmentOverride(BackendMatchResultPathEnvVar, config.MatchResultPath);
            config.LinkCompletePath = GetEnvironmentOverride(BackendLinkCompletePathEnvVar, config.LinkCompletePath);
            config.MutePath = GetEnvironmentOverride(BackendMutePathEnvVar, config.MutePath);
            config.TempBanPath = GetEnvironmentOverride(BackendTempBanPathEnvVar, config.TempBanPath);
            config.UnmutePath = GetEnvironmentOverride(BackendUnmutePathEnvVar, config.UnmutePath);
            config.UnbanPath = GetEnvironmentOverride(BackendUnbanPathEnvVar, config.UnbanPath);
            config.TimeoutMs = GetEnvironmentOverrideInt(BackendTimeoutMsEnvVar, config.TimeoutMs);
            config.ModerationCacheSeconds = GetEnvironmentOverrideInt(BackendModerationCacheSecondsEnvVar, config.ModerationCacheSeconds);
            config.BadgeCacheSeconds = GetEnvironmentOverrideInt(BackendBadgeCacheSecondsEnvVar, config.BadgeCacheSeconds);

            var runtimeResolved = TryResolveAuthoritativeServerMode(out var rawRuntimeServerMode, out var normalizedRuntimeServerMode, out var runtimeSourceWinner, out var runtimeResolutionMessage);
            config.ServerMode = runtimeResolved ? normalizedRuntimeServerMode : CompetitiveServerMode;

            var sourceWinner = runtimeResolved ? (runtimeSourceWinner ?? "serverConfiguration") : "fallback-competitive";
            var contradictionParts = new List<string>();

            if (backendFileDeclaredServerMode
                && TryNormalizeServerMode(rawBackendFileServerMode, out var normalizedBackendFileServerMode)
                && !string.Equals(normalizedBackendFileServerMode, config.ServerMode, StringComparison.OrdinalIgnoreCase))
            {
                contradictionParts.Add($"backendConfig={normalizedBackendFileServerMode}");
            }

            if (TryNormalizeServerMode(rawBackendEnvServerMode, out var normalizedBackendEnvServerMode)
                && !string.Equals(normalizedBackendEnvServerMode, config.ServerMode, StringComparison.OrdinalIgnoreCase))
            {
                contradictionParts.Add($"env={normalizedBackendEnvServerMode}");
            }

            var contradictionText = contradictionParts.Count > 0 ? string.Join(", ", contradictionParts) : "none";
            var usedFallback = !runtimeResolved;

            Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Authoritative serverMode resolved. runtimeRaw={DescribeServerModeValue(rawRuntimeServerMode)} backendRaw={(backendFileDeclaredServerMode ? DescribeServerModeValue(rawBackendFileServerMode) : "missing")} envRaw={DescribeServerModeValue(rawBackendEnvServerMode)} effective={config.ServerMode} source={sourceWinner} contradiction={contradictionText} fallback={usedFallback} details={runtimeResolutionMessage ?? "none"}");

            if (usedFallback || contradictionParts.Count > 0)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] serverMode resolution warning. runtimeRaw={DescribeServerModeValue(rawRuntimeServerMode)} backendRaw={(backendFileDeclaredServerMode ? DescribeServerModeValue(rawBackendFileServerMode) : "missing")} envRaw={DescribeServerModeValue(rawBackendEnvServerMode)} effective={config.ServerMode} source={sourceWinner} contradiction={contradictionText} fallback={usedFallback}");
            }

            Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Effective backend config. enabled={config.Enabled} serverMode={config.ServerMode} baseUrl={config.BaseUrl ?? "null"} playerStatePath={config.PlayerStatePath ?? "null"} matchResultPath={config.MatchResultPath ?? "null"} linkCompletePath={config.LinkCompletePath ?? "null"} mutePath={config.MutePath ?? "null"} tempBanPath={config.TempBanPath ?? "null"} unmutePath={config.UnmutePath ?? "null"} unbanPath={config.UnbanPath ?? "null"} authConfigured={!string.IsNullOrWhiteSpace(config.ApiKey)} timeoutMs={config.TimeoutMs}");
            return config;
        }

        private static bool IsBackendConfigured(BackendConfig config)
        {
            return config != null && config.Enabled && IsCompetitiveServerMode(config) && !string.IsNullOrWhiteSpace(config.BaseUrl);
        }

        public static bool IsTrainingServerModeActive()
        {
            return IsTrainingServerMode(GetBackendConfig());
        }

        private static string NormalizeServerMode(string configuredMode)
        {
            if (TryNormalizeServerMode(configuredMode, out var normalizedMode))
            {
                return normalizedMode;
            }

            if (!string.IsNullOrWhiteSpace(configuredMode))
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Unknown serverMode '{configuredMode}'. Falling back to {CompetitiveServerMode}.");
            }

            return CompetitiveServerMode;
        }

        private static bool TryNormalizeServerMode(string configuredMode, out string normalizedMode)
        {
            normalizedMode = null;
            if (string.IsNullOrWhiteSpace(configuredMode))
            {
                return false;
            }

            var trimmedMode = configuredMode.Trim();
            if (string.Equals(trimmedMode, PublicServerMode, StringComparison.OrdinalIgnoreCase))
            {
                normalizedMode = PublicServerMode;
                return true;
            }

            if (string.Equals(trimmedMode, CompetitiveServerMode, StringComparison.OrdinalIgnoreCase))
            {
                normalizedMode = CompetitiveServerMode;
                return true;
            }

            if (string.Equals(trimmedMode, TrainingServerMode, StringComparison.OrdinalIgnoreCase))
            {
                normalizedMode = TrainingServerMode;
                return true;
            }

            return false;
        }

        private static bool IsPublicServerMode(BackendConfig config)
        {
            return string.Equals(config?.ServerMode, PublicServerMode, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCompetitiveServerMode(BackendConfig config)
        {
            return string.Equals(config?.ServerMode, CompetitiveServerMode, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTrainingServerMode(BackendConfig config)
        {
            return string.Equals(config?.ServerMode, TrainingServerMode, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOnboardingBypassedServerMode(BackendConfig config)
        {
            return IsPublicServerMode(config) || IsTrainingServerMode(config);
        }

        private static string BuildConfiguredUrl(string baseUrl, string configuredPath, string steamId)
        {
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(configuredPath))
            {
                return null;
            }

            var normalizedBaseUrl = baseUrl.Trim().TrimEnd('/');
            var normalizedPath = configuredPath.Trim();
            normalizedPath = normalizedPath.Replace("{steamId}", Uri.EscapeDataString(steamId ?? string.Empty));
            if (normalizedPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedPath;
            }

            if (!normalizedPath.StartsWith("/", StringComparison.Ordinal))
            {
                normalizedPath = "/" + normalizedPath;
            }

            return normalizedBaseUrl + normalizedPath;
        }

        private static BackendPlayerState ParseBackendPlayerState(string steamId, string responseText, BackendConfig config)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return null;
            }

            try
            {
                var root = JObject.Parse(responseText);
                var payload = SelectPrimaryPayload(root);
                var moderationNode = payload["moderation"] as JObject;
                var badgeNode = payload["badge"] as JObject;
                var rankedNode = payload["ranked"] as JObject;
                var tierNode = rankedNode?["tier"] as JObject
                    ?? payload["tier"] as JObject
                    ?? payload["rankTier"] as JObject;
                var linkingNode = payload["linking"] as JObject;
                var discordNode = payload["discord"] as JObject;
                var effectiveLinkingNode = linkingNode ?? discordNode;
                var nowUtc = DateTime.UtcNow;
                var discordId = FirstNonEmptyString(effectiveLinkingNode, "discordId", "id")
                    ?? FirstNonEmptyString(payload, "discordId");
                var isDiscordLinked = FirstBoolean(effectiveLinkingNode, "linked", "isLinked", "verified", "isVerified")
                    ?? FirstBoolean(payload, "discordLinked", "isDiscordLinked", "linked", "isLinked", "verified", "isVerified")
                    ?? !string.IsNullOrWhiteSpace(discordId);
                var tierKey = FirstNonEmptyString(tierNode, "tierKey", "key", "id")
                    ?? FirstNonEmptyString(payload, "tierKey");
                var tierName = NormalizeTierVisualText(
                    FirstNonEmptyString(tierNode, "tierName", "name", "displayName", "label")
                        ?? FirstNonEmptyString(payload, "tierName"),
                    "tier-name",
                    steamId);
                var tierTag = NormalizeTierVisualText(
                    FirstNonEmptyString(tierNode, "tierTag", "tag", "shortName")
                        ?? FirstNonEmptyString(payload, "tierTag"),
                    "tier-tag",
                    steamId);
                var tierColorHex = NormalizeColorHex(
                    FirstNonEmptyString(tierNode, "tierColorHex", "colorHex", "color")
                        ?? FirstNonEmptyString(payload, "tierColorHex"),
                    DefaultBadgeColorHex);

                return new BackendPlayerState
                {
                    SteamId = FirstNonEmptyString(payload, "steamId", "playerId", "id") ?? steamId,
                    IsMuted = FirstBoolean(moderationNode, "muted", "isMuted") ?? FirstBoolean(payload, "muted", "isMuted") ?? false,
                    IsBanned = FirstBoolean(moderationNode, "banned", "isBanned") ?? FirstBoolean(payload, "banned", "isBanned") ?? false,
                    IsDiscordLinked = isDiscordLinked,
                    DiscordId = discordId,
                    TierKey = tierKey,
                    TierName = tierName,
                    TierTag = tierTag,
                    TierColorHex = tierColorHex,
                    MuteReason = FirstNonEmptyString(moderationNode, "muteReason", "reason") ?? FirstNonEmptyString(payload, "muteReason"),
                    BanReason = FirstNonEmptyString(moderationNode, "banReason", "reason") ?? FirstNonEmptyString(payload, "banReason"),
                    TagText = NormalizeVisibleBadgeText(FirstNonEmptyString(badgeNode, "tag", "tagText", "badge", "label") ?? FirstNonEmptyString(payload, "tag", "tagText", "badge", "label"), "badge-tag", steamId),
                    TagColorHex = NormalizeColorHex(FirstNonEmptyString(badgeNode, "tagColorHex", "tagColor", "color") ?? FirstNonEmptyString(payload, "tagColorHex", "tagColor", "badgeColor", "color"), DefaultBadgeColorHex),
                    TitleText = NormalizeVisibleBadgeText(FirstNonEmptyString(badgeNode, "title", "rankedTitle", "subtitle") ?? FirstNonEmptyString(payload, "title", "rankedTitle", "subtitle"), "badge-title", steamId),
                    TitleColorHex = NormalizeColorHex(FirstNonEmptyString(badgeNode, "titleColorHex", "titleColor") ?? FirstNonEmptyString(payload, "titleColorHex", "titleColor"), DefaultTitleColorHex),
                    CachedAtUtc = nowUtc,
                    ModerationExpiresAtUtc = nowUtc.AddSeconds(Math.Max(15, config != null ? config.ModerationCacheSeconds : DefaultBackendModerationCacheSeconds)),
                    BadgeExpiresAtUtc = nowUtc.AddSeconds(Math.Max(30, config != null ? config.BadgeCacheSeconds : DefaultBackendBadgeCacheSeconds))
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Failed to parse player state payload for {steamId}: {ex.Message}");
                return null;
            }
        }

        private static JObject SelectPrimaryPayload(JObject root)
        {
            if (root == null)
            {
                return new JObject();
            }

            if (root["data"] is JObject dataNode)
            {
                return dataNode;
            }

            if (root["player"] is JObject playerNode)
            {
                return playerNode;
            }

            return root;
        }

        private static string BuildCompactScoreboardBadgeText(BackendPlayerState state)
        {
            if (state == null)
            {
                return null;
            }

            var tierText = ResolveTierDisplayText(state, "scoreboard-tier", state.SteamId);
            var tagText = NormalizeVisibleBadgeText(state.TagText, "scoreboard-tag", state.SteamId);
            var titleText = NormalizeVisibleBadgeText(state.TitleText, "scoreboard-title", state.SteamId);

            if (!string.IsNullOrWhiteSpace(tierText))
            {
                if (!string.IsNullOrWhiteSpace(tagText)
                    && !string.Equals(tagText, tierText, StringComparison.OrdinalIgnoreCase))
                {
                    return tierText + " / " + tagText;
                }

                return tierText;
            }

            if (!string.IsNullOrWhiteSpace(tagText))
            {
                return tagText;
            }

            return string.IsNullOrWhiteSpace(titleText) ? null : titleText;
        }

        private static string ResolveCompactBadgeColor(BackendPlayerState state)
        {
            if (state == null)
            {
                return DefaultBadgeColorHex;
            }

            if (!string.IsNullOrWhiteSpace(ResolveTierDisplayText(state, "scoreboard-tier-color", state.SteamId)))
            {
                return NormalizeColorHex(state.TierColorHex, DefaultBadgeColorHex);
            }

            return !string.IsNullOrWhiteSpace(NormalizeVisibleBadgeText(state.TagText, "scoreboard-tag-color", state.SteamId))
                ? NormalizeColorHex(state.TagColorHex, DefaultBadgeColorHex)
                : NormalizeColorHex(state.TitleColorHex, DefaultTitleColorHex);
        }

        private static BackendMatchResultReport BuildBackendMatchResultReport(MatchResultMessage matchResult)
        {
            try
            {
                ResolveAuthoritativeFinalScore(out var redScore, out var blueScore);
                TryGetCurrentServerName(out var serverName);
                return new BackendMatchResultReport
                {
                    ServerName = serverName ?? string.Empty,
                    CompletedAtUtc = DateTime.UtcNow.ToString("o"),
                    WinningTeam = matchResult.WinningTeam.ToString(),
                    RedScore = redScore,
                    BlueScore = blueScore,
                    Players = (matchResult.Players ?? Array.Empty<MatchResultPlayerMessage>())
                        .Where(player => player != null)
                        .Select(player => new BackendMatchResultPlayerReport
                        {
                            Id = player.Id,
                            SteamId = player.SteamId,
                            Username = player.Username,
                            Team = player.Team.ToString(),
                            Goals = player.Goals,
                            Assists = player.Assists,
                            Saves = player.Saves,
                            Shots = player.Shots,
                            MmrBefore = player.MmrBefore,
                            MmrAfter = player.MmrAfter,
                            MmrDelta = player.MmrDelta,
                            IsMvp = player.IsMVP,
                            ExcludedFromMmr = player.ExcludedFromMmr,
                            IsSharedGoalie = player.IsSharedGoalie
                        })
                        .ToArray()
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Failed to build match result report: {ex.Message}");
                return null;
            }
        }

        private static void LogBackendMatchResultAudit(BackendMatchResultReport report)
        {
            if (report == null)
            {
                return;
            }

            try
            {
                var players = report.Players ?? Array.Empty<BackendMatchResultPlayerReport>();
                var mvpSummary = string.Join(", ", players
                    .Where(player => player != null && player.IsMvp)
                    .Select(player => $"{player.Team}:{(string.IsNullOrWhiteSpace(player.Username) ? (player.SteamId ?? player.Id ?? "unknown") : player.Username)}"));

                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND-AUDIT] Match result payload ready. serverName={report.ServerName ?? string.Empty} winningTeam={report.WinningTeam ?? string.Empty} redScore={report.RedScore} blueScore={report.BlueScore} players={players.Length} mvps={(string.IsNullOrWhiteSpace(mvpSummary) ? "none" : mvpSummary)}");
                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND-AUDIT] Match result payload JSON={JsonConvert.SerializeObject(report, Formatting.None)}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND-AUDIT] Failed to log match result payload audit: {ex.Message}");
            }
        }

        private static async Task SendMatchResultToBackendAsync(BackendConfig config, BackendMatchResultReport report)
        {
            var requestUrl = string.Empty;

            try
            {
                requestUrl = BuildConfiguredUrl(config.BaseUrl, config.MatchResultPath, null);
                if (string.IsNullOrWhiteSpace(requestUrl))
                {
                    return;
                }

                var json = JsonConvert.SerializeObject(report, Formatting.None);
                Debug.Log($"[{Constants.MOD_NAME}] [BACKEND-AUDIT] Posting match result to backend. url={requestUrl} timeoutMs={Math.Max(1000, config.TimeoutMs)} players={report?.Players?.Length ?? 0}");
                using (var request = new HttpRequestMessage(HttpMethod.Post, requestUrl))
                {
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    if (!string.IsNullOrWhiteSpace(config.ApiKey))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey.Trim());
                    }

                    using (var timeoutCts = new CancellationTokenSource(Math.Max(1000, config.TimeoutMs)))
                    using (var response = await backendHttpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false))
                    {
                        var responseText = string.Empty;
                        try
                        {
                            responseText = await AwaitWithTimeoutAsync(response.Content.ReadAsStringAsync(), Math.Max(1000, config.TimeoutMs), "Backend match result response body timed out").ConfigureAwait(false);
                        }
                        catch (TimeoutException ex)
                        {
                            Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Match result response body timed out: {ex.Message}");
                            return;
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Match result report failed: {(int)response.StatusCode} {response.ReasonPhrase}. url={requestUrl} response={responseText}");
                            return;
                        }

                        Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Match result sent successfully to backend. url={requestUrl} status={(int)response.StatusCode} response={responseText}");
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Match result report timed out: {ex.Message} url={requestUrl}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Match result report failed: {ex.Message} url={requestUrl}");
            }
        }

        private static string GetBackendConfigPath()
        {
            var candidateDirectories = new List<string>();

            try
            {
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrWhiteSpace(baseDirectory))
                {
                    candidateDirectories.Add(Path.Combine(baseDirectory, "UserData"));
                }
            }
            catch
            {
            }

            try
            {
                var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrWhiteSpace(assemblyDirectory))
                {
                    candidateDirectories.Add(Path.Combine(assemblyDirectory, "UserData"));
                }
            }
            catch
            {
            }

            foreach (var candidateDirectory in candidateDirectories
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var candidatePath = Path.Combine(candidateDirectory, BackendConfigFileName);
                if (File.Exists(candidatePath))
                {
                    Directory.CreateDirectory(candidateDirectory);
                    return candidatePath;
                }
            }

            var preferredDirectory = candidateDirectories
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? Path.Combine(".", "UserData");
            Directory.CreateDirectory(preferredDirectory);
            return Path.Combine(preferredDirectory, BackendConfigFileName);
        }

        private static string GetEnvironmentOverride(string variableName, string fallback)
        {
            try
            {
                var value = Environment.GetEnvironmentVariable(variableName);
                return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            }
            catch
            {
                return fallback;
            }
        }

        private static int GetEnvironmentOverrideInt(string variableName, int fallback)
        {
            try
            {
                var value = Environment.GetEnvironmentVariable(variableName);
                return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static bool TryResolveAuthoritativeServerMode(out string rawServerMode, out string normalizedServerMode, out string sourceWinner, out string message)
        {
            rawServerMode = null;
            normalizedServerMode = CompetitiveServerMode;
            sourceWinner = null;

            if (TryResolveServerModeFromActiveServerConfigurationJson(out rawServerMode, out normalizedServerMode, out sourceWinner, out message, out var explicitServerModeMissingFromJson))
            {
                return true;
            }

            if (!explicitServerModeMissingFromJson)
            {
                return false;
            }

            if (!TryGetDedicatedServerConfigurationForBackend(out var configuration, out message))
            {
                message = $"Explicit serverMode missing in active server config JSON and {message}";
                return false;
            }

            if (TryGetDynamicMemberValue(configuration, "serverMode", out var runtimeServerModeValue))
            {
                rawServerMode = ExtractDynamicMemberValueToString(runtimeServerModeValue);
                if (!TryNormalizeServerMode(rawServerMode, out normalizedServerMode))
                {
                    message = $"serverMode field on live ServerConfiguration is invalid ({DescribeDynamicValue(runtimeServerModeValue)}).";
                    normalizedServerMode = CompetitiveServerMode;
                    return false;
                }

                sourceWinner = "serverConfiguration.serverMode";
                message = "Explicit serverMode was missing in active server config JSON. Resolved serverMode from live ServerConfiguration.serverMode.";
                return true;
            }

            if (!TryGetDynamicMemberValue(configuration, "isPublic", out var legacyIsPublicValue))
            {
                message = "Explicit serverMode missing in active server config JSON, live ServerConfiguration.serverMode unavailable, and legacy isPublic is unavailable.";
                return false;
            }

            rawServerMode = ExtractDynamicMemberValueToString(legacyIsPublicValue);
            if (!TryConvertDynamicMemberToBoolean(legacyIsPublicValue, out var legacyIsPublic))
            {
                message = $"Explicit serverMode missing in active server config JSON, live ServerConfiguration.serverMode unavailable, and legacy isPublic could not be parsed ({DescribeDynamicValue(legacyIsPublicValue)}).";
                return false;
            }

            normalizedServerMode = legacyIsPublic ? PublicServerMode : CompetitiveServerMode;
            sourceWinner = "serverConfiguration.isPublic-legacy";
            message = $"Explicit serverMode missing in active server config JSON and live ServerConfiguration.serverMode unavailable. Resolved serverMode from legacy live ServerConfiguration.isPublic={legacyIsPublic}.";
            return true;
        }

        private static bool TryResolveServerModeFromActiveServerConfigurationJson(out string rawServerMode, out string normalizedServerMode, out string sourceWinner, out string message, out bool explicitServerModeMissing)
        {
            rawServerMode = null;
            normalizedServerMode = CompetitiveServerMode;
            sourceWinner = null;
            explicitServerModeMissing = false;

            if (!TryReadActiveServerConfigurationJson(out var json, out var jsonSourceKind, out var jsonSourcePath, out message))
            {
                return false;
            }

            JObject jsonRoot;
            try
            {
                jsonRoot = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                message = $"Active server config JSON could not be parsed from {DescribeActiveServerConfigurationSource(jsonSourceKind, jsonSourcePath)}: {ex.Message}";
                return false;
            }

            if (!TryGetDynamicMemberValue(jsonRoot, "serverMode", out var explicitServerModeValue))
            {
                explicitServerModeMissing = true;
                message = $"Explicit serverMode is missing from active server config JSON ({DescribeActiveServerConfigurationSource(jsonSourceKind, jsonSourcePath)}).";
                return false;
            }

            rawServerMode = ExtractDynamicMemberValueToString(explicitServerModeValue);
            if (!TryNormalizeServerMode(rawServerMode, out normalizedServerMode))
            {
                message = $"Explicit serverMode in active server config JSON is invalid ({DescribeDynamicValue(explicitServerModeValue)}) from {DescribeActiveServerConfigurationSource(jsonSourceKind, jsonSourcePath)}.";
                normalizedServerMode = CompetitiveServerMode;
                return false;
            }

            sourceWinner = string.Equals(jsonSourceKind, "env", StringComparison.OrdinalIgnoreCase)
                ? "serverConfigJson.env.serverMode"
                : "serverConfigJson.file.serverMode";
            message = $"Resolved serverMode from active server config JSON ({DescribeActiveServerConfigurationSource(jsonSourceKind, jsonSourcePath)}).";
            return true;
        }

        private static bool TryReadActiveServerConfigurationJson(out string json, out string sourceKind, out string sourcePath, out string message)
        {
            json = null;
            sourceKind = null;
            sourcePath = null;

            try
            {
                var environmentJson = Environment.GetEnvironmentVariable(PuckServerConfigurationEnvVar);
                if (!string.IsNullOrWhiteSpace(environmentJson))
                {
                    json = environmentJson;
                    sourceKind = "env";
                    sourcePath = PuckServerConfigurationEnvVar;
                    message = $"Read active server configuration JSON from environment variable {PuckServerConfigurationEnvVar}.";
                    return true;
                }
            }
            catch (Exception ex)
            {
                message = $"Failed to read environment variable {PuckServerConfigurationEnvVar}: {ex.Message}";
                return false;
            }

            if (!TryResolveActiveServerConfigurationPath(out sourcePath, out message))
            {
                return false;
            }

            try
            {
                if (!File.Exists(sourcePath))
                {
                    message = $"Active server configuration file was not found at {sourcePath}.";
                    return false;
                }

                json = File.ReadAllText(sourcePath);
                sourceKind = "file";
                message = $"Read active server configuration JSON from file {sourcePath}.";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Failed to read active server configuration file {sourcePath}: {ex.Message}";
                return false;
            }
        }

        private static bool TryResolveActiveServerConfigurationPath(out string resolvedPath, out string message)
        {
            resolvedPath = null;

            try
            {
                var configuredPath = DefaultServerConfigurationRelativePath;
                var commandLineArgs = Environment.GetCommandLineArgs() ?? Array.Empty<string>();
                for (var i = 0; i < commandLineArgs.Length; i++)
                {
                    if (!string.Equals(commandLineArgs[i], PuckServerConfigurationCommandLineArg, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (i + 1 >= commandLineArgs.Length || string.IsNullOrWhiteSpace(commandLineArgs[i + 1]))
                    {
                        message = $"Command line argument {PuckServerConfigurationCommandLineArg} was provided without a path value.";
                        return false;
                    }

                    configuredPath = commandLineArgs[i + 1];
                    break;
                }

                resolvedPath = Uri.UnescapeDataString(new Uri(Path.GetFullPath(configuredPath)).AbsolutePath);
                message = $"Resolved active server configuration path from {PuckServerConfigurationCommandLineArg}: {resolvedPath}";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Failed to resolve active server configuration path: {ex.Message}";
                return false;
            }
        }

        private static string DescribeActiveServerConfigurationSource(string sourceKind, string sourcePath)
        {
            if (string.Equals(sourceKind, "env", StringComparison.OrdinalIgnoreCase))
            {
                return $"env:{sourcePath ?? PuckServerConfigurationEnvVar}";
            }

            if (!string.IsNullOrWhiteSpace(sourcePath))
            {
                return sourcePath;
            }

            return sourceKind ?? "unknown-source";
        }

        private static bool TryGetDedicatedServerConfigurationForBackend(out object configuration, out string message)
        {
            configuration = null;

            var serverManagerType = ReflectionUtils.FindTypeByName("ServerManager", "Puck.ServerManager");
            if (serverManagerType == null)
            {
                message = "ServerManager type could not be resolved.";
                return false;
            }

            var serverManager = ReflectionUtils.GetManagerInstance(serverManagerType);
            if (serverManager == null)
            {
                message = "ServerManager instance is unavailable.";
                return false;
            }

            if (!TryGetDynamicMemberValue(serverManager, "ServerConfigurationManager", out var configurationManager) || configurationManager == null)
            {
                message = "ServerConfigurationManager is unavailable on ServerManager.";
                return false;
            }

            if (!TryGetDynamicMemberValue(configurationManager, "ServerConfiguration", out configuration) || configuration == null)
            {
                message = "ServerConfiguration is unavailable on ServerConfigurationManager.";
                return false;
            }

            message = "Resolved ServerConfiguration from ServerConfigurationManager.ServerConfiguration.";
            return true;
        }

        private static bool TryGetDynamicMemberValue(object instance, string memberName, out object value)
        {
            value = null;
            if (instance == null || string.IsNullOrWhiteSpace(memberName))
            {
                return false;
            }

            if (instance is JObject jObject)
            {
                var property = jObject.Properties().FirstOrDefault(candidate => string.Equals(candidate.Name, memberName, StringComparison.OrdinalIgnoreCase));
                if (property == null)
                {
                    return false;
                }

                value = property.Value;
                return true;
            }

            if (instance is JToken token && token.Type == JTokenType.Object)
            {
                return TryGetDynamicMemberValue((JObject)token, memberName, out value);
            }

            var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = instance.GetType();
            var propertyInfo = type.GetProperty(memberName, bindingFlags)
                ?? type.GetProperties(bindingFlags).FirstOrDefault(candidate => string.Equals(candidate.Name, memberName, StringComparison.OrdinalIgnoreCase));
            if (propertyInfo != null)
            {
                value = propertyInfo.GetValue(instance);
                return true;
            }

            var fieldInfo = type.GetField(memberName, bindingFlags)
                ?? type.GetFields(bindingFlags).FirstOrDefault(candidate => string.Equals(candidate.Name, memberName, StringComparison.OrdinalIgnoreCase));
            if (fieldInfo != null)
            {
                value = fieldInfo.GetValue(instance);
                return true;
            }

            return false;
        }

        private static string ExtractDynamicMemberValueToString(object value)
        {
            var normalizedValue = UnwrapDynamicToken(value);
            if (normalizedValue == null)
            {
                return null;
            }

            var stringValue = normalizedValue.ToString();
            return string.IsNullOrWhiteSpace(stringValue) ? null : stringValue.Trim();
        }

        private static bool TryConvertDynamicMemberToBoolean(object value, out bool parsed)
        {
            parsed = false;
            var normalizedValue = UnwrapDynamicToken(value);
            if (normalizedValue == null)
            {
                return false;
            }

            switch (normalizedValue)
            {
                case bool booleanValue:
                    parsed = booleanValue;
                    return true;
                case int intValue:
                    parsed = intValue != 0;
                    return true;
                case long longValue:
                    parsed = longValue != 0;
                    return true;
                case uint uintValue:
                    parsed = uintValue != 0;
                    return true;
                case ulong ulongValue:
                    parsed = ulongValue != 0;
                    return true;
                case string stringValue:
                    if (bool.TryParse(stringValue, out var parsedBoolean))
                    {
                        parsed = parsedBoolean;
                        return true;
                    }

                    if (int.TryParse(stringValue, out var parsedInt))
                    {
                        parsed = parsedInt != 0;
                        return true;
                    }
                    break;
            }

            return false;
        }

        private static string DescribeDynamicValue(object value)
        {
            var normalizedValue = UnwrapDynamicToken(value);
            return normalizedValue == null ? "null" : normalizedValue.ToString();
        }

        private static string DescribeServerModeValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "null" : value.Trim();
        }

        private static object UnwrapDynamicToken(object value)
        {
            if (value is JValue jValue)
            {
                return jValue.Value;
            }

            return value;
        }

        private static string FirstNonEmptyString(JObject node, params string[] propertyNames)
        {
            if (node == null || propertyNames == null)
            {
                return null;
            }

            foreach (var propertyName in propertyNames)
            {
                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    continue;
                }

                var token = node[propertyName];
                if (token == null || token.Type == JTokenType.Null)
                {
                    continue;
                }

                string value;
                switch (token.Type)
                {
                    case JTokenType.String:
                        value = token.Value<string>();
                        break;
                    case JTokenType.Integer:
                    case JTokenType.Float:
                    case JTokenType.Boolean:
                        value = token.ToString(Formatting.None);
                        break;
                    default:
                        continue;
                }

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        private static string NormalizeVisibleBadgeText(string value, string source, string steamId)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return null;
            }

            if (trimmed == "{}" || trimmed == "[]")
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Ignoring empty badge placeholder. steamId={steamId ?? "unknown"} source={source} value={trimmed}");
                return null;
            }

            if (trimmed.StartsWith("{", StringComparison.Ordinal)
                || trimmed.StartsWith("[", StringComparison.Ordinal)
                || trimmed.StartsWith("\"", StringComparison.Ordinal)
                || trimmed.Contains("\"tag\"", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("\"title\"", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("colorHex", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Ignoring serialized badge text. steamId={steamId ?? "unknown"} source={source} value={trimmed}");
                return null;
            }

            return trimmed;
        }

        private static string ResolveTierDisplayText(BackendPlayerState state, string source, string steamId)
        {
            if (state == null)
            {
                return null;
            }

            var tierTag = NormalizeTierVisualText(state.TierTag, source + "-tag", steamId);
            if (!string.IsNullOrWhiteSpace(tierTag))
            {
                return tierTag;
            }

            return NormalizeTierVisualText(state.TierName, source + "-name", steamId);
        }

        private static string NormalizeTierVisualText(string value, string source, string steamId)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return null;
            }

            if (trimmed.StartsWith("[", StringComparison.Ordinal)
                && trimmed.EndsWith("]", StringComparison.Ordinal)
                && trimmed.Length > 2)
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
            }

            return NormalizeVisibleBadgeText(trimmed, source, steamId);
        }

        private static void AppendChatBadgeSegment(StringBuilder builder, string text, string colorHex)
        {
            if (builder == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append("<color=#d7e7f3>/</color> ");
            }

            builder.Append("<b><color=");
            builder.Append(colorHex);
            builder.Append(">");
            builder.Append(EscapeRichText(text));
            builder.Append("</color></b>");
        }

        private static void AppendChatTitleSegment(StringBuilder builder, string text, string colorHex)
        {
            if (builder == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(" <color=#d7e7f3>/</color> ");
            }

            builder.Append("<color=");
            builder.Append(colorHex);
            builder.Append(">");
            builder.Append(EscapeRichText(text));
            builder.Append("</color>");
        }

        private static bool? FirstBoolean(JObject node, params string[] propertyNames)
        {
            if (node == null || propertyNames == null)
            {
                return null;
            }

            foreach (var propertyName in propertyNames)
            {
                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    continue;
                }

                var token = node[propertyName];
                if (token == null || token.Type == JTokenType.Null)
                {
                    continue;
                }

                if (token.Type == JTokenType.Boolean)
                {
                    return token.Value<bool>();
                }

                if (token.Type == JTokenType.Integer)
                {
                    return token.Value<int>() != 0;
                }

                if (token.Type == JTokenType.String)
                {
                    var rawValue = token.Value<string>();
                    if (bool.TryParse(rawValue, out var parsedBoolean))
                    {
                        return parsedBoolean;
                    }

                    if (int.TryParse(rawValue, out var parsedInt))
                    {
                        return parsedInt != 0;
                    }
                }
            }

            return null;
        }

        private static string NormalizeColorHex(string colorHex, string fallback)
        {
            var effectiveFallback = string.IsNullOrWhiteSpace(fallback) ? "#ffffff" : fallback.Trim();
            if (!effectiveFallback.StartsWith("#", StringComparison.Ordinal))
            {
                effectiveFallback = "#" + effectiveFallback;
            }

            if (string.IsNullOrWhiteSpace(colorHex))
            {
                return effectiveFallback;
            }

            var trimmed = colorHex.Trim();
            if (!trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                trimmed = "#" + trimmed;
            }

            return trimmed.Length == 7 ? trimmed : effectiveFallback;
        }

        private static string EscapeRichText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static void EnqueueBackendNotification(ulong clientId, string message)
        {
            if (clientId == 0 || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            lock (backendNotificationLock)
            {
                pendingBackendNotifications.Enqueue(new BackendClientNotification
                {
                    ClientId = clientId,
                    Message = message
                });
            }
        }

        private static BackendLinkCompleteResponse ParseBackendLinkCompleteResponse(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return null;
            }

            try
            {
                var root = JObject.Parse(responseText);
                var payload = SelectPrimaryPayload(root);
                var linkingNode = payload["linking"] as JObject;
                var discordNode = payload["discord"] as JObject;
                var effectiveLinkingNode = linkingNode ?? discordNode;
                var error = FirstNonEmptyString(root, "error", "code") ?? FirstNonEmptyString(payload, "error", "code");
                var message = FirstNonEmptyString(root, "message") ?? FirstNonEmptyString(payload, "message");
                var discordId = FirstNonEmptyString(root, "discordId")
                    ?? FirstNonEmptyString(payload, "discordId")
                    ?? FirstNonEmptyString(effectiveLinkingNode, "discordId", "id");
                var steamId = FirstNonEmptyString(root, "steamId") ?? FirstNonEmptyString(payload, "steamId");
                var explicitOk = FirstBoolean(payload, "ok", "isOk")
                    ?? FirstBoolean(root, "ok", "isOk");
                var explicitSuccess = FirstBoolean(payload, "success", "isSuccess")
                    ?? FirstBoolean(root, "success", "isSuccess");
                var linked = FirstBoolean(effectiveLinkingNode, "linked", "isLinked")
                    ?? FirstBoolean(payload, "linked", "isLinked")
                    ?? FirstBoolean(root, "linked", "isLinked");
                var verified = FirstBoolean(effectiveLinkingNode, "verified", "isVerified")
                    ?? FirstBoolean(payload, "verified", "isVerified")
                    ?? FirstBoolean(root, "verified", "isVerified");
                return new BackendLinkCompleteResponse
                {
                    Ok = explicitOk ?? false,
                    Success = explicitSuccess,
                    Linked = linked,
                    Verified = verified,
                    DiscordId = discordId,
                    SteamId = steamId,
                    Error = error,
                    Message = message,
                    Code = FirstNonEmptyString(root, "code") ?? FirstNonEmptyString(payload, "code")
                };
            }
            catch
            {
                return new BackendLinkCompleteResponse
                {
                    Ok = false,
                    Message = responseText
                };
            }
        }

        private static bool IsSuccessfulLinkCompleteResponse(System.Net.HttpStatusCode statusCode, BackendLinkCompleteResponse response, string responseText)
        {
            if ((int)statusCode < 200 || (int)statusCode > 299)
            {
                return false;
            }

            if ((int)statusCode == 204)
            {
                return true;
            }

            if (response == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(response.Error) || LooksLikeFailureMessage(response.Message))
            {
                return false;
            }

            if (response.Ok || response.Success == true)
            {
                return true;
            }

            if (response.Linked == true || response.Verified == true)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(response.DiscordId))
            {
                return true;
            }

            return false;
        }

        private static bool LooksLikeFailureMessage(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim().ToLowerInvariant();
            return normalized.Contains("error")
                || normalized.Contains("invalid")
                || normalized.Contains("expired")
                || normalized.Contains("already used")
                || normalized.Contains("failed")
                || normalized.Contains("unavailable");
        }

        private static string BuildDiscordLinkFailureMessage(System.Net.HttpStatusCode statusCode, string responseText, out string branch)
        {
            var parsed = ParseBackendLinkCompleteResponse(responseText);
            var errorToken = ((parsed?.Error ?? parsed?.Code ?? parsed?.Message) ?? string.Empty).Trim().ToLowerInvariant();

            if ((int)statusCode == 400 || (int)statusCode == 404 || errorToken.Contains("invalid") || errorToken.Contains("expired"))
            {
                branch = "invalid-or-expired";
                return "<size=14><color=#ff6666>This verification code is invalid or expired.</color></size>";
            }

            if ((int)statusCode == 409 || errorToken.Contains("already used") || errorToken.Contains("used"))
            {
                branch = "already-used";
                return "<size=14><color=#ff6666>This verification code has already been used.</color></size>";
            }

            branch = "service-unavailable";
            return "<size=14><color=#ff6666>Linking service is currently unavailable. Please try again later.</color></size>";
        }
    }
}