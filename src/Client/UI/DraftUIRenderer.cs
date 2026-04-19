using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace schrader
{
    public static class DraftUIRenderer
    {
        private const float VoteRosterRefreshInterval = 0.75f;
        private const int AvatarRefreshIntervalMilliseconds = 300;
        private const int AvatarRefreshMaxAttempts = 30;

        public sealed class View
        {
            public VisualElement Container;
            public VisualElement Backdrop;
            public VisualElement Root;
            public VisualElement ApprovalPopupLayer;
            internal DiscordOnboardingUIRenderer.View DiscordOnboarding;
            internal WelcomeUIRenderer.View Welcome;
            public VisualElement VotingPanel;
            public Label VotingChipLabel;
            public Label VotingTitleLabel;
            public Label VotingPromptLabel;
            public Label VotingTimerLabel;
            public Label VotingStatsLabel;
            public Label VotingFooterLabel;
            public VisualElement VotingReadyFill;
            public VisualElement VotingCountdownFill;
            public VisualElement VotingPlayersLeftColumn;
            public VisualElement VotingPlayersRightColumn;
            public Button VotingAcceptButton;
            public Label VotingAcceptTitleLabel;
            public Label VotingAcceptSubtitleLabel;
            public Button VotingRejectButton;
            public Label VotingRejectTitleLabel;
            public Label VotingRejectSubtitleLabel;
            public string VotingRosterSignature;
            public float VotingRosterRefreshedAt;
            public VisualElement ApprovalPanel;
            public VisualElement ApprovalStatusChip;
            public VisualElement ApprovalMetaCard;
            public VisualElement ApprovalButtonRow;
            public Label ApprovalStatusLabel;
            public Label ApprovalTitleLabel;
            public Label ApprovalTimerLabel;
            public Label ApprovalPlayerNameLabel;
            public Label ApprovalPromptLabel;
            public Label ApprovalMetaLabel;
            public Label ApprovalFooterLabel;
            public Label ApprovalHintLabel;
            public Button ApprovalAcceptButton;
            public Button ApprovalRejectButton;
            public VisualElement DraftPanel;
            public Label DraftPhaseChipLabel;
            public Label DraftTitleLabel;
            public Label DraftTurnLabel;
            public Label DraftTurnSubLabel;
            public Label DraftTurnStatusLabel;
            public Label DraftTurnMetaLabel;
            public Label DraftFooterLabel;
            public Label DraftFooterRoleLabel;
            public Label DraftFooterPoolLabel;
            public Label DraftFooterStatusLabel;
            public VisualElement DraftRedCaptainCard;
            public Label DraftRedCaptainLabel;
            public VisualElement DraftBlueCaptainCard;
            public Label DraftBlueCaptainLabel;
            public VisualElement DraftTurnCard;
            public VisualElement DraftRedTeamPanel;
            public VisualElement DraftAvailablePanel;
            public VisualElement DraftBlueTeamPanel;
            public VisualElement RedTeamContainer;
            public VisualElement AvailablePlayersContainer;
            public VisualElement BlueTeamContainer;
            public VisualElement PendingPlayersContainer;
            internal PostMatchUIRenderer.View PostMatch;
        }

        public static void CreateUI(UIHUD hud, VisualElement rootVisualElement, out View view, Action onVoteAccepted, Action onVoteRejected, Action onApprovalAccepted, Action onApprovalRejected, Action<string> onPickPlayer, Action<string> onAcceptLateJoiner, Action<string> onDiscordOnboardingVerify, Action onDiscordOnboardingJoin, Action onDiscordOnboardingLeave, Action onDiscordOnboardingCloseVerification, Action onWelcomeDiscordOpen, Action onWelcomeHostOpen, Action onWelcomeContinue, Action onPostMatchHostOpen, Action onPostMatchContinue, Action onPostMatchClose)
        {
            view = null;

            if (rootVisualElement == null || rootVisualElement.parent == null)
            {
                DraftUIPlugin.LogError("Root VisualElement not found!");
                return;
            }

            var root = rootVisualElement.parent;

            RemoveExistingRootElement(root, "DraftUIContainer");
            RemoveExistingRootElement(root, "ApprovalPopupLayer");

            view = new View();
            view.Container = new VisualElement();
            view.Container.name = "DraftUIContainer";
            view.Container.style.position = Position.Absolute;
            view.Container.style.display = DisplayStyle.None;
            view.Container.style.top = 0;
            view.Container.style.left = 0;
            view.Container.style.right = 0;
            view.Container.style.bottom = 0;
            view.Container.style.justifyContent = Justify.Center;
            view.Container.style.alignItems = Align.Center;
            view.Container.style.opacity = 0f;
            view.Container.pickingMode = PickingMode.Position;
            root.Add(view.Container);

            view.ApprovalPopupLayer = new VisualElement();
            view.ApprovalPopupLayer.name = "ApprovalPopupLayer";
            view.ApprovalPopupLayer.style.position = Position.Absolute;
            view.ApprovalPopupLayer.style.display = DisplayStyle.None;
            view.ApprovalPopupLayer.style.top = 0;
            view.ApprovalPopupLayer.style.left = 0;
            view.ApprovalPopupLayer.style.right = 0;
            view.ApprovalPopupLayer.style.bottom = 0;
            view.ApprovalPopupLayer.pickingMode = PickingMode.Ignore;
            root.Add(view.ApprovalPopupLayer);

            view.Backdrop = new VisualElement();
            view.Backdrop.name = "DraftUIBackdrop";
            view.Backdrop.style.position = Position.Absolute;
            view.Backdrop.style.top = 0;
            view.Backdrop.style.left = 0;
            view.Backdrop.style.right = 0;
            view.Backdrop.style.bottom = 0;
            view.Backdrop.style.backgroundColor = new StyleColor(new Color(0.03f, 0.05f, 0.08f, 0.68f));
            view.Backdrop.pickingMode = PickingMode.Position;
            view.Container.Add(view.Backdrop);

            view.Root = new VisualElement();
            view.Root.style.display = DisplayStyle.Flex;
            view.Root.style.position = Position.Relative;
            view.Root.style.flexDirection = FlexDirection.Column;
            view.Root.style.alignItems = Align.Stretch;
            view.Root.style.width = new StyleLength(new Length(92, LengthUnit.Percent));
            view.Root.style.maxWidth = 1220;
            view.Root.style.marginLeft = Auto();
            view.Root.style.marginRight = Auto();
            view.Root.style.paddingTop = new StyleLength(new Length(24, LengthUnit.Pixel));
            view.Root.style.paddingBottom = new StyleLength(new Length(24, LengthUnit.Pixel));
            view.Root.style.paddingLeft = new StyleLength(new Length(28, LengthUnit.Pixel));
            view.Root.style.paddingRight = new StyleLength(new Length(28, LengthUnit.Pixel));
            view.Root.style.backgroundColor = new StyleColor(new Color(0.06f, 0.09f, 0.12f, 0.94f));
            view.Root.style.borderTopLeftRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            view.Root.style.borderTopRightRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            view.Root.style.borderBottomLeftRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            view.Root.style.borderBottomRightRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            view.Root.style.borderTopWidth = 1;
            view.Root.style.borderRightWidth = 1;
            view.Root.style.borderBottomWidth = 1;
            view.Root.style.borderLeftWidth = 1;
            view.Root.style.borderTopColor = new StyleColor(new Color(0.48f, 0.60f, 0.70f, 0.28f));
            view.Root.style.borderRightColor = new StyleColor(new Color(0.48f, 0.60f, 0.70f, 0.18f));
            view.Root.style.borderBottomColor = new StyleColor(new Color(0.02f, 0.03f, 0.05f, 0.65f));
            view.Root.style.borderLeftColor = new StyleColor(new Color(0.48f, 0.60f, 0.70f, 0.18f));
            view.Container.Add(view.Root);

            BuildVotingUI(view, onVoteAccepted, onVoteRejected);
            BuildApprovalUI(view, onApprovalAccepted, onApprovalRejected);
            BuildDraftUI(view);
            DiscordOnboardingUIRenderer.BuildUI(view, onDiscordOnboardingVerify, onDiscordOnboardingJoin, onDiscordOnboardingLeave, onDiscordOnboardingCloseVerification);
            WelcomeUIRenderer.BuildUI(view, onWelcomeDiscordOpen, onWelcomeHostOpen, onWelcomeContinue);
            PostMatchUIRenderer.BuildUI(view, onPostMatchHostOpen, onPostMatchContinue, onPostMatchClose);

            DraftUIPlugin.Log("UI BUILD COMPLETE");
        }

        public static void ShowHidden(View view)
        {
            if (view == null) return;
            ApplyDefaultRootChrome(view);
            view.Container.style.display = DisplayStyle.None;
            view.Container.style.opacity = 0f;
            SetApprovalPopupVisible(view, false);
            if (view.DiscordOnboarding?.Panel != null) view.DiscordOnboarding.Panel.style.display = DisplayStyle.None;
            if (view.Welcome?.Panel != null) view.Welcome.Panel.style.display = DisplayStyle.None;
            if (view.PostMatch?.Panel != null) view.PostMatch.Panel.style.display = DisplayStyle.None;
        }

        public static void ShowVoting(View view)
        {
            if (view == null) return;
            ApplyVotingRootChrome(view);
            view.Container.style.display = DisplayStyle.Flex;
            if (view.DiscordOnboarding?.Panel != null) view.DiscordOnboarding.Panel.style.display = DisplayStyle.None;
            if (view.Welcome?.Panel != null) view.Welcome.Panel.style.display = DisplayStyle.None;
            view.VotingPanel.style.display = DisplayStyle.Flex;
            view.DraftPanel.style.display = DisplayStyle.None;
            if (view.PostMatch?.Panel != null) view.PostMatch.Panel.style.display = DisplayStyle.None;
        }

        public static void ShowApproval(View view)
        {
            if (view == null) return;
            SetApprovalPopupVisible(view, true);
        }

        public static void ShowDraft(View view)
        {
            if (view == null) return;
            ApplyDraftRootChrome(view);
            view.Container.style.display = DisplayStyle.Flex;
            view.VotingPanel.style.display = DisplayStyle.None;
            if (view.DiscordOnboarding?.Panel != null) view.DiscordOnboarding.Panel.style.display = DisplayStyle.None;
            if (view.Welcome?.Panel != null) view.Welcome.Panel.style.display = DisplayStyle.None;
            view.DraftPanel.style.display = DisplayStyle.Flex;
            if (view.PostMatch?.Panel != null) view.PostMatch.Panel.style.display = DisplayStyle.None;
        }

        public static void SetApprovalPopupVisible(View view, bool isVisible)
        {
            if (view?.ApprovalPopupLayer == null || view.ApprovalPanel == null)
            {
                return;
            }

            view.ApprovalPopupLayer.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            view.ApprovalPanel.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void ApplyVotingRootChrome(View view)
        {
            if (view?.Root == null)
            {
                return;
            }

            view.Root.style.width = new StyleLength(new Length(86, LengthUnit.Percent));
            view.Root.style.maxWidth = 860;
            view.Root.style.paddingTop = 0;
            view.Root.style.paddingBottom = 0;
            view.Root.style.paddingLeft = 0;
            view.Root.style.paddingRight = 0;
            view.Root.style.backgroundColor = new StyleColor(Color.clear);
            view.Root.style.borderTopWidth = 0;
            view.Root.style.borderRightWidth = 0;
            view.Root.style.borderBottomWidth = 0;
            view.Root.style.borderLeftWidth = 0;
        }

        private static void ApplyDefaultRootChrome(View view)
        {
            if (view?.Root == null)
            {
                return;
            }

            view.Root.style.width = new StyleLength(new Length(90, LengthUnit.Percent));
            view.Root.style.maxWidth = 1160;
            view.Root.style.paddingTop = new StyleLength(new Length(22, LengthUnit.Pixel));
            view.Root.style.paddingBottom = new StyleLength(new Length(22, LengthUnit.Pixel));
            view.Root.style.paddingLeft = new StyleLength(new Length(24, LengthUnit.Pixel));
            view.Root.style.paddingRight = new StyleLength(new Length(24, LengthUnit.Pixel));
            view.Root.style.backgroundColor = new StyleColor(new Color(0.05f, 0.08f, 0.11f, 0.95f));
            view.Root.style.borderTopWidth = 1;
            view.Root.style.borderRightWidth = 1;
            view.Root.style.borderBottomWidth = 1;
            view.Root.style.borderLeftWidth = 1;
            ApplyUniformBorder(view.Root, new Color(0.70f, 0.78f, 0.85f, 0.12f));
        }

        private static void ApplyDraftRootChrome(View view)
        {
            if (view?.Root == null)
            {
                return;
            }

            view.Root.style.width = new StyleLength(new Length(94, LengthUnit.Percent));
            view.Root.style.maxWidth = 1180;
            view.Root.style.paddingTop = 0;
            view.Root.style.paddingBottom = 0;
            view.Root.style.paddingLeft = 0;
            view.Root.style.paddingRight = 0;
            view.Root.style.backgroundColor = new StyleColor(Color.clear);
            view.Root.style.borderTopWidth = 0;
            view.Root.style.borderRightWidth = 0;
            view.Root.style.borderBottomWidth = 0;
            view.Root.style.borderLeftWidth = 0;
        }

        private static void RemoveExistingRootElement(VisualElement root, string elementName)
        {
            if (root == null || string.IsNullOrWhiteSpace(elementName))
            {
                return;
            }

            var existing = root.Q<VisualElement>(elementName);
            while (existing != null)
            {
                existing.RemoveFromHierarchy();
                existing = root.Q<VisualElement>(elementName);
            }
        }

        public static void RenderVoting(View view, VoteOverlayStateMessage state, float secondsRemainingPrecise, bool hasAccepted, bool hasRejected, bool contentChanged)
        {
            if (view == null || state == null) return;

            var voteLocked = hasAccepted || hasRejected;
            var durationSeconds = Mathf.Max(1f, state.VoteDurationSeconds > 0 ? state.VoteDurationSeconds : Mathf.Max(1, state.SecondsRemaining));
            var remainingSeconds = Mathf.Clamp(secondsRemainingPrecise, 0f, durationSeconds);
            var readyProgress = state.EligibleCount > 0
                ? Mathf.Clamp01((float)state.YesVotes / state.EligibleCount)
                : 0f;
            var countdownProgress = Mathf.Clamp01(remainingSeconds / durationSeconds);
            var pulse = 0.5f + (Mathf.Sin(Time.unscaledTime * 3.2f) * 0.5f);
            var remainingYesVotes = Mathf.Max(0, state.RequiredYesVotes - state.YesVotes);
            var waitingVotes = Mathf.Max(0, state.EligibleCount - state.YesVotes - state.NoVotes);

            view.VotingTitleLabel.text = string.IsNullOrWhiteSpace(state.Title) ? "MATCH FOUND" : state.Title;
            view.VotingPromptLabel.text = "Vote with the buttons below to begin ranked play.";
            view.VotingTimerLabel.text = FormatVoteCountdown(remainingSeconds);
            view.VotingStatsLabel.text = state.NoVotes > 0
                ? $"Ready {state.YesVotes}  ·  Waiting {waitingVotes}\nDeclined {state.NoVotes}  ·  Need {remainingYesVotes} yes"
                : $"Ready {state.YesVotes}  ·  Waiting {waitingVotes}\nNeed {remainingYesVotes} yes";
            view.VotingStatsLabel.style.display = DisplayStyle.Flex;
            view.VotingFooterLabel.text = string.Empty;
            view.VotingFooterLabel.style.display = DisplayStyle.None;

            view.VotingReadyFill.style.width = new StyleLength(new Length(readyProgress * 100f, LengthUnit.Percent));
            view.VotingCountdownFill.style.width = new StyleLength(new Length(countdownProgress * 100f, LengthUnit.Percent));
            view.VotingReadyFill.style.opacity = 0.88f + (pulse * 0.12f);
            view.VotingCountdownFill.style.opacity = 0f;

            view.VotingAcceptButton.SetEnabled(!voteLocked);
            view.VotingRejectButton.SetEnabled(!voteLocked);
            ConfigureVoteActionButton(
                view.VotingAcceptButton,
                view.VotingAcceptTitleLabel,
                view.VotingAcceptSubtitleLabel,
                "VOTE YES",
                string.Empty,
                voteLocked,
                hasAccepted);
            ConfigureVoteActionButton(
                view.VotingRejectButton,
                view.VotingRejectTitleLabel,
                view.VotingRejectSubtitleLabel,
                "VOTE NO",
                string.Empty,
                voteLocked,
                hasRejected);

            ApplyVoteAmbientMotion(view, hasAccepted, hasRejected, pulse);

            var rosterSignature = string.Join(",", (state.PlayerEntries ?? Array.Empty<VoteOverlayPlayerEntryMessage>()).Select(BuildVotePlayerSignature));
            if (contentChanged
                || !string.Equals(rosterSignature, view.VotingRosterSignature, StringComparison.Ordinal)
                || Time.unscaledTime - view.VotingRosterRefreshedAt >= VoteRosterRefreshInterval)
            {
                view.VotingRosterSignature = rosterSignature;
                view.VotingRosterRefreshedAt = Time.unscaledTime;
                PopulateVotePlayerEntries(view, state.PlayerEntries);
            }
        }

        public static void RenderApproval(View view, ApprovalRequestStateMessage state, bool isInteractionMode)
        {
            if (view == null || state == null) return;

            var isCaptainDecision = state.ViewRole == ApprovalRequestViewRole.CaptainDecision;
            var isPending = state.Status == ApprovalRequestDisplayStatus.Pending;
            var statusAccent = ResolveApprovalStatusAccent(state.Status, isCaptainDecision);
            var canRespond = isCaptainDecision && isPending;

            view.ApprovalStatusLabel.text = ResolveApprovalStatusText(state);
            view.ApprovalStatusLabel.style.color = new StyleColor(statusAccent);
            if (view.ApprovalStatusChip != null)
            {
                view.ApprovalStatusChip.style.backgroundColor = new StyleColor(ResolveApprovalChipBackground(state.Status, isCaptainDecision));
                ApplyUniformBorder(view.ApprovalStatusChip, new Color(statusAccent.r, statusAccent.g, statusAccent.b, 0.18f));
            }

            if (view.ApprovalMetaCard != null)
            {
                view.ApprovalMetaCard.style.backgroundColor = new StyleColor(canRespond
                    ? new Color(0.10f, 0.12f, 0.16f, 0.94f)
                    : new Color(1f, 1f, 1f, 0.04f));
            }

            view.ApprovalTitleLabel.text = ResolveApprovalTitleText(state);
            view.ApprovalTimerLabel.text = isPending ? $"{Mathf.Max(0, Mathf.CeilToInt(state.SecondsRemaining))}s" : string.Empty;
            view.ApprovalTimerLabel.style.display = isPending ? DisplayStyle.Flex : DisplayStyle.None;
            view.ApprovalPlayerNameLabel.text = BuildApprovalSubjectText(state);
            view.ApprovalPromptLabel.text = string.IsNullOrWhiteSpace(state.PromptText)
                ? (isCaptainDecision ? "Review this request and decide whether to approve the move." : "Your request is being tracked by the approval system.")
                : state.PromptText;
            view.ApprovalMetaLabel.text = BuildApprovalMetaText(state);
            view.ApprovalMetaLabel.style.display = string.IsNullOrWhiteSpace(view.ApprovalMetaLabel.text) ? DisplayStyle.None : DisplayStyle.Flex;
            view.ApprovalFooterLabel.text = string.IsNullOrWhiteSpace(state.FooterText)
                ? (canRespond
                    ? "Approve places the player into your team. Reject keeps the current state intact."
                    : "This popup closes automatically after the state is resolved.")
                : state.FooterText;
            view.ApprovalHintLabel.text = BuildApprovalHintText(state, isInteractionMode);

            if (view.ApprovalButtonRow != null)
            {
                view.ApprovalButtonRow.style.display = canRespond ? DisplayStyle.Flex : DisplayStyle.None;
            }

            view.ApprovalAcceptButton.style.display = canRespond ? DisplayStyle.Flex : DisplayStyle.None;
            view.ApprovalRejectButton.style.display = canRespond ? DisplayStyle.Flex : DisplayStyle.None;
            view.ApprovalAcceptButton.SetEnabled(canRespond);
            view.ApprovalRejectButton.SetEnabled(canRespond);
        }

        private static string ResolveApprovalStatusText(ApprovalRequestStateMessage state)
        {
            if (state == null)
            {
                return string.Empty;
            }

            switch (state.Status)
            {
                case ApprovalRequestDisplayStatus.Pending:
                    return state.ViewRole == ApprovalRequestViewRole.CaptainDecision ? "CAPTAIN DECISION" : "PENDING";
                case ApprovalRequestDisplayStatus.Approved:
                    return "APPROVED";
                case ApprovalRequestDisplayStatus.Rejected:
                    return "DENIED";
                case ApprovalRequestDisplayStatus.Cooldown:
                    return "COOLDOWN";
                case ApprovalRequestDisplayStatus.Expired:
                    return "EXPIRED";
                default:
                    return "CANCELLED";
            }
        }

        private static string ResolveApprovalTitleText(ApprovalRequestStateMessage state)
        {
            if (!string.IsNullOrWhiteSpace(state?.Title))
            {
                return state.Title;
            }

            if (state?.ViewRole == ApprovalRequestViewRole.CaptainDecision)
            {
                return "TEAM SWITCH REQUEST";
            }

            switch (state?.Status)
            {
                case ApprovalRequestDisplayStatus.Approved:
                    return "SWITCH APPROVED";
                case ApprovalRequestDisplayStatus.Rejected:
                    return "SWITCH DENIED";
                case ApprovalRequestDisplayStatus.Pending:
                    return "REQUEST PENDING";
                case ApprovalRequestDisplayStatus.Cooldown:
                    return "REQUEST COOLDOWN";
                case ApprovalRequestDisplayStatus.Expired:
                    return "REQUEST EXPIRED";
                default:
                    return "TEAM APPROVAL REQUIRED";
            }
        }

        private static string BuildApprovalSubjectText(ApprovalRequestStateMessage state)
        {
            if (state == null)
            {
                return "Unknown request";
            }

            var teamName = string.IsNullOrWhiteSpace(state.TargetTeamName) ? "team" : state.TargetTeamName.Trim();
            var playerName = string.IsNullOrWhiteSpace(state.PlayerName) ? "Unknown Player" : NormalizeDraftIdentityText(state.PlayerName, 0);

            if (state.ViewRole == ApprovalRequestViewRole.CaptainDecision)
            {
                return state.IsSwitchRequest
                    ? $"{playerName} wants to join {teamName}"
                    : $"{playerName} wants to join your team";
            }

            switch (state.Status)
            {
                case ApprovalRequestDisplayStatus.Approved:
                    return state.IsSwitchRequest ? $"Move approved to {teamName}" : $"Join approved for {teamName}";
                case ApprovalRequestDisplayStatus.Rejected:
                    return state.IsSwitchRequest ? $"Move denied to {teamName}" : $"Join denied for {teamName}";
                case ApprovalRequestDisplayStatus.Pending:
                    return state.IsSwitchRequest ? $"Waiting to move to {teamName}" : $"Waiting to join {teamName}";
                case ApprovalRequestDisplayStatus.Cooldown:
                    return "Approval request on cooldown";
                case ApprovalRequestDisplayStatus.Expired:
                    return "Approval request expired";
                default:
                    return state.IsSwitchRequest ? $"Switch request for {teamName}" : $"Approval request for {teamName}";
            }
        }

        private static string BuildApprovalMetaText(ApprovalRequestStateMessage state)
        {
            if (state == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(state.TargetTeamName))
            {
                parts.Add(state.IsSwitchRequest ? $"Move to {state.TargetTeamName}" : $"Join {state.TargetTeamName}");
            }

            if (state.IsSwitchRequest && !string.IsNullOrWhiteSpace(state.PreviousTeamName))
            {
                parts.Add($"Currently {state.PreviousTeamName}");
            }

            if (state.ViewRole == ApprovalRequestViewRole.CaptainDecision && state.QueueLength > 1)
            {
                parts.Add($"{Mathf.Max(1, state.QueuePosition)} of {state.QueueLength} waiting");
            }

            return string.Join("  |  ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        private static Color ResolveApprovalChipBackground(ApprovalRequestDisplayStatus status, bool isCaptainDecision)
        {
            switch (status)
            {
                case ApprovalRequestDisplayStatus.Pending:
                    return isCaptainDecision
                        ? new Color(0.72f, 0.56f, 0.16f, 0.18f)
                        : new Color(0.30f, 0.44f, 0.58f, 0.18f);
                case ApprovalRequestDisplayStatus.Approved:
                    return new Color(0.24f, 0.48f, 0.35f, 0.18f);
                case ApprovalRequestDisplayStatus.Rejected:
                    return new Color(0.52f, 0.20f, 0.20f, 0.18f);
                case ApprovalRequestDisplayStatus.Cooldown:
                case ApprovalRequestDisplayStatus.Expired:
                    return new Color(0.56f, 0.42f, 0.18f, 0.18f);
                default:
                    return new Color(1f, 1f, 1f, 0.06f);
            }
        }

        private static string BuildApprovalHintText(ApprovalRequestStateMessage state, bool isInteractionMode)
        {
            if (state == null)
            {
                return string.Empty;
            }

            if (state.ViewRole == ApprovalRequestViewRole.CaptainDecision && state.Status == ApprovalRequestDisplayStatus.Pending)
            {
                return "TIP: Press Enter to approve or Delete to reject.";
            }

            if (state.Status == ApprovalRequestDisplayStatus.Pending)
            {
                return "TIP: Waiting for the captain to approve or reject this request.";
            }

            return "TIP: This notice closes automatically after a short delay.";
        }

        private static Color ResolveApprovalStatusAccent(ApprovalRequestDisplayStatus status, bool isCaptainDecision)
        {
            switch (status)
            {
                case ApprovalRequestDisplayStatus.Pending:
                    return isCaptainDecision
                        ? new Color(1f, 0.88f, 0.50f, 1f)
                        : new Color(0.77f, 0.90f, 1f, 1f);
                case ApprovalRequestDisplayStatus.Approved:
                    return new Color(0.60f, 1f, 0.77f, 1f);
                case ApprovalRequestDisplayStatus.Rejected:
                    return new Color(1f, 0.70f, 0.70f, 1f);
                case ApprovalRequestDisplayStatus.Cooldown:
                    return new Color(1f, 0.84f, 0.56f, 1f);
                case ApprovalRequestDisplayStatus.Expired:
                    return new Color(1f, 0.87f, 0.60f, 1f);
                default:
                    return new Color(0.88f, 0.91f, 0.95f, 1f);
            }
        }

        public static void RenderDraft(View view, DraftOverlayStateMessage state, DraftOverlayExtendedMessage extendedState, bool isLocalTurn, TeamResult currentTurnTeam, Action<string> onPickPlayer, Action<string> onAcceptLateJoiner)
        {
            if (view == null || state == null) return;

            extendedState = extendedState ?? DraftOverlayExtendedMessage.Hidden();

            var availableCount = extendedState.AvailablePlayerEntries != null && extendedState.AvailablePlayerEntries.Length > 0
                ? extendedState.AvailablePlayerEntries.Count(entry => entry != null && !string.IsNullOrWhiteSpace(entry.DisplayName))
                : (state.AvailablePlayers?.Length ?? 0);
            var pendingCount = extendedState.PendingLateJoinerEntries != null && extendedState.PendingLateJoinerEntries.Length > 0
                ? extendedState.PendingLateJoinerEntries.Count(entry => entry != null && !string.IsNullOrWhiteSpace(entry.DisplayName))
                : (state.PendingLateJoiners?.Length ?? 0);

            view.DraftTitleLabel.text = string.IsNullOrWhiteSpace(state.Title) ? "CAPTAIN DRAFT" : state.Title;
            if (view.DraftPhaseChipLabel != null)
            {
                view.DraftPhaseChipLabel.text = state.IsCompleted
                    ? "LOCKED"
                    : isLocalTurn
                        ? "ON THE CLOCK"
                        : string.IsNullOrWhiteSpace(state.CurrentTurnName)
                            ? "DRAFT PHASE"
                            : "LIVE DRAFT";
            }
            view.DraftRedCaptainLabel.text = NormalizeCaptainName(state.RedCaptainName);
            view.DraftBlueCaptainLabel.text = NormalizeCaptainName(state.BlueCaptainName);

            if (state.IsCompleted)
            {
                view.DraftTurnLabel.text = "TEAMS LOCKED";
                view.DraftTurnSubLabel.text = "Final teams are ready. Match start is next.";
                if (view.DraftTurnStatusLabel != null) view.DraftTurnStatusLabel.text = "MATCH READY";
                if (view.DraftTurnMetaLabel != null) view.DraftTurnMetaLabel.text = $"{availableCount} open slots";
            }
            else if (isLocalTurn)
            {
                view.DraftTurnLabel.text = "YOUR PICK";
                view.DraftTurnSubLabel.text = "Captain turn. Choose 1 player from AVAILABLE PICKS.";
                if (view.DraftTurnStatusLabel != null) view.DraftTurnStatusLabel.text = "PRIORITY PICK";
                if (view.DraftTurnMetaLabel != null) view.DraftTurnMetaLabel.text = $"{availableCount} players available";
            }
            else if (!string.IsNullOrWhiteSpace(state.CurrentTurnName))
            {
                view.DraftTurnLabel.text = "CURRENT PICK";
                view.DraftTurnSubLabel.text = $"{NormalizeCaptainName(state.CurrentTurnName)} is choosing now.";
                if (view.DraftTurnStatusLabel != null) view.DraftTurnStatusLabel.text = "PHASE LIVE";
                if (view.DraftTurnMetaLabel != null) view.DraftTurnMetaLabel.text = NormalizeCaptainName(state.CurrentTurnName);
            }
            else
            {
                view.DraftTurnLabel.text = "WAITING FOR NEXT PICK";
                view.DraftTurnSubLabel.text = "Captain order will appear here.";
                if (view.DraftTurnStatusLabel != null) view.DraftTurnStatusLabel.text = "STAGING";
                if (view.DraftTurnMetaLabel != null) view.DraftTurnMetaLabel.text = $"{availableCount} players in pool";
            }

            StyleCaptainSummaryCard(view.DraftRedCaptainCard, TeamResult.Red, currentTurnTeam == TeamResult.Red, state.IsCompleted);
            StyleCaptainSummaryCard(view.DraftBlueCaptainCard, TeamResult.Blue, currentTurnTeam == TeamResult.Blue, state.IsCompleted);

            if (view.DraftFooterRoleLabel != null)
            {
                view.DraftFooterRoleLabel.text = state.IsCompleted
                    ? "Teams locked"
                    : isLocalTurn
                        ? "You are selecting"
                        : string.IsNullOrWhiteSpace(state.CurrentTurnName)
                            ? "Waiting for captains"
                            : $"{NormalizeCaptainName(state.CurrentTurnName)} selecting";
            }

            if (view.DraftFooterPoolLabel != null)
            {
                view.DraftFooterPoolLabel.text = $"{availableCount} available · {pendingCount} pending";
            }

            if (view.DraftFooterStatusLabel != null)
            {
                view.DraftFooterStatusLabel.text = state.DummyModeActive ? "Dummy mode active" : "System ready";
            }

            view.DraftFooterLabel.text = state.IsCompleted
                ? "Final teams are set. Preparing the match."
                : string.IsNullOrWhiteSpace(state.FooterText)
                    ? "Role: Waiting for captains. Follow the current pick card above."
                    : state.FooterText;

            PopulatePlayerEntries(view.RedTeamContainer, ResolveEntries(extendedState.RedPlayerEntries, state.RedPlayers, TeamResult.Red), TeamColor(TeamResult.Red), false, null, currentTurnTeam);
            PopulatePlayerEntries(view.BlueTeamContainer, ResolveEntries(extendedState.BluePlayerEntries, state.BluePlayers, TeamResult.Blue), TeamColor(TeamResult.Blue), false, null, currentTurnTeam);
            PopulatePlayerEntries(view.AvailablePlayersContainer, ResolveEntries(extendedState.AvailablePlayerEntries, state.AvailablePlayers, TeamResult.Unknown), new Color(0.24f, 0.28f, 0.14f, 0.74f), true, onPickPlayer, currentTurnTeam);
            PopulatePlayerEntries(view.PendingPlayersContainer, ResolveEntries(extendedState.PendingLateJoinerEntries, state.PendingLateJoiners, TeamResult.Unknown), new Color(0.17f, 0.19f, 0.16f, 0.72f), true, onAcceptLateJoiner, currentTurnTeam);
            UpdateDraftAmbientMotion(view, isLocalTurn, currentTurnTeam);
        }

        public static void UpdateDraftAmbientMotion(View view, bool isLocalTurn, TeamResult currentTurnTeam)
        {
            if (view == null)
            {
                return;
            }

            var pulse = 0.5f + (Mathf.Sin(Time.unscaledTime * (isLocalTurn ? 4.1f : 2.8f)) * 0.5f);
            var turnBorder = isLocalTurn
                ? new Color(0.98f, 0.82f, 0.36f, 0.56f + (pulse * 0.18f))
                : ResolveTurnAccent(currentTurnTeam, 0.32f + (pulse * 0.08f));
            var turnBackground = isLocalTurn
                ? new Color(0.22f, 0.17f, 0.07f, 0.94f)
                : ResolveTurnBackground(currentTurnTeam);

            if (view.DraftTurnCard != null)
            {
                view.DraftTurnCard.style.backgroundColor = new StyleColor(turnBackground);
                ApplyUniformBorder(view.DraftTurnCard, turnBorder);
                view.DraftTurnCard.transform.scale = new Vector3(1f + ((isLocalTurn ? 0.008f : 0.0025f) * pulse), 1f + ((isLocalTurn ? 0.008f : 0.0025f) * pulse), 1f);
            }

            if (view.DraftTurnLabel != null)
            {
                view.DraftTurnLabel.style.color = new StyleColor(isLocalTurn
                    ? new Color(1f, 0.94f, 0.78f, 1f)
                    : new Color(0.97f, 0.98f, 1f, 1f));
            }

            if (view.DraftAvailablePanel != null)
            {
                var availableAccent = isLocalTurn
                    ? new Color(1f, 0.90f, 0.54f, 0.18f + (pulse * 0.08f))
                    : new Color(1f, 0.90f, 0.54f, 0.10f);
                ApplyUniformBorder(view.DraftAvailablePanel, availableAccent);
                view.DraftAvailablePanel.transform.scale = new Vector3(1f + (isLocalTurn ? 0.002f * pulse : 0f), 1f + (isLocalTurn ? 0.002f * pulse : 0f), 1f);
            }

            ApplyCaptainCardPulse(view.DraftRedCaptainCard, currentTurnTeam == TeamResult.Red, pulse);
            ApplyCaptainCardPulse(view.DraftBlueCaptainCard, currentTurnTeam == TeamResult.Blue, pulse);
        }

        private static void BuildVotingUI(View view, Action onVoteAccepted, Action onVoteRejected)
        {
            var panel = new VisualElement();
            panel.name = "VotingPanel";
            panel.style.display = DisplayStyle.None;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.alignSelf = Align.Center;
            panel.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            panel.style.maxWidth = 1000;
            panel.style.paddingTop = new StyleLength(new Length(28, LengthUnit.Pixel));
            panel.style.paddingBottom = new StyleLength(new Length(28, LengthUnit.Pixel));
            panel.style.paddingLeft = new StyleLength(new Length(28, LengthUnit.Pixel));
            panel.style.paddingRight = new StyleLength(new Length(28, LengthUnit.Pixel));
            panel.style.backgroundColor = new StyleColor(new Color(0.04f, 0.07f, 0.10f, 0.96f));
            panel.style.borderTopLeftRadius = new StyleLength(new Length(30, LengthUnit.Pixel));
            panel.style.borderTopRightRadius = new StyleLength(new Length(30, LengthUnit.Pixel));
            panel.style.borderBottomLeftRadius = new StyleLength(new Length(30, LengthUnit.Pixel));
            panel.style.borderBottomRightRadius = new StyleLength(new Length(30, LengthUnit.Pixel));

            var panelOutline = new VisualElement();
            panelOutline.style.position = Position.Absolute;
            panelOutline.style.top = 0;
            panelOutline.style.left = 0;
            panelOutline.style.right = 0;
            panelOutline.style.bottom = 0;
            panelOutline.style.borderTopWidth = 1;
            panelOutline.style.borderRightWidth = 1;
            panelOutline.style.borderBottomWidth = 1;
            panelOutline.style.borderLeftWidth = 1;
            panelOutline.style.borderTopColor = new StyleColor(new Color(0.95f, 0.78f, 0.32f, 0.30f));
            panelOutline.style.borderRightColor = new StyleColor(new Color(0.82f, 0.88f, 0.94f, 0.12f));
            panelOutline.style.borderBottomColor = new StyleColor(new Color(0.95f, 0.78f, 0.32f, 0.18f));
            panelOutline.style.borderLeftColor = new StyleColor(new Color(0.82f, 0.88f, 0.94f, 0.12f));
            panelOutline.style.borderTopLeftRadius = new StyleLength(new Length(30, LengthUnit.Pixel));
            panelOutline.style.borderTopRightRadius = new StyleLength(new Length(30, LengthUnit.Pixel));
            panelOutline.style.borderBottomLeftRadius = new StyleLength(new Length(30, LengthUnit.Pixel));
            panelOutline.style.borderBottomRightRadius = new StyleLength(new Length(30, LengthUnit.Pixel));
            panelOutline.pickingMode = PickingMode.Ignore;

            var topAccent = new VisualElement();
            topAccent.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            topAccent.style.height = 1;
            topAccent.style.marginBottom = new StyleLength(new Length(22, LengthUnit.Pixel));
            topAccent.style.backgroundColor = new StyleColor(new Color(0.93f, 0.77f, 0.30f, 0.82f));

            var headerRow = new VisualElement();
            headerRow.style.display = DisplayStyle.Flex;
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.FlexStart;
            headerRow.style.flexWrap = Wrap.NoWrap;

            var heroColumn = new VisualElement();
            heroColumn.style.display = DisplayStyle.Flex;
            heroColumn.style.flexDirection = FlexDirection.Column;
            heroColumn.style.flexGrow = 1;
            heroColumn.style.flexShrink = 1;
            heroColumn.style.flexBasis = new StyleLength(new Length(0, LengthUnit.Pixel));
            heroColumn.style.minWidth = 360;
            heroColumn.style.marginRight = new StyleLength(new Length(18, LengthUnit.Pixel));

            var headerChip = new VisualElement();
            headerChip.style.alignSelf = Align.FlexStart;
            headerChip.style.paddingTop = new StyleLength(new Length(5, LengthUnit.Pixel));
            headerChip.style.paddingBottom = new StyleLength(new Length(5, LengthUnit.Pixel));
            headerChip.style.paddingLeft = new StyleLength(new Length(14, LengthUnit.Pixel));
            headerChip.style.paddingRight = new StyleLength(new Length(14, LengthUnit.Pixel));
            headerChip.style.backgroundColor = new StyleColor(new Color(0.22f, 0.18f, 0.08f, 0.82f));
            headerChip.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            headerChip.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            headerChip.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            headerChip.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            headerChip.style.borderTopWidth = 1;
            headerChip.style.borderRightWidth = 1;
            headerChip.style.borderBottomWidth = 1;
            headerChip.style.borderLeftWidth = 1;
            headerChip.style.borderTopColor = new StyleColor(new Color(1f, 0.90f, 0.62f, 0.24f));
            headerChip.style.borderRightColor = new StyleColor(new Color(1f, 0.90f, 0.62f, 0.10f));
            headerChip.style.borderBottomColor = new StyleColor(new Color(1f, 0.90f, 0.62f, 0.12f));
            headerChip.style.borderLeftColor = new StyleColor(new Color(1f, 0.90f, 0.62f, 0.10f));
            headerChip.Add(CreateLabel("RANKED VOTE", 10, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.90f, 0.60f, 0.98f)));

            view.VotingTitleLabel = CreateLabel("MATCH FOUND", 36, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.98f, 0.99f, 1f, 1f));
            view.VotingTitleLabel.style.marginTop = new StyleLength(new Length(16, LengthUnit.Pixel));
            view.VotingTitleLabel.style.whiteSpace = WhiteSpace.Normal;
            view.VotingTitleLabel.style.maxWidth = 620;
            view.VotingPromptLabel = CreateLabel("Vote with the buttons below to begin ranked play.", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.78f, 0.84f, 0.90f, 0.96f));
            view.VotingPromptLabel.style.marginTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            view.VotingPromptLabel.style.maxWidth = 520;

            heroColumn.Add(headerChip);
            heroColumn.Add(view.VotingTitleLabel);
            heroColumn.Add(view.VotingPromptLabel);

            var topAside = new VisualElement();
            topAside.style.display = DisplayStyle.Flex;
            topAside.style.flexDirection = FlexDirection.Column;
            topAside.style.width = 280;
            topAside.style.minWidth = 250;
            topAside.style.flexShrink = 0;

            var timerCard = CreateVoteSectionCard();
            timerCard.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            timerCard.style.minWidth = 230;
            timerCard.style.alignSelf = Align.Stretch;

            var timerEyebrow = CreateLabel("COUNTDOWN", 10, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.90f, 0.62f, 0.90f));
            timerEyebrow.style.alignSelf = Align.Center;
            view.VotingTimerLabel = CreateLabel("00:00", 40, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.97f, 0.98f, 1f, 1f));
            view.VotingTimerLabel.style.marginTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            view.VotingTimerLabel.style.alignSelf = Align.Center;
            var readyTrack = CreateVoteProgressTrack(new Color(0.13f, 0.17f, 0.22f, 0.96f), new Color(0.24f, 0.81f, 0.49f, 0.98f), 6, out view.VotingReadyFill);
            readyTrack.style.marginTop = new StyleLength(new Length(14, LengthUnit.Pixel));
            var summaryCard = CreateVoteSectionCard();
            summaryCard.style.marginTop = new StyleLength(new Length(14, LengthUnit.Pixel));
            var summaryEyebrow = CreateLabel("VOTE STATUS", 10, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.90f, 0.62f, 0.90f));
            view.VotingStatsLabel = CreateLabel(string.Empty, 11, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.77f, 0.83f, 0.89f, 0.96f));
            view.VotingStatsLabel.style.display = DisplayStyle.None;
            view.VotingStatsLabel.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            view.VotingStatsLabel.style.alignSelf = Align.FlexStart;
            view.VotingStatsLabel.style.whiteSpace = WhiteSpace.Normal;

            timerCard.Add(timerEyebrow);
            timerCard.Add(view.VotingTimerLabel);
            timerCard.Add(readyTrack);
            summaryCard.Add(summaryEyebrow);
            summaryCard.Add(view.VotingStatsLabel);

            topAside.Add(timerCard);
            topAside.Add(summaryCard);

            headerRow.Add(heroColumn);
            headerRow.Add(topAside);

            var contentRow = new VisualElement();
            contentRow.style.display = DisplayStyle.Flex;
            contentRow.style.flexDirection = FlexDirection.Row;
            contentRow.style.flexWrap = Wrap.Wrap;
            contentRow.style.alignItems = Align.Stretch;
            contentRow.style.marginTop = new StyleLength(new Length(24, LengthUnit.Pixel));

            var playersSection = CreateVoteSectionCard();
            playersSection.style.flexGrow = 1;
            playersSection.style.flexShrink = 1;
            playersSection.style.flexBasis = new StyleLength(new Length(0, LengthUnit.Pixel));
            playersSection.style.minWidth = 460;
            playersSection.style.marginRight = new StyleLength(new Length(16, LengthUnit.Pixel));

            var rosterHeader = new VisualElement();
            rosterHeader.style.display = DisplayStyle.Flex;
            rosterHeader.style.flexDirection = FlexDirection.Row;
            rosterHeader.style.justifyContent = Justify.SpaceBetween;
            rosterHeader.style.alignItems = Align.Center;

            var rosterTitle = CreateLabel("MATCH LOBBY", 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.97f, 0.92f, 0.98f));
            var rosterHint = CreateLabel("Ready / Waiting / Declined", 11, FontStyle.Normal, TextAnchor.MiddleRight, new Color(0.74f, 0.81f, 0.88f, 0.90f));
            rosterHeader.Add(rosterTitle);
            rosterHeader.Add(rosterHint);

            var rosterColumns = new VisualElement();
            rosterColumns.style.display = DisplayStyle.Flex;
            rosterColumns.style.flexDirection = FlexDirection.Row;
            rosterColumns.style.flexWrap = Wrap.Wrap;
            rosterColumns.style.alignItems = Align.FlexStart;
            rosterColumns.style.marginTop = new StyleLength(new Length(14, LengthUnit.Pixel));

            view.VotingPlayersLeftColumn = new VisualElement();
            view.VotingPlayersLeftColumn.style.display = DisplayStyle.Flex;
            view.VotingPlayersLeftColumn.style.flexDirection = FlexDirection.Column;
            view.VotingPlayersLeftColumn.style.flexGrow = 1;
            view.VotingPlayersLeftColumn.style.flexShrink = 1;
            view.VotingPlayersLeftColumn.style.flexBasis = new StyleLength(new Length(0, LengthUnit.Pixel));
            view.VotingPlayersLeftColumn.style.minWidth = 220;

            view.VotingPlayersRightColumn = new VisualElement();
            view.VotingPlayersRightColumn.style.display = DisplayStyle.Flex;
            view.VotingPlayersRightColumn.style.flexDirection = FlexDirection.Column;
            view.VotingPlayersRightColumn.style.flexGrow = 1;
            view.VotingPlayersRightColumn.style.flexShrink = 1;
            view.VotingPlayersRightColumn.style.flexBasis = new StyleLength(new Length(0, LengthUnit.Pixel));
            view.VotingPlayersRightColumn.style.minWidth = 220;
            view.VotingPlayersRightColumn.style.marginLeft = new StyleLength(new Length(12, LengthUnit.Pixel));

            rosterColumns.Add(view.VotingPlayersLeftColumn);
            rosterColumns.Add(view.VotingPlayersRightColumn);
            playersSection.Add(rosterHeader);
            playersSection.Add(rosterColumns);

            var decisionColumn = CreateVoteSectionCard();
            decisionColumn.style.width = 240;
            decisionColumn.style.minWidth = 220;
            decisionColumn.style.alignSelf = Align.Stretch;

            var decisionEyebrow = CreateLabel("CAST VOTE", 10, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.90f, 0.62f, 0.90f));
            var decisionHint = CreateLabel("One choice locks your response.", 12, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.76f, 0.83f, 0.89f, 0.94f));
            decisionHint.style.marginTop = new StyleLength(new Length(6, LengthUnit.Pixel));

            view.VotingAcceptButton = new Button(() => onVoteAccepted?.Invoke());
            view.VotingAcceptButton.text = string.Empty;
            StyleButton(view.VotingAcceptButton, new ButtonPalette(
                new Color(0.15f, 0.47f, 0.31f, 0.98f),
                new Color(0.21f, 0.59f, 0.39f, 1f),
                new Color(0.11f, 0.37f, 0.24f, 1f),
                Color.white));
            view.VotingAcceptButton.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            view.VotingAcceptButton.style.height = 78;
            view.VotingAcceptButton.style.flexGrow = 0;
            view.VotingAcceptButton.style.flexShrink = 0;
            view.VotingAcceptButton.style.marginTop = new StyleLength(new Length(18, LengthUnit.Pixel));
            view.VotingAcceptButton.style.marginBottom = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.VotingAcceptButton.style.justifyContent = Justify.Center;
            view.VotingAcceptButton.style.alignItems = Align.Center;
            view.VotingAcceptButton.style.borderTopLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.VotingAcceptButton.style.borderTopRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.VotingAcceptButton.style.borderBottomLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.VotingAcceptButton.style.borderBottomRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));

            var acceptTextStack = new VisualElement();
            acceptTextStack.style.display = DisplayStyle.Flex;
            acceptTextStack.style.flexDirection = FlexDirection.Column;
            acceptTextStack.style.alignItems = Align.Center;
            acceptTextStack.style.justifyContent = Justify.Center;
            view.VotingAcceptTitleLabel = CreateLabel("VOTE YES", 16, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            view.VotingAcceptSubtitleLabel = CreateLabel(string.Empty, 11, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white);
            view.VotingAcceptSubtitleLabel.style.display = DisplayStyle.None;
            acceptTextStack.Add(view.VotingAcceptTitleLabel);
            acceptTextStack.Add(view.VotingAcceptSubtitleLabel);
            view.VotingAcceptButton.Add(acceptTextStack);

            view.VotingRejectButton = new Button(() => onVoteRejected?.Invoke());
            view.VotingRejectButton.text = string.Empty;
            StyleButton(view.VotingRejectButton, new ButtonPalette(
                new Color(0.55f, 0.20f, 0.20f, 0.98f),
                new Color(0.67f, 0.25f, 0.25f, 1f),
                new Color(0.44f, 0.16f, 0.16f, 1f),
                Color.white));
            view.VotingRejectButton.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            view.VotingRejectButton.style.height = 78;
            view.VotingRejectButton.style.flexGrow = 0;
            view.VotingRejectButton.style.flexShrink = 0;
            view.VotingRejectButton.style.justifyContent = Justify.Center;
            view.VotingRejectButton.style.alignItems = Align.Center;
            view.VotingRejectButton.style.marginBottom = 0;
            view.VotingRejectButton.style.borderTopLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.VotingRejectButton.style.borderTopRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.VotingRejectButton.style.borderBottomLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.VotingRejectButton.style.borderBottomRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));

            var rejectTextStack = new VisualElement();
            rejectTextStack.style.display = DisplayStyle.Flex;
            rejectTextStack.style.flexDirection = FlexDirection.Column;
            rejectTextStack.style.alignItems = Align.Center;
            rejectTextStack.style.justifyContent = Justify.Center;
            view.VotingRejectTitleLabel = CreateLabel("VOTE NO", 16, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            view.VotingRejectSubtitleLabel = CreateLabel(string.Empty, 11, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white);
            view.VotingRejectSubtitleLabel.style.display = DisplayStyle.None;
            rejectTextStack.Add(view.VotingRejectTitleLabel);
            rejectTextStack.Add(view.VotingRejectSubtitleLabel);
            view.VotingRejectButton.Add(rejectTextStack);

            decisionColumn.Add(decisionEyebrow);
            decisionColumn.Add(decisionHint);
            decisionColumn.Add(view.VotingAcceptButton);
            decisionColumn.Add(view.VotingRejectButton);

            var countdownTrack = CreateVoteProgressTrack(new Color(0.14f, 0.16f, 0.19f, 0.96f), new Color(0.91f, 0.74f, 0.28f, 0.98f), 4, out view.VotingCountdownFill);
            countdownTrack.style.display = DisplayStyle.None;
            view.VotingFooterLabel = CreateLabel(string.Empty, 11, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.88f, 0.92f, 0.97f, 0.96f));
            view.VotingFooterLabel.style.display = DisplayStyle.None;

            contentRow.Add(playersSection);
            contentRow.Add(decisionColumn);

            panel.Add(topAccent);
            panel.Add(headerRow);
            panel.Add(contentRow);
            panel.Add(panelOutline);

            view.Root.Add(panel);
            view.VotingPanel = panel;
        }

        private static VisualElement CreateVoteSectionCard()
        {
            var card = new VisualElement();
            card.style.display = DisplayStyle.Flex;
            card.style.flexDirection = FlexDirection.Column;
            card.style.paddingTop = new StyleLength(new Length(16, LengthUnit.Pixel));
            card.style.paddingBottom = new StyleLength(new Length(16, LengthUnit.Pixel));
            card.style.paddingLeft = new StyleLength(new Length(16, LengthUnit.Pixel));
            card.style.paddingRight = new StyleLength(new Length(16, LengthUnit.Pixel));
            card.style.backgroundColor = new StyleColor(new Color(0.08f, 0.11f, 0.15f, 0.92f));
            card.style.borderTopLeftRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            card.style.borderTopRightRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            card.style.borderBottomLeftRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            card.style.borderBottomRightRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            card.style.borderTopWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderTopColor = new StyleColor(new Color(0.95f, 0.78f, 0.32f, 0.14f));
            card.style.borderRightColor = new StyleColor(new Color(0.80f, 0.86f, 0.92f, 0.08f));
            card.style.borderBottomColor = new StyleColor(new Color(0.80f, 0.86f, 0.92f, 0.10f));
            card.style.borderLeftColor = new StyleColor(new Color(0.80f, 0.86f, 0.92f, 0.08f));
            return card;
        }

        private static VisualElement CreateVoteProgressTrack(Color trackColor, Color fillColor, int height, out VisualElement fill)
        {
            var track = new VisualElement();
            track.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            track.style.height = height;
            track.style.backgroundColor = new StyleColor(trackColor);
            track.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            track.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            track.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            track.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            track.style.overflow = Overflow.Hidden;

            fill = new VisualElement();
            fill.style.width = 0;
            fill.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
            fill.style.backgroundColor = new StyleColor(fillColor);
            fill.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            fill.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            fill.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            fill.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            track.Add(fill);
            return track;
        }

        private static void ConfigureVoteActionButton(Button button, Label titleLabel, Label subtitleLabel, string title, string subtitle, bool isLocked, bool isSelected)
        {
            if (button == null || titleLabel == null || subtitleLabel == null)
            {
                return;
            }

            titleLabel.text = title ?? string.Empty;
            subtitleLabel.text = subtitle ?? string.Empty;
            subtitleLabel.style.display = string.IsNullOrWhiteSpace(subtitle) ? DisplayStyle.None : DisplayStyle.Flex;
            button.style.opacity = isSelected ? 1f : isLocked ? 0.48f : 1f;
            button.style.transformOrigin = new TransformOrigin(50, 50, 0);
            titleLabel.style.color = new StyleColor(isSelected ? Color.white : new Color(0.96f, 0.97f, 0.99f, 1f));
            subtitleLabel.style.color = new StyleColor(isSelected
                ? new Color(0.96f, 0.98f, 0.99f, 1f)
                : isLocked
                    ? new Color(0.78f, 0.83f, 0.88f, 1f)
                    : new Color(0.86f, 0.91f, 0.95f, 1f));
        }

        private static string FormatVoteCountdown(float remainingSeconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
            var minutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;
            return $"{minutes:00}:{seconds:00}";
        }

        private static void ApplyVoteAmbientMotion(View view, bool hasAccepted, bool hasRejected, float pulse)
        {
            if (view == null)
            {
                return;
            }

            var acceptScale = hasAccepted ? 1.02f + (pulse * 0.01f) : 1f + (pulse * 0.012f);
            var rejectScale = hasRejected ? 1.02f + (pulse * 0.01f) : 1f + ((1f - pulse) * 0.012f);
            if (view.VotingAcceptButton != null)
            {
                view.VotingAcceptButton.transform.scale = new Vector3(acceptScale, acceptScale, 1f);
            }

            if (view.VotingRejectButton != null)
            {
                view.VotingRejectButton.transform.scale = new Vector3(rejectScale, rejectScale, 1f);
            }
        }

        private static void PopulateVotePlayerEntries(View view, VoteOverlayPlayerEntryMessage[] entries)
        {
            if (view?.VotingPlayersLeftColumn == null || view.VotingPlayersRightColumn == null)
            {
                return;
            }

            view.VotingPlayersLeftColumn.Clear();
            view.VotingPlayersRightColumn.Clear();

            var orderedEntries = (entries ?? Array.Empty<VoteOverlayPlayerEntryMessage>())
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.DisplayName))
                .GroupBy(entry => entry.PlayerId ?? entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();

            if (orderedEntries.Length == 0)
            {
                var emptyLabel = CreateLabel("Waiting for player responses", 13, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.74f, 0.80f, 0.86f, 1f));
                emptyLabel.style.opacity = 0.82f;
                view.VotingPlayersLeftColumn.Add(emptyLabel);
                return;
            }

            var splitIndex = Mathf.CeilToInt(orderedEntries.Length / 2f);
            for (var index = 0; index < orderedEntries.Length; index++)
            {
                var targetColumn = index < splitIndex ? view.VotingPlayersLeftColumn : view.VotingPlayersRightColumn;
                targetColumn.Add(CreateVotePlayerCard(orderedEntries[index]));
            }
        }

        private static VisualElement CreateVotePlayerCard(VoteOverlayPlayerEntryMessage entry)
        {
            var card = new VisualElement();
            card.style.display = DisplayStyle.Flex;
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.Center;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.paddingTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            card.style.paddingBottom = new StyleLength(new Length(10, LengthUnit.Pixel));
            card.style.paddingLeft = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.paddingRight = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            card.style.backgroundColor = new StyleColor(ResolveVoteCardBackground(entry));
            card.style.borderTopLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.borderTopRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.borderBottomLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.borderBottomRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.borderTopWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderTopColor = new StyleColor(ResolveVoteCardBorder(entry));
            card.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.07f));
            card.style.borderBottomColor = new StyleColor(new Color(1f, 1f, 1f, 0.09f));
            card.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.07f));

            var leftSide = new VisualElement();
            leftSide.style.display = DisplayStyle.Flex;
            leftSide.style.flexDirection = FlexDirection.Row;
            leftSide.style.alignItems = Align.Center;
            leftSide.style.flexGrow = 1;

            leftSide.Add(CreateVoteAvatarFrame(entry));

            var textColumn = new VisualElement();
            textColumn.style.display = DisplayStyle.Flex;
            textColumn.style.flexDirection = FlexDirection.Column;
            textColumn.style.flexGrow = 1;
            textColumn.style.overflow = Overflow.Hidden;

            var nameLabel = CreateLabel(FormatVotePlayerName(entry), 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.99f, 0.99f, 1f, 1f));
            nameLabel.style.whiteSpace = WhiteSpace.Normal;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.flexShrink = 1;
            var detailLabel = CreateLabel(ResolveVoteDetailText(entry), 10, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.74f, 0.81f, 0.88f, 0.92f));
            detailLabel.style.whiteSpace = WhiteSpace.Normal;
            detailLabel.style.overflow = Overflow.Hidden;
            detailLabel.style.marginTop = new StyleLength(new Length(2, LengthUnit.Pixel));

            textColumn.Add(nameLabel);
            textColumn.Add(detailLabel);
            leftSide.Add(textColumn);

            card.Add(leftSide);
            card.Add(CreateVoteStatusPill(entry));
            return card;
        }

        private static VisualElement CreateVoteAvatarFrame(VoteOverlayPlayerEntryMessage entry)
        {
            var frame = new VisualElement();
            frame.style.width = 38;
            frame.style.height = 38;
            frame.style.flexShrink = 0;
            frame.style.marginRight = new StyleLength(new Length(12, LengthUnit.Pixel));
            frame.style.alignItems = Align.Center;
            frame.style.justifyContent = Justify.Center;
            frame.style.backgroundColor = new StyleColor(ResolveVoteAccent(entry));
            frame.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            frame.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            frame.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            frame.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            frame.style.borderTopWidth = 1;
            frame.style.borderRightWidth = 1;
            frame.style.borderBottomWidth = 1;
            frame.style.borderLeftWidth = 1;
            frame.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.18f));
            frame.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.10f));
            frame.style.borderBottomColor = new StyleColor(new Color(1f, 1f, 1f, 0.12f));
            frame.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.10f));
            frame.style.overflow = Overflow.Hidden;

            BindAvatarFrameContent(frame, entry?.SteamId, ResolveVoteAvatarInitial(entry), 16);

            return frame;
        }

        private static void BindAvatarFrameContent(VisualElement frame, string avatarSteamId, string fallbackText, int fontSize)
        {
            Debug.Log($"[AVATAR] UI bind request. context=draft requested={avatarSteamId ?? "none"} fallback={fallbackText ?? "?"}");
            if (TryApplyAvatarFrameContent(frame, avatarSteamId, fallbackText, fontSize))
            {
                Debug.Log($"[AVATAR] UI bind success. context=draft steamId={avatarSteamId ?? "none"} attempts=0");
                return;
            }

            if (string.IsNullOrWhiteSpace(avatarSteamId))
            {
                Debug.LogWarning($"[AVATAR] UI bind failed. context=draft reason=missing-steam-id fallback={fallbackText ?? "?"}");
                return;
            }

            var attempts = 0;
            IVisualElementScheduledItem refreshItem = null;
            refreshItem = frame.schedule.Execute(() =>
            {
                if (frame.panel == null)
                {
                    refreshItem.Pause();
                    return;
                }

                attempts++;
                if (TryApplyAvatarFrameContent(frame, avatarSteamId, fallbackText, fontSize)
                    || attempts >= AvatarRefreshMaxAttempts)
                {
                    if (frame.panel != null)
                    {
                        Debug.Log($"[AVATAR] UI bind {(attempts >= AvatarRefreshMaxAttempts ? "stopped" : "success")}. context=draft steamId={avatarSteamId} attempts={attempts}");
                    }
                    refreshItem.Pause();
                }
            }).Every(AvatarRefreshIntervalMilliseconds);
        }

        private static bool TryApplyAvatarFrameContent(VisualElement frame, string avatarSteamId, string fallbackText, int fontSize)
        {
            frame.Clear();

            if (!string.IsNullOrWhiteSpace(avatarSteamId)
                && VoteAvatarCache.TryGetAvatarTexture(avatarSteamId, out var avatarTexture)
                && avatarTexture != null)
            {
                var avatarImage = new Image();
                avatarImage.image = avatarTexture;
                avatarImage.scaleMode = ScaleMode.ScaleAndCrop;
                avatarImage.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
                avatarImage.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
                frame.Add(avatarImage);
                return true;
            }

            frame.Add(CreateLabel(fallbackText, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.98f, 0.99f, 1f, 1f)));
            return false;
        }

        private static VisualElement CreateVoteStatusPill(VoteOverlayPlayerEntryMessage entry)
        {
            var pill = new VisualElement();
            pill.style.display = DisplayStyle.Flex;
            pill.style.flexDirection = FlexDirection.Column;
            pill.style.alignItems = Align.Center;
            pill.style.justifyContent = Justify.Center;
            pill.style.marginLeft = new StyleLength(new Length(10, LengthUnit.Pixel));
            pill.style.paddingTop = new StyleLength(new Length(5, LengthUnit.Pixel));
            pill.style.paddingBottom = new StyleLength(new Length(5, LengthUnit.Pixel));
            pill.style.paddingLeft = new StyleLength(new Length(10, LengthUnit.Pixel));
            pill.style.paddingRight = new StyleLength(new Length(10, LengthUnit.Pixel));
            pill.style.backgroundColor = new StyleColor(ResolveVoteStatusBackground(entry));
            pill.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            pill.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            pill.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            pill.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            pill.style.flexShrink = 0;
            pill.style.borderTopWidth = 1;
            pill.style.borderRightWidth = 1;
            pill.style.borderBottomWidth = 1;
            pill.style.borderLeftWidth = 1;
            pill.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.12f));
            pill.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
            pill.style.borderBottomColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));
            pill.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
            pill.Add(CreateLabel(ResolveVoteStatusText(entry), 9, FontStyle.Bold, TextAnchor.MiddleCenter, ResolveVoteStatusTextColor(entry)));
            return pill;
        }

        private static string BuildVotePlayerSignature(VoteOverlayPlayerEntryMessage entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            return string.Join("~",
                entry.ClientId,
                entry.PlayerId ?? string.Empty,
                entry.SteamId ?? string.Empty,
                entry.DisplayName ?? string.Empty,
                entry.PlayerNumber,
                entry.HasVoted,
                entry.VoteAccepted,
                entry.IsInitiator);
        }

        private static string FormatVotePlayerName(VoteOverlayPlayerEntryMessage entry)
        {
            if (entry == null)
            {
                return "Player";
            }

            var displayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? "Player"
                : NormalizeDraftIdentityText(entry.DisplayName, entry.PlayerNumber);
            if (entry.PlayerNumber > 0)
            {
                var playerPrefix = $"#{entry.PlayerNumber}";
                if (!HasLeadingPlayerNumber(displayName, entry.PlayerNumber))
                {
                    displayName = $"{playerPrefix} {displayName}";
                }
            }

            return displayName;
        }

        private static string ResolveVoteDetailText(VoteOverlayPlayerEntryMessage entry)
        {
            if (entry == null)
            {
                return "Waiting for response";
            }

            if (entry.IsInitiator && entry.HasVoted)
            {
                return entry.VoteAccepted ? "Vote starter  ·  Ready" : "Vote starter  ·  Declined";
            }

            if (entry.IsInitiator)
            {
                return "Vote starter  ·  Waiting";
            }

            if (!entry.HasVoted)
            {
                return "Awaiting response";
            }

            return entry.VoteAccepted ? "Ready for ranked" : "Declined this vote";
        }

        private static string ResolveVoteStatusText(VoteOverlayPlayerEntryMessage entry)
        {
            if (entry == null || !entry.HasVoted)
            {
                return "WAITING";
            }

            return entry.VoteAccepted ? "READY" : "DECLINED";
        }

        private static string ResolveVoteAvatarInitial(VoteOverlayPlayerEntryMessage entry)
        {
            var displayName = FormatVotePlayerName(entry);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "?";
            }

            var trimmed = displayName.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? "?" : trimmed.Substring(0, 1).ToUpperInvariant();
        }

        private static Color ResolveVoteCardBackground(VoteOverlayPlayerEntryMessage entry)
        {
            if (entry == null || !entry.HasVoted)
            {
                return new Color(0.09f, 0.12f, 0.16f, 0.94f);
            }

            return entry.VoteAccepted
                ? new Color(0.09f, 0.18f, 0.14f, 0.94f)
                : new Color(0.18f, 0.10f, 0.10f, 0.94f);
        }

        private static Color ResolveVoteCardBorder(VoteOverlayPlayerEntryMessage entry)
        {
            if (entry == null || !entry.HasVoted)
            {
                return new Color(0.92f, 0.76f, 0.28f, 0.18f);
            }

            return entry.VoteAccepted
                ? new Color(0.34f, 0.82f, 0.53f, 0.28f)
                : new Color(0.92f, 0.42f, 0.42f, 0.28f);
        }

        private static Color ResolveVoteAccent(VoteOverlayPlayerEntryMessage entry)
        {
            if (entry == null || !entry.HasVoted)
            {
                return new Color(0.66f, 0.52f, 0.18f, 0.84f);
            }

            return entry.VoteAccepted
                ? new Color(0.19f, 0.61f, 0.39f, 0.90f)
                : new Color(0.68f, 0.24f, 0.24f, 0.90f);
        }

        private static Color ResolveVoteStatusBackground(VoteOverlayPlayerEntryMessage entry)
        {
            if (entry == null || !entry.HasVoted)
            {
                return new Color(0.36f, 0.28f, 0.08f, 0.84f);
            }

            return entry.VoteAccepted
                ? new Color(0.14f, 0.41f, 0.27f, 0.88f)
                : new Color(0.48f, 0.18f, 0.18f, 0.88f);
        }

        private static Color ResolveVoteStatusTextColor(VoteOverlayPlayerEntryMessage entry)
        {
            if (entry == null || !entry.HasVoted)
            {
                return new Color(1f, 0.90f, 0.32f, 1f);
            }

            return entry.VoteAccepted
                ? new Color(0.90f, 1f, 0.95f, 1f)
                : new Color(1f, 0.93f, 0.93f, 1f);
        }

        private static void BuildApprovalUI(View view, Action onApprovalAccepted, Action onApprovalRejected)
        {
            var panel = new VisualElement();
            panel.name = "ApprovalPanel";
            panel.style.display = DisplayStyle.None;
            panel.style.position = Position.Absolute;
            panel.style.right = 26;
            panel.style.bottom = 26;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.width = 432;
            panel.style.maxWidth = 432;
            panel.style.paddingTop = new StyleLength(new Length(20, LengthUnit.Pixel));
            panel.style.paddingBottom = new StyleLength(new Length(20, LengthUnit.Pixel));
            panel.style.paddingLeft = new StyleLength(new Length(20, LengthUnit.Pixel));
            panel.style.paddingRight = new StyleLength(new Length(20, LengthUnit.Pixel));
            panel.style.backgroundColor = new StyleColor(new Color(0.06f, 0.08f, 0.11f, 0.97f));
            panel.style.borderTopLeftRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            panel.style.borderTopRightRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            panel.style.borderBottomLeftRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            panel.style.borderBottomRightRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            panel.style.borderTopWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            ApplyUniformBorder(panel, new Color(1f, 1f, 1f, 0.08f));
            panel.pickingMode = PickingMode.Position;

            var headerRow = new VisualElement();
            headerRow.style.display = DisplayStyle.Flex;
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.justifyContent = Justify.SpaceBetween;

            view.ApprovalStatusChip = new VisualElement();
            view.ApprovalStatusChip.style.paddingTop = new StyleLength(new Length(6, LengthUnit.Pixel));
            view.ApprovalStatusChip.style.paddingBottom = new StyleLength(new Length(6, LengthUnit.Pixel));
            view.ApprovalStatusChip.style.paddingLeft = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.ApprovalStatusChip.style.paddingRight = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.ApprovalStatusChip.style.backgroundColor = new StyleColor(new Color(0.76f, 0.58f, 0.18f, 0.15f));
            view.ApprovalStatusChip.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            view.ApprovalStatusChip.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            view.ApprovalStatusChip.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            view.ApprovalStatusChip.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            view.ApprovalStatusChip.style.borderTopWidth = 1;
            view.ApprovalStatusChip.style.borderRightWidth = 1;
            view.ApprovalStatusChip.style.borderBottomWidth = 1;
            view.ApprovalStatusChip.style.borderLeftWidth = 1;
            ApplyUniformBorder(view.ApprovalStatusChip, new Color(1f, 0.90f, 0.54f, 0.18f));

            view.ApprovalStatusLabel = CreateLabel("PENDING", 11, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.68f, 1f));
            view.ApprovalStatusChip.Add(view.ApprovalStatusLabel);

            view.ApprovalTimerLabel = CreateLabel(string.Empty, 14, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.90f, 0.95f, 1f, 1f));
            view.ApprovalTimerLabel.style.display = DisplayStyle.None;

            headerRow.Add(view.ApprovalStatusChip);
            headerRow.Add(view.ApprovalTimerLabel);

            view.ApprovalTitleLabel = CreateLabel("TEAM APPROVAL REQUIRED", 19, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.98f, 0.99f, 1f, 1f));
            view.ApprovalTitleLabel.style.marginTop = new StyleLength(new Length(16, LengthUnit.Pixel));
            view.ApprovalPlayerNameLabel = CreateLabel("Player", 21, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.95f, 0.82f, 1f));
            view.ApprovalPlayerNameLabel.style.marginTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            view.ApprovalPromptLabel = CreateLabel(string.Empty, 13, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.80f, 0.86f, 0.92f, 1f));
            view.ApprovalPromptLabel.style.marginTop = new StyleLength(new Length(8, LengthUnit.Pixel));

            view.ApprovalMetaCard = new VisualElement();
            view.ApprovalMetaCard.style.marginTop = new StyleLength(new Length(14, LengthUnit.Pixel));
            view.ApprovalMetaCard.style.paddingTop = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.ApprovalMetaCard.style.paddingBottom = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.ApprovalMetaCard.style.paddingLeft = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.ApprovalMetaCard.style.paddingRight = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.ApprovalMetaCard.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.04f));
            view.ApprovalMetaCard.style.borderTopLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            view.ApprovalMetaCard.style.borderTopRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            view.ApprovalMetaCard.style.borderBottomLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            view.ApprovalMetaCard.style.borderBottomRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            view.ApprovalMetaCard.style.borderTopWidth = 1;
            view.ApprovalMetaCard.style.borderRightWidth = 1;
            view.ApprovalMetaCard.style.borderBottomWidth = 1;
            view.ApprovalMetaCard.style.borderLeftWidth = 1;
            ApplyUniformBorder(view.ApprovalMetaCard, new Color(1f, 1f, 1f, 0.06f));

            view.ApprovalMetaLabel = CreateLabel(string.Empty, 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.79f, 0.88f, 0.96f, 1f));
            view.ApprovalMetaCard.Add(view.ApprovalMetaLabel);

            view.ApprovalButtonRow = new VisualElement();
            view.ApprovalButtonRow.style.display = DisplayStyle.Flex;
            view.ApprovalButtonRow.style.flexDirection = FlexDirection.Row;
            view.ApprovalButtonRow.style.justifyContent = Justify.SpaceBetween;
            view.ApprovalButtonRow.style.alignItems = Align.Stretch;
            view.ApprovalButtonRow.style.marginTop = new StyleLength(new Length(16, LengthUnit.Pixel));
            view.ApprovalButtonRow.style.flexWrap = Wrap.NoWrap;

            view.ApprovalAcceptButton = new Button(() => onApprovalAccepted?.Invoke());
            view.ApprovalAcceptButton.text = string.Empty;
            StyleButton(view.ApprovalAcceptButton, new ButtonPalette(
                new Color(0.20f, 0.48f, 0.34f, 0.98f),
                new Color(0.26f, 0.60f, 0.41f, 1f),
                new Color(0.15f, 0.39f, 0.27f, 1f),
                Color.white));
            view.ApprovalAcceptButton.style.width = 186;
            view.ApprovalAcceptButton.style.height = 64;
            view.ApprovalAcceptButton.style.marginBottom = 0;
            view.ApprovalAcceptButton.style.alignItems = Align.Center;
            view.ApprovalAcceptButton.style.justifyContent = Justify.Center;
            view.ApprovalAcceptButton.style.flexDirection = FlexDirection.Column;
            view.ApprovalAcceptButton.style.whiteSpace = WhiteSpace.NoWrap;
            view.ApprovalAcceptButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            view.ApprovalAcceptButton.style.paddingLeft = new StyleLength(new Length(16, LengthUnit.Pixel));
            view.ApprovalAcceptButton.style.paddingRight = new StyleLength(new Length(16, LengthUnit.Pixel));
            ApplyUniformBorder(view.ApprovalAcceptButton, new Color(0.78f, 1f, 0.88f, 0.20f));

            var approvalAcceptTextStack = new VisualElement();
            approvalAcceptTextStack.style.display = DisplayStyle.Flex;
            approvalAcceptTextStack.style.flexDirection = FlexDirection.Column;
            approvalAcceptTextStack.style.alignItems = Align.Center;
            approvalAcceptTextStack.style.justifyContent = Justify.Center;
            var approvalAcceptTitle = CreateLabel("APPROVE", 14, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            var approvalAcceptSubtitle = CreateLabel("Press Enter", 11, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.88f, 1f, 0.92f, 0.96f));
            approvalAcceptSubtitle.style.marginTop = new StyleLength(new Length(1, LengthUnit.Pixel));
            approvalAcceptTextStack.Add(approvalAcceptTitle);
            approvalAcceptTextStack.Add(approvalAcceptSubtitle);
            view.ApprovalAcceptButton.Add(approvalAcceptTextStack);

            view.ApprovalRejectButton = new Button(() => onApprovalRejected?.Invoke());
            view.ApprovalRejectButton.text = string.Empty;
            StyleButton(view.ApprovalRejectButton, new ButtonPalette(
                new Color(0.58f, 0.22f, 0.22f, 0.98f),
                new Color(0.70f, 0.28f, 0.28f, 1f),
                new Color(0.47f, 0.17f, 0.17f, 1f),
                Color.white));
            view.ApprovalRejectButton.style.width = 186;
            view.ApprovalRejectButton.style.height = 64;
            view.ApprovalRejectButton.style.marginBottom = 0;
            view.ApprovalRejectButton.style.alignItems = Align.Center;
            view.ApprovalRejectButton.style.justifyContent = Justify.Center;
            view.ApprovalRejectButton.style.flexDirection = FlexDirection.Column;
            view.ApprovalRejectButton.style.whiteSpace = WhiteSpace.NoWrap;
            view.ApprovalRejectButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            view.ApprovalRejectButton.style.paddingLeft = new StyleLength(new Length(16, LengthUnit.Pixel));
            view.ApprovalRejectButton.style.paddingRight = new StyleLength(new Length(16, LengthUnit.Pixel));
            ApplyUniformBorder(view.ApprovalRejectButton, new Color(1f, 0.82f, 0.82f, 0.20f));
            view.ApprovalRejectButton.style.marginLeft = new StyleLength(new Length(12, LengthUnit.Pixel));

            var approvalRejectTextStack = new VisualElement();
            approvalRejectTextStack.style.display = DisplayStyle.Flex;
            approvalRejectTextStack.style.flexDirection = FlexDirection.Column;
            approvalRejectTextStack.style.alignItems = Align.Center;
            approvalRejectTextStack.style.justifyContent = Justify.Center;
            var approvalRejectTitle = CreateLabel("REJECT", 14, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            var approvalRejectSubtitle = CreateLabel("Press Delete", 11, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(1f, 0.91f, 0.91f, 0.96f));
            approvalRejectSubtitle.style.marginTop = new StyleLength(new Length(1, LengthUnit.Pixel));
            approvalRejectTextStack.Add(approvalRejectTitle);
            approvalRejectTextStack.Add(approvalRejectSubtitle);
            view.ApprovalRejectButton.Add(approvalRejectTextStack);

            view.ApprovalButtonRow.Add(view.ApprovalAcceptButton);
            view.ApprovalButtonRow.Add(view.ApprovalRejectButton);

            view.ApprovalFooterLabel = CreateLabel(string.Empty, 12, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.71f, 0.80f, 0.86f, 1f));
            view.ApprovalFooterLabel.style.marginTop = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.ApprovalFooterLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

            view.ApprovalHintLabel = CreateLabel(string.Empty, 11, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.95f, 0.80f, 1f));
            view.ApprovalHintLabel.style.marginTop = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.ApprovalHintLabel.style.paddingTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            view.ApprovalHintLabel.style.paddingBottom = new StyleLength(new Length(10, LengthUnit.Pixel));
            view.ApprovalHintLabel.style.paddingLeft = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.ApprovalHintLabel.style.paddingRight = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.ApprovalHintLabel.style.backgroundColor = new StyleColor(new Color(0.32f, 0.24f, 0.08f, 0.32f));
            view.ApprovalHintLabel.style.borderTopLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.ApprovalHintLabel.style.borderTopRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.ApprovalHintLabel.style.borderBottomLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.ApprovalHintLabel.style.borderBottomRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.ApprovalHintLabel.style.borderTopWidth = 1;
            view.ApprovalHintLabel.style.borderRightWidth = 1;
            view.ApprovalHintLabel.style.borderBottomWidth = 1;
            view.ApprovalHintLabel.style.borderLeftWidth = 1;
            ApplyUniformBorder(view.ApprovalHintLabel, new Color(1f, 0.82f, 0.36f, 0.18f));

            panel.Add(headerRow);
            panel.Add(view.ApprovalTitleLabel);
            panel.Add(view.ApprovalPlayerNameLabel);
            panel.Add(view.ApprovalPromptLabel);
            panel.Add(view.ApprovalMetaCard);
            panel.Add(view.ApprovalButtonRow);
            panel.Add(view.ApprovalFooterLabel);
            panel.Add(view.ApprovalHintLabel);

            view.ApprovalPopupLayer.Add(panel);
            view.ApprovalPanel = panel;
        }

        private static void BuildDraftUI(View view)
        {
            var panel = new VisualElement();
            panel.name = "DraftPanel";
            panel.style.display = DisplayStyle.None;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            panel.style.maxWidth = 1080;
            panel.style.alignSelf = Align.Center;

            var boardShell = new VisualElement();
            boardShell.style.display = DisplayStyle.Flex;
            boardShell.style.flexDirection = FlexDirection.Column;
            boardShell.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            boardShell.style.paddingTop = new StyleLength(new Length(22, LengthUnit.Pixel));
            boardShell.style.paddingBottom = new StyleLength(new Length(20, LengthUnit.Pixel));
            boardShell.style.paddingLeft = new StyleLength(new Length(24, LengthUnit.Pixel));
            boardShell.style.paddingRight = new StyleLength(new Length(24, LengthUnit.Pixel));
            boardShell.style.backgroundColor = new StyleColor(new Color(0.04f, 0.06f, 0.08f, 0.96f));
            boardShell.style.borderTopLeftRadius = new StyleLength(new Length(28, LengthUnit.Pixel));
            boardShell.style.borderTopRightRadius = new StyleLength(new Length(28, LengthUnit.Pixel));
            boardShell.style.borderBottomLeftRadius = new StyleLength(new Length(28, LengthUnit.Pixel));
            boardShell.style.borderBottomRightRadius = new StyleLength(new Length(28, LengthUnit.Pixel));
            boardShell.style.borderTopWidth = 1;
            boardShell.style.borderRightWidth = 1;
            boardShell.style.borderBottomWidth = 1;
            boardShell.style.borderLeftWidth = 1;
            ApplyUniformBorder(boardShell, new Color(0.70f, 0.78f, 0.85f, 0.10f));

            var draftHeader = new VisualElement();
            draftHeader.style.display = DisplayStyle.Flex;
            draftHeader.style.flexDirection = FlexDirection.Column;
            draftHeader.style.paddingTop = new StyleLength(new Length(2, LengthUnit.Pixel));
            draftHeader.style.paddingBottom = new StyleLength(new Length(14, LengthUnit.Pixel));

            view.DraftPhaseChipLabel = CreateLabel("DRAFT PHASE", 10, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.86f, 0.89f, 0.92f, 0.88f));
            view.DraftPhaseChipLabel.style.alignSelf = Align.Center;
            view.DraftPhaseChipLabel.style.paddingLeft = new StyleLength(new Length(10, LengthUnit.Pixel));
            view.DraftPhaseChipLabel.style.paddingRight = new StyleLength(new Length(10, LengthUnit.Pixel));
            view.DraftPhaseChipLabel.style.paddingTop = new StyleLength(new Length(4, LengthUnit.Pixel));
            view.DraftPhaseChipLabel.style.paddingBottom = new StyleLength(new Length(4, LengthUnit.Pixel));
            view.DraftPhaseChipLabel.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.05f));
            view.DraftPhaseChipLabel.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            view.DraftPhaseChipLabel.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            view.DraftPhaseChipLabel.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            view.DraftPhaseChipLabel.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            view.DraftPhaseChipLabel.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));

            view.DraftTitleLabel = CreateLabel("CAPTAIN DRAFT", 26, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.97f, 0.98f, 0.99f, 1f));

            var captainRow = new VisualElement();
            captainRow.style.display = DisplayStyle.Flex;
            captainRow.style.flexDirection = FlexDirection.Row;
            captainRow.style.justifyContent = Justify.Center;
            captainRow.style.alignItems = Align.Stretch;
            captainRow.style.flexWrap = Wrap.Wrap;
            captainRow.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));

            captainRow.Add(CreateCaptainSummaryCard("RED CAPTAIN", TeamResult.Red, out view.DraftRedCaptainCard, out view.DraftRedCaptainLabel));
            captainRow.Add(CreateCaptainSummaryCard("BLUE CAPTAIN", TeamResult.Blue, out view.DraftBlueCaptainCard, out view.DraftBlueCaptainLabel));

            view.DraftTurnCard = new VisualElement();
            view.DraftTurnCard.style.display = DisplayStyle.Flex;
            view.DraftTurnCard.style.flexDirection = FlexDirection.Row;
            view.DraftTurnCard.style.justifyContent = Justify.SpaceBetween;
            view.DraftTurnCard.style.alignItems = Align.Center;
            view.DraftTurnCard.style.alignSelf = Align.Center;
            view.DraftTurnCard.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            view.DraftTurnCard.style.maxWidth = 760;
            view.DraftTurnCard.style.minHeight = 108;
            view.DraftTurnCard.style.marginTop = new StyleLength(new Length(18, LengthUnit.Pixel));
            view.DraftTurnCard.style.paddingTop = new StyleLength(new Length(16, LengthUnit.Pixel));
            view.DraftTurnCard.style.paddingBottom = new StyleLength(new Length(16, LengthUnit.Pixel));
            view.DraftTurnCard.style.paddingLeft = new StyleLength(new Length(22, LengthUnit.Pixel));
            view.DraftTurnCard.style.paddingRight = new StyleLength(new Length(22, LengthUnit.Pixel));
            view.DraftTurnCard.style.backgroundColor = new StyleColor(new Color(0.08f, 0.10f, 0.13f, 0.96f));
            view.DraftTurnCard.style.borderTopLeftRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            view.DraftTurnCard.style.borderTopRightRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            view.DraftTurnCard.style.borderBottomLeftRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            view.DraftTurnCard.style.borderBottomRightRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            view.DraftTurnCard.style.borderTopWidth = 1;
            view.DraftTurnCard.style.borderRightWidth = 1;
            view.DraftTurnCard.style.borderBottomWidth = 1;
            view.DraftTurnCard.style.borderLeftWidth = 1;
            ApplyUniformBorder(view.DraftTurnCard, new Color(0.95f, 0.83f, 0.45f, 0.16f));

            var turnCopy = new VisualElement();
            turnCopy.style.display = DisplayStyle.Flex;
            turnCopy.style.flexDirection = FlexDirection.Column;
            turnCopy.style.flexGrow = 1;
            turnCopy.style.flexBasis = new StyleLength(new Length(0, LengthUnit.Pixel));
            turnCopy.style.minWidth = 0;

            view.DraftTurnLabel = CreateLabel("WAITING FOR NEXT PICK", 22, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.99f, 0.94f, 0.78f, 1f));
            view.DraftTurnSubLabel = CreateLabel("Captain order will appear here.", 12, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.77f, 0.83f, 0.89f, 1f));
            view.DraftTurnSubLabel.style.marginTop = new StyleLength(new Length(4, LengthUnit.Pixel));
            view.DraftTurnSubLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            turnCopy.Add(view.DraftTurnLabel);
            turnCopy.Add(view.DraftTurnSubLabel);

            var turnMeta = new VisualElement();
            turnMeta.style.display = DisplayStyle.Flex;
            turnMeta.style.flexDirection = FlexDirection.Column;
            turnMeta.style.alignItems = Align.FlexEnd;
            turnMeta.style.justifyContent = Justify.Center;
            turnMeta.style.marginLeft = new StyleLength(new Length(18, LengthUnit.Pixel));
            turnMeta.style.flexShrink = 0;

            view.DraftTurnStatusLabel = CreateLabel("SYSTEM READY", 10, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.99f, 0.92f, 0.72f, 1f));
            view.DraftTurnMetaLabel = CreateLabel("Pool open", 11, FontStyle.Normal, TextAnchor.MiddleRight, new Color(0.72f, 0.78f, 0.84f, 0.94f));
            view.DraftTurnMetaLabel.style.marginTop = new StyleLength(new Length(5, LengthUnit.Pixel));
            turnMeta.Add(view.DraftTurnStatusLabel);
            turnMeta.Add(view.DraftTurnMetaLabel);

            view.DraftTurnCard.Add(turnCopy);
            view.DraftTurnCard.Add(turnMeta);

            draftHeader.Add(view.DraftPhaseChipLabel);
            draftHeader.Add(view.DraftTitleLabel);
            draftHeader.Add(captainRow);
            draftHeader.Add(view.DraftTurnCard);

            var columns = new VisualElement();
            columns.style.display = DisplayStyle.Flex;
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.justifyContent = Justify.Center;
            columns.style.alignItems = Align.FlexStart;
            columns.style.flexWrap = Wrap.Wrap;
            columns.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));

            view.RedTeamContainer = CreatePanelColumn(columns, "RED ROSTER", new Color(0.39f, 0.12f, 0.15f, 0.40f), new Color(1f, 0.82f, 0.82f, 1f), false, out view.DraftRedTeamPanel);
            view.AvailablePlayersContainer = CreatePanelColumn(columns, "AVAILABLE PICKS", new Color(0.34f, 0.28f, 0.08f, 0.42f), new Color(1f, 0.95f, 0.74f, 1f), true, out view.DraftAvailablePanel);
            view.BlueTeamContainer = CreatePanelColumn(columns, "BLUE ROSTER", new Color(0.10f, 0.20f, 0.39f, 0.42f), new Color(0.82f, 0.90f, 1f, 1f), false, out view.DraftBlueTeamPanel);

            var pendingSection = new VisualElement();
            pendingSection.style.display = DisplayStyle.Flex;
            pendingSection.style.flexDirection = FlexDirection.Column;
            pendingSection.style.alignSelf = Align.Center;
            pendingSection.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            pendingSection.style.maxWidth = 900;
            pendingSection.style.marginTop = new StyleLength(new Length(14, LengthUnit.Pixel));
            pendingSection.style.paddingTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            pendingSection.style.paddingBottom = new StyleLength(new Length(10, LengthUnit.Pixel));
            pendingSection.style.paddingLeft = new StyleLength(new Length(16, LengthUnit.Pixel));
            pendingSection.style.paddingRight = new StyleLength(new Length(16, LengthUnit.Pixel));
            pendingSection.style.backgroundColor = new StyleColor(new Color(0.10f, 0.12f, 0.15f, 0.90f));
            pendingSection.style.borderTopLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            pendingSection.style.borderTopRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            pendingSection.style.borderBottomLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            pendingSection.style.borderBottomRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            pendingSection.style.borderTopWidth = 1;
            pendingSection.style.borderRightWidth = 1;
            pendingSection.style.borderBottomWidth = 1;
            pendingSection.style.borderLeftWidth = 1;
            ApplyUniformBorder(pendingSection, new Color(1f, 0.90f, 0.66f, 0.10f));

            var pendingTitle = CreateLabel("WAITING FOR APPROVAL", 12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.88f, 0.90f, 0.84f, 1f));
            pendingTitle.style.marginBottom = new StyleLength(new Length(6, LengthUnit.Pixel));
            pendingSection.Add(pendingTitle);

            view.PendingPlayersContainer = new VisualElement();
            view.PendingPlayersContainer.style.display = DisplayStyle.Flex;
            view.PendingPlayersContainer.style.flexDirection = FlexDirection.Row;
            view.PendingPlayersContainer.style.flexWrap = Wrap.Wrap;
            pendingSection.Add(view.PendingPlayersContainer);

            var footerBar = new VisualElement();
            footerBar.style.display = DisplayStyle.Flex;
            footerBar.style.flexDirection = FlexDirection.Row;
            footerBar.style.justifyContent = Justify.Center;
            footerBar.style.alignItems = Align.Stretch;
            footerBar.style.flexWrap = Wrap.Wrap;
            footerBar.style.marginTop = new StyleLength(new Length(14, LengthUnit.Pixel));

            footerBar.Add(CreateDraftStatusCell("ROLE", out view.DraftFooterRoleLabel));
            footerBar.Add(CreateDraftStatusCell("POOL", out view.DraftFooterPoolLabel));
            footerBar.Add(CreateDraftStatusCell("STATUS", out view.DraftFooterStatusLabel));

            view.DraftFooterLabel = CreateLabel("Role: Waiting for captains. Follow the current pick card above.", 10, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.64f, 0.70f, 0.76f, 1f));
            view.DraftFooterLabel.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));

            boardShell.Add(draftHeader);
            boardShell.Add(columns);
            boardShell.Add(pendingSection);
            boardShell.Add(footerBar);
            boardShell.Add(view.DraftFooterLabel);

            panel.Add(boardShell);

            view.Root.Add(panel);
            view.DraftPanel = panel;
        }

        private static VisualElement CreatePanelColumn(VisualElement parent, string titleText, Color backgroundColor, Color titleColor, bool isFocusColumn, out VisualElement panel)
        {
            panel = new VisualElement();
            panel.style.display = DisplayStyle.Flex;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.flexGrow = 1;
            panel.style.flexBasis = new StyleLength(new Length(0, LengthUnit.Pixel));
            panel.style.minWidth = isFocusColumn ? 340 : 230;
            panel.style.maxWidth = isFocusColumn ? 392 : 280;
            panel.style.minHeight = 252;
            panel.style.paddingTop = new StyleLength(new Length(14, LengthUnit.Pixel));
            panel.style.paddingBottom = new StyleLength(new Length(14, LengthUnit.Pixel));
            panel.style.paddingLeft = new StyleLength(new Length(14, LengthUnit.Pixel));
            panel.style.paddingRight = new StyleLength(new Length(14, LengthUnit.Pixel));
            panel.style.backgroundColor = new StyleColor(isFocusColumn
                ? new Color(0.11f, 0.11f, 0.09f, 0.96f)
                : new Color(0.08f, 0.10f, 0.13f, 0.92f));
            panel.style.borderTopLeftRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            panel.style.borderTopRightRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            panel.style.borderBottomLeftRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            panel.style.borderBottomRightRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            panel.style.borderTopWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            ApplyUniformBorder(panel, isFocusColumn ? new Color(titleColor.r, titleColor.g, titleColor.b, 0.16f) : new Color(1f, 1f, 1f, 0.06f));
            panel.style.marginLeft = new StyleLength(new Length(8, LengthUnit.Pixel));
            panel.style.marginRight = new StyleLength(new Length(8, LengthUnit.Pixel));
            panel.style.marginBottom = new StyleLength(new Length(12, LengthUnit.Pixel));

            var header = new VisualElement();
            header.style.display = DisplayStyle.Flex;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = new StyleLength(new Length(12, LengthUnit.Pixel));

            var accent = new VisualElement();
            accent.style.width = 3;
            accent.style.height = 14;
            accent.style.flexShrink = 0;
            accent.style.marginRight = new StyleLength(new Length(8, LengthUnit.Pixel));
            accent.style.backgroundColor = new StyleColor(new Color(titleColor.r, titleColor.g, titleColor.b, isFocusColumn ? 0.78f : 0.54f));
            accent.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            accent.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            accent.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            accent.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));

            var title = CreateLabel(titleText, 11, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(titleColor.r, titleColor.g, titleColor.b, isFocusColumn ? 0.96f : 0.82f));
            title.style.flexGrow = 1;

            header.Add(accent);
            header.Add(title);
            panel.Add(header);

            var content = new VisualElement();
            content.style.display = DisplayStyle.Flex;
            content.style.flexDirection = FlexDirection.Column;
            content.style.flexGrow = 1;
            panel.Add(content);
            parent.Add(panel);
            return content;
        }

        private static VisualElement CreateDraftStatusCell(string title, out Label valueLabel)
        {
            var cell = new VisualElement();
            cell.style.display = DisplayStyle.Flex;
            cell.style.flexDirection = FlexDirection.Column;
            cell.style.alignItems = Align.Center;
            cell.style.justifyContent = Justify.Center;
            cell.style.minWidth = 168;
            cell.style.marginLeft = new StyleLength(new Length(6, LengthUnit.Pixel));
            cell.style.marginRight = new StyleLength(new Length(6, LengthUnit.Pixel));
            cell.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            cell.style.paddingTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            cell.style.paddingBottom = new StyleLength(new Length(10, LengthUnit.Pixel));
            cell.style.paddingLeft = new StyleLength(new Length(12, LengthUnit.Pixel));
            cell.style.paddingRight = new StyleLength(new Length(12, LengthUnit.Pixel));
            cell.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.03f));
            cell.style.borderTopLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            cell.style.borderTopRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            cell.style.borderBottomLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            cell.style.borderBottomRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            cell.style.borderTopWidth = 1;
            cell.style.borderRightWidth = 1;
            cell.style.borderBottomWidth = 1;
            cell.style.borderLeftWidth = 1;
            ApplyUniformBorder(cell, new Color(1f, 1f, 1f, 0.05f));

            var titleLabel = CreateLabel(title, 9, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.72f, 0.78f, 0.84f, 0.84f));
            valueLabel = CreateLabel("--", 11, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.95f, 0.97f, 1f, 1f));
            valueLabel.style.marginTop = new StyleLength(new Length(4, LengthUnit.Pixel));
            valueLabel.style.maxWidth = 200;
            valueLabel.style.whiteSpace = WhiteSpace.NoWrap;
            valueLabel.style.overflow = Overflow.Hidden;

            cell.Add(titleLabel);
            cell.Add(valueLabel);
            return cell;
        }

        private static void PopulatePlayerEntries(VisualElement container, IEnumerable<DraftOverlayPlayerEntryMessage> entries, Color backgroundColor, bool clickable, Action<string> onClick, TeamResult currentTurnTeam)
        {
            container.Clear();
            container.style.alignItems = clickable ? Align.Center : Align.Stretch;

            var orderedEntries = (entries ?? Array.Empty<DraftOverlayPlayerEntryMessage>())
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.DisplayName))
                .GroupBy(entry => entry.CommandTarget ?? entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();

            if (orderedEntries.Length == 0)
            {
                container.Add(CreateEmptyLabel());
                return;
            }

            foreach (var entry in orderedEntries)
            {
                var commandTarget = string.IsNullOrWhiteSpace(entry.CommandTarget)
                    ? null
                    : entry.CommandTarget.Trim();
                DraftUIPlugin.Log($"[CLIENT] Row target = {commandTarget ?? "none"} / mmr = {(entry.Mmr > 0 ? entry.Mmr : Constants.DEFAULT_MMR)}");

                if (clickable)
                {
                    container.Add(CreateInteractivePlayerButton(entry, backgroundColor, commandTarget, onClick));
                }
                else
                {
                    container.Add(CreatePlayerCard(entry, backgroundColor, entry.IsCaptain && entry.Team == currentTurnTeam));
                }
            }
        }

        private static Label CreateEmptyLabel()
        {
            var label = CreateLabel("---", 14, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.72f, 0.78f, 0.83f, 1f));
            label.style.opacity = 0.7f;
            label.style.marginTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            return label;
        }

        private static IEnumerable<DraftOverlayPlayerEntryMessage> ResolveEntries(DraftOverlayPlayerEntryMessage[] entries, string[] names, TeamResult fallbackTeam)
        {
            if (entries != null && entries.Length > 0)
            {
                return entries;
            }

            return (names ?? Array.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name =>
                {
                    DraftUIPlugin.Log($"[CLIENT] Falling back to basic row for {name}");
                    return new DraftOverlayPlayerEntryMessage
                    {
                        ClientId = 0,
                        SteamId = null,
                        CommandTarget = null,
                        DisplayName = name,
                        PlayerNumber = 0,
                        HasMmr = true,
                        Mmr = Constants.DEFAULT_MMR,
                        IsCaptain = false,
                        Team = fallbackTeam
                    };
                });
        }

        private static VisualElement CreatePlayerCard(DraftOverlayPlayerEntryMessage entry, Color backgroundColor, bool isCurrentTurnCaptain)
        {
            var card = new VisualElement();
            card.style.display = DisplayStyle.Flex;
            card.style.flexDirection = FlexDirection.Row;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.alignItems = Align.Center;
            card.style.backgroundColor = new StyleColor(ResolveDraftRowBackground(entry, backgroundColor, isCurrentTurnCaptain));
            card.style.paddingTop = new StyleLength(new Length(7, LengthUnit.Pixel));
            card.style.paddingBottom = new StyleLength(new Length(7, LengthUnit.Pixel));
            card.style.paddingLeft = new StyleLength(new Length(10, LengthUnit.Pixel));
            card.style.paddingRight = new StyleLength(new Length(10, LengthUnit.Pixel));
            card.style.borderTopLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.borderTopRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.borderBottomLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.borderBottomRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.borderTopWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            ApplyUniformBorder(card, ResolveDraftRowBorder(entry, isCurrentTurnCaptain));
            card.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            card.Add(CreateDraftRowContent(entry, false));
            return card;
        }

        private static Button CreateInteractivePlayerButton(DraftOverlayPlayerEntryMessage entry, Color backgroundColor, string commandTarget, Action<string> onClick)
        {
            var button = new Button(() =>
            {
                if (!string.IsNullOrWhiteSpace(commandTarget))
                {
                    onClick?.Invoke(commandTarget);
                }
            })
            {
                text = string.Empty
            };

            StyleButton(button, new ButtonPalette(
                backgroundColor,
                Tint(backgroundColor, 1.08f, 0.05f),
                Tint(backgroundColor, 0.94f, 0.00f),
                Color.white));
            button.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            button.style.maxWidth = 284;
            button.style.height = StyleKeyword.Auto;
            button.style.minHeight = 48;
            button.style.minWidth = 0;
            button.style.flexGrow = 0;
            button.style.flexShrink = 0;
            button.style.alignSelf = Align.Center;
            button.style.marginRight = 0;
            button.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            button.style.paddingLeft = new StyleLength(new Length(10, LengthUnit.Pixel));
            button.style.paddingRight = new StyleLength(new Length(10, LengthUnit.Pixel));
            button.style.paddingTop = new StyleLength(new Length(7, LengthUnit.Pixel));
            button.style.paddingBottom = new StyleLength(new Length(7, LengthUnit.Pixel));
            button.style.alignItems = Align.Stretch;
            button.style.justifyContent = Justify.Center;
            button.style.flexDirection = FlexDirection.Row;
            button.style.whiteSpace = WhiteSpace.Normal;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            ApplyUniformBorder(button, new Color(1f, 0.94f, 0.72f, 0.16f));
            button.SetEnabled(!string.IsNullOrWhiteSpace(commandTarget));
            button.Add(CreateDraftRowContent(entry, true));
            return button;
        }

        private static VisualElement CreateDraftRowContent(DraftOverlayPlayerEntryMessage entry, bool clickable)
        {
            var row = new VisualElement();
            row.style.display = DisplayStyle.Flex;
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.FlexStart;
            row.style.alignItems = Align.Center;
            row.style.flexGrow = 1;
            row.style.minHeight = 36;

            var left = new VisualElement();
            left.style.display = DisplayStyle.Flex;
            left.style.flexDirection = FlexDirection.Row;
            left.style.alignItems = Align.Center;
            left.style.flexGrow = 1;
            left.style.flexBasis = new StyleLength(new Length(0, LengthUnit.Pixel));
            left.style.minWidth = 0;

            var rowAccent = new VisualElement();
            rowAccent.style.width = 4;
            rowAccent.style.height = 28;
            rowAccent.style.flexShrink = 0;
            rowAccent.style.marginRight = new StyleLength(new Length(10, LengthUnit.Pixel));
            rowAccent.style.backgroundColor = new StyleColor(entry != null && entry.IsCaptain
                ? new Color(0.96f, 0.84f, 0.55f, 0.72f)
                : new Color(0.76f, 0.80f, 0.84f, 0.34f));
            rowAccent.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            rowAccent.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            rowAccent.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            rowAccent.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));

            left.Add(rowAccent);

            left.Add(CreateDraftAvatarFrame(entry, entry != null && entry.IsCaptain ? 30 : 26));

            var textStack = new VisualElement();
            textStack.style.display = DisplayStyle.Flex;
            textStack.style.flexDirection = FlexDirection.Column;
            textStack.style.justifyContent = Justify.Center;
            textStack.style.flexGrow = 1;
            textStack.style.flexBasis = new StyleLength(new Length(0, LengthUnit.Pixel));
            textStack.style.minWidth = 0;
            textStack.style.marginRight = 0;

            var nameRow = new VisualElement();
            nameRow.style.display = DisplayStyle.Flex;
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;
            nameRow.style.flexGrow = 1;
            nameRow.style.minWidth = 0;

            var nameLabel = CreateLabel(FormatPlayerIdentity(entry), 13, entry != null && entry.IsCaptain ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft, entry != null && entry.IsCaptain ? new Color(1f, 0.95f, 0.84f, 1f) : new Color(0.93f, 0.95f, 0.98f, 1f));
            nameLabel.style.flexGrow = 1;
            nameLabel.style.minWidth = 0;
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            nameLabel.style.overflow = Overflow.Hidden;
            nameRow.Add(nameLabel);

            textStack.Add(nameRow);

            var metaText = FormatDraftMeta(entry);
            if (!string.IsNullOrWhiteSpace(metaText))
            {
                var metaLabel = CreateLabel(metaText, 10, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.73f, 0.78f, 0.84f, 0.92f));
                metaLabel.style.marginTop = new StyleLength(new Length(2, LengthUnit.Pixel));
                metaLabel.style.whiteSpace = WhiteSpace.NoWrap;
                metaLabel.style.overflow = Overflow.Hidden;
                textStack.Add(metaLabel);
            }

            left.Add(textStack);

            row.Add(left);
            return row;
        }

        private static VisualElement CreateDraftAvatarFrame(DraftOverlayPlayerEntryMessage entry, int size)
        {
            var frame = new VisualElement();
            frame.style.width = size;
            frame.style.height = size;
            frame.style.flexShrink = 0;
            frame.style.marginRight = new StyleLength(new Length(10, LengthUnit.Pixel));
            frame.style.alignItems = Align.Center;
            frame.style.justifyContent = Justify.Center;
            frame.style.backgroundColor = new StyleColor(entry != null && entry.IsCaptain
                ? new Color(0.66f, 0.56f, 0.26f, 0.92f)
                : new Color(0.24f, 0.28f, 0.33f, 0.92f));
            frame.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            frame.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            frame.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            frame.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            frame.style.borderTopWidth = 1;
            frame.style.borderRightWidth = 1;
            frame.style.borderBottomWidth = 1;
            frame.style.borderLeftWidth = 1;
            ApplyUniformBorder(frame, new Color(1f, 1f, 1f, 0.10f));
            frame.style.overflow = Overflow.Hidden;

            BindAvatarFrameContent(frame, entry?.SteamId, ResolveDraftAvatarInitial(entry), size >= 30 ? 13 : 11);

            return frame;
        }

        private static VisualElement CreateDraftPill(string text, Color backgroundColor, Color textColor, int fontSize)
        {
            var pill = new VisualElement();
            pill.style.display = DisplayStyle.Flex;
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.alignItems = Align.Center;
            pill.style.justifyContent = Justify.Center;
            pill.style.paddingTop = new StyleLength(new Length(3, LengthUnit.Pixel));
            pill.style.paddingBottom = new StyleLength(new Length(3, LengthUnit.Pixel));
            pill.style.paddingLeft = new StyleLength(new Length(7, LengthUnit.Pixel));
            pill.style.paddingRight = new StyleLength(new Length(7, LengthUnit.Pixel));
            pill.style.backgroundColor = new StyleColor(backgroundColor);
            pill.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            pill.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            pill.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            pill.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            pill.style.flexShrink = 0;
            pill.Add(CreateLabel(text, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, textColor));
            return pill;
        }

        private static string FormatPlayerLine(DraftOverlayPlayerEntryMessage entry)
        {
            if (entry == null)
            {
                return "---";
            }

            return $"{FormatPlayerIdentity(entry)} • {FormatMmr(entry)}";
        }

        private static string FormatMmr(DraftOverlayPlayerEntryMessage entry)
        {
            if (entry == null)
            {
                return $"{Constants.DEFAULT_MMR} MMR";
            }

            var visibleMmr = entry.Mmr > 0 ? entry.Mmr : Constants.DEFAULT_MMR;
            return $"{visibleMmr} MMR";
        }

        private static string FormatPlayerIdentity(DraftOverlayPlayerEntryMessage entry)
        {
            if (entry == null)
            {
                return "---";
            }

            var displayName = NormalizeDraftIdentityText(entry.DisplayName, entry.PlayerNumber);
            if (entry.PlayerNumber > 0)
            {
                var playerPrefix = $"#{entry.PlayerNumber} ";
                if (!HasLeadingPlayerNumber(displayName, entry.PlayerNumber))
                {
                    return playerPrefix + displayName;
                }
            }

            return displayName;
        }

        private static string NormalizeDraftDisplayName(string displayName)
        {
            var clean = string.IsNullOrWhiteSpace(displayName) ? "---" : StripDraftRichTextTags(displayName).Trim();
            var bulletIndex = clean.LastIndexOf('•');
            if (bulletIndex > 0)
            {
                var suffix = clean.Substring(bulletIndex + 1).Trim();
                if (suffix.EndsWith("MMR", StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean.Substring(0, bulletIndex).TrimEnd();
                }
            }

            return string.IsNullOrWhiteSpace(clean) ? "---" : clean;
        }

        private static string NormalizeDraftIdentityText(string displayName, int playerNumber)
        {
            var clean = NormalizeDraftDisplayName(displayName);
            if (playerNumber > 0)
            {
                var anchored = TrimToPlayerNumberToken(clean, playerNumber);
                if (!string.IsNullOrWhiteSpace(anchored))
                {
                    clean = anchored;
                }
            }
            else
            {
                var firstTaggedSegment = TrimToFirstPlayerNumberToken(clean);
                if (!string.IsNullOrWhiteSpace(firstTaggedSegment))
                {
                    clean = firstTaggedSegment;
                }
            }

            clean = clean.Replace("★", string.Empty).Replace("☆", string.Empty).Trim();
            clean = TrimLeadingIdentityDecorators(clean);
            return string.IsNullOrWhiteSpace(clean) ? "---" : CollapseWhitespace(clean);
        }

        private static string TrimLeadingIdentityDecorators(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var clean = CollapseWhitespace(value);
            var tokens = clean.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var removableTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "bronze",
                "silver",
                "gold",
                "platinum",
                "diamond",
                "elite",
                "champion",
                "owner",
                "mvp"
            };

            while (tokens.Count > 1)
            {
                var token = tokens[0].Trim('[', ']', '(', ')', '-', '•', '|');
                if (string.IsNullOrWhiteSpace(token) || !removableTokens.Contains(token))
                {
                    break;
                }

                tokens.RemoveAt(0);
            }

            return string.Join(" ", tokens).Trim();
        }

        private static string FormatDraftMeta(DraftOverlayPlayerEntryMessage entry)
        {
            var badgeText = ResolveDraftBadgeText(entry);
            var mmrText = FormatMmr(entry);

            if (!string.IsNullOrWhiteSpace(badgeText))
            {
                return $"{badgeText} · {mmrText}";
            }

            return mmrText;
        }

        private static string ResolveDraftBadgeText(DraftOverlayPlayerEntryMessage entry)
        {
            if (entry == null)
            {
                return null;
            }

            if (ScoreboardBadgeClientState.TryGetBadge(entry.SteamId, entry.ClientId, out var badgeText, out _))
            {
                var primaryBadge = NormalizeDraftBadgeSegment(badgeText);
                if (!string.IsNullOrWhiteSpace(primaryBadge))
                {
                    return primaryBadge;
                }
            }

            return ExtractTierKeyword(entry.DisplayName);
        }

        private static string NormalizeDraftBadgeSegment(string badgeText)
        {
            if (string.IsNullOrWhiteSpace(badgeText))
            {
                return null;
            }

            var firstSegment = badgeText.Split(new[] { '/' }, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return string.IsNullOrWhiteSpace(firstSegment) ? null : CollapseWhitespace(StripDraftRichTextTags(firstSegment).Trim());
        }

        private static string ExtractTierKeyword(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return null;
            }

            var clean = NormalizeDraftDisplayName(source);
            var knownTiers = new[] { "Champion", "Platinum", "Diamond", "Bronze", "Silver", "Gold", "Elite" };
            foreach (var tier in knownTiers)
            {
                if (clean.IndexOf(tier, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return tier;
                }
            }

            return null;
        }

        private static string TrimToPlayerNumberToken(string value, int playerNumber)
        {
            if (string.IsNullOrWhiteSpace(value) || playerNumber <= 0)
            {
                return null;
            }

            var token = $"#{playerNumber}";
            var index = value.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            return value.Substring(index).Trim();
        }

        private static string TrimToFirstPlayerNumberToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            for (var index = 0; index < value.Length - 1; index++)
            {
                if (value[index] != '#' || !char.IsDigit(value[index + 1]))
                {
                    continue;
                }

                return value.Substring(index).Trim();
            }

            return null;
        }

        private static string StripDraftRichTextTags(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var buffer = new System.Text.StringBuilder(value.Length);
            var insideTag = false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (character == '<')
                {
                    insideTag = true;
                    continue;
                }

                if (insideTag)
                {
                    if (character == '>')
                    {
                        insideTag = false;
                    }

                    continue;
                }

                buffer.Append(character);
            }

            return buffer.ToString();
        }

        private static string CollapseWhitespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var buffer = new System.Text.StringBuilder(value.Length);
            var previousWasWhitespace = false;
            foreach (var character in value)
            {
                if (char.IsWhiteSpace(character))
                {
                    if (previousWasWhitespace)
                    {
                        continue;
                    }

                    buffer.Append(' ');
                    previousWasWhitespace = true;
                    continue;
                }

                buffer.Append(character);
                previousWasWhitespace = false;
            }

            return buffer.ToString().Trim();
        }

        private static bool HasLeadingPlayerNumber(string displayName, int playerNumber)
        {
            if (playerNumber <= 0 || string.IsNullOrWhiteSpace(displayName))
            {
                return false;
            }

            var clean = displayName.Trim();
            var token = $"#{playerNumber}";
            var searchIndex = 0;
            while (searchIndex < clean.Length)
            {
                var tokenIndex = clean.IndexOf(token, searchIndex, StringComparison.Ordinal);
                if (tokenIndex < 0)
                {
                    return false;
                }

                var hasLeftBoundary = tokenIndex == 0 || char.IsWhiteSpace(clean[tokenIndex - 1]) || clean[tokenIndex - 1] == '★' || clean[tokenIndex - 1] == '☆';
                var rightIndex = tokenIndex + token.Length;
                var hasRightBoundary = rightIndex >= clean.Length || char.IsWhiteSpace(clean[rightIndex]);
                if (hasLeftBoundary && hasRightBoundary)
                {
                    return true;
                }

                searchIndex = tokenIndex + token.Length;
            }

            return false;
        }

        private static string NormalizeCaptainName(string displayName)
        {
            return string.IsNullOrWhiteSpace(displayName) ? "Pending" : NormalizeDraftIdentityText(displayName, 0);
        }

        private static VisualElement CreateCaptainSummaryCard(string title, TeamResult team, out VisualElement card, out Label valueLabel)
        {
            var backgroundColor = team == TeamResult.Red
                ? new Color(0.14f, 0.10f, 0.11f, 0.92f)
                : new Color(0.09f, 0.12f, 0.17f, 0.92f);
            var titleColor = team == TeamResult.Red
                ? new Color(0.95f, 0.79f, 0.79f, 1f)
                : new Color(0.82f, 0.89f, 0.96f, 1f);

            card = new VisualElement();
            card.style.display = DisplayStyle.Flex;
            card.style.flexDirection = FlexDirection.Column;
            card.style.alignItems = Align.Center;
            card.style.justifyContent = Justify.Center;
            card.style.minWidth = 196;
            card.style.marginLeft = new StyleLength(new Length(6, LengthUnit.Pixel));
            card.style.marginRight = new StyleLength(new Length(6, LengthUnit.Pixel));
            card.style.marginBottom = new StyleLength(new Length(6, LengthUnit.Pixel));
            card.style.paddingTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            card.style.paddingBottom = new StyleLength(new Length(10, LengthUnit.Pixel));
            card.style.paddingLeft = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.paddingRight = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.backgroundColor = new StyleColor(backgroundColor);
            card.style.borderTopLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.borderTopRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.borderBottomLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.borderBottomRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.borderTopWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            ApplyUniformBorder(card, new Color(titleColor.r, titleColor.g, titleColor.b, 0.18f));

            var titleLabel = CreateLabel(title, 10, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(titleColor.r, titleColor.g, titleColor.b, 0.80f));
            valueLabel = CreateLabel("Pending", 13, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            valueLabel.style.marginTop = new StyleLength(new Length(5, LengthUnit.Pixel));
            valueLabel.style.maxWidth = 260;
            valueLabel.style.whiteSpace = WhiteSpace.NoWrap;
            valueLabel.style.overflow = Overflow.Hidden;
            card.Add(titleLabel);
            card.Add(valueLabel);
            return card;
        }

        private static void StyleCaptainSummaryCard(VisualElement card, TeamResult team, bool isActiveTurn, bool isCompleted)
        {
            if (card == null)
            {
                return;
            }

            var accent = team == TeamResult.Red
                ? new Color(0.90f, 0.48f, 0.48f, isActiveTurn ? 0.34f : 0.18f)
                : new Color(0.56f, 0.74f, 0.94f, isActiveTurn ? 0.34f : 0.18f);
            var background = team == TeamResult.Red
                ? new Color(0.17f, 0.10f, 0.11f, isActiveTurn && !isCompleted ? 0.94f : 0.86f)
                : new Color(0.09f, 0.13f, 0.18f, isActiveTurn && !isCompleted ? 0.94f : 0.86f);

            card.style.backgroundColor = new StyleColor(background);
            ApplyUniformBorder(card, accent);
        }

        private static void ApplyCaptainCardPulse(VisualElement card, bool isActiveTurn, float pulse)
        {
            if (card == null)
            {
                return;
            }

            card.transform.scale = new Vector3(1f + (isActiveTurn ? 0.008f * pulse : 0f), 1f + (isActiveTurn ? 0.008f * pulse : 0f), 1f);
        }

        private static Color ResolveDraftRowBackground(DraftOverlayPlayerEntryMessage entry, Color backgroundColor, bool isCurrentTurnCaptain)
        {
            if (isCurrentTurnCaptain)
            {
                return new Color(0.32f, 0.24f, 0.11f, 0.92f);
            }

            return Tint(backgroundColor, entry != null && entry.IsCaptain ? 1.03f : 1.00f, 0.08f);
        }

        private static Color ResolveDraftRowBorder(DraftOverlayPlayerEntryMessage entry, bool isCurrentTurnCaptain)
        {
            if (isCurrentTurnCaptain)
            {
                return new Color(1f, 0.88f, 0.52f, 0.26f);
            }

            return entry != null && entry.IsCaptain
                ? new Color(1f, 0.90f, 0.58f, 0.14f)
                : new Color(1f, 1f, 1f, 0.08f);
        }

        private static Color ResolveTurnAccent(TeamResult currentTurnTeam, float alpha)
        {
            switch (currentTurnTeam)
            {
                case TeamResult.Red:
                    return new Color(1f, 0.46f, 0.46f, alpha);
                case TeamResult.Blue:
                    return new Color(0.47f, 0.72f, 1f, alpha);
                default:
                    return new Color(0.95f, 0.83f, 0.45f, alpha);
            }
        }

        private static Color ResolveTurnBackground(TeamResult currentTurnTeam)
        {
            switch (currentTurnTeam)
            {
                case TeamResult.Red:
                    return new Color(0.22f, 0.12f, 0.14f, 0.88f);
                case TeamResult.Blue:
                    return new Color(0.10f, 0.16f, 0.26f, 0.88f);
                default:
                    return new Color(0.12f, 0.16f, 0.21f, 0.88f);
            }
        }

        private static string ResolveDraftAvatarInitial(DraftOverlayPlayerEntryMessage entry)
        {
            var displayName = entry?.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "?";
            }

            var trimmed = NormalizeDraftDisplayName(displayName);
            return string.IsNullOrWhiteSpace(trimmed) ? "?" : trimmed.Substring(0, 1).ToUpperInvariant();
        }

        private static Color TeamColor(TeamResult team)
        {
            return new Color(0.19f, 0.21f, 0.24f, 0.46f);
        }

        private static void ApplyUniformBorder(VisualElement element, Color color)
        {
            if (element == null)
            {
                return;
            }

            element.style.borderTopColor = new StyleColor(color);
            element.style.borderRightColor = new StyleColor(color);
            element.style.borderBottomColor = new StyleColor(color);
            element.style.borderLeftColor = new StyleColor(color);
        }

        private static Color ReadableTeamColor(TeamResult team)
        {
            return Color.white;
        }

        private static Label CreateLabel(string text, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
        {
            var label = new Label(text);
            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = fontStyle;
            label.style.unityTextAlign = alignment;
            label.style.color = new StyleColor(color);
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private static void StyleButton(Button button, ButtonPalette palette)
        {
            button.style.minWidth = 0;
            button.style.height = 42;
            button.style.paddingLeft = new StyleLength(new Length(16, LengthUnit.Pixel));
            button.style.paddingRight = new StyleLength(new Length(16, LengthUnit.Pixel));
            button.style.paddingTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            button.style.paddingBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            button.style.backgroundColor = new StyleColor(palette.Normal);
            button.style.color = new StyleColor(palette.Text);
            button.style.fontSize = 13;
            button.style.whiteSpace = WhiteSpace.NoWrap;
            button.style.overflow = Overflow.Hidden;
            button.style.flexGrow = 1;
            button.style.flexShrink = 1;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            button.style.borderTopRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            button.style.borderBottomLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            button.style.borderBottomRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            button.style.borderTopWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            ApplyUniformBorder(button, new Color(1f, 1f, 1f, 0.10f));
            button.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            ApplyButtonVisual(button, palette, palette.Normal, 1f);

            button.RegisterCallback<PointerEnterEvent>(_ => ApplyButtonVisual(button, palette, palette.Hover, 1.025f));
            button.RegisterCallback<PointerLeaveEvent>(_ => ApplyButtonVisual(button, palette, palette.Normal, 1f));
            button.RegisterCallback<PointerDownEvent>(_ => ApplyButtonVisual(button, palette, palette.Pressed, 0.985f));
            button.RegisterCallback<PointerUpEvent>(_ => ApplyButtonVisual(button, palette, palette.Hover, 1.025f));
        }

        private static void ApplyButtonVisual(Button button, ButtonPalette palette, Color color, float scale)
        {
            if (button == null) return;

            button.style.backgroundColor = new StyleColor(color);
            button.style.color = new StyleColor(palette.Text);
            button.transform.scale = new Vector3(scale, scale, 1f);
        }

        private static Color Tint(Color color, float multiplier, float alphaOffset)
        {
            return new Color(
                Mathf.Clamp01(color.r * multiplier),
                Mathf.Clamp01(color.g * multiplier),
                Mathf.Clamp01(color.b * multiplier),
                Mathf.Clamp01(color.a + alphaOffset));
        }

        private static StyleLength Auto()
        {
            return new StyleLength(StyleKeyword.Auto);
        }

        private readonly struct ButtonPalette
        {
            public readonly Color Normal;
            public readonly Color Hover;
            public readonly Color Pressed;
            public readonly Color Text;

            public ButtonPalette(Color normal, Color hover, Color pressed, Color text)
            {
                Normal = normal;
                Hover = hover;
                Pressed = pressed;
                Text = text;
            }
        }
    }
}