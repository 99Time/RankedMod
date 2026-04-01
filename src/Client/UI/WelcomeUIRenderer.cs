using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace schrader
{
    internal static class WelcomeUIRenderer
    {
        public sealed class View
        {
            public VisualElement Panel;
            public Button DiscordButton;
            public Button ContinueButton;
        }

        private static Texture2D cachedLogoTexture;
        private static bool logoLoadAttempted;

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
            panel.style.maxWidth = 860;
            panel.style.paddingTop = new StyleLength(new Length(4, LengthUnit.Pixel));
            panel.style.paddingBottom = new StyleLength(new Length(6, LengthUnit.Pixel));

            var hero = new VisualElement();
            hero.style.display = DisplayStyle.Flex;
            hero.style.flexDirection = FlexDirection.Column;
            hero.style.alignItems = Align.Center;
            hero.style.marginBottom = new StyleLength(new Length(16, LengthUnit.Pixel));

            var logoFrame = new VisualElement();
            logoFrame.style.display = DisplayStyle.Flex;
            logoFrame.style.justifyContent = Justify.Center;
            logoFrame.style.alignItems = Align.Center;
            logoFrame.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            logoFrame.style.maxWidth = 360;
            logoFrame.style.minHeight = 114;
            logoFrame.style.paddingTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            logoFrame.style.paddingBottom = new StyleLength(new Length(10, LengthUnit.Pixel));
            logoFrame.style.paddingLeft = new StyleLength(new Length(16, LengthUnit.Pixel));
            logoFrame.style.paddingRight = new StyleLength(new Length(16, LengthUnit.Pixel));
            logoFrame.style.backgroundColor = new StyleColor(new Color(0.10f, 0.16f, 0.22f, 0.56f));
            logoFrame.style.borderTopLeftRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            logoFrame.style.borderTopRightRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            logoFrame.style.borderBottomLeftRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            logoFrame.style.borderBottomRightRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            logoFrame.style.borderTopWidth = 1;
            logoFrame.style.borderRightWidth = 1;
            logoFrame.style.borderBottomWidth = 1;
            logoFrame.style.borderLeftWidth = 1;
            logoFrame.style.borderTopColor = new StyleColor(new Color(0.74f, 0.86f, 0.96f, 0.20f));
            logoFrame.style.borderRightColor = new StyleColor(new Color(0.74f, 0.86f, 0.96f, 0.10f));
            logoFrame.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.26f));
            logoFrame.style.borderLeftColor = new StyleColor(new Color(0.74f, 0.86f, 0.96f, 0.10f));

            var logoTexture = TryLoadLogoTexture();
            if (logoTexture != null)
            {
                var logo = new Image();
                logo.name = "SpeedRankedLogo";
                logo.image = logoTexture;
                logo.scaleMode = ScaleMode.ScaleToFit;
                logo.style.width = new StyleLength(new Length(336, LengthUnit.Pixel));
                logo.style.maxWidth = new StyleLength(new Length(100, LengthUnit.Percent));
                logo.style.height = new StyleLength(new Length(94, LengthUnit.Pixel));
                logoFrame.Add(logo);
            }

            var titleLabel = CreateLabel("Welcome to SpeedRankeds", 30, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.98f, 0.99f, 1f, 1f));
            titleLabel.style.marginTop = new StyleLength(new Length(14, LengthUnit.Pixel));

            var subtitleLabel = CreateLabel("Read the rules, then continue to choose Spectator, Red, or Blue.", 14, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.82f, 0.89f, 0.95f, 1f));
            subtitleLabel.style.marginTop = new StyleLength(new Length(8, LengthUnit.Pixel));

            hero.Add(logoFrame);
            hero.Add(titleLabel);
            hero.Add(subtitleLabel);

            var rulesSection = CreateSectionPanel("RULES");
            AddRuleRow(rulesSection, "No racism");
            AddRuleRow(rulesSection, "No insults or harassment");
            AddRuleRow(rulesSection, "No mocking other players");
            AddRuleRow(rulesSection, "Play to have fun");

            var commandsSection = CreateSectionPanel("COMMANDS OVERVIEW");
            commandsSection.style.marginTop = new StyleLength(new Length(16, LengthUnit.Pixel));
            var commandsHintLabel = CreateLabel("To see all commands, use /commands", 13, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.82f, 0.89f, 0.95f, 1f));
            commandsHintLabel.style.marginBottom = new StyleLength(new Length(10, LengthUnit.Pixel));
            commandsSection.Add(commandsHintLabel);
            AddCommandRow(commandsSection, "/vr", "Start a Ranked vote (use /y or /n).");
            AddCommandRow(commandsSection, "/discord", "Open the Discord invite in your browser.");
            AddCommandRow(commandsSection, "/s", "Spawn a puck.");
            AddCommandRow(commandsSection, "/cs", "Clear all pucks.");
            AddCommandRow(commandsSection, "/mmr", "Show your current MMR.");

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
            buttonRow.style.marginTop = new StyleLength(new Length(24, LengthUnit.Pixel));
            buttonRow.style.paddingLeft = 0;
            buttonRow.style.paddingRight = 0;

            view.DiscordButton = new Button(() => onOpenDiscord?.Invoke())
            {
                text = "DISCORD"
            };
            StyleButton(view.DiscordButton, new ButtonPalette(
                new Color(0.16f, 0.33f, 0.70f, 0.98f),
                new Color(0.23f, 0.42f, 0.84f, 1f),
                new Color(0.11f, 0.25f, 0.56f, 1f),
                Color.white));
            view.DiscordButton.style.minWidth = 180;
            view.DiscordButton.style.height = 44;
            view.DiscordButton.style.alignSelf = Align.Center;
            view.DiscordButton.style.flexShrink = 0;
            view.DiscordButton.style.marginRight = new StyleLength(new Length(14, LengthUnit.Pixel));

            view.ContinueButton = new Button(() => onContinue?.Invoke())
            {
                text = "CONTINUE"
            };
            StyleButton(view.ContinueButton, new ButtonPalette(
                new Color(0.16f, 0.53f, 0.36f, 0.98f),
                new Color(0.23f, 0.66f, 0.44f, 1f),
                new Color(0.12f, 0.42f, 0.28f, 1f),
                Color.white));
            view.ContinueButton.style.minWidth = 200;
            view.ContinueButton.style.height = 44;
            view.ContinueButton.style.alignSelf = Align.Center;
            view.ContinueButton.style.flexShrink = 0;

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

            rootView.Container.style.display = DisplayStyle.Flex;
            if (rootView.VotingPanel != null) rootView.VotingPanel.style.display = DisplayStyle.None;
            if (rootView.ApprovalPanel != null) rootView.ApprovalPanel.style.display = DisplayStyle.None;
            if (rootView.DraftPanel != null) rootView.DraftPanel.style.display = DisplayStyle.None;
            if (rootView.PostMatch?.Panel != null) rootView.PostMatch.Panel.style.display = DisplayStyle.None;
            rootView.Welcome.Panel.style.display = DisplayStyle.Flex;
        }

        private static VisualElement CreateSectionPanel(string title)
        {
            var section = new VisualElement();
            section.style.display = DisplayStyle.Flex;
            section.style.flexDirection = FlexDirection.Column;
            section.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            section.style.paddingTop = new StyleLength(new Length(16, LengthUnit.Pixel));
            section.style.paddingBottom = new StyleLength(new Length(16, LengthUnit.Pixel));
            section.style.paddingLeft = new StyleLength(new Length(18, LengthUnit.Pixel));
            section.style.paddingRight = new StyleLength(new Length(18, LengthUnit.Pixel));
            section.style.backgroundColor = new StyleColor(new Color(0.09f, 0.13f, 0.18f, 0.86f));
            section.style.borderTopLeftRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            section.style.borderTopRightRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            section.style.borderBottomLeftRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            section.style.borderBottomRightRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            section.style.borderTopWidth = 1;
            section.style.borderRightWidth = 1;
            section.style.borderBottomWidth = 1;
            section.style.borderLeftWidth = 1;
            section.style.borderTopColor = new StyleColor(new Color(0.62f, 0.78f, 0.90f, 0.18f));
            section.style.borderRightColor = new StyleColor(new Color(0.62f, 0.78f, 0.90f, 0.08f));
            section.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.30f));
            section.style.borderLeftColor = new StyleColor(new Color(0.62f, 0.78f, 0.90f, 0.08f));

            var chip = new VisualElement();
            chip.style.alignSelf = Align.FlexStart;
            chip.style.paddingTop = new StyleLength(new Length(6, LengthUnit.Pixel));
            chip.style.paddingBottom = new StyleLength(new Length(6, LengthUnit.Pixel));
            chip.style.paddingLeft = new StyleLength(new Length(14, LengthUnit.Pixel));
            chip.style.paddingRight = new StyleLength(new Length(14, LengthUnit.Pixel));
            chip.style.marginBottom = new StyleLength(new Length(12, LengthUnit.Pixel));
            chip.style.backgroundColor = new StyleColor(new Color(0.18f, 0.30f, 0.40f, 0.54f));
            chip.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            chip.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            chip.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            chip.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            chip.Add(CreateLabel(title, 12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.90f, 0.95f, 0.99f, 1f)));

            section.Add(chip);
            return section;
        }

        private static void AddRuleRow(VisualElement parent, string text)
        {
            var row = new VisualElement();
            row.style.display = DisplayStyle.Flex;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            row.style.paddingTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            row.style.paddingBottom = new StyleLength(new Length(10, LengthUnit.Pixel));
            row.style.paddingLeft = new StyleLength(new Length(12, LengthUnit.Pixel));
            row.style.paddingRight = new StyleLength(new Length(12, LengthUnit.Pixel));
            row.style.backgroundColor = new StyleColor(new Color(0.12f, 0.17f, 0.23f, 0.82f));
            row.style.borderTopLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            row.style.borderTopRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            row.style.borderBottomLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            row.style.borderBottomRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));

            var accent = new VisualElement();
            accent.style.width = 10;
            accent.style.height = 10;
            accent.style.marginRight = new StyleLength(new Length(12, LengthUnit.Pixel));
            accent.style.backgroundColor = new StyleColor(new Color(0.44f, 0.80f, 0.62f, 1f));
            accent.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            accent.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            accent.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            accent.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));

            var label = CreateLabel(text, 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.94f, 0.97f, 1f, 1f));

            row.Add(accent);
            row.Add(label);
            parent.Add(row);
        }

        private static void AddCommandRow(VisualElement parent, string command, string description)
        {
            var row = new VisualElement();
            row.style.display = DisplayStyle.Flex;
            row.style.flexDirection = FlexDirection.Column;
            row.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            row.style.paddingTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            row.style.paddingBottom = new StyleLength(new Length(10, LengthUnit.Pixel));
            row.style.paddingLeft = new StyleLength(new Length(12, LengthUnit.Pixel));
            row.style.paddingRight = new StyleLength(new Length(12, LengthUnit.Pixel));
            row.style.backgroundColor = new StyleColor(new Color(0.12f, 0.17f, 0.23f, 0.82f));
            row.style.borderTopLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            row.style.borderTopRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            row.style.borderBottomLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            row.style.borderBottomRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));

            var commandLabel = CreateLabel(command, 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.98f, 0.90f, 0.62f, 1f));
            var descriptionLabel = CreateLabel(description, 12, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.82f, 0.89f, 0.95f, 1f));
            descriptionLabel.style.marginTop = new StyleLength(new Length(4, LengthUnit.Pixel));

            row.Add(commandLabel);
            row.Add(descriptionLabel);
            parent.Add(row);
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
            button.style.backgroundColor = new StyleColor(palette.Normal);
            button.style.color = new StyleColor(palette.Text);
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.borderTopLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            button.style.borderTopRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            button.style.borderBottomLeftRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            button.style.borderBottomRightRadius = new StyleLength(new Length(16, LengthUnit.Pixel));
            button.style.borderTopWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.18f));
            button.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));
            button.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.24f));
            button.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));

            button.RegisterCallback<MouseEnterEvent>(_ => button.style.backgroundColor = new StyleColor(palette.Hover));
            button.RegisterCallback<MouseLeaveEvent>(_ => button.style.backgroundColor = new StyleColor(palette.Normal));
            button.RegisterCallback<MouseDownEvent>(_ => button.style.backgroundColor = new StyleColor(palette.Pressed));
            button.RegisterCallback<MouseUpEvent>(_ => button.style.backgroundColor = new StyleColor(palette.Hover));
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