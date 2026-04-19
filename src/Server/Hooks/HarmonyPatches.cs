using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace schrader.Server
{
    public static partial class RankedSystem
    {
        private const ulong PracticeModeFakePlayerClientIdMin = 7777777UL;
        private const ulong PracticeModeFakePlayerClientIdMax = 7777778UL;

        private static bool IsReplayPlayerObject(object player, ulong fallbackClientId = 0)
        {
            try
            {
                if (player == null)
                {
                    return false;
                }

                if (HasReplayFlag(player))
                {
                    return true;
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
                    if (IsReplayClientId(clientId))
                    {
                        return true;
                    }

                    var getReplayPlayerByClientId = managerType.GetMethod("GetReplayPlayerByClientId", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (getReplayPlayerByClientId != null)
                    {
                        var replayPlayer = getReplayPlayerByClientId.Invoke(manager, new object[] { clientId });
                        if (replayPlayer != null && AreEquivalentPlayerObjects(replayPlayer, player))
                        {
                            return true;
                        }

                        if (clientId >= ReplayClientIdOffset)
                        {
                            var sourceClientId = clientId - ReplayClientIdOffset;
                            replayPlayer = getReplayPlayerByClientId.Invoke(manager, new object[] { sourceClientId });
                            if (replayPlayer != null && AreEquivalentPlayerObjects(replayPlayer, player))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        private static bool IsPracticeModeFakePlayerObject(object player, ulong fallbackClientId = 0, string fallbackIdentity = null)
        {
            try
            {
                var clientId = fallbackClientId;
                if (clientId == 0)
                {
                    TryGetClientId(player, out clientId);
                }

                if (IsPracticeModeFakePlayerClientId(clientId))
                {
                    return true;
                }

                if (IsPracticeModeFakePlayerIdentity(fallbackIdentity))
                {
                    return true;
                }

                if (IsPracticeModeFakePlayerIdentity(TryGetPlayerIdNoFallback(player)))
                {
                    return true;
                }

                var playerName = TryGetPlayerName(player);
                if (!string.IsNullOrWhiteSpace(playerName)
                    && playerName.StartsWith("demBot", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var typeName = player?.GetType().FullName ?? player?.GetType().Name ?? string.Empty;
                if (typeName.IndexOf("FakePlayer", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static bool IsPracticeModeFakePlayerIdentity(string candidate)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    return false;
                }

                var clean = candidate.Trim();
                const string clientIdPrefix = "clientId:";
                if (clean.StartsWith(clientIdPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean.Substring(clientIdPrefix.Length).Trim();
                }

                return ulong.TryParse(clean, out var rawClientId)
                    && IsPracticeModeFakePlayerClientId(rawClientId);
            }
            catch { }

            return false;
        }

        private static bool IsPracticeModeFakePlayerClientId(ulong clientId)
        {
            return clientId >= PracticeModeFakePlayerClientIdMin
                && clientId <= PracticeModeFakePlayerClientIdMax;
        }

        private static bool ShouldIgnoreTransientTeamHookPlayer(object player, ulong clientId, string steamId)
        {
            try
            {
                if (IsReplayPlayerObject(player, clientId))
                {
                    return true;
                }

                if (IsPracticeModeFakePlayerObject(player, clientId, steamId))
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

        private static bool IsReplayPlaybackPhaseActive()
        {
            try
            {
                lock (phaseLock)
                {
                    var phase = lastGamePhaseName ?? string.Empty;
                    return phase.IndexOf("replay", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch { }

            return false;
        }

        private static bool ShouldBypassReplayTeamHooks(object player, ulong clientId, string playerKey = null)
        {
            if (!IsReplayPlaybackPhaseActive())
            {
                return false;
            }

            try
            {
                if (IsReplayPlayerObject(player, clientId))
                {
                    return true;
                }

                if (IsPracticeModeFakePlayerObject(player, clientId, playerKey))
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

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

                if ((actorPlayer == null || actorClientId == 0) && TryGetLocalPlayer(out var localPlayer, out var localClientId))
                {
                    actorPlayer = localPlayer;
                    actorClientId = localClientId;
                }

                var clickedPlayer = TryGetPlayerFromDict(dict);
                var clickedPlayerName = TryGetPlayerName(clickedPlayer) ?? "unknown";
                Debug.Log($"[{Constants.MOD_NAME}] [DRAFT] Scoreboard click received. actorClientId={actorClientId} clickedPlayer={clickedPlayerName}");

                if (actorPlayer == null || actorClientId == 0)
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [DRAFT] Scoreboard click rejected: could not resolve clicking captain.");
                    return false;
                }

                if (!TryResolveDraftUiTarget(dict, out var targetParticipant))
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [DRAFT] Scoreboard translation mismatch: clicked player could not resolve to a ranked participant.");
                    if (actorClientId != 0)
                    {
                        SendSystemChatToClient(ChatStyle.Message(ChatStyle.DraftUiModule, "Could not resolve the clicked player from the scoreboard event.", ChatTone.Error, 13), actorClientId);
                    }
                    return false;
                }

                var targetKey = ResolveParticipantIdToKey(targetParticipant);
                if (string.IsNullOrEmpty(targetKey)) return false;

                if (draftActive)
                {
                    if (!TryResolveAvailableDraftParticipant(targetKey, out var resolvedCandidate, out var failureReason, out var availableTargets, out var staleEntries, out var duplicateEntries))
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [DRAFT] Scoreboard translation mismatch. clicked={targetKey} reason={failureReason} available={string.Join(", ", availableTargets ?? Array.Empty<string>())}");

                        if (staleEntries != null && staleEntries.Length > 0)
                        {
                            Debug.LogWarning($"[{Constants.MOD_NAME}] [DRAFT] Scoreboard stale pool entries: {string.Join(", ", staleEntries)}");
                        }

                        if (duplicateEntries != null && duplicateEntries.Length > 0)
                        {
                            Debug.LogWarning($"[{Constants.MOD_NAME}] [DRAFT] Scoreboard duplicate pool entries: {string.Join(", ", duplicateEntries)}");
                        }

                        SendSystemChatToClient(ChatStyle.Message(ChatStyle.DraftUiModule, "Clicked player is not in the current available draft pool.", ChatTone.Error, 13), actorClientId);
                        return false;
                    }

                    targetParticipant = resolvedCandidate.Participant;
                    targetKey = resolvedCandidate.AuthoritativeKey;
                }

                if (draftActive && IsDraftAvailablePlayer(targetKey))
                {
                    HandleDraftPick(actorPlayer, actorClientId, targetKey);
                    return false;
                }

                if (IsPendingLateJoiner(targetKey))
                {
                    HandleLateJoinAcceptance(actorPlayer, actorClientId, targetKey);
                    return false;
                }

                if (actorClientId != 0)
                {
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.DraftUiModule, "Clicked player is not available for a draft action.", ChatTone.Warning, 13), actorClientId);
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
                var practiceExpiryPrefix = typeof(RankedSystem).GetMethod(nameof(PracticePhaseSetPrefix), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
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

                            var setPhaseMethod = t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                                .FirstOrDefault(candidate => string.Equals(candidate.Name, "Server_SetPhase", StringComparison.Ordinal) && candidate.GetParameters().Length >= 1);

                            var h = new Harmony(Constants.MOD_NAME + ".gamestate");
                            h.Patch(method, postfix: new HarmonyLib.HarmonyMethod(postfix));
                            if (setPhaseMethod != null && practiceExpiryPrefix != null)
                            {
                                h.Patch(setPhaseMethod, prefix: new HarmonyLib.HarmonyMethod(practiceExpiryPrefix));
                                Debug.Log($"[{Constants.MOD_NAME}] Practice expiry hook aplicado: {t.FullName}.Server_SetPhase");
                            }

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

        private static void TryPatchReplayDebugHooks()
        {
            if (replayDebugHooksPatched) return;
            try
            {
                var replayPlayerType = FindTypeByName("ReplayPlayer", "Puck.ReplayPlayer");
                if (replayPlayerType == null)
                {
                    replayDebugHooksPatched = true;
                    return;
                }

                var startPostfix = typeof(RankedSystem).GetMethod(nameof(ReplayStartPostfix), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var replayEventPrefix = typeof(RankedSystem).GetMethod(nameof(ReplayEventPrefix), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var harmony = new Harmony(Constants.MOD_NAME + ".replaydebug");

                var startMethod = replayPlayerType.GetMethod("Server_StartReplay", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (startMethod != null && startPostfix != null)
                {
                    harmony.Patch(startMethod, postfix: new HarmonyMethod(startPostfix));
                    Debug.Log($"[{Constants.MOD_NAME}] Replay debug hook applied: {replayPlayerType.FullName}.Server_StartReplay");
                }

                var replayEventMethod = replayPlayerType.GetMethod("Server_ReplayEvent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (replayEventMethod != null && replayEventPrefix != null)
                {
                    harmony.Patch(replayEventMethod, prefix: new HarmonyMethod(replayEventPrefix));
                    Debug.Log($"[{Constants.MOD_NAME}] Replay debug hook applied: {replayPlayerType.FullName}.Server_ReplayEvent");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Replay debug hook failed: {ex.Message}");
            }
            finally
            {
                replayDebugHooksPatched = true;
            }
        }

        private static void ReplayStartPostfix(object __0, int __1, int __2)
        {
            try
            {
                var tickCount = 0;
                if (__0 != null)
                {
                    var countProperty = __0.GetType().GetProperty("Count", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (countProperty != null)
                    {
                        TryConvertToInt(countProperty.GetValue(__0), out tickCount);
                    }
                }

                Debug.Log($"[{Constants.MOD_NAME}] [REPLAY-DEBUG] StartReplay phase={GetTrackedPhaseName()} tickRate={__1} fromTick={__2} tickCount={tickCount}");
            }
            catch { }
        }

        private static bool ReplayEventPrefix(string __0, object __1)
        {
            try
            {
                if (string.Equals(__0, "PlayerSpawned", StringComparison.Ordinal))
                {
                    var ownerClientId = TryReadReplayEventOwnerClientId(__1);
                    Debug.Log($"[{Constants.MOD_NAME}] [REPLAY-DEBUG] PlayerSpawned sourceClientId={ownerClientId} replayClientId={(ownerClientId != 0 ? ownerClientId + ReplayClientIdOffset : 0UL)} phase={GetTrackedPhaseName()}");
                    return true;
                }

                if (!string.Equals(__0, "PlayerBodySpawned", StringComparison.Ordinal))
                {
                    return true;
                }

                var ownerId = TryReadReplayEventOwnerClientId(__1);
                var replayUsername = TryReadReplayEventString(__1, "Username");
                TryReadReplayEventInt(__1, "Number", out var replayNumber);
                var replayTeam = TryReadReplayEventValue(__1, "Team");

                object replayPlayer = null;
                if (ownerId != 0 && TryGetPlayerManager(out var manager) && manager != null)
                {
                    var method = manager.GetType().GetMethod("GetReplayPlayerByClientId", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (method != null)
                    {
                        replayPlayer = method.Invoke(manager, new object[] { ownerId });
                    }
                }

                var resolvedReplayClientId = 0UL;
                TryGetClientId(replayPlayer, out resolvedReplayClientId);
                TryGetPlayerNumber(replayPlayer, out var currentNumber);
                var currentTeam = GetCurrentTeamValue(replayPlayer);
                var currentSteamId = TryGetPlayerIdNoFallback(replayPlayer);
                var replayFlag = replayPlayer != null && HasReplayFlag(replayPlayer);

                Debug.Log(
                    $"[{Constants.MOD_NAME}] [REPLAY-DEBUG] PlayerBodySpawned phase={GetTrackedPhaseName()} sourceClientId={ownerId} replayClientId={resolvedReplayClientId} resolvedReplay={replayPlayer != null} replayFlag={replayFlag} username={replayUsername ?? "?"} number={replayNumber} team={FormatTeamValue(replayTeam)} currentNumber={currentNumber} currentTeam={FormatTeamValue(currentTeam)} steamId={currentSteamId ?? "?"}");
            }
            catch { }

            return true;
        }

        private static ulong TryReadReplayEventOwnerClientId(object eventData)
        {
            try
            {
                var value = TryReadReplayEventValue(eventData, "OwnerClientId");
                if (TryConvertToUlong(value, out var clientId))
                {
                    return clientId;
                }
            }
            catch { }

            return 0;
        }

        private static string TryReadReplayEventString(object eventData, string memberName)
        {
            try
            {
                return ExtractSimpleValueToString(TryReadReplayEventValue(eventData, memberName));
            }
            catch { }

            return null;
        }

        private static bool TryReadReplayEventInt(object eventData, string memberName, out int value)
        {
            value = 0;
            try
            {
                return TryConvertToInt(TryReadReplayEventValue(eventData, memberName), out value);
            }
            catch { }

            return false;
        }

        private static object TryReadReplayEventValue(object eventData, string memberName)
        {
            if (eventData == null || string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            try
            {
                var type = eventData.GetType();
                var property = type.GetProperty(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (property != null)
                {
                    return property.GetValue(eventData);
                }

                var field = type.GetField(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    return field.GetValue(eventData);
                }
            }
            catch { }

            return null;
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
                if (rankedActive && !draftActive && !draftTeamLockActive)
                {
                    return true;
                }
                ulong clientId = 0;
                if (__0 is Dictionary<string, object> dict && dict.ContainsKey("clientId"))
                {
                    try { clientId = Convert.ToUInt64(dict["clientId"]); } catch { }
                }
                if (clientId != 0)
                {
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, "Team changes are locked during active matches.", ChatTone.Warning, 13), clientId);
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
                if (!active)
                {
                    return true;
                }

                if (!rankedActive || draftActive || draftTeamLockActive)
                {
                    return true;
                }

                ulong clientId = 0;
                object player = null;
                if (__0 is Dictionary<string, object> dict && dict.ContainsKey("clientId"))
                {
                    try { clientId = Convert.ToUInt64(dict["clientId"]); } catch { }
                    try { player = TryGetPlayerFromDict(dict); } catch { }
                }

                if (player == null && clientId != 0 && TryGetPlayerByClientId(clientId, out var resolvedPlayer))
                {
                    player = resolvedPlayer;
                }

                var currentTeam = GetCurrentTeamValue(player);
                if (IsTeamNoneLike(currentTeam))
                {
                    return true;
                }

                var playerKey = TryGetPlayerIdNoFallback(player);
                if (string.IsNullOrWhiteSpace(playerKey))
                {
                    playerKey = TryGetPlayerId(player, clientId);
                }

                Debug.Log($"[{Constants.MOD_NAME}] SwitchTeamMenuPrefix rerouting active player to TeamSelect. clientId={clientId} current={FormatTeamValue(currentTeam)}");
                TryOpenPlayerTeamSelection(playerKey, clientId);
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

                if (ShouldBypassReplayTeamHooks(playerObj, clientId, steamId))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] Event_OnPlayerTeamChanged bypassed for replay/transient player during replay phase={GetTrackedPhaseName()} clientId={clientId} replayPlayer={IsReplayPlayerObject(playerObj, clientId)}");
                    return true;
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
                    if (ShouldBypassReplayTeamHooks(player, clientId, playerKey))
                    {
                        Debug.Log($"[{Constants.MOD_NAME}] PlayerSelectTeamPrefix bypassed for replay/transient player during replay phase={GetTrackedPhaseName()} clientId={clientId}");
                        return true;
                    }

                    if (ShouldIgnoreTransientTeamHookPlayer(player, clientId, playerKey))
                    {
                        return true;
                    }

                var currentTeam = GetCurrentTeamValue(player);

                if (TryRejectTeamSelectionForMandatoryVerification(player, clientId, requestedTeam, "team-select-click"))
                {
                    return false;
                }

                Debug.Log($"[{Constants.MOD_NAME}] Event_Client_OnPlayerSelectTeam protectActive={protectActive} clientId={clientId} current={FormatTeamValue(currentTeam)} requested={FormatTeamValue(requestedTeam)}");
                if (IsReplayPlaybackPhaseActive())
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [JOIN-DEBUG] Replay-phase team select kept on approval path for live player clientId={clientId} current={FormatTeamValue(currentTeam)} requested={FormatTeamValue(requestedTeam)}");
                }

                if (IsInternalTeamAssignmentAllowed(player, clientId, requestedTeam, false))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] PlayerSelectTeamPrefix: allowing internal assignment {FormatTeamValue(currentTeam)} -> {FormatTeamValue(requestedTeam)}");
                    return true;
                }

                if (rankedActive && !draftActive && !draftTeamLockActive)
                {
                    if (TryHandleTeamSelectionRequest(player, clientId, currentTeam, requestedTeam))
                    {
                        return false;
                    }
                }

                // Selecting same team: do nothing and avoid side effects.
                if (AreTeamValuesEqual(currentTeam, requestedTeam))
                {
                    TryHandleSameTeamPositionSelectionRequest(player, currentTeam, requestedTeam);
                    Debug.Log($"[{Constants.MOD_NAME}] PlayerSelectTeamPrefix: same team selected, skipping.");
                    return false;
                }

                if (!protectActive) return true;

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

                    ulong replayClientId = 0;
                    TryGetClientId(__instance, out replayClientId);

                    var playerKey = TryGetPlayerIdNoFallback(__instance);
                    if (ShouldBypassReplayTeamHooks(__instance, replayClientId, playerKey))
                    {
                        Debug.Log($"[{Constants.MOD_NAME}] PlayerTeamSetPrefix bypassed for replay/transient player during replay phase={GetTrackedPhaseName()} clientId={replayClientId} requested={FormatTeamValue(__0)} replayPlayer={IsReplayPlayerObject(__instance, replayClientId)}");
                        return true;
                    }

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

                if (rankedActive && !draftActive && !draftTeamLockActive)
                {
                    if (TryHandleTeamSelectionRequest(__instance, 0UL, currentTeam, requestedTeam))
                    {
                        return false;
                    }
                }

                if (AreTeamValuesEqual(currentTeam, requestedTeam))
                {
                    TryHandleSameTeamPositionSelectionRequest(__instance, currentTeam, requestedTeam);
                    // Same team selected: do nothing (avoid double-switch side effects)
                    Debug.Log($"[{Constants.MOD_NAME}] PlayerTeamSetPrefix: same team selected ({FormatTeamValue(currentTeam)}), skipping.");
                    return false;
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

        private static bool TryGetMemberValue(object instance, string memberName, out object value)
        {
            value = null;
            try
            {
                if (instance == null || string.IsNullOrWhiteSpace(memberName))
                {
                    return false;
                }

                var type = instance.GetType();
                var property = type.GetProperty(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (property != null)
                {
                    value = property.GetValue(instance, null);
                    return true;
                }

                var field = type.GetField(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    value = field.GetValue(instance);
                    return true;
                }
            }
            catch { }

            value = null;
            return false;
        }

        private static bool TryConvertToIntValue(object value, out int parsed)
        {
            parsed = 0;
            try
            {
                if (value == null)
                {
                    return false;
                }

                if (value is int intValue)
                {
                    parsed = intValue;
                    return true;
                }

                if (value is IConvertible)
                {
                    parsed = Convert.ToInt32(value);
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static void GameStateUpdatePostfix(object __instance, object __0, object __1, object __2, object __3, object __4)
        {
            try
            {
                string previousTrackedPhase;
                lock (phaseLock)
                {
                    previousTrackedPhase = lastGamePhaseName;
                }

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

                    var previousNormalized = (previousTrackedPhase ?? string.Empty).Trim().ToLowerInvariant();
                    if (previousNormalized.Contains("replay") && !p.Contains("replay"))
                    {
                        Debug.Log($"[{Constants.MOD_NAME}] [REPLAY-CLEANUP] Replay phase transition detected: {previousTrackedPhase ?? "unknown"} -> {phaseStrTracked ?? "unknown"}. Forcing transient sweep.");
                        TryPurgeReplayLeftovers(force: true, reason: $"phase-transition:{previousTrackedPhase ?? "unknown"}->{phaseStrTracked ?? "unknown"}");
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
                object goalPlayer = null;
                object assistPlayer = null;
                object secondAssistPlayer = null;
                ulong goalPlayerClientId = 0;
                ulong assistPlayerClientId = 0;
                ulong secondAssistPlayerClientId = 0;
                float speedAcrossLine = 0f;
                float highestSpeedSinceStick = 0f;

                if (__args != null && __args.Length >= 6 && !(__args[1] is bool))
                {
                    try { team = ConvertTeamValue(__args[0]); } catch { }
                    goalPlayer = __args[2];
                    assistPlayer = __args[3];
                    secondAssistPlayer = __args[4];

                    if (__args[5] != null)
                    {
                        TryReadFloatMember(__args[5], "Speed", out speedAcrossLine);
                        TryReadFloatMember(__args[5], "ShotSpeed", out highestSpeedSinceStick);
                    }
                }
                else if (__args != null && __args.Length >= 11 && __args[1] is bool)
                {
                    try { team = ConvertTeamValue(__args[0]); } catch { }
                    if (TryConvertToBool(__args[3], out var hasGoalPlayer) && hasGoalPlayer)
                    {
                        TryConvertToUlong(__args[4], out goalPlayerClientId);
                    }

                    if (TryConvertToBool(__args[5], out var hasAssistPlayer) && hasAssistPlayer)
                    {
                        TryConvertToUlong(__args[6], out assistPlayerClientId);
                    }

                    if (TryConvertToBool(__args[7], out var hasSecondAssistPlayer) && hasSecondAssistPlayer)
                    {
                        TryConvertToUlong(__args[8], out secondAssistPlayerClientId);
                    }

                    TryConvertToFloat(__args[9], out speedAcrossLine);
                    TryConvertToFloat(__args[10], out highestSpeedSinceStick);
                }

                lock (goalLock)
                {
                    if (team == TeamResult.Red) currentRedGoals++;
                    else if (team == TeamResult.Blue) currentBlueGoals++;
                }

                TryResolveTrackedStatParticipant(goalPlayer, goalPlayerClientId, out var goalKey, out var goalName);
                TryResolveTrackedStatParticipant(assistPlayer, assistPlayerClientId, out var assistKey, out var assistName);
                TryResolveTrackedStatParticipant(secondAssistPlayer, secondAssistPlayerClientId, out var secondAssistKey, out var secondAssistName);

                var goalTotal = 0;
                if (!string.IsNullOrWhiteSpace(goalKey))
                {
                    lock (playerGoalLock)
                    {
                        IncrementTrackedStatCount(playerGoalCounts, goalKey);
                        goalTotal = playerGoalCounts[goalKey];
                    }
                }

                var primaryAssistTotal = 0;
                if (!string.IsNullOrWhiteSpace(assistKey))
                {
                    lock (playerAssistLock)
                    {
                        IncrementTrackedStatCount(playerPrimaryAssistCounts, assistKey);
                        primaryAssistTotal = playerPrimaryAssistCounts[assistKey];
                    }
                }

                var secondaryAssistTotal = 0;
                if (!string.IsNullOrWhiteSpace(secondAssistKey))
                {
                    lock (playerAssistLock)
                    {
                        IncrementTrackedStatCount(playerSecondaryAssistCounts, secondAssistKey);
                        secondaryAssistTotal = playerSecondaryAssistCounts[secondAssistKey];
                    }
                }

                Debug.Log($"[{Constants.MOD_NAME}] [STATS] Goal event detected. team={team} redGoals={currentRedGoals} blueGoals={currentBlueGoals} scorer={goalName ?? "none"} scorerKey={goalKey ?? "none"} scorerGoals={goalTotal} primaryAssist={assistName ?? "none"} primaryAssistKey={assistKey ?? "none"} primaryAssistTotal={primaryAssistTotal} secondaryAssist={secondAssistName ?? "none"} secondaryAssistKey={secondAssistKey ?? "none"} secondaryAssistTotal={secondaryAssistTotal} speedAcrossLine={speedAcrossLine:0.00} highestSpeedSinceStick={highestSpeedSinceStick:0.00}");
            }
            catch (Exception ex)
            {
                try { Debug.LogError($"[{Constants.MOD_NAME}] GoalScoredPostfix error: {ex.Message}"); } catch { }
            }
        }

        private static void IncrementTrackedStatCount(Dictionary<string, int> counts, string key)
        {
            if (counts == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (!counts.TryGetValue(key, out var currentValue))
            {
                currentValue = 0;
            }

            counts[key] = currentValue + 1;
        }

        private static bool TryResolveTrackedStatParticipant(object player, ulong fallbackClientId, out string key, out string displayName)
        {
            key = null;
            displayName = null;

            try
            {
                var effectivePlayer = player;
                if (effectivePlayer == null && fallbackClientId != 0)
                {
                    TryGetPlayerByClientId(fallbackClientId, out effectivePlayer);
                }

                if (effectivePlayer != null)
                {
                    displayName = StripRichTextTags(TryGetPlayerName(effectivePlayer))?.Trim();
                    key = NormalizeResolvedPlayerKey(ResolvePlayerObjectKey(effectivePlayer, fallbackClientId));
                }

                if (string.Equals(key, "clientId:0", StringComparison.OrdinalIgnoreCase))
                {
                    key = null;
                }

                if (string.IsNullOrWhiteSpace(key) && fallbackClientId != 0)
                {
                    var resolvedClientKey = ResolveStoredIdToSteam($"clientId:{fallbackClientId}");
                    key = string.IsNullOrWhiteSpace(resolvedClientKey)
                        ? $"clientId:{fallbackClientId}"
                        : resolvedClientKey;
                }

                if (string.IsNullOrWhiteSpace(displayName) && fallbackClientId != 0)
                {
                    displayName = $"clientId:{fallbackClientId}";
                }

                return !string.IsNullOrWhiteSpace(key) || !string.IsNullOrWhiteSpace(displayName);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadFloatMember(object instance, string memberName, out float value)
        {
            value = 0f;
            if (instance == null || string.IsNullOrWhiteSpace(memberName))
            {
                return false;
            }

            return TryGetEntryMemberValue(instance, memberName, out var memberValue)
                && TryConvertToFloat(memberValue, out value);
        }

        private static bool TryConvertToFloat(object value, out float result)
        {
            result = 0f;

            try
            {
                if (value == null)
                {
                    return false;
                }

                if (value is float floatValue)
                {
                    result = floatValue;
                    return true;
                }

                if (value is double doubleValue)
                {
                    result = (float)doubleValue;
                    return true;
                }

                if (value is int intValue)
                {
                    result = intValue;
                    return true;
                }

                if (value is long longValue)
                {
                    result = longValue;
                    return true;
                }

                var stringValue = ExtractSimpleValueToString(value);
                return !string.IsNullOrWhiteSpace(stringValue)
                    && float.TryParse(stringValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result);
            }
            catch
            {
                return false;
            }
        }

        private static void RankedMatchEndPostfix(object __instance)
        {
            try
            {
                if (!rankedActive) return;
                if (Time.unscaledTime - lastRankedEndTime < 1f) return;

                TeamResult winner = TeamResult.Unknown;
                
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
                }

                EndMatch(winner, requestRuntimeEnd: false, forceRequestedWinner: true);
            }
            catch { }
        }

    }
}
