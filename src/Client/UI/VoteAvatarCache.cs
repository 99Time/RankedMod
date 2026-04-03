using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace schrader
{
    internal static class VoteAvatarCache
    {
        private sealed class CacheEntry
        {
            public Texture2D Texture;
            public float LastAttemptAt = -999f;
        }

        private static readonly Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        private const float AvatarRetryIntervalSeconds = 1f;

        public static bool TryGetAvatarTexture(string steamId, out Texture2D texture)
        {
            texture = null;

            if (!TryParseSteamId(steamId, out var friendSteamId))
            {
                return false;
            }

            if (!cache.TryGetValue(steamId, out var entry))
            {
                entry = new CacheEntry();
                cache[steamId] = entry;
            }

            if (entry.Texture != null)
            {
                texture = entry.Texture;
                return true;
            }

            if (Time.unscaledTime - entry.LastAttemptAt < AvatarRetryIntervalSeconds)
            {
                return false;
            }

            entry.LastAttemptAt = Time.unscaledTime;
            if (!TryLoadTexture(friendSteamId, out texture))
            {
                return false;
            }

            entry.Texture = texture;
            return true;
        }

        public static void Clear()
        {
            foreach (var entry in cache.Values)
            {
                if (entry?.Texture != null)
                {
                    UnityEngine.Object.Destroy(entry.Texture);
                }
            }

            cache.Clear();
        }

        private static bool TryLoadTexture(CSteamID friendSteamId, out Texture2D texture)
        {
            texture = null;

            try
            {
                SteamFriends.RequestUserInformation(friendSteamId, false);
                var imageHandle = SteamFriends.GetLargeFriendAvatar(friendSteamId);
                if (imageHandle <= 0)
                {
                    return false;
                }

                if (!SteamUtils.GetImageSize(imageHandle, out var width, out var height) || width == 0 || height == 0)
                {
                    return false;
                }

                var buffer = new byte[width * height * 4];
                if (!SteamUtils.GetImageRGBA(imageHandle, buffer, buffer.Length))
                {
                    return false;
                }

                FlipRgbaVertically(buffer, (int)width, (int)height);

                texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
                texture.name = $"VoteAvatar_{friendSteamId.m_SteamID}";
                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.LoadRawTextureData(buffer);
                texture.Apply(false, true);
                return true;
            }
            catch
            {
                texture = null;
                return false;
            }
        }

        private static void FlipRgbaVertically(byte[] buffer, int width, int height)
        {
            if (buffer == null || width <= 0 || height <= 0)
            {
                return;
            }

            var rowSize = width * 4;
            var tempRow = new byte[rowSize];
            for (var top = 0; top < height / 2; top++)
            {
                var bottom = height - 1 - top;
                var topOffset = top * rowSize;
                var bottomOffset = bottom * rowSize;
                Buffer.BlockCopy(buffer, topOffset, tempRow, 0, rowSize);
                Buffer.BlockCopy(buffer, bottomOffset, buffer, topOffset, rowSize);
                Buffer.BlockCopy(tempRow, 0, buffer, bottomOffset, rowSize);
            }
        }

        private static bool TryParseSteamId(string steamId, out CSteamID parsedSteamId)
        {
            parsedSteamId = default(CSteamID);

            if (string.IsNullOrWhiteSpace(steamId) || !ulong.TryParse(steamId.Trim(), out var rawSteamId) || rawSteamId == 0)
            {
                return false;
            }

            parsedSteamId = new CSteamID(rawSteamId);
            return true;
        }
    }
}