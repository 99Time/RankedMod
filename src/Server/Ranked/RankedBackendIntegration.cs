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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace schrader.Server
{
    public static partial class RankedSystem
    {
        private const string BackendConfigFileName = "schrader_backend_config.json";
        private const string BackendBaseUrlEnvVar = "SCHRADER_BACKEND_BASE_URL";
        private const string BackendApiKeyEnvVar = "SCHRADER_BACKEND_API_KEY";
        private const string BackendPlayerStatePathEnvVar = "SCHRADER_BACKEND_PLAYER_STATE_PATH";
        private const string BackendMatchResultPathEnvVar = "SCHRADER_BACKEND_MATCH_RESULT_PATH";
        private const string BackendTimeoutMsEnvVar = "SCHRADER_BACKEND_TIMEOUT_MS";
        private const string BackendModerationCacheSecondsEnvVar = "SCHRADER_BACKEND_MODERATION_CACHE_SECONDS";
        private const string BackendBadgeCacheSecondsEnvVar = "SCHRADER_BACKEND_BADGE_CACHE_SECONDS";
        private const string DefaultBackendPlayerStatePath = "/api/puck/players/{steamId}";
        private const string DefaultBackendMatchResultPath = "/api/puck/matches";
        private const int DefaultBackendTimeoutMs = 5000;
        private const int DefaultBackendModerationCacheSeconds = 60;
        private const int DefaultBackendBadgeCacheSeconds = 300;
        private const string DefaultBadgeColorHex = "#f7c66b";
        private const string DefaultTitleColorHex = "#cfe6ff";
        private static readonly HttpClient backendHttpClient = new HttpClient();
        private static readonly object backendStateLock = new object();
        private static readonly Dictionary<string, BackendPlayerState> backendPlayerStateBySteamId = new Dictionary<string, BackendPlayerState>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> backendPlayerFetchesInFlight = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static BackendConfig backendConfig;
        private static bool backendConfigLoaded;

        private sealed class BackendConfig
        {
            public bool Enabled = true;
            public string BaseUrl;
            public string ApiKey;
            public string PlayerStatePath = DefaultBackendPlayerStatePath;
            public string MatchResultPath = DefaultBackendMatchResultPath;
            public int TimeoutMs = DefaultBackendTimeoutMs;
            public int ModerationCacheSeconds = DefaultBackendModerationCacheSeconds;
            public int BadgeCacheSeconds = DefaultBackendBadgeCacheSeconds;
        }

        private sealed class BackendPlayerState
        {
            public string SteamId;
            public bool IsMuted;
            public bool IsBanned;
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
                if (clientId == 0 || !TryGetPlayerByClientId(clientId, out var player) || player == null)
                {
                    return;
                }

                var steamId = NormalizeResolvedPlayerKey(TryGetPlayerId(player, clientId));
                if (!IsSteamIdentityKey(steamId))
                {
                    return;
                }

                RankedOverlayNetwork.PublishScoreboardBadgesToClient(clientId, GetScoreboardBadgeStateForClient(clientId));
                EnsureBackendPlayerStateQueued(steamId, "synchronize-complete", forceRefresh: true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] [BACKEND] Failed to handle synchronize-complete backend bootstrap: {ex.Message}");
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

                var builder = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(state.TagText))
                {
                    builder.Append("<b><color=");
                    builder.Append(NormalizeColorHex(state.TagColorHex, DefaultBadgeColorHex));
                    builder.Append(">[");
                    builder.Append(EscapeRichText(state.TagText));
                    builder.Append("]</color></b> ");
                }

                if (!string.IsNullOrWhiteSpace(state.TitleText)
                    && !string.Equals(state.TitleText, state.TagText, StringComparison.OrdinalIgnoreCase))
                {
                    builder.Append("<color=");
                    builder.Append(NormalizeColorHex(state.TitleColorHex, DefaultTitleColorHex));
                    builder.Append(">");
                    builder.Append(EscapeRichText(state.TitleText));
                    builder.Append("</color> ");
                }

                return builder.ToString();
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

                _ = SendMatchResultToBackendAsync(config, report);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] [BACKEND] Failed to queue match result report: {ex.Message}");
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

                entryMap[steamId] = new ScoreboardBadgeEntryMessage
                {
                    PlayerId = steamId,
                    ClientId = snapshot.clientId,
                    BadgeText = badgeText,
                    ColorHex = ResolveCompactBadgeColor(state)
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

        private static void EnsureBackendPlayerStateQueued(string steamId, string reason, bool forceRefresh)
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

                        lock (backendStateLock)
                        {
                            backendPlayerStateBySteamId[steamId] = state;
                        }

                        Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Player state refreshed. steamId={steamId} muted={state.IsMuted} banned={state.IsBanned} tag={state.TagText ?? "none"} title={state.TitleText ?? "none"} reason={reason}");
                        EnforceConnectedBackendModeration(steamId, state, reason);
                        PublishScoreboardBadgeState();
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

        private static void EnforceConnectedBackendModeration(string steamId, BackendPlayerState state, string reason)
        {
            if (state == null || !state.IsBanned || !TryFindConnectedPlayerBySteamId(steamId, out _, out _))
            {
                return;
            }

            Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Disconnecting banned player {steamId}. reason={state.BanReason ?? "backend ban"} source={reason}");
            TryKickPlayer(steamId, 0UL);
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
            try
            {
                var configPath = GetBackendConfigPath();
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var fileConfig = JsonConvert.DeserializeObject<BackendConfig>(json);
                    if (fileConfig != null)
                    {
                        config.Enabled = fileConfig.Enabled;
                        config.BaseUrl = fileConfig.BaseUrl;
                        config.ApiKey = fileConfig.ApiKey;
                        config.PlayerStatePath = string.IsNullOrWhiteSpace(fileConfig.PlayerStatePath) ? config.PlayerStatePath : fileConfig.PlayerStatePath;
                        config.MatchResultPath = string.IsNullOrWhiteSpace(fileConfig.MatchResultPath) ? config.MatchResultPath : fileConfig.MatchResultPath;
                        config.TimeoutMs = fileConfig.TimeoutMs > 0 ? fileConfig.TimeoutMs : config.TimeoutMs;
                        config.ModerationCacheSeconds = fileConfig.ModerationCacheSeconds > 0 ? fileConfig.ModerationCacheSeconds : config.ModerationCacheSeconds;
                        config.BadgeCacheSeconds = fileConfig.BadgeCacheSeconds > 0 ? fileConfig.BadgeCacheSeconds : config.BadgeCacheSeconds;
                    }
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
            config.TimeoutMs = GetEnvironmentOverrideInt(BackendTimeoutMsEnvVar, config.TimeoutMs);
            config.ModerationCacheSeconds = GetEnvironmentOverrideInt(BackendModerationCacheSecondsEnvVar, config.ModerationCacheSeconds);
            config.BadgeCacheSeconds = GetEnvironmentOverrideInt(BackendBadgeCacheSecondsEnvVar, config.BadgeCacheSeconds);
            return config;
        }

        private static bool IsBackendConfigured(BackendConfig config)
        {
            return config != null && config.Enabled && !string.IsNullOrWhiteSpace(config.BaseUrl);
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
                var nowUtc = DateTime.UtcNow;

                return new BackendPlayerState
                {
                    SteamId = FirstNonEmptyString(payload, "steamId", "playerId", "id") ?? steamId,
                    IsMuted = FirstBoolean(moderationNode, "muted", "isMuted") ?? FirstBoolean(payload, "muted", "isMuted") ?? false,
                    IsBanned = FirstBoolean(moderationNode, "banned", "isBanned") ?? FirstBoolean(payload, "banned", "isBanned") ?? false,
                    MuteReason = FirstNonEmptyString(moderationNode, "muteReason", "reason") ?? FirstNonEmptyString(payload, "muteReason"),
                    BanReason = FirstNonEmptyString(moderationNode, "banReason", "reason") ?? FirstNonEmptyString(payload, "banReason"),
                    TagText = FirstNonEmptyString(badgeNode, "tag", "tagText", "badge", "label") ?? FirstNonEmptyString(payload, "tag", "tagText", "badge", "label"),
                    TagColorHex = NormalizeColorHex(FirstNonEmptyString(badgeNode, "tagColorHex", "tagColor", "color") ?? FirstNonEmptyString(payload, "tagColorHex", "tagColor", "badgeColor", "color"), DefaultBadgeColorHex),
                    TitleText = FirstNonEmptyString(badgeNode, "title", "rankedTitle", "subtitle") ?? FirstNonEmptyString(payload, "title", "rankedTitle", "subtitle"),
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

            if (!string.IsNullOrWhiteSpace(state.TagText))
            {
                return state.TagText.Trim();
            }

            return string.IsNullOrWhiteSpace(state.TitleText) ? null : state.TitleText.Trim();
        }

        private static string ResolveCompactBadgeColor(BackendPlayerState state)
        {
            if (state == null)
            {
                return DefaultBadgeColorHex;
            }

            return !string.IsNullOrWhiteSpace(state.TagText)
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

        private static async Task SendMatchResultToBackendAsync(BackendConfig config, BackendMatchResultReport report)
        {
            try
            {
                var requestUrl = BuildConfiguredUrl(config.BaseUrl, config.MatchResultPath, null);
                if (string.IsNullOrWhiteSpace(requestUrl))
                {
                    return;
                }

                var json = JsonConvert.SerializeObject(report, Formatting.None);
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
                            Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Match result report failed: {(int)response.StatusCode} {response.ReasonPhrase}. Response={responseText}");
                            return;
                        }

                        Debug.Log($"[{Constants.MOD_NAME}] [BACKEND] Match result reported successfully.");
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Match result report timed out: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [BACKEND] Match result report failed: {ex.Message}");
            }
        }

        private static string GetBackendConfigPath()
        {
            var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            var userDataDirectory = Path.Combine(root, "UserData");
            Directory.CreateDirectory(userDataDirectory);
            return Path.Combine(userDataDirectory, BackendConfigFileName);
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

                var value = token.Type == JTokenType.String ? token.Value<string>() : token.ToString(Formatting.None);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
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
    }
}