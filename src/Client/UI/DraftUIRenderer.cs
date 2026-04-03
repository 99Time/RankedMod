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

        public sealed class View
        {
            public VisualElement Container;
            public VisualElement Backdrop;
            public VisualElement Root;
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
            public Label ApprovalTitleLabel;
            public Label ApprovalPlayerNameLabel;
            public Label ApprovalPromptLabel;
            public Label ApprovalMetaLabel;
            public Label ApprovalFooterLabel;
            public Button ApprovalAcceptButton;
            public Button ApprovalRejectButton;
            public VisualElement DraftPanel;
            public Label DraftTitleLabel;
            public Label DraftTurnLabel;
            public Label DraftTurnSubLabel;
            public Label DraftFooterLabel;
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

        public static void CreateUI(UIHUD hud, VisualElement rootVisualElement, out View view, Action onVoteAccepted, Action onVoteRejected, Action onApprovalAccepted, Action onApprovalRejected, Action<string> onPickPlayer, Action<string> onAcceptLateJoiner, Action onWelcomeDiscordOpen, Action onWelcomeContinue, Action onPostMatchContinue, Action onPostMatchClose)
        {
            view = null;

            if (rootVisualElement == null || rootVisualElement.parent == null)
            {
                DraftUIPlugin.LogError("Root VisualElement not found!");
                return;
            }

            var root = rootVisualElement.parent;
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
            WelcomeUIRenderer.BuildUI(view, onWelcomeDiscordOpen, onWelcomeContinue);
            PostMatchUIRenderer.BuildUI(view, onPostMatchContinue, onPostMatchClose);

            DraftUIPlugin.Log("UI BUILD COMPLETE");
        }

        public static void ShowHidden(View view)
        {
            if (view == null) return;
            ApplyDefaultRootChrome(view);
            view.Container.style.display = DisplayStyle.None;
            view.Container.style.opacity = 0f;
            if (view.Welcome?.Panel != null) view.Welcome.Panel.style.display = DisplayStyle.None;
            if (view.PostMatch?.Panel != null) view.PostMatch.Panel.style.display = DisplayStyle.None;
        }

        public static void ShowVoting(View view)
        {
            if (view == null) return;
            ApplyVotingRootChrome(view);
            view.Container.style.display = DisplayStyle.Flex;
            if (view.Welcome?.Panel != null) view.Welcome.Panel.style.display = DisplayStyle.None;
            view.VotingPanel.style.display = DisplayStyle.Flex;
            view.ApprovalPanel.style.display = DisplayStyle.None;
            view.DraftPanel.style.display = DisplayStyle.None;
            if (view.PostMatch?.Panel != null) view.PostMatch.Panel.style.display = DisplayStyle.None;
        }

        public static void ShowApproval(View view)
        {
            if (view == null) return;
            ApplyDefaultRootChrome(view);
            view.Container.style.display = DisplayStyle.Flex;
            view.VotingPanel.style.display = DisplayStyle.None;
            if (view.Welcome?.Panel != null) view.Welcome.Panel.style.display = DisplayStyle.None;
            view.ApprovalPanel.style.display = DisplayStyle.Flex;
            view.DraftPanel.style.display = DisplayStyle.None;
            if (view.PostMatch?.Panel != null) view.PostMatch.Panel.style.display = DisplayStyle.None;
        }

        public static void ShowDraft(View view)
        {
            if (view == null) return;
            ApplyDefaultRootChrome(view);
            view.Container.style.display = DisplayStyle.Flex;
            view.VotingPanel.style.display = DisplayStyle.None;
            view.ApprovalPanel.style.display = DisplayStyle.None;
            if (view.Welcome?.Panel != null) view.Welcome.Panel.style.display = DisplayStyle.None;
            view.DraftPanel.style.display = DisplayStyle.Flex;
            if (view.PostMatch?.Panel != null) view.PostMatch.Panel.style.display = DisplayStyle.None;
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

            view.Root.style.width = new StyleLength(new Length(92, LengthUnit.Percent));
            view.Root.style.maxWidth = 1220;
            view.Root.style.paddingTop = new StyleLength(new Length(24, LengthUnit.Pixel));
            view.Root.style.paddingBottom = new StyleLength(new Length(24, LengthUnit.Pixel));
            view.Root.style.paddingLeft = new StyleLength(new Length(28, LengthUnit.Pixel));
            view.Root.style.paddingRight = new StyleLength(new Length(28, LengthUnit.Pixel));
            view.Root.style.backgroundColor = new StyleColor(new Color(0.06f, 0.09f, 0.12f, 0.94f));
            view.Root.style.borderTopWidth = 1;
            view.Root.style.borderRightWidth = 1;
            view.Root.style.borderBottomWidth = 1;
            view.Root.style.borderLeftWidth = 1;
            view.Root.style.borderTopColor = new StyleColor(new Color(0.48f, 0.60f, 0.70f, 0.28f));
            view.Root.style.borderRightColor = new StyleColor(new Color(0.48f, 0.60f, 0.70f, 0.18f));
            view.Root.style.borderBottomColor = new StyleColor(new Color(0.02f, 0.03f, 0.05f, 0.65f));
            view.Root.style.borderLeftColor = new StyleColor(new Color(0.48f, 0.60f, 0.70f, 0.18f));
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

            view.VotingTitleLabel.text = "RANKED MATCH VOTE";
            view.VotingPromptLabel.text = "Confirm if you are ready";
            view.VotingTimerLabel.text = $"{state.YesVotes} / {Mathf.Max(0, state.EligibleCount)} players ready";
            view.VotingStatsLabel.text = string.Empty;
            view.VotingFooterLabel.text = string.Empty;

            view.VotingReadyFill.style.width = new StyleLength(new Length(readyProgress * 100f, LengthUnit.Percent));
            view.VotingCountdownFill.style.width = new StyleLength(new Length(countdownProgress * 100f, LengthUnit.Percent));
            view.VotingReadyFill.style.opacity = 0.88f + (pulse * 0.12f);
            view.VotingCountdownFill.style.opacity = 0.78f + ((1f - pulse) * 0.18f);

            view.VotingAcceptButton.SetEnabled(!voteLocked);
            view.VotingRejectButton.SetEnabled(!voteLocked);
            ConfigureVoteActionButton(
                view.VotingAcceptButton,
                view.VotingAcceptTitleLabel,
                view.VotingAcceptSubtitleLabel,
                "ACCEPT",
                "Ready",
                voteLocked,
                hasAccepted);
            ConfigureVoteActionButton(
                view.VotingRejectButton,
                view.VotingRejectTitleLabel,
                view.VotingRejectSubtitleLabel,
                "DECLINE",
                "Not Ready",
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

        public static void RenderApproval(View view, ApprovalRequestStateMessage state)
        {
            if (view == null || state == null) return;

            view.ApprovalTitleLabel.text = string.IsNullOrWhiteSpace(state.Title) ? "Team Approval Required" : state.Title;
            view.ApprovalPlayerNameLabel.text = string.IsNullOrWhiteSpace(state.PlayerName) ? "Unknown Player" : state.PlayerName;
            view.ApprovalPromptLabel.text = string.IsNullOrWhiteSpace(state.PromptText)
                ? "A player is waiting for your decision."
                : state.PromptText;
            view.ApprovalMetaLabel.text = state.IsSwitchRequest
                ? $"Switch Request  |  Target {state.TargetTeamName ?? "Team"}"
                : $"Late Join Request  |  Target {state.TargetTeamName ?? "Team"}";
            view.ApprovalFooterLabel.text = string.IsNullOrWhiteSpace(state.FooterText)
                ? "Approve to place the player into your team. Reject keeps the current state intact."
                : state.FooterText;
        }

        public static void RenderDraft(View view, DraftOverlayStateMessage state, DraftOverlayExtendedMessage extendedState, bool isLocalTurn, TeamResult currentTurnTeam, Action<string> onPickPlayer, Action<string> onAcceptLateJoiner)
        {
            if (view == null || state == null) return;

            extendedState = extendedState ?? DraftOverlayExtendedMessage.Hidden();

            view.DraftTitleLabel.text = string.IsNullOrWhiteSpace(state.Title) ? "RANKED MATCH SETUP" : state.Title;
            view.DraftRedCaptainLabel.text = NormalizeCaptainName(state.RedCaptainName);
            view.DraftBlueCaptainLabel.text = NormalizeCaptainName(state.BlueCaptainName);

            if (state.IsCompleted)
            {
                view.DraftTurnLabel.text = "DRAFT COMPLETE";
                view.DraftTurnSubLabel.text = "Teams locked in. Match start is next.";
            }
            else if (isLocalTurn)
            {
                view.DraftTurnLabel.text = "YOUR TURN TO PICK";
                view.DraftTurnSubLabel.text = "Select a player from Available Players.";
            }
            else if (!string.IsNullOrWhiteSpace(state.CurrentTurnName))
            {
                view.DraftTurnLabel.text = "WAITING FOR CAPTAIN...";
                view.DraftTurnSubLabel.text = $"{NormalizeCaptainName(state.CurrentTurnName)} is choosing a player.";
            }
            else
            {
                view.DraftTurnLabel.text = "WAITING FOR CAPTAIN...";
                view.DraftTurnSubLabel.text = "Pick order will appear here.";
            }

            StyleCaptainSummaryCard(view.DraftRedCaptainCard, TeamResult.Red, currentTurnTeam == TeamResult.Red, state.IsCompleted);
            StyleCaptainSummaryCard(view.DraftBlueCaptainCard, TeamResult.Blue, currentTurnTeam == TeamResult.Blue, state.IsCompleted);

            view.DraftFooterLabel.text = state.IsCompleted
                ? "Teams are ready. Preparing the match."
                : string.IsNullOrWhiteSpace(state.FooterText)
                    ? "Select a player to add them to your team."
                    : state.FooterText;

            PopulatePlayerEntries(view.RedTeamContainer, ResolveEntries(extendedState.RedPlayerEntries, state.RedPlayers, TeamResult.Red), TeamColor(TeamResult.Red), false, null, currentTurnTeam);
            PopulatePlayerEntries(view.BlueTeamContainer, ResolveEntries(extendedState.BluePlayerEntries, state.BluePlayers, TeamResult.Blue), TeamColor(TeamResult.Blue), false, null, currentTurnTeam);
            PopulatePlayerEntries(view.AvailablePlayersContainer, ResolveEntries(extendedState.AvailablePlayerEntries, state.AvailablePlayers, TeamResult.Unknown), new Color(0.28f, 0.34f, 0.13f, 0.66f), true, onPickPlayer, currentTurnTeam);
            PopulatePlayerEntries(view.PendingPlayersContainer, ResolveEntries(extendedState.PendingLateJoinerEntries, state.PendingLateJoiners, TeamResult.Unknown), new Color(0.49f, 0.40f, 0.13f, 0.58f), true, onAcceptLateJoiner, currentTurnTeam);
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
                ? new Color(0.29f, 0.22f, 0.08f, 0.88f)
                : ResolveTurnBackground(currentTurnTeam);

            if (view.DraftTurnCard != null)
            {
                view.DraftTurnCard.style.backgroundColor = new StyleColor(turnBackground);
                view.DraftTurnCard.style.borderTopColor = new StyleColor(turnBorder);
                view.DraftTurnCard.style.borderRightColor = new StyleColor(new Color(turnBorder.r, turnBorder.g, turnBorder.b, turnBorder.a * 0.45f));
                view.DraftTurnCard.style.borderLeftColor = new StyleColor(new Color(turnBorder.r, turnBorder.g, turnBorder.b, turnBorder.a * 0.45f));
                view.DraftTurnCard.transform.scale = new Vector3(1f + ((isLocalTurn ? 0.012f : 0.004f) * pulse), 1f + ((isLocalTurn ? 0.012f : 0.004f) * pulse), 1f);
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
                    ? new Color(1f, 0.90f, 0.54f, 0.34f + (pulse * 0.16f))
                    : new Color(1f, 0.90f, 0.54f, 0.24f);
                view.DraftAvailablePanel.style.borderTopColor = new StyleColor(availableAccent);
                view.DraftAvailablePanel.style.borderRightColor = new StyleColor(new Color(availableAccent.r, availableAccent.g, availableAccent.b, availableAccent.a * 0.55f));
                view.DraftAvailablePanel.style.borderLeftColor = new StyleColor(new Color(availableAccent.r, availableAccent.g, availableAccent.b, availableAccent.a * 0.55f));
                view.DraftAvailablePanel.transform.scale = new Vector3(1f + (isLocalTurn ? 0.006f * pulse : 0f), 1f + (isLocalTurn ? 0.006f * pulse : 0f), 1f);
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
            panel.style.maxWidth = 820;
            panel.style.paddingTop = new StyleLength(new Length(20, LengthUnit.Pixel));
            panel.style.paddingBottom = new StyleLength(new Length(18, LengthUnit.Pixel));
            panel.style.paddingLeft = new StyleLength(new Length(24, LengthUnit.Pixel));
            panel.style.paddingRight = new StyleLength(new Length(24, LengthUnit.Pixel));
            panel.style.backgroundColor = new StyleColor(new Color(0.05f, 0.08f, 0.11f, 0.94f));
            panel.style.borderTopLeftRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            panel.style.borderTopRightRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            panel.style.borderBottomLeftRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            panel.style.borderBottomRightRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            panel.style.borderTopWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderTopColor = new StyleColor(new Color(0.77f, 0.83f, 0.90f, 0.26f));
            panel.style.borderRightColor = new StyleColor(new Color(0.77f, 0.83f, 0.90f, 0.10f));
            panel.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.55f));
            panel.style.borderLeftColor = new StyleColor(new Color(0.77f, 0.83f, 0.90f, 0.10f));

            var panelShadow = new VisualElement();
            panelShadow.style.position = Position.Absolute;
            panelShadow.style.top = 10;
            panelShadow.style.left = 10;
            panelShadow.style.right = 10;
            panelShadow.style.bottom = -10;
            panelShadow.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.16f));
            panelShadow.style.borderTopLeftRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            panelShadow.style.borderTopRightRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            panelShadow.style.borderBottomLeftRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            panelShadow.style.borderBottomRightRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            panel.Add(panelShadow);

            var topAccent = new VisualElement();
            topAccent.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            topAccent.style.height = 2;
            topAccent.style.marginBottom = new StyleLength(new Length(18, LengthUnit.Pixel));
            topAccent.style.backgroundColor = new StyleColor(new Color(0.91f, 0.74f, 0.26f, 0.96f));
            topAccent.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            topAccent.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            topAccent.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            topAccent.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));

            var header = new VisualElement();
            header.style.display = DisplayStyle.Flex;
            header.style.flexDirection = FlexDirection.Column;
            header.style.alignItems = Align.Center;

            view.VotingTitleLabel = CreateLabel("RANKED MATCH VOTE", 26, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.98f, 0.99f, 1f, 1f));
            view.VotingPromptLabel = CreateLabel("Confirm if you are ready", 13, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.72f, 0.79f, 0.86f, 1f));
            view.VotingPromptLabel.style.marginTop = new StyleLength(new Length(4, LengthUnit.Pixel));

            header.Add(view.VotingTitleLabel);
            header.Add(view.VotingPromptLabel);

            var actionRow = new VisualElement();
            actionRow.style.display = DisplayStyle.Flex;
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.alignItems = Align.Center;
            actionRow.style.justifyContent = Justify.Center;
            actionRow.style.flexWrap = Wrap.Wrap;
            actionRow.style.marginTop = new StyleLength(new Length(20, LengthUnit.Pixel));

            view.VotingAcceptButton = new Button(() => onVoteAccepted?.Invoke());
            view.VotingAcceptButton.text = string.Empty;
            StyleButton(view.VotingAcceptButton, new ButtonPalette(
                new Color(0.19f, 0.71f, 0.40f, 0.98f),
                new Color(0.26f, 0.82f, 0.49f, 1f),
                new Color(0.13f, 0.53f, 0.30f, 1f),
                Color.white));
            view.VotingAcceptButton.style.width = 200;
            view.VotingAcceptButton.style.height = 72;
            view.VotingAcceptButton.style.marginBottom = 0;
            view.VotingAcceptButton.style.alignItems = Align.Stretch;
            view.VotingAcceptButton.style.justifyContent = Justify.Center;
            view.VotingAcceptButton.style.flexDirection = FlexDirection.Row;
            view.VotingAcceptButton.style.whiteSpace = WhiteSpace.Normal;
            view.VotingAcceptButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            view.VotingAcceptButton.style.paddingLeft = new StyleLength(new Length(16, LengthUnit.Pixel));
            view.VotingAcceptButton.style.paddingRight = new StyleLength(new Length(16, LengthUnit.Pixel));
            view.VotingAcceptButton.style.borderTopColor = new StyleColor(new Color(0.80f, 1f, 0.88f, 0.52f));
            view.VotingAcceptButton.style.borderRightColor = new StyleColor(new Color(0.80f, 1f, 0.88f, 0.22f));
            view.VotingAcceptButton.style.borderLeftColor = new StyleColor(new Color(0.80f, 1f, 0.88f, 0.22f));
            var acceptIcon = CreateLabel("✓", 24, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            acceptIcon.style.width = 28;
            acceptIcon.style.marginRight = new StyleLength(new Length(8, LengthUnit.Pixel));
            var acceptTextStack = new VisualElement();
            acceptTextStack.style.display = DisplayStyle.Flex;
            acceptTextStack.style.flexDirection = FlexDirection.Column;
            acceptTextStack.style.alignItems = Align.FlexStart;
            acceptTextStack.style.justifyContent = Justify.Center;
            view.VotingAcceptTitleLabel = CreateLabel("ACCEPT", 15, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            view.VotingAcceptSubtitleLabel = CreateLabel("Ready", 11, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.88f, 1f, 0.92f, 0.96f));
            view.VotingAcceptSubtitleLabel.style.marginTop = new StyleLength(new Length(1, LengthUnit.Pixel));
            acceptTextStack.Add(view.VotingAcceptTitleLabel);
            acceptTextStack.Add(view.VotingAcceptSubtitleLabel);
            view.VotingAcceptButton.Add(acceptIcon);
            view.VotingAcceptButton.Add(acceptTextStack);

            var progressStack = new VisualElement();
            progressStack.style.display = DisplayStyle.Flex;
            progressStack.style.flexDirection = FlexDirection.Column;
            progressStack.style.alignItems = Align.Center;
            progressStack.style.justifyContent = Justify.Center;
            progressStack.style.width = 250;
            progressStack.style.marginLeft = new StyleLength(new Length(20, LengthUnit.Pixel));
            progressStack.style.marginRight = new StyleLength(new Length(20, LengthUnit.Pixel));
            view.VotingTimerLabel = CreateLabel("0 / 0 players ready", 14, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.94f, 0.97f, 1f, 1f));
            var readyTrack = CreateVoteProgressTrack(new Color(0.17f, 0.21f, 0.26f, 0.96f), new Color(0.25f, 0.90f, 0.53f, 0.98f), 4, out view.VotingReadyFill);
            readyTrack.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            progressStack.Add(view.VotingTimerLabel);
            progressStack.Add(readyTrack);
            view.VotingStatsLabel = CreateLabel(string.Empty, 11, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.74f, 0.80f, 0.86f, 1f));
            view.VotingStatsLabel.style.display = DisplayStyle.None;

            view.VotingRejectButton = new Button(() => onVoteRejected?.Invoke());
            view.VotingRejectButton.text = string.Empty;
            StyleButton(view.VotingRejectButton, new ButtonPalette(
                new Color(0.80f, 0.28f, 0.28f, 0.98f),
                new Color(0.92f, 0.34f, 0.34f, 1f),
                new Color(0.60f, 0.19f, 0.19f, 1f),
                Color.white));
            view.VotingRejectButton.style.width = 200;
            view.VotingRejectButton.style.height = 72;
            view.VotingRejectButton.style.marginBottom = 0;
            view.VotingRejectButton.style.alignItems = Align.Stretch;
            view.VotingRejectButton.style.justifyContent = Justify.Center;
            view.VotingRejectButton.style.flexDirection = FlexDirection.Row;
            view.VotingRejectButton.style.whiteSpace = WhiteSpace.Normal;
            view.VotingRejectButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            view.VotingRejectButton.style.paddingLeft = new StyleLength(new Length(16, LengthUnit.Pixel));
            view.VotingRejectButton.style.paddingRight = new StyleLength(new Length(16, LengthUnit.Pixel));
            view.VotingRejectButton.style.borderTopColor = new StyleColor(new Color(1f, 0.82f, 0.82f, 0.52f));
            view.VotingRejectButton.style.borderRightColor = new StyleColor(new Color(1f, 0.82f, 0.82f, 0.22f));
            view.VotingRejectButton.style.borderLeftColor = new StyleColor(new Color(1f, 0.82f, 0.82f, 0.22f));
            var rejectIcon = CreateLabel("✕", 22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            rejectIcon.style.width = 28;
            rejectIcon.style.marginRight = new StyleLength(new Length(8, LengthUnit.Pixel));
            var rejectTextStack = new VisualElement();
            rejectTextStack.style.display = DisplayStyle.Flex;
            rejectTextStack.style.flexDirection = FlexDirection.Column;
            rejectTextStack.style.alignItems = Align.FlexStart;
            rejectTextStack.style.justifyContent = Justify.Center;
            view.VotingRejectTitleLabel = CreateLabel("DECLINE", 15, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            view.VotingRejectSubtitleLabel = CreateLabel("Not Ready", 11, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(1f, 0.91f, 0.91f, 0.96f));
            view.VotingRejectSubtitleLabel.style.marginTop = new StyleLength(new Length(1, LengthUnit.Pixel));
            rejectTextStack.Add(view.VotingRejectTitleLabel);
            rejectTextStack.Add(view.VotingRejectSubtitleLabel);
            view.VotingRejectButton.Add(rejectIcon);
            view.VotingRejectButton.Add(rejectTextStack);

            actionRow.Add(view.VotingAcceptButton);
            actionRow.Add(progressStack);
            actionRow.Add(view.VotingRejectButton);

            var playersSection = CreateVoteSectionCard();
            playersSection.style.marginTop = new StyleLength(new Length(18, LengthUnit.Pixel));

            var rosterHeader = new VisualElement();
            rosterHeader.style.display = DisplayStyle.Flex;
            rosterHeader.style.flexDirection = FlexDirection.Row;
            rosterHeader.style.justifyContent = Justify.FlexStart;
            rosterHeader.style.alignItems = Align.Center;

            var rosterTitle = CreateLabel("PLAYERS", 13, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.98f, 0.99f, 1f, 1f));
            rosterHeader.Add(rosterTitle);

            var rosterColumns = new VisualElement();
            rosterColumns.style.display = DisplayStyle.Flex;
            rosterColumns.style.flexDirection = FlexDirection.Row;
            rosterColumns.style.flexWrap = Wrap.Wrap;
            rosterColumns.style.alignItems = Align.FlexStart;
            rosterColumns.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));

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
            view.VotingPlayersRightColumn.style.marginLeft = new StyleLength(new Length(10, LengthUnit.Pixel));

            rosterColumns.Add(view.VotingPlayersLeftColumn);
            rosterColumns.Add(view.VotingPlayersRightColumn);
            playersSection.Add(rosterHeader);
            playersSection.Add(rosterColumns);

            var countdownTrack = CreateVoteProgressTrack(new Color(0.20f, 0.17f, 0.10f, 0.88f), new Color(0.93f, 0.74f, 0.26f, 0.98f), 3, out view.VotingCountdownFill);
            countdownTrack.style.marginTop = new StyleLength(new Length(16, LengthUnit.Pixel));

            view.VotingFooterLabel = CreateLabel(string.Empty, 11, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white);
            view.VotingFooterLabel.style.display = DisplayStyle.None;

            panel.Add(topAccent);
            panel.Add(header);
            panel.Add(actionRow);
            panel.Add(playersSection);
            panel.Add(countdownTrack);

            view.Root.Add(panel);
            view.VotingPanel = panel;
        }

        private static VisualElement CreateVoteSectionCard()
        {
            var card = new VisualElement();
            card.style.display = DisplayStyle.Flex;
            card.style.flexDirection = FlexDirection.Column;
            card.style.paddingTop = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.paddingBottom = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.paddingLeft = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.paddingRight = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.backgroundColor = new StyleColor(new Color(0.10f, 0.14f, 0.20f, 0.86f));
            card.style.borderTopLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.borderTopRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.borderBottomLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.borderBottomRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.borderTopWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderTopColor = new StyleColor(new Color(0.72f, 0.80f, 0.88f, 0.18f));
            card.style.borderRightColor = new StyleColor(new Color(0.72f, 0.80f, 0.88f, 0.08f));
            card.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.36f));
            card.style.borderLeftColor = new StyleColor(new Color(0.72f, 0.80f, 0.88f, 0.08f));
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
            button.style.opacity = isSelected ? 1f : isLocked ? 0.48f : 1f;
            button.style.transformOrigin = new TransformOrigin(50, 50, 0);
            titleLabel.style.color = new StyleColor(isSelected ? Color.white : new Color(0.96f, 0.97f, 0.99f, 1f));
            subtitleLabel.style.color = new StyleColor(isSelected
                ? new Color(0.96f, 0.98f, 0.99f, 1f)
                : isLocked
                    ? new Color(0.78f, 0.83f, 0.88f, 1f)
                    : new Color(0.86f, 0.91f, 0.95f, 1f));
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
            card.style.paddingTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            card.style.paddingBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            card.style.paddingLeft = new StyleLength(new Length(10, LengthUnit.Pixel));
            card.style.paddingRight = new StyleLength(new Length(10, LengthUnit.Pixel));
            card.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            card.style.backgroundColor = new StyleColor(ResolveVoteCardBackground(entry));
            card.style.borderTopLeftRadius = new StyleLength(new Length(10, LengthUnit.Pixel));
            card.style.borderTopRightRadius = new StyleLength(new Length(10, LengthUnit.Pixel));
            card.style.borderBottomLeftRadius = new StyleLength(new Length(10, LengthUnit.Pixel));
            card.style.borderBottomRightRadius = new StyleLength(new Length(10, LengthUnit.Pixel));
            card.style.borderTopWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderTopColor = new StyleColor(ResolveVoteCardBorder(entry));
            card.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.07f));
            card.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.28f));
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
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            nameLabel.style.overflow = Overflow.Hidden;

            textColumn.Add(nameLabel);
            leftSide.Add(textColumn);

            card.Add(leftSide);
            card.Add(CreateVoteStatusPill(entry));
            return card;
        }

        private static VisualElement CreateVoteAvatarFrame(VoteOverlayPlayerEntryMessage entry)
        {
            var frame = new VisualElement();
            frame.style.width = 34;
            frame.style.height = 34;
            frame.style.flexShrink = 0;
            frame.style.marginRight = new StyleLength(new Length(10, LengthUnit.Pixel));
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
            frame.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.26f));
            frame.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.10f));
            frame.style.overflow = Overflow.Hidden;

            if (VoteAvatarCache.TryGetAvatarTexture(entry?.SteamId, out var avatarTexture) && avatarTexture != null)
            {
                var avatarImage = new Image();
                avatarImage.image = avatarTexture;
                avatarImage.scaleMode = ScaleMode.ScaleAndCrop;
                avatarImage.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
                avatarImage.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
                frame.Add(avatarImage);
            }
            else
            {
                frame.Add(CreateLabel(ResolveVoteAvatarInitial(entry), 16, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.98f, 0.99f, 1f, 1f)));
            }

            return frame;
        }

        private static VisualElement CreateVoteStatusPill(VoteOverlayPlayerEntryMessage entry)
        {
            var pill = new VisualElement();
            pill.style.display = DisplayStyle.Flex;
            pill.style.flexDirection = FlexDirection.Column;
            pill.style.alignItems = Align.Center;
            pill.style.justifyContent = Justify.Center;
            pill.style.marginLeft = new StyleLength(new Length(10, LengthUnit.Pixel));
            pill.style.paddingTop = new StyleLength(new Length(4, LengthUnit.Pixel));
            pill.style.paddingBottom = new StyleLength(new Length(4, LengthUnit.Pixel));
            pill.style.paddingLeft = new StyleLength(new Length(8, LengthUnit.Pixel));
            pill.style.paddingRight = new StyleLength(new Length(8, LengthUnit.Pixel));
            pill.style.backgroundColor = new StyleColor(ResolveVoteStatusBackground(entry));
            pill.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            pill.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            pill.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            pill.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            pill.style.flexShrink = 0;
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

            var displayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? "Player" : entry.DisplayName.Trim();
            if (entry.PlayerNumber > 0)
            {
                var playerPrefix = $"#{entry.PlayerNumber}";
                if (!displayName.StartsWith(playerPrefix, StringComparison.OrdinalIgnoreCase))
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
                return "Vote starter";
            }

            if (entry.IsInitiator)
            {
                return "Started the vote";
            }

            if (!entry.HasVoted)
            {
                return "Waiting for response";
            }

            return entry.VoteAccepted ? "Ready for ranked" : "Opted out";
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
            var displayName = entry?.DisplayName;
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
                return new Color(0.12f, 0.17f, 0.23f, 0.92f);
            }

            return entry.VoteAccepted
                ? new Color(0.11f, 0.23f, 0.18f, 0.92f)
                : new Color(0.25f, 0.12f, 0.12f, 0.92f);
        }

        private static Color ResolveVoteCardBorder(VoteOverlayPlayerEntryMessage entry)
        {
            if (entry == null || !entry.HasVoted)
            {
                return new Color(0.74f, 0.66f, 0.26f, 0.20f);
            }

            return entry.VoteAccepted
                ? new Color(0.37f, 0.87f, 0.58f, 0.34f)
                : new Color(0.98f, 0.46f, 0.46f, 0.34f);
        }

        private static Color ResolveVoteAccent(VoteOverlayPlayerEntryMessage entry)
        {
            if (entry == null || !entry.HasVoted)
            {
                return new Color(0.58f, 0.50f, 0.20f, 0.84f);
            }

            return entry.VoteAccepted
                ? new Color(0.20f, 0.67f, 0.43f, 0.90f)
                : new Color(0.74f, 0.26f, 0.26f, 0.90f);
        }

        private static Color ResolveVoteStatusBackground(VoteOverlayPlayerEntryMessage entry)
        {
            if (entry == null || !entry.HasVoted)
            {
                return new Color(0.44f, 0.36f, 0.10f, 0.82f);
            }

            return entry.VoteAccepted
                ? new Color(0.16f, 0.47f, 0.31f, 0.88f)
                : new Color(0.56f, 0.20f, 0.20f, 0.88f);
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
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.alignSelf = Align.Center;
            panel.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            panel.style.maxWidth = 760;
            panel.style.paddingTop = new StyleLength(new Length(28, LengthUnit.Pixel));
            panel.style.paddingBottom = new StyleLength(new Length(26, LengthUnit.Pixel));
            panel.style.paddingLeft = new StyleLength(new Length(30, LengthUnit.Pixel));
            panel.style.paddingRight = new StyleLength(new Length(30, LengthUnit.Pixel));
            panel.style.backgroundColor = new StyleColor(new Color(0.08f, 0.11f, 0.16f, 0.92f));
            panel.style.borderTopLeftRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            panel.style.borderTopRightRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            panel.style.borderBottomLeftRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            panel.style.borderBottomRightRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            panel.style.borderTopWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderTopColor = new StyleColor(new Color(0.90f, 0.73f, 0.32f, 0.45f));
            panel.style.borderRightColor = new StyleColor(new Color(0.90f, 0.73f, 0.32f, 0.22f));
            panel.style.borderBottomColor = new StyleColor(new Color(0.02f, 0.03f, 0.05f, 0.78f));
            panel.style.borderLeftColor = new StyleColor(new Color(0.90f, 0.73f, 0.32f, 0.22f));

            var headerChip = new VisualElement();
            headerChip.style.alignSelf = Align.Center;
            headerChip.style.paddingTop = new StyleLength(new Length(6, LengthUnit.Pixel));
            headerChip.style.paddingBottom = new StyleLength(new Length(6, LengthUnit.Pixel));
            headerChip.style.paddingLeft = new StyleLength(new Length(14, LengthUnit.Pixel));
            headerChip.style.paddingRight = new StyleLength(new Length(14, LengthUnit.Pixel));
            headerChip.style.backgroundColor = new StyleColor(new Color(0.76f, 0.58f, 0.18f, 0.18f));
            headerChip.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            headerChip.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            headerChip.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            headerChip.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            headerChip.style.marginBottom = new StyleLength(new Length(14, LengthUnit.Pixel));

            var headerChipLabel = CreateLabel("CAPTAIN DECISION", 13, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.68f, 1f));
            headerChip.Add(headerChipLabel);

            view.ApprovalTitleLabel = CreateLabel("Team Approval Required", 28, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.98f, 0.99f, 1f, 1f));
            view.ApprovalPlayerNameLabel = CreateLabel("Player", 24, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.93f, 0.75f, 1f));
            view.ApprovalPlayerNameLabel.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            view.ApprovalPromptLabel = CreateLabel(string.Empty, 16, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.82f, 0.88f, 0.94f, 1f));
            view.ApprovalPromptLabel.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));

            var metaCard = new VisualElement();
            metaCard.style.marginTop = new StyleLength(new Length(18, LengthUnit.Pixel));
            metaCard.style.paddingTop = new StyleLength(new Length(14, LengthUnit.Pixel));
            metaCard.style.paddingBottom = new StyleLength(new Length(14, LengthUnit.Pixel));
            metaCard.style.paddingLeft = new StyleLength(new Length(16, LengthUnit.Pixel));
            metaCard.style.paddingRight = new StyleLength(new Length(16, LengthUnit.Pixel));
            metaCard.style.backgroundColor = new StyleColor(new Color(0.11f, 0.15f, 0.22f, 0.84f));
            metaCard.style.borderTopLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            metaCard.style.borderTopRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            metaCard.style.borderBottomLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            metaCard.style.borderBottomRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            metaCard.style.borderTopWidth = 1;
            metaCard.style.borderRightWidth = 1;
            metaCard.style.borderBottomWidth = 1;
            metaCard.style.borderLeftWidth = 1;
            metaCard.style.borderTopColor = new StyleColor(new Color(0.49f, 0.66f, 0.81f, 0.20f));
            metaCard.style.borderRightColor = new StyleColor(new Color(0.49f, 0.66f, 0.81f, 0.10f));
            metaCard.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.30f));
            metaCard.style.borderLeftColor = new StyleColor(new Color(0.49f, 0.66f, 0.81f, 0.10f));

            view.ApprovalMetaLabel = CreateLabel(string.Empty, 14, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.79f, 0.88f, 0.96f, 1f));
            metaCard.Add(view.ApprovalMetaLabel);

            var buttonRow = new VisualElement();
            buttonRow.style.display = DisplayStyle.Flex;
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.Center;
            buttonRow.style.alignItems = Align.Center;
            buttonRow.style.marginTop = new StyleLength(new Length(22, LengthUnit.Pixel));
            buttonRow.style.flexWrap = Wrap.Wrap;

            view.ApprovalAcceptButton = new Button(() => onApprovalAccepted?.Invoke())
            {
                text = "Approve"
            };
            StyleButton(view.ApprovalAcceptButton, new ButtonPalette(
                new Color(0.17f, 0.56f, 0.35f, 0.98f),
                new Color(0.24f, 0.68f, 0.43f, 1f),
                new Color(0.12f, 0.45f, 0.28f, 1f),
                Color.white));

            view.ApprovalRejectButton = new Button(() => onApprovalRejected?.Invoke())
            {
                text = "Reject"
            };
            StyleButton(view.ApprovalRejectButton, new ButtonPalette(
                new Color(0.66f, 0.24f, 0.24f, 0.98f),
                new Color(0.79f, 0.30f, 0.30f, 1f),
                new Color(0.53f, 0.18f, 0.18f, 1f),
                Color.white));
            view.ApprovalRejectButton.style.marginLeft = new StyleLength(new Length(12, LengthUnit.Pixel));

            buttonRow.Add(view.ApprovalAcceptButton);
            buttonRow.Add(view.ApprovalRejectButton);

            view.ApprovalFooterLabel = CreateLabel(string.Empty, 13, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.71f, 0.80f, 0.86f, 1f));
            view.ApprovalFooterLabel.style.marginTop = new StyleLength(new Length(14, LengthUnit.Pixel));

            panel.Add(headerChip);
            panel.Add(view.ApprovalTitleLabel);
            panel.Add(view.ApprovalPlayerNameLabel);
            panel.Add(view.ApprovalPromptLabel);
            panel.Add(metaCard);
            panel.Add(buttonRow);
            panel.Add(view.ApprovalFooterLabel);

            view.Root.Add(panel);
            view.ApprovalPanel = panel;
        }

        private static void BuildDraftUI(View view)
        {
            var panel = new VisualElement();
            panel.name = "DraftPanel";
            panel.style.display = DisplayStyle.None;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            panel.style.maxWidth = 1020;
            panel.style.alignSelf = Align.Center;

            var draftHeader = new VisualElement();
            draftHeader.style.display = DisplayStyle.Flex;
            draftHeader.style.flexDirection = FlexDirection.Column;
            draftHeader.style.paddingTop = new StyleLength(new Length(4, LengthUnit.Pixel));
            draftHeader.style.paddingBottom = new StyleLength(new Length(14, LengthUnit.Pixel));

            view.DraftTitleLabel = CreateLabel("RANKED MATCH SETUP", 24, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.97f, 0.98f, 0.99f, 1f));

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
            view.DraftTurnCard.style.flexDirection = FlexDirection.Column;
            view.DraftTurnCard.style.alignSelf = Align.Center;
            view.DraftTurnCard.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            view.DraftTurnCard.style.maxWidth = 520;
            view.DraftTurnCard.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            view.DraftTurnCard.style.paddingTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            view.DraftTurnCard.style.paddingBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            view.DraftTurnCard.style.paddingLeft = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.DraftTurnCard.style.paddingRight = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.DraftTurnCard.style.backgroundColor = new StyleColor(new Color(0.12f, 0.16f, 0.21f, 0.90f));
            view.DraftTurnCard.style.borderTopLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            view.DraftTurnCard.style.borderTopRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            view.DraftTurnCard.style.borderBottomLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            view.DraftTurnCard.style.borderBottomRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            view.DraftTurnCard.style.borderTopWidth = 1;
            view.DraftTurnCard.style.borderRightWidth = 1;
            view.DraftTurnCard.style.borderBottomWidth = 1;
            view.DraftTurnCard.style.borderLeftWidth = 1;
            view.DraftTurnCard.style.borderTopColor = new StyleColor(new Color(0.95f, 0.83f, 0.45f, 0.38f));
            view.DraftTurnCard.style.borderRightColor = new StyleColor(new Color(0.95f, 0.83f, 0.45f, 0.16f));
            view.DraftTurnCard.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.34f));
            view.DraftTurnCard.style.borderLeftColor = new StyleColor(new Color(0.95f, 0.83f, 0.45f, 0.16f));

            view.DraftTurnLabel = CreateLabel("WAITING FOR CAPTAIN...", 17, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.99f, 0.94f, 0.78f, 1f));
            view.DraftTurnSubLabel = CreateLabel("Pick order will appear here.", 11, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.77f, 0.83f, 0.89f, 1f));
            view.DraftTurnSubLabel.style.marginTop = new StyleLength(new Length(4, LengthUnit.Pixel));
            view.DraftTurnCard.Add(view.DraftTurnLabel);
            view.DraftTurnCard.Add(view.DraftTurnSubLabel);

            draftHeader.Add(view.DraftTitleLabel);
            draftHeader.Add(captainRow);
            draftHeader.Add(view.DraftTurnCard);

            var columns = new VisualElement();
            columns.style.display = DisplayStyle.Flex;
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.justifyContent = Justify.Center;
            columns.style.alignItems = Align.FlexStart;
            columns.style.flexWrap = Wrap.Wrap;

            view.RedTeamContainer = CreatePanelColumn(columns, "RED TEAM", new Color(0.39f, 0.12f, 0.15f, 0.40f), new Color(1f, 0.82f, 0.82f, 1f), false, out view.DraftRedTeamPanel);
            view.AvailablePlayersContainer = CreatePanelColumn(columns, "AVAILABLE PLAYERS", new Color(0.34f, 0.28f, 0.08f, 0.42f), new Color(1f, 0.95f, 0.74f, 1f), true, out view.DraftAvailablePanel);
            view.BlueTeamContainer = CreatePanelColumn(columns, "BLUE TEAM", new Color(0.10f, 0.20f, 0.39f, 0.42f), new Color(0.82f, 0.90f, 1f, 1f), false, out view.DraftBlueTeamPanel);

            var pendingSection = new VisualElement();
            pendingSection.style.display = DisplayStyle.Flex;
            pendingSection.style.flexDirection = FlexDirection.Column;
            pendingSection.style.alignSelf = Align.Center;
            pendingSection.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            pendingSection.style.maxWidth = 840;
            pendingSection.style.marginTop = new StyleLength(new Length(12, LengthUnit.Pixel));
            pendingSection.style.paddingTop = new StyleLength(new Length(12, LengthUnit.Pixel));
            pendingSection.style.paddingBottom = new StyleLength(new Length(12, LengthUnit.Pixel));
            pendingSection.style.paddingLeft = new StyleLength(new Length(14, LengthUnit.Pixel));
            pendingSection.style.paddingRight = new StyleLength(new Length(14, LengthUnit.Pixel));
            pendingSection.style.backgroundColor = new StyleColor(new Color(0.11f, 0.13f, 0.17f, 0.82f));
            pendingSection.style.borderTopLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            pendingSection.style.borderTopRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            pendingSection.style.borderBottomLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            pendingSection.style.borderBottomRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            pendingSection.style.borderTopWidth = 1;
            pendingSection.style.borderRightWidth = 1;
            pendingSection.style.borderBottomWidth = 1;
            pendingSection.style.borderLeftWidth = 1;
            pendingSection.style.borderTopColor = new StyleColor(new Color(1f, 0.90f, 0.66f, 0.20f));
            pendingSection.style.borderRightColor = new StyleColor(new Color(1f, 0.90f, 0.66f, 0.08f));
            pendingSection.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.28f));
            pendingSection.style.borderLeftColor = new StyleColor(new Color(1f, 0.90f, 0.66f, 0.08f));

            var pendingTitle = CreateLabel("PENDING PLAYERS", 13, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.77f, 1f));
            pendingTitle.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            pendingSection.Add(pendingTitle);

            view.PendingPlayersContainer = new VisualElement();
            view.PendingPlayersContainer.style.display = DisplayStyle.Flex;
            view.PendingPlayersContainer.style.flexDirection = FlexDirection.Row;
            view.PendingPlayersContainer.style.flexWrap = Wrap.Wrap;
            pendingSection.Add(view.PendingPlayersContainer);

            view.DraftFooterLabel = CreateLabel("Select a player to add them to your team.", 12, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.76f, 0.82f, 0.88f, 1f));
            view.DraftFooterLabel.style.marginTop = new StyleLength(new Length(12, LengthUnit.Pixel));

            panel.Add(draftHeader);
            panel.Add(columns);
            panel.Add(pendingSection);
            panel.Add(view.DraftFooterLabel);

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
            panel.style.minWidth = isFocusColumn ? 286 : 234;
            panel.style.minHeight = 232;
            panel.style.paddingTop = new StyleLength(new Length(9, LengthUnit.Pixel));
            panel.style.paddingBottom = new StyleLength(new Length(9, LengthUnit.Pixel));
            panel.style.paddingLeft = new StyleLength(new Length(9, LengthUnit.Pixel));
            panel.style.paddingRight = new StyleLength(new Length(9, LengthUnit.Pixel));
            panel.style.backgroundColor = new StyleColor(backgroundColor);
            panel.style.borderTopLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            panel.style.borderTopRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            panel.style.borderBottomLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            panel.style.borderBottomRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            panel.style.borderTopWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderTopColor = new StyleColor(isFocusColumn ? new Color(1f, 0.92f, 0.63f, 0.30f) : new Color(1f, 1f, 1f, 0.16f));
            panel.style.borderRightColor = new StyleColor(isFocusColumn ? new Color(1f, 0.92f, 0.63f, 0.14f) : new Color(1f, 1f, 1f, 0.08f));
            panel.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.30f));
            panel.style.borderLeftColor = new StyleColor(isFocusColumn ? new Color(1f, 0.92f, 0.63f, 0.14f) : new Color(1f, 1f, 1f, 0.08f));
            panel.style.marginLeft = new StyleLength(new Length(6, LengthUnit.Pixel));
            panel.style.marginRight = new StyleLength(new Length(6, LengthUnit.Pixel));
            panel.style.marginBottom = new StyleLength(new Length(12, LengthUnit.Pixel));

            var accent = new VisualElement();
            accent.style.width = 36;
            accent.style.height = 3;
            accent.style.alignSelf = Align.Center;
            accent.style.backgroundColor = new StyleColor(new Color(titleColor.r, titleColor.g, titleColor.b, isFocusColumn ? 0.98f : 0.78f));
            accent.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            accent.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            accent.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            accent.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            accent.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));

            var title = CreateLabel(titleText, 14, FontStyle.Bold, TextAnchor.MiddleCenter, titleColor);
            title.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            panel.Add(accent);
            panel.Add(title);

            var content = new VisualElement();
            content.style.display = DisplayStyle.Flex;
            content.style.flexDirection = FlexDirection.Column;
            panel.Add(content);
            parent.Add(panel);
            return content;
        }

        private static void PopulatePlayerEntries(VisualElement container, IEnumerable<DraftOverlayPlayerEntryMessage> entries, Color backgroundColor, bool clickable, Action<string> onClick, TeamResult currentTurnTeam)
        {
            container.Clear();

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
            card.style.paddingTop = new StyleLength(new Length(6, LengthUnit.Pixel));
            card.style.paddingBottom = new StyleLength(new Length(6, LengthUnit.Pixel));
            card.style.paddingLeft = new StyleLength(new Length(9, LengthUnit.Pixel));
            card.style.paddingRight = new StyleLength(new Length(9, LengthUnit.Pixel));
            card.style.borderTopLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.borderTopRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.borderBottomLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.borderBottomRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.borderTopWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderTopColor = new StyleColor(ResolveDraftRowBorder(entry, isCurrentTurnCaptain));
            card.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, entry.IsCaptain ? 0.12f : 0.06f));
            card.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.30f));
            card.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, entry.IsCaptain ? 0.12f : 0.06f));
            card.style.marginBottom = new StyleLength(new Length(6, LengthUnit.Pixel));
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
                Tint(backgroundColor, 1.14f, 0.08f),
                Tint(backgroundColor, 0.90f, 0.02f),
                Color.white));
            button.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            button.style.height = 46;
            button.style.minWidth = 0;
            button.style.marginRight = new StyleLength(new Length(6, LengthUnit.Pixel));
            button.style.marginBottom = new StyleLength(new Length(6, LengthUnit.Pixel));
            button.style.paddingLeft = new StyleLength(new Length(9, LengthUnit.Pixel));
            button.style.paddingRight = new StyleLength(new Length(9, LengthUnit.Pixel));
            button.style.paddingTop = new StyleLength(new Length(6, LengthUnit.Pixel));
            button.style.paddingBottom = new StyleLength(new Length(6, LengthUnit.Pixel));
            button.style.alignItems = Align.Stretch;
            button.style.justifyContent = Justify.Center;
            button.style.flexDirection = FlexDirection.Row;
            button.style.whiteSpace = WhiteSpace.Normal;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.borderTopColor = new StyleColor(new Color(1f, 0.94f, 0.72f, 0.34f));
            button.style.borderRightColor = new StyleColor(new Color(1f, 0.94f, 0.72f, 0.16f));
            button.style.borderLeftColor = new StyleColor(new Color(1f, 0.94f, 0.72f, 0.16f));
            button.SetEnabled(!string.IsNullOrWhiteSpace(commandTarget));
            button.Add(CreateDraftRowContent(entry, true));
            return button;
        }

        private static VisualElement CreateDraftRowContent(DraftOverlayPlayerEntryMessage entry, bool clickable)
        {
            var row = new VisualElement();
            row.style.display = DisplayStyle.Flex;
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.flexGrow = 1;

            var left = new VisualElement();
            left.style.display = DisplayStyle.Flex;
            left.style.flexDirection = FlexDirection.Row;
            left.style.alignItems = Align.Center;
            left.style.flexGrow = 1;
            left.style.minWidth = 0;

            left.Add(CreateDraftAvatarFrame(entry, entry != null && entry.IsCaptain ? 30 : 26));

            var textStack = new VisualElement();
            textStack.style.display = DisplayStyle.Flex;
            textStack.style.flexDirection = FlexDirection.Column;
            textStack.style.flexGrow = 1;
            textStack.style.minWidth = 0;
            textStack.style.marginRight = new StyleLength(new Length(10, LengthUnit.Pixel));

            var nameRow = new VisualElement();
            nameRow.style.display = DisplayStyle.Flex;
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;
            nameRow.style.minWidth = 0;

            var nameLabel = CreateLabel(FormatPlayerIdentity(entry), 13, entry != null && entry.IsCaptain ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft, entry != null && entry.IsCaptain ? new Color(1f, 0.95f, 0.84f, 1f) : ReadableTeamColor(entry != null ? entry.Team : TeamResult.Unknown));
            nameLabel.style.flexGrow = 1;
            nameLabel.style.minWidth = 0;
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            nameLabel.style.overflow = Overflow.Hidden;
            nameRow.Add(nameLabel);

            textStack.Add(nameRow);
            left.Add(textStack);

            var right = new VisualElement();
            right.style.display = DisplayStyle.Flex;
            right.style.flexDirection = FlexDirection.Row;
            right.style.alignItems = Align.Center;
            right.style.flexShrink = 0;
            right.style.marginLeft = new StyleLength(new Length(12, LengthUnit.Pixel));

            if (entry != null && entry.IsCaptain)
            {
                var captainTag = CreateDraftPill("♛ CAPTAIN", new Color(1f, 0.90f, 0.58f, 0.16f), new Color(1f, 0.93f, 0.76f, 1f), 9);
                captainTag.style.marginRight = new StyleLength(new Length(12, LengthUnit.Pixel));
                right.Add(captainTag);
            }

            right.Add(CreateDraftPill(FormatMmr(entry), new Color(0.14f, 0.19f, 0.25f, 0.82f), new Color(0.98f, 0.94f, 0.72f, 1f), 10));

            row.Add(left);
            row.Add(right);
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
                ? new Color(0.76f, 0.62f, 0.20f, 0.90f)
                : new Color(0.22f, 0.29f, 0.37f, 0.92f));
            frame.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            frame.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            frame.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            frame.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            frame.style.borderTopWidth = 1;
            frame.style.borderRightWidth = 1;
            frame.style.borderBottomWidth = 1;
            frame.style.borderLeftWidth = 1;
            frame.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.18f));
            frame.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));
            frame.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.24f));
            frame.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));
            frame.style.overflow = Overflow.Hidden;

            if (VoteAvatarCache.TryGetAvatarTexture(entry?.SteamId, out var avatarTexture) && avatarTexture != null)
            {
                var avatarImage = new Image();
                avatarImage.image = avatarTexture;
                avatarImage.scaleMode = ScaleMode.ScaleAndCrop;
                avatarImage.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
                avatarImage.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
                frame.Add(avatarImage);
            }
            else
            {
                frame.Add(CreateLabel(ResolveDraftAvatarInitial(entry), size >= 30 ? 13 : 11, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.98f, 0.99f, 1f, 1f)));
            }

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

            var displayName = NormalizeDraftDisplayName(entry.DisplayName);
            if (entry.PlayerNumber > 0)
            {
                var playerPrefix = $"#{entry.PlayerNumber} ";
                if (!displayName.StartsWith(playerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return playerPrefix + displayName;
                }
            }

            return displayName;
        }

        private static string NormalizeDraftDisplayName(string displayName)
        {
            var clean = string.IsNullOrWhiteSpace(displayName) ? "---" : displayName.Trim();
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

        private static string NormalizeCaptainName(string displayName)
        {
            return string.IsNullOrWhiteSpace(displayName) ? "Pending" : NormalizeDraftDisplayName(displayName);
        }

        private static VisualElement CreateCaptainSummaryCard(string title, TeamResult team, out VisualElement card, out Label valueLabel)
        {
            var backgroundColor = team == TeamResult.Red
                ? new Color(0.29f, 0.11f, 0.14f, 0.82f)
                : new Color(0.10f, 0.18f, 0.33f, 0.82f);
            var titleColor = team == TeamResult.Red
                ? new Color(1f, 0.83f, 0.83f, 1f)
                : new Color(0.84f, 0.92f, 1f, 1f);

            card = new VisualElement();
            card.style.display = DisplayStyle.Flex;
            card.style.flexDirection = FlexDirection.Column;
            card.style.alignItems = Align.Center;
            card.style.justifyContent = Justify.Center;
            card.style.minWidth = 220;
            card.style.marginLeft = new StyleLength(new Length(6, LengthUnit.Pixel));
            card.style.marginRight = new StyleLength(new Length(6, LengthUnit.Pixel));
            card.style.marginBottom = new StyleLength(new Length(6, LengthUnit.Pixel));
            card.style.paddingTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            card.style.paddingBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            card.style.paddingLeft = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.paddingRight = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.backgroundColor = new StyleColor(backgroundColor);
            card.style.borderTopLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            card.style.borderTopRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            card.style.borderBottomLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            card.style.borderBottomRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            card.style.borderTopWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderTopColor = new StyleColor(new Color(titleColor.r, titleColor.g, titleColor.b, 0.32f));
            card.style.borderRightColor = new StyleColor(new Color(titleColor.r, titleColor.g, titleColor.b, 0.12f));
            card.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.26f));
            card.style.borderLeftColor = new StyleColor(new Color(titleColor.r, titleColor.g, titleColor.b, 0.12f));

            var titleLabel = CreateLabel(title, 11, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(titleColor.r, titleColor.g, titleColor.b, 0.88f));
            valueLabel = CreateLabel("Pending", 14, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            valueLabel.style.marginTop = new StyleLength(new Length(4, LengthUnit.Pixel));
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
                ? new Color(1f, 0.46f, 0.46f, isActiveTurn ? 0.44f : 0.24f)
                : new Color(0.47f, 0.72f, 1f, isActiveTurn ? 0.44f : 0.24f);
            var background = team == TeamResult.Red
                ? new Color(0.29f, 0.11f, 0.14f, isActiveTurn && !isCompleted ? 0.90f : 0.80f)
                : new Color(0.10f, 0.18f, 0.33f, isActiveTurn && !isCompleted ? 0.90f : 0.80f);

            card.style.backgroundColor = new StyleColor(background);
            card.style.borderTopColor = new StyleColor(accent);
            card.style.borderRightColor = new StyleColor(new Color(accent.r, accent.g, accent.b, accent.a * 0.42f));
            card.style.borderLeftColor = new StyleColor(new Color(accent.r, accent.g, accent.b, accent.a * 0.42f));
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
                return new Color(0.34f, 0.24f, 0.10f, 0.94f);
            }

            return entry != null && entry.IsCaptain
                ? Tint(backgroundColor, 1.16f, 0.12f)
                : backgroundColor;
        }

        private static Color ResolveDraftRowBorder(DraftOverlayPlayerEntryMessage entry, bool isCurrentTurnCaptain)
        {
            if (isCurrentTurnCaptain)
            {
                return new Color(1f, 0.88f, 0.52f, 0.52f);
            }

            return entry != null && entry.IsCaptain
                ? new Color(1f, 0.90f, 0.58f, 0.44f)
                : new Color(1f, 1f, 1f, 0.14f);
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
            switch (team)
            {
                case TeamResult.Red:
                    return new Color(0.60f, 0.18f, 0.18f, 0.55f);
                case TeamResult.Blue:
                    return new Color(0.18f, 0.30f, 0.55f, 0.55f);
                default:
                    return new Color(0.25f, 0.25f, 0.25f, 0.55f);
            }
        }

        private static Color ReadableTeamColor(TeamResult team)
        {
            switch (team)
            {
                case TeamResult.Red:
                    return new Color(1f, 0.75f, 0.75f, 1f);
                case TeamResult.Blue:
                    return new Color(0.75f, 0.85f, 1f, 1f);
                default:
                    return Color.white;
            }
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
            button.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.18f));
            button.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));
            button.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.30f));
            button.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));
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