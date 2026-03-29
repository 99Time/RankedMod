using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace schrader.Server
{
    public static partial class RankedSystem
    {
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
                object player = null;
                if (__0 is Dictionary<string, object> dict && dict.ContainsKey("clientId"))
                {
                    try { clientId = Convert.ToUInt64(dict["clientId"]); } catch { }
                    try { player = TryGetPlayerFromDict(dict); } catch { }
                }

                if (rankedActive && !draftActive && !draftTeamLockActive && TryHandleSwitchTeamMenuRequest(player, clientId))
                {
                    return false;
                }

                if (rankedActive && !draftActive && !draftTeamLockActive)
                {
                    if (player == null && clientId != 0 && TryGetPlayerByClientId(clientId, out var resolvedPlayer)) player = resolvedPlayer;
                    var currentTeam = GetCurrentTeamValue(player);
                    if (IsTeamNoneLike(currentTeam)) return true;
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

                if (rankedActive && !draftActive && !draftTeamLockActive)
                {
                    if (TryHandleTeamSelectionRequest(player, clientId, currentTeam, requestedTeam))
                    {
                        return false;
                    }
                }

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

                if (rankedActive && !draftActive && !draftTeamLockActive)
                {
                    if (TryHandleTeamSelectionRequest(__instance, 0UL, currentTeam, requestedTeam))
                    {
                        return false;
                    }
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

    }
}
