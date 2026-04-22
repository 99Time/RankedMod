using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Unity.Netcode;

namespace schrader
{
    internal static class TrainingClientRuntime
    {
        private const string ControllerObjectName = "SchraderTrainingClientRuntime";
        private static GameObject controllerObject;
        private static TrainingClientController controller;

        public static void Initialize()
        {
            if (DraftUIPlugin.IsDedicatedServer() || controllerObject != null)
            {
                return;
            }

            controllerObject = new GameObject(ControllerObjectName);
            UnityEngine.Object.DontDestroyOnLoad(controllerObject);
            controller = controllerObject.AddComponent<TrainingClientController>();
        }

        public static void Shutdown()
        {
            if (controllerObject != null)
            {
                UnityEngine.Object.Destroy(controllerObject);
            }

            controllerObject = null;
            controller = null;
        }

        public static void SetTrainingServerMode(bool active)
        {
            if (DraftUIPlugin.IsDedicatedServer())
            {
                return;
            }

            if (controller == null)
            {
                Initialize();
            }

            controller?.SetTrainingServerMode(active);
        }

        public static void SetAuthoritativeTrainingPose(bool isOpenWorldActive, Vector3 position, Quaternion rotation, string reason)
        {
            if (DraftUIPlugin.IsDedicatedServer())
            {
                return;
            }

            if (controller == null)
            {
                Initialize();
            }

            controller?.SetAuthoritativeTrainingPose(isOpenWorldActive, position, rotation, reason);
        }

        public static bool TryHandleLocalChatCommand(string message)
        {
            return controller != null && controller.TryHandleLocalChatCommand(message);
        }

        private sealed class TrainingClientController : MonoBehaviour
        {
            private const string ExternalTrainingControllerObjectName = "PuckAttackController";
            private const float ExternalTrainingSuppressionIntervalSeconds = 1f;
            private const float TrainingBundleRetryIntervalSeconds = 1f;
            private const float TrainingRepresentationLogIntervalSeconds = 0.5f;
            private const float TrainingPoseReconcileIntervalSeconds = 0.1f;
            private const float TrainingPosePendingDurationSeconds = 3f;
            private const float TrainingPoseSatisfiedDistanceThreshold = 1.5f;
            private const float TrainingPoseReconcileDistanceThreshold = 5f;
            private const float TrainingPoseSatisfiedAngleThreshold = 8f;
            private static readonly Vector3 TrainingOpenWorldVisualRootPosition = new Vector3(200f, 0f, 0f);

            private bool trainingServerModeActive;
            private bool trainingWelcomeSent;
            private float lastExternalTrainingSuppressionAt = -999f;
            private float nextTrainingBundleLoadAttemptAt = -999f;
            private float lastRepresentationLogAt = -999f;
            private AssetBundle trainingBundle;
            private GameObject trainingOpenWorldVisualRoot;
            private GameObject platformPrefab;
            private GameObject platformNCPrefab;
            private GameObject platformsPrefab;
            private GameObject platformTPrefab;
            private ulong lastObservedLocalBodyNetworkObjectId;
            private ulong lastObservedLocalCameraNetworkObjectId;
            private string lastObservedLocalRepresentationSignature;
            private bool hasPendingAuthoritativePose;
            private bool pendingOpenWorldActive;
            private Vector3 pendingAuthoritativePosition;
            private Quaternion pendingAuthoritativeRotation = Quaternion.identity;
            private string pendingAuthoritativeReason;
            private float pendingAuthoritativePoseExpiresAt = -999f;
            private float lastAuthoritativePoseApplyAt = -999f;
            private ulong lastAuthoritativePoseBodyNetworkObjectId;

            private void Update()
            {
                if (!trainingServerModeActive)
                {
                    return;
                }

                SuppressExternalTrainingModIfNeeded();
                EnsureTrainingWelcomeShown();
                EnsureTrainingOpenWorldVisualsLoaded();
                ReconcileAuthoritativeTrainingPoseIfNeeded();
                ObserveLocalPlayerRepresentation();
            }

            private void OnDestroy()
            {
                DestroyTrainingOpenWorldVisuals();
            }

