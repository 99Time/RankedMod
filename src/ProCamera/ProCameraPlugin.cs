using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProCameraMod
{
    public sealed class ProCameraPlugin : IPuckMod
    {
        internal const string ModName = "ProCamera";
        internal const string ModVersion = "0.1.0";
        internal const string ModGuid = "schrader.procamera";

        private static readonly Harmony harmony = new Harmony(ModGuid);

        public bool OnEnable()
        {
            Log($"Enabling v{ModVersion}...");

            try
            {
                if (IsDedicatedServer())
                {
                    Log("Detected dedicated server environment. ProCamera stays inactive here.");
                    return true;
                }

                if (!TryPatchRuntime())
                {
                    LogError("Runtime validation failed.");
                    return false;
                }

                ProCameraRuntime.Reset();
                LogPatchedMethods();
                Log("Enabled.");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to enable: {ex}");
                LogError(ex.ToString());
                return false;
            }
        }

        public bool OnDisable()
        {
            try
            {
                Log("Disabling...");
                ProCameraRuntime.Reset();
                harmony.UnpatchSelf();
                Log("Disabled.");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to disable: {ex}");
                LogError(ex.ToString());
                return false;
            }
        }

        private static bool TryPatchRuntime()
        {
            var playerCameraEnable = AccessTools.DeclaredMethod(typeof(PlayerCamera), "Enable", Type.EmptyTypes);
            var playerCameraDisable = AccessTools.DeclaredMethod(typeof(PlayerCamera), "Disable", Type.EmptyTypes);
            var playerCameraOnTick = AccessTools.DeclaredMethod(typeof(PlayerCamera), "OnTick", new[] { typeof(float) });

            var runtimeType = typeof(ProCameraRuntime);
            var enablePostfix = AccessTools.DeclaredMethod(runtimeType, nameof(ProCameraRuntime.OnPlayerCameraEnabled));
            var disablePrefix = AccessTools.DeclaredMethod(runtimeType, nameof(ProCameraRuntime.OnPlayerCameraDisabled));
            var tickPostfix = AccessTools.DeclaredMethod(runtimeType, nameof(ProCameraRuntime.OnPlayerCameraTick));

            var isValid = true;
            isValid &= LogMissingMember(playerCameraEnable, "PlayerCamera.Enable()");
            isValid &= LogMissingMember(playerCameraDisable, "PlayerCamera.Disable()");
            isValid &= LogMissingMember(playerCameraOnTick, "PlayerCamera.OnTick(float)");
            isValid &= LogMissingMember(enablePostfix, "ProCameraRuntime.OnPlayerCameraEnabled(PlayerCamera)");
            isValid &= LogMissingMember(disablePrefix, "ProCameraRuntime.OnPlayerCameraDisabled(PlayerCamera)");
            isValid &= LogMissingMember(tickPostfix, "ProCameraRuntime.OnPlayerCameraTick(PlayerCamera, float)");

            if (!isValid)
            {
                return false;
            }

            harmony.Patch(playerCameraEnable, postfix: new HarmonyMethod(enablePostfix));
            harmony.Patch(playerCameraDisable, prefix: new HarmonyMethod(disablePrefix));
            harmony.Patch(playerCameraOnTick, postfix: new HarmonyMethod(tickPostfix));
            return true;
        }

        private static bool LogMissingMember(MemberInfo member, string label)
        {
            if (member != null)
            {
                return true;
            }

            LogError($"Missing runtime member: {label}");
            return false;
        }

        private static void LogPatchedMethods()
        {
            var methods = harmony.GetPatchedMethods()
                .Select(method => new { method, info = Harmony.GetPatchInfo(method) })
                .Where(item =>
                    item.info.Prefixes.Any(p => p.owner == harmony.Id) ||
                    item.info.Postfixes.Any(p => p.owner == harmony.Id) ||
                    item.info.Transpilers.Any(p => p.owner == harmony.Id) ||
                    item.info.Finalizers.Any(p => p.owner == harmony.Id))
                .Select(item => item.method)
                .ToArray();

            foreach (var method in methods)
            {
                Log($"Patched {method.DeclaringType?.FullName}.{method.Name}");
            }
        }

        internal static bool IsDedicatedServer()
        {
            return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || Application.isBatchMode;
        }

        internal static void Log(string message)
        {
            Debug.Log($"[{ModName}] {message}");
        }

        internal static void LogError(string message)
        {
            Debug.LogError($"[{ModName}] {message}");
        }
    }
}