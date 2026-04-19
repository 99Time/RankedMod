using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace schrader
{
    internal static class PostMatchUIRenderer
    {
        private const int AvatarRefreshMaxAttempts = 30;
        private const int AvatarRefreshIntervalMilliseconds = 300;

        private const float PanelEntranceDurationSeconds = 0.24f;

        internal sealed class TeamSectionView
        {
            public VisualElement Panel;
            public Label StatusLabel;
            public Label SummaryLabel;
            public Label RosterLabel;
            public VisualElement MvpContainer;
            public VisualElement PlayersContainer;
        }

        public sealed class View
        {
            public VisualElement Panel;
            public Label TitleLabel;
            public Label WinnerLabel;
            internal TeamSectionView RedTeam;
            internal TeamSectionView BlueTeam;
            public Button HostButton;
            public Button ContinueButton;
            public Button CloseButton;
        }

        public static void BuildUI(DraftUIRenderer.View rootView, Action onOpenHost, Action onContinue, Action onClose)
        {
            if (rootView?.Root == null)
            {
                return;
            }

            var view = new View();
            var panel = new VisualElement();
            panel.name = "PostMatchPanel";
            panel.style.display = DisplayStyle.None;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            panel.style.maxWidth = 1024;
            panel.style.alignSelf = Align.Center;
            panel.style.paddingTop = new StyleLength(new Length(22, LengthUnit.Pixel));
            panel.style.paddingBottom = new StyleLength(new Length(22, LengthUnit.Pixel));
            panel.style.paddingLeft = new StyleLength(new Length(24, LengthUnit.Pixel));
            panel.style.paddingRight = new StyleLength(new Length(24, LengthUnit.Pixel));
            panel.style.backgroundColor = new StyleColor(new Color(0.05f, 0.08f, 0.12f, 0.965f));
            panel.style.borderTopLeftRadius = new StyleLength(new Length(30, LengthUnit.Pixel));
            panel.style.borderTopRightRadius = new StyleLength(new Length(30, LengthUnit.Pixel));
            panel.style.borderBottomLeftRadius = new StyleLength(new Length(30, LengthUnit.Pixel));
            panel.style.borderBottomRightRadius = new StyleLength(new Length(30, LengthUnit.Pixel));
            panel.style.borderTopWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            ApplyUniformBorder(panel, new Color(1f, 1f, 1f, 0.07f));
            panel.style.opacity = 0f;
            panel.transform.scale = new Vector3(0.985f, 0.985f, 1f);
            panel.transform.position = new Vector3(0f, 12f, 0f);

            var headerChip = new VisualElement();
            headerChip.style.alignSelf = Align.Center;
            headerChip.style.paddingTop = new StyleLength(new Length(5, LengthUnit.Pixel));
            headerChip.style.paddingBottom = new StyleLength(new Length(5, LengthUnit.Pixel));
            headerChip.style.paddingLeft = new StyleLength(new Length(14, LengthUnit.Pixel));
            headerChip.style.paddingRight = new StyleLength(new Length(14, LengthUnit.Pixel));
            headerChip.style.marginBottom = new StyleLength(new Length(14, LengthUnit.Pixel));
            headerChip.style.backgroundColor = new StyleColor(new Color(0.16f, 0.21f, 0.29f, 0.72f));
            headerChip.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            headerChip.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            headerChip.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            headerChip.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            headerChip.style.borderTopWidth = 1;
            headerChip.style.borderRightWidth = 1;
            headerChip.style.borderBottomWidth = 1;
            headerChip.style.borderLeftWidth = 1;
            ApplyUniformBorder(headerChip, new Color(1f, 1f, 1f, 0.08f));
            headerChip.Add(CreateLabel("MATCH FINAL", 12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.86f, 0.92f, 0.98f, 1f)));

            view.TitleLabel = CreateLabel("FINAL RESULTS", 34, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.98f, 0.99f, 1f, 1f));
            view.WinnerLabel = CreateLabel("Results Ready", 14, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.84f, 0.89f, 0.95f, 0.92f));
            view.WinnerLabel.style.marginTop = new StyleLength(new Length(8, LengthUnit.Pixel));

            var teamsRow = new VisualElement();
            teamsRow.style.display = DisplayStyle.Flex;
            teamsRow.style.flexDirection = FlexDirection.Row;
            teamsRow.style.justifyContent = Justify.Center;
            teamsRow.style.alignItems = Align.Stretch;
            teamsRow.style.flexWrap = Wrap.Wrap;
            teamsRow.style.marginTop = new StyleLength(new Length(26, LengthUnit.Pixel));

            view.RedTeam = BuildTeamPanel(teamsRow, TeamResult.Red);
            view.BlueTeam = BuildTeamPanel(teamsRow, TeamResult.Blue);

            var buttonRow = new VisualElement();
            buttonRow.style.display = DisplayStyle.Flex;
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.Center;
            buttonRow.style.alignItems = Align.Center;
            buttonRow.style.flexWrap = Wrap.Wrap;
            buttonRow.style.marginTop = new StyleLength(new Length(22, LengthUnit.Pixel));
            buttonRow.style.paddingTop = new StyleLength(new Length(16, LengthUnit.Pixel));
            buttonRow.style.borderTopWidth = 1;
            buttonRow.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));

            view.HostButton = new Button(() => onOpenHost?.Invoke())
            {
                text = "Host My Server"
            };
            StyleButton(view.HostButton, new ButtonPalette(
                new Color(0.14f, 0.42f, 0.78f, 0.98f),
                new Color(0.20f, 0.52f, 0.92f, 1f),
                new Color(0.10f, 0.30f, 0.60f, 1f),
                Color.white));
            view.HostButton.style.minWidth = 172;
            view.HostButton.style.marginRight = new StyleLength(new Length(6, LengthUnit.Pixel));

            view.ContinueButton = new Button(() => onContinue?.Invoke())
            {
                text = "Continue"
            };
            StyleButton(view.ContinueButton, new ButtonPalette(
                new Color(0.74f, 0.54f, 0.24f, 0.98f),
                new Color(0.82f, 0.61f, 0.29f, 1f),
                new Color(0.60f, 0.42f, 0.18f, 1f),
                Color.white));
            view.ContinueButton.style.height = 44;
            view.ContinueButton.style.minWidth = 208;

            view.CloseButton = new Button(() => onClose?.Invoke())
            {
                text = "Close"
            };
            StyleButton(view.CloseButton, new ButtonPalette(
                new Color(0.19f, 0.23f, 0.29f, 0.98f),
                new Color(0.25f, 0.30f, 0.38f, 1f),
                new Color(0.14f, 0.17f, 0.22f, 1f),
                new Color(0.94f, 0.96f, 0.99f, 1f)));
            view.ContinueButton.style.marginLeft = new StyleLength(new Length(24, LengthUnit.Pixel));
            view.CloseButton.style.display = DisplayStyle.None;

            buttonRow.Add(view.HostButton);
            buttonRow.Add(view.ContinueButton);

            panel.Add(headerChip);
            panel.Add(view.TitleLabel);
            panel.Add(view.WinnerLabel);
            panel.Add(teamsRow);
            panel.Add(buttonRow);

            rootView.Root.Add(panel);
            view.Panel = panel;
            rootView.PostMatch = view;
        }

        public static void Show(DraftUIRenderer.View rootView)
        {
            if (rootView?.PostMatch?.Panel == null)
            {
                return;
            }

            var wasAlreadyVisible = rootView.PostMatch.Panel.style.display != DisplayStyle.None
                && rootView.PostMatch.Panel.resolvedStyle.display != DisplayStyle.None;

            rootView.Container.style.display = DisplayStyle.Flex;
            if (rootView.Welcome?.Panel != null) rootView.Welcome.Panel.style.display = DisplayStyle.None;
            if (rootView.VotingPanel != null) rootView.VotingPanel.style.display = DisplayStyle.None;
            if (rootView.ApprovalPanel != null) rootView.ApprovalPanel.style.display = DisplayStyle.None;
            if (rootView.DraftPanel != null) rootView.DraftPanel.style.display = DisplayStyle.None;
            rootView.PostMatch.Panel.style.display = DisplayStyle.Flex;

            if (wasAlreadyVisible)
            {
                rootView.PostMatch.Panel.style.opacity = 1f;
                rootView.PostMatch.Panel.transform.scale = Vector3.one;
                rootView.PostMatch.Panel.transform.position = Vector3.zero;
                return;
            }

            StartPanelEntranceAnimation(rootView.PostMatch.Panel);
        }

        public static void Render(DraftUIRenderer.View rootView, MatchResultMessage state)
        {
            if (rootView?.PostMatch == null || state == null)
            {
                return;
            }

            var view = rootView.PostMatch;
            view.TitleLabel.text = "FINAL RESULTS";
            view.WinnerLabel.text = BuildWinningHeadline(state);
            view.WinnerLabel.style.display = string.IsNullOrWhiteSpace(view.WinnerLabel.text) ? DisplayStyle.None : DisplayStyle.Flex;
            view.WinnerLabel.style.color = new StyleColor(state.WinningTeam == TeamResult.Red
                ? new Color(1f, 0.82f, 0.82f, 1f)
                : state.WinningTeam == TeamResult.Blue
                    ? new Color(0.80f, 0.90f, 1f, 1f)
                    : new Color(0.92f, 0.95f, 0.99f, 0.92f));

            PopulateTeam(view.RedTeam, TeamResult.Red, state);
            PopulateTeam(view.BlueTeam, TeamResult.Blue, state);
        }

        private static TeamSectionView BuildTeamPanel(VisualElement parent, TeamResult team)
        {
            var teamView = new TeamSectionView();
            var teamPanel = new VisualElement();
            teamPanel.style.display = DisplayStyle.Flex;
            teamPanel.style.flexDirection = FlexDirection.Column;
            teamPanel.style.flexGrow = 1;
            teamPanel.style.flexBasis = new StyleLength(new Length(0, LengthUnit.Pixel));
            teamPanel.style.minWidth = 318;
            teamPanel.style.maxWidth = 474;
            teamPanel.style.marginLeft = new StyleLength(new Length(8, LengthUnit.Pixel));
            teamPanel.style.marginRight = new StyleLength(new Length(8, LengthUnit.Pixel));
            teamPanel.style.marginBottom = new StyleLength(new Length(14, LengthUnit.Pixel));
            teamPanel.style.paddingTop = new StyleLength(new Length(18, LengthUnit.Pixel));
            teamPanel.style.paddingBottom = new StyleLength(new Length(18, LengthUnit.Pixel));
            teamPanel.style.paddingLeft = new StyleLength(new Length(18, LengthUnit.Pixel));
            teamPanel.style.paddingRight = new StyleLength(new Length(18, LengthUnit.Pixel));
            teamPanel.style.backgroundColor = new StyleColor(TeamSurfaceColor(team));
            teamPanel.style.borderTopLeftRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            teamPanel.style.borderTopRightRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            teamPanel.style.borderBottomLeftRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            teamPanel.style.borderBottomRightRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            teamPanel.style.borderTopWidth = 1;
            teamPanel.style.borderRightWidth = 1;
            teamPanel.style.borderBottomWidth = 1;
            teamPanel.style.borderLeftWidth = 1;
            ApplyUniformBorder(teamPanel, new Color(1f, 1f, 1f, 0.05f));
            teamView.Panel = teamPanel;

            var accentRail = new VisualElement();
            accentRail.style.height = 3;
            accentRail.style.marginBottom = new StyleLength(new Length(14, LengthUnit.Pixel));
            accentRail.style.backgroundColor = new StyleColor(Tint(TeamAccent(team), 0.96f, -0.04f));
            accentRail.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            accentRail.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            accentRail.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            accentRail.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));

            var titleRow = new VisualElement();
            titleRow.style.display = DisplayStyle.Flex;
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = new StyleLength(new Length(14, LengthUnit.Pixel));

            var titleColumn = new VisualElement();
            titleColumn.style.display = DisplayStyle.Flex;
            titleColumn.style.flexDirection = FlexDirection.Column;

            var teamLabel = CreateLabel(FormatTeamName(team), 18, FontStyle.Bold, TextAnchor.MiddleLeft, ReadableTeamColor(team));
            var teamMeta = CreateLabel("Featured player and roster", 10, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.75f, 0.82f, 0.89f, 0.56f));
            teamMeta.style.marginTop = new StyleLength(new Length(3, LengthUnit.Pixel));
            titleColumn.Add(teamLabel);
            titleColumn.Add(teamMeta);

            var headerMeta = new VisualElement();
            headerMeta.style.display = DisplayStyle.Flex;
            headerMeta.style.flexDirection = FlexDirection.Column;
            headerMeta.style.alignItems = Align.FlexEnd;

            var statusLabel = CreatePillLabel("TEAM", 10, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.94f, 0.97f, 1f, 1f));
            ApplyPillVisual(statusLabel, new Color(0.16f, 0.20f, 0.27f, 0.92f), new Color(0.92f, 0.95f, 1f, 1f));
            var summaryLabel = CreateLabel("0 PLAYERS", 10, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.82f, 0.88f, 0.95f, 0.84f));
            summaryLabel.style.marginTop = new StyleLength(new Length(5, LengthUnit.Pixel));
            headerMeta.Add(statusLabel);
            headerMeta.Add(summaryLabel);

            titleRow.Add(titleColumn);
            titleRow.Add(headerMeta);

            teamView.StatusLabel = statusLabel;
            teamView.SummaryLabel = summaryLabel;

            var mvpContainer = new VisualElement();
            mvpContainer.style.display = DisplayStyle.Flex;
            mvpContainer.style.flexDirection = FlexDirection.Column;
            mvpContainer.style.minHeight = 136;
            mvpContainer.style.marginBottom = new StyleLength(new Length(16, LengthUnit.Pixel));
            teamView.MvpContainer = mvpContainer;

            var rosterTitle = CreateLabel("FULL ROSTER", 11, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.88f, 0.92f, 0.97f, 0.82f));
            rosterTitle.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            teamView.RosterLabel = rosterTitle;

            var playersContainer = new VisualElement();
            playersContainer.style.display = DisplayStyle.Flex;
            playersContainer.style.flexDirection = FlexDirection.Column;
            teamView.PlayersContainer = playersContainer;

            teamPanel.Add(accentRail);
            teamPanel.Add(titleRow);
            teamPanel.Add(mvpContainer);
            teamPanel.Add(rosterTitle);
            teamPanel.Add(playersContainer);

            parent.Add(teamPanel);
            return teamView;
        }

        private static void PopulateTeam(TeamSectionView teamView, TeamResult team, MatchResultMessage state)
        {
            if (teamView == null)
            {
                return;
            }

            var mvpContainer = teamView.MvpContainer;
            var playersContainer = teamView.PlayersContainer;
            mvpContainer.Clear();
            playersContainer.Clear();

            var teamPlayers = SortPlayers(state, team).ToArray();
            var additionalPlayerCount = Mathf.Max(0, teamPlayers.Length - 1);
            teamView.SummaryLabel.text = teamPlayers.Length == 1 ? "1 PLAYER" : $"{teamPlayers.Length} PLAYERS";
            teamView.RosterLabel.text = additionalPlayerCount > 0 ? $"FULL ROSTER · {additionalPlayerCount} MORE" : "FULL ROSTER";

            if (teamPlayers.Length > 0)
            {
                var isWinner = state.WinningTeam != TeamResult.Unknown && team == state.WinningTeam;
                ApplyPillVisual(teamView.StatusLabel,
                    isWinner ? new Color(0.74f, 0.56f, 0.23f, 0.92f) : new Color(0.17f, 0.21f, 0.28f, 0.92f),
                    isWinner ? new Color(0.17f, 0.13f, 0.05f, 1f) : new Color(0.94f, 0.97f, 1f, 1f));
                teamView.StatusLabel.text = isWinner ? "WINNER" : "LIVE DATA";
            }
            else
            {
                ApplyPillVisual(teamView.StatusLabel, new Color(0.17f, 0.21f, 0.28f, 0.92f), new Color(0.84f, 0.89f, 0.96f, 0.94f));
                teamView.StatusLabel.text = "NO DATA";
            }

            if (teamPlayers.Length == 0)
            {
                mvpContainer.Add(CreateEmptyStateCard("No player data", "This team did not report tracked player results.", team));
                playersContainer.Add(CreateInlineEmptyState("No additional players"));
                return;
            }

            var mvp = teamPlayers.FirstOrDefault(player => player.IsMVP) ?? teamPlayers[0];
            mvpContainer.Add(CreateMvpCard(mvp, team, team == state.WinningTeam));

            var remainingPlayers = teamPlayers.Where(player => !ReferenceEquals(player, mvp)).ToArray();
            if (remainingPlayers.Length == 0)
            {
                playersContainer.Add(CreateInlineEmptyState("No additional players"));
                return;
            }

            foreach (var player in remainingPlayers)
            {
                playersContainer.Add(CreatePlayerRow(player, team));
            }
        }

        private static MatchResultPlayerMessage[] SortPlayers(MatchResultMessage state, TeamResult team)
        {
            return (state?.Players ?? Array.Empty<MatchResultPlayerMessage>())
                .Where(player => player != null && player.Team == team)
                .OrderByDescending(player => player.IsMVP)
                .ThenByDescending(player => ComputePerformanceScore(player, state.WinningTeam))
                .ThenByDescending(player => player.MmrBefore)
                .ThenBy(player => player.Username ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(player => player.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static int ComputePerformanceScore(MatchResultPlayerMessage player, TeamResult winningTeam)
        {
            if (player == null)
            {
                return 0;
            }

            return (player.Goals * 5)
                + (player.Assists * 3)
                + (player.Team == winningTeam ? 2 : 0);
        }

        private static VisualElement CreateMvpCard(MatchResultPlayerMessage player, TeamResult team, bool winningTeam)
        {
            var card = new VisualElement();
            card.style.display = DisplayStyle.Flex;
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.FlexStart;
            card.style.paddingTop = new StyleLength(new Length(18, LengthUnit.Pixel));
            card.style.paddingBottom = new StyleLength(new Length(18, LengthUnit.Pixel));
            card.style.paddingLeft = new StyleLength(new Length(18, LengthUnit.Pixel));
            card.style.paddingRight = new StyleLength(new Length(18, LengthUnit.Pixel));
            card.style.backgroundColor = new StyleColor(Tint(TeamSurfaceColor(team), winningTeam ? 1.10f : 1.04f, 0.08f));
            card.style.borderTopLeftRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            card.style.borderTopRightRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            card.style.borderBottomLeftRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            card.style.borderBottomRightRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            card.style.borderTopWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            ApplyUniformBorder(card, new Color(1f, 1f, 1f, 0.05f));

            var avatar = CreateAvatarFrame(player, 64, team, emphasize: true);
            avatar.style.marginRight = new StyleLength(new Length(16, LengthUnit.Pixel));

            var content = new VisualElement();
            content.style.display = DisplayStyle.Flex;
            content.style.flexDirection = FlexDirection.Column;
            content.style.flexGrow = 1;

            var topRow = new VisualElement();
            topRow.style.display = DisplayStyle.Flex;
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.justifyContent = Justify.SpaceBetween;
            topRow.style.alignItems = Align.Center;

            var badgeText = player != null && player.IsMVP ? "MVP" : "FEATURED PLAYER";
            var badge = CreatePillLabel(badgeText, 10, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.20f, 0.15f, 0.05f, 1f));
            ApplyPillVisual(badge, new Color(0.87f, 0.74f, 0.34f, 0.92f), new Color(0.20f, 0.15f, 0.05f, 1f));

            var delta = CreatePillLabel(FormatRatingDelta(player), 13, FontStyle.Bold, TextAnchor.MiddleCenter, RatingDeltaColor(player));
            ApplyPillVisual(delta, Tint(RatingDeltaColor(player), 0.28f, 0.08f), RatingDeltaColor(player));

            topRow.Add(badge);
            topRow.Add(delta);

            var nameRow = new VisualElement();
            nameRow.style.display = DisplayStyle.Flex;
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;
            nameRow.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));

            var nameLabel = CreateLabel(FormatVisiblePlayerName(player), 22, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            nameRow.Add(nameLabel);
            if (player != null && player.IsSharedGoalie)
            {
                nameRow.Add(CreateSharedGoalieBadge(11));
            }

            var statsRail = new VisualElement();
            statsRail.style.display = DisplayStyle.Flex;
            statsRail.style.flexDirection = FlexDirection.Row;
            statsRail.style.flexWrap = Wrap.Wrap;
            statsRail.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            statsRail.Add(CreateMetricBlock("GOALS", player?.Goals ?? 0, new Color(0.45f, 0.89f, 0.62f, 1f)));
            statsRail.Add(CreateMetricBlock("ASSISTS", player?.Assists ?? 0, new Color(0.56f, 0.84f, 1f, 1f)));

            var mmrRow = new VisualElement();
            mmrRow.style.display = DisplayStyle.Flex;
            mmrRow.style.flexDirection = FlexDirection.Row;
            mmrRow.style.alignItems = Align.Center;
            mmrRow.style.marginTop = new StyleLength(new Length(12, LengthUnit.Pixel));

            var mmrLabel = CreateLabel(FormatDetailedRatingSummary(player), 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.94f, 0.90f, 0.74f, 1f));
            mmrRow.Add(mmrLabel);

            content.Add(topRow);
            content.Add(nameRow);
            content.Add(statsRail);
            content.Add(mmrRow);

            card.Add(avatar);
            card.Add(content);

            return card;
        }

        private static VisualElement CreatePlayerRow(MatchResultPlayerMessage player, TeamResult team)
        {
            var row = new VisualElement();
            row.style.display = DisplayStyle.Flex;
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = new StyleLength(new Length(12, LengthUnit.Pixel));
            row.style.paddingBottom = new StyleLength(new Length(12, LengthUnit.Pixel));
            row.style.paddingLeft = new StyleLength(new Length(14, LengthUnit.Pixel));
            row.style.paddingRight = new StyleLength(new Length(14, LengthUnit.Pixel));
            row.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            row.style.backgroundColor = new StyleColor(Tint(TeamSurfaceColor(team), 0.98f, 0.04f));
            row.style.borderTopLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            row.style.borderTopRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            row.style.borderBottomLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            row.style.borderBottomRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            row.style.borderTopWidth = 1;
            row.style.borderRightWidth = 1;
            row.style.borderBottomWidth = 1;
            row.style.borderLeftWidth = 1;
            ApplyUniformBorder(row, new Color(1f, 1f, 1f, 0.04f));

            var left = new VisualElement();
            left.style.display = DisplayStyle.Flex;
            left.style.flexDirection = FlexDirection.Row;
            left.style.alignItems = Align.Center;
            left.style.flexGrow = 1;

            var avatar = CreateAvatarFrame(player, 30, team, emphasize: false);
            avatar.style.marginRight = new StyleLength(new Length(12, LengthUnit.Pixel));
            left.Add(avatar);

            var textColumn = new VisualElement();
            textColumn.style.display = DisplayStyle.Flex;
            textColumn.style.flexDirection = FlexDirection.Column;
            textColumn.style.flexGrow = 1;

            var nameLabel = CreateLabel(FormatVisiblePlayerName(player), 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.96f, 0.97f, 1f, 0.98f));
            var nameRow = new VisualElement();
            nameRow.style.display = DisplayStyle.Flex;
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;

            nameRow.Add(nameLabel);
            if (player != null && player.IsSharedGoalie)
            {
                nameRow.Add(CreateSharedGoalieBadge(10));
            }

            var statsLabel = CreateLabel(FormatInlineStats(player), 11, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.76f, 0.84f, 0.92f, 0.90f));
            statsLabel.style.marginTop = new StyleLength(new Length(5, LengthUnit.Pixel));

            textColumn.Add(nameRow);
            textColumn.Add(statsLabel);
            left.Add(textColumn);

            var right = new VisualElement();
            right.style.display = DisplayStyle.Flex;
            right.style.flexDirection = FlexDirection.Column;
            right.style.alignItems = Align.FlexEnd;
            right.style.marginLeft = new StyleLength(new Length(16, LengthUnit.Pixel));

            var deltaLabel = CreatePillLabel(FormatRatingDelta(player), 10, FontStyle.Bold, TextAnchor.MiddleCenter, RatingDeltaColor(player));
            ApplyPillVisual(deltaLabel, Tint(RatingDeltaColor(player), 0.24f, 0.06f), RatingDeltaColor(player));

            var mmrLabel = CreateLabel(FormatCompactRatingSummary(player), 11, FontStyle.Normal, TextAnchor.MiddleRight, new Color(0.95f, 0.92f, 0.75f, 1f));
            mmrLabel.style.marginTop = new StyleLength(new Length(5, LengthUnit.Pixel));

            right.Add(deltaLabel);
            right.Add(mmrLabel);

            row.Add(left);
            row.Add(right);
            return row;
        }

        private static VisualElement CreateEmptyStateCard(string title, string subtitle, TeamResult team)
        {
            var card = new VisualElement();
            card.style.display = DisplayStyle.Flex;
            card.style.flexDirection = FlexDirection.Column;
            card.style.alignItems = Align.Center;
            card.style.justifyContent = Justify.Center;
            card.style.minHeight = 104;
            card.style.paddingTop = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.paddingBottom = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.paddingLeft = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.paddingRight = new StyleLength(new Length(14, LengthUnit.Pixel));
            card.style.backgroundColor = new StyleColor(Tint(TeamSurfaceColor(team), 0.88f, 0.01f));
            card.style.borderTopLeftRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            card.style.borderTopRightRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            card.style.borderBottomLeftRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            card.style.borderBottomRightRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            card.style.borderTopWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            ApplyUniformBorder(card, new Color(1f, 1f, 1f, 0.035f));

            var marker = CreateLabel("•", 14, FontStyle.Bold, TextAnchor.MiddleCenter, Tint(TeamAccent(team), 0.95f, -0.05f));
            marker.style.opacity = 0.7f;
            var titleLabel = CreateLabel(title, 12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.86f, 0.91f, 0.97f, 0.88f));
            titleLabel.style.marginTop = new StyleLength(new Length(1, LengthUnit.Pixel));
            var subtitleLabel = CreateLabel(subtitle, 10, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.72f, 0.79f, 0.87f, 0.68f));
            subtitleLabel.style.marginTop = new StyleLength(new Length(3, LengthUnit.Pixel));

            card.Add(marker);
            card.Add(titleLabel);
            card.Add(subtitleLabel);
            return card;
        }

        private static VisualElement CreateInlineEmptyState(string text)
        {
            var wrapper = new VisualElement();
            wrapper.style.display = DisplayStyle.Flex;
            wrapper.style.flexDirection = FlexDirection.Row;
            wrapper.style.justifyContent = Justify.Center;
            wrapper.style.alignItems = Align.Center;
            wrapper.style.paddingTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            wrapper.style.paddingBottom = new StyleLength(new Length(10, LengthUnit.Pixel));

            var label = CreatePillLabel(text, 11, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.77f, 0.83f, 0.91f, 0.90f));
            ApplyPillVisual(label, new Color(0.14f, 0.18f, 0.24f, 0.78f), new Color(0.77f, 0.83f, 0.91f, 0.90f));
            wrapper.Add(label);
            return wrapper;
        }

        private static string FormatWinningTeam(TeamResult team)
        {
            switch (team)
            {
                case TeamResult.Red:
                    return "RED TEAM VICTORY";
                case TeamResult.Blue:
                    return "BLUE TEAM VICTORY";
                default:
                    return "Results Ready";
            }
        }

        private static string BuildWinningHeadline(MatchResultMessage state)
        {
            if (state == null)
            {
                return string.Empty;
            }

            var winner = FormatWinningTeam(state.WinningTeam);
            if (string.IsNullOrWhiteSpace(winner))
            {
                return "RESULTS READY";
            }

            var redGoals = CountTeamGoals(state, TeamResult.Red);
            var blueGoals = CountTeamGoals(state, TeamResult.Blue);
            return redGoals > 0 || blueGoals > 0
                ? $"{winner}  ·  {redGoals} - {blueGoals}"
                : winner;
        }

        private static int CountTeamGoals(MatchResultMessage state, TeamResult team)
        {
            return (state?.Players ?? Array.Empty<MatchResultPlayerMessage>())
                .Where(player => player != null && player.Team == team)
                .Sum(player => Mathf.Max(0, player.Goals));
        }

        private static string FormatTeamName(TeamResult team)
        {
            switch (team)
            {
                case TeamResult.Red:
                    return "RED TEAM";
                case TeamResult.Blue:
                    return "BLUE TEAM";
                default:
                    return "UNASSIGNED";
            }
        }

        private static string FormatInlineStats(MatchResultPlayerMessage player)
        {
            return $"Goals {player?.Goals ?? 0}   ·   Assists {player?.Assists ?? 0}";
        }

        private static VisualElement CreateMetricBlock(string label, int value, Color accent)
        {
            var block = new VisualElement();
            block.style.display = DisplayStyle.Flex;
            block.style.flexDirection = FlexDirection.Column;
            block.style.marginRight = new StyleLength(new Length(10, LengthUnit.Pixel));
            block.style.paddingTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            block.style.paddingBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            block.style.paddingLeft = new StyleLength(new Length(10, LengthUnit.Pixel));
            block.style.paddingRight = new StyleLength(new Length(10, LengthUnit.Pixel));
            block.style.minWidth = 84;
            block.style.backgroundColor = new StyleColor(Tint(accent, 0.16f, 0.02f));
            block.style.borderTopLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            block.style.borderTopRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            block.style.borderBottomLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            block.style.borderBottomRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            block.style.borderTopWidth = 1;
            block.style.borderRightWidth = 1;
            block.style.borderBottomWidth = 1;
            block.style.borderLeftWidth = 1;
            ApplyUniformBorder(block, Tint(accent, 0.34f, 0.02f));

            var labelElement = CreateLabel(label, 10, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.76f, 0.83f, 0.91f, 0.82f));
            var valueElement = CreateLabel(value.ToString(), 18, FontStyle.Bold, TextAnchor.MiddleLeft, accent);
            valueElement.style.marginTop = new StyleLength(new Length(2, LengthUnit.Pixel));

            block.Add(labelElement);
            block.Add(valueElement);
            return block;
        }

        private static string FormatVisiblePlayerName(MatchResultPlayerMessage player)
        {
            var clean = NormalizePresentedName(player?.Username);
            return string.IsNullOrWhiteSpace(clean) ? "---" : clean;
        }

        private static string FormatRatingDelta(MatchResultPlayerMessage player)
        {
            if (player != null && player.ExcludedFromMmr)
            {
                return "SG";
            }

            return FormatMmrDelta(player?.MmrDelta ?? 0);
        }

        private static string FormatDetailedRatingSummary(MatchResultPlayerMessage player)
        {
            if (player != null && player.ExcludedFromMmr)
            {
                return "Shared Goalie • Unrated";
            }

            return $"MMR {player?.MmrBefore ?? 0} → {player?.MmrAfter ?? 0}";
        }

        private static string FormatCompactRatingSummary(MatchResultPlayerMessage player)
        {
            if (player != null && player.ExcludedFromMmr)
            {
                return "Unrated";
            }

            return $"{player?.MmrAfter ?? 0} MMR";
        }

        private static string FormatMmrDelta(int delta)
        {
            return delta > 0 ? $"+{delta}" : delta.ToString();
        }

        private static Color RatingDeltaColor(MatchResultPlayerMessage player)
        {
            if (player != null && player.ExcludedFromMmr)
            {
                return new Color(0.98f, 0.84f, 0.44f, 1f);
            }

            return DeltaColor(player?.MmrDelta ?? 0);
        }

        private static Color DeltaColor(int delta)
        {
            if (delta > 0)
            {
                return new Color(0.59f, 0.93f, 0.70f, 1f);
            }

            if (delta < 0)
            {
                return new Color(1f, 0.74f, 0.74f, 1f);
            }

            return new Color(0.85f, 0.89f, 0.95f, 1f);
        }

        private static VisualElement CreateSharedGoalieBadge(int fontSize)
        {
            var badge = CreatePillLabel("SG", fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.22f, 0.16f, 0.05f, 1f));
            ApplyPillVisual(badge, new Color(0.98f, 0.84f, 0.44f, 0.98f), new Color(0.22f, 0.16f, 0.05f, 1f));
            badge.style.marginLeft = new StyleLength(new Length(8, LengthUnit.Pixel));
            return badge;
        }

        private static string NormalizePresentedName(string username)
        {
            var clean = StripRichTextTags(username ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(clean))
            {
                return "---";
            }

            var bulletIndex = clean.LastIndexOf('•');
            if (bulletIndex > 0)
            {
                var suffix = clean.Substring(bulletIndex + 1).Trim();
                if (suffix.EndsWith("MMR", StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean.Substring(0, bulletIndex).TrimEnd();
                }
            }

            var numberIndex = FindPlayerNumberToken(clean);
            if (numberIndex >= 0)
            {
                clean = clean.Substring(numberIndex).Trim();
            }

            clean = clean.Replace("★", string.Empty).Replace("☆", string.Empty).Trim();
            clean = TrimLeadingIdentityDecorators(clean);
            return CollapseWhitespace(clean);
        }

        private static int FindPlayerNumberToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return -1;
            }

            for (var index = 0; index < value.Length - 1; index++)
            {
                if (value[index] == '#' && char.IsDigit(value[index + 1]))
                {
                    return index;
                }
            }

            return -1;
        }

        private static string TrimLeadingIdentityDecorators(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var tokens = CollapseWhitespace(value)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            var removableTokens = new[] { "bronze", "silver", "gold", "platinum", "diamond", "elite", "champion", "owner", "mvp" };

            while (tokens.Count > 1)
            {
                var token = tokens[0].Trim('[', ']', '(', ')', '-', '•', '|');
                if (!removableTokens.Contains(token, StringComparer.OrdinalIgnoreCase))
                {
                    break;
                }

                tokens.RemoveAt(0);
            }

            return string.Join(" ", tokens).Trim();
        }

        private static Color TeamPanelColor(TeamResult team)
        {
            switch (team)
            {
                case TeamResult.Red:
                    return new Color(0.24f, 0.11f, 0.13f, 0.90f);
                case TeamResult.Blue:
                    return new Color(0.10f, 0.17f, 0.28f, 0.90f);
                default:
                    return new Color(0.14f, 0.14f, 0.18f, 0.90f);
            }
        }

        private static Color TeamSurfaceColor(TeamResult team)
        {
            switch (team)
            {
                case TeamResult.Red:
                    return new Color(0.17f, 0.10f, 0.12f, 0.72f);
                case TeamResult.Blue:
                    return new Color(0.09f, 0.14f, 0.23f, 0.72f);
                default:
                    return new Color(0.11f, 0.13f, 0.18f, 0.72f);
            }
        }

        private static Color TeamAccent(TeamResult team)
        {
            switch (team)
            {
                case TeamResult.Red:
                    return new Color(0.90f, 0.34f, 0.38f, 1f);
                case TeamResult.Blue:
                    return new Color(0.34f, 0.58f, 0.95f, 1f);
                default:
                    return new Color(0.75f, 0.75f, 0.75f, 1f);
            }
        }

        private static Color ReadableTeamColor(TeamResult team)
        {
            switch (team)
            {
                case TeamResult.Red:
                    return new Color(1f, 0.83f, 0.83f, 1f);
                case TeamResult.Blue:
                    return new Color(0.82f, 0.90f, 1f, 1f);
                default:
                    return new Color(0.96f, 0.96f, 0.96f, 1f);
            }
        }

            private static VisualElement CreateAvatarFrame(MatchResultPlayerMessage player, int size, TeamResult team, bool emphasize)
            {
                var frame = new VisualElement();
                frame.style.width = size;
                frame.style.height = size;
                frame.style.flexShrink = 0;
                frame.style.alignItems = Align.Center;
                frame.style.justifyContent = Justify.Center;
                frame.style.backgroundColor = new StyleColor(emphasize
                    ? new Color(0.80f, 0.66f, 0.24f, 0.92f)
                    : Tint(TeamAccent(team), 0.56f, 0.10f));
                frame.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
                frame.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
                frame.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
                frame.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
                frame.style.borderTopWidth = 1;
                frame.style.borderRightWidth = 1;
                frame.style.borderBottomWidth = 1;
                frame.style.borderLeftWidth = 1;
                ApplyUniformBorder(frame, new Color(1f, 1f, 1f, emphasize ? 0.12f : 0.08f));
                frame.style.overflow = Overflow.Hidden;

                BindAvatarFrameContent(frame, player?.SteamId, player?.Id, player?.Username, size >= 52 ? 20 : 12);
                return frame;
            }

            private static void BindAvatarFrameContent(VisualElement frame, string steamId, string playerId, string username, int fontSize)
            {
                var avatarSteamId = ResolveAvatarSteamId(steamId) ?? ResolveAvatarSteamId(playerId);
                Debug.Log($"[AVATAR] UI bind request. context=postmatch requested={avatarSteamId ?? "none"} steamField={steamId ?? "none"} playerId={playerId ?? "none"}");
                if (TryApplyAvatarFrameContent(frame, avatarSteamId, username, fontSize))
                {
                    Debug.Log($"[AVATAR] UI bind success. context=postmatch steamId={avatarSteamId ?? "none"} attempts=0");
                    return;
                }

                if (string.IsNullOrEmpty(avatarSteamId))
                {
                    Debug.LogWarning($"[AVATAR] UI bind failed. context=postmatch reason=missing-steam-id username={username ?? "?"}");
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
                    if (TryApplyAvatarFrameContent(frame, avatarSteamId, username, fontSize) || attempts >= AvatarRefreshMaxAttempts)
                    {
                        if (frame.panel != null)
                        {
                            Debug.Log($"[AVATAR] UI bind {(attempts >= AvatarRefreshMaxAttempts ? "stopped" : "success")}. context=postmatch steamId={avatarSteamId} attempts={attempts}");
                        }
                        refreshItem.Pause();
                    }
                }).Every(AvatarRefreshIntervalMilliseconds);
            }

            private static bool TryApplyAvatarFrameContent(VisualElement frame, string avatarSteamId, string username, int fontSize)
            {
                frame.Clear();

                if (!string.IsNullOrEmpty(avatarSteamId)
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

                frame.Add(CreateLabel(ResolveAvatarInitial(username), fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.98f, 0.99f, 1f, 1f)));
                return false;
            }

            private static string ResolveAvatarSteamId(string candidate)
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

            private static string ResolveAvatarInitial(string username)
            {
                var name = NormalizePresentedName(username);
                return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
            }

            private static VisualElement CreateStatChip(string label, int value, Color accent)
            {
                var chip = new Label($"{label} {value}");
                chip.style.marginRight = new StyleLength(new Length(6, LengthUnit.Pixel));
                chip.style.marginBottom = new StyleLength(new Length(6, LengthUnit.Pixel));
                chip.style.paddingTop = new StyleLength(new Length(4, LengthUnit.Pixel));
                chip.style.paddingBottom = new StyleLength(new Length(4, LengthUnit.Pixel));
                chip.style.paddingLeft = new StyleLength(new Length(8, LengthUnit.Pixel));
                chip.style.paddingRight = new StyleLength(new Length(8, LengthUnit.Pixel));
                chip.style.backgroundColor = new StyleColor(Tint(accent, 0.34f, 0.06f));
                chip.style.color = new StyleColor(accent);
                chip.style.fontSize = 11;
                chip.style.unityFontStyleAndWeight = FontStyle.Bold;
                chip.style.unityTextAlign = TextAnchor.MiddleCenter;
                chip.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
                chip.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
                chip.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
                chip.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
                chip.style.borderTopWidth = 1;
                chip.style.borderRightWidth = 1;
                chip.style.borderBottomWidth = 1;
                chip.style.borderLeftWidth = 1;
                ApplyUniformBorder(chip, Tint(accent, 0.44f, 0.02f));
                return chip;
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

        private static Label CreatePillLabel(string text, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
        {
            var label = CreateLabel(text, fontSize, fontStyle, alignment, color);
            label.style.paddingTop = new StyleLength(new Length(4, LengthUnit.Pixel));
            label.style.paddingBottom = new StyleLength(new Length(4, LengthUnit.Pixel));
            label.style.paddingLeft = new StyleLength(new Length(8, LengthUnit.Pixel));
            label.style.paddingRight = new StyleLength(new Length(8, LengthUnit.Pixel));
            label.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            label.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            label.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            label.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            label.style.borderTopWidth = 1;
            label.style.borderRightWidth = 1;
            label.style.borderBottomWidth = 1;
            label.style.borderLeftWidth = 1;
            return label;
        }

        private static void ApplyPillVisual(Label label, Color background, Color textColor)
        {
            if (label == null)
            {
                return;
            }

            label.style.backgroundColor = new StyleColor(background);
            label.style.color = new StyleColor(textColor);
            ApplyUniformBorder(label, Tint(background, 1.02f, 0.02f));
        }

        private static void StyleButton(Button button, ButtonPalette palette)
        {
            button.style.minWidth = 168;
            button.style.height = 40;
            button.style.paddingLeft = new StyleLength(new Length(16, LengthUnit.Pixel));
            button.style.paddingRight = new StyleLength(new Length(16, LengthUnit.Pixel));
            button.style.paddingTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            button.style.paddingBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            button.style.backgroundColor = new StyleColor(palette.Normal);
            button.style.color = new StyleColor(palette.Text);
            button.style.fontSize = 13;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            button.style.borderTopRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            button.style.borderBottomLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            button.style.borderBottomRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            button.style.borderTopWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            ApplyButtonVisual(button, palette, palette.Normal, 1f);

            button.RegisterCallback<PointerEnterEvent>(_ => ApplyButtonVisual(button, palette, palette.Hover, 1.025f));
            button.RegisterCallback<PointerLeaveEvent>(_ => ApplyButtonVisual(button, palette, palette.Normal, 1f));
            button.RegisterCallback<PointerDownEvent>(_ => ApplyButtonVisual(button, palette, palette.Pressed, 0.985f));
            button.RegisterCallback<PointerUpEvent>(_ => ApplyButtonVisual(button, palette, palette.Hover, 1.025f));
        }

        private static void ApplyButtonVisual(Button button, ButtonPalette palette, Color color, float scale)
        {
            if (button == null)
            {
                return;
            }

            button.style.backgroundColor = new StyleColor(color);
            button.style.color = new StyleColor(palette.Text);
            ApplyUniformBorder(button, Tint(color, 1.02f, 0.02f));
            button.transform.scale = new Vector3(scale, scale, 1f);
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

        private static string StripRichTextTags(string value)
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

        private static void StartPanelEntranceAnimation(VisualElement panel)
        {
            if (panel == null)
            {
                return;
            }

            panel.style.opacity = 0f;
            panel.transform.scale = new Vector3(0.985f, 0.985f, 1f);
            panel.transform.position = new Vector3(0f, 12f, 0f);

            var startedAt = Time.unscaledTime;
            IVisualElementScheduledItem animationItem = null;
            animationItem = panel.schedule.Execute(() =>
            {
                if (panel.panel == null)
                {
                    animationItem.Pause();
                    return;
                }

                var t = Mathf.Clamp01((Time.unscaledTime - startedAt) / PanelEntranceDurationSeconds);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                panel.style.opacity = eased;
                panel.transform.scale = Vector3.LerpUnclamped(new Vector3(0.985f, 0.985f, 1f), Vector3.one, eased);
                panel.transform.position = Vector3.LerpUnclamped(new Vector3(0f, 12f, 0f), Vector3.zero, eased);

                if (t >= 1f)
                {
                    animationItem.Pause();
                }
            }).Every(16);
        }

        private static void StartPulseAnimation(VisualElement element, float baseOpacity, float opacityAmplitude, float scaleAmplitude, float speed)
        {
            if (element == null)
            {
                return;
            }

            var startedAt = Time.unscaledTime;
            element.schedule.Execute(() =>
            {
                if (element.panel == null)
                {
                    return;
                }

                var wave = 0.5f + (Mathf.Sin((Time.unscaledTime - startedAt) * speed * Mathf.PI * 2f) * 0.5f);
                element.style.opacity = baseOpacity + (wave * opacityAmplitude);
                var scale = 1f + (wave * scaleAmplitude);
                element.transform.scale = new Vector3(scale, scale, 1f);
            }).Every(32);
        }

        private static Color Tint(Color color, float multiplier, float alphaOffset)
        {
            return new Color(
                Mathf.Clamp01(color.r * multiplier),
                Mathf.Clamp01(color.g * multiplier),
                Mathf.Clamp01(color.b * multiplier),
                Mathf.Clamp01(color.a + alphaOffset));
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