            public void SetTrainingServerMode(bool active)
            {
                if (trainingServerModeActive == active)
                {
                    if (active)
                    {
                        EnsureTrainingWelcomeShown();
                        EnsureTrainingOpenWorldVisualsLoaded();
                    }

                    return;
                }

                trainingServerModeActive = active;
                trainingWelcomeSent = false;
                lastRepresentationLogAt = -999f;
                lastObservedLocalBodyNetworkObjectId = 0;
                lastObservedLocalCameraNetworkObjectId = 0;
                lastObservedLocalRepresentationSignature = null;
                ClearPendingAuthoritativeTrainingPose();

                if (!active)
                {
                    DestroyTrainingOpenWorldVisuals();
                    return;
                }

                SuppressExternalTrainingModIfNeeded(force: true);
            }

            public void SetAuthoritativeTrainingPose(bool isOpenWorldActive, Vector3 position, Quaternion rotation, string reason)
            {
                hasPendingAuthoritativePose = true;
                pendingOpenWorldActive = isOpenWorldActive;
                pendingAuthoritativePosition = position;
                pendingAuthoritativeRotation = rotation;
                pendingAuthoritativeReason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason.Trim();
                pendingAuthoritativePoseExpiresAt = Time.unscaledTime + TrainingPosePendingDurationSeconds;
                lastAuthoritativePoseApplyAt = -999f;
                lastAuthoritativePoseBodyNetworkObjectId = 0;

                DraftUIPlugin.Log($"[CLIENT][TRAINING] Queued authoritative pose reconcile. active={(pendingOpenWorldActive ? "yes" : "no")} targetPos={FormatVector3(pendingAuthoritativePosition)} targetRot={FormatQuaternion(pendingAuthoritativeRotation)} reason={pendingAuthoritativeReason}");
            }

            public bool TryHandleLocalChatCommand(string message)
            {
                return false;
            }

            private void EnsureTrainingWelcomeShown()
            {
                if (trainingWelcomeSent || UIChat.Instance == null)
                {
                    return;
                }

                AddChatMessage("Training mode active. Use /openworld to enter the dedicated training anchor and /return to go back.");
                AddChatMessage("Training visuals are client-only and load from puckobjects when that bundle is deployed next to the client mod assembly.");
                trainingWelcomeSent = true;
            }

            private void EnsureTrainingOpenWorldVisualsLoaded()
            {
                if (trainingOpenWorldVisualRoot != null)
                {
                    return;
                }

                if (!EnsureTrainingBundleLoaded() || !EnsureTrainingOpenWorldPrefabsResolved())
                {
                    return;
                }

                trainingOpenWorldVisualRoot = new GameObject("SchraderTrainingOpenWorldVisuals");
                UnityEngine.Object.DontDestroyOnLoad(trainingOpenWorldVisualRoot);
                trainingOpenWorldVisualRoot.transform.position = TrainingOpenWorldVisualRootPosition;
                trainingOpenWorldVisualRoot.transform.rotation = Quaternion.identity;

                InstantiateTrainingVisual(platformPrefab);
                InstantiateTrainingVisual(platformNCPrefab);
                InstantiateTrainingVisual(platformsPrefab);
                InstantiateTrainingVisual(platformTPrefab);

                DraftUIPlugin.Log("[CLIENT][TRAINING] Loaded minimal open-world visuals for training mode.");
            }

