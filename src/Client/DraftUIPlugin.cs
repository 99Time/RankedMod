using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace schrader
{
    public class DraftUIPlugin : IPuckMod
    {
        public static string MOD_NAME = Constants.MOD_NAME;
        public static string MOD_VERSION = "1.0.0";
        public static string MOD_GUID = Constants.MOD_NAME + ".client";

        private static readonly Harmony harmony = new Harmony(MOD_GUID);

        public bool OnEnable()
        {
            DraftUIPlugin.Log("Enabling...");
            try
            {
                if (IsDedicatedServer())
                {
                    DraftUIPlugin.Log("Environment: dedicated server.");
                    DraftUIPlugin.Log("This mod is designed to be used only on clients!");
                }
                else
                {
                    DraftUIPlugin.Log("Environment: client.");

                    if (!DraftUIManager.ValidateRuntimeMethods())
                    {
                        DraftUIPlugin.LogError("Failed to Enable: runtime validation failed!");
                        return false;
                    }

                    DraftUIPlugin.Log("Patching methods...");
                    harmony.PatchAll();
                    TrainingClientRuntime.Initialize();
                    DraftUIPlugin.Log("All patched! Patched methods:");
                    LogAllPatchedMethods();

                    var patchCount = harmony.GetPatchedMethods()
                        .Select(method => new { method, info = Harmony.GetPatchInfo(method) })
                        .Count(x =>
                            x.info.Prefixes.Any(p => p.owner == harmony.Id) ||
                            x.info.Postfixes.Any(p => p.owner == harmony.Id) ||
                            x.info.Transpilers.Any(p => p.owner == harmony.Id) ||
                            x.info.Finalizers.Any(p => p.owner == harmony.Id));

                    if (patchCount < 4)
                    {
                        DraftUIPlugin.LogError($"Failed to Enable: expected at least 4 patches but found {patchCount}!");
                        harmony.UnpatchSelf();
                        return false;
                    }
                }

                DraftUIPlugin.Log("Enabled!");
                return true;
            }
            catch (Exception e)
            {
                DraftUIPlugin.LogError($"Failed to Enable: {e.Message}!");
                DraftUIPlugin.LogError(e.ToString());
                return false;
            }
        }

        public bool OnDisable()
        {
            try
            {
                DraftUIPlugin.Log("Disabling...");
                harmony.UnpatchSelf();
                TrainingClientRuntime.Shutdown();
                DraftUIManager.Shutdown();
                DraftUIPlugin.Log("Disabled! Goodbye!");
                return true;
            }
            catch (Exception e)
            {
                DraftUIPlugin.LogError($"Failed to disable: {e.Message}!");
                return false;
            }
        }

        public static bool IsDedicatedServer()
        {
            return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || Application.isBatchMode;
        }

        public static void LogAllPatchedMethods()
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
                DraftUIPlugin.Log($" - {m.DeclaringType.FullName}.{m.Name}");
            }
        }

        public static void Log(string message)
        {
            Debug.Log($"[{MOD_NAME}] {message}");
        }

        public static void LogError(string message)
        {
            Debug.LogError($"[{MOD_NAME}] {message}");
        }
    }
}