using UnityEngine;

namespace ProCameraMod
{
    internal static class ProCameraRuntime
    {
        private static readonly Vector3 competitiveOffset = new Vector3(0f, 0.22f, -0.38f);
        private const float CompetitiveBaseFov = 96f;
        private const float CompetitiveMaxFovBonus = 8f;
        private const float SpeedForMaxBonus = 18f;
        private const float PositionSmoothing = 10f;
        private const float FovSmoothing = 8f;

        private static PlayerCamera activeCamera;
        private static Vector3 baselineLocalPosition;
        private static float baselineFov;
        private static bool baselineCaptured;

        internal static void Reset()
        {
            if (activeCamera != null)
            {
                RestoreBaseline(activeCamera);
            }

            activeCamera = null;
            baselineLocalPosition = Vector3.zero;
            baselineFov = 0f;
            baselineCaptured = false;
        }

        internal static void OnPlayerCameraEnabled(PlayerCamera __instance)
        {
            if (!ShouldManage(__instance))
            {
                return;
            }

            if (activeCamera != null && !ReferenceEquals(activeCamera, __instance))
            {
                RestoreBaseline(activeCamera);
            }

            if (ReferenceEquals(activeCamera, __instance) && baselineCaptured)
            {
                ApplyCompetitivePreset(__instance, immediate: true, deltaTime: 0f);
                return;
            }

            activeCamera = __instance;
            CaptureBaseline(__instance);
            ApplyCompetitivePreset(__instance, immediate: true, deltaTime: 0f);
            ProCameraPlugin.Log("Competitive camera attached to local player.");
        }

        internal static void OnPlayerCameraDisabled(PlayerCamera __instance)
        {
            if (__instance == null)
            {
                return;
            }

            if (ReferenceEquals(activeCamera, __instance))
            {
                RestoreBaseline(__instance);
                activeCamera = null;
                baselineCaptured = false;
            }
        }

        internal static void OnPlayerCameraTick(PlayerCamera __instance, float deltaTime)
        {
            if (!ShouldManage(__instance) || !ReferenceEquals(activeCamera, __instance))
            {
                return;
            }

            if (!baselineCaptured)
            {
                CaptureBaseline(__instance);
            }

            ApplyCompetitivePreset(__instance, immediate: false, deltaTime: deltaTime);
        }

        private static bool ShouldManage(PlayerCamera playerCamera)
        {
            return playerCamera != null
                && playerCamera.Player != null
                && playerCamera.Player.IsLocalPlayer
                && playerCamera.CameraComponent != null
                && !Application.isBatchMode;
        }

        private static void CaptureBaseline(PlayerCamera playerCamera)
        {
            baselineLocalPosition = playerCamera.transform.localPosition;
            baselineFov = playerCamera.CameraComponent.fieldOfView;
            baselineCaptured = true;
        }

        private static void RestoreBaseline(PlayerCamera playerCamera)
        {
            if (!baselineCaptured || playerCamera == null)
            {
                return;
            }

            playerCamera.transform.localPosition = baselineLocalPosition;
            playerCamera.SetFieldOfView(baselineFov);
        }

        private static void ApplyCompetitivePreset(PlayerCamera playerCamera, bool immediate, float deltaTime)
        {
            var targetLocalPosition = baselineLocalPosition + competitiveOffset;
            var targetFov = ResolveTargetFov(playerCamera);

            if (immediate)
            {
                playerCamera.transform.localPosition = targetLocalPosition;
                playerCamera.SetFieldOfView(targetFov);
                return;
            }

            var positionBlend = Mathf.Clamp01(deltaTime * PositionSmoothing);
            var fovBlend = Mathf.Clamp01(deltaTime * FovSmoothing);

            playerCamera.transform.localPosition = Vector3.Lerp(
                playerCamera.transform.localPosition,
                targetLocalPosition,
                positionBlend);

            var nextFov = Mathf.Lerp(playerCamera.CameraComponent.fieldOfView, targetFov, fovBlend);
            playerCamera.SetFieldOfView(nextFov);
        }

        private static float ResolveTargetFov(PlayerCamera playerCamera)
        {
            var playerBody = playerCamera.PlayerBody;
            if (playerBody == null || playerBody.Rigidbody == null)
            {
                return CompetitiveBaseFov;
            }

            var velocity = playerBody.Rigidbody.linearVelocity;
            var horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            var speedRatio = Mathf.Clamp01(horizontalSpeed / SpeedForMaxBonus);
            return CompetitiveBaseFov + CompetitiveMaxFovBonus * speedRatio;
        }
    }
}