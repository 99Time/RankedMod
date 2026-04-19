using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace schrader
{
    internal static class DiscordOnboardingUIRenderer
    {
        private const float PanelEntranceDurationSeconds = 0.24f;
        private const float ModalEntranceDurationSeconds = 0.18f;
        private const string VerificationPendingMessage = "Verification sent. Stay on this screen until the backend confirms your Discord link.";
        private const string ButtonLabelClass = "discord-onboarding-button-label";

        public sealed class View
        {
            public VisualElement Panel;
            public VisualElement Card;
            public VisualElement HeaderSection;
            public VisualElement StepsSection;
            public VisualElement NoteSection;
            public VisualElement ActionRow;
            public VisualElement VerificationModalBackdrop;
            public VisualElement VerificationModalCard;
            public TextField VerificationCodeInput;
            public Label VerificationStatusLabel;
            public Button LinkDiscordButton;
            public Button JoinDiscordButton;
            public Button LaterButton;
            public Button VerifyButton;
            public Button CloseVerificationButton;
        }

        public static void BuildUI(DraftUIRenderer.View rootView, Action<string> onVerifyCode, Action onJoinDiscord, Action onLeaveServer, Action onCloseVerification)
        {
            if (rootView?.Root == null)
            {
                return;
            }

            var existingPanel = rootView.Root.Q<VisualElement>("DiscordOnboardingPanel");
            while (existingPanel != null)
            {
                existingPanel.RemoveFromHierarchy();
                existingPanel = rootView.Root.Q<VisualElement>("DiscordOnboardingPanel");
            }

            var existingBackdrop = rootView.Root.Q<VisualElement>("DiscordOnboardingVerificationBackdrop");
            while (existingBackdrop != null)
            {
                existingBackdrop.RemoveFromHierarchy();
                existingBackdrop = rootView.Root.Q<VisualElement>("DiscordOnboardingVerificationBackdrop");
            }

            var view = new View();

            var panel = new VisualElement();
            panel.name = "DiscordOnboardingPanel";
            panel.style.display = DisplayStyle.None;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.alignSelf = Align.Center;
            panel.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            panel.style.maxWidth = 780;
            panel.style.paddingTop = new StyleLength(new Length(24, LengthUnit.Pixel));
            panel.style.paddingBottom = new StyleLength(new Length(22, LengthUnit.Pixel));
            panel.style.paddingLeft = new StyleLength(new Length(24, LengthUnit.Pixel));
            panel.style.paddingRight = new StyleLength(new Length(24, LengthUnit.Pixel));
            panel.style.backgroundColor = new StyleColor(new Color(0.07f, 0.10f, 0.15f, 0.94f));
            panel.style.borderTopWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderTopColor = new StyleColor(new Color(0.78f, 0.89f, 1f, 0.07f));
            panel.style.borderRightColor = new StyleColor(new Color(0.78f, 0.89f, 1f, 0.07f));
            panel.style.borderBottomColor = new StyleColor(new Color(0.78f, 0.89f, 1f, 0.07f));
            panel.style.borderLeftColor = new StyleColor(new Color(0.78f, 0.89f, 1f, 0.07f));
            SetCornerRadius(panel, 24);
            panel.style.opacity = 0f;
            panel.transform.scale = new Vector3(0.986f, 0.986f, 1f);
            panel.transform.position = new Vector3(0f, 12f, 0f);
            view.Panel = panel;
            view.Card = panel;

            var headerSection = new VisualElement();
            headerSection.style.display = DisplayStyle.Flex;
            headerSection.style.flexDirection = FlexDirection.Column;
            headerSection.style.alignItems = Align.Stretch;

            var eyebrow = CreateEyebrow("DISCORD VERIFICATION");
            var title = CreateLabel("Ranked access requires Discord verification", 30, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.98f, 0.99f, 1f, 1f));
            title.style.marginTop = new StyleLength(new Length(16, LengthUnit.Pixel));
            title.style.whiteSpace = WhiteSpace.Normal;

            var body = CreateLabel("Before team selection opens, connect the Discord account tied to your Steam profile. Once verified, you can continue straight into normal play.", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.81f, 0.88f, 0.94f, 0.95f));
            body.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            body.style.whiteSpace = WhiteSpace.Normal;

            headerSection.Add(eyebrow);
            headerSection.Add(title);
            headerSection.Add(body);
            view.HeaderSection = headerSection;

            view.LinkDiscordButton = new Button(() => ShowVerificationModal(view));
            StylePrimaryButton(view.LinkDiscordButton, new Color(0.23f, 0.60f, 0.40f, 1f), new Color(0.28f, 0.70f, 0.47f, 1f));
            view.LinkDiscordButton.style.minWidth = 154;
            view.LinkDiscordButton.style.height = 44;
            view.LinkDiscordButton.style.marginLeft = 0;
            view.LinkDiscordButton.style.marginRight = 0;
            view.LinkDiscordButton.style.marginBottom = 0;
            SetButtonText(view.LinkDiscordButton, "LINK DISCORD");

            var stepsSection = new VisualElement();
            stepsSection.style.display = DisplayStyle.Flex;
            stepsSection.style.flexDirection = FlexDirection.Column;
            stepsSection.style.marginTop = new StyleLength(new Length(22, LengthUnit.Pixel));
            stepsSection.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            stepsSection.Add(CreateGuidedStepItem("01", "Join the Discord server", "Open the server and follow the onboarding flow to request your personal verification code.", new Color(0.28f, 0.63f, 0.96f, 1f)));
            stepsSection.Add(CreateGuidedStepItem("02", "Receive your unique code", "Discord will generate a code specifically for your account. Keep it ready for the verification step.", new Color(0.43f, 0.76f, 0.98f, 1f)));
            stepsSection.Add(CreateGuidedStepItem("03", "Finish the link in game", "Open the verification panel here, paste your code, and press Verify to unlock ranked access.", new Color(0.23f, 0.64f, 0.48f, 1f), view.LinkDiscordButton));
            view.StepsSection = stepsSection;

            var noteSection = CreateNoteCard("If you prefer not to verify right now, choosing Leave Server will safely end the current session.");
            noteSection.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            view.NoteSection = noteSection;

            var buttonRow = new VisualElement();
            buttonRow.style.display = DisplayStyle.Flex;
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.Center;
            buttonRow.style.alignItems = Align.Center;
            buttonRow.style.flexWrap = Wrap.Wrap;
            buttonRow.style.marginTop = new StyleLength(new Length(22, LengthUnit.Pixel));
            view.ActionRow = buttonRow;

            view.JoinDiscordButton = new Button(() => onJoinDiscord?.Invoke());
            StylePrimaryButton(view.JoinDiscordButton, new Color(0.22f, 0.42f, 0.82f, 1f), new Color(0.28f, 0.50f, 0.92f, 1f));
            SetButtonText(view.JoinDiscordButton, "JOIN DISCORD");

            view.LaterButton = new Button(() => onLeaveServer?.Invoke());
            StylePrimaryButton(view.LaterButton, new Color(0.26f, 0.31f, 0.40f, 1f), new Color(0.31f, 0.37f, 0.47f, 1f));
            SetButtonText(view.LaterButton, "LEAVE SERVER");

            buttonRow.Add(view.JoinDiscordButton);
            buttonRow.Add(view.LaterButton);

            panel.Add(headerSection);
            panel.Add(stepsSection);
            panel.Add(noteSection);
            panel.Add(buttonRow);

            rootView.Root.Add(panel);
            view.VerificationModalBackdrop = BuildVerificationModal(view, onVerifyCode, onCloseVerification);
            rootView.Root.Add(view.VerificationModalBackdrop);
            rootView.DiscordOnboarding = view;
        }

        public static void Show(DraftUIRenderer.View rootView)
        {
            if (rootView?.DiscordOnboarding?.Panel == null)
            {
                return;
            }

            var wasAlreadyVisible = rootView.DiscordOnboarding.Panel.style.display != DisplayStyle.None
                && rootView.DiscordOnboarding.Panel.resolvedStyle.display != DisplayStyle.None;

            rootView.Container.style.display = DisplayStyle.Flex;
            if (rootView.VotingPanel != null) rootView.VotingPanel.style.display = DisplayStyle.None;
            if (rootView.ApprovalPanel != null) rootView.ApprovalPanel.style.display = DisplayStyle.None;
            if (rootView.DraftPanel != null) rootView.DraftPanel.style.display = DisplayStyle.None;
            if (rootView.Welcome?.Panel != null) rootView.Welcome.Panel.style.display = DisplayStyle.None;
            if (rootView.PostMatch?.Panel != null) rootView.PostMatch.Panel.style.display = DisplayStyle.None;
            if (rootView.DiscordOnboarding.VerificationModalBackdrop != null) rootView.DiscordOnboarding.VerificationModalBackdrop.style.display = DisplayStyle.None;
            rootView.DiscordOnboarding.Panel.style.display = DisplayStyle.Flex;

            if (wasAlreadyVisible)
            {
                CompleteVisibleState(rootView.DiscordOnboarding);
                return;
            }

            StartEntranceAnimation(rootView.DiscordOnboarding);
        }

        public static void CloseVerificationModal(DraftUIRenderer.View rootView)
        {
            HideVerificationModal(rootView?.DiscordOnboarding);
        }

        public static void HideOnboarding(DraftUIRenderer.View rootView)
        {
            HideOnboarding(rootView?.DiscordOnboarding);
        }

        public static bool IsOnboardingVisible(DraftUIRenderer.View rootView)
        {
            return rootView?.DiscordOnboarding?.Panel != null
                && rootView.DiscordOnboarding.Panel.style.display == DisplayStyle.Flex;
        }

        public static bool IsVerificationModalOpen(DraftUIRenderer.View rootView)
        {
            return rootView?.DiscordOnboarding?.VerificationModalBackdrop != null
                && rootView.DiscordOnboarding.VerificationModalBackdrop.style.display == DisplayStyle.Flex;
        }

        private static VisualElement BuildVerificationModal(View view, Action<string> onVerifyCode, Action onCloseVerification)
        {
            var backdrop = new VisualElement();
            backdrop.name = "DiscordOnboardingVerificationBackdrop";
            backdrop.style.display = DisplayStyle.None;
            backdrop.style.position = Position.Absolute;
            backdrop.style.top = 0;
            backdrop.style.left = 0;
            backdrop.style.right = 0;
            backdrop.style.bottom = 0;
            backdrop.style.justifyContent = Justify.Center;
            backdrop.style.alignItems = Align.Center;
            backdrop.style.paddingLeft = new StyleLength(new Length(18, LengthUnit.Pixel));
            backdrop.style.paddingRight = new StyleLength(new Length(18, LengthUnit.Pixel));
            backdrop.style.backgroundColor = new StyleColor(new Color(0.02f, 0.04f, 0.08f, 0.72f));

            var modalShell = new VisualElement();
            modalShell.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            modalShell.style.maxWidth = 560;
            modalShell.style.paddingTop = new StyleLength(new Length(16, LengthUnit.Pixel));
            modalShell.style.paddingBottom = new StyleLength(new Length(16, LengthUnit.Pixel));
            modalShell.style.paddingLeft = new StyleLength(new Length(16, LengthUnit.Pixel));
            modalShell.style.paddingRight = new StyleLength(new Length(16, LengthUnit.Pixel));
            StyleSurface(modalShell, new Color(0.05f, 0.08f, 0.12f, 0.80f), 26, new Color(0.78f, 0.89f, 1f, 0.04f));

            var modalCard = new VisualElement();
            modalCard.style.display = DisplayStyle.Flex;
            modalCard.style.flexDirection = FlexDirection.Column;
            modalCard.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            modalCard.style.paddingTop = new StyleLength(new Length(24, LengthUnit.Pixel));
            modalCard.style.paddingBottom = new StyleLength(new Length(22, LengthUnit.Pixel));
            modalCard.style.paddingLeft = new StyleLength(new Length(24, LengthUnit.Pixel));
            modalCard.style.paddingRight = new StyleLength(new Length(24, LengthUnit.Pixel));
            StyleSurface(modalCard, new Color(0.10f, 0.13f, 0.18f, 0.98f), 22, new Color(0.76f, 0.88f, 1f, 0.08f));
            view.VerificationModalCard = modalCard;

            var eyebrow = CreateEyebrow("CODE VERIFICATION");

            var title = CreateLabel("Complete your Discord link", 25, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.98f, 0.99f, 1f, 1f));
            title.style.marginTop = new StyleLength(new Length(16, LengthUnit.Pixel));

            var body = CreateLabel("Paste the code you received in Discord below and press Verify to unlock ranked access on this server.", 13, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.83f, 0.89f, 0.95f, 0.95f));
            body.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            body.style.whiteSpace = WhiteSpace.Normal;

            var inputLabel = CreateMutedLabel("Verification code", 12, FontStyle.Bold, new Color(0.91f, 0.96f, 1f, 0.95f));
            inputLabel.style.marginTop = new StyleLength(new Length(18, LengthUnit.Pixel));

            view.VerificationCodeInput = new TextField();
            view.VerificationCodeInput.label = string.Empty;
            view.VerificationCodeInput.value = string.Empty;
            view.VerificationCodeInput.style.marginTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            view.VerificationCodeInput.style.height = 48;
            view.VerificationCodeInput.style.paddingLeft = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.VerificationCodeInput.style.paddingRight = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.VerificationCodeInput.style.backgroundColor = new StyleColor(new Color(0.06f, 0.09f, 0.13f, 0.98f));
            view.VerificationCodeInput.style.color = new StyleColor(new Color(0.97f, 0.98f, 1f, 1f));
            view.VerificationCodeInput.style.borderTopWidth = 1;
            view.VerificationCodeInput.style.borderRightWidth = 1;
            view.VerificationCodeInput.style.borderBottomWidth = 1;
            view.VerificationCodeInput.style.borderLeftWidth = 1;
            view.VerificationCodeInput.style.borderTopColor = new StyleColor(new Color(0.80f, 0.90f, 1f, 0.10f));
            view.VerificationCodeInput.style.borderRightColor = new StyleColor(new Color(0.80f, 0.90f, 1f, 0.10f));
            view.VerificationCodeInput.style.borderBottomColor = new StyleColor(new Color(0.80f, 0.90f, 1f, 0.10f));
            view.VerificationCodeInput.style.borderLeftColor = new StyleColor(new Color(0.80f, 0.90f, 1f, 0.10f));
            SetCornerRadius(view.VerificationCodeInput, 14);

            view.VerificationStatusLabel = CreateLabel(string.Empty, 12, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.98f, 0.86f, 0.56f, 0.96f));
            view.VerificationStatusLabel.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));
            view.VerificationStatusLabel.style.whiteSpace = WhiteSpace.Normal;
            view.VerificationStatusLabel.style.display = DisplayStyle.None;

            var buttonRow = new VisualElement();
            buttonRow.style.display = DisplayStyle.Flex;
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            buttonRow.style.alignItems = Align.Center;
            buttonRow.style.flexWrap = Wrap.Wrap;
            buttonRow.style.marginTop = new StyleLength(new Length(18, LengthUnit.Pixel));

            view.CloseVerificationButton = new Button(() => onCloseVerification?.Invoke());
            StylePrimaryButton(view.CloseVerificationButton, new Color(0.26f, 0.31f, 0.40f, 1f), new Color(0.31f, 0.37f, 0.47f, 1f));
            view.CloseVerificationButton.style.minWidth = 150;
            SetButtonText(view.CloseVerificationButton, "LEAVE SERVER");

            view.VerifyButton = new Button(() => SubmitVerificationCode(view, onVerifyCode));
            StylePrimaryButton(view.VerifyButton, new Color(0.23f, 0.60f, 0.40f, 1f), new Color(0.28f, 0.70f, 0.47f, 1f));
            view.VerifyButton.style.minWidth = 166;
            SetButtonText(view.VerifyButton, "VERIFY");

            buttonRow.Add(view.CloseVerificationButton);
            buttonRow.Add(view.VerifyButton);

            modalCard.Add(eyebrow);
            modalCard.Add(title);
            modalCard.Add(body);
            modalCard.Add(inputLabel);
            modalCard.Add(view.VerificationCodeInput);
            modalCard.Add(view.VerificationStatusLabel);
            modalCard.Add(buttonRow);
            modalShell.Add(modalCard);
            backdrop.Add(modalShell);
            return backdrop;
        }

        private static void ShowVerificationModal(View view)
        {
            if (view?.VerificationModalBackdrop == null || view.VerificationCodeInput == null)
            {
                return;
            }

            view.VerificationCodeInput.value = string.Empty;
            view.VerificationStatusLabel.text = string.Empty;
            view.VerificationStatusLabel.style.display = DisplayStyle.None;
            view.VerificationModalBackdrop.style.display = DisplayStyle.Flex;
            view.VerificationModalBackdrop.BringToFront();
            StartModalEntranceAnimation(view);
            view.VerificationCodeInput.Focus();
        }

        private static void HideVerificationModal(View view)
        {
            if (view?.VerificationModalBackdrop == null)
            {
                return;
            }

            view.VerificationModalBackdrop.style.display = DisplayStyle.None;
        }

        private static void HideOnboarding(View view)
        {
            if (view == null)
            {
                return;
            }

            HideVerificationModal(view);
            if (view.VerificationCodeInput != null)
            {
                view.VerificationCodeInput.value = string.Empty;
            }

            if (view.VerificationStatusLabel != null)
            {
                view.VerificationStatusLabel.text = string.Empty;
                view.VerificationStatusLabel.style.display = DisplayStyle.None;
            }

            if (view.Panel != null)
            {
                view.Panel.style.display = DisplayStyle.None;
            }
        }

        private static void SubmitVerificationCode(View view, Action<string> onVerifyCode)
        {
            if (view?.VerificationCodeInput == null || view.VerificationStatusLabel == null)
            {
                return;
            }

            var code = string.IsNullOrWhiteSpace(view.VerificationCodeInput.value)
                ? string.Empty
                : view.VerificationCodeInput.value.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                view.VerificationStatusLabel.text = "Paste the verification code from Discord before pressing Verify.";
                view.VerificationStatusLabel.style.display = DisplayStyle.Flex;
                return;
            }

            onVerifyCode?.Invoke(code);
            view.VerificationStatusLabel.text = VerificationPendingMessage;
            view.VerificationStatusLabel.style.display = DisplayStyle.Flex;
        }

        private static VisualElement CreateEyebrow(string text)
        {
            var chip = new VisualElement();
            chip.style.alignSelf = Align.FlexStart;
            chip.style.paddingTop = new StyleLength(new Length(5, LengthUnit.Pixel));
            chip.style.paddingBottom = new StyleLength(new Length(5, LengthUnit.Pixel));
            chip.style.paddingLeft = new StyleLength(new Length(12, LengthUnit.Pixel));
            chip.style.paddingRight = new StyleLength(new Length(12, LengthUnit.Pixel));
            chip.style.backgroundColor = new StyleColor(new Color(0.17f, 0.25f, 0.36f, 0.54f));
            SetCornerRadius(chip, 999);
            chip.Add(CreateMutedLabel(text, 11, FontStyle.Bold, new Color(0.90f, 0.95f, 1f, 0.90f)));
            return chip;
        }

        private static VisualElement CreateGuidedStepItem(string number, string title, string description, Color accentColor, VisualElement actionElement = null)
        {
            var row = new VisualElement();
            row.style.display = DisplayStyle.Flex;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = new StyleLength(new Length(10, LengthUnit.Pixel));
            row.style.paddingTop = new StyleLength(new Length(16, LengthUnit.Pixel));
            row.style.paddingBottom = new StyleLength(new Length(16, LengthUnit.Pixel));
            row.style.paddingLeft = new StyleLength(new Length(16, LengthUnit.Pixel));
            row.style.paddingRight = new StyleLength(new Length(16, LengthUnit.Pixel));
            StyleSurface(row, new Color(0.11f, 0.14f, 0.20f, 0.72f), 18, new Color(0.78f, 0.89f, 1f, 0.04f));

            var badge = new VisualElement();
            badge.style.width = 34;
            badge.style.height = 34;
            badge.style.flexShrink = 0;
            badge.style.marginRight = new StyleLength(new Length(14, LengthUnit.Pixel));
            badge.style.alignItems = Align.Center;
            badge.style.justifyContent = Justify.Center;
            badge.style.backgroundColor = new StyleColor(new Color(accentColor.r, accentColor.g, accentColor.b, 0.18f));
            SetCornerRadius(badge, 999);
            badge.Add(CreateLabel(number, 11, FontStyle.Bold, TextAnchor.MiddleCenter, accentColor));

            var textColumn = new VisualElement();
            textColumn.style.display = DisplayStyle.Flex;
            textColumn.style.flexDirection = FlexDirection.Column;
            textColumn.style.flexGrow = 1;
            textColumn.style.minWidth = 0;

            var titleLabel = CreateLabel(title, 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.96f, 0.98f, 1f, 1f));
            titleLabel.style.whiteSpace = WhiteSpace.Normal;

            var bodyLabel = CreateLabel(description, 13, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.79f, 0.86f, 0.93f, 0.92f));
            bodyLabel.style.marginTop = new StyleLength(new Length(4, LengthUnit.Pixel));
            bodyLabel.style.whiteSpace = WhiteSpace.Normal;

            textColumn.Add(titleLabel);
            textColumn.Add(bodyLabel);

            if (actionElement != null)
            {
                actionElement.style.marginTop = new StyleLength(new Length(12, LengthUnit.Pixel));
                textColumn.Add(actionElement);
            }

            row.Add(badge);
            row.Add(textColumn);
            return row;
        }

        private static VisualElement CreateCommandPill(string command)
        {
            var pill = new VisualElement();
            pill.style.display = DisplayStyle.Flex;
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.alignItems = Align.Center;
            pill.style.alignSelf = Align.FlexStart;
            pill.style.paddingTop = new StyleLength(new Length(6, LengthUnit.Pixel));
            pill.style.paddingBottom = new StyleLength(new Length(6, LengthUnit.Pixel));
            pill.style.paddingLeft = new StyleLength(new Length(10, LengthUnit.Pixel));
            pill.style.paddingRight = new StyleLength(new Length(10, LengthUnit.Pixel));
            pill.style.backgroundColor = new StyleColor(new Color(0.13f, 0.20f, 0.30f, 0.88f));
            SetCornerRadius(pill, 999);

            var commandLabel = CreateLabel(command, 12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.57f, 0.86f, 1f, 1f));
            commandLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            pill.Add(commandLabel);
            return pill;
        }

        private static VisualElement CreateNoteCard(string text)
        {
            var note = new VisualElement();
            note.style.display = DisplayStyle.Flex;
            note.style.flexDirection = FlexDirection.Row;
            note.style.alignItems = Align.Center;
            note.style.paddingTop = new StyleLength(new Length(12, LengthUnit.Pixel));
            note.style.paddingBottom = new StyleLength(new Length(12, LengthUnit.Pixel));
            note.style.paddingLeft = new StyleLength(new Length(14, LengthUnit.Pixel));
            note.style.paddingRight = new StyleLength(new Length(14, LengthUnit.Pixel));
            StyleSurface(note, new Color(0.10f, 0.13f, 0.18f, 0.62f), 16, new Color(0.78f, 0.89f, 1f, 0.03f));

            var indicator = new VisualElement();
            indicator.style.width = 7;
            indicator.style.height = 7;
            indicator.style.flexShrink = 0;
            indicator.style.marginRight = new StyleLength(new Length(10, LengthUnit.Pixel));
            indicator.style.backgroundColor = new StyleColor(new Color(0.48f, 0.78f, 0.96f, 0.95f));
            SetCornerRadius(indicator, 999);

            var label = CreateLabel(text, 12, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.78f, 0.85f, 0.92f, 0.90f));
            label.style.flexGrow = 1;
            label.style.whiteSpace = WhiteSpace.Normal;

            note.Add(indicator);
            note.Add(label);
            return note;
        }

        private static Label CreateMutedLabel(string text, int fontSize, FontStyle fontStyle, Color color)
        {
            return CreateLabel(text, fontSize, fontStyle, TextAnchor.MiddleLeft, color);
        }

        private static Label CreateLabel(string text, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
        {
            var label = new Label(text);
            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = fontStyle;
            label.style.unityTextAlign = alignment;
            label.style.color = new StyleColor(color);
            return label;
        }

        private static void StylePrimaryButton(Button button, Color baseColor, Color hoverColor)
        {
            button.style.height = 52;
            button.style.minWidth = 174;
            button.style.marginLeft = new StyleLength(new Length(6, LengthUnit.Pixel));
            button.style.marginRight = new StyleLength(new Length(6, LengthUnit.Pixel));
            button.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            button.style.paddingTop = new StyleLength(new Length(0, LengthUnit.Pixel));
            button.style.paddingBottom = new StyleLength(new Length(0, LengthUnit.Pixel));
            button.style.paddingLeft = new StyleLength(new Length(20, LengthUnit.Pixel));
            button.style.paddingRight = new StyleLength(new Length(20, LengthUnit.Pixel));
            button.style.backgroundColor = new StyleColor(baseColor);
            button.style.borderTopWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            button.style.color = new StyleColor(Color.white);
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.fontSize = 13;
            button.style.justifyContent = Justify.Center;
            button.style.alignItems = Align.Center;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            SetCornerRadius(button, 16);

            button.RegisterCallback<PointerEnterEvent>(_ => button.style.backgroundColor = new StyleColor(hoverColor));
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = new StyleColor(baseColor));
        }

        private static void SetButtonText(Button button, string text)
        {
            if (button == null)
            {
                return;
            }

            button.text = string.Empty;
            button.Clear();

            var label = CreateLabel(text, 13, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            label.name = ButtonLabelClass;
            label.style.flexGrow = 1;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.alignSelf = Align.Center;
            label.style.marginLeft = 0;
            label.style.marginRight = 0;
            label.style.marginTop = 0;
            label.style.marginBottom = 0;
            label.style.color = new StyleColor(Color.white);
            label.pickingMode = PickingMode.Ignore;
            button.Add(label);
        }

        private static void StartEntranceAnimation(View view)
        {
            if (view?.Panel == null)
            {
                return;
            }

            view.Panel.style.opacity = 0f;
            view.Panel.transform.scale = new Vector3(0.986f, 0.986f, 1f);
            view.Panel.transform.position = new Vector3(0f, 12f, 0f);

            var startedAt = Time.unscaledTime;
            view.Panel.schedule.Execute(() =>
            {
                if (view.Panel == null || view.Panel.style.display == DisplayStyle.None)
                {
                    return;
                }

                var progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / PanelEntranceDurationSeconds);
                var eased = EaseOutCubic(progress);
                view.Panel.style.opacity = eased;
                view.Panel.transform.scale = Vector3.LerpUnclamped(new Vector3(0.986f, 0.986f, 1f), Vector3.one, eased);
                view.Panel.transform.position = Vector3.LerpUnclamped(new Vector3(0f, 12f, 0f), Vector3.zero, eased);

                if (progress >= 1f)
                {
                    CompleteVisibleState(view);
                }
            }).Every(16);
        }

        private static void StartModalEntranceAnimation(View view)
        {
            if (view?.VerificationModalBackdrop == null || view.VerificationModalCard == null)
            {
                return;
            }

            view.VerificationModalBackdrop.style.opacity = 0f;
            view.VerificationModalCard.style.opacity = 0f;
            view.VerificationModalCard.transform.scale = new Vector3(0.988f, 0.988f, 1f);
            view.VerificationModalCard.transform.position = new Vector3(0f, 10f, 0f);

            var startedAt = Time.unscaledTime;
            view.VerificationModalBackdrop.schedule.Execute(() =>
            {
                if (view.VerificationModalBackdrop == null || view.VerificationModalBackdrop.style.display == DisplayStyle.None)
                {
                    return;
                }

                var progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / ModalEntranceDurationSeconds);
                var eased = EaseOutCubic(progress);
                view.VerificationModalBackdrop.style.opacity = eased;
                view.VerificationModalCard.style.opacity = eased;
                view.VerificationModalCard.transform.scale = Vector3.LerpUnclamped(new Vector3(0.988f, 0.988f, 1f), Vector3.one, eased);
                view.VerificationModalCard.transform.position = Vector3.LerpUnclamped(new Vector3(0f, 10f, 0f), Vector3.zero, eased);

                if (progress >= 1f)
                {
                    view.VerificationModalBackdrop.style.opacity = 1f;
                    view.VerificationModalCard.style.opacity = 1f;
                    view.VerificationModalCard.transform.scale = Vector3.one;
                    view.VerificationModalCard.transform.position = Vector3.zero;
                }
            }).Every(16);
        }

        private static void CompleteVisibleState(View view)
        {
            if (view?.Panel == null)
            {
                return;
            }

            view.Panel.style.opacity = 1f;
            view.Panel.transform.scale = Vector3.one;
            view.Panel.transform.position = Vector3.zero;
        }

        private static float EaseOutCubic(float value)
        {
            var clamped = Mathf.Clamp01(value);
            return 1f - Mathf.Pow(1f - clamped, 3f);
        }

        private static void StyleSurface(VisualElement element, Color backgroundColor, int radius, Color borderColor)
        {
            element.style.backgroundColor = new StyleColor(backgroundColor);
            element.style.borderTopWidth = 1;
            element.style.borderRightWidth = 1;
            element.style.borderBottomWidth = 1;
            element.style.borderLeftWidth = 1;
            element.style.borderTopColor = new StyleColor(borderColor);
            element.style.borderRightColor = new StyleColor(borderColor);
            element.style.borderBottomColor = new StyleColor(borderColor);
            element.style.borderLeftColor = new StyleColor(borderColor);
            SetCornerRadius(element, radius);
        }

        private static void SetCornerRadius(VisualElement element, int radius)
        {
            element.style.borderTopLeftRadius = new StyleLength(new Length(radius, LengthUnit.Pixel));
            element.style.borderTopRightRadius = new StyleLength(new Length(radius, LengthUnit.Pixel));
            element.style.borderBottomLeftRadius = new StyleLength(new Length(radius, LengthUnit.Pixel));
            element.style.borderBottomRightRadius = new StyleLength(new Length(radius, LengthUnit.Pixel));
        }
    }
}