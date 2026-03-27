using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using Unity.Netcode;
using Newtonsoft.Json;

namespace schrader
{
    public class CustomMOTD : IPuckMod
    {
        static readonly Harmony harmony = new Harmony(Constants.MOD_NAME);
        static string motd = "<size=18><color=#66ccff>Welcome to SpeedRankeds server!</color></size>\n<size=12>Type <b>/commands</b> to see available commands.</size>";
        private static GameObject updaterGo;
        private static RankedSystemUpdater updaterInstance;
        private static bool allowRankedWithoutPlayers = true; // testing override: allow starting ranked even if not enough players

        [HarmonyPatch(typeof(UIChatController), "Event_Server_OnSynchronizeComplete")]
        public class UIChatControllerPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(Dictionary<string, object> message)
            {
                ulong clientId = (ulong)message["clientId"];

                UIChat.Instance.Server_SendSystemChatMessage(motd, clientId);
                return false;
            }
        }

        // Server-side only: use chat command `/s` to spawn a networked puck.

        [HarmonyPatch(typeof(UIChat), "Server_ProcessPlayerChatMessage")]
        public class UIChatServerProcessChatPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(object player, string message, ulong clientId, bool useTeamChat, bool isMuted)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(message)) return true;
                    var trimmed = message.Trim();

                    if (Server.RankedSystem.TryHandleDraftCommand(player, clientId, trimmed))
                    {
                        return false;
                    }

                    if (trimmed.Equals("/ff", StringComparison.OrdinalIgnoreCase))
                    {
                        try { Server.RankedSystem.HandleForfeitVote(player, clientId); } catch { }
                        return false;
                    }

                    if (trimmed.Equals("/vr", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleRankedVoteStart(player, clientId);
                        return false;
                    }

                    if (trimmed.Equals("/y", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Server.RankedSystem.IsForfeitActive())
                            Server.RankedSystem.HandleForfeitVoteResponse(player, clientId, true);
                        else
                            HandleRankedVoteResponse(player, clientId, true);
                        return false;
                    }

                    if (trimmed.Equals("/n", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Server.RankedSystem.IsForfeitActive())
                            Server.RankedSystem.HandleForfeitVoteResponse(player, clientId, false);
                        else
                            HandleRankedVoteResponse(player, clientId, false);
                        return false;
                    }

                    if (trimmed.Equals("/mmr", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var pid = TryGetPlayerId(player, clientId);
                            var mmr = GetMmr(pid);
                            UIChat.Instance.Server_SendSystemChatMessage($"<size=14>Your MMR: <b>{mmr}</b></size>", clientId);
                        }
                        catch { }
                        return false;
                    }

                    if (trimmed.Equals("/commands", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            UIChat.Instance.Server_SendSystemChatMessage("<size=15><b>Server Commands</b></size>", clientId);
                            UIChat.Instance.Server_SendSystemChatMessage("<size=13>/s</size> <size=12>- Spawn a puck (server). Blocked during matches, goals and replays.", clientId);
                            UIChat.Instance.Server_SendSystemChatMessage("<size=13>/vr</size> <size=12>- Start a Ranked vote. Use /y or /n to vote.", clientId);
                            UIChat.Instance.Server_SendSystemChatMessage("<size=13>/y</size> <size=12>- Vote yes in a Ranked vote.", clientId);
                            UIChat.Instance.Server_SendSystemChatMessage("<size=13>/n</size> <size=12>- Vote no in a Ranked vote.", clientId);
                            UIChat.Instance.Server_SendSystemChatMessage("<size=13>/mmr</size> <size=12>- Show your current MMR.", clientId);
                            UIChat.Instance.Server_SendSystemChatMessage("<size=13>/cs</size> <size=12>- Despawn all pucks on the map.", clientId);
                            UIChat.Instance.Server_SendSystemChatMessage("<size=13>/ranked start|end</size> <size=12>- Admin commands to force start or end a ranked match.", clientId);
                            UIChat.Instance.Server_SendSystemChatMessage("<size=13>/ff</size> <size=12>- Start/vote forfeit (surrender) for your team.", clientId);
                            UIChat.Instance.Server_SendSystemChatMessage("<size=13>Draft UI</size> <size=12>- During ranked draft, open the scoreboard and click a player row to pick or accept them.</size>", clientId);
                            UIChat.Instance.Server_SendSystemChatMessage("<size=13>/pick &lt;player&gt;</size> <size=12>- Chat fallback for captains if scoreboard click is unavailable.</size>", clientId);
                            UIChat.Instance.Server_SendSystemChatMessage("<size=13>/accept &lt;player&gt;</size> <size=12>- Chat fallback to approve a late joiner into your team.</size>", clientId);
                            UIChat.Instance.Server_SendSystemChatMessage("<size=13>/draft</size> <size=12>- Show current draft status.</size>", clientId);
                            UIChat.Instance.Server_SendSystemChatMessage("<size=13>/draftui</size> <size=12>- Toggle the Unity draft overlay on or off. Without an active draft it opens test mode locally.</size>", clientId);
                            UIChat.Instance.Server_SendSystemChatMessage("<size=13>/dummy &lt;count&gt;</size> <size=12>- Create logical test dummies for the next draft or as late joiners in an active ranked.</size>", clientId);
                        }
                        catch { }
                        return false;
                    }

                    if (trimmed.Equals("/cs", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var pmTypeDespawn = FindTypeByName("PuckManager", "Puck.PuckManager");
                            if (pmTypeDespawn != null)
                            {
                                var pmInstance = GetManagerInstance(pmTypeDespawn);
                                if (pmInstance != null)
                                {
                                    var method = pmTypeDespawn.GetMethod("Server_DespawnPucks", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                                    if (method != null)
                                    {
                                        // call with true to force/despawn network objects
                                        method.Invoke(pmInstance, new object[] { true });
                                        UIChat.Instance.Server_SendSystemChatMessage("<size=14><color=#00ff00>All pucks despawned.</color></size>", clientId);
                                        return false;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[{Constants.MOD_NAME}] /cs failed: {ex.Message}");
                        }
                        return false;
                    }

                        // Admin-style ranked controls: /ranked start | /ranked end <red|blue|draw>
                        if (trimmed.StartsWith("/ranked", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = trimmed.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                var sub = parts[1].ToLowerInvariant();
                                if (sub == "start")
                                {
                                    if (!TryIsAdmin(player, clientId))
                                    {
                                        SendSystemChatToClient("<size=14><color=#ff6666>Permission denied.</color></size>", clientId);
                                        return false;
                                    }
                                        if (!TryGetEligiblePlayers(out var eligible, out var reason))
                                        {
                                            if (allowRankedWithoutPlayers)
                                            {
                                                eligible = new List<RankedParticipant>();
                                                var pid = TryGetPlayerId(player, clientId);
                                                var initiatorName = TryGetPlayerName(player) ?? $"Player {clientId}";
                                                eligible.Add(new RankedParticipant { clientId = clientId, playerId = pid, displayName = initiatorName, team = TeamResult.Red });
                                            }
                                            else
                                            {
                                                SendSystemChatToClient($"<size=14><color=#ff6666>Ranked</color> cannot start: {reason}</size>", clientId);
                                                return false;
                                            }
                                        }

                                        // Delegate forcing a ranked start to the server-ranked system
                                        Server.RankedSystem.ForceStart(eligible);
                                    return false;
                                }
                                else if (sub == "end")
                                {
                                    if (!TryIsAdmin(player, clientId))
                                    {
                                        SendSystemChatToClient("<size=14><color=#ff6666>Permission denied.</color></size>", clientId);
                                        return false;
                                    }

                                        // optional winner argument
                                        if (parts.Length >= 3)
                                        {
                                            var arg = parts[2].ToLowerInvariant();
                                            if (arg == "red" || arg == "r")
                                            {
                                                ApplyRankedResults(TeamResult.Red);
                                                SendSystemChatToAll("<size=14><color=#ffcc66>Ranked</color> forcibly ended by admin. Winner: Red.</size>");
                                                return false;
                                            }
                                            else if (arg == "blue" || arg == "b")
                                            {
                                                ApplyRankedResults(TeamResult.Blue);
                                                SendSystemChatToAll("<size=14><color=#ffcc66>Ranked</color> forcibly ended by admin. Winner: Blue.</size>");
                                                return false;
                                            }
                                            else if (arg == "draw")
                                            {
                                                Server.RankedSystem.ForceEnd();
                                                SendSystemChatToAll("<size=14><color=#ffcc66>Ranked</color> forcibly ended by admin: draw. MMR unchanged.</size>");
                                                return false;
                                            }
                                        }

                                        // no arg -> cancel vote/match
                                        Server.RankedSystem.ForceEnd();
                                        return false;
                                }
                            }

                            SendSystemChatToClient("<size=14>Usage: /ranked start | /ranked end <red|blue|draw></size>", clientId);
                            return false;
                        }

                    // Only handle exact "/s" or "/s <args>". Avoid capturing commands like "/start".
                    if (!(trimmed.Equals("/s", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("/s ", StringComparison.OrdinalIgnoreCase))) return true;

                    // Server-side: spawn puck at player's stick position
                    Vector3 spawnPos = Vector3.zero;
                    Quaternion rot = Quaternion.identity;
                    Vector3 vel = Vector3.zero;

                    var playerComp = player as Component;
                    if (playerComp != null)
                    {
                        if (!TryGetBladeSpawn(playerComp, out spawnPos, out rot, out vel))
                        {
                            spawnPos = playerComp.transform.position + playerComp.transform.forward * 1.5f + Vector3.up * 0.05f;
                            rot = Quaternion.LookRotation(playerComp.transform.forward);
                            vel = playerComp.transform.forward * 5f;
                        }
                    }
                    else
                    {
                        var cam = Camera.main;
                        if (cam != null)
                        {
                            spawnPos = cam.transform.position + cam.transform.forward * 2f;
                            rot = cam.transform.rotation;
                            vel = cam.transform.forward * 5f;
                        }
                    }

                    var pmTypeSpawn = FindTypeByName("PuckManager", "Puck.PuckManager");
                    if (pmTypeSpawn != null)
                    {
                        var pmInstance = GetManagerInstance(pmTypeSpawn);
                        if (pmInstance != null)
                        {
                            var method = pmTypeSpawn.GetMethod("Server_SpawnPuck", BindingFlags.Public | BindingFlags.Instance);
                            if (method != null)
                            {
                                try
                                {
                                    if (Server.RankedSystem.IsMatchActive())
                                    {
                                        try { UIChat.Instance.Server_SendSystemChatMessage("<size=14><color=#ff6666>Cannot spawn pucks during an active match.</color></size>", clientId); } catch { }
                                        return false;
                                    }
                                    using (Server.RankedSystem.BeginManualSpawn())
                                    {
                                        var spawned = method.Invoke(pmInstance, new object[] { spawnPos, rot, vel, false });
                                        Debug.Log($"[{Constants.MOD_NAME}] Spawned puck from /s at {spawnPos}");
                                        // Try to unfreeze the puck or apply initial velocity if needed
                                        if (spawned != null && spawned is Component spawnedComp)
                                        {
                                            try
                                            {
                                                var puckType = spawnedComp.GetType();
                                                var unfreeze = puckType.GetMethod("Server_Unfreeze", BindingFlags.Public | BindingFlags.Instance);
                                                if (unfreeze != null)
                                                {
                                                    unfreeze.Invoke(spawnedComp, null);
                                                }

                                                var rb = spawnedComp.GetComponent<Rigidbody>();
                                                if (rb != null)
                                                {
                                                    rb.linearVelocity = vel;
                                                    rb.WakeUp();
                                                }
                                            }
                                            catch { }
                                        }
                                        try
                                        {
                                            var pname = TryGetPlayerName(player) ?? "Player";
                                            UIChat.Instance.Server_SendSystemChatMessage($"<size=14><b><color=#00ff00>{pname}</color></b> has spawned a puck.</size>", clientId);
                                        }
                                        catch { }
                                        return false;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogError($"[{Constants.MOD_NAME}] spawn invoke failed: {ex}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{Constants.MOD_NAME}] Server_ProcessPlayerChatMessage patch error: {ex.Message}");
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(UIChatController), "Event_Server_OnChatCommand")]
        public class UIChatControllerChatCommandPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(Dictionary<string, object> message)
            {
                try
                {
                    if (message == null) return true;

                    // extract command and clientId
                    string cmd = null;
                    ulong clientId = 0;
                    if (message.ContainsKey("clientId")) { try { clientId = Convert.ToUInt64(message["clientId"]); } catch { } }

                    string[] possibleKeys = new[] { "message", "text", "chatMessage", "command", "msg" };
                    foreach (var k in possibleKeys)
                    {
                        if (message.ContainsKey(k) && message[k] is string s) { cmd = s.Trim(); break; }
                    }
                    if (cmd == null)
                    {
                        foreach (var val in message.Values) { if (val is string sv && sv.TrimStart().StartsWith("/")) { cmd = sv.Trim(); break; } }
                    }
                    if (string.IsNullOrEmpty(cmd)) return true;

                    if (Server.RankedSystem.TryHandleDraftCommand(null, clientId, cmd))
                    {
                        return false;
                    }

                    // /cs -> despawn all pucks
                    if (cmd.Equals("/cs", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var pmType = FindTypeByName("PuckManager", "Puck.PuckManager");
                            if (pmType != null)
                            {
                                var pmInstance = GetManagerInstance(pmType);
                                if (pmInstance != null)
                                {
                                    var method = pmType.GetMethod("Server_DespawnPucks", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                                    if (method != null)
                                    {
                                        method.Invoke(pmInstance, new object[] { true });
                                        try { UIChat.Instance.Server_SendSystemChatMessage("<size=14><color=#00ff00>All pucks despawned.</color></size>", clientId); } catch { }
                                        return false;
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { Debug.LogError($"[{Constants.MOD_NAME}] /cs failed: {ex.Message}"); }
                        return false;
                    }

                    // /s -> spawn puck
                    if (cmd.Equals("/s", StringComparison.OrdinalIgnoreCase) || cmd.StartsWith("/s ", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var pmType = FindTypeByName("PuckManager", "Puck.PuckManager");
                            var playerManagerType = FindTypeByName("PlayerManager", "Puck.PlayerManager");

                            object playerInstance = null;
                            if (playerManagerType != null)
                            {
                                var pmgr = GetManagerInstance(playerManagerType);
                                if (pmgr != null)
                                {
                                    var getPlayer = playerManagerType.GetMethod("GetPlayerByClientId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                                    if (getPlayer != null) playerInstance = getPlayer.Invoke(pmgr, new object[] { clientId });
                                }
                            }

                            Vector3 spawnPos = Vector3.zero; Quaternion rot = Quaternion.identity; Vector3 vel = Vector3.zero;
                            if (playerInstance is Component playerComp)
                            {
                                if (!TryGetBladeSpawn(playerComp, out spawnPos, out rot, out vel))
                                {
                                    spawnPos = playerComp.transform.position + playerComp.transform.forward * 1.5f + Vector3.up * 0.05f;
                                    rot = Quaternion.LookRotation(playerComp.transform.forward);
                                    vel = playerComp.transform.forward * 5f;
                                }
                            }
                            else { var cam = Camera.main; if (cam != null) { spawnPos = cam.transform.position + cam.transform.forward * 2f; rot = cam.transform.rotation; vel = cam.transform.forward * 5f; } }

                            if (pmType != null)
                            {
                                var pmInst = GetManagerInstance(pmType);
                                if (pmInst != null)
                                {
                                    var method = pmType.GetMethod("Server_SpawnPuck", BindingFlags.Public | BindingFlags.Instance);
                                    if (method != null)
                                    {
                                        if (Server.RankedSystem.IsMatchActive())
                                        {
                                            try { UIChat.Instance.Server_SendSystemChatMessage("<size=14><color=#ff6666>Cannot spawn pucks during an active match.</color></size>", clientId); } catch { }
                                            return false;
                                        }
                                        using (Server.RankedSystem.BeginManualSpawn())
                                        {
                                            var spawned = method.Invoke(pmInst, new object[] { spawnPos, rot, vel, false });
                                            if (spawned is Component spawnedComp)
                                            {
                                                try { var unfreeze = spawnedComp.GetType().GetMethod("Server_Unfreeze", BindingFlags.Public | BindingFlags.Instance); if (unfreeze != null) unfreeze.Invoke(spawnedComp, null); }
                                                catch { }
                                                try { var rb = spawnedComp.GetComponent<Rigidbody>(); if (rb != null) { rb.linearVelocity = vel; rb.WakeUp(); } } catch { }
                                            }
                                            try { var pname = TryGetPlayerName(playerInstance) ?? "Player"; UIChat.Instance.Server_SendSystemChatMessage($"<size=14><b><color=#00ff00>{pname}</color></b> has spawned a puck.</size>", clientId); } catch { }
                                            return false;
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { Debug.LogError($"[{Constants.MOD_NAME}] chat spawn failed: {ex.Message}"); }
                        return false;
                    }

                }
                catch (Exception e)
                {
                    Debug.LogError($"[{Constants.MOD_NAME}] ChatCommand patch error: {e.Message}");
                }

                return true;
            }
        }

        public bool OnEnable()
        {
            try
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Enabling...");

                if (Application.isBatchMode)
                {
                    InitializeServer();
                }
                else
                {
                    InitializeClient();
                }

                Debug.Log($"[{Constants.MOD_NAME}] Enabled");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] failed to enable: {e}");
                return false;
            }
        }

        private static void InitializeServer()
        {
            Debug.Log($"[{Constants.MOD_NAME}] SERVER INIT");

            try
            {
                LoadMmr();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Failed to load MMR: {ex.Message}");
            }

            string rootPath = Path.GetFullPath(".");
            string motdPath = Path.Combine(rootPath, "motd.txt");

            if (!File.Exists(motdPath))
                File.WriteAllText(motdPath, motd);
            else
                motd = File.ReadAllText(motdPath);

            try
            {
                harmony.PatchAll();
                Server.RankedSystem.Initialize();
                EnsureUpdater();
            }
            catch (Exception hex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Server initialization failed: {hex}");
                throw;
            }
        }

        private static void InitializeClient()
        {
            Debug.Log($"[{Constants.MOD_NAME}] CLIENT INIT");
            InitializeClientUI();
        }

        private static void InitializeClientUI()
        {
            if (Application.isBatchMode)
            {
                Debug.Log($"[{Constants.MOD_NAME}] Skipping UI: running on server (batch mode)");
                return;
            }

            DraftStateBridge.EnsureInitialized();
            Debug.Log($"[{Constants.MOD_NAME}] UI INIT");
            Debug.Log($"[{Constants.MOD_NAME}] CLIENT UI INITIALIZED");
        }

        public static void SpawnPuck()
        {
            try
            {
                Camera cam = Camera.main;
                Vector3 spawnPos = Vector3.zero;
                Vector3 forward = Vector3.forward;

                if (cam != null)
                {
                    spawnPos = cam.transform.position + cam.transform.forward * 2f;
                    forward = cam.transform.forward;
                }

                // If we're the server, ask the game's PuckManager to spawn a networked puck
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    try
                    {
                        // Try to get the PuckManager type and instance
                        Type pmType = null;
                        object pmInstance = null;

                        try { pmType = Type.GetType("PuckManager"); } catch { pmType = null; }
                        if (pmType == null)
                        {
                            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                pmType = asm.GetType("PuckManager") ?? asm.GetType("Puck.PuckManager");
                                if (pmType != null) break;
                            }
                        }

                            if (pmType != null)
                            {
                                // Try static Instance property first, otherwise find any object of that type
                                var prop = pmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                                if (prop != null)
                                    pmInstance = prop.GetValue(null);

                                if (pmInstance == null)
                                    pmInstance = FindFirstObjectOfType(pmType);

                                if (pmInstance != null)
                                {
                                    var method = pmType.GetMethod("Server_SpawnPuck", BindingFlags.Public | BindingFlags.Instance);
                                    if (method != null)
                                    {
                                        if (Server.RankedSystem.IsMatchActive())
                                        {
                                            Debug.Log($"[{Constants.MOD_NAME}] Manual spawn aborted because a match is already active.");
                                            return;
                                        }
                                        // signature: Puck Server_SpawnPuck(Vector3, Quaternion, Vector3, Boolean)
                                        var rot = Quaternion.LookRotation(forward);
                                        using (Server.RankedSystem.BeginManualSpawn())
                                        {
                                            method.Invoke(pmInstance, new object[] { spawnPos, rot, forward * 5f, false });
                                        }
                                        Debug.Log($"[{Constants.MOD_NAME}] Requested server spawn puck at {spawnPos}");
                                        return;
                                    }
                                }
                        }

                        Debug.LogError($"[{Constants.MOD_NAME}] PuckManager not found or spawn method missing");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[{Constants.MOD_NAME}] Server spawn failed: {ex.Message}");
                    }
                }
                // Fallback: spawn locally for testing when not server
                GameObject puck = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puck.name = "Puck";
                puck.transform.position = spawnPos;
                puck.transform.localScale = Vector3.one * 0.3f;

                var rb = puck.AddComponent<Rigidbody>();
                rb.mass = 0.17f;
                rb.linearVelocity = forward * 5f;
                rb.WakeUp();

                Debug.Log($"[{Constants.MOD_NAME}] Spawned local puck at {spawnPos}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] SpawnPuck failed: {e.Message}");
            }
        }

        public bool OnDisable()
        {
            try
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Disabling...");

                harmony.UnpatchSelf();
                try
                {
                    if (updaterGo != null)
                    {
                        UnityEngine.Object.Destroy(updaterGo);
                        updaterGo = null;
                        updaterInstance = null;
                    }
                }
                catch { }

                try { DraftStateBridge.Shutdown(); } catch { }

                Debug.Log($"[{Constants.MOD_NAME}] Disabled");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] failed to disable: {e.Message}");
                return false;
            }
        }

        // Helper: find a live object of the given type without using the obsolete API.
        private static object FindFirstObjectOfType(Type t)
        {
            try
            {
                var objs = Resources.FindObjectsOfTypeAll(t);
                if (objs != null && objs.Length > 0)
                    return objs[0];
            }
            catch { }
            return null;
        }

        // Helper: get manager instance by checking static Instance property or finding in scene
        private static object GetManagerInstance(Type managerType)
        {
            try { return Server.ReflectionUtils.GetManagerInstance(managerType); } catch { return null; }
        }

        private static bool TryGetBladeSpawn(Component playerComp, out Vector3 spawnPos, out Quaternion rot, out Vector3 vel)
        {
            try { return Server.ReflectionUtils.TryGetBladeSpawn(playerComp, out spawnPos, out rot, out vel); } catch { spawnPos = Vector3.zero; rot = Quaternion.identity; vel = Vector3.zero; return false; }
        }

        private static Type FindTypeByName(params string[] names)
        {
            try { return Server.ReflectionUtils.FindTypeByName(names); } catch { return null; }
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

        private static void EnsureUpdater()
        {
            if (updaterInstance != null) return;
            try
            {
                updaterGo = new GameObject($"{Constants.MOD_NAME}.Updater");
                UnityEngine.Object.DontDestroyOnLoad(updaterGo);
                updaterInstance = updaterGo.AddComponent<RankedSystemUpdater>();
            }
            catch { }
        }

        private class RankedSystemUpdater : MonoBehaviour
        {
            private void Update()
            {
                try { Server.RankedSystem.Update(); } catch { }
            }
        }

        // Delegates to the server-side RankedSystem implementation
        private static void LoadMmr() => Server.RankedSystem.LoadMmr();
        private static void SaveMmr() => Server.RankedSystem.SaveMmr();
        private static void UpdateRankedVote() => Server.RankedSystem.Update();
        private static void HandleRankedVoteStart(object player, ulong clientId) => Server.RankedSystem.HandleRankedVoteStart(player, clientId);
        private static void HandleRankedVoteResponse(object player, ulong clientId, bool accept) => Server.RankedSystem.HandleRankedVoteResponse(player, clientId, accept);
        private static void FinalizeRankedVote() { /* handled by RankedSystem */ }
        private static bool TryHandleDraftCommand(object player, ulong clientId, string message) => Server.RankedSystem.TryHandleDraftCommand(player, clientId, message);
        private static bool TryGetEligiblePlayers(out List<RankedParticipant> eligible, out string reason) => Server.RankedSystem.TryGetEligiblePlayers(out eligible, out reason);
        private static string TryGetPlayerId(object player, ulong fallbackClientId) => Server.RankedSystem.TryGetPlayerId(player, fallbackClientId);
        private static string TryGetPlayerName(object player) => Server.RankedSystem.TryGetPlayerName(player);
        private static int GetMmr(string playerId) => Server.RankedSystem.GetMmr(playerId);
        private static bool TryIsAdmin(object player, ulong clientId) => Server.RankedSystem.TryIsAdminPublic(player, clientId);
        private static void SendSystemChatToAll(string message) => Server.RankedSystem.SendSystemChatToAll(message);
        private static void SendSystemChatToClient(string message, ulong clientId) => Server.RankedSystem.SendSystemChatToClient(message, clientId);
        private static void TryStartMatch() => Server.RankedSystem.TryStartMatch();
        private static void ApplyRankedResults(TeamResult winner) => Server.RankedSystem.ApplyRankedResults(winner);

        // Diagnostic Harmony patch: intercept NetworkVariableBase.CanSend to detect null NetworkBehaviour and avoid crashing the network tick
        [HarmonyPatch]
        public class NetworkVariableCanSendPatch
        {
            static System.Reflection.MethodBase TargetMethod()
            {
                var t = FindTypeByName("Unity.Netcode.NetworkVariableBase", "Unity.Netcode.NetworkVariableBase");
                if (t == null) return null;
                return t.GetMethod("CanSend", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            }

            [HarmonyPrefix]
            public static bool Prefix(object __instance, ref bool __result)
            {
                try
                {
                    if (__instance == null) return true;

                    var nb = __instance;
                    var baseType = nb.GetType();

                    var field = typeof(NetworkVariableBase).GetField("m_NetworkBehaviour", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field == null)
                    {
                        // field not found on this runtime version � don't interfere
                        return true;
                    }

                    var val = field.GetValue(nb);
                    if (val == null)
                    {
                        try
                        {
                            Debug.LogError($"[{Constants.MOD_NAME}] NetworkVariable detected with null m_NetworkBehaviour. Variable type: {baseType.FullName}. Stack:\n{Environment.StackTrace}");
                        }
                        catch { }
                        // prevent original CanSend from running (which would throw); indicate cannot send
                        __result = false;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    try { Debug.LogError($"[{Constants.MOD_NAME}] NetworkVariableCanSendPatch error: {ex}"); } catch { }
                }
                return true;
            }
        }
    }
}
