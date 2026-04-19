using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;

namespace schrader.Server
{
    public static partial class RankedSystem
    {
        private const string MatchResultDiscordWebhookUrl = "https://discord.com/api/webhooks/1489772336268837075/OceMBsCeDQA3erEMMyuKQXhOHnEde8ck8EqEY2zeDouqRhX5i-_KnO4V0Szyd4tKFPJb";
        private const string ServerStatusDiscordWebhookUrl = "https://discord.com/api/webhooks/1490448379308671209/9p0kVsoDaAcgI7cjo5H2lIgkG8oqzzfp4WYrJD1f6Vm04dNO07XbGUa6Dqh0FhM3lkDl";
        private const string ServerStatusActivityApiUrl = "http://127.0.0.1:8080/api/servers/activity";
        private const string ServerStatusActivityApiBearerToken = "sr_sp1212";
        private const int ServerStatusHttpTimeoutMs = 8000;
        private static readonly bool MatchResultWebhookEnabled = false;
        private static readonly bool ServerStatusWebhookUsesWithComponents = false;
        private static readonly bool ServerStatusWebhookUsesComponentsV2 = false;
        private static readonly HttpClient discordWebhookHttpClient = new HttpClient();
        private static readonly object serverStatusWebhookMessageLock = new object();
        private static readonly string[] ServerStatusWebhookServerNames = { "2 vs 2", "3 vs 3", "6 vs 6", "2 vs 2 #2" };
        private static readonly Dictionary<string, string> steamToDiscordMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Example: ["76561198000000000"] = "123456789012345678"
        };

        private sealed class ServerStatusWebhookEntry
        {
            public string Name;
            public ServerAvailabilityState Status;
        }

        internal enum ServerAvailabilityState
        {
            Active,
            Inactive,
            Offline
        }

        private sealed class ServerStatusApiResponse
        {
            public ServerStatusApiServer[] Servers { get; set; }
        }

        private sealed class ServerStatusApiServer
        {
            public string Name { get; set; }
            public string Status { get; set; }
        }

        private sealed class DiscordWebhookMessageResponse
        {
            public string Id { get; set; }
        }

        private sealed class MatchResultData
        {
            public TeamResult WinningTeam;
            public int RedScore;
            public int BlueScore;
            public List<DiscordMatchPlayerData> RedTeam;
            public List<DiscordMatchPlayerData> BlueTeam;
            public DiscordMatchPlayerData RedMvp;
            public DiscordMatchPlayerData BlueMvp;
        }

        private sealed class DiscordMatchPlayerData
        {
            public string DisplayName;
            public string CommandTarget;
            public int Mmr;
            public int MmrBefore;
            public int MmrAfter;
            public int MmrDelta;
            public int Goals;
            public int Assists;
            public int Saves;
            public int Shots;
            public bool IsMVP;
            public TeamResult Team;
        }

        private static MatchResultData BuildDiscordMatchResultData(MatchResultMessage matchResult)
        {
            if (matchResult == null || !matchResult.IsVisible)
            {
                return null;
            }

            var players = matchResult.Players ?? Array.Empty<MatchResultPlayerMessage>();
            ResolveAuthoritativeFinalScore(out var redScore, out var blueScore);
            var redTeam = players
                .Where(player => player != null && player.Team == TeamResult.Red)
                .Select(BuildDiscordMatchPlayerData)
                .Where(player => player != null)
                .ToList();
            var blueTeam = players
                .Where(player => player != null && player.Team == TeamResult.Blue)
                .Select(BuildDiscordMatchPlayerData)
                .Where(player => player != null)
                .ToList();

            return new MatchResultData
            {
                WinningTeam = matchResult.WinningTeam,
                RedScore = redScore,
                BlueScore = blueScore,
                RedTeam = redTeam,
                BlueTeam = blueTeam,
                RedMvp = SelectTeamMvp(redTeam),
                BlueMvp = SelectTeamMvp(blueTeam)
            };
        }

        private static DiscordMatchPlayerData BuildDiscordMatchPlayerData(MatchResultPlayerMessage player)
        {
            if (player == null)
            {
                return null;
            }

            return new DiscordMatchPlayerData
            {
                DisplayName = NormalizeVisiblePlayerName(player.Username) ?? "Player",
                CommandTarget = NormalizeResolvedPlayerKey(player.Id),
                Mmr = player.MmrAfter,
                MmrBefore = player.MmrBefore,
                MmrAfter = player.MmrAfter,
                MmrDelta = player.MmrDelta,
                Goals = player.Goals,
                Assists = player.Assists,
                Saves = player.Saves,
                Shots = player.Shots,
                IsMVP = player.IsMVP,
                Team = player.Team
            };
        }

