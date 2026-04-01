using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace schrader
{
    public static class DraftUIRenderer
    {
        public sealed class View
        {
            public VisualElement Container;
            public VisualElement Backdrop;
            public VisualElement Root;
            internal WelcomeUIRenderer.View Welcome;
            public VisualElement VotingPanel;
            public Label VotingTitleLabel;
            public Label VotingPromptLabel;
            public Label VotingTimerLabel;
            public Label VotingStatsLabel;
            public Label VotingFooterLabel;
            public Button VotingAcceptButton;
            public Button VotingRejectButton;
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
            public Label DraftCaptainLabel;
            public Label DraftTurnLabel;
            public Label DraftFooterLabel;
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
            view.Container.style.display = DisplayStyle.None;
            view.Container.style.opacity = 0f;
            if (view.Welcome?.Panel != null) view.Welcome.Panel.style.display = DisplayStyle.None;
            if (view.PostMatch?.Panel != null) view.PostMatch.Panel.style.display = DisplayStyle.None;
        }

        public static void ShowVoting(View view)
        {
            if (view == null) return;
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
            view.Container.style.display = DisplayStyle.Flex;
            view.VotingPanel.style.display = DisplayStyle.None;
            view.ApprovalPanel.style.display = DisplayStyle.None;
            if (view.Welcome?.Panel != null) view.Welcome.Panel.style.display = DisplayStyle.None;
            view.DraftPanel.style.display = DisplayStyle.Flex;
            if (view.PostMatch?.Panel != null) view.PostMatch.Panel.style.display = DisplayStyle.None;
        }

        public static void RenderVoting(View view, VoteOverlayStateMessage state, int secondsRemaining, bool hasAccepted)
        {
            if (view == null || state == null) return;

            view.VotingTitleLabel.text = string.IsNullOrWhiteSpace(state.Title) ? "Ranked Match Found" : state.Title;
            view.VotingPromptLabel.text = string.IsNullOrWhiteSpace(state.PromptText) ? "Enable ranked captain draft for this lobby?" : state.PromptText;
            view.VotingTimerLabel.text = $"0:{secondsRemaining:00}";
            view.VotingStatsLabel.text = string.Join("\n", new[]
            {
                string.IsNullOrWhiteSpace(state.InitiatorName) ? "Vote started" : $"Started by {state.InitiatorName}",
                $"Yes {state.YesVotes} / Need {state.RequiredYesVotes}",
                $"No {state.NoVotes} / Total {state.EligibleCount}"
            });
            view.VotingFooterLabel.text = hasAccepted
                ? "Waiting for the rest of the lobby..."
                : (string.IsNullOrWhiteSpace(state.FooterText) ? "Vote with Accept or Reject." : state.FooterText);
            view.VotingAcceptButton.SetEnabled(!hasAccepted);
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

        public static void RenderDraft(View view, DraftOverlayStateMessage state, DraftOverlayExtendedMessage extendedState, Action<string> onPickPlayer, Action<string> onAcceptLateJoiner)
        {
            if (view == null || state == null) return;

            extendedState = extendedState ?? DraftOverlayExtendedMessage.Hidden();

            view.DraftTitleLabel.text = string.IsNullOrWhiteSpace(state.Title) ? "Ranked Draft" : state.Title;
            view.DraftCaptainLabel.text = $"RED CAPTAIN: {state.RedCaptainName ?? "Pending"}\nBLUE CAPTAIN: {state.BlueCaptainName ?? "Pending"}";
            view.DraftTurnLabel.text = state.IsCompleted
                ? "Draft complete"
                : $"Turn: {state.CurrentTurnName ?? "Pending"}";
            view.DraftFooterLabel.text = string.IsNullOrWhiteSpace(state.FooterText)
                ? "Pick with the overlay buttons or use chat fallback."
                : state.FooterText;

            PopulatePlayerEntries(view.RedTeamContainer, ResolveEntries(extendedState.RedPlayerEntries, state.RedPlayers, TeamResult.Red), TeamColor(TeamResult.Red), false, null);
            PopulatePlayerEntries(view.BlueTeamContainer, ResolveEntries(extendedState.BluePlayerEntries, state.BluePlayers, TeamResult.Blue), TeamColor(TeamResult.Blue), false, null);
            PopulatePlayerEntries(view.AvailablePlayersContainer, ResolveEntries(extendedState.AvailablePlayerEntries, state.AvailablePlayers, TeamResult.Unknown), new Color(0.29f, 0.39f, 0.18f, 0.62f), true, onPickPlayer);
            PopulatePlayerEntries(view.PendingPlayersContainer, ResolveEntries(extendedState.PendingLateJoinerEntries, state.PendingLateJoiners, TeamResult.Unknown), new Color(0.62f, 0.50f, 0.18f, 0.55f), true, onAcceptLateJoiner);
        }

        private static void BuildVotingUI(View view, Action onVoteAccepted, Action onVoteRejected)
        {
            var panel = new VisualElement();
            panel.name = "VotingPanel";
            panel.style.display = DisplayStyle.None;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.alignSelf = Align.Center;
            panel.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            panel.style.maxWidth = 720;
            panel.style.paddingTop = new StyleLength(new Length(28, LengthUnit.Pixel));
            panel.style.paddingBottom = new StyleLength(new Length(24, LengthUnit.Pixel));
            panel.style.paddingLeft = new StyleLength(new Length(28, LengthUnit.Pixel));
            panel.style.paddingRight = new StyleLength(new Length(28, LengthUnit.Pixel));
            panel.style.backgroundColor = new StyleColor(new Color(0.08f, 0.12f, 0.16f, 0.88f));
            panel.style.borderTopLeftRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            panel.style.borderTopRightRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            panel.style.borderBottomLeftRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            panel.style.borderBottomRightRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            panel.style.borderTopWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderTopColor = new StyleColor(new Color(0.44f, 0.67f, 0.76f, 0.30f));
            panel.style.borderRightColor = new StyleColor(new Color(0.44f, 0.67f, 0.76f, 0.20f));
            panel.style.borderBottomColor = new StyleColor(new Color(0.02f, 0.03f, 0.05f, 0.75f));
            panel.style.borderLeftColor = new StyleColor(new Color(0.44f, 0.67f, 0.76f, 0.20f));

            view.VotingTitleLabel = CreateLabel("Ranked Match Found", 30, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.97f, 0.98f, 0.99f, 1f));
            view.VotingPromptLabel = CreateLabel("Enable ranked captain draft for this lobby?", 17, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.82f, 0.88f, 0.92f, 1f));
            view.VotingPromptLabel.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));

            view.VotingTimerLabel = CreateLabel("0:45", 30, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.99f, 0.91f, 0.52f, 1f));
            view.VotingTimerLabel.style.marginTop = new StyleLength(new Length(16, LengthUnit.Pixel));

            view.VotingStatsLabel = CreateLabel(string.Empty, 15, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.92f, 0.95f, 0.98f, 1f));
            view.VotingStatsLabel.style.marginTop = new StyleLength(new Length(14, LengthUnit.Pixel));
            view.VotingStatsLabel.style.whiteSpace = WhiteSpace.Normal;

            var votingStatsCard = new VisualElement();
            votingStatsCard.style.marginTop = new StyleLength(new Length(16, LengthUnit.Pixel));
            votingStatsCard.style.paddingTop = new StyleLength(new Length(14, LengthUnit.Pixel));
            votingStatsCard.style.paddingBottom = new StyleLength(new Length(14, LengthUnit.Pixel));
            votingStatsCard.style.paddingLeft = new StyleLength(new Length(18, LengthUnit.Pixel));
            votingStatsCard.style.paddingRight = new StyleLength(new Length(18, LengthUnit.Pixel));
            votingStatsCard.style.backgroundColor = new StyleColor(new Color(0.10f, 0.15f, 0.20f, 0.82f));
            votingStatsCard.style.borderTopLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            votingStatsCard.style.borderTopRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            votingStatsCard.style.borderBottomLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            votingStatsCard.style.borderBottomRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            votingStatsCard.Add(view.VotingStatsLabel);

            view.VotingFooterLabel = CreateLabel(string.Empty, 13, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.70f, 0.79f, 0.85f, 1f));
            view.VotingFooterLabel.style.marginTop = new StyleLength(new Length(14, LengthUnit.Pixel));

            var buttonRow = new VisualElement();
            buttonRow.style.display = DisplayStyle.Flex;
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.Center;
            buttonRow.style.alignItems = Align.Center;
            buttonRow.style.marginTop = new StyleLength(new Length(22, LengthUnit.Pixel));
            buttonRow.style.flexWrap = Wrap.Wrap;

            view.VotingAcceptButton = new Button(() => onVoteAccepted?.Invoke())
            {
                text = "Accept"
            };
            StyleButton(view.VotingAcceptButton, new ButtonPalette(
                new Color(0.18f, 0.56f, 0.36f, 0.98f),
                new Color(0.24f, 0.67f, 0.44f, 1f),
                new Color(0.12f, 0.45f, 0.29f, 1f),
                Color.white));

            view.VotingRejectButton = new Button(() => onVoteRejected?.Invoke())
            {
                text = "Reject"
            };
            StyleButton(view.VotingRejectButton, new ButtonPalette(
                new Color(0.66f, 0.24f, 0.24f, 0.98f),
                new Color(0.79f, 0.30f, 0.30f, 1f),
                new Color(0.53f, 0.18f, 0.18f, 1f),
                Color.white));
            view.VotingRejectButton.style.marginLeft = new StyleLength(new Length(12, LengthUnit.Pixel));

            buttonRow.Add(view.VotingAcceptButton);
            buttonRow.Add(view.VotingRejectButton);

            panel.Add(view.VotingTitleLabel);
            panel.Add(view.VotingPromptLabel);
            panel.Add(view.VotingTimerLabel);
            panel.Add(votingStatsCard);
            panel.Add(buttonRow);
            panel.Add(view.VotingFooterLabel);

            view.Root.Add(panel);
            view.VotingPanel = panel;
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

            var draftHeader = new VisualElement();
            draftHeader.style.display = DisplayStyle.Flex;
            draftHeader.style.flexDirection = FlexDirection.Column;
            draftHeader.style.paddingTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            draftHeader.style.paddingBottom = new StyleLength(new Length(18, LengthUnit.Pixel));

            view.DraftTitleLabel = CreateLabel("Ranked Draft", 30, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.97f, 0.98f, 0.99f, 1f));
            view.DraftCaptainLabel = CreateLabel(string.Empty, 16, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.83f, 0.89f, 0.94f, 1f));
            view.DraftCaptainLabel.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            view.DraftTurnLabel = CreateLabel("Turn: RED", 18, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.99f, 0.91f, 0.52f, 1f));
            view.DraftTurnLabel.style.marginTop = new StyleLength(new Length(8, LengthUnit.Pixel));

            draftHeader.Add(view.DraftTitleLabel);
            draftHeader.Add(view.DraftCaptainLabel);
            draftHeader.Add(view.DraftTurnLabel);

            var columns = new VisualElement();
            columns.style.display = DisplayStyle.Flex;
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.justifyContent = Justify.Center;
            columns.style.alignItems = Align.FlexStart;
            columns.style.flexWrap = Wrap.Wrap;

            view.RedTeamContainer = CreatePanelColumn(columns, "RED TEAM", new Color(0.44f, 0.14f, 0.17f, 0.44f), new Color(1f, 0.79f, 0.79f, 1f));
            view.AvailablePlayersContainer = CreatePanelColumn(columns, "AVAILABLE PLAYERS", new Color(0.43f, 0.35f, 0.10f, 0.42f), new Color(1f, 0.94f, 0.73f, 1f));
            view.BlueTeamContainer = CreatePanelColumn(columns, "BLUE TEAM", new Color(0.13f, 0.24f, 0.45f, 0.46f), new Color(0.79f, 0.89f, 1f, 1f));
            if (view.AvailablePlayersContainer.parent != null)
            {
                view.AvailablePlayersContainer.parent.style.minWidth = 340;
                view.AvailablePlayersContainer.parent.style.borderTopColor = new StyleColor(new Color(1f, 0.92f, 0.63f, 0.32f));
                view.AvailablePlayersContainer.parent.style.borderRightColor = new StyleColor(new Color(1f, 0.92f, 0.63f, 0.18f));
                view.AvailablePlayersContainer.parent.style.borderLeftColor = new StyleColor(new Color(1f, 0.92f, 0.63f, 0.18f));
            }

            var pendingSection = new VisualElement();
            pendingSection.style.display = DisplayStyle.Flex;
            pendingSection.style.flexDirection = FlexDirection.Column;
            pendingSection.style.marginTop = new StyleLength(new Length(18, LengthUnit.Pixel));
            pendingSection.style.paddingTop = new StyleLength(new Length(16, LengthUnit.Pixel));
            pendingSection.style.paddingBottom = new StyleLength(new Length(16, LengthUnit.Pixel));
            pendingSection.style.paddingLeft = new StyleLength(new Length(18, LengthUnit.Pixel));
            pendingSection.style.paddingRight = new StyleLength(new Length(18, LengthUnit.Pixel));
            pendingSection.style.backgroundColor = new StyleColor(new Color(0.11f, 0.13f, 0.17f, 0.82f));
            pendingSection.style.borderTopLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            pendingSection.style.borderTopRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            pendingSection.style.borderBottomLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            pendingSection.style.borderBottomRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));

            var pendingTitle = CreateLabel("PENDING PLAYERS", 15, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.77f, 1f));
            pendingTitle.style.marginBottom = new StyleLength(new Length(10, LengthUnit.Pixel));
            pendingSection.Add(pendingTitle);

            view.PendingPlayersContainer = new VisualElement();
            view.PendingPlayersContainer.style.display = DisplayStyle.Flex;
            view.PendingPlayersContainer.style.flexDirection = FlexDirection.Row;
            view.PendingPlayersContainer.style.flexWrap = Wrap.Wrap;
            pendingSection.Add(view.PendingPlayersContainer);

            view.DraftFooterLabel = CreateLabel(string.Empty, 13, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.71f, 0.80f, 0.86f, 1f));
            view.DraftFooterLabel.style.marginTop = new StyleLength(new Length(16, LengthUnit.Pixel));

            panel.Add(draftHeader);
            panel.Add(columns);
            panel.Add(pendingSection);
            panel.Add(view.DraftFooterLabel);

            view.Root.Add(panel);
            view.DraftPanel = panel;
        }

        private static VisualElement CreatePanelColumn(VisualElement parent, string titleText, Color backgroundColor, Color titleColor)
        {
            var panel = new VisualElement();
            panel.style.display = DisplayStyle.Flex;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.flexGrow = 1;
            panel.style.flexBasis = new StyleLength(new Length(0, LengthUnit.Pixel));
            panel.style.minWidth = 280;
            panel.style.minHeight = 280;
            panel.style.paddingTop = new StyleLength(new Length(14, LengthUnit.Pixel));
            panel.style.paddingBottom = new StyleLength(new Length(14, LengthUnit.Pixel));
            panel.style.paddingLeft = new StyleLength(new Length(14, LengthUnit.Pixel));
            panel.style.paddingRight = new StyleLength(new Length(14, LengthUnit.Pixel));
            panel.style.backgroundColor = new StyleColor(backgroundColor);
            panel.style.borderTopLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            panel.style.borderTopRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            panel.style.borderBottomLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            panel.style.borderBottomRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            panel.style.borderTopWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.16f));
            panel.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));
            panel.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.30f));
            panel.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));
            panel.style.marginLeft = new StyleLength(new Length(9, LengthUnit.Pixel));
            panel.style.marginRight = new StyleLength(new Length(9, LengthUnit.Pixel));
            panel.style.marginBottom = new StyleLength(new Length(18, LengthUnit.Pixel));

            var title = CreateLabel(titleText, 16, FontStyle.Bold, TextAnchor.MiddleCenter, titleColor);
            title.style.marginBottom = new StyleLength(new Length(12, LengthUnit.Pixel));
            panel.Add(title);

            var content = new VisualElement();
            content.style.display = DisplayStyle.Flex;
            content.style.flexDirection = FlexDirection.Column;
            panel.Add(content);
            parent.Add(panel);
            return content;
        }

        private static void PopulatePlayerEntries(VisualElement container, IEnumerable<DraftOverlayPlayerEntryMessage> entries, Color backgroundColor, bool clickable, Action<string> onClick)
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

                if (clickable)
                {
                    var button = new Button(() =>
                    {
                        if (!string.IsNullOrWhiteSpace(commandTarget))
                        {
                            onClick?.Invoke(commandTarget);
                        }
                    })
                    {
                        text = FormatPlayerLine(entry)
                    };
                    var textColor = backgroundColor.r > 0.55f && backgroundColor.g > 0.40f
                        ? new Color(0.14f, 0.12f, 0.08f, 1f)
                        : Color.white;
                    StyleButton(button, new ButtonPalette(
                        backgroundColor,
                        Tint(backgroundColor, 1.12f, 0.08f),
                        Tint(backgroundColor, 0.88f, 0.02f),
                        textColor));
                    button.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
                    button.style.height = 42;
                    button.style.minWidth = 0;
                    button.style.marginRight = new StyleLength(new Length(8, LengthUnit.Pixel));
                    button.SetEnabled(!string.IsNullOrWhiteSpace(commandTarget));
                    container.Add(button);
                }
                else
                {
                    container.Add(CreatePlayerCard(entry, backgroundColor));
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
                .Select(name => new DraftOverlayPlayerEntryMessage
                {
                    CommandTarget = null,
                    DisplayName = name,
                    PlayerNumber = 0,
                    HasMmr = true,
                    Mmr = Constants.DEFAULT_MMR,
                    IsCaptain = false,
                    Team = fallbackTeam
                });
        }

        private static VisualElement CreatePlayerCard(DraftOverlayPlayerEntryMessage entry, Color backgroundColor)
        {
            var card = new VisualElement();
            card.style.display = DisplayStyle.Flex;
            card.style.flexDirection = FlexDirection.Row;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.alignItems = Align.Center;
            card.style.backgroundColor = new StyleColor(entry.IsCaptain ? Tint(backgroundColor, 1.18f, 0.10f) : backgroundColor);
            card.style.paddingTop = new StyleLength(new Length(9, LengthUnit.Pixel));
            card.style.paddingBottom = new StyleLength(new Length(9, LengthUnit.Pixel));
            card.style.paddingLeft = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.paddingRight = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.borderTopLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.borderTopRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.borderBottomLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.borderBottomRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.borderTopWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderTopColor = new StyleColor(entry.IsCaptain ? new Color(1f, 0.90f, 0.58f, 0.50f) : new Color(1f, 1f, 1f, 0.14f));
            card.style.borderRightColor = new StyleColor(entry.IsCaptain ? new Color(1f, 0.90f, 0.58f, 0.22f) : new Color(1f, 1f, 1f, 0.06f));
            card.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.30f));
            card.style.borderLeftColor = new StyleColor(entry.IsCaptain ? new Color(1f, 0.90f, 0.58f, 0.22f) : new Color(1f, 1f, 1f, 0.06f));
            card.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));

            var nameRow = new VisualElement();
            nameRow.style.display = DisplayStyle.Flex;
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;
            nameRow.style.flexGrow = 1;

            if (entry.IsCaptain)
            {
                var crown = CreateLabel("♛", 16, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.90f, 0.58f, 1f));
                crown.style.marginRight = new StyleLength(new Length(8, LengthUnit.Pixel));
                nameRow.Add(crown);
            }

            var nameLabel = CreateLabel(FormatPlayerIdentity(entry), 14, entry.IsCaptain ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft, entry.IsCaptain ? new Color(1f, 0.95f, 0.84f, 1f) : ReadableTeamColor(entry.Team));
            nameLabel.style.flexGrow = 1;
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            nameLabel.style.overflow = Overflow.Hidden;
            nameRow.Add(nameLabel);

            var mmrLabel = CreateLabel(FormatMmr(entry), 13, entry.IsCaptain ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleRight, new Color(0.98f, 0.94f, 0.72f, 1f));
            mmrLabel.style.marginLeft = new StyleLength(new Length(12, LengthUnit.Pixel));
            mmrLabel.style.flexShrink = 0;

            card.Add(nameRow);
            card.Add(mmrLabel);
            return card;
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