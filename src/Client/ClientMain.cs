using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace schrader
{
    public class ClientMain : IPuckMod
    {
        private static readonly Harmony harmony = new Harmony(Constants.MOD_NAME + ".client");
        private static GameObject bootstrapGo;
        private static ClientBootstrapBehaviour bootstrapBehaviour;

        private sealed class ClientBootstrapBehaviour : MonoBehaviour
        {
        }

        public static bool IsDedicatedServer()
        {
            return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
        }

        private static void Log(string message)
        {
            Debug.Log($"[{Constants.MOD_NAME}] {message}");
        }

        private static void LogError(string message)
        {
            Debug.LogError($"[{Constants.MOD_NAME}] {message}");
        }

        private static void LogAllPatchedMethods()
        {
            var allPatchedMethods = harmony.GetPatchedMethods();
            var pluginId = harmony.Id;

            var mine = allPatchedMethods
                .Select(m => new { method = m, info = Harmony.GetPatchInfo(m) })
                .Where(x =>
                    x.info.Prefixes.Any(p => p.owner == pluginId) ||
                    x.info.Postfixes.Any(p => p.owner == pluginId) ||
                    x.info.Transpilers.Any(p => p.owner == pluginId) ||
                    x.info.Finalizers.Any(p => p.owner == pluginId))
                .Select(x => x.method);

            foreach (var m in mine)
            {
                Log($" - {m.DeclaringType.FullName}.{m.Name}");
            }
        }

        private static void EnsureBootstrapObject()
        {
            if (bootstrapBehaviour != null) return;

            bootstrapGo = new GameObject($"{Constants.MOD_NAME}.ClientMain");
            UnityEngine.Object.DontDestroyOnLoad(bootstrapGo);
            bootstrapBehaviour = bootstrapGo.AddComponent<ClientBootstrapBehaviour>();
        }

        public bool OnEnable()
        {
            try
            {
                Log("Enabling...");

                if (IsDedicatedServer() || Application.isBatchMode)
                {
                    Log("Environment: dedicated server.");
                    Log("This mod is designed to be used only on clients!");
                    return true;
                }

                Log("Environment: client.");
                Log("VOICECHAT PATTERN INIT");
                Log("CLIENT UI INIT");
                EnsureBootstrapObject();

                DraftUIController.Initialize();

                Log("Patching methods...");
                harmony.PatchAll();
                Log("All patched! Patched methods:");
                LogAllPatchedMethods();

                Log("Enabled!");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to Enable: {ex.Message}!");
                return false;
            }
        }

        public bool OnDisable()
        {
            try
            {
                Log("Disabling...");
                harmony.UnpatchSelf();

                if (bootstrapGo != null)
                {
                    UnityEngine.Object.Destroy(bootstrapGo);
                    bootstrapGo = null;
                    bootstrapBehaviour = null;
                }

                Log("Disabled! Goodbye!");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to disable: {ex.Message}!");
                return false;
            }
        }
    }
}
