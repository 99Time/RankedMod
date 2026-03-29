using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace schrader
{
    internal static class PostMatchUIRenderer
    {
        public sealed class View
        {
            public VisualElement Panel;
            public Label TitleLabel;
            public Label WinnerLabel;
            public VisualElement RedMvpContainer;
            public VisualElement RedPlayersContainer;
            public VisualElement BlueMvpContainer;
            public VisualElement BluePlayersContainer;
            public Button ContinueButton;
            public Button CloseButton;
        }

        public static void BuildUI(DraftUIRenderer.View rootView, Action onContinue, Action onClose)
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
            panel.style.maxWidth = 1120;
            panel.style.alignSelf = Align.Center;
            panel.style.paddingTop = new StyleLength(new Length(12, LengthUnit.Pixel));
            panel.style.paddingBottom = new StyleLength(new Length(6, LengthUnit.Pixel));

            var headerChip = new VisualElement();
            headerChip.style.alignSelf = Align.Center;
            headerChip.style.paddingTop = new StyleLength(new Length(6, LengthUnit.Pixel));
            headerChip.style.paddingBottom = new StyleLength(new Length(6, LengthUnit.Pixel));
            headerChip.style.paddingLeft = new StyleLength(new Length(16, LengthUnit.Pixel));
            headerChip.style.paddingRight = new StyleLength(new Length(16, LengthUnit.Pixel));
            headerChip.style.marginBottom = new StyleLength(new Length(16, LengthUnit.Pixel));
            headerChip.style.backgroundColor = new StyleColor(new Color(0.19f, 0.25f, 0.33f, 0.56f));
            headerChip.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            headerChip.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            headerChip.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            headerChip.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            headerChip.Add(CreateLabel("RANKED RESULTS", 13, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.86f, 0.92f, 0.98f, 1f)));

            view.TitleLabel = CreateLabel("MATCH COMPLETE", 32, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.98f, 0.99f, 1f, 1f));
            view.WinnerLabel = CreateLabel("RED TEAM WINS", 18, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.91f, 0.64f, 1f));
            view.WinnerLabel.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));

            var teamsRow = new VisualElement();
            teamsRow.style.display = DisplayStyle.Flex;
            teamsRow.style.flexDirection = FlexDirection.Row;
            teamsRow.style.justifyContent = Justify.Center;
            teamsRow.style.alignItems = Align.Stretch;
            teamsRow.style.flexWrap = Wrap.Wrap;
            teamsRow.style.marginTop = new StyleLength(new Length(24, LengthUnit.Pixel));

            BuildTeamPanel(teamsRow, TeamResult.Red, out var redMvpContainer, out var redPlayersContainer);
            BuildTeamPanel(teamsRow, TeamResult.Blue, out var blueMvpContainer, out var bluePlayersContainer);

            view.RedMvpContainer = redMvpContainer;
            view.RedPlayersContainer = redPlayersContainer;
            view.BlueMvpContainer = blueMvpContainer;
            view.BluePlayersContainer = bluePlayersContainer;

            var buttonRow = new VisualElement();
            buttonRow.style.display = DisplayStyle.Flex;
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.Center;
            buttonRow.style.alignItems = Align.Center;
            buttonRow.style.flexWrap = Wrap.Wrap;
            buttonRow.style.marginTop = new StyleLength(new Length(24, LengthUnit.Pixel));

            view.ContinueButton = new Button(() => onContinue?.Invoke())
            {
                text = "Continue"
            };
            StyleButton(view.ContinueButton, new ButtonPalette(
                new Color(0.16f, 0.53f, 0.36f, 0.98f),
                new Color(0.23f, 0.66f, 0.44f, 1f),
                new Color(0.12f, 0.42f, 0.28f, 1f),
                Color.white));

            view.CloseButton = new Button(() => onClose?.Invoke())
            {
                text = "Close"
            };
            StyleButton(view.CloseButton, new ButtonPalette(
                new Color(0.23f, 0.28f, 0.34f, 0.98f),
                new Color(0.31f, 0.37f, 0.45f, 1f),
                new Color(0.18f, 0.22f, 0.27f, 1f),
                new Color(0.94f, 0.96f, 0.99f, 1f)));
            view.CloseButton.style.marginLeft = new StyleLength(new Length(12, LengthUnit.Pixel));

            buttonRow.Add(view.ContinueButton);
            buttonRow.Add(view.CloseButton);

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

            rootView.Container.style.display = DisplayStyle.Flex;
            if (rootView.Welcome?.Panel != null) rootView.Welcome.Panel.style.display = DisplayStyle.None;
            if (rootView.VotingPanel != null) rootView.VotingPanel.style.display = DisplayStyle.None;
            if (rootView.ApprovalPanel != null) rootView.ApprovalPanel.style.display = DisplayStyle.None;
            if (rootView.DraftPanel != null) rootView.DraftPanel.style.display = DisplayStyle.None;
            rootView.PostMatch.Panel.style.display = DisplayStyle.Flex;
        }

        public static void Render(DraftUIRenderer.View rootView, MatchResultMessage state)
        {
            if (rootView?.PostMatch == null || state == null)
            {
                return;
            }

            var view = rootView.PostMatch;
            view.TitleLabel.text = "MATCH COMPLETE";
            view.WinnerLabel.text = FormatWinningTeam(state.WinningTeam);
            view.WinnerLabel.style.color = new StyleColor(state.WinningTeam == TeamResult.Red
                ? new Color(1f, 0.80f, 0.80f, 1f)
                : state.WinningTeam == TeamResult.Blue
                    ? new Color(0.78f, 0.88f, 1f, 1f)
                    : new Color(0.96f, 0.96f, 0.96f, 1f));

            PopulateTeam(view.RedMvpContainer, view.RedPlayersContainer, TeamResult.Red, state);
            PopulateTeam(view.BlueMvpContainer, view.BluePlayersContainer, TeamResult.Blue, state);
        }

        private static void BuildTeamPanel(VisualElement parent, TeamResult team, out VisualElement mvpContainer, out VisualElement playersContainer)
        {
            var teamPanel = new VisualElement();
            teamPanel.style.display = DisplayStyle.Flex;
            teamPanel.style.flexDirection = FlexDirection.Column;
            teamPanel.style.flexGrow = 1;
            teamPanel.style.flexBasis = new StyleLength(new Length(0, LengthUnit.Pixel));
            teamPanel.style.minWidth = 360;
            teamPanel.style.maxWidth = 520;
            teamPanel.style.marginLeft = new StyleLength(new Length(10, LengthUnit.Pixel));
            teamPanel.style.marginRight = new StyleLength(new Length(10, LengthUnit.Pixel));
            teamPanel.style.marginBottom = new StyleLength(new Length(18, LengthUnit.Pixel));
            teamPanel.style.paddingTop = new StyleLength(new Length(18, LengthUnit.Pixel));
            teamPanel.style.paddingBottom = new StyleLength(new Length(18, LengthUnit.Pixel));
            teamPanel.style.paddingLeft = new StyleLength(new Length(18, LengthUnit.Pixel));
            teamPanel.style.paddingRight = new StyleLength(new Length(18, LengthUnit.Pixel));
            teamPanel.style.backgroundColor = new StyleColor(TeamPanelColor(team));
            teamPanel.style.borderTopLeftRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            teamPanel.style.borderTopRightRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            teamPanel.style.borderBottomLeftRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            teamPanel.style.borderBottomRightRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            teamPanel.style.borderTopWidth = 1;
            teamPanel.style.borderRightWidth = 1;
            teamPanel.style.borderBottomWidth = 1;
            teamPanel.style.borderLeftWidth = 1;
            teamPanel.style.borderTopColor = new StyleColor(Tint(TeamAccent(team), 1.1f, 0.12f));
            teamPanel.style.borderRightColor = new StyleColor(Tint(TeamAccent(team), 0.85f, -0.08f));
            teamPanel.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.32f));
            teamPanel.style.borderLeftColor = new StyleColor(Tint(TeamAccent(team), 0.85f, -0.08f));

            var titleRow = new VisualElement();
            titleRow.style.display = DisplayStyle.Flex;
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = new StyleLength(new Length(14, LengthUnit.Pixel));

            var teamLabel = CreateLabel(FormatTeamName(team), 18, FontStyle.Bold, TextAnchor.MiddleLeft, ReadableTeamColor(team));
            var mvpTag = CreateLabel("TEAM MVP", 12, FontStyle.Bold, TextAnchor.MiddleRight, Tint(ReadableTeamColor(team), 1f, 0f));
            titleRow.Add(teamLabel);
            titleRow.Add(mvpTag);

            mvpContainer = new VisualElement();
            mvpContainer.style.display = DisplayStyle.Flex;
            mvpContainer.style.flexDirection = FlexDirection.Column;
            mvpContainer.style.minHeight = 150;
            mvpContainer.style.marginBottom = new StyleLength(new Length(18, LengthUnit.Pixel));

            var rosterTitle = CreateLabel("ROSTER", 13, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.90f, 0.93f, 0.97f, 0.84f));
            rosterTitle.style.marginBottom = new StyleLength(new Length(10, LengthUnit.Pixel));

            playersContainer = new VisualElement();
            playersContainer.style.display = DisplayStyle.Flex;
            playersContainer.style.flexDirection = FlexDirection.Column;

            teamPanel.Add(titleRow);
            teamPanel.Add(mvpContainer);
            teamPanel.Add(rosterTitle);
            teamPanel.Add(playersContainer);

            parent.Add(teamPanel);
        }

        private static void PopulateTeam(VisualElement mvpContainer, VisualElement playersContainer, TeamResult team, MatchResultMessage state)
        {
            mvpContainer.Clear();
            playersContainer.Clear();

            var teamPlayers = SortPlayers(state, team).ToArray();
            if (teamPlayers.Length == 0)
            {
                mvpContainer.Add(CreateEmptyLabel("No player data"));
                playersContainer.Add(CreateEmptyLabel("No additional players"));
                return;
            }

            var mvp = teamPlayers.FirstOrDefault(player => player.IsMVP) ?? teamPlayers[0];
            mvpContainer.Add(CreateMvpCard(mvp, team, team == state.WinningTeam));

            var remainingPlayers = teamPlayers.Where(player => !ReferenceEquals(player, mvp)).ToArray();
            if (remainingPlayers.Length == 0)
            {
                playersContainer.Add(CreateEmptyLabel("No additional players"));
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
                + (player.Saves * 3)
                + player.Shots
                + (player.Team == winningTeam ? 2 : 0);
        }

        private static VisualElement CreateMvpCard(MatchResultPlayerMessage player, TeamResult team, bool winningTeam)
        {
            var card = new VisualElement();
            card.style.display = DisplayStyle.Flex;
            card.style.flexDirection = FlexDirection.Column;
            card.style.paddingTop = new StyleLength(new Length(18, LengthUnit.Pixel));
            card.style.paddingBottom = new StyleLength(new Length(18, LengthUnit.Pixel));
            card.style.paddingLeft = new StyleLength(new Length(18, LengthUnit.Pixel));
            card.style.paddingRight = new StyleLength(new Length(18, LengthUnit.Pixel));
            card.style.backgroundColor = new StyleColor(Tint(TeamPanelColor(team), winningTeam ? 1.18f : 1.08f, 0.14f));
            card.style.borderTopLeftRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            card.style.borderTopRightRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            card.style.borderBottomLeftRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            card.style.borderBottomRightRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            card.style.borderTopWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderTopColor = new StyleColor(Tint(TeamAccent(team), 1.25f, 0.22f));
            card.style.borderRightColor = new StyleColor(Tint(TeamAccent(team), 0.95f, 0.04f));
            card.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.34f));
            card.style.borderLeftColor = new StyleColor(Tint(TeamAccent(team), 0.95f, 0.04f));

            var topRow = new VisualElement();
            topRow.style.display = DisplayStyle.Flex;
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.justifyContent = Justify.SpaceBetween;
            topRow.style.alignItems = Align.Center;

            var badge = CreateLabel("♛ MVP", 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.92f, 0.64f, 1f));
            var delta = CreateLabel(FormatMmrDelta(player.MmrDelta), 18, FontStyle.Bold, TextAnchor.MiddleRight, DeltaColor(player.MmrDelta));

            var nameLabel = CreateLabel(player.Username ?? "---", 24, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            nameLabel.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));

            var statsLabel = CreateLabel(FormatStats(player), 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.87f, 0.92f, 0.97f, 1f));
            statsLabel.style.marginTop = new StyleLength(new Length(8, LengthUnit.Pixel));

            var mmrLabel = CreateLabel($"MMR {player.MmrBefore} → {player.MmrAfter}", 13, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.96f, 0.92f, 0.72f, 1f));
            mmrLabel.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));

            topRow.Add(badge);
            topRow.Add(delta);
            card.Add(topRow);
            card.Add(nameLabel);
            card.Add(statsLabel);
            card.Add(mmrLabel);

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
            row.style.marginBottom = new StyleLength(new Length(10, LengthUnit.Pixel));
            row.style.backgroundColor = new StyleColor(Tint(TeamPanelColor(team), 0.95f, 0.06f));
            row.style.borderTopLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            row.style.borderTopRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            row.style.borderBottomLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            row.style.borderBottomRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            row.style.borderTopWidth = 1;
            row.style.borderRightWidth = 1;
            row.style.borderBottomWidth = 1;
            row.style.borderLeftWidth = 1;
            row.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.12f));
            row.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
            row.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.24f));
            row.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));

            var left = new VisualElement();
            left.style.display = DisplayStyle.Flex;
            left.style.flexDirection = FlexDirection.Column;
            left.style.flexGrow = 1;

            var nameRow = new VisualElement();
            nameRow.style.display = DisplayStyle.Flex;
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;

            if (player.IsMVP)
            {
                var crown = CreateLabel("♛", 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.90f, 0.60f, 1f));
                crown.style.marginRight = new StyleLength(new Length(8, LengthUnit.Pixel));
                nameRow.Add(crown);
            }

            var nameLabel = CreateLabel(player.Username ?? "---", 15, FontStyle.Bold, TextAnchor.MiddleLeft, ReadableTeamColor(team));
            nameRow.Add(nameLabel);

            var statsLabel = CreateLabel(FormatStats(player), 12, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.83f, 0.89f, 0.95f, 1f));
            statsLabel.style.marginTop = new StyleLength(new Length(6, LengthUnit.Pixel));

            left.Add(nameRow);
            left.Add(statsLabel);

            var right = new VisualElement();
            right.style.display = DisplayStyle.Flex;
            right.style.flexDirection = FlexDirection.Column;
            right.style.alignItems = Align.FlexEnd;
            right.style.marginLeft = new StyleLength(new Length(12, LengthUnit.Pixel));

            var deltaLabel = CreateLabel(FormatMmrDelta(player.MmrDelta), 15, FontStyle.Bold, TextAnchor.MiddleRight, DeltaColor(player.MmrDelta));
            var mmrLabel = CreateLabel($"{player.MmrAfter} MMR", 12, FontStyle.Normal, TextAnchor.MiddleRight, new Color(0.95f, 0.92f, 0.75f, 1f));
            mmrLabel.style.marginTop = new StyleLength(new Length(4, LengthUnit.Pixel));

            right.Add(deltaLabel);
            right.Add(mmrLabel);

            row.Add(left);
            row.Add(right);
            return row;
        }

        private static Label CreateEmptyLabel(string text)
        {
            var label = CreateLabel(text, 13, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.74f, 0.80f, 0.86f, 0.84f));
            label.style.paddingTop = new StyleLength(new Length(18, LengthUnit.Pixel));
            label.style.paddingBottom = new StyleLength(new Length(18, LengthUnit.Pixel));
            return label;
        }

        private static string FormatWinningTeam(TeamResult team)
        {
            switch (team)
            {
                case TeamResult.Red:
                    return "RED TEAM WINS";
                case TeamResult.Blue:
                    return "BLUE TEAM WINS";
                default:
                    return "MATCH COMPLETE";
            }
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

        private static string FormatStats(MatchResultPlayerMessage player)
        {
            return $"G {player?.Goals ?? 0}   A {player?.Assists ?? 0}   S {player?.Saves ?? 0}   SH {player?.Shots ?? 0}";
        }

        private static string FormatMmrDelta(int delta)
        {
            return delta > 0 ? $"+{delta}" : delta.ToString();
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
            button.style.minWidth = 190;
            button.style.height = 42;
            button.style.paddingLeft = new StyleLength(new Length(16, LengthUnit.Pixel));
            button.style.paddingRight = new StyleLength(new Length(16, LengthUnit.Pixel));
            button.style.paddingTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            button.style.paddingBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            button.style.backgroundColor = new StyleColor(palette.Normal);
            button.style.color = new StyleColor(palette.Text);
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
            if (button == null)
            {
                return;
            }

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