        private static bool TryQueueMatchResultWebhookFallback(MatchResultMessage matchResult, string serverName)
        {
            if (IsPublicServerMode(GetBackendConfig()))
            {
                Debug.Log($"[{Constants.MOD_NAME}] [DISCORD] Match result webhook skipped because serverMode=public.");
                return false;
            }

            if (!MatchResultWebhookEnabled)
            {
                Debug.Log($"[{Constants.MOD_NAME}] [DISCORD] Match result webhook skipped. mode=disabled backendPrimary=true fallbackUsed=false unsupportedPublicStatsRemoved=true");
                return false;
            }

            var discordMatchResult = BuildDiscordMatchResultData(matchResult);
            if (discordMatchResult == null)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Match result webhook fallback skipped because no visible match result payload was available.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(serverName))
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Match result webhook fallback skipped because the server name could not be resolved.");
                return false;
            }

            Debug.Log($"[{Constants.MOD_NAME}] [DISCORD] Match result webhook fallback engaged. backendPrimary=true fallbackUsed=true unsupportedPublicStatsRemoved=true");
            _ = SendMatchResultToDiscordAsync(MatchResultDiscordWebhookUrl, discordMatchResult, serverName);
            return true;
        }

        private static async Task SendMatchResultToDiscordAsync(string webhookUrl, MatchResultData data, string serverName)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl) || data == null)
            {
                return;
            }

            try
            {
                var webhookExecuteUrl = webhookUrl.Trim();
                var payload = new
                {
                    embeds = new[]
                    {
                        new
                        {
                            title = $"{FormatWebhookServerName(serverName)} | Match Result",
                            color = ResolveWebhookColor(data.WinningTeam),
                            timestamp = DateTime.UtcNow.ToString("O"),
                            fields = new object[]
                            {
                                new
                                {
                                    name = "Score",
                                    value = FormatWebhookScore(data),
                                    inline = false
                                },
                                new
                                {
                                    name = "Winning Team",
                                    value = FormatWinningTeamForWebhook(data.WinningTeam),
                                    inline = false
                                },
                                new
                                {
                                    name = "Red MVP",
                                    value = FormatWebhookMvp(data.RedMvp),
                                    inline = true
                                },
                                new
                                {
                                    name = "Blue MVP",
                                    value = FormatWebhookMvp(data.BlueMvp),
                                    inline = true
                                },
                                new
                                {
                                    name = "Red Team",
                                    value = FormatWebhookTeamPlayers(data.RedTeam),
                                    inline = false
                                },
                                new
                                {
                                    name = "Blue Team",
                                    value = FormatWebhookTeamPlayers(data.BlueTeam),
                                    inline = false
                                }
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                using (var response = await discordWebhookHttpClient.PostAsync(webhookUrl.Trim(), content).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Webhook post failed: {(int)response.StatusCode} {response.ReasonPhrase}. Response={responseText}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Failed to send match result webhook: {ex.Message}");
            }
        }

        internal static async Task SendServerStatusToDiscordAsync(IReadOnlyDictionary<string, ServerAvailabilityState> authoritativeStatuses, Action<string> debugOutput = null)
        {
            if (!TryBuildServerStatusWebhookEntries(authoritativeStatuses, out var entries))
            {
                debugOutput?.Invoke("Discord webhook post skipped because required server names were missing.");
                return;
            }

            try
            {
                var payload = new
                {
                    embeds = new[]
                    {
                        new
                        {
                            title = "PUCK Ranked Server Status",
                            color = 0x2F80ED,
                            description = "Live server availability.",
                            timestamp = DateTime.UtcNow.ToString("O"),
                            fields = entries.Select(entry => new
                            {
                                name = entry.Name,
                                value = BuildServerStatusFieldValue(entry),
                                inline = true
                            }).ToArray()
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var storedMessageId = LoadStoredServerStatusWebhookMessageId();
                var attemptedEdit = !string.IsNullOrWhiteSpace(storedMessageId);
                var targetUrl = attemptedEdit
                    ? BuildServerStatusWebhookMessageEditUrl(storedMessageId)
                    : BuildServerStatusWebhookPostUrl(waitForMessageId: true);
                var method = attemptedEdit ? HttpMethod.Patch : HttpMethod.Post;

                using (var request = new HttpRequestMessage(method, targetUrl))
                {
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = null;
                    string responseText = null;

                    try
                    {
                        debugOutput?.Invoke(attemptedEdit ? "Before PATCH to Discord." : "Before POST to Discord.");
                        debugOutput?.Invoke($"Final webhook URL used: {SanitizeDiscordWebhookUrlForDebug(targetUrl)}");
                        debugOutput?.Invoke($"with_components added: {ServerStatusWebhookUsesWithComponents}");
                        debugOutput?.Invoke($"IS_COMPONENTS_V2 used: {ServerStatusWebhookUsesComponentsV2}");
                        using (var timeoutCts = new System.Threading.CancellationTokenSource(ServerStatusHttpTimeoutMs))
                        {
                            response = await discordWebhookHttpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
                        }

                        debugOutput?.Invoke($"After {method.Method} returns: {(int)response.StatusCode} {response.ReasonPhrase}");
                    }
                    catch (OperationCanceledException ex)
                    {
                        debugOutput?.Invoke("Discord webhook request timed out");
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Discord webhook request timed out: {ex.Message}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        debugOutput?.Invoke($"Discord POST failure: {ex.GetType().Name}: {ex.Message}");
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Failed to send server status webhook: {ex}");
                        return;
                    }

                    using (response)
                    {
                        try
                        {
                            debugOutput?.Invoke("Before reading Discord response body.");
                            responseText = await AwaitWithTimeoutAsync(response.Content.ReadAsStringAsync(), ServerStatusHttpTimeoutMs, "Discord response body read timed out").ConfigureAwait(false);
                            debugOutput?.Invoke($"After reading Discord response body: {(string.IsNullOrWhiteSpace(responseText) ? "(empty)" : responseText)}");
                        }
                        catch (TimeoutException ex)
                        {
                            debugOutput?.Invoke(ex.Message);
                            Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] {ex.Message}");
                            return;
                        }
                        catch (Exception ex)
                        {
                            debugOutput?.Invoke($"Discord response read failure: {ex.GetType().Name}: {ex.Message}");
                            Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Failed to read Discord response body: {ex}");
                            return;
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            debugOutput?.Invoke($"Discord response status code: {(int)response.StatusCode}");
                            debugOutput?.Invoke($"Discord failure body: {(string.IsNullOrWhiteSpace(responseText) ? "(empty)" : responseText)}");
                            if (attemptedEdit && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                            {
                                debugOutput?.Invoke("Stored webhook message id no longer exists. Reposting new status message.");
                                ClearStoredServerStatusWebhookMessageId();
                            }
                            else
                            {
                                Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Server status webhook {method.Method} failed: {(int)response.StatusCode} {response.ReasonPhrase}. Response={responseText}");
                                return;
                            }
                        }
                        else
                        {
                            if (!attemptedEdit)
                            {
                                var postedMessageId = TryParseDiscordWebhookMessageId(responseText);
                                if (!string.IsNullOrWhiteSpace(postedMessageId))
                                {
                                    SaveStoredServerStatusWebhookMessageId(postedMessageId);
                                    debugOutput?.Invoke($"Stored new webhook message id: {postedMessageId}");
                                }
                            }

                            debugOutput?.Invoke($"Discord response status code: {(int)response.StatusCode}");
                            debugOutput?.Invoke("Discord webhook publish succeeded.");
                            return;
                        }
                    }
                }

                await PostNewServerStatusWebhookMessageAsync(json, debugOutput).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                debugOutput?.Invoke($"Discord webhook exception: {ex.GetType().Name}: {ex.Message}");
                Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Failed to send server status webhook: {ex.Message}");
            }
        }

        private static async Task PostNewServerStatusWebhookMessageAsync(string json, Action<string> debugOutput)
        {
            var webhookPostUrl = BuildServerStatusWebhookPostUrl(waitForMessageId: true);
            using (var request = new HttpRequestMessage(HttpMethod.Post, webhookPostUrl))
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using (var timeoutCts = new System.Threading.CancellationTokenSource(ServerStatusHttpTimeoutMs))
                using (var response = await discordWebhookHttpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false))
                {
                    var responseText = await AwaitWithTimeoutAsync(response.Content.ReadAsStringAsync(), ServerStatusHttpTimeoutMs, "Discord response body read timed out").ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Server status webhook POST failed: {(int)response.StatusCode} {response.ReasonPhrase}. Response={responseText}");
                        return;
                    }

                    var postedMessageId = TryParseDiscordWebhookMessageId(responseText);
                    if (!string.IsNullOrWhiteSpace(postedMessageId))
                    {
                        SaveStoredServerStatusWebhookMessageId(postedMessageId);
                        debugOutput?.Invoke($"Stored new webhook message id: {postedMessageId}");
                    }

                    debugOutput?.Invoke($"Discord response status code: {(int)response.StatusCode}");
                    debugOutput?.Invoke("Discord webhook publish succeeded after repost.");
                }
            }
        }

        internal static async Task PublishServerStatusFromSpeedupAsync(Action<string> debugOutput = null)
        {
            HttpResponseMessage response = null;
            string responseText = null;

            try
            {
                debugOutput?.Invoke("Starting fetch to /api/servers/activity.");
                try
                {
                    debugOutput?.Invoke("Before GET.");
                    debugOutput?.Invoke($"Final request URL: {ServerStatusActivityApiUrl}");
                    using (var request = new HttpRequestMessage(HttpMethod.Get, ServerStatusActivityApiUrl))
                    using (var timeoutCts = new System.Threading.CancellationTokenSource(ServerStatusHttpTimeoutMs))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ServerStatusActivityApiBearerToken);
                        debugOutput?.Invoke("Auth header attached.");
                        response = await discordWebhookHttpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
                    }

                    debugOutput?.Invoke($"After GET returns: {(int)response.StatusCode} {response.ReasonPhrase}");
                    debugOutput?.Invoke($"API response status: {(int)response.StatusCode} {response.ReasonPhrase}");
                }
                catch (OperationCanceledException ex)
                {
                    debugOutput?.Invoke("API request timed out");
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Server activity request timed out: {ex.Message}");
                    await SendServerStatusToDiscordAsync(BuildOfflineServerStatusMap(), debugOutput).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    debugOutput?.Invoke($"GET failure: {ex.GetType().Name}: {ex.Message}");
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Server activity fetch failed: {ex}");
                    await SendServerStatusToDiscordAsync(BuildOfflineServerStatusMap(), debugOutput).ConfigureAwait(false);
                    return;
                }

                using (response)
                {
                    try
                    {
                        debugOutput?.Invoke("Before reading API response body.");
                        responseText = await AwaitWithTimeoutAsync(response.Content.ReadAsStringAsync(), ServerStatusHttpTimeoutMs, "API response body read timed out").ConfigureAwait(false);
                        debugOutput?.Invoke($"After response body is read: {(string.IsNullOrWhiteSpace(responseText) ? "(empty)" : $"{responseText.Length} chars")}");
                    }
                    catch (TimeoutException ex)
                    {
                        debugOutput?.Invoke(ex.Message);
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] {ex.Message}");
                        await SendServerStatusToDiscordAsync(BuildOfflineServerStatusMap(), debugOutput).ConfigureAwait(false);
                        return;
                    }
                    catch (Exception ex)
                    {
                        debugOutput?.Invoke($"API body read failure: {ex.GetType().Name}: {ex.Message}");
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Failed to read server activity response body: {ex}");
                        await SendServerStatusToDiscordAsync(BuildOfflineServerStatusMap(), debugOutput).ConfigureAwait(false);
                        return;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        debugOutput?.Invoke($"API failure: {(int)response.StatusCode} {response.ReasonPhrase}");
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Server activity fetch failed: {(int)response.StatusCode} {response.ReasonPhrase}. Response={responseText}");
                        await SendServerStatusToDiscordAsync(BuildOfflineServerStatusMap(), debugOutput).ConfigureAwait(false);
                        return;
                    }

                    debugOutput?.Invoke($"API success: {(int)response.StatusCode} {response.ReasonPhrase}");

                    Dictionary<string, ServerAvailabilityState> authoritativeStatuses;
                    int deserializedServerCount;
                    try
                    {
                        TryBuildAuthoritativeServerStatusMapFromApiJson(responseText, out authoritativeStatuses, out deserializedServerCount);
                    }
                    catch (JsonException ex)
                    {
                        debugOutput?.Invoke($"JSON deserialize failure: {ex.GetType().Name}: {ex.Message}");
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Failed to parse SpeedUP server activity JSON: {ex}");
                        authoritativeStatuses = BuildOfflineServerStatusMap();
                        deserializedServerCount = 0;
                        debugOutput?.Invoke("Falling back to Offline status for all servers.");
                    }
                    catch (Exception ex)
                    {
                        debugOutput?.Invoke($"Mapping failure: {ex.GetType().Name}: {ex.Message}");
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Failed while mapping SpeedUP server activity: {ex}");
                        authoritativeStatuses = BuildOfflineServerStatusMap();
                        deserializedServerCount = 0;
                        debugOutput?.Invoke("Falling back to Offline status for all servers.");
                    }

                    debugOutput?.Invoke($"Number of servers deserialized: {deserializedServerCount}");
                    debugOutput?.Invoke($"Number of servers mapped: {authoritativeStatuses.Count(status => status.Value != ServerAvailabilityState.Offline)}");
                    debugOutput?.Invoke($"Mapped servers: {authoritativeStatuses.Count(status => status.Value != ServerAvailabilityState.Offline)}");
                    debugOutput?.Invoke($"Mapped final server names: {string.Join(", ", authoritativeStatuses.Keys.OrderBy(name => name, StringComparer.Ordinal))}");
                    await SendServerStatusToDiscordAsync(authoritativeStatuses, debugOutput).ConfigureAwait(false);
                    Debug.Log($"[{Constants.MOD_NAME}] [DISCORD] Server status publish completed from SpeedUP API.");
                }
            }
            catch (Exception ex)
            {
                debugOutput?.Invoke($"Status publish exception: {ex.GetType().Name}: {ex.Message}");
                Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Failed to publish server status from SpeedUP API: {ex}");
                await SendServerStatusToDiscordAsync(BuildOfflineServerStatusMap(), debugOutput).ConfigureAwait(false);
            }
        }

        private static async Task<T> AwaitWithTimeoutAsync<T>(Task<T> task, int timeoutMs, string timeoutMessage)
        {
            var completedTask = await Task.WhenAny(task, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (completedTask != task)
            {
                throw new TimeoutException(timeoutMessage);
            }

            return await task.ConfigureAwait(false);
        }

        private static bool TryBuildServerStatusWebhookEntries(IReadOnlyDictionary<string, ServerAvailabilityState> authoritativeStatuses, out ServerStatusWebhookEntry[] entries)
        {
            entries = Array.Empty<ServerStatusWebhookEntry>();
            if (authoritativeStatuses == null)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Skipped server status webhook because no authoritative status map was provided.");
                return false;
            }

            var builtEntries = new List<ServerStatusWebhookEntry>(ServerStatusWebhookServerNames.Length);
            foreach (var serverName in ServerStatusWebhookServerNames)
            {
                if (!authoritativeStatuses.TryGetValue(serverName, out var status))
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Skipped server status webhook because '{serverName}' was missing from the authoritative status map.");
                    return false;
                }

                builtEntries.Add(new ServerStatusWebhookEntry
                {
                    Name = serverName,
                    Status = status
                });
            }

            entries = builtEntries.ToArray();
            return true;
        }

        private static string BuildServerStatusFieldValue(ServerStatusWebhookEntry entry)
        {
            return FormatServerAvailability(entry.Status);
        }

        private static string BuildServerStatusWebhookPostUrl(bool waitForMessageId)
        {
            var queryParameters = new List<string>();
            if (waitForMessageId)
            {
                queryParameters.Add("wait=true");
            }

            if (ServerStatusWebhookUsesWithComponents)
            {
                queryParameters.Add("with_components=true");
            }

            if (queryParameters.Count == 0)
            {
                return ServerStatusDiscordWebhookUrl;
            }

            var separator = ServerStatusDiscordWebhookUrl.Contains("?") ? "&" : "?";
            return $"{ServerStatusDiscordWebhookUrl}{separator}{string.Join("&", queryParameters)}";
        }

        private static string BuildServerStatusWebhookMessageEditUrl(string messageId)
        {
            if (string.IsNullOrWhiteSpace(messageId))
            {
                return ServerStatusDiscordWebhookUrl;
            }

            var baseWebhookUrl = ServerStatusDiscordWebhookUrl.Split('?')[0].TrimEnd('/');
            var editUrl = $"{baseWebhookUrl}/messages/{messageId}";
            if (!ServerStatusWebhookUsesWithComponents)
            {
                return editUrl;
            }

            return $"{editUrl}?with_components=true";
        }

        private static string TryParseDiscordWebhookMessageId(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return null;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<DiscordWebhookMessageResponse>(responseText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return string.IsNullOrWhiteSpace(parsed?.Id) ? null : parsed.Id.Trim();
            }
            catch { }

            return null;
        }

        private static string GetServerStatusWebhookMessageIdPath()
        {
            var root = GetGameRootPath();
            var userDataDirectory = Path.Combine(root, "UserData");
            Directory.CreateDirectory(userDataDirectory);
            return Path.Combine(userDataDirectory, "schrader_server_status_webhook_message.txt");
        }

        private static string LoadStoredServerStatusWebhookMessageId()
        {
            lock (serverStatusWebhookMessageLock)
            {
                try
                {
                    var path = GetServerStatusWebhookMessageIdPath();
                    if (!File.Exists(path))
                    {
                        return null;
                    }

                    var text = File.ReadAllText(path)?.Trim();
                    return string.IsNullOrWhiteSpace(text) ? null : text;
                }
                catch { }

                return null;
            }
        }

        private static void SaveStoredServerStatusWebhookMessageId(string messageId)
        {
            if (string.IsNullOrWhiteSpace(messageId))
            {
                return;
            }

            lock (serverStatusWebhookMessageLock)
            {
                try
                {
                    File.WriteAllText(GetServerStatusWebhookMessageIdPath(), messageId.Trim());
                }
                catch { }
            }
        }

        private static void ClearStoredServerStatusWebhookMessageId()
        {
            lock (serverStatusWebhookMessageLock)
            {
                try
                {
                    var path = GetServerStatusWebhookMessageIdPath();
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch { }
            }
        }

        private static Dictionary<string, ServerAvailabilityState> BuildOfflineServerStatusMap()
        {
            var statuses = new Dictionary<string, ServerAvailabilityState>(StringComparer.Ordinal);
            foreach (var serverName in ServerStatusWebhookServerNames)
            {
                statuses[serverName] = ServerAvailabilityState.Offline;
            }

            return statuses;
        }

        private static string SanitizeDiscordWebhookUrlForDebug(string webhookUrl)
        {
            if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out var uri))
            {
                return "[invalid webhook url]";
            }

            var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 3 && string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) && string.Equals(segments[1], "webhooks", StringComparison.OrdinalIgnoreCase))
            {
                var builder = new StringBuilder();
                builder.Append(uri.Scheme).Append("://").Append(uri.Authority).Append("/");
                builder.Append(segments[0]).Append("/").Append(segments[1]).Append("/").Append(segments[2]).Append("/[redacted]");
                builder.Append(uri.Query);
                return builder.ToString();
            }

            return $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}{uri.Query}";
        }

        private static bool TryBuildAuthoritativeServerStatusMapFromApiJson(string json, out Dictionary<string, ServerAvailabilityState> authoritativeStatuses, out int deserializedServerCount)
        {
            authoritativeStatuses = BuildOfflineServerStatusMap();
            deserializedServerCount = 0;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                var response = JsonSerializer.Deserialize<ServerStatusApiResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                var servers = response?.Servers;
                if (servers == null || servers.Length == 0)
                {
                    return false;
                }

                deserializedServerCount = servers.Length;
                foreach (var server in servers)
                {
                    if (server == null || string.IsNullOrWhiteSpace(server.Name))
                    {
                        continue;
                    }

                    if (!TryMapApiServerNameToDiscordName(server.Name, out var discordName))
                    {
                        continue;
                    }

                    if (!TryParseServerActivityStatus(server.Status, out var status))
                    {
                        continue;
                    }

                    authoritativeStatuses[discordName] = status;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [DISCORD] Failed to parse SpeedUP server activity JSON: {ex.Message}");
                return false;
            }
        }

        private static bool TryMapApiServerNameToDiscordName(string apiName, out string discordName)
        {
            discordName = null;
            var normalizedApiName = NormalizeServerStatusName(apiName);
            if (string.IsNullOrWhiteSpace(normalizedApiName))
            {
                return false;
            }

            if (normalizedApiName.Contains("2v2 #2"))
            {
                discordName = "2 vs 2 #2";
                return true;
            }

            if (normalizedApiName.Contains("2v2"))
            {
                discordName = "2 vs 2";
                return true;
            }

            if (normalizedApiName.Contains("3v3"))
            {
                discordName = "3 vs 3";
                return true;
            }

            if (normalizedApiName.Contains("6v6"))
            {
                discordName = "6 vs 6";
                return true;
            }

            return false;
        }

        private static string NormalizeServerStatusName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            var insideTag = false;
            foreach (var ch in value)
            {
                if (ch == '<')
                {
                    insideTag = true;
                    continue;
                }

                if (ch == '>')
                {
                    insideTag = false;
                    continue;
                }

                if (!insideTag)
                {
                    builder.Append(ch);
                }
            }

            return string.Join(" ", builder.ToString().Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        }

        private static bool TryParseServerActivityStatus(string status, out ServerAvailabilityState state)
        {
            state = ServerAvailabilityState.Offline;
            if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                state = ServerAvailabilityState.Active;
                return true;
            }

            if (string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
            {
                state = ServerAvailabilityState.Inactive;
                return true;
            }

            return false;
        }

        private static string FormatServerAvailability(ServerAvailabilityState state)
        {
            switch (state)
            {
                case ServerAvailabilityState.Active:
                    return "🟢 Active";
                case ServerAvailabilityState.Inactive:
                    return "🟡 Inactive";
                default:
                    return "🔴 Offline";
            }
        }

        private static bool TryGetCurrentServerName(out string serverName)
        {
            serverName = null;

            try
            {
                if (TryGetServerNameFromConfiguration(out serverName))
                {
                    return true;
                }

                if (TryGetServerNameFromRuntimeServer(out serverName))
                {
                    return true;
                }
            }
            catch { }

            serverName = null;
            return false;
        }

        private static bool TryGetServerNameFromConfiguration(out string serverName)
        {
            serverName = null;

            try
            {
                var managerType = FindTypeByName("ServerConfigurationManager", "Puck.ServerConfigurationManager");
                var manager = GetManagerInstance(managerType);
                if (manager == null)
                {
                    return false;
                }

                var managerTypeResolved = manager.GetType();
                var configuration = managerTypeResolved.GetProperty("ServerConfiguration", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(manager);
                if (configuration == null)
                {
                    var configMethod = managerTypeResolved.GetMethod("get_ServerConfiguration", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                    if (configMethod != null)
                    {
                        configuration = configMethod.Invoke(manager, null);
                    }
                }

                if (configuration == null)
                {
                    return false;
                }

                var configurationType = configuration.GetType();
                var nameValue = configurationType.GetProperty("name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(configuration)
                    ?? configurationType.GetProperty("Name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(configuration)
                    ?? configurationType.GetField("name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(configuration)
                    ?? configurationType.GetField("Name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(configuration);

                serverName = NormalizeVisiblePlayerName(ExtractSimpleValueToString(nameValue));
                return !string.IsNullOrWhiteSpace(serverName);
            }
            catch
            {
                serverName = null;
                return false;
            }
        }

        private static bool TryGetServerNameFromRuntimeServer(out string serverName)
        {
            serverName = null;

            try
            {
                var managerType = FindTypeByName("ServerManager", "Puck.ServerManager");
                var manager = GetManagerInstance(managerType);
                if (manager == null)
                {
                    return false;
                }

                var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var managerTypeResolved = manager.GetType();
                var serverValue = managerTypeResolved.GetProperty("Server", flags)?.GetValue(manager)
                    ?? managerTypeResolved.GetField("Server", flags)?.GetValue(manager);
                if (serverValue == null)
                {
                    var serverMethod = managerTypeResolved.GetMethod("get_Server", flags, null, Type.EmptyTypes, null);
                    if (serverMethod != null)
                    {
                        serverValue = serverMethod.Invoke(manager, null);
                    }
                }

                if (serverValue == null)
                {
                    return false;
                }

                var serverType = serverValue.GetType();
                var nameValue = serverType.GetProperty("Name", flags)?.GetValue(serverValue)
                    ?? serverType.GetField("Name", flags)?.GetValue(serverValue)
                    ?? serverType.GetProperty("name", flags)?.GetValue(serverValue)
                    ?? serverType.GetField("name", flags)?.GetValue(serverValue);

                serverName = NormalizeVisiblePlayerName(ExtractSimpleValueToString(nameValue));
                return !string.IsNullOrWhiteSpace(serverName);
            }
            catch
            {
                serverName = null;
                return false;
            }
        }

        private static string FormatWebhookServerName(string serverName)
        {
            var clean = NormalizeVisiblePlayerName(serverName);
            return string.IsNullOrWhiteSpace(clean) ? "Server" : clean;
        }

        private static string FormatWinningTeamForWebhook(TeamResult winner)
        {
            switch (winner)
            {
                case TeamResult.Red:
                    return "Red";
                case TeamResult.Blue:
                    return "Blue";
                default:
                    return "Draw";
            }
        }

        private static int ResolveWebhookColor(TeamResult winner)
        {
            switch (winner)
            {
                case TeamResult.Red:
                    return 0xD64545;
                case TeamResult.Blue:
                    return 0x3D7DFF;
                default:
                    return 0x95A5A6;
            }
        }

        private static string FormatWebhookTeamPlayers(IEnumerable<DiscordMatchPlayerData> players)
        {
            var lines = (players ?? Enumerable.Empty<DiscordMatchPlayerData>())
                .Where(player => player != null)
                .Select(FormatWebhookPlayerLine)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            return lines.Length == 0 ? "No players recorded." : string.Join("\n", lines);
        }

        private static string FormatWebhookPlayerLine(DiscordMatchPlayerData player)
        {
            if (player == null)
            {
                return null;
            }

            return $"- {FormatWebhookPlayerIdentity(player)} — G:{Mathf.Max(0, player.Goals)} A:{Mathf.Max(0, player.Assists)} | {FormatWebhookMmrProgress(player)}";
        }

        private static string FormatWebhookScore(MatchResultData data)
        {
            if (data == null)
            {
                return "Unknown";
            }

            return $"Red {Mathf.Max(0, data.RedScore)} - Blue {Mathf.Max(0, data.BlueScore)}";
        }

        private static string FormatWebhookMvp(DiscordMatchPlayerData player)
        {
            if (player == null)
            {
                return "No MVP recorded.";
            }

            return $"{FormatWebhookPlayerIdentity(player)} | {player.Mmr} MMR";
        }

        private static string FormatWebhookMmrProgress(DiscordMatchPlayerData player)
        {
            if (player == null)
            {
                return "MMR: 0 → 0 (+0)";
            }

            var before = player.MmrBefore;
            var after = player.MmrAfter;
            var delta = player.MmrDelta;
            var deltaText = delta > 0 ? $"+{delta}" : delta.ToString();
            return $"MMR: {before} → {after} ({deltaText})";
        }

        private static string FormatWebhookPlayerIdentity(DiscordMatchPlayerData player)
        {
            if (player == null)
            {
                return "Player";
            }

            if (TryBuildDiscordMention(player.CommandTarget, out var mention))
            {
                return mention;
            }

            var safeDisplayName = SanitizeDiscordLinkText(player.DisplayName);
            if (TryBuildSteamProfileUrl(player.CommandTarget, out var steamProfileUrl))
            {
                return FormatDiscordMarkdownLink(safeDisplayName, steamProfileUrl);
            }

            return safeDisplayName;
        }

        private static string FormatDiscordMarkdownLink(string linkText, string url)
        {
            var safeLinkText = SanitizeDiscordLinkText(linkText);
            if (string.IsNullOrWhiteSpace(safeLinkText) || string.IsNullOrWhiteSpace(url))
            {
                return string.IsNullOrWhiteSpace(safeLinkText) ? "Link" : safeLinkText;
            }

            return $"[{safeLinkText}]({url})";
        }

        private static DiscordMatchPlayerData SelectTeamMvp(IEnumerable<DiscordMatchPlayerData> players)
        {
            var roster = (players ?? Enumerable.Empty<DiscordMatchPlayerData>())
                .Where(player => player != null)
                .ToList();
            if (roster.Count == 0)
            {
                return null;
            }

            var markedMvp = roster.FirstOrDefault(player => player.IsMVP);
            if (markedMvp != null)
            {
                return markedMvp;
            }

            return roster
                .OrderByDescending(ComputeWebhookPerformanceScore)
                .ThenByDescending(player => player.Goals)
                .ThenByDescending(player => player.Assists)
                .ThenByDescending(player => player.Mmr)
                .ThenBy(player => player.DisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static int ComputeWebhookPerformanceScore(DiscordMatchPlayerData player)
        {
            if (player == null)
            {
                return 0;
            }

            return (player.Goals * 5) + (player.Assists * 3);
        }

        private static void ResolveAuthoritativeFinalScore(out int redScore, out int blueScore)
        {
            if (lastRedScore.HasValue && lastBlueScore.HasValue)
            {
                redScore = Mathf.Max(0, lastRedScore.Value);
                blueScore = Mathf.Max(0, lastBlueScore.Value);
                return;
            }

            if (TryGetRuntimeGameManager(out var gameManager, out var _) && TryGetScoresFromGameState(gameManager, out var liveRedScore, out var liveBlueScore))
            {
                redScore = Mathf.Max(0, liveRedScore);
                blueScore = Mathf.Max(0, liveBlueScore);
                return;
            }

            redScore = Mathf.Max(0, currentRedGoals);
            blueScore = Mathf.Max(0, currentBlueGoals);
        }

        private static bool TryBuildDiscordMention(string commandTarget, out string mention)
        {
            mention = null;
            if (string.IsNullOrWhiteSpace(commandTarget))
            {
                return false;
            }

            var normalizedTarget = NormalizeResolvedPlayerKey(commandTarget);
            if (string.IsNullOrWhiteSpace(normalizedTarget))
            {
                return false;
            }

            if (!ulong.TryParse(normalizedTarget, out var _))
            {
                return false;
            }

            if (!steamToDiscordMap.TryGetValue(normalizedTarget, out var discordUserId) || string.IsNullOrWhiteSpace(discordUserId))
            {
                return false;
            }

            mention = $"<@{discordUserId.Trim()}>";
            return true;
        }

        private static bool TryBuildSteamProfileUrl(string commandTarget, out string steamProfileUrl)
        {
            steamProfileUrl = null;

            if (string.IsNullOrWhiteSpace(commandTarget)
                || commandTarget.StartsWith("bot:", StringComparison.OrdinalIgnoreCase)
                || commandTarget.StartsWith("dummy:", StringComparison.OrdinalIgnoreCase)
                || commandTarget.StartsWith("clientId:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!ulong.TryParse(commandTarget.Trim(), out var steamId) || steamId == 0)
            {
                return false;
            }

            steamProfileUrl = $"https://steamcommunity.com/profiles/{steamId}";
            return true;
        }

        private static string SanitizeDiscordLinkText(string value)
        {
            var clean = NormalizeVisiblePlayerName(value);
            if (string.IsNullOrWhiteSpace(clean))
            {
                return "Player";
            }

            return clean
                .Replace("[", "(")
                .Replace("]", ")")
                .Replace("\r", string.Empty)
                .Replace("\n", " ");
        }
    }
}