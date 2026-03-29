using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LevelManager : NetworkBehaviourSingleton<LevelManager>
{
	[Header("References")]
	[Space(20f)]
	[SerializeField]
	private BaseCamera observerCamera;

	[SerializeField]
	private BaseCamera bluePositionSelectionCamera;

	[SerializeField]
	private BaseCamera redPositionSelectionCamera;

	[SerializeField]
	private BaseCamera replayCamera;

	[Space(20f)]
	[SerializeField]
	private MeshRenderer iceMeshRenderer;

	[Space(20f)]
	[SerializeField]
	private List<SynchronizedAudio> crowdCheerAudioSources;

	[SerializeField]
	private SynchronizedAudio periodHornAudioSource;

	[SerializeField]
	private SynchronizedAudio blueGoalAudioSource;

	[SerializeField]
	private SynchronizedAudio redGoalAudioSource;

	[Space(20f)]
	[SerializeField]
	private Light blueGoalLight;

	[SerializeField]
	private Light redGoalLight;

	[Space(20f)]
	[SerializeField]
	private List<PuckPosition> puckPositions = new List<PuckPosition>();

	[Space(20f)]
	[SerializeField]
	private List<PlayerPosition> playerBluePositions;

	[SerializeField]
	private List<PlayerPosition> playerRedPositions;

	[SerializeField]
	private List<Transform> spectatorPositions = new List<Transform>();

	[SerializeField]
	private PuckShooter puckShooter;

	private float initialBlueGoalLightIntensity;

	private float initialRedGoalLightIntensity;

	public PuckShooter PuckShooter => puckShooter;

	public Bounds IceBounds => iceMeshRenderer.bounds;

	public override void Awake()
	{
		base.Awake();
		DestroyOnLoad();
		initialBlueGoalLightIntensity = blueGoalLight.intensity;
		initialRedGoalLightIntensity = redGoalLight.intensity;
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnLevelStarted", new Dictionary<string, object>
		{
			{ "puckPositions", puckPositions },
			{ "playerBluePositions", playerBluePositions },
			{ "playerRedPositions", playerRedPositions },
			{ "spectatorPositions", spectatorPositions }
		});
	}

	protected override void OnNetworkPostSpawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnLevelSpawned");
		base.OnNetworkPostSpawn();
	}

	public override void OnNetworkDespawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnLevelDespawned");
		base.OnNetworkDespawn();
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnLevelDestroyed");
		base.OnDestroy();
	}

	public void Client_DisableAllCameras()
	{
		observerCamera.Disable();
		bluePositionSelectionCamera.Disable();
		redPositionSelectionCamera.Disable();
		replayCamera.Disable();
	}

	public void Client_EnableObserverCamera()
	{
		Client_DisableAllCameras();
		observerCamera.Enable();
	}

	public void Client_EnableBluePositionSelectionCamera()
	{
		Client_DisableAllCameras();
		bluePositionSelectionCamera.Enable();
	}

	public void Client_EnableRedPositionSelectionCamera()
	{
		Client_DisableAllCameras();
		redPositionSelectionCamera.Enable();
	}

	public void Client_EnableReplayCamera()
	{
		Client_DisableAllCameras();
		replayCamera.Enable();
	}

	public void Server_PlayPeriodHornSound()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			periodHornAudioSource.Server_Play();
		}
	}

	public void Server_PlayTeamBlueGoalSound()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			blueGoalAudioSource.Server_Play();
		}
	}

	public void Server_PlayTeamRedGoalSound()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			redGoalAudioSource.Server_Play();
		}
	}

	public void Server_PlayCheerSound()
	{
		if (!NetworkManager.Singleton.IsServer)
		{
			return;
		}
		foreach (SynchronizedAudio crowdCheerAudioSource in crowdCheerAudioSources)
		{
			crowdCheerAudioSource.Server_Play(-1f, -1f, isOneShot: false, -1, 0f, randomClip: true, randomTime: true, fadeIn: true, 0.5f, fadeOut: true, 5f, 15f);
		}
	}

	public void Client_ActivateBlueGoalLight()
	{
		Client_DeactivateGoalLights();
		blueGoalLight.intensity = initialBlueGoalLightIntensity;
	}

	public void Client_ActivateRedGoalLight()
	{
		Client_DeactivateGoalLights();
		redGoalLight.intensity = initialRedGoalLightIntensity;
	}

	public void Client_DeactivateGoalLights()
	{
		blueGoalLight.intensity = 0f;
		redGoalLight.intensity = 0f;
	}

	protected override void __initializeVariables()
	{
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		base.__initializeRpcs();
	}

	protected internal override string __getTypeName()
	{
		return "LevelManager";
	}
}
