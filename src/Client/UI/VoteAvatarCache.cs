using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using System.Linq;

namespace schrader
{
    internal static class VoteAvatarCache
    {
        private sealed class CacheEntry
        {
            public Texture2D Texture;
            public float LastAttemptAt = -999f;
            public bool CacheMissLogged;
            public bool FirstHitLogged;
        }

        private static readonly Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        private const float AvatarRetryIntervalSeconds = 1f;

        public static bool TryGetAvatarTexture(string steamId, out Texture2D texture)
        {
            texture = null;

            var normalizedSteamId = NormalizeSteamId(steamId);

            if (!TryParseSteamId(normalizedSteamId, out var friendSteamId))
            {
                if (!string.IsNullOrWhiteSpace(steamId))
                {
                    Debug.LogWarning($"[AVATAR] Invalid avatar lookup. requested={steamId} normalized={normalizedSteamId ?? "none"}");
                }
                return false;
            }

            if (!cache.TryGetValue(normalizedSteamId, out var entry))
            {
                entry = new CacheEntry();
                cache[normalizedSteamId] = entry;
            }

            if (!entry.CacheMissLogged)
            {
                entry.CacheMissLogged = true;
                Debug.Log($"[AVATAR] Cache miss. requested={steamId ?? "none"} normalized={normalizedSteamId}");
            }

            if (entry.Texture != null)
            {
                texture = entry.Texture;
                if (!entry.FirstHitLogged)
                {
                    entry.FirstHitLogged = true;
                    Debug.Log($"[AVATAR] Cache hit. steamId={normalizedSteamId}");
                }
                return true;
            }

            if (Time.unscaledTime - entry.LastAttemptAt < AvatarRetryIntervalSeconds)
            {
                return false;
            }

            entry.LastAttemptAt = Time.unscaledTime;
            Debug.Log($"[AVATAR] Async load start. steamId={normalizedSteamId}");
            if (!TryLoadTexture(friendSteamId, out texture, out var failureReason))
            {
                Debug.Log($"[AVATAR] Async load pending/fail. steamId={normalizedSteamId} reason={failureReason}");
                return false;
            }

            entry.Texture = texture;
            Debug.Log($"[AVATAR] Async load finish. steamId={normalizedSteamId} size={texture.width}x{texture.height}");
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

        private static bool TryLoadTexture(CSteamID friendSteamId, out Texture2D texture, out string failureReason)
        {
            texture = null;
            failureReason = null;

            try
            {
                SteamFriends.RequestUserInformation(friendSteamId, false);
                var imageHandle = ResolveAvatarHandle(friendSteamId);
                if (imageHandle <= 0)
                {
                    failureReason = imageHandle == -1 ? "pending-steam-avatar" : "missing-avatar-handle";
                    return false;
                }

                if (!SteamUtils.GetImageSize(imageHandle, out var width, out var height) || width == 0 || height == 0)
                {
                    failureReason = "missing-avatar-size";
                    return false;
                }

                var buffer = new byte[width * height * 4];
                if (!SteamUtils.GetImageRGBA(imageHandle, buffer, buffer.Length))
                {
                    failureReason = "missing-avatar-rgba";
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
            catch (Exception ex)
            {
                texture = null;
                failureReason = $"exception:{ex.GetType().Name}";
                return false;
            }
        }

        private static int ResolveAvatarHandle(CSteamID friendSteamId)
        {
            try
            {
                var largeHandle = SteamFriends.GetLargeFriendAvatar(friendSteamId);
                if (largeHandle > 0)
                {
                    return largeHandle;
                }

                var mediumHandle = SteamFriends.GetMediumFriendAvatar(friendSteamId);
                if (mediumHandle > 0)
                {
                    return mediumHandle;
                }

                var smallHandle = SteamFriends.GetSmallFriendAvatar(friendSteamId);
                if (smallHandle > 0)
                {
                    return smallHandle;
                }

                if (largeHandle == -1 || mediumHandle == -1 || smallHandle == -1)
                {
                    return -1;
                }
            }
            catch { }

            return 0;
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

        private static string NormalizeSteamId(string candidate)
        {
            var clean = candidate?.Trim();
            if (string.IsNullOrWhiteSpace(clean))
            {
                return null;
            }

            if (ulong.TryParse(clean, out var rawSteamId) && rawSteamId != 0)
            {
                return clean;
            }

            if (clean.IndexOf(':') >= 0 || clean.IndexOf('_') >= 0 || clean.IndexOf('/') >= 0 || clean.IndexOf('\\') >= 0)
            {
                foreach (var token in clean.Split(new[] { ':', '_', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (ulong.TryParse(token, out rawSteamId) && rawSteamId != 0)
                    {
                        return token;
                    }
                }
            }

            if (clean.StartsWith("steam", StringComparison.OrdinalIgnoreCase))
            {
                var digits = new string(clean.Where(char.IsDigit).ToArray());
                if (ulong.TryParse(digits, out rawSteamId) && rawSteamId != 0)
                {
                    return digits;
                }
            }

            return null;
        }
    }
}