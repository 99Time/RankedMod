using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;
using DraftState = schrader.DraftOverlayStateMessage;

namespace schrader 
{
    public static class DraftUI
    {
        private static VisualElement _hudRoot;
        private static VisualElement _overlayRoot;
        private static VisualElement _panel;
        private static Label _titleLabel;
        private static Label _stateLabel;
        private static Button _readyButton;

        private static bool _isSetup = false;
        private static bool _isUiVisible = false;
        private static bool _cursorStateCaptured = false;
        private static CursorLockMode _previousCursorLockState = CursorLockMode.Locked;
        private static bool _previousCursorVisible = false;

        static readonly FieldInfo _uiHudField =
            typeof(UIHUDController).GetField("uiHud", BindingFlags.Instance | BindingFlags.NonPublic);

        static readonly FieldInfo _uiHudContainerField =
            typeof(UIHUD).GetField("container", BindingFlags.Instance | BindingFlags.NonPublic);

        [HarmonyPatch(typeof(UIHUDController), "Event_OnPlayerBodySpawned")]
        public static class Patch
        {
            [HarmonyPostfix]
            public static void Postfix(UIHUDController __instance)
            {
                if (_isSetup) return;

                Setup(__instance);
                _isSetup = true;
            }
        }

        // 🔥 CHAT COMMAND HOOK (/ui)
        [HarmonyPatch(typeof(UIChat), "AddChatMessage")]
        public static class ChatPatch
        {
            static void Postfix(string message)
            {
                if (string.IsNullOrWhiteSpace(message)) return;

                if (message.Equals("/ui", StringComparison.OrdinalIgnoreCase))
                {
                    DraftUI.ForceToggleUI();
                }
            }
        }

        private static void Setup(UIHUDController controller)
        {
            try
            {
                UIHUD hud = (UIHUD)_uiHudField.GetValue(controller);
                VisualElement uiHudContainer = (VisualElement)_uiHudContainerField.GetValue(hud);

                if (uiHudContainer == null)
                {
                    Debug.LogError("HUD ROOT NULL");
                    return;
                }

                _hudRoot = uiHudContainer;
                BuildUI();

                _hudRoot.Add(_overlayRoot);

                _overlayRoot.style.display = DisplayStyle.None;
                _overlayRoot.BringToFront();

                Debug.Log("DRAFT UI CREATED + ATTACHED");
            }
            catch (Exception ex)
            {
                Debug.LogError("Draft UI setup failed: " + ex.Message);
            }
        }

        private static void BuildUI()
        {
            _overlayRoot = new VisualElement();
            _overlayRoot.style.position = Position.Absolute;
            _overlayRoot.style.left = 0;
            _overlayRoot.style.right = 0;
            _overlayRoot.style.top = 0;
            _overlayRoot.style.bottom = 0;

            _overlayRoot.style.justifyContent = Justify.Center;
            _overlayRoot.style.alignItems = Align.Center;

            _overlayRoot.pickingMode = PickingMode.Position;

            _panel = new VisualElement();
            _panel.style.width = 460;
            _panel.style.minHeight = 240;
            _panel.style.paddingTop = 14;
            _panel.style.paddingBottom = 14;
            _panel.style.paddingLeft = 18;
            _panel.style.paddingRight = 18;
            _panel.style.flexDirection = FlexDirection.Column;
            _panel.style.alignItems = Align.Center;
            _panel.style.justifyContent = Justify.Center;

            _panel.style.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 0.95f);
            _panel.style.borderTopLeftRadius = 16;
            _panel.style.borderTopRightRadius = 16;
            _panel.style.borderBottomLeftRadius = 16;
            _panel.style.borderBottomRightRadius = 16;

            _panel.pickingMode = PickingMode.Position;

            _titleLabel = new Label("RANKED MATCH SETUP");
            _titleLabel.style.color = Color.white;
            _titleLabel.style.fontSize = 21;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            _stateLabel = new Label("Draft loading...");
            _stateLabel.style.color = Color.white;
            _stateLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _stateLabel.style.marginTop = 10;

            _readyButton = new Button(OnReadyClicked)
            {
                text = "GET READY"
            };

            _readyButton.style.marginTop = 12;
            _readyButton.style.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
            _readyButton.style.color = Color.white;

            _panel.Add(_titleLabel);
            _panel.Add(_stateLabel);
            _panel.Add(_readyButton);

            _overlayRoot.Add(_panel);
        }

