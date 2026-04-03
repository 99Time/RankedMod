using System;
using System.Text;
using Newtonsoft.Json;
using Unity.Netcode;

namespace schrader
{
    internal static class RankedOverlayNetcode
    {
        private const int MinBufferCapacity = 256;
        private const int CapacityPadding = 512;

        public static int EstimateCapacity<T>(T message)
        {
            var json = SerializeJson(message);
            var utf8Bytes = Encoding.UTF8.GetByteCount(json);
            var utf16Bytes = Encoding.Unicode.GetByteCount(json);
            var utf32Bytes = Encoding.UTF32.GetByteCount(json);
            var worstCaseBytes = Math.Max(json.Length * 8, Math.Max(utf8Bytes, Math.Max(utf16Bytes, utf32Bytes)) * 2);
            return Math.Max(MinBufferCapacity, worstCaseBytes + CapacityPadding);
        }

        public static void WriteJson<T>(ref FastBufferWriter writer, T message)
        {
            var json = SerializeJson(message);
            writer.WriteValueSafe(json, false);
        }

        public static T ReadJson<T>(ref FastBufferReader reader) where T : class
        {
            string json = string.Empty;
            reader.ReadValueSafe(out json, false);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonConvert.DeserializeObject<T>(json);
        }

        private static string SerializeJson<T>(T message)
        {
            return JsonConvert.SerializeObject(message) ?? string.Empty;
        }
    }
}