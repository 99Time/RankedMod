using System;
using UnityEngine;

namespace schrader.Server
{
    public static class SpawnManager
    {
        public static void SpawnPuckAt(Vector3 position, Vector3 forward)
        {
            try
            {
                // Prevent spawning pucks while any match (ranked or normal) is active
                try { if (RankedSystem.IsMatchActive()) return; } catch { }

                // Try to call the game's PuckManager via reflection (server-side)
                Type pmType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    pmType = asm.GetType("PuckManager") ?? asm.GetType("Puck.PuckManager");
                    if (pmType != null) break;
                }

                if (pmType == null) return;
                var prop = pmType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                object pmInstance = prop?.GetValue(null);
                if (pmInstance == null)
                    pmInstance = FindFirstObjectOfType(pmType);

                if (pmInstance == null) return;
                var method = pmType.GetMethod("Server_SpawnPuck", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (method == null) return;
                var rot = Quaternion.LookRotation(forward);
                method.Invoke(pmInstance, new object[] { position, rot, forward * 5f, false });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] SpawnManager error: {ex}");
            }
        }

        public static object GetManagerInstance(Type managerType)
        {
            if (managerType == null) return null;
            try
            {
                var prop = managerType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop != null)
                {
                    var val = prop.GetValue(null);
                    if (val != null) return val;
                }
                return FindFirstObjectOfType(managerType);
            }
            catch { }
            return null;
        }

        public static Type FindTypeByName(params string[] names)
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

        public static bool TryGetBladeSpawn(Component playerComp, out Vector3 spawnPos, out Quaternion rot, out Vector3 vel)
        {
            spawnPos = Vector3.zero; rot = Quaternion.identity; vel = Vector3.zero;
            if (playerComp == null) return false;
            try
            {
                var stickPosType = FindTypeByName("StickPositioner", "Puck.StickPositioner");
                if (stickPosType != null)
                {
                    var stickPosComp = playerComp.GetComponentInChildren(stickPosType, true);
                    if (stickPosComp != null)
                    {
                        Vector3 bladePos = Vector3.zero; Vector3 shaftPos = Vector3.zero; Vector3 bladeVel = Vector3.zero;
                        bool hasBladePos = false; bool hasShaftPos = false; bool hasBladeVel = false;

                        var getBladePos = stickPosType.GetMethod("get_BladeTargetPosition", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        var getShaftPos = stickPosType.GetMethod("get_ShaftTargetPosition", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        var getBladeVel = stickPosType.GetMethod("get_BladeTargetVelocity", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                        if (getBladePos != null)
                        {
                            var bp = getBladePos.Invoke(stickPosComp, null);
                            if (bp is Vector3 bpos) { bladePos = bpos; hasBladePos = true; }
                        }

                        if (getShaftPos != null)
                        {
                            var sp = getShaftPos.Invoke(stickPosComp, null);
                            if (sp is Vector3 spos) { shaftPos = spos; hasShaftPos = true; }
                        }

                        if (getBladeVel != null)
                        {
                            var bv = getBladeVel.Invoke(stickPosComp, null);
                            if (bv is Vector3 bvel) { bladeVel = bvel; hasBladeVel = true; }
                        }

                        if (hasBladePos)
                        {
                            var dir = hasShaftPos ? (bladePos - shaftPos) : playerComp.transform.forward;
                            if (dir.sqrMagnitude < 0.0001f) dir = playerComp.transform.forward;
                            dir = dir.normalized;
                            spawnPos = bladePos + Vector3.up * 0.02f + dir * 0.02f;
                            rot = Quaternion.LookRotation(dir);
                            vel = (hasBladeVel && bladeVel.sqrMagnitude > 0.01f) ? bladeVel : dir * 5f;
                            return true;
                        }
                    }
                }

                Transform stick = null;
                foreach (var t in playerComp.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.IndexOf("stick", StringComparison.OrdinalIgnoreCase) >= 0) { stick = t; break; }
                }

                if (stick == null)
                {
                    var stickType = FindTypeByName("Stick", "Puck.Stick");
                    if (stickType != null)
                    {
                        var comps = playerComp.GetComponentsInChildren(stickType, true);
                        if (comps != null && comps.Length > 0) stick = ((Component)comps[0]).transform;
                    }
                }

                if (stick != null)
                {
                    Transform tip = null;
                    foreach (var tt in stick.GetComponentsInChildren<Transform>(true))
                    {
                        var n = tt.name.ToLowerInvariant();
                        if (n.Contains("blade") || n.Contains("tip") || n.Contains("end")) { tip = tt; break; }
                    }

                    var target = tip ?? stick; var dir = target.forward;
                    if (dir.sqrMagnitude < 0.0001f) dir = playerComp.transform.forward;
                    dir = dir.normalized;
                    spawnPos = target.position + Vector3.up * 0.02f + dir * 0.02f;
                    rot = target.rotation;
                    vel = dir * 5f;
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static object FindFirstObjectOfType(Type t)
        {
            try
            {
                var objs = Resources.FindObjectsOfTypeAll(t);
                if (objs != null && objs.Length > 0) return objs[0];
            }
            catch { }
            return null;
        }
    }
}