        public static void ForceToggleUI()
        {
            if (_overlayRoot == null) return;

            bool isVisible = _overlayRoot.style.display == DisplayStyle.Flex;

            if (isVisible)
            {
                Hide();
                Debug.Log("UI FORCED HIDE");
            }
            else
            {
                _overlayRoot.style.display = DisplayStyle.Flex;
                EnableCursor();
                Debug.Log("UI FORCED SHOW");
            }
        }

        private static void EnableCursor()
        {
            if (!_cursorStateCaptured)
            {
                _previousCursorLockState = UnityEngine.Cursor.lockState;
                _previousCursorVisible = UnityEngine.Cursor.visible;
                _cursorStateCaptured = true;
            }

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private static void DisableCursor()
        {
            if (_cursorStateCaptured)
            {
                UnityEngine.Cursor.lockState = _previousCursorLockState;
                UnityEngine.Cursor.visible = _previousCursorVisible;
                _cursorStateCaptured = false;
            }
        }

        private static void Hide()
        {
            if (_overlayRoot == null) return;

            _overlayRoot.style.display = DisplayStyle.None;
            DisableCursor();
            if (_isUiVisible)
            {
                _isUiVisible = false;
                Debug.Log("draft UI hidden");
            }
        }

        private static void Show()
        {
            if (_overlayRoot == null) return;

            _overlayRoot.style.display = DisplayStyle.Flex;
            _overlayRoot.style.visibility = Visibility.Visible;
            _overlayRoot.style.opacity = 1f;
            _overlayRoot.BringToFront();
            EnableCursor();
            if (!_isUiVisible)
            {
                _isUiVisible = true;
                Debug.Log("draft UI shown");
            }
        }

        public static void UpdateDraftUI(DraftState state)
        {
            if (_overlayRoot == null) return;

            if (state == null || !state.IsVisible)
            {
                Hide();
                return;
            }

            Show();

            var title = string.IsNullOrWhiteSpace(state.Title) ? "RANKED MATCH SETUP" : state.Title;
            var red = string.IsNullOrWhiteSpace(state.RedCaptainName) ? "Pending" : state.RedCaptainName;
            var blue = string.IsNullOrWhiteSpace(state.BlueCaptainName) ? "Pending" : state.BlueCaptainName;
            var status = state.IsCompleted ? "Draft Complete" : "Draft Active";
            var turn = string.IsNullOrWhiteSpace(state.CurrentTurnName) ? "Pending" : state.CurrentTurnName;
            var available = FormatList(state.AvailablePlayers);
            var redPlayers = FormatList(state.RedPlayers);
            var bluePlayers = FormatList(state.BluePlayers);
            var lateJoiners = state.PendingLateJoinerCount <= 0 ? "none" : state.PendingLateJoinerCount.ToString();
            var dummyMode = state.DummyModeActive ? "ON" : "OFF";
            var footer = string.IsNullOrWhiteSpace(state.FooterText) ? "Use /pick, /accept, /dummy, /draft" : state.FooterText;

            if (_titleLabel != null)
            {
                _titleLabel.text = title;
            }

            if (_stateLabel != null)
            {
                _stateLabel.text =
                    $"Red Captain: {red}\n" +
                    $"Blue Captain: {blue}\n" +
                    $"Status: {status}\n" +
                    $"Turn: {turn}\n" +
                    $"Available: {available}\n" +
                    $"Red Team: {redPlayers}\n" +
                    $"Blue Team: {bluePlayers}\n" +
                    $"Late Joiners: {lateJoiners}\n" +
                    $"Dummy Mode: {dummyMode}\n" +
                    $"{footer}";
            }
        }

        private static string FormatList(string[] values)
        {
            if (values == null || values.Length == 0) return "none";
            var filtered = values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToArray();
            return filtered.Length == 0 ? "none" : string.Join(", ", filtered);
        }

        private static void OnReadyClicked()
        {
            try
            {
                var controller = UnityEngine.Object.FindFirstObjectByType<UIChatController>();
                if (controller != null)
                {
                    var method = controller.GetType().GetMethod("SendChatMessage",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (method != null)
                    {
                        method.Invoke(controller, new object[] { "/ready" });
                        Debug.Log("READY SENT OK");
                        return;
                    }
                }

                Debug.LogError("FAILED TO SEND /ready");
            }
            catch (Exception e)
            {
                Debug.LogError("READY ERROR: " + e.Message);
            }
        }

        public static void Shutdown()
        {
            Hide();
            _isSetup = false;
            _titleLabel = null;
            _stateLabel = null;
            _readyButton = null;
            _panel = null;
            _hudRoot = null;
            _isUiVisible = false;

            if (_overlayRoot != null)
            {
                _overlayRoot.RemoveFromHierarchy();
                _overlayRoot = null;
            }
        }
    }
}
