using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace schrader.Server
{
    internal static class RankedServerInstanceActivation
    {
        internal const string SpeedRankedsEnabledFlagName = "speedRankedsEnabled";
        internal const ulong SpeedRankedsWorkshopId = 3691658485UL;
        private const string SpeedRankedsWorkshopIdText = "3691658485";

        internal static bool ShouldEnableForCurrentServerInstance()
        {
            if (!Application.isBatchMode)
            {
                Debug.Log($"[{Constants.MOD_NAME}] [SERVER-ACTIVATION] Non-dedicated process detected. Keeping current behaviour.");
                return true;
            }

            try
            {
                if (!TryGetDedicatedServerConfiguration(out var configuration, out var configurationMessage))
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [SERVER-ACTIVATION] {configurationMessage}");
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [SERVER-ACTIVATION] RankedMod inactive for this dedicated server instance (fail-safe disabled).");
                    return false;
                }

                if (!TryResolveEnabledFromConfiguration(configuration, out var enabled, out var resolutionMessage))
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [SERVER-ACTIVATION] {resolutionMessage}");
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [SERVER-ACTIVATION] RankedMod inactive for this dedicated server instance (fail-safe disabled).");
                    return false;
                }

                Debug.Log($"[{Constants.MOD_NAME}] [SERVER-ACTIVATION] {resolutionMessage}");
                Debug.Log($"[{Constants.MOD_NAME}] [SERVER-ACTIVATION] RankedMod {(enabled ? "active" : "inactive")} for this dedicated server instance.");
                return enabled;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] [SERVER-ACTIVATION] Failed to resolve dedicated server activation: {ex}");
                Debug.LogWarning($"[{Constants.MOD_NAME}] [SERVER-ACTIVATION] RankedMod inactive for this dedicated server instance (fail-safe disabled).");
                return false;
            }
        }

        internal static bool TryResolveEnabledFromConfiguration(object configuration, out bool enabled, out string resolutionMessage)
        {
            enabled = false;

            if (configuration == null)
            {
                resolutionMessage = "ServerConfiguration is unavailable.";
                return false;
            }

            if (TryGetMemberValue(configuration, SpeedRankedsEnabledFlagName, out var explicitFlagValue))
            {
                if (TryConvertToBoolean(explicitFlagValue, out enabled))
                {
                    resolutionMessage = $"Detected explicit {SpeedRankedsEnabledFlagName}={enabled} in ServerConfiguration.";
                    return true;
                }

                resolutionMessage = $"Detected explicit {SpeedRankedsEnabledFlagName}, but its value could not be parsed ({DescribeValue(explicitFlagValue)}).";
                return false;
            }

            if (TryResolveEnabledFromMods(configuration, out enabled, out resolutionMessage))
            {
                return true;
            }

            return false;
        }

        private static bool TryResolveEnabledFromMods(object configuration, out bool enabled, out string resolutionMessage)
        {
            enabled = false;

            if (!TryGetMemberValue(configuration, "mods", out var modsValue) || modsValue == null)
            {
                resolutionMessage = $"{SpeedRankedsEnabledFlagName} not present and ServerConfiguration.mods is unavailable.";
                return false;
            }

            var entries = EnumerateEntries(modsValue).ToArray();
            if (entries.Length == 0)
            {
                resolutionMessage = $"{SpeedRankedsEnabledFlagName} not present and ServerConfiguration.mods is empty.";
                return false;
            }

            foreach (var entry in entries)
            {
                if (!TryReadWorkshopId(entry, out var workshopId))
                {
                    continue;
                }

                if (!IsSpeedRankedsWorkshopId(workshopId))
                {
                    continue;
                }

                if (TryGetMemberValue(entry, "enabled", out var enabledValue))
                {
                    if (TryConvertToBoolean(enabledValue, out enabled))
                    {
                        resolutionMessage = $"{SpeedRankedsEnabledFlagName} not present. Fallback matched mods entry workshopId={SpeedRankedsWorkshopIdText} with enabled={enabled}.";
                        return true;
                    }

                    resolutionMessage = $"{SpeedRankedsEnabledFlagName} not present. Fallback matched workshopId={SpeedRankedsWorkshopIdText}, but its enabled value could not be parsed ({DescribeValue(enabledValue)}).";
                    return false;
                }

                enabled = true;
                resolutionMessage = $"{SpeedRankedsEnabledFlagName} not present. Fallback matched legacy mods entry workshopId={SpeedRankedsWorkshopIdText} without enabled field; assuming enabled.";
                return true;
            }

            resolutionMessage = $"{SpeedRankedsEnabledFlagName} not present. Workshop {SpeedRankedsWorkshopIdText} was not found as enabled in ServerConfiguration.mods.";
            return false;
        }

        private static bool TryGetDedicatedServerConfiguration(out object configuration, out string message)
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

            if (!TryGetMemberValue(serverManager, "ServerConfigurationManager", out var configurationManager) || configurationManager == null)
            {
                message = "ServerConfigurationManager is unavailable on ServerManager.";
                return false;
            }

            if (!TryGetMemberValue(configurationManager, "ServerConfiguration", out configuration) || configuration == null)
            {
                message = "ServerConfiguration is unavailable on ServerConfigurationManager.";
                return false;
            }

            message = "Resolved ServerConfiguration from ServerConfigurationManager.ServerConfiguration.";
            return true;
        }

        private static IEnumerable<object> EnumerateEntries(object value)
        {
            if (value == null)
            {
                yield break;
            }

            if (value is JArray jArray)
            {
                foreach (var item in jArray)
                {
                    yield return item;
                }
                yield break;
            }

            if (value is IEnumerable enumerable && !(value is string))
            {
                foreach (var item in enumerable)
                {
                    yield return item;
                }
                yield break;
            }

            yield return value;
        }

        private static bool TryReadWorkshopId(object entry, out string workshopId)
        {
            workshopId = null;

            if (entry == null)
            {
                return false;
            }

            if (TryConvertToWorkshopId(entry, out workshopId))
            {
                return true;
            }

            var candidateNames = new[]
            {
                "workshopId",
                "publishedFileId",
                "fileId",
                "id",
                "modId"
            };

            foreach (var candidateName in candidateNames)
            {
                if (!TryGetMemberValue(entry, candidateName, out var candidateValue))
                {
                    continue;
                }

                if (TryConvertToWorkshopId(candidateValue, out workshopId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryConvertToWorkshopId(object value, out string workshopId)
        {
            workshopId = null;
            var normalized = UnwrapToken(value);
            if (normalized == null)
            {
                return false;
            }

            switch (normalized)
            {
                case string stringValue:
                    workshopId = NormalizeWorkshopId(stringValue);
                    return !string.IsNullOrWhiteSpace(workshopId);
                case ulong ulongValue:
                    workshopId = ulongValue.ToString(CultureInfo.InvariantCulture);
                    return true;
                case long longValue:
                    workshopId = longValue.ToString(CultureInfo.InvariantCulture);
                    return true;
                case uint uintValue:
                    workshopId = uintValue.ToString(CultureInfo.InvariantCulture);
                    return true;
                case int intValue:
                    workshopId = intValue.ToString(CultureInfo.InvariantCulture);
                    return true;
            }

            if (normalized is IConvertible convertible)
            {
                try
                {
                    workshopId = convertible.ToString(CultureInfo.InvariantCulture);
                    workshopId = NormalizeWorkshopId(workshopId);
                    return !string.IsNullOrWhiteSpace(workshopId);
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryConvertToBoolean(object value, out bool result)
        {
            result = false;
            var normalized = UnwrapToken(value);
            if (normalized == null)
            {
                return false;
            }

            switch (normalized)
            {
                case bool boolValue:
                    result = boolValue;
                    return true;
                case string stringValue:
                    stringValue = stringValue.Trim();
                    if (bool.TryParse(stringValue, out var parsedBool))
                    {
                        result = parsedBool;
                        return true;
                    }

                    if (int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
                    {
                        result = parsedInt != 0;
                        return true;
                    }
                    return false;
                case sbyte sbyteValue:
                    result = sbyteValue != 0;
                    return true;
                case byte byteValue:
                    result = byteValue != 0;
                    return true;
                case short shortValue:
                    result = shortValue != 0;
                    return true;
                case ushort ushortValue:
                    result = ushortValue != 0;
                    return true;
                case int intValue:
                    result = intValue != 0;
                    return true;
                case uint uintValue:
                    result = uintValue != 0;
                    return true;
                case long longValue:
                    result = longValue != 0;
                    return true;
                case ulong ulongValue:
                    result = ulongValue != 0;
                    return true;
            }

            if (normalized is IConvertible convertible)
            {
                try
                {
                    result = convertible.ToBoolean(CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryGetMemberValue(object instance, string memberName, out object value)
        {
            value = null;
            if (instance == null || string.IsNullOrWhiteSpace(memberName))
            {
                return false;
            }

            if (instance is JObject jObject)
            {
                var property = jObject.Properties().FirstOrDefault(prop => string.Equals(prop.Name, memberName, StringComparison.OrdinalIgnoreCase));
                if (property != null)
                {
                    value = property.Value;
                    return true;
                }

                return false;
            }

            if (instance is JToken token && token.Type == JTokenType.Object)
            {
                return TryGetMemberValue((JObject)token, memberName, out value);
            }

            var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = instance.GetType();

            var propertyInfo = type.GetProperty(memberName, bindingFlags)
                ?? type.GetProperties(bindingFlags).FirstOrDefault(property => string.Equals(property.Name, memberName, StringComparison.OrdinalIgnoreCase));
            if (propertyInfo != null)
            {
                value = propertyInfo.GetValue(instance);
                return true;
            }

            var fieldInfo = type.GetField(memberName, bindingFlags)
                ?? type.GetFields(bindingFlags).FirstOrDefault(field => string.Equals(field.Name, memberName, StringComparison.OrdinalIgnoreCase));
            if (fieldInfo != null)
            {
                value = fieldInfo.GetValue(instance);
                return true;
            }

            return false;
        }

        private static object UnwrapToken(object value)
        {
            if (value is JValue jValue)
            {
                return jValue.Value;
            }

            return value;
        }

        private static bool IsSpeedRankedsWorkshopId(string workshopId)
        {
            return string.Equals(NormalizeWorkshopId(workshopId), SpeedRankedsWorkshopIdText, StringComparison.Ordinal);
        }

        private static string NormalizeWorkshopId(string workshopId)
        {
            return string.IsNullOrWhiteSpace(workshopId) ? null : workshopId.Trim();
        }

        private static string DescribeValue(object value)
        {
            var normalized = UnwrapToken(value);
            return normalized == null ? "null" : $"{normalized} ({normalized.GetType().Name})";
        }
    }
}