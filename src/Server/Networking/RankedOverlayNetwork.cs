using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace schrader
{
    internal static class RankedOverlayNetwork
    {
        private const NetworkDelivery MatchResultDelivery = NetworkDelivery.ReliableFragmentedSequenced;
        private static readonly Dictionary<ulong, string> lastApprovalRequestSignatureByClient = new Dictionary<ulong, string>();
        private static string lastDraftSignature;
        private static string lastDraftExtendedSignature;
        private static string lastVoteSignature;
        private static CustomMessagingManager currentServerMessagingManager;
        private static bool serverHandlersRegistered;

        public static void EnsureServerHandlers()
        {
            try
            {
                var messagingManager = GetMessagingManager();
                if (ReferenceEquals(messagingManager, currentServerMessagingManager) && serverHandlersRegistered)
                {
                    return;
                }

                if (currentServerMessagingManager != null && serverHandlersRegistered)
                {
                    try { currentServerMessagingManager.UnregisterNamedMessageHandler(RankedOverlayChannels.MatchResultDismiss); } catch { }
                }

                currentServerMessagingManager = messagingManager;
                serverHandlersRegistered = false;
                if (messagingManager == null)
                {
                    return;
                }

                messagingManager.RegisterNamedMessageHandler(RankedOverlayChannels.MatchResultDismiss, OnMatchResultDismissReceived);
                serverHandlersRegistered = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Failed to register post-match handlers: {ex.Message}");
            }
        }

        public static void PublishVoteState(VoteOverlayStateMessage state)
        {
            var message = state ?? VoteOverlayStateMessage.Hidden();
            var signature = BuildVoteSignature(message);
            if (string.Equals(signature, lastVoteSignature, StringComparison.Ordinal)) return;

            lastVoteSignature = signature;
            Broadcast(RankedOverlayChannels.VoteState, message);
        }

        public static void PublishVoteStateToClient(ulong clientId, VoteOverlayStateMessage state)
        {
            SendToClient(RankedOverlayChannels.VoteState, state ?? VoteOverlayStateMessage.Hidden(), clientId);
        }

        public static void PublishDraftState(Server.RankedSystem.DraftOverlayState state)
        {
            var message = ToDraftMessage(state);
            var extendedMessage = ToDraftExtendedMessage(state);
            var extendedSignature = BuildDraftExtendedSignature(extendedMessage);
            var signature = BuildDraftSignature(message);
            var shouldBroadcastExtended = !string.Equals(extendedSignature, lastDraftExtendedSignature, StringComparison.Ordinal);
            var shouldBroadcastBasic = !string.Equals(signature, lastDraftSignature, StringComparison.Ordinal);

            if (shouldBroadcastExtended)
            {
                lastDraftExtendedSignature = extendedSignature;
                Broadcast(RankedOverlayChannels.DraftStateExtended, extendedMessage);
            }

            if (shouldBroadcastBasic)
            {
                lastDraftSignature = signature;
                Broadcast(RankedOverlayChannels.DraftState, message);
            }
        }

        public static void PublishDraftStateToClient(ulong clientId, Server.RankedSystem.DraftOverlayState state)
        {
            SendToClient(RankedOverlayChannels.DraftStateExtended, ToDraftExtendedMessage(state), clientId);
            SendToClient(RankedOverlayChannels.DraftState, ToDraftMessage(state), clientId);
        }

        public static void PublishMatchResult(MatchResultMessage state)
        {
            BroadcastReliable(RankedOverlayChannels.MatchResult, state ?? MatchResultMessage.Hidden(), MatchResultDelivery);
        }

        public static void PublishMatchResultToClient(ulong clientId, MatchResultMessage state)
        {
            SendToClientReliable(RankedOverlayChannels.MatchResult, state ?? MatchResultMessage.Hidden(), clientId, MatchResultDelivery);
        }

        public static void PublishApprovalRequestStateToClient(ulong clientId, ApprovalRequestStateMessage state)
        {
            var message = state ?? ApprovalRequestStateMessage.Hidden();
            var signature = BuildApprovalRequestSignature(message);
            if (lastApprovalRequestSignatureByClient.TryGetValue(clientId, out var lastSignature)
                && string.Equals(signature, lastSignature, StringComparison.Ordinal))
            {
                return;
            }

            lastApprovalRequestSignatureByClient[clientId] = signature;
            SendToClient(RankedOverlayChannels.ApprovalRequestState, message, clientId);
        }

        public static void PublishDiscordInviteOpenToClient(ulong clientId, string url = null)
        {
            SendToClient(RankedOverlayChannels.DiscordInviteOpen, new OpenDiscordInviteMessage
            {
                Url = string.IsNullOrWhiteSpace(url) ? Constants.DISCORD_INVITE_URL : url
            }, clientId);
        }

        public static void ResyncClient(ulong clientId)
        {
            try
            {
                PublishVoteStateToClient(clientId, Server.RankedSystem.GetVoteOverlayState());
                PublishDraftStateToClient(clientId, Server.RankedSystem.GetDraftOverlayState());
                PublishApprovalRequestStateToClient(clientId, Server.RankedSystem.GetApprovalRequestStateForClient(clientId));
                PublishMatchResultToClient(clientId, Server.RankedSystem.GetMatchResultStateForClient(clientId));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Ranked overlay resync failed: {ex.Message}");
            }
        }

        private static void OnMatchResultDismissReceived(ulong senderClientId, FastBufferReader reader)
        {
            try
            {
                RankedOverlayNetcode.ReadJson<MatchResultDismissMessage>(ref reader);
                Server.RankedSystem.HandlePostMatchDismiss(senderClientId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Failed to process post-match dismiss: {ex.Message}");
            }
        }

        private static void Broadcast<T>(string messageName, T message)
        {
            var manager = GetMessagingManager();
            if (manager == null) return;

            var capacity = RankedOverlayNetcode.EstimateCapacity(message);
            var writer = new FastBufferWriter(capacity, Allocator.Temp);
            try
            {
                RankedOverlayNetcode.WriteJson(ref writer, message);
                manager.SendNamedMessageToAll(messageName, writer);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private static void BroadcastReliable<T>(string messageName, T message, NetworkDelivery delivery)
        {
            var manager = GetMessagingManager();
            if (manager == null) return;

            var capacity = RankedOverlayNetcode.EstimateCapacity(message);
            var writer = new FastBufferWriter(capacity, Allocator.Temp);
            try
            {
                RankedOverlayNetcode.WriteJson(ref writer, message);
                manager.SendNamedMessageToAll(messageName, writer, delivery);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private static void SendToClient<T>(string messageName, T message, ulong clientId)
        {
            var manager = GetMessagingManager();
            if (manager == null) return;

            var capacity = RankedOverlayNetcode.EstimateCapacity(message);
            var writer = new FastBufferWriter(capacity, Allocator.Temp);
            try
            {
                RankedOverlayNetcode.WriteJson(ref writer, message);
                manager.SendNamedMessage(messageName, clientId, writer);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private static void SendToClientReliable<T>(string messageName, T message, ulong clientId, NetworkDelivery delivery)
        {
            var manager = GetMessagingManager();
            if (manager == null) return;

            var capacity = RankedOverlayNetcode.EstimateCapacity(message);
            var writer = new FastBufferWriter(capacity, Allocator.Temp);
            try
            {
                RankedOverlayNetcode.WriteJson(ref writer, message);
                manager.SendNamedMessage(messageName, clientId, writer, delivery);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private static CustomMessagingManager GetMessagingManager()
        {
            try
            {
                var networkManager = NetworkManager.Singleton;
                if (networkManager == null || !networkManager.IsServer) return null;
                return networkManager.CustomMessagingManager;
            }
            catch
            {
                return null;
            }
        }

        private static DraftOverlayStateMessage ToDraftMessage(Server.RankedSystem.DraftOverlayState state)
        {
            if (state == null || !state.IsVisible)
            {
                return DraftOverlayStateMessage.Hidden();
            }

            return new DraftOverlayStateMessage
            {
                IsVisible = state.IsVisible,
                IsCompleted = state.IsCompleted,
                Title = state.Title,
                RedCaptainName = state.RedCaptainName,
                BlueCaptainName = state.BlueCaptainName,
                CurrentTurnName = state.CurrentTurnName,
                AvailablePlayers = state.AvailablePlayers ?? Array.Empty<string>(),
                RedPlayers = state.RedPlayers ?? Array.Empty<string>(),
                BluePlayers = state.BluePlayers ?? Array.Empty<string>(),
                PendingLateJoinerCount = state.PendingLateJoinerCount,
                PendingLateJoiners = state.PendingLateJoiners ?? Array.Empty<string>(),
                DummyModeActive = state.DummyModeActive,
                FooterText = state.FooterText
            };
        }

        private static DraftOverlayExtendedMessage ToDraftExtendedMessage(Server.RankedSystem.DraftOverlayState state)
        {
            if (state == null || !state.IsVisible)
            {
                return DraftOverlayExtendedMessage.Hidden();
            }

            return new DraftOverlayExtendedMessage
            {
                IsVisible = state.IsVisible,
                AvailablePlayerEntries = state.AvailablePlayerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>(),
                RedPlayerEntries = state.RedPlayerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>(),
                BluePlayerEntries = state.BluePlayerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>(),
                PendingLateJoinerEntries = state.PendingLateJoinerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>()
            };
        }

        private static string BuildVoteSignature(VoteOverlayStateMessage state)
        {
            return string.Join("|",
                state.IsVisible,
                state.Title ?? string.Empty,
                state.PromptText ?? string.Empty,
                state.InitiatorName ?? string.Empty,
                state.SecondsRemaining,
                state.EligibleCount,
                state.YesVotes,
                state.NoVotes,
                state.RequiredYesVotes,
                state.FooterText ?? string.Empty);
        }

        private static string BuildDraftSignature(DraftOverlayStateMessage state)
        {
            return string.Join("|",
                state.IsVisible,
                state.IsCompleted,
                state.Title ?? string.Empty,
                state.RedCaptainName ?? string.Empty,
                state.BlueCaptainName ?? string.Empty,
                state.CurrentTurnName ?? string.Empty,
                state.PendingLateJoinerCount,
                state.DummyModeActive,
                string.Join(",", state.AvailablePlayers ?? Array.Empty<string>()),
                string.Join(",", state.RedPlayers ?? Array.Empty<string>()),
                string.Join(",", state.BluePlayers ?? Array.Empty<string>()),
                string.Join(",", state.PendingLateJoiners ?? Array.Empty<string>()),
                state.FooterText ?? string.Empty);
        }

            private static string BuildDraftExtendedSignature(DraftOverlayExtendedMessage state)
            {
                return string.Join("|",
                state.IsVisible,
                string.Join(",", (state.AvailablePlayerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>()).Select(BuildDraftEntrySignature)),
                string.Join(",", (state.RedPlayerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>()).Select(BuildDraftEntrySignature)),
                string.Join(",", (state.BluePlayerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>()).Select(BuildDraftEntrySignature)),
                string.Join(",", (state.PendingLateJoinerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>()).Select(BuildDraftEntrySignature)));
            }

        private static string BuildDraftEntrySignature(DraftOverlayPlayerEntryMessage entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            return string.Join("~",
                entry.CommandTarget ?? string.Empty,
                entry.DisplayName ?? string.Empty,
                entry.PlayerNumber,
                entry.HasMmr,
                entry.Mmr,
                entry.IsCaptain,
                entry.Team);
        }

        private static string BuildApprovalRequestSignature(ApprovalRequestStateMessage state)
        {
            return string.Join("|",
                state.IsVisible,
                state.RequestId ?? string.Empty,
                state.Title ?? string.Empty,
                state.PlayerName ?? string.Empty,
                state.PromptText ?? string.Empty,
                state.TargetTeamName ?? string.Empty,
                state.PreviousTeamName ?? string.Empty,
                state.IsSwitchRequest,
                state.FooterText ?? string.Empty);
        }
    }
}