            private bool EnsureTrainingBundleLoaded()
            {
                if (trainingBundle != null)
                {
                    return true;
                }

                if (Time.unscaledTime < nextTrainingBundleLoadAttemptAt)
                {
                    return false;
                }

                nextTrainingBundleLoadAttemptAt = Time.unscaledTime + TrainingBundleRetryIntervalSeconds;

                try
                {
                    SuppressExternalTrainingModIfNeeded(force: true);

                    var loadedBundle = TryFindAlreadyLoadedTrainingBundle();
                    if (loadedBundle != null)
                    {
                        trainingBundle = loadedBundle;
                        nextTrainingBundleLoadAttemptAt = -999f;
                        DraftUIPlugin.Log("[CLIENT][TRAINING] Reusing already-loaded puckobjects bundle for training visuals.");
                        return true;
                    }

                    var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    if (string.IsNullOrWhiteSpace(assemblyDirectory))
                    {
                        DraftUIPlugin.LogError("[CLIENT][TRAINING] Training bundle load failed because the client assembly directory could not be resolved.");
                        return false;
                    }

                    var candidatePaths = new[]
                    {
                        Path.Combine(assemblyDirectory, "Windows", "puckobjects"),
                        Path.Combine(assemblyDirectory, "Linux", "puckobjects"),
                        Path.Combine(assemblyDirectory, "puckobjects")
                    };

                    foreach (var candidatePath in candidatePaths)
                    {
                        if (!File.Exists(candidatePath))
                        {
                            continue;
                        }

                        trainingBundle = AssetBundle.LoadFromFile(candidatePath);
                        if (trainingBundle != null)
                        {
                            nextTrainingBundleLoadAttemptAt = -999f;
                            DraftUIPlugin.Log($"[CLIENT][TRAINING] Loaded training bundle from {candidatePath}.");
                            return true;
                        }

                        loadedBundle = TryFindAlreadyLoadedTrainingBundle();
                        if (loadedBundle != null)
                        {
                            trainingBundle = loadedBundle;
                            nextTrainingBundleLoadAttemptAt = -999f;
                            DraftUIPlugin.Log($"[CLIENT][TRAINING] Reused already-loaded puckobjects bundle after duplicate-load conflict at {candidatePath}.");
                            return true;
                        }
                    }

                    DraftUIPlugin.Log("[CLIENT][TRAINING] Could not acquire puckobjects for training visuals. The file may be absent or already loaded by another mod without the expected training assets.");
                    return false;
                }
                catch (Exception ex)
                {
                    DraftUIPlugin.LogError($"[CLIENT][TRAINING] Failed to load training bundle: {ex}");
                    return false;
                }
            }

            private static AssetBundle TryFindAlreadyLoadedTrainingBundle()
            {
                try
                {
                    foreach (var loadedBundle in AssetBundle.GetAllLoadedAssetBundles())
                    {
                        if (loadedBundle == null)
                        {
                            continue;
                        }

                        if (TryResolveTrainingOpenWorldAssetNames(loadedBundle, out _, out _, out _, out _))
                        {
                            return loadedBundle;
                        }
                    }
                }
                catch (Exception ex)
                {
                    DraftUIPlugin.LogError($"[CLIENT][TRAINING] Failed while scanning already-loaded AssetBundles: {ex}");
                }

                return null;
            }

            private static bool BundleLooksLikeTrainingOpenWorldBundle(AssetBundle bundle)
            {
                return TryResolveTrainingOpenWorldAssetNames(bundle, out _, out _, out _, out _);
            }

            private bool EnsureTrainingOpenWorldPrefabsResolved()
            {
                if (platformPrefab != null && platformNCPrefab != null && platformsPrefab != null && platformTPrefab != null)
                {
                    return true;
                }

                if (trainingBundle == null)
                {
                    return false;
                }

                try
                {
                    if (!TryResolveTrainingOpenWorldAssetNames(trainingBundle, out var platformAssetName, out var platformNcAssetName, out var platformsAssetName, out var platformTAssetName))
                    {
                        DraftUIPlugin.Log("[CLIENT][TRAINING] Training bundle loaded, but open-world platform prefabs were not all found.");
                        return false;
                    }

                    if (platformPrefab == null && !string.IsNullOrWhiteSpace(platformAssetName))
                    {
                        platformPrefab = trainingBundle.LoadAsset<GameObject>(platformAssetName);
                    }

                    if (platformNCPrefab == null && !string.IsNullOrWhiteSpace(platformNcAssetName))
                    {
                        platformNCPrefab = trainingBundle.LoadAsset<GameObject>(platformNcAssetName);
                    }

                    if (platformsPrefab == null && !string.IsNullOrWhiteSpace(platformsAssetName))
                    {
                        platformsPrefab = trainingBundle.LoadAsset<GameObject>(platformsAssetName);
                    }

                    if (platformTPrefab == null && !string.IsNullOrWhiteSpace(platformTAssetName))
                    {
                        platformTPrefab = trainingBundle.LoadAsset<GameObject>(platformTAssetName);
                    }

                    if (platformPrefab != null && platformNCPrefab != null && platformsPrefab != null && platformTPrefab != null)
                    {
                        return true;
                    }

                    DraftUIPlugin.Log("[CLIENT][TRAINING] Training bundle loaded, but open-world platform prefabs were not all found.");
                    return false;
                }
                catch (Exception ex)
                {
                    DraftUIPlugin.LogError($"[CLIENT][TRAINING] Failed to resolve open-world prefabs from the training bundle: {ex}");
                    return false;
                }
            }

            private static bool TryResolveTrainingOpenWorldAssetNames(AssetBundle bundle, out string platformAssetName, out string platformNcAssetName, out string platformsAssetName, out string platformTAssetName)
            {
                platformAssetName = null;
                platformNcAssetName = null;
                platformsAssetName = null;
                platformTAssetName = null;

                if (bundle == null)
                {
                    return false;
                }

                try
                {
                    foreach (var assetName in bundle.GetAllAssetNames())
                    {
                        var assetShortName = Path.GetFileNameWithoutExtension(assetName).ToLowerInvariant();

                        if (platformAssetName == null && assetShortName.Contains("platform") && !assetShortName.Contains("platformnc") && !assetShortName.Contains("platforms") && !assetShortName.Contains("platformt"))
                        {
                            platformAssetName = assetName;
                            continue;
                        }

                        if (platformNcAssetName == null && assetShortName.Contains("platformnc"))
                        {
                            platformNcAssetName = assetName;
                            continue;
                        }

                        if (platformsAssetName == null && assetShortName.Contains("platforms"))
                        {
                            platformsAssetName = assetName;
                            continue;
                        }

                        if (platformTAssetName == null && assetShortName.Contains("platformt"))
                        {
                            platformTAssetName = assetName;
                        }

                        if (platformAssetName != null && platformNcAssetName != null && platformsAssetName != null && platformTAssetName != null)
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                }

                return platformAssetName != null && platformNcAssetName != null && platformsAssetName != null && platformTAssetName != null;
            }

            private void InstantiateTrainingVisual(GameObject prefab)
            {
                if (prefab == null || trainingOpenWorldVisualRoot == null)
                {
                    return;
                }

                try
                {
                    var instance = UnityEngine.Object.Instantiate(prefab, trainingOpenWorldVisualRoot.transform, false);
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;

                    foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                    {
                        collider.enabled = false;
                    }

                    foreach (var rigidbody in instance.GetComponentsInChildren<Rigidbody>(true))
                    {
                        rigidbody.isKinematic = true;
                        rigidbody.detectCollisions = false;
                    }
                }
                catch (Exception ex)
                {
                    DraftUIPlugin.LogError($"[CLIENT][TRAINING] Failed to instantiate a training open-world visual prefab: {ex}");
                }
            }

            private void DestroyTrainingOpenWorldVisuals()
            {
                if (trainingOpenWorldVisualRoot != null)
                {
                    UnityEngine.Object.Destroy(trainingOpenWorldVisualRoot);
                    trainingOpenWorldVisualRoot = null;
                }
            }

            private void ReconcileAuthoritativeTrainingPoseIfNeeded()
            {
                if (!hasPendingAuthoritativePose)
                {
                    return;
                }

                if (Time.unscaledTime > pendingAuthoritativePoseExpiresAt)
                {
                    DraftUIPlugin.Log($"[CLIENT][TRAINING] Authoritative pose reconcile expired before the local representation settled. targetPos={FormatVector3(pendingAuthoritativePosition)} reason={pendingAuthoritativeReason}");
                    ClearPendingAuthoritativeTrainingPose();
                    return;
                }

                if (!TryGetLocalPlayerRepresentation(out _, out var playerBody, out var playerCamera) || playerBody == null)
                {
                    return;
                }

                var positionDistance = Vector3.Distance(playerBody.transform.position, pendingAuthoritativePosition);
                var rotationAngle = Quaternion.Angle(playerBody.transform.rotation, pendingAuthoritativeRotation);
                if (positionDistance <= TrainingPoseSatisfiedDistanceThreshold && rotationAngle <= TrainingPoseSatisfiedAngleThreshold)
                {
                    DraftUIPlugin.Log($"[CLIENT][TRAINING] Authoritative pose reconcile satisfied. bodyId={playerBody.NetworkObjectId} pos={FormatVector3(playerBody.transform.position)} targetPos={FormatVector3(pendingAuthoritativePosition)} reason={pendingAuthoritativeReason}");
                    ClearPendingAuthoritativeTrainingPose();
                    return;
                }

                if (positionDistance <= TrainingPoseReconcileDistanceThreshold && rotationAngle <= TrainingPoseSatisfiedAngleThreshold)
                {
                    return;
                }

                if (Time.unscaledTime - lastAuthoritativePoseApplyAt < TrainingPoseReconcileIntervalSeconds
                    && lastAuthoritativePoseBodyNetworkObjectId == playerBody.NetworkObjectId)
                {
                    return;
                }

                lastAuthoritativePoseApplyAt = Time.unscaledTime;
                lastAuthoritativePoseBodyNetworkObjectId = playerBody.NetworkObjectId;

                try
                {
                    playerBody.Server_Teleport(pendingAuthoritativePosition, pendingAuthoritativeRotation);
                    Physics.SyncTransforms();
                    DraftUIPlugin.Log($"[CLIENT][TRAINING] Applied authoritative pose reconcile to local body. active={(pendingOpenWorldActive ? "yes" : "no")} bodyId={playerBody.NetworkObjectId} cameraId={(playerCamera != null ? playerCamera.NetworkObjectId : 0UL)} beforePos={FormatVector3(playerBody.transform.position)} targetPos={FormatVector3(pendingAuthoritativePosition)} targetRot={FormatQuaternion(pendingAuthoritativeRotation)} reason={pendingAuthoritativeReason}");
                }
                catch (Exception ex)
                {
                    DraftUIPlugin.LogError($"[CLIENT][TRAINING] Failed to apply authoritative pose reconcile: {ex}");
                }
            }

            private void ObserveLocalPlayerRepresentation()
            {
                if (!TryGetLocalPlayerRepresentation(out var localPlayer, out var playerBody, out var playerCamera))
                {
                    return;
                }

                var bodyNetworkObjectId = playerBody != null ? playerBody.NetworkObjectId : 0UL;
                var cameraNetworkObjectId = playerCamera != null ? playerCamera.NetworkObjectId : 0UL;
                var bodyPosition = playerBody != null ? playerBody.transform.position : Vector3.zero;
                var cameraPosition = playerCamera != null ? playerCamera.transform.position : Vector3.zero;
                var anchorDistance = playerBody != null ? Vector3.Distance(bodyPosition, TrainingOpenWorldVisualRootPosition) : float.PositiveInfinity;
                var cameraFollowsBody = playerBody != null && playerCamera != null && playerCamera.transform.IsChildOf(playerBody.transform);
                var signature = string.Concat(
                    bodyNetworkObjectId.ToString(), "|",
                    cameraNetworkObjectId.ToString(), "|",
                    RoundForSignature(bodyPosition.x), ",", RoundForSignature(bodyPosition.y), ",", RoundForSignature(bodyPosition.z), "|",
                    RoundForSignature(cameraPosition.x), ",", RoundForSignature(cameraPosition.y), ",", RoundForSignature(cameraPosition.z), "|",
                    cameraFollowsBody ? "1" : "0", "|",
                    trainingOpenWorldVisualRoot != null ? "1" : "0");

                var idsChanged = bodyNetworkObjectId != lastObservedLocalBodyNetworkObjectId || cameraNetworkObjectId != lastObservedLocalCameraNetworkObjectId;
                var signatureChanged = !string.Equals(signature, lastObservedLocalRepresentationSignature, StringComparison.Ordinal);
                var intervalElapsed = Time.unscaledTime - lastRepresentationLogAt >= TrainingRepresentationLogIntervalSeconds;
                if (!idsChanged && (!signatureChanged || !intervalElapsed))
                {
                    return;
                }

                lastObservedLocalBodyNetworkObjectId = bodyNetworkObjectId;
                lastObservedLocalCameraNetworkObjectId = cameraNetworkObjectId;
                lastObservedLocalRepresentationSignature = signature;
                lastRepresentationLogAt = Time.unscaledTime;

                DraftUIPlugin.Log($"[CLIENT][TRAINING] Local representation player={(localPlayer.Username.Value.ToString() ?? "Player")} bodyId={bodyNetworkObjectId} cameraId={cameraNetworkObjectId} bodyPos={FormatVector3(bodyPosition)} cameraPos={FormatVector3(cameraPosition)} anchorDistance={anchorDistance:0.00} cameraFollowsBody={(cameraFollowsBody ? "yes" : "no")} visualsLoaded={(trainingOpenWorldVisualRoot != null ? "yes" : "no")} bundleLoaded={(trainingBundle != null ? "yes" : "no")}");
            }

            private static bool TryGetLocalPlayerRepresentation(out Player localPlayer, out PlayerBodyV2 playerBody, out PlayerCamera playerCamera)
            {
                localPlayer = null;
                playerBody = null;
                playerCamera = null;

                try
                {
                    var playerManager = PlayerManager.Instance;
                    if (!playerManager)
                    {
                        return false;
                    }

                    localPlayer = playerManager.GetLocalPlayer();
                    if (!localPlayer)
                    {
                        return false;
                    }

                    playerBody = localPlayer.PlayerBody;
                    playerCamera = localPlayer.PlayerCamera;
                    return true;
                }
                catch
                {
                    localPlayer = null;
                    playerBody = null;
                    playerCamera = null;
                    return false;
                }
            }

            private void SuppressExternalTrainingModIfNeeded(bool force = false)
            {
                if (!force && Time.unscaledTime - lastExternalTrainingSuppressionAt < ExternalTrainingSuppressionIntervalSeconds)
                {
                    return;
                }

                lastExternalTrainingSuppressionAt = Time.unscaledTime;

                try
                {
                    var destroyedAny = false;
                    var externalController = GameObject.Find(ExternalTrainingControllerObjectName);
                    if (externalController != null)
                    {
                        UnityEngine.Object.Destroy(externalController);
                        destroyedAny = true;
                    }

                    foreach (var behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
                    {
                        if (behaviour == null)
                        {
                            continue;
                        }

                        var behaviourType = behaviour.GetType();
                        var fullName = behaviourType.FullName ?? string.Empty;
                        if (!string.Equals(fullName, "MyPuckMod.PuckAttackBehaviour", StringComparison.Ordinal)
                            && !string.Equals(fullName, "PuckAttackBehaviour", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        UnityEngine.Object.Destroy(behaviour.gameObject);
                        destroyedAny = true;
                    }

                    if (destroyedAny)
                    {
                        DraftUIPlugin.Log("[CLIENT][TRAINING] Suppressed external MyPuckMod runtime so the training reset stays owned by SpeedRankeds only.");
                    }
                }
                catch (Exception ex)
                {
                    DraftUIPlugin.LogError($"[CLIENT][TRAINING] Failed to suppress external training mod runtime: {ex}");
                }
            }

            private static string RoundForSignature(float value)
            {
                return Mathf.Round(value * 10f).ToString();
            }

            private void ClearPendingAuthoritativeTrainingPose()
            {
                hasPendingAuthoritativePose = false;
                pendingOpenWorldActive = false;
                pendingAuthoritativePosition = Vector3.zero;
                pendingAuthoritativeRotation = Quaternion.identity;
                pendingAuthoritativeReason = string.Empty;
                pendingAuthoritativePoseExpiresAt = -999f;
                lastAuthoritativePoseApplyAt = -999f;
                lastAuthoritativePoseBodyNetworkObjectId = 0;
            }

            private static string FormatVector3(Vector3 value)
            {
                return $"({value.x:0.00},{value.y:0.00},{value.z:0.00})";
            }

            private static string FormatQuaternion(Quaternion value)
            {
                var euler = value.eulerAngles;
                return $"({euler.x:0.0},{euler.y:0.0},{euler.z:0.0})";
            }

            private static void AddChatMessage(string message)
            {
                try
                {
                    if (UIChat.Instance != null)
                    {
                        UIChat.Instance.AddChatMessage($"<color=#78d8ff>[Training]</color> {message}");
                    }
                }
                catch (Exception ex)
                {
                    DraftUIPlugin.LogError($"[CLIENT][TRAINING] Failed to write local training chat message: {ex}");
                }
            }
        }
    }
}