using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace schrader
{
    internal static class DiscordOnboardingUIRenderer
    {
        private const float PanelEntranceDurationSeconds = 0.2f;
        private const string VerificationPendingMessage = "Verification sent. Stay on this screen until the backend confirms your Discord link.";
        private const string ButtonLabelClass = "discord-onboarding-button-label";

        public sealed class View
        {
            public VisualElement Panel;
            public VisualElement Card;
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

            var view = new View();
            var panel = new VisualElement();
            panel.name = "DiscordOnboardingPanel";
            panel.style.display = DisplayStyle.None;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.alignItems = Align.Center;
            panel.style.justifyContent = Justify.Center;
            panel.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            panel.style.paddingTop = new StyleLength(new Length(16, LengthUnit.Pixel));
            panel.style.paddingBottom = new StyleLength(new Length(16, LengthUnit.Pixel));
            panel.style.opacity = 0f;
            panel.style.position = Position.Relative;
            panel.transform.scale = new Vector3(0.985f, 0.985f, 1f);
            panel.transform.position = new Vector3(0f, 10f, 0f);

            var card = new VisualElement();
            card.style.display = DisplayStyle.Flex;
            card.style.flexDirection = FlexDirection.Column;
            card.style.alignItems = Align.Stretch;
            card.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            card.style.maxWidth = 760;
            card.style.paddingTop = new StyleLength(new Length(22, LengthUnit.Pixel));
            card.style.paddingBottom = new StyleLength(new Length(22, LengthUnit.Pixel));
            card.style.paddingLeft = new StyleLength(new Length(22, LengthUnit.Pixel));
            card.style.paddingRight = new StyleLength(new Length(22, LengthUnit.Pixel));
            card.style.backgroundColor = new StyleColor(new Color(0.07f, 0.10f, 0.15f, 0.92f));
            card.style.borderTopLeftRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            card.style.borderTopRightRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            card.style.borderBottomLeftRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            card.style.borderBottomRightRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            card.style.borderTopWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderTopColor = new StyleColor(new Color(0.72f, 0.84f, 0.96f, 0.16f));
            card.style.borderRightColor = new StyleColor(new Color(0.72f, 0.84f, 0.96f, 0.08f));
            card.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.28f));
            card.style.borderLeftColor = new StyleColor(new Color(0.72f, 0.84f, 0.96f, 0.08f));
            card.style.unityTextAlign = TextAnchor.MiddleCenter;
            view.Card = card;

            var chip = new VisualElement();
            chip.style.alignSelf = Align.Center;
            chip.style.paddingTop = new StyleLength(new Length(4, LengthUnit.Pixel));
            chip.style.paddingBottom = new StyleLength(new Length(4, LengthUnit.Pixel));
            chip.style.paddingLeft = new StyleLength(new Length(12, LengthUnit.Pixel));
            chip.style.paddingRight = new StyleLength(new Length(12, LengthUnit.Pixel));
            chip.style.backgroundColor = new StyleColor(new Color(0.18f, 0.30f, 0.44f, 0.46f));
            chip.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            chip.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            chip.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            chip.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            chip.Add(CreateLabel("DISCORD ONBOARDING", 11, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.91f, 0.96f, 1f, 0.94f)));

            var title = CreateLabel("Discord verification is required to play", 28, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.98f, 0.99f, 1f, 1f));
            title.style.marginTop = new StyleLength(new Length(16, LengthUnit.Pixel));

            var body = CreateLabel("This server now requires a verified Discord to Steam link before you can join a team or continue into normal play.", 14, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.82f, 0.89f, 0.95f, 0.94f));
            body.style.marginTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            body.style.whiteSpace = WhiteSpace.Normal;

            var list = new VisualElement();
            list.style.display = DisplayStyle.Flex;
            list.style.flexDirection = FlexDirection.Column;
            list.style.marginTop = new StyleLength(new Length(18, LengthUnit.Pixel));
            list.style.marginBottom = new StyleLength(new Length(18, LengthUnit.Pixel));
            list.Add(CreateBenefitRow("Join the Discord server and request your verification code"));
            list.Add(CreateBenefitRow("Use /link CODE in-game to complete the required verification"));
            list.Add(CreateBenefitRow("Leaving or closing verification will disconnect you from the server"));

            var buttonRow = new VisualElement();
            buttonRow.style.display = DisplayStyle.Flex;
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.Center;
            buttonRow.style.alignItems = Align.Center;
            buttonRow.style.flexWrap = Wrap.Wrap;
            buttonRow.style.marginTop = new StyleLength(new Length(6, LengthUnit.Pixel));

            view.LinkDiscordButton = new Button(() => ShowVerificationModal(view));
            StyleButton(view.LinkDiscordButton, new Color(0.20f, 0.55f, 0.37f, 1f), new Color(0.26f, 0.67f, 0.45f, 1f));
            SetButtonText(view.LinkDiscordButton, "LINK DISCORD");

            view.JoinDiscordButton = new Button(() => onJoinDiscord?.Invoke());
            StyleButton(view.JoinDiscordButton, new Color(0.17f, 0.35f, 0.72f, 1f), new Color(0.23f, 0.45f, 0.86f, 1f));
            SetButtonText(view.JoinDiscordButton, "JOIN DISCORD");

            view.LaterButton = new Button(() => onLeaveServer?.Invoke());
            StyleButton(view.LaterButton, new Color(0.20f, 0.24f, 0.30f, 1f), new Color(0.28f, 0.33f, 0.40f, 1f));
            SetButtonText(view.LaterButton, "LEAVE SERVER");

            buttonRow.Add(view.LinkDiscordButton);
            buttonRow.Add(view.JoinDiscordButton);
            buttonRow.Add(view.LaterButton);

            card.Add(chip);
            card.Add(title);
            card.Add(body);
            card.Add(list);
            card.Add(buttonRow);
            panel.Add(card);

            view.VerificationModalBackdrop = BuildVerificationModal(view, onVerifyCode, onCloseVerification);
            panel.Add(view.VerificationModalBackdrop);

            rootView.Root.Add(panel);
            view.Panel = panel;
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
            backdrop.style.display = DisplayStyle.None;
            backdrop.style.position = Position.Absolute;
            backdrop.style.top = 0;
            backdrop.style.left = 0;
            backdrop.style.right = 0;
            backdrop.style.bottom = 0;
            backdrop.style.justifyContent = Justify.Center;
            backdrop.style.alignItems = Align.Center;
            backdrop.style.backgroundColor = new StyleColor(new Color(0.02f, 0.04f, 0.07f, 0.84f));
            backdrop.style.borderTopLeftRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            backdrop.style.borderTopRightRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            backdrop.style.borderBottomLeftRadius = new StyleLength(new Length(24, LengthUnit.Pixel));
            backdrop.style.borderBottomRightRadius = new StyleLength(new Length(24, LengthUnit.Pixel));

            var modalCard = new VisualElement();
            modalCard.style.display = DisplayStyle.Flex;
            modalCard.style.flexDirection = FlexDirection.Column;
            modalCard.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            modalCard.style.maxWidth = 560;
            modalCard.style.paddingTop = new StyleLength(new Length(20, LengthUnit.Pixel));
            modalCard.style.paddingBottom = new StyleLength(new Length(20, LengthUnit.Pixel));
            modalCard.style.paddingLeft = new StyleLength(new Length(20, LengthUnit.Pixel));
            modalCard.style.paddingRight = new StyleLength(new Length(20, LengthUnit.Pixel));
            modalCard.style.backgroundColor = new StyleColor(new Color(0.09f, 0.13f, 0.18f, 0.98f));
            modalCard.style.borderTopLeftRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            modalCard.style.borderTopRightRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            modalCard.style.borderBottomLeftRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            modalCard.style.borderBottomRightRadius = new StyleLength(new Length(20, LengthUnit.Pixel));
            modalCard.style.borderTopWidth = 1;
            modalCard.style.borderRightWidth = 1;
            modalCard.style.borderBottomWidth = 1;
            modalCard.style.borderLeftWidth = 1;
            modalCard.style.borderTopColor = new StyleColor(new Color(0.72f, 0.84f, 0.96f, 0.16f));
            modalCard.style.borderRightColor = new StyleColor(new Color(0.72f, 0.84f, 0.96f, 0.08f));
            modalCard.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.30f));
            modalCard.style.borderLeftColor = new StyleColor(new Color(0.72f, 0.84f, 0.96f, 0.08f));

            var title = CreateLabel("Verify Discord Link", 24, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.98f, 0.99f, 1f, 1f));
            var instructions = CreateLabel("1. Join the Discord server if you have not already.\n2. Use the Discord bot flow to receive your verification code.\n3. Paste that code below and press Verify.\n4. If you close this window, you will leave the server immediately.", 13, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.84f, 0.90f, 0.95f, 0.96f));
            instructions.style.whiteSpace = WhiteSpace.Normal;
            instructions.style.marginTop = new StyleLength(new Length(10, LengthUnit.Pixel));

            var inputLabel = CreateLabel("Verification code", 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.90f, 0.96f, 1f, 0.94f));
            inputLabel.style.marginTop = new StyleLength(new Length(14, LengthUnit.Pixel));

            view.VerificationCodeInput = new TextField();
            view.VerificationCodeInput.style.marginTop = new StyleLength(new Length(8, LengthUnit.Pixel));
            view.VerificationCodeInput.style.height = 42;
            view.VerificationCodeInput.style.paddingLeft = new StyleLength(new Length(8, LengthUnit.Pixel));
            view.VerificationCodeInput.style.paddingRight = new StyleLength(new Length(8, LengthUnit.Pixel));
            view.VerificationCodeInput.style.backgroundColor = new StyleColor(new Color(0.05f, 0.08f, 0.12f, 0.96f));
            view.VerificationCodeInput.style.color = new StyleColor(new Color(0.96f, 0.98f, 1f, 1f));
            view.VerificationCodeInput.style.borderTopLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.VerificationCodeInput.style.borderTopRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.VerificationCodeInput.style.borderBottomLeftRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.VerificationCodeInput.style.borderBottomRightRadius = new StyleLength(new Length(12, LengthUnit.Pixel));
            view.VerificationCodeInput.style.borderTopWidth = 1;
            view.VerificationCodeInput.style.borderRightWidth = 1;
            view.VerificationCodeInput.style.borderBottomWidth = 1;
            view.VerificationCodeInput.style.borderLeftWidth = 1;
            view.VerificationCodeInput.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.10f));
            view.VerificationCodeInput.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
            view.VerificationCodeInput.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.24f));
            view.VerificationCodeInput.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
            view.VerificationCodeInput.label = string.Empty;
            view.VerificationCodeInput.value = string.Empty;

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
            buttonRow.style.marginTop = new StyleLength(new Length(16, LengthUnit.Pixel));

            view.CloseVerificationButton = new Button(() => onCloseVerification?.Invoke());
            StyleButton(view.CloseVerificationButton, new Color(0.20f, 0.24f, 0.30f, 1f), new Color(0.28f, 0.33f, 0.40f, 1f));
            SetButtonText(view.CloseVerificationButton, "LEAVE SERVER");
            view.CloseVerificationButton.style.minWidth = 144;

            view.VerifyButton = new Button(() => SubmitVerificationCode(view, onVerifyCode));
            StyleButton(view.VerifyButton, new Color(0.20f, 0.55f, 0.37f, 1f), new Color(0.26f, 0.67f, 0.45f, 1f));
            SetButtonText(view.VerifyButton, "VERIFY");
            view.VerifyButton.style.minWidth = 160;

            buttonRow.Add(view.CloseVerificationButton);
            buttonRow.Add(view.VerifyButton);

            modalCard.Add(title);
            modalCard.Add(instructions);
            modalCard.Add(inputLabel);
            modalCard.Add(view.VerificationCodeInput);
            modalCard.Add(view.VerificationStatusLabel);
            modalCard.Add(buttonRow);
            backdrop.Add(modalCard);
            view.VerificationModalCard = modalCard;
            return backdrop;
        }

        private static void ShowVerificationModal(View view)
        {
            if (view?.VerificationModalBackdrop == null)
            {
                return;
            }

            view.VerificationCodeInput.value = string.Empty;
            view.VerificationStatusLabel.text = string.Empty;
            view.VerificationStatusLabel.style.display = DisplayStyle.None;
            view.VerificationModalBackdrop.style.display = DisplayStyle.Flex;
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

        private static VisualElement CreateBenefitRow(string text)
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
            row.style.backgroundColor = new StyleColor(new Color(0.11f, 0.15f, 0.21f, 0.84f));
            row.style.borderTopLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            row.style.borderTopRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            row.style.borderBottomLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            row.style.borderBottomRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));

            var dot = new VisualElement();
            dot.style.width = 8;
            dot.style.height = 8;
            dot.style.marginRight = new StyleLength(new Length(10, LengthUnit.Pixel));
            dot.style.backgroundColor = new StyleColor(new Color(0.40f, 0.78f, 0.96f, 1f));
            dot.style.borderTopLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            dot.style.borderTopRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            dot.style.borderBottomLeftRadius = new StyleLength(new Length(999, LengthUnit.Pixel));
            dot.style.borderBottomRightRadius = new StyleLength(new Length(999, LengthUnit.Pixel));

            var label = CreateLabel(text, 13, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.93f, 0.97f, 1f, 0.98f));
            label.style.flexGrow = 1;
            label.style.whiteSpace = WhiteSpace.Normal;

            row.Add(dot);
            row.Add(label);
            return row;
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

        private static void StyleButton(Button button, Color baseColor, Color hoverColor)
        {
            button.style.height = 48;
            button.style.minWidth = 176;
            button.style.marginLeft = new StyleLength(new Length(6, LengthUnit.Pixel));
            button.style.marginRight = new StyleLength(new Length(6, LengthUnit.Pixel));
            button.style.marginBottom = new StyleLength(new Length(8, LengthUnit.Pixel));
            button.style.paddingTop = new StyleLength(new Length(0, LengthUnit.Pixel));
            button.style.paddingBottom = new StyleLength(new Length(0, LengthUnit.Pixel));
            button.style.paddingLeft = new StyleLength(new Length(18, LengthUnit.Pixel));
            button.style.paddingRight = new StyleLength(new Length(18, LengthUnit.Pixel));
            button.style.backgroundColor = new StyleColor(baseColor);
            button.style.borderTopLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            button.style.borderTopRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            button.style.borderBottomLeftRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
            button.style.borderBottomRightRadius = new StyleLength(new Length(14, LengthUnit.Pixel));
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
            view.Panel.transform.scale = new Vector3(0.985f, 0.985f, 1f);
            view.Panel.transform.position = new Vector3(0f, 10f, 0f);

            var startedAt = Time.unscaledTime;
            view.Panel.schedule.Execute(() =>
            {
                if (view.Panel == null || view.Panel.style.display == DisplayStyle.None)
                {
                    return;
                }

                var progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / PanelEntranceDurationSeconds);
                var eased = 1f - Mathf.Pow(1f - progress, 3f);
                view.Panel.style.opacity = eased;
                view.Panel.transform.scale = Vector3.LerpUnclamped(new Vector3(0.985f, 0.985f, 1f), Vector3.one, eased);
                view.Panel.transform.position = Vector3.LerpUnclamped(new Vector3(0f, 10f, 0f), Vector3.zero, eased);

                if (progress >= 1f)
                {
                    CompleteVisibleState(view);
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
    }
}