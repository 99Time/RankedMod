using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace schrader
{
    internal static class WelcomeUIRenderer
    {
        private const float PanelEntranceDurationSeconds = 0.22f;
        private const float CardEntranceDurationSeconds = 0.18f;

        public sealed class View
        {
            public VisualElement Panel;
            public VisualElement Hero;
            public VisualElement RulesSection;
            public VisualElement CommandsSection;
            public VisualElement ButtonRow;
            public Button DiscordButton;
            public Button ContinueButton;
        }

        private static Texture2D cachedLogoTexture;
        private static bool logoLoadAttempted;
        private static Texture2D cachedDiscordTexture;
        private static bool discordLogoLoadAttempted;

        public static void BuildUI(DraftUIRenderer.View rootView, Action onOpenDiscord, Action onContinue)
        {
            if (rootView?.Root == null)
            {
                return;
            }

            var view = new View();
            var panel = new VisualElement();
            panel.name = "WelcomeScreenPanel";
            panel.style.display = DisplayStyle.None;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.alignSelf = Align.Center;
            panel.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            panel.style.maxWidth = 816;
            panel.style.paddingTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            panel.style.paddingBottom = new StyleLength(new Length(10, LengthUnit.Pixel));
            panel.style.paddingLeft = new StyleLength(new Length(14, LengthUnit.Pixel));
            panel.style.paddingRight = new StyleLength(new Length(14, LengthUnit.Pixel));
            panel.style.backgroundColor = new StyleColor(new Color(0.05f, 0.08f, 0.12f, 0.78f));
            panel.style.borderTopLeftRadius = new StyleLength(new Length(28, LengthUnit.Pixel));
            panel.style.borderTopRightRadius = new StyleLength(new Length(28, LengthUnit.Pixel));
            panel.style.borderBottomLeftRadius = new StyleLength(new Length(28, LengthUnit.Pixel));
            panel.style.borderBottomRightRadius = new StyleLength(new Length(28, LengthUnit.Pixel));
            panel.style.borderTopWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderTopColor = new StyleColor(new Color(0.92f, 0.97f, 1f, 0.12f));
            panel.style.borderRightColor = new StyleColor(new Color(0.92f, 0.97f, 1f, 0.05f));
            panel.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.30f));
            panel.style.borderLeftColor = new StyleColor(new Color(0.92f, 0.97f, 1f, 0.05f));
            panel.style.opacity = 0f;
            panel.transform.scale = new Vector3(0.988f, 0.988f, 1f);
            panel.transform.position = new Vector3(0f, 14f, 0f);

            var hero = new VisualElement();
            hero.style.display = DisplayStyle.Flex;
            hero.style.flexDirection = FlexDirection.Column;
            hero.style.alignItems = Align.Center;
            hero.style.marginBottom = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.Hero = hero;

            var logoFrame = new VisualElement();
            logoFrame.style.display = DisplayStyle.Flex;
            logoFrame.style.justifyContent = Justify.Center;
            logoFrame.style.alignItems = Align.Center;
            logoFrame.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            logoFrame.style.maxWidth = 452;
            logoFrame.style.minHeight = 126;
            logoFrame.style.paddingTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            logoFrame.style.paddingBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            logoFrame.style.paddingLeft = new StyleLength(new Length(14, LengthUnit.Pixel));
            logoFrame.style.paddingRight = new StyleLength(new Length(14, LengthUnit.Pixel));
            logoFrame.style.backgroundColor = new StyleColor(new Color(0.10f, 0.16f, 0.22f, 0.52f));
            logoFrame.style.borderTopLeftRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            logoFrame.style.borderTopRightRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            logoFrame.style.borderBottomLeftRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            logoFrame.style.borderBottomRightRadius = new StyleLength(new Length(22, LengthUnit.Pixel));
            logoFrame.style.borderTopWidth = 1;
            logoFrame.style.borderRightWidth = 1;
            logoFrame.style.borderBottomWidth = 1;
            logoFrame.style.borderLeftWidth = 1;
            logoFrame.style.borderTopColor = new StyleColor(new Color(0.74f, 0.86f, 0.96f, 0.18f));
            logoFrame.style.borderRightColor = new StyleColor(new Color(0.74f, 0.86f, 0.96f, 0.08f));
            logoFrame.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.28f));
            logoFrame.style.borderLeftColor = new StyleColor(new Color(0.74f, 0.86f, 0.96f, 0.08f));

            var logoTexture = TryLoadLogoTexture();
            if (logoTexture != null)
            {
                var logo = new Image();
                logo.name = "SpeedRankedLogo";
                logo.image = logoTexture;
                logo.scaleMode = ScaleMode.ScaleToFit;
                logo.style.width = new StyleLength(new Length(420, LengthUnit.Pixel));
                logo.style.maxWidth = new StyleLength(new Length(100, LengthUnit.Percent));
                logo.style.height = new StyleLength(new Length(112, LengthUnit.Pixel));
                logoFrame.Add(logo);
            }

            var titleLabel = CreateLabel("Welcome to SpeedRankeds", 27, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.98f, 0.99f, 1f, 1f));
            titleLabel.style.marginTop = new StyleLength(new Length(12, LengthUnit.Pixel));

            var subtitleLabel = CreateLabel("Read the basics, then choose Spectator, Red, or Blue.", 12, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.82f, 0.89f, 0.95f, 0.92f));
            subtitleLabel.style.marginTop = new StyleLength(new Length(6, LengthUnit.Pixel));
            subtitleLabel.style.maxWidth = 560;

            hero.Add(logoFrame);
            hero.Add(titleLabel);
            hero.Add(subtitleLabel);

            var rulesSection = CreateSectionPanel("START HERE");
            view.RulesSection = rulesSection;
            AddRuleRow(rulesSection, "Keep chat clean");
            AddRuleRow(rulesSection, "No slurs, harassment, or dogpiling");
            AddRuleRow(rulesSection, "Respect teammates and opponents");
            AddRuleRow(rulesSection, "Continue, then pick Spectator, Red, or Blue");

            var commandsSection = CreateSectionPanel("QUICK COMMANDS");
            view.CommandsSection = commandsSection;
            commandsSection.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            var commandsHintLabel = CreateLabel("Need everything? Use /commands. Admin-only lines appear automatically for admins.", 12, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.82f, 0.89f, 0.95f, 0.90f));
            commandsHintLabel.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            commandsSection.Add(commandsHintLabel);
            commandsSection.Add(BuildCommandGrid());

            var content = new VisualElement();
            content.style.display = DisplayStyle.Flex;
            content.style.flexDirection = FlexDirection.Column;
            content.style.width = new StyleLength(new Length(100, LengthUnit.Percent));

            content.Add(rulesSection);
            content.Add(commandsSection);

            var buttonRow = new VisualElement();
            buttonRow.style.display = DisplayStyle.Flex;
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            buttonRow.style.alignSelf = Align.Stretch;
            buttonRow.style.justifyContent = Justify.Center;
            buttonRow.style.alignItems = Align.Center;
            buttonRow.style.flexWrap = Wrap.Wrap;
            buttonRow.style.marginTop = new StyleLength(new Length(16, LengthUnit.Pixel));
            buttonRow.style.paddingTop = new StyleLength(new Length(12, LengthUnit.Pixel));
            buttonRow.style.borderTopWidth = 1;
            buttonRow.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));
            buttonRow.style.paddingLeft = 0;
            buttonRow.style.paddingRight = 0;
            view.ButtonRow = buttonRow;

            view.DiscordButton = new Button(() => onOpenDiscord?.Invoke());
            StyleButton(view.DiscordButton, new ButtonPalette(
                new Color(0.16f, 0.33f, 0.70f, 0.98f),
                new Color(0.23f, 0.42f, 0.84f, 1f),
                new Color(0.11f, 0.25f, 0.56f, 1f),
                Color.white));
            SetButtonContent(view.DiscordButton, CreateButtonContent("JOIN DISCORD", CreateDiscordIcon()));
            view.DiscordButton.style.minWidth = 184;
            view.DiscordButton.style.height = 46;
            view.DiscordButton.style.alignSelf = Align.Center;
            view.DiscordButton.style.flexShrink = 0;
            view.DiscordButton.style.marginRight = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.DiscordButton.style.marginBottom = new StyleLength(new Length(6, LengthUnit.Pixel));

            view.ContinueButton = new Button(() => onContinue?.Invoke());
            StyleButton(view.ContinueButton, new ButtonPalette(
                new Color(0.16f, 0.53f, 0.36f, 0.98f),
                new Color(0.23f, 0.66f, 0.44f, 1f),
                new Color(0.12f, 0.42f, 0.28f, 1f),
                Color.white));
            SetButtonContent(view.ContinueButton, CreateButtonContent("CHOOSE TEAM", null));
            view.ContinueButton.style.minWidth = 208;
            view.ContinueButton.style.height = 46;
            view.ContinueButton.style.alignSelf = Align.Center;
            view.ContinueButton.style.flexShrink = 0;
            view.ContinueButton.style.marginBottom = new StyleLength(new Length(6, LengthUnit.Pixel));

            buttonRow.Add(view.DiscordButton);
            buttonRow.Add(view.ContinueButton);

            panel.Add(hero);
            panel.Add(content);
            panel.Add(buttonRow);

            rootView.Root.Add(panel);
            view.Panel = panel;
            rootView.Welcome = view;
        }

        public static void Show(DraftUIRenderer.View rootView)
        {
            if (rootView?.Welcome?.Panel == null)
            {
                return;
            }

            var wasAlreadyVisible = rootView.Welcome.Panel.style.display != DisplayStyle.None
                && rootView.Welcome.Panel.resolvedStyle.display != DisplayStyle.None;

            rootView.Container.style.display = DisplayStyle.Flex;
            if (rootView.VotingPanel != null) rootView.VotingPanel.style.display = DisplayStyle.None;
            if (rootView.ApprovalPanel != null) rootView.ApprovalPanel.style.display = DisplayStyle.None;
            if (rootView.DraftPanel != null) rootView.DraftPanel.style.display = DisplayStyle.None;
            if (rootView.PostMatch?.Panel != null) rootView.PostMatch.Panel.style.display = DisplayStyle.None;
            rootView.Welcome.Panel.style.display = DisplayStyle.Flex;

            if (wasAlreadyVisible)
            {
                CompleteWelcomeVisibleState(rootView.Welcome);
                return;
            }

            StartWelcomeEntranceAnimation(rootView.Welcome);
        }

        private static VisualElement CreateSectionPanel(string title)
        {
            var section = new VisualElement();
            section.style.display = DisplayStyle.Flex;
            section.style.flexDirection = FlexDirection.Column;
            section.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            section.style.paddingTop = new StyleLength(new Length(12, LengthUnit.Pixel));
            section.style.paddingBottom = new StyleLength(new Length(12, LengthUnit.Pixel));
            section.style.paddingLeft = new StyleLength(new Length(14, LengthUnit.Pixel));
            section.style.paddingRight = new StyleLength(new Length(14, LengthUnit.Pixel));
            section.style.backgroundColor = new StyleColor(new Color(0.09f, 0.13f, 0.18f, 0.88f));
            section.style.borderTopLeftRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            section.style.borderTopRightRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            section.style.borderBottomLeftRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            section.style.borderBottomRightRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            section.style.borderTopWidth = 1;
            section.style.borderRightWidth = 1;
            section.style.borderBottomWidth = 1;
            section.style.borderLeftWidth = 1;
            section.style.borderTopColor = new StyleColor(new Color(0.62f, 0.78f, 0.90f, 0.16f));
            section.style.borderRightColor = new StyleColor(new Color(0.62f, 0.78f, 0.90f, 0.07f));
            section.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.32f));
            section.style.borderLeftColor = new StyleColor(new Color(0.62f, 0.78f, 0.90f, 0.07f));

            var chip = new VisualElement();
            chip.style.alignSelf = Align.FlexStart;
            chip.style.paddingTop = new StyleLength(new Length(4, LengthUnit.Pixel));
            chip.style.paddingBottom = new StyleLength(new Length(4, LengthUnit.Pixel));
            chip.style.paddingLeft = new StyleLength(new Length(10, LengthUnit.Pixel));
            chip.style.paddingRight = new StyleLength(new Length(10, LengthUnit.Pixel));
            chip.style.marginBottom = new StyleLength(new Length(10, LengthUnit.Pixel));
            chip.style.backgroundColor = new StyleColor(new Color(0.18f, 0.30f, 0.40f, 0.44f));
            chip.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            chip.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            chip.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            chip.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            chip.style.borderTopWidth = 1;
            chip.style.borderRightWidth = 1;
            chip.style.borderBottomWidth = 1;
            chip.style.borderLeftWidth = 1;
            chip.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));
            chip.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.04f));
            chip.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.20f));
            chip.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.04f));
            chip.Add(CreateLabel(title, 11, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.90f, 0.95f, 0.99f, 0.94f)));

            section.Add(chip);
            return section;
        }

        private static void AddRuleRow(VisualElement parent, string text)
        {
            var row = new VisualElement();
            row.style.display = DisplayStyle.Flex;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = new StyleLength(new Length(6, LengthUnit.Pixel));
            row.style.paddingTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            row.style.paddingBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            row.style.paddingLeft = new StyleLength(new Length(10, LengthUnit.Pixel));
            row.style.paddingRight = new StyleLength(new Length(10, LengthUnit.Pixel));
            row.style.backgroundColor = new StyleColor(new Color(0.12f, 0.17f, 0.23f, 0.84f));
            row.style.borderTopLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            row.style.borderTopRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            row.style.borderBottomLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            row.style.borderBottomRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            row.style.borderTopWidth = 1;
            row.style.borderRightWidth = 1;
            row.style.borderBottomWidth = 1;
            row.style.borderLeftWidth = 1;
            row.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.05f));
            row.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.03f));
            row.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.22f));
            row.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.03f));

            var accent = new VisualElement();
            accent.style.width = 8;
            accent.style.height = 8;
            accent.style.marginRight = new StyleLength(new Length(10, LengthUnit.Pixel));
            accent.style.backgroundColor = new StyleColor(new Color(0.44f, 0.80f, 0.62f, 1f));
            accent.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            accent.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            accent.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            accent.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));

            var label = CreateLabel(text, 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.94f, 0.97f, 1f, 0.98f));
            label.style.flexGrow = 1;

            row.Add(accent);
            row.Add(label);
            parent.Add(row);
        }

        private static VisualElement BuildCommandGrid()
        {
            var grid = new VisualElement();
            grid.style.display = DisplayStyle.Flex;
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.width = new StyleLength(new Length(100, LengthUnit.Percent));

            var leftColumn = CreateCommandColumn();
            leftColumn.style.marginRight = new StyleLength(new Length(6, LengthUnit.Pixel));
            leftColumn.Add(CreateCommandCard("/commands", "Show the full command list in chat."));
            leftColumn.Add(CreateCommandCard("/vr", "Start the ranked ready vote."));
            leftColumn.Add(CreateCommandCard("/y or /n", "Answer ready checks and forfeit votes."));

            var rightColumn = CreateCommandColumn();
            rightColumn.style.marginLeft = new StyleLength(new Length(6, LengthUnit.Pixel));
            rightColumn.Add(CreateCommandCard("/ff", "Start or vote on a team forfeit."));
            rightColumn.Add(CreateCommandCard("/mmr", "Show your current MMR."));
            rightColumn.Add(CreateCommandCard("/discord", "Open the Discord invite in your browser."));

            grid.Add(leftColumn);
            grid.Add(rightColumn);
            return grid;
        }

        private static VisualElement CreateCommandColumn()
        {
            var column = new VisualElement();
            column.style.display = DisplayStyle.Flex;
            column.style.flexDirection = FlexDirection.Column;
            column.style.flexGrow = 1;
            column.style.flexBasis = new StyleLength(new Length(0, LengthUnit.Pixel));
            column.style.minWidth = 240;
            return column;
        }

        private static VisualElement CreateCommandCard(string command, string description)
        {
            var card = new VisualElement();
            card.style.display = DisplayStyle.Flex;
            card.style.flexDirection = FlexDirection.Column;
            card.style.marginBottom = new StyleLength(new Length(6, LengthUnit.Pixel));
            card.style.paddingTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            card.style.paddingBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            card.style.paddingLeft = new StyleLength(new Length(10, LengthUnit.Pixel));
            card.style.paddingRight = new StyleLength(new Length(10, LengthUnit.Pixel));
            card.style.backgroundColor = new StyleColor(new Color(0.12f, 0.17f, 0.23f, 0.84f));
            card.style.borderTopLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.borderTopRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.borderBottomLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.borderBottomRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            card.style.borderTopWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;

            var normalColor = new Color(0.12f, 0.17f, 0.23f, 0.84f);
            var hoverColor = new Color(0.15f, 0.21f, 0.28f, 0.92f);
            ApplyCommandCardVisual(card, normalColor, 1f);

            card.RegisterCallback<PointerEnterEvent>(_ => ApplyCommandCardVisual(card, hoverColor, 1.01f));
            card.RegisterCallback<PointerLeaveEvent>(_ => ApplyCommandCardVisual(card, normalColor, 1f));

            var commandLabel = CreateLabel(command, 13, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.98f, 0.90f, 0.62f, 1f));
            var descriptionLabel = CreateLabel(description, 11, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.84f, 0.90f, 0.96f, 0.94f));
            descriptionLabel.style.marginTop = new StyleLength(new Length(3, LengthUnit.Pixel));

            card.Add(commandLabel);
            card.Add(descriptionLabel);
            return card;
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
            button.style.paddingLeft = new StyleLength(new Length(16, LengthUnit.Pixel));
            button.style.paddingRight = new StyleLength(new Length(16, LengthUnit.Pixel));
            button.style.paddingTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            button.style.paddingBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.justifyContent = Justify.Center;
            button.style.alignItems = Align.Center;
            button.style.borderTopLeftRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            button.style.borderTopRightRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            button.style.borderBottomLeftRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            button.style.borderBottomRightRadius = new StyleLength(new Length(18, LengthUnit.Pixel));
            button.style.borderTopWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            ApplyButtonVisual(button, palette, palette.Normal, 1f);

            button.RegisterCallback<PointerEnterEvent>(_ => ApplyButtonVisual(button, palette, palette.Hover, 1.02f));
            button.RegisterCallback<PointerLeaveEvent>(_ => ApplyButtonVisual(button, palette, palette.Normal, 1f));
            button.RegisterCallback<PointerDownEvent>(_ => ApplyButtonVisual(button, palette, palette.Pressed, 0.99f));
            button.RegisterCallback<PointerUpEvent>(_ => ApplyButtonVisual(button, palette, palette.Hover, 1.02f));
        }

        private static void SetButtonContent(Button button, VisualElement content)
        {
            if (button == null || content == null)
            {
                return;
            }

            button.text = string.Empty;
            button.Clear();
            button.Add(content);
        }

        private static VisualElement CreateButtonContent(string labelText, VisualElement leadingIcon)
        {
            var content = new VisualElement();
            content.style.display = DisplayStyle.Flex;
            content.style.flexDirection = FlexDirection.Row;
            content.style.justifyContent = Justify.Center;
            content.style.alignItems = Align.Center;
            content.pickingMode = PickingMode.Ignore;

            if (leadingIcon != null)
            {
                leadingIcon.style.marginRight = new StyleLength(new Length(12, LengthUnit.Pixel));
                leadingIcon.pickingMode = PickingMode.Ignore;
                content.Add(leadingIcon);
            }

            var label = CreateLabel(labelText, 13, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            label.style.letterSpacing = 0.3f;
            label.pickingMode = PickingMode.Ignore;
            content.Add(label);
            return content;
        }

        private static VisualElement CreateDiscordIcon()
        {
            var iconTexture = TryLoadDiscordTexture();
            if (iconTexture != null)
            {
                var icon = new Image();
                icon.image = iconTexture;
                icon.scaleMode = ScaleMode.ScaleToFit;
                icon.style.width = new StyleLength(new Length(64, LengthUnit.Pixel));
                icon.style.height = new StyleLength(new Length(64, LengthUnit.Pixel));
                icon.style.unityBackgroundImageTintColor = new StyleColor(Color.white);
                return icon;
            }

            return CreateDiscordFallbackIcon();
        }

        private static VisualElement CreateDiscordFallbackIcon()
        {
            var iconBadge = new VisualElement();
            iconBadge.style.width = 64;
            iconBadge.style.height = 64;
            iconBadge.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.18f));
            iconBadge.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            iconBadge.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            iconBadge.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            iconBadge.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));

            var shell = new VisualElement();
            shell.style.position = Position.Absolute;
            shell.style.left = 6;
            shell.style.top = 9;
            shell.style.width = 20;
            shell.style.height = 14;
            shell.style.backgroundColor = new StyleColor(Color.white);
            shell.style.borderTopLeftRadius = new StyleLength(new Length(8, LengthUnit.Pixel));
            shell.style.borderTopRightRadius = new StyleLength(new Length(8, LengthUnit.Pixel));
            shell.style.borderBottomLeftRadius = new StyleLength(new Length(8, LengthUnit.Pixel));
            shell.style.borderBottomRightRadius = new StyleLength(new Length(8, LengthUnit.Pixel));

            var leftEye = new VisualElement();
            leftEye.style.position = Position.Absolute;
            leftEye.style.left = 4;
            leftEye.style.top = 4;
            leftEye.style.width = 3;
            leftEye.style.height = 3;
            leftEye.style.backgroundColor = new StyleColor(new Color(0.23f, 0.42f, 0.84f, 1f));
            leftEye.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            leftEye.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            leftEye.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            leftEye.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));

            var rightEye = new VisualElement();
            rightEye.style.position = Position.Absolute;
            rightEye.style.right = 4;
            rightEye.style.top = 4;
            rightEye.style.width = 3;
            rightEye.style.height = 3;
            rightEye.style.backgroundColor = new StyleColor(new Color(0.23f, 0.42f, 0.84f, 1f));
            rightEye.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            rightEye.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            rightEye.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            rightEye.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));

            var smile = new VisualElement();
            smile.style.position = Position.Absolute;
            smile.style.left = 5;
            smile.style.right = 5;
            smile.style.bottom = 3;
            smile.style.height = 2;
            smile.style.backgroundColor = new StyleColor(new Color(0.23f, 0.42f, 0.84f, 1f));
            smile.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            smile.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            smile.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            smile.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));

            shell.Add(leftEye);
            shell.Add(rightEye);
            shell.Add(smile);
            iconBadge.Add(shell);
            return iconBadge;
        }

        private static void ApplyButtonVisual(Button button, ButtonPalette palette, Color color, float scale)
        {
            if (button == null)
            {
                return;
            }

            button.style.backgroundColor = new StyleColor(color);
            button.style.color = new StyleColor(palette.Text);
            button.style.borderTopColor = new StyleColor(Tint(color, 1.10f, 0.10f));
            button.style.borderRightColor = new StyleColor(Tint(color, 1.02f, 0.02f));
            button.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.28f));
            button.style.borderLeftColor = new StyleColor(Tint(color, 1.02f, 0.02f));
            button.transform.scale = new Vector3(scale, scale, 1f);
        }

        private static void ApplyCommandCardVisual(VisualElement card, Color color, float scale)
        {
            if (card == null)
            {
                return;
            }

            card.style.backgroundColor = new StyleColor(color);
            card.style.borderTopColor = new StyleColor(Tint(color, 1.08f, 0.06f));
            card.style.borderRightColor = new StyleColor(Tint(color, 1.02f, 0.02f));
            card.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.22f));
            card.style.borderLeftColor = new StyleColor(Tint(color, 1.02f, 0.02f));
            card.transform.scale = new Vector3(scale, scale, 1f);
        }

        private static void StartWelcomeEntranceAnimation(View view)
        {
            if (view == null)
            {
                return;
            }

            StartElementEntranceAnimation(view.Panel, 0f, PanelEntranceDurationSeconds, 14f, new Vector3(0.988f, 0.988f, 1f));
            StartElementEntranceAnimation(view.Hero, 0.03f, CardEntranceDurationSeconds, 8f, new Vector3(1f, 1f, 1f));
            StartElementEntranceAnimation(view.RulesSection, 0.07f, CardEntranceDurationSeconds, 10f, new Vector3(0.996f, 0.996f, 1f));
            StartElementEntranceAnimation(view.CommandsSection, 0.11f, CardEntranceDurationSeconds, 10f, new Vector3(0.996f, 0.996f, 1f));
            StartElementEntranceAnimation(view.ButtonRow, 0.15f, CardEntranceDurationSeconds, 8f, new Vector3(0.996f, 0.996f, 1f));
        }

        private static void CompleteWelcomeVisibleState(View view)
        {
            if (view == null)
            {
                return;
            }

            CompleteElementVisibleState(view.Panel);
            CompleteElementVisibleState(view.Hero);
            CompleteElementVisibleState(view.RulesSection);
            CompleteElementVisibleState(view.CommandsSection);
            CompleteElementVisibleState(view.ButtonRow);
        }

        private static void StartElementEntranceAnimation(VisualElement element, float delaySeconds, float durationSeconds, float yOffset, Vector3 startScale)
        {
            if (element == null)
            {
                return;
            }

            element.style.opacity = 0f;
            element.transform.scale = startScale;
            element.transform.position = new Vector3(0f, yOffset, 0f);

            var startTime = Time.unscaledTime + delaySeconds;
            IVisualElementScheduledItem animationItem = null;
            animationItem = element.schedule.Execute(() =>
            {
                if (element.panel == null)
                {
                    animationItem.Pause();
                    return;
                }

                var t = Mathf.Clamp01((Time.unscaledTime - startTime) / durationSeconds);
                if (Time.unscaledTime < startTime)
                {
                    return;
                }

                var eased = 1f - Mathf.Pow(1f - t, 3f);
                element.style.opacity = eased;
                element.transform.scale = Vector3.LerpUnclamped(startScale, Vector3.one, eased);
                element.transform.position = Vector3.LerpUnclamped(new Vector3(0f, yOffset, 0f), Vector3.zero, eased);

                if (t >= 1f)
                {
                    animationItem.Pause();
                }
            }).Every(16);
        }

        private static void CompleteElementVisibleState(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            element.style.opacity = 1f;
            element.transform.scale = Vector3.one;
            element.transform.position = Vector3.zero;
        }

        private static Color Tint(Color color, float multiplier, float alphaOffset)
        {
            return new Color(
                Mathf.Clamp01(color.r * multiplier),
                Mathf.Clamp01(color.g * multiplier),
                Mathf.Clamp01(color.b * multiplier),
                Mathf.Clamp01(color.a + alphaOffset));
        }

        private static Texture2D TryLoadLogoTexture()
        {
            if (logoLoadAttempted)
            {
                return cachedLogoTexture;
            }

            logoLoadAttempted = true;

            try
            {
                var logoPath = ResolveLogoPath();
                if (string.IsNullOrWhiteSpace(logoPath) || !File.Exists(logoPath))
                {
                    DraftUIPlugin.LogError("Welcome logo not found in any expected location.");
                    return null;
                }

                var fileBytes = File.ReadAllBytes(logoPath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, fileBytes, false))
                {
                    UnityEngine.Object.Destroy(texture);
                    DraftUIPlugin.LogError("Failed to decode welcome logo image");
                    return null;
                }

                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                cachedLogoTexture = texture;
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to load welcome logo: {ex}");
            }

            return cachedLogoTexture;
        }

        private static Texture2D TryLoadDiscordTexture()
        {
            if (discordLogoLoadAttempted)
            {
                return cachedDiscordTexture;
            }

            discordLogoLoadAttempted = true;

            try
            {
                var logoPath = ResolveDiscordLogoPath();
                if (string.IsNullOrWhiteSpace(logoPath) || !File.Exists(logoPath))
                {
                    DraftUIPlugin.LogError("Welcome Discord logo not found in any expected location.");
                    return null;
                }

                var fileBytes = File.ReadAllBytes(logoPath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, fileBytes, false))
                {
                    UnityEngine.Object.Destroy(texture);
                    DraftUIPlugin.LogError("Failed to decode welcome Discord logo image");
                    return null;
                }

                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                cachedDiscordTexture = texture;
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to load welcome Discord logo: {ex}");
            }

            return cachedDiscordTexture;
        }

        private static string ResolveLogoPath()
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            var baseDirectory = AppContext.BaseDirectory;

            var candidates = new[]
            {
                assemblyDirectory != null ? Path.Combine(assemblyDirectory, "speedrankedlogo.png") : null,
                assemblyDirectory != null ? Path.Combine(assemblyDirectory, "src", "speedrankedlogo.png") : null,
                !string.IsNullOrWhiteSpace(baseDirectory) ? Path.Combine(baseDirectory, "speedrankedlogo.png") : null,
                !string.IsNullOrWhiteSpace(baseDirectory) ? Path.Combine(baseDirectory, "src", "speedrankedlogo.png") : null
            };

            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string ResolveDiscordLogoPath()
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            var baseDirectory = AppContext.BaseDirectory;

            var candidates = new[]
            {
                assemblyDirectory != null ? Path.Combine(assemblyDirectory, "discord.png") : null,
                assemblyDirectory != null ? Path.Combine(assemblyDirectory, "src", "discord.png") : null,
                !string.IsNullOrWhiteSpace(baseDirectory) ? Path.Combine(baseDirectory, "discord.png") : null,
                !string.IsNullOrWhiteSpace(baseDirectory) ? Path.Combine(baseDirectory, "src", "discord.png") : null
            };

            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
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