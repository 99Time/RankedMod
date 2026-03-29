using System;
using Newtonsoft.Json;
using Unity.Netcode;

namespace schrader
{
    internal static class RankedOverlayNetcode
    {
        private const int MinBufferCapacity = 256;

        public static int EstimateCapacity<T>(T message)
        {
            var json = JsonConvert.SerializeObject(message);
            return Math.Max(MinBufferCapacity, ((json?.Length ?? 0) * 4) + 32);
        }

        public static void WriteJson<T>(ref FastBufferWriter writer, T message)
        {
            var json = JsonConvert.SerializeObject(message) ?? string.Empty;
            writer.WriteValueSafe(json, false);
        }

        public static T ReadJson<T>(ref FastBufferReader reader) where T : class
        {
            string json = string.Empty;
            reader.ReadValueSafe(out json, false);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}