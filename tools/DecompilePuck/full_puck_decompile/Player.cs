using System;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour
{
	[Header("Prefabs")]
	[SerializeField]
	private PlayerCamera playerCameraPrefab;

	[SerializeField]
	private PlayerBodyV2 playerBodyAttackerPrefab;

	[SerializeField]
	private PlayerBodyV2 playerBodyGoaliePrefab;

	[SerializeField]
	private StickPositioner stickPositionerPrefab;

	[SerializeField]
	private SpectatorCamera spectatorCameraPrefab;

	[SerializeField]
	private Stick stickAttackerPrefab;

	[SerializeField]
	private Stick stickGoaliePrefab;

	[HideInInspector]
	public NetworkVariable<PlayerState> State = new NetworkVariable<PlayerState>(PlayerState.None);

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> Username = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<int> Number = new NetworkVariable<int>(0);

	[HideInInspector]
	public NetworkVariable<PlayerHandedness> Handedness = new NetworkVariable<PlayerHandedness>(PlayerHandedness.Left);

	[HideInInspector]
	public NetworkVariable<PlayerTeam> Team = new NetworkVariable<PlayerTeam>(PlayerTeam.None);

	[HideInInspector]
	public NetworkVariable<PlayerRole> Role = new NetworkVariable<PlayerRole>(PlayerRole.None);

	[HideInInspector]
	public NetworkVariable<int> Goals = new NetworkVariable<int>(0);

	[HideInInspector]
	public NetworkVariable<int> Assists = new NetworkVariable<int>(0);

	[HideInInspector]
	public NetworkVariable<int> Ping = new NetworkVariable<int>(0);

	[HideInInspector]
	public NetworkVariable<NetworkObjectReference> PlayerPositionReference = new NetworkVariable<NetworkObjectReference>();

	[HideInInspector]
	public NetworkVariable<bool> IsReplay = new NetworkVariable<bool>(value: false);

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> Country = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> VisorAttackerBlueSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> VisorAttackerRedSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> VisorGoalieBlueSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> VisorGoalieRedSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> Mustache = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> Beard = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> JerseyAttackerBlueSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> JerseyAttackerRedSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> JerseyGoalieBlueSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> JerseyGoalieRedSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> StickAttackerBlueSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> StickAttackerRedSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> StickGoalieBlueSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> StickGoalieRedSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> StickShaftAttackerBlueTapeSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> StickShaftAttackerRedTapeSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> StickShaftGoalieBlueTapeSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> StickShaftGoalieRedTapeSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> StickBladeAttackerBlueTapeSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> StickBladeAttackerRedTapeSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> StickBladeGoalieBlueTapeSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> StickBladeGoalieRedTapeSkin = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public NetworkVariable<int> PatreonLevel = new NetworkVariable<int>(0);

	[HideInInspector]
	public NetworkVariable<int> AdminLevel = new NetworkVariable<int>(0);

	[HideInInspector]
	public NetworkVariable<FixedString32Bytes> SteamId = new NetworkVariable<FixedString32Bytes>();

	[HideInInspector]
	public PlayerInput PlayerInput;

	[HideInInspector]
	public SpectatorCamera SpectatorCamera;

	[HideInInspector]
	public PlayerCamera PlayerCamera;

	[HideInInspector]
	public PlayerBodyV2 PlayerBody;

	[HideInInspector]
	public StickPositioner StickPositioner;

	[HideInInspector]
	public Stick Stick;

	[HideInInspector]
	public PlayerPosition PlayerPosition;

	private Tween delayedStateTween;

	public bool IsCharacterFullySpawned
	{
		get
		{
			if ((bool)PlayerCamera && (bool)PlayerBody && (bool)StickPositioner)
			{
				return Stick;
			}
			return false;
		}
	}

	public bool IsCharacterPartiallySpawned
	{
		get
		{
			if (!PlayerCamera && !PlayerBody && !StickPositioner)
			{
				return Stick;
			}
			return true;
		}
	}

	public bool IsSpectatorCameraSpawned => SpectatorCamera;

	private void Awake()
	{
		PlayerInput = GetComponent<PlayerInput>();
	}

	public override void OnNetworkSpawn()
	{
		State.Initialize(this);
		NetworkVariable<PlayerState> state = State;
		state.OnValueChanged = (NetworkVariable<PlayerState>.OnValueChangedDelegate)Delegate.Combine(state.OnValueChanged, new NetworkVariable<PlayerState>.OnValueChangedDelegate(OnPlayerStateChanged));
		Username.Initialize(this);
		NetworkVariable<FixedString32Bytes> username = Username;
		username.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(username.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerUsernameChanged));
		Number.Initialize(this);
		NetworkVariable<int> number = Number;
		number.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(number.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnPlayerNumberChanged));
		Handedness.Initialize(this);
		NetworkVariable<PlayerHandedness> handedness = Handedness;
		handedness.OnValueChanged = (NetworkVariable<PlayerHandedness>.OnValueChangedDelegate)Delegate.Combine(handedness.OnValueChanged, new NetworkVariable<PlayerHandedness>.OnValueChangedDelegate(OnPlayerHandednessChanged));
		Team.Initialize(this);
		NetworkVariable<PlayerTeam> team = Team;
		team.OnValueChanged = (NetworkVariable<PlayerTeam>.OnValueChangedDelegate)Delegate.Combine(team.OnValueChanged, new NetworkVariable<PlayerTeam>.OnValueChangedDelegate(OnPlayerTeamChanged));
		Role.Initialize(this);
		NetworkVariable<PlayerRole> role = Role;
		role.OnValueChanged = (NetworkVariable<PlayerRole>.OnValueChangedDelegate)Delegate.Combine(role.OnValueChanged, new NetworkVariable<PlayerRole>.OnValueChangedDelegate(OnPlayerRoleChanged));
		Goals.Initialize(this);
		NetworkVariable<int> goals = Goals;
		goals.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(goals.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnPlayerGoalsChanged));
		Assists.Initialize(this);
		NetworkVariable<int> assists = Assists;
		assists.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(assists.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnPlayerAssistsChanged));
		Ping.Initialize(this);
		NetworkVariable<int> ping = Ping;
		ping.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(ping.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnPlayerPingChanged));
		PlayerPositionReference.Initialize(this);
		NetworkVariable<NetworkObjectReference> playerPositionReference = PlayerPositionReference;
		playerPositionReference.OnValueChanged = (NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate)Delegate.Combine(playerPositionReference.OnValueChanged, new NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate(OnPlayerPlayerPositionReferenceChanged));
		Country.Initialize(this);
		NetworkVariable<FixedString32Bytes> country = Country;
		country.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(country.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerCountryChanged));
		VisorAttackerBlueSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> visorAttackerBlueSkin = VisorAttackerBlueSkin;
		visorAttackerBlueSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(visorAttackerBlueSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerVisorChanged));
		VisorAttackerRedSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> visorAttackerRedSkin = VisorAttackerRedSkin;
		visorAttackerRedSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(visorAttackerRedSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerVisorChanged));
		VisorGoalieBlueSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> visorGoalieBlueSkin = VisorGoalieBlueSkin;
		visorGoalieBlueSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(visorGoalieBlueSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerVisorChanged));
		VisorGoalieRedSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> visorGoalieRedSkin = VisorGoalieRedSkin;
		visorGoalieRedSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(visorGoalieRedSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerVisorChanged));
		Mustache.Initialize(this);
		NetworkVariable<FixedString32Bytes> mustache = Mustache;
		mustache.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(mustache.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerMustacheChanged));
		Beard.Initialize(this);
		NetworkVariable<FixedString32Bytes> beard = Beard;
		beard.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(beard.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerBeardChanged));
		JerseyAttackerBlueSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> jerseyAttackerBlueSkin = JerseyAttackerBlueSkin;
		jerseyAttackerBlueSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(jerseyAttackerBlueSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerJerseyChanged));
		JerseyAttackerRedSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> jerseyAttackerRedSkin = JerseyAttackerRedSkin;
		jerseyAttackerRedSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(jerseyAttackerRedSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerJerseyChanged));
		JerseyGoalieBlueSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> jerseyGoalieBlueSkin = JerseyGoalieBlueSkin;
		jerseyGoalieBlueSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(jerseyGoalieBlueSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerJerseyChanged));
		JerseyGoalieRedSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> jerseyGoalieRedSkin = JerseyGoalieRedSkin;
		jerseyGoalieRedSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(jerseyGoalieRedSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerJerseyChanged));
		StickAttackerBlueSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> stickAttackerBlueSkin = StickAttackerBlueSkin;
		stickAttackerBlueSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(stickAttackerBlueSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickSkinChanged));
		StickAttackerRedSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> stickAttackerRedSkin = StickAttackerRedSkin;
		stickAttackerRedSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(stickAttackerRedSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickSkinChanged));
		StickGoalieBlueSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> stickGoalieBlueSkin = StickGoalieBlueSkin;
		stickGoalieBlueSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(stickGoalieBlueSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickSkinChanged));
		StickGoalieRedSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> stickGoalieRedSkin = StickGoalieRedSkin;
		stickGoalieRedSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(stickGoalieRedSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickSkinChanged));
		StickShaftAttackerBlueTapeSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> stickShaftAttackerBlueTapeSkin = StickShaftAttackerBlueTapeSkin;
		stickShaftAttackerBlueTapeSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(stickShaftAttackerBlueTapeSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickShaftTapeSkinChanged));
		StickShaftAttackerRedTapeSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> stickShaftAttackerRedTapeSkin = StickShaftAttackerRedTapeSkin;
		stickShaftAttackerRedTapeSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(stickShaftAttackerRedTapeSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickShaftTapeSkinChanged));
		StickShaftGoalieBlueTapeSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> stickShaftGoalieBlueTapeSkin = StickShaftGoalieBlueTapeSkin;
		stickShaftGoalieBlueTapeSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(stickShaftGoalieBlueTapeSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickShaftTapeSkinChanged));
		StickShaftGoalieRedTapeSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> stickShaftGoalieRedTapeSkin = StickShaftGoalieRedTapeSkin;
		stickShaftGoalieRedTapeSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(stickShaftGoalieRedTapeSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickShaftTapeSkinChanged));
		StickBladeAttackerBlueTapeSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> stickBladeAttackerBlueTapeSkin = StickBladeAttackerBlueTapeSkin;
		stickBladeAttackerBlueTapeSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(stickBladeAttackerBlueTapeSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickBladeTapeSkinChanged));
		StickBladeAttackerRedTapeSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> stickBladeAttackerRedTapeSkin = StickBladeAttackerRedTapeSkin;
		stickBladeAttackerRedTapeSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(stickBladeAttackerRedTapeSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickBladeTapeSkinChanged));
		StickBladeGoalieBlueTapeSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> stickBladeGoalieBlueTapeSkin = StickBladeGoalieBlueTapeSkin;
		stickBladeGoalieBlueTapeSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(stickBladeGoalieBlueTapeSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickBladeTapeSkinChanged));
		StickBladeGoalieRedTapeSkin.Initialize(this);
		NetworkVariable<FixedString32Bytes> stickBladeGoalieRedTapeSkin = StickBladeGoalieRedTapeSkin;
		stickBladeGoalieRedTapeSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(stickBladeGoalieRedTapeSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickBladeTapeSkinChanged));
		PatreonLevel.Initialize(this);
		NetworkVariable<int> patreonLevel = PatreonLevel;
		patreonLevel.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(patreonLevel.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnPlayerPatreonLevelChanged));
		AdminLevel.Initialize(this);
		NetworkVariable<int> adminLevel = AdminLevel;
		adminLevel.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(adminLevel.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnPlayerAdminLevelChanged));
		SteamId.Initialize(this);
		NetworkVariable<FixedString32Bytes> steamId = SteamId;
		steamId.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Combine(steamId.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerSteamIdChanged));
		Client_InitializeNetworkVariables();
		base.OnNetworkSpawn();
	}

	protected override void OnNetworkPostSpawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerSpawned", new Dictionary<string, object> { { "player", this } });
		if (base.IsOwner)
		{
			Client_PlayerSubscriptionRpc(MonoBehaviourSingleton<StateManager>.Instance.PlayerData.username, MonoBehaviourSingleton<StateManager>.Instance.PlayerData.number, (!(MonoBehaviourSingleton<SettingsManager>.Instance.Handedness == "LEFT")) ? PlayerHandedness.Right : PlayerHandedness.Left, MonoBehaviourSingleton<SettingsManager>.Instance.Country, MonoBehaviourSingleton<SettingsManager>.Instance.VisorAttackerBlueSkin, MonoBehaviourSingleton<SettingsManager>.Instance.VisorAttackerRedSkin, MonoBehaviourSingleton<SettingsManager>.Instance.VisorGoalieBlueSkin, MonoBehaviourSingleton<SettingsManager>.Instance.VisorGoalieRedSkin, MonoBehaviourSingleton<SettingsManager>.Instance.Mustache, MonoBehaviourSingleton<SettingsManager>.Instance.Beard, MonoBehaviourSingleton<SettingsManager>.Instance.JerseyAttackerBlueSkin, MonoBehaviourSingleton<SettingsManager>.Instance.JerseyAttackerRedSkin, MonoBehaviourSingleton<SettingsManager>.Instance.JerseyGoalieBlueSkin, MonoBehaviourSingleton<SettingsManager>.Instance.JerseyGoalieRedSkin, MonoBehaviourSingleton<SettingsManager>.Instance.StickAttackerBlueSkin, MonoBehaviourSingleton<SettingsManager>.Instance.StickAttackerRedSkin, MonoBehaviourSingleton<SettingsManager>.Instance.StickGoalieBlueSkin, MonoBehaviourSingleton<SettingsManager>.Instance.StickGoalieRedSkin, MonoBehaviourSingleton<SettingsManager>.Instance.StickShaftAttackerBlueTapeSkin, MonoBehaviourSingleton<SettingsManager>.Instance.StickShaftAttackerRedTapeSkin, MonoBehaviourSingleton<SettingsManager>.Instance.StickShaftGoalieBlueTapeSkin, MonoBehaviourSingleton<SettingsManager>.Instance.StickShaftGoalieRedTapeSkin, MonoBehaviourSingleton<SettingsManager>.Instance.StickBladeAttackerBlueTapeSkin, MonoBehaviourSingleton<SettingsManager>.Instance.StickBladeAttackerRedTapeSkin, MonoBehaviourSingleton<SettingsManager>.Instance.StickBladeGoalieBlueTapeSkin, MonoBehaviourSingleton<SettingsManager>.Instance.StickBladeGoalieRedTapeSkin, MonoBehaviourSingleton<StateManager>.Instance.PlayerData.patreonLevel, MonoBehaviourSingleton<StateManager>.Instance.PlayerData.adminLevel, MonoBehaviourSingleton<StateManager>.Instance.PlayerData.steamId, MonoBehaviourSingleton<ModManagerV2>.Instance.EnabledModIds);
		}
		base.OnNetworkPostSpawn();
	}

	protected override void OnNetworkSessionSynchronized()
	{
		Client_InitializeNetworkVariables();
		base.OnNetworkSessionSynchronized();
	}

	public override void OnNetworkDespawn()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			delayedStateTween?.Kill();
			if (IsCharacterPartiallySpawned)
			{
				Server_DespawnCharacter();
			}
			if (IsSpectatorCameraSpawned)
			{
				Server_DespawnSpectatorCamera();
			}
		}
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerDespawned", new Dictionary<string, object> { { "player", this } });
		NetworkVariable<PlayerState> state = State;
		state.OnValueChanged = (NetworkVariable<PlayerState>.OnValueChangedDelegate)Delegate.Remove(state.OnValueChanged, new NetworkVariable<PlayerState>.OnValueChangedDelegate(OnPlayerStateChanged));
		State.Dispose();
		NetworkVariable<FixedString32Bytes> username = Username;
		username.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(username.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerUsernameChanged));
		Username.Dispose();
		NetworkVariable<int> number = Number;
		number.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(number.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnPlayerNumberChanged));
		Number.Dispose();
		NetworkVariable<PlayerHandedness> handedness = Handedness;
		handedness.OnValueChanged = (NetworkVariable<PlayerHandedness>.OnValueChangedDelegate)Delegate.Remove(handedness.OnValueChanged, new NetworkVariable<PlayerHandedness>.OnValueChangedDelegate(OnPlayerHandednessChanged));
		Handedness.Dispose();
		NetworkVariable<PlayerTeam> team = Team;
		team.OnValueChanged = (NetworkVariable<PlayerTeam>.OnValueChangedDelegate)Delegate.Remove(team.OnValueChanged, new NetworkVariable<PlayerTeam>.OnValueChangedDelegate(OnPlayerTeamChanged));
		Team.Dispose();
		NetworkVariable<PlayerRole> role = Role;
		role.OnValueChanged = (NetworkVariable<PlayerRole>.OnValueChangedDelegate)Delegate.Remove(role.OnValueChanged, new NetworkVariable<PlayerRole>.OnValueChangedDelegate(OnPlayerRoleChanged));
		Role.Dispose();
		NetworkVariable<int> goals = Goals;
		goals.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(goals.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnPlayerGoalsChanged));
		Goals.Dispose();
		NetworkVariable<int> assists = Assists;
		assists.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(assists.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnPlayerAssistsChanged));
		Assists.Dispose();
		NetworkVariable<int> ping = Ping;
		ping.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(ping.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnPlayerPingChanged));
		Ping.Dispose();
		NetworkVariable<NetworkObjectReference> playerPositionReference = PlayerPositionReference;
		playerPositionReference.OnValueChanged = (NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate)Delegate.Remove(playerPositionReference.OnValueChanged, new NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate(OnPlayerPlayerPositionReferenceChanged));
		PlayerPositionReference.Dispose();
		NetworkVariable<FixedString32Bytes> country = Country;
		country.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(country.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerCountryChanged));
		Country.Dispose();
		NetworkVariable<FixedString32Bytes> visorAttackerBlueSkin = VisorAttackerBlueSkin;
		visorAttackerBlueSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(visorAttackerBlueSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerVisorChanged));
		VisorAttackerBlueSkin.Dispose();
		NetworkVariable<FixedString32Bytes> visorAttackerRedSkin = VisorAttackerRedSkin;
		visorAttackerRedSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(visorAttackerRedSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerVisorChanged));
		VisorAttackerRedSkin.Dispose();
		NetworkVariable<FixedString32Bytes> visorGoalieBlueSkin = VisorGoalieBlueSkin;
		visorGoalieBlueSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(visorGoalieBlueSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerVisorChanged));
		VisorGoalieBlueSkin.Dispose();
		NetworkVariable<FixedString32Bytes> visorGoalieRedSkin = VisorGoalieRedSkin;
		visorGoalieRedSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(visorGoalieRedSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerVisorChanged));
		VisorGoalieRedSkin.Dispose();
		NetworkVariable<FixedString32Bytes> mustache = Mustache;
		mustache.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(mustache.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerMustacheChanged));
		Mustache.Dispose();
		NetworkVariable<FixedString32Bytes> beard = Beard;
		beard.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(beard.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerBeardChanged));
		Beard.Dispose();
		NetworkVariable<FixedString32Bytes> jerseyAttackerBlueSkin = JerseyAttackerBlueSkin;
		jerseyAttackerBlueSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(jerseyAttackerBlueSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerJerseyChanged));
		JerseyAttackerBlueSkin.Dispose();
		NetworkVariable<FixedString32Bytes> jerseyAttackerRedSkin = JerseyAttackerRedSkin;
		jerseyAttackerRedSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(jerseyAttackerRedSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerJerseyChanged));
		JerseyAttackerRedSkin.Dispose();
		NetworkVariable<FixedString32Bytes> jerseyGoalieBlueSkin = JerseyGoalieBlueSkin;
		jerseyGoalieBlueSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(jerseyGoalieBlueSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerJerseyChanged));
		JerseyGoalieBlueSkin.Dispose();
		NetworkVariable<FixedString32Bytes> jerseyGoalieRedSkin = JerseyGoalieRedSkin;
		jerseyGoalieRedSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(jerseyGoalieRedSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerJerseyChanged));
		JerseyGoalieRedSkin.Dispose();
		NetworkVariable<FixedString32Bytes> stickAttackerBlueSkin = StickAttackerBlueSkin;
		stickAttackerBlueSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(stickAttackerBlueSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickSkinChanged));
		StickAttackerBlueSkin.Dispose();
		NetworkVariable<FixedString32Bytes> stickAttackerRedSkin = StickAttackerRedSkin;
		stickAttackerRedSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(stickAttackerRedSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickSkinChanged));
		StickAttackerRedSkin.Dispose();
		NetworkVariable<FixedString32Bytes> stickGoalieBlueSkin = StickGoalieBlueSkin;
		stickGoalieBlueSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(stickGoalieBlueSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickSkinChanged));
		StickGoalieBlueSkin.Dispose();
		NetworkVariable<FixedString32Bytes> stickGoalieRedSkin = StickGoalieRedSkin;
		stickGoalieRedSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(stickGoalieRedSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickSkinChanged));
		StickGoalieRedSkin.Dispose();
		NetworkVariable<FixedString32Bytes> stickShaftAttackerBlueTapeSkin = StickShaftAttackerBlueTapeSkin;
		stickShaftAttackerBlueTapeSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(stickShaftAttackerBlueTapeSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickShaftTapeSkinChanged));
		StickShaftAttackerBlueTapeSkin.Dispose();
		NetworkVariable<FixedString32Bytes> stickShaftAttackerRedTapeSkin = StickShaftAttackerRedTapeSkin;
		stickShaftAttackerRedTapeSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(stickShaftAttackerRedTapeSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickShaftTapeSkinChanged));
		StickShaftAttackerRedTapeSkin.Dispose();
		NetworkVariable<FixedString32Bytes> stickShaftGoalieBlueTapeSkin = StickShaftGoalieBlueTapeSkin;
		stickShaftGoalieBlueTapeSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(stickShaftGoalieBlueTapeSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickShaftTapeSkinChanged));
		StickShaftGoalieBlueTapeSkin.Dispose();
		NetworkVariable<FixedString32Bytes> stickShaftGoalieRedTapeSkin = StickShaftGoalieRedTapeSkin;
		stickShaftGoalieRedTapeSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(stickShaftGoalieRedTapeSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickShaftTapeSkinChanged));
		StickShaftGoalieRedTapeSkin.Dispose();
		NetworkVariable<FixedString32Bytes> stickBladeAttackerBlueTapeSkin = StickBladeAttackerBlueTapeSkin;
		stickBladeAttackerBlueTapeSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(stickBladeAttackerBlueTapeSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickBladeTapeSkinChanged));
		StickBladeAttackerBlueTapeSkin.Dispose();
		NetworkVariable<FixedString32Bytes> stickBladeAttackerRedTapeSkin = StickBladeAttackerRedTapeSkin;
		stickBladeAttackerRedTapeSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(stickBladeAttackerRedTapeSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickBladeTapeSkinChanged));
		StickBladeAttackerRedTapeSkin.Dispose();
		NetworkVariable<FixedString32Bytes> stickBladeGoalieBlueTapeSkin = StickBladeGoalieBlueTapeSkin;
		stickBladeGoalieBlueTapeSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(stickBladeGoalieBlueTapeSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickBladeTapeSkinChanged));
		StickBladeGoalieBlueTapeSkin.Dispose();
		NetworkVariable<FixedString32Bytes> stickBladeGoalieRedTapeSkin = StickBladeGoalieRedTapeSkin;
		stickBladeGoalieRedTapeSkin.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(stickBladeGoalieRedTapeSkin.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerStickBladeTapeSkinChanged));
		StickBladeGoalieRedTapeSkin.Dispose();
		NetworkVariable<int> patreonLevel = PatreonLevel;
		patreonLevel.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(patreonLevel.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnPlayerPatreonLevelChanged));
		PatreonLevel.Dispose();
		NetworkVariable<int> adminLevel = AdminLevel;
		adminLevel.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(adminLevel.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnPlayerAdminLevelChanged));
		AdminLevel.Dispose();
		NetworkVariable<FixedString32Bytes> steamId = SteamId;
		steamId.OnValueChanged = (NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate)Delegate.Remove(steamId.OnValueChanged, new NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate(OnPlayerSteamIdChanged));
		SteamId.Dispose();
		Debug.Log($"[Player] Despawned player ({base.OwnerClientId})");
		base.OnNetworkDespawn();
	}

	public FixedString32Bytes GetPlayerJerseySkin()
	{
		switch (Team.Value)
		{
		case PlayerTeam.Blue:
			if (Role.Value != PlayerRole.Attacker)
			{
				return JerseyGoalieBlueSkin.Value;
			}
			return JerseyAttackerBlueSkin.Value;
		case PlayerTeam.Red:
			if (Role.Value != PlayerRole.Attacker)
			{
				return JerseyGoalieRedSkin.Value;
			}
			return JerseyAttackerRedSkin.Value;
		default:
			return default(FixedString32Bytes);
		}
	}

	public FixedString32Bytes GetPlayerVisorSkin()
	{
		switch (Team.Value)
		{
		case PlayerTeam.Blue:
			if (Role.Value != PlayerRole.Attacker)
			{
				return VisorGoalieBlueSkin.Value;
			}
			return VisorAttackerBlueSkin.Value;
		case PlayerTeam.Red:
			if (Role.Value != PlayerRole.Attacker)
			{
				return VisorGoalieRedSkin.Value;
			}
			return VisorAttackerRedSkin.Value;
		default:
			return default(FixedString32Bytes);
		}
	}

	public FixedString32Bytes GetPlayerStickSkin()
	{
		switch (Team.Value)
		{
		case PlayerTeam.Blue:
			if (Role.Value != PlayerRole.Attacker)
			{
				return StickGoalieBlueSkin.Value;
			}
			return StickAttackerBlueSkin.Value;
		case PlayerTeam.Red:
			if (Role.Value != PlayerRole.Attacker)
			{
				return StickGoalieRedSkin.Value;
			}
			return StickAttackerRedSkin.Value;
		default:
			return default(FixedString32Bytes);
		}
	}

	public FixedString32Bytes GetPlayerStickShaftTapeSkin()
	{
		switch (Team.Value)
		{
		case PlayerTeam.Blue:
			if (Role.Value != PlayerRole.Attacker)
			{
				return StickShaftGoalieBlueTapeSkin.Value;
			}
			return StickShaftAttackerBlueTapeSkin.Value;
		case PlayerTeam.Red:
			if (Role.Value != PlayerRole.Attacker)
			{
				return StickShaftGoalieRedTapeSkin.Value;
			}
			return StickShaftAttackerRedTapeSkin.Value;
		default:
			return default(FixedString32Bytes);
		}
	}

	public FixedString32Bytes GetPlayerStickBladeTapeSkin()
	{
		switch (Team.Value)
		{
		case PlayerTeam.Blue:
			if (Role.Value != PlayerRole.Attacker)
			{
				return StickBladeGoalieBlueTapeSkin.Value;
			}
			return StickBladeAttackerBlueTapeSkin.Value;
		case PlayerTeam.Red:
			if (Role.Value != PlayerRole.Attacker)
			{
				return StickBladeGoalieRedTapeSkin.Value;
			}
			return StickBladeAttackerRedTapeSkin.Value;
		default:
			return default(FixedString32Bytes);
		}
	}

	private void OnPlayerStateChanged(PlayerState oldState, PlayerState newState)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerStateChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldState", oldState },
			{ "newState", newState }
		});
	}

	private void OnPlayerUsernameChanged(FixedString32Bytes oldUsername, FixedString32Bytes newUsername)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerUsernameChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldUsername", oldUsername },
			{ "newUsername", newUsername }
		});
	}

	private void OnPlayerNumberChanged(int oldNumber, int newNumber)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerNumberChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldNumber", oldNumber },
			{ "newNumber", newNumber }
		});
	}

	private void OnPlayerHandednessChanged(PlayerHandedness oldHandedness, PlayerHandedness newHandedness)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerHandednessChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldHandedness", oldHandedness },
			{ "newHandedness", newHandedness }
		});
	}

	private void OnPlayerTeamChanged(PlayerTeam oldTeam, PlayerTeam newTeam)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerTeamChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldTeam", oldTeam },
			{ "newTeam", newTeam }
		});
	}

	private void OnPlayerRoleChanged(PlayerRole oldRole, PlayerRole newRole)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerRoleChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldRole", oldRole },
			{ "newRole", newRole }
		});
	}

	private void OnPlayerGoalsChanged(int oldGoals, int newGoals)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerGoalsChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldGoals", oldGoals },
			{ "newGoals", newGoals }
		});
	}

	private void OnPlayerAssistsChanged(int oldAssists, int newAssists)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerAssistsChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldAssists", oldAssists },
			{ "newAssists", newAssists }
		});
	}

	private void OnPlayerPingChanged(int oldPing, int newPing)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerPingChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldPing", oldPing },
			{ "newPing", newPing }
		});
	}

	private void OnPlayerPlayerPositionReferenceChanged(NetworkObjectReference oldPlayerPositionReference, NetworkObjectReference newPlayerPositionReference)
	{
		PlayerPosition playerPosition = PlayerPosition;
		if (newPlayerPositionReference.TryGet(out var networkObject))
		{
			PlayerPosition = networkObject.GetComponent<PlayerPosition>();
		}
		else
		{
			PlayerPosition = null;
		}
		OnPlayerPositionChanged(playerPosition, PlayerPosition);
	}

	private void OnPlayerPositionChanged(PlayerPosition oldPlayerPosition, PlayerPosition newPlayerPosition)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerPositionChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldPlayerPosition", oldPlayerPosition },
			{ "newPlayerPosition", newPlayerPosition }
		});
	}

	private void OnPlayerCountryChanged(FixedString32Bytes oldCountry, FixedString32Bytes newCountry)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerCountryChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldCountry", oldCountry },
			{ "newCountry", newCountry }
		});
	}

	private void OnPlayerVisorChanged(FixedString32Bytes oldVisor, FixedString32Bytes newVisor)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerVisorChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldVisor", oldVisor },
			{ "newVisor", newVisor }
		});
	}

	private void OnPlayerMustacheChanged(FixedString32Bytes oldMustache, FixedString32Bytes newMustache)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerMustacheChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldMustache", oldMustache },
			{ "newMustache", newMustache }
		});
	}

	private void OnPlayerBeardChanged(FixedString32Bytes oldBeard, FixedString32Bytes newBeard)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerBeardChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldBeard", oldBeard },
			{ "newBeard", newBeard }
		});
	}

	private void OnPlayerJerseyChanged(FixedString32Bytes oldJersey, FixedString32Bytes newJersey)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerJerseyChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldJersey", oldJersey },
			{ "newJersey", newJersey }
		});
	}

	private void OnPlayerStickSkinChanged(FixedString32Bytes oldStickGoalieSkin, FixedString32Bytes newStickGoalieSkin)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerStickSkinChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldStickGoalieSkin", oldStickGoalieSkin },
			{ "newStickGoalieSkin", newStickGoalieSkin }
		});
	}

	private void OnPlayerStickShaftTapeSkinChanged(FixedString32Bytes oldStickShaftTapeSkin, FixedString32Bytes newStickShaftTapeSkin)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerStickShaftTapeSkinChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldStickShaftTapeSkin", oldStickShaftTapeSkin },
			{ "newStickShaftTapeSkin", newStickShaftTapeSkin }
		});
	}

	private void OnPlayerStickBladeTapeSkinChanged(FixedString32Bytes oldStickBladeTapeSkin, FixedString32Bytes newStickBladeTapeSkin)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerStickBladeTapeSkinChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldStickBladeTapeSkin", oldStickBladeTapeSkin },
			{ "newStickBladeTapeSkin", newStickBladeTapeSkin }
		});
	}

	private void OnPlayerPatreonLevelChanged(int oldPatreonLevel, int newPatreonLevel)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerPatreonLevelChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldPatreonLevel", oldPatreonLevel },
			{ "newPatreonLevel", newPatreonLevel }
		});
	}

	private void OnPlayerAdminLevelChanged(int oldAdminLevel, int newAdminLevel)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerAdminLevelChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldAdminLevel", oldAdminLevel },
			{ "newAdminLevel", newAdminLevel }
		});
	}

	private void OnPlayerSteamIdChanged(FixedString32Bytes oldSteamId, FixedString32Bytes newSteamId)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerSteamIdChanged", new Dictionary<string, object>
		{
			{ "player", this },
			{ "oldSteamId", oldSteamId },
			{ "newSteamId", newSteamId }
		});
	}

	public void Server_SpawnCharacter(Vector3 position, Quaternion rotation, PlayerRole role)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Server_SpawnPlayerBody(position, rotation, role);
			Server_SpawnStick(StickPositioner.RaycastOriginPosition, Quaternion.LookRotation(PlayerBody.transform.right, Vector3.up), role);
			Debug.Log($"[Player] Spawned character for {Username.Value} ({base.OwnerClientId})");
		}
	}

	public void Server_SpawnPlayerCamera(PlayerBodyV2 playerBody)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			PlayerCamera playerCamera = UnityEngine.Object.Instantiate(playerCameraPrefab);
			playerCamera.PlayerReference.Value = new NetworkObjectReference(base.NetworkObject);
			playerCamera.NetworkObject.SpawnWithOwnership(base.OwnerClientId);
			playerCamera.NetworkObject.TrySetParent(playerBody.transform, worldPositionStays: false);
			Debug.Log($"[Player] Spawned PlayerCamera for {Username.Value} ({base.OwnerClientId})");
		}
	}

	public void Server_SpawnPlayerBody(Vector3 position, Quaternion rotation, PlayerRole role)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			PlayerBodyV2 original = ((role != PlayerRole.Goalie) ? playerBodyAttackerPrefab : playerBodyGoaliePrefab);
			PlayerBodyV2 playerBodyV = UnityEngine.Object.Instantiate(original, position, rotation);
			playerBodyV.PlayerReference.Value = new NetworkObjectReference(base.NetworkObject);
			playerBodyV.NetworkObject.SpawnWithOwnership(base.OwnerClientId);
			playerBodyV.NetworkObject.TrySetParent(base.gameObject.transform);
			Debug.Log($"[Player] Spawned PlayerBody for {Username.Value} ({base.OwnerClientId})");
			Server_SpawnPlayerCamera(playerBodyV);
			Server_SpawnStickPositioner(playerBodyV);
		}
	}

	public void Server_SpawnStickPositioner(PlayerBodyV2 playerBody)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			StickPositioner stickPositioner = UnityEngine.Object.Instantiate(stickPositionerPrefab);
			stickPositioner.PlayerReference.Value = new NetworkObjectReference(base.NetworkObject);
			stickPositioner.NetworkObject.SpawnWithOwnership(base.OwnerClientId);
			stickPositioner.NetworkObject.TrySetParent(playerBody.transform, worldPositionStays: false);
			Debug.Log($"[Player] Spawned StickPositioner for {Username.Value} ({base.OwnerClientId})");
		}
	}

	public void Server_SpawnStick(Vector3 position, Quaternion rotation, PlayerRole role)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Stick original = ((role != PlayerRole.Goalie) ? stickAttackerPrefab : stickGoaliePrefab);
			Stick stick = UnityEngine.Object.Instantiate(original, position, rotation);
			stick.PlayerReference.Value = new NetworkObjectReference(base.NetworkObject);
			stick.NetworkObject.SpawnWithOwnership(base.OwnerClientId);
			stick.NetworkObject.TrySetParent(base.gameObject.transform);
			Debug.Log($"[Player] Spawned Stick for {Username.Value} ({base.OwnerClientId})");
		}
	}

	public void Server_DespawnCharacter()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Server_DespawnPlayerBody();
			Server_DespawnStick();
			Debug.Log($"[Player] Despawned character for ({base.OwnerClientId})");
		}
	}

	public void Server_DespawnPlayerCamera()
	{
		if (NetworkManager.Singleton.IsServer && (bool)PlayerCamera && PlayerCamera.NetworkObject.IsSpawned)
		{
			PlayerCamera.NetworkObject.Despawn();
			Debug.Log($"[Player] Despawned PlayerCamera for ({base.OwnerClientId})");
		}
	}

	public void Server_DespawnPlayerBody()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Server_DespawnPlayerCamera();
			Server_DespawnStickPositioner();
			if ((bool)PlayerBody && PlayerBody.NetworkObject.IsSpawned)
			{
				PlayerBody.NetworkObject.Despawn();
				Debug.Log($"[Player] Despawned PlayerBody for ({base.OwnerClientId})");
			}
		}
	}

	public void Server_DespawnStickPositioner()
	{
		if (NetworkManager.Singleton.IsServer && (bool)StickPositioner && StickPositioner.NetworkObject.IsSpawned)
		{
			StickPositioner.NetworkObject.Despawn();
			Debug.Log($"[Player] Despawned StickPositioner for ({base.OwnerClientId})");
		}
	}

	public void Server_DespawnStick()
	{
		if (NetworkManager.Singleton.IsServer && (bool)Stick && Stick.NetworkObject.IsSpawned)
		{
			Stick.NetworkObject.Despawn();
			Debug.Log($"[Player] Despawned Stick for ({base.OwnerClientId})");
		}
	}

	public void Server_RespawnCharacter(Vector3 position, Quaternion rotation, PlayerRole role)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			if (IsCharacterPartiallySpawned)
			{
				Server_DespawnCharacter();
				Server_SpawnCharacter(position, rotation, role);
				Debug.Log($"[Player] Respawned character for {Username.Value} ({base.OwnerClientId})");
			}
			else
			{
				Server_SpawnCharacter(position, rotation, role);
			}
		}
	}

	public void Server_SpawnSpectatorCamera(Vector3 position, Quaternion rotation)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			SpectatorCamera spectatorCamera = UnityEngine.Object.Instantiate(spectatorCameraPrefab, position, rotation);
			spectatorCamera.PlayerReference.Value = new NetworkObjectReference(base.NetworkObject);
			spectatorCamera.NetworkObject.SpawnWithOwnership(base.OwnerClientId);
			spectatorCamera.NetworkObject.TrySetParent(base.gameObject.transform);
			Debug.Log($"[Player] Spawned spectator camera for {Username.Value} ({base.OwnerClientId})");
		}
	}

	public void Server_RespawnSpectatorCamera(Vector3 position, Quaternion rotation)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			if (IsSpectatorCameraSpawned)
			{
				Server_DespawnSpectatorCamera();
				Server_SpawnSpectatorCamera(position, rotation);
				Debug.Log($"[Player] Respawned spectator camera for {Username.Value} ({base.OwnerClientId})");
			}
			else
			{
				Server_SpawnSpectatorCamera(position, rotation);
			}
		}
	}

	public void Server_DespawnSpectatorCamera()
	{
		if (NetworkManager.Singleton.IsServer && (bool)SpectatorCamera && SpectatorCamera.NetworkObject.IsSpawned)
		{
			SpectatorCamera.NetworkObject.Despawn();
			Debug.Log($"[Player] Despawned spectator camera for {Username.Value} ({base.OwnerClientId})");
		}
	}

	public void Server_GoalScored()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Goals.Value++;
		}
	}

	public void Server_AssistScored()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Assists.Value++;
		}
	}

	public void Server_ResetPoints()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Goals.Value = 0;
			Assists.Value = 0;
		}
	}

	public void Server_UpdatePing()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Ping.Value = (int)NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(base.OwnerClientId);
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_SetPlayerStateRpc(PlayerState state, float delay = 0f)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
			RpcParams rpcParams = default(RpcParams);
			FastBufferWriter bufferWriter = __beginSendRpc(2891939837u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in state, default(FastBufferWriter.ForEnums));
			bufferWriter.WriteValueSafe(in delay, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 2891939837u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		PlayerState state2 = state;
		delayedStateTween?.Kill();
		if (delay > 0f)
		{
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnPlayerStateDelayed", new Dictionary<string, object>
			{
				{ "player", this },
				{ "oldState", State.Value },
				{ "newState", state2 },
				{ "delay", delay }
			});
			delayedStateTween = DOVirtual.DelayedCall(delay, delegate
			{
				Client_SetPlayerStateRpc(state2);
			});
			return;
		}
		switch (state2)
		{
		case PlayerState.TeamSelect:
			Server_DespawnCharacter();
			Server_DespawnSpectatorCamera();
			Team.Value = PlayerTeam.None;
			break;
		case PlayerState.PositionSelectBlue:
		case PlayerState.PositionSelectRed:
			if (Team.Value == PlayerTeam.None || Team.Value == PlayerTeam.Spectator)
			{
				return;
			}
			Server_DespawnCharacter();
			Server_DespawnSpectatorCamera();
			if ((bool)PlayerPosition)
			{
				PlayerPosition.Server_Unclaim();
			}
			break;
		case PlayerState.Play:
			if (Team.Value == PlayerTeam.None || Team.Value == PlayerTeam.Spectator)
			{
				return;
			}
			if ((bool)PlayerPosition)
			{
				Server_RespawnCharacter(PlayerPosition.transform.position, PlayerPosition.transform.rotation, Role.Value);
			}
			break;
		case PlayerState.Replay:
			if (Team.Value == PlayerTeam.None || Team.Value == PlayerTeam.Spectator)
			{
				return;
			}
			Server_DespawnCharacter();
			break;
		case PlayerState.Spectate:
			Server_DespawnCharacter();
			Server_RespawnSpectatorCamera(Vector3.zero + Vector3.up, Quaternion.identity);
			break;
		}
		State.Value = state2;
	}

	[Rpc(SendTo.Server)]
	public void Client_SetPlayerUsernameRpc(FixedString32Bytes username)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(946455295u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in username, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 946455295u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Username.Value = username;
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_SetPlayerNumberRpc(int number)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(2614219144u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, number);
				__endSendRpc(ref bufferWriter, 2614219144u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Number.Value = number;
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_SetPlayerHandednessRpc(PlayerHandedness handedness)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(1024064498u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in handedness, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 1024064498u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Handedness.Value = handedness;
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_SetPlayerTeamRpc(PlayerTeam team)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(2680549476u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in team, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 2680549476u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Team.Value = team;
			}
		}
	}

	[Rpc(SendTo.Server)]
	private void Client_PlayerSubscriptionRpc(FixedString32Bytes username, int number, PlayerHandedness handedness, FixedString32Bytes country, FixedString32Bytes visorAttackerBlueSkin, FixedString32Bytes visorAttackerRedSkin, FixedString32Bytes visorGoalieBlueSkin, FixedString32Bytes visorGoalieRedSkin, FixedString32Bytes mustache, FixedString32Bytes beard, FixedString32Bytes jerseyAttackerBlueSkin, FixedString32Bytes jerseyAttackerRedSkin, FixedString32Bytes jerseyGoalieBlueSkin, FixedString32Bytes jerseyGoalieRedSkin, FixedString32Bytes stickAttackerBlueSkin, FixedString32Bytes stickAttackerRedSkin, FixedString32Bytes stickGoalieBlueSkin, FixedString32Bytes stickGoalieRedSkin, FixedString32Bytes stickShaftAttackerBlueTapeSkin, FixedString32Bytes stickShaftAttackerRedTapeSkin, FixedString32Bytes stickShaftGoalieBlueTapeSkin, FixedString32Bytes stickShaftGoalieRedTapeSkin, FixedString32Bytes stickBladeAttackerBlueTapeSkin, FixedString32Bytes stickBladeAttackerRedTapeSkin, FixedString32Bytes stickBladeGoalieBlueTapeSkin, FixedString32Bytes stickBladeGoalieRedTapeSkin, int patreonLevel, int adminLevel, FixedString32Bytes steamId, ulong[] enabledModIds)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
			RpcParams rpcParams = default(RpcParams);
			FastBufferWriter bufferWriter = __beginSendRpc(1379186733u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in username, default(FastBufferWriter.ForFixedStrings));
			BytePacker.WriteValueBitPacked(bufferWriter, number);
			bufferWriter.WriteValueSafe(in handedness, default(FastBufferWriter.ForEnums));
			bufferWriter.WriteValueSafe(in country, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in visorAttackerBlueSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in visorAttackerRedSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in visorGoalieBlueSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in visorGoalieRedSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in mustache, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in beard, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in jerseyAttackerBlueSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in jerseyAttackerRedSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in jerseyGoalieBlueSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in jerseyGoalieRedSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in stickAttackerBlueSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in stickAttackerRedSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in stickGoalieBlueSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in stickGoalieRedSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in stickShaftAttackerBlueTapeSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in stickShaftAttackerRedTapeSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in stickShaftGoalieBlueTapeSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in stickShaftGoalieRedTapeSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in stickBladeAttackerBlueTapeSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in stickBladeAttackerRedTapeSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in stickBladeGoalieBlueTapeSkin, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in stickBladeGoalieRedTapeSkin, default(FastBufferWriter.ForFixedStrings));
			BytePacker.WriteValueBitPacked(bufferWriter, patreonLevel);
			BytePacker.WriteValueBitPacked(bufferWriter, adminLevel);
			bufferWriter.WriteValueSafe(in steamId, default(FastBufferWriter.ForFixedStrings));
			bool value = enabledModIds != null;
			bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			if (value)
			{
				bufferWriter.WriteValueSafe(enabledModIds, default(FastBufferWriter.ForPrimitives));
			}
			__endSendRpc(ref bufferWriter, 1379186733u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			Username.Value = username;
			Number.Value = number;
			Handedness.Value = handedness;
			Country.Value = country;
			VisorAttackerBlueSkin.Value = visorAttackerBlueSkin;
			VisorAttackerRedSkin.Value = visorAttackerRedSkin;
			VisorGoalieBlueSkin.Value = visorGoalieBlueSkin;
			VisorGoalieRedSkin.Value = visorGoalieRedSkin;
			Mustache.Value = mustache;
			Beard.Value = beard;
			JerseyAttackerBlueSkin.Value = jerseyAttackerBlueSkin;
			JerseyAttackerRedSkin.Value = jerseyAttackerRedSkin;
			JerseyGoalieBlueSkin.Value = jerseyGoalieBlueSkin;
			JerseyGoalieRedSkin.Value = jerseyGoalieRedSkin;
			StickAttackerBlueSkin.Value = stickAttackerBlueSkin;
			StickAttackerRedSkin.Value = stickAttackerRedSkin;
			StickGoalieBlueSkin.Value = stickGoalieBlueSkin;
			StickGoalieRedSkin.Value = stickGoalieRedSkin;
			StickShaftAttackerBlueTapeSkin.Value = stickShaftAttackerBlueTapeSkin;
			StickShaftAttackerRedTapeSkin.Value = stickShaftAttackerRedTapeSkin;
			StickShaftGoalieBlueTapeSkin.Value = stickShaftGoalieBlueTapeSkin;
			StickShaftGoalieRedTapeSkin.Value = stickShaftGoalieRedTapeSkin;
			StickBladeAttackerBlueTapeSkin.Value = stickBladeAttackerBlueTapeSkin;
			StickBladeAttackerRedTapeSkin.Value = stickBladeAttackerRedTapeSkin;
			StickBladeGoalieBlueTapeSkin.Value = stickBladeGoalieBlueTapeSkin;
			StickBladeGoalieRedTapeSkin.Value = stickBladeGoalieRedTapeSkin;
			PatreonLevel.Value = patreonLevel;
			AdminLevel.Value = adminLevel;
			SteamId.Value = steamId;
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnPlayerSubscription", new Dictionary<string, object> { { "player", this } });
			Debug.Log($"[Player] Received subscription from {username} ({base.OwnerClientId})");
		}
	}

	public void Client_InitializeNetworkVariables()
	{
		OnPlayerStateChanged(State.Value, State.Value);
		OnPlayerUsernameChanged(Username.Value, Username.Value);
		OnPlayerNumberChanged(Number.Value, Number.Value);
		OnPlayerHandednessChanged(Handedness.Value, Handedness.Value);
		OnPlayerTeamChanged(Team.Value, Team.Value);
		OnPlayerRoleChanged(Role.Value, Role.Value);
		OnPlayerGoalsChanged(Goals.Value, Goals.Value);
		OnPlayerAssistsChanged(Assists.Value, Assists.Value);
		OnPlayerPingChanged(Ping.Value, Ping.Value);
		OnPlayerPlayerPositionReferenceChanged(PlayerPositionReference.Value, PlayerPositionReference.Value);
		OnPlayerCountryChanged(Country.Value, Country.Value);
		switch (Team.Value)
		{
		case PlayerTeam.Blue:
			switch (Role.Value)
			{
			case PlayerRole.Attacker:
				OnPlayerVisorChanged(VisorAttackerBlueSkin.Value, VisorAttackerBlueSkin.Value);
				break;
			case PlayerRole.Goalie:
				OnPlayerVisorChanged(VisorGoalieBlueSkin.Value, VisorGoalieBlueSkin.Value);
				break;
			}
			break;
		case PlayerTeam.Red:
			switch (Role.Value)
			{
			case PlayerRole.Attacker:
				OnPlayerVisorChanged(VisorAttackerRedSkin.Value, VisorAttackerRedSkin.Value);
				break;
			case PlayerRole.Goalie:
				OnPlayerVisorChanged(VisorGoalieRedSkin.Value, VisorGoalieRedSkin.Value);
				break;
			}
			break;
		}
		OnPlayerMustacheChanged(Mustache.Value, Mustache.Value);
		OnPlayerBeardChanged(Beard.Value, Beard.Value);
		switch (Team.Value)
		{
		case PlayerTeam.Blue:
			switch (Role.Value)
			{
			case PlayerRole.Attacker:
				OnPlayerJerseyChanged(JerseyAttackerBlueSkin.Value, JerseyAttackerBlueSkin.Value);
				break;
			case PlayerRole.Goalie:
				OnPlayerJerseyChanged(JerseyGoalieBlueSkin.Value, JerseyGoalieBlueSkin.Value);
				break;
			}
			break;
		case PlayerTeam.Red:
			switch (Role.Value)
			{
			case PlayerRole.Attacker:
				OnPlayerJerseyChanged(JerseyAttackerRedSkin.Value, JerseyAttackerRedSkin.Value);
				break;
			case PlayerRole.Goalie:
				OnPlayerJerseyChanged(JerseyGoalieRedSkin.Value, JerseyGoalieRedSkin.Value);
				break;
			}
			break;
		}
		switch (Team.Value)
		{
		case PlayerTeam.Blue:
			switch (Role.Value)
			{
			case PlayerRole.Attacker:
				OnPlayerStickSkinChanged(StickAttackerBlueSkin.Value, StickAttackerBlueSkin.Value);
				break;
			case PlayerRole.Goalie:
				OnPlayerStickSkinChanged(StickGoalieBlueSkin.Value, StickGoalieBlueSkin.Value);
				break;
			}
			break;
		case PlayerTeam.Red:
			switch (Role.Value)
			{
			case PlayerRole.Attacker:
				OnPlayerStickSkinChanged(StickAttackerRedSkin.Value, StickAttackerRedSkin.Value);
				break;
			case PlayerRole.Goalie:
				OnPlayerStickSkinChanged(StickGoalieRedSkin.Value, StickGoalieRedSkin.Value);
				break;
			}
			break;
		}
		switch (Team.Value)
		{
		case PlayerTeam.Blue:
			switch (Role.Value)
			{
			case PlayerRole.Attacker:
				OnPlayerStickShaftTapeSkinChanged(StickShaftAttackerBlueTapeSkin.Value, StickShaftAttackerBlueTapeSkin.Value);
				break;
			case PlayerRole.Goalie:
				OnPlayerStickShaftTapeSkinChanged(StickShaftGoalieBlueTapeSkin.Value, StickShaftGoalieBlueTapeSkin.Value);
				break;
			}
			break;
		case PlayerTeam.Red:
			switch (Role.Value)
			{
			case PlayerRole.Attacker:
				OnPlayerStickShaftTapeSkinChanged(StickShaftAttackerRedTapeSkin.Value, StickShaftAttackerRedTapeSkin.Value);
				break;
			case PlayerRole.Goalie:
				OnPlayerStickShaftTapeSkinChanged(StickShaftGoalieRedTapeSkin.Value, StickShaftGoalieRedTapeSkin.Value);
				break;
			}
			break;
		}
		switch (Team.Value)
		{
		case PlayerTeam.Blue:
			switch (Role.Value)
			{
			case PlayerRole.Attacker:
				OnPlayerStickBladeTapeSkinChanged(StickBladeAttackerBlueTapeSkin.Value, StickBladeAttackerBlueTapeSkin.Value);
				break;
			case PlayerRole.Goalie:
				OnPlayerStickBladeTapeSkinChanged(StickBladeGoalieBlueTapeSkin.Value, StickBladeGoalieBlueTapeSkin.Value);
				break;
			}
			break;
		case PlayerTeam.Red:
			switch (Role.Value)
			{
			case PlayerRole.Attacker:
				OnPlayerStickBladeTapeSkinChanged(StickBladeAttackerRedTapeSkin.Value, StickBladeAttackerRedTapeSkin.Value);
				break;
			case PlayerRole.Goalie:
				OnPlayerStickBladeTapeSkinChanged(StickBladeGoalieRedTapeSkin.Value, StickBladeGoalieRedTapeSkin.Value);
				break;
			}
			break;
		}
		OnPlayerPatreonLevelChanged(PatreonLevel.Value, PatreonLevel.Value);
		OnPlayerAdminLevelChanged(AdminLevel.Value, AdminLevel.Value);
		OnPlayerSteamIdChanged(SteamId.Value, SteamId.Value);
	}

	protected override void __initializeVariables()
	{
		if (State == null)
		{
			throw new Exception("Player.State cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		State.Initialize(this);
		__nameNetworkVariable(State, "State");
		NetworkVariableFields.Add(State);
		if (Username == null)
		{
			throw new Exception("Player.Username cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		Username.Initialize(this);
		__nameNetworkVariable(Username, "Username");
		NetworkVariableFields.Add(Username);
		if (Number == null)
		{
			throw new Exception("Player.Number cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		Number.Initialize(this);
		__nameNetworkVariable(Number, "Number");
		NetworkVariableFields.Add(Number);
		if (Handedness == null)
		{
			throw new Exception("Player.Handedness cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		Handedness.Initialize(this);
		__nameNetworkVariable(Handedness, "Handedness");
		NetworkVariableFields.Add(Handedness);
		if (Team == null)
		{
			throw new Exception("Player.Team cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		Team.Initialize(this);
		__nameNetworkVariable(Team, "Team");
		NetworkVariableFields.Add(Team);
		if (Role == null)
		{
			throw new Exception("Player.Role cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		Role.Initialize(this);
		__nameNetworkVariable(Role, "Role");
		NetworkVariableFields.Add(Role);
		if (Goals == null)
		{
			throw new Exception("Player.Goals cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		Goals.Initialize(this);
		__nameNetworkVariable(Goals, "Goals");
		NetworkVariableFields.Add(Goals);
		if (Assists == null)
		{
			throw new Exception("Player.Assists cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		Assists.Initialize(this);
		__nameNetworkVariable(Assists, "Assists");
		NetworkVariableFields.Add(Assists);
		if (Ping == null)
		{
			throw new Exception("Player.Ping cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		Ping.Initialize(this);
		__nameNetworkVariable(Ping, "Ping");
		NetworkVariableFields.Add(Ping);
		if (PlayerPositionReference == null)
		{
			throw new Exception("Player.PlayerPositionReference cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		PlayerPositionReference.Initialize(this);
		__nameNetworkVariable(PlayerPositionReference, "PlayerPositionReference");
		NetworkVariableFields.Add(PlayerPositionReference);
		if (IsReplay == null)
		{
			throw new Exception("Player.IsReplay cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		IsReplay.Initialize(this);
		__nameNetworkVariable(IsReplay, "IsReplay");
		NetworkVariableFields.Add(IsReplay);
		if (Country == null)
		{
			throw new Exception("Player.Country cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		Country.Initialize(this);
		__nameNetworkVariable(Country, "Country");
		NetworkVariableFields.Add(Country);
		if (VisorAttackerBlueSkin == null)
		{
			throw new Exception("Player.VisorAttackerBlueSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		VisorAttackerBlueSkin.Initialize(this);
		__nameNetworkVariable(VisorAttackerBlueSkin, "VisorAttackerBlueSkin");
		NetworkVariableFields.Add(VisorAttackerBlueSkin);
		if (VisorAttackerRedSkin == null)
		{
			throw new Exception("Player.VisorAttackerRedSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		VisorAttackerRedSkin.Initialize(this);
		__nameNetworkVariable(VisorAttackerRedSkin, "VisorAttackerRedSkin");
		NetworkVariableFields.Add(VisorAttackerRedSkin);
		if (VisorGoalieBlueSkin == null)
		{
			throw new Exception("Player.VisorGoalieBlueSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		VisorGoalieBlueSkin.Initialize(this);
		__nameNetworkVariable(VisorGoalieBlueSkin, "VisorGoalieBlueSkin");
		NetworkVariableFields.Add(VisorGoalieBlueSkin);
		if (VisorGoalieRedSkin == null)
		{
			throw new Exception("Player.VisorGoalieRedSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		VisorGoalieRedSkin.Initialize(this);
		__nameNetworkVariable(VisorGoalieRedSkin, "VisorGoalieRedSkin");
		NetworkVariableFields.Add(VisorGoalieRedSkin);
		if (Mustache == null)
		{
			throw new Exception("Player.Mustache cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		Mustache.Initialize(this);
		__nameNetworkVariable(Mustache, "Mustache");
		NetworkVariableFields.Add(Mustache);
		if (Beard == null)
		{
			throw new Exception("Player.Beard cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		Beard.Initialize(this);
		__nameNetworkVariable(Beard, "Beard");
		NetworkVariableFields.Add(Beard);
		if (JerseyAttackerBlueSkin == null)
		{
			throw new Exception("Player.JerseyAttackerBlueSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		JerseyAttackerBlueSkin.Initialize(this);
		__nameNetworkVariable(JerseyAttackerBlueSkin, "JerseyAttackerBlueSkin");
		NetworkVariableFields.Add(JerseyAttackerBlueSkin);
		if (JerseyAttackerRedSkin == null)
		{
			throw new Exception("Player.JerseyAttackerRedSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		JerseyAttackerRedSkin.Initialize(this);
		__nameNetworkVariable(JerseyAttackerRedSkin, "JerseyAttackerRedSkin");
		NetworkVariableFields.Add(JerseyAttackerRedSkin);
		if (JerseyGoalieBlueSkin == null)
		{
			throw new Exception("Player.JerseyGoalieBlueSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		JerseyGoalieBlueSkin.Initialize(this);
		__nameNetworkVariable(JerseyGoalieBlueSkin, "JerseyGoalieBlueSkin");
		NetworkVariableFields.Add(JerseyGoalieBlueSkin);
		if (JerseyGoalieRedSkin == null)
		{
			throw new Exception("Player.JerseyGoalieRedSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		JerseyGoalieRedSkin.Initialize(this);
		__nameNetworkVariable(JerseyGoalieRedSkin, "JerseyGoalieRedSkin");
		NetworkVariableFields.Add(JerseyGoalieRedSkin);
		if (StickAttackerBlueSkin == null)
		{
			throw new Exception("Player.StickAttackerBlueSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		StickAttackerBlueSkin.Initialize(this);
		__nameNetworkVariable(StickAttackerBlueSkin, "StickAttackerBlueSkin");
		NetworkVariableFields.Add(StickAttackerBlueSkin);
		if (StickAttackerRedSkin == null)
		{
			throw new Exception("Player.StickAttackerRedSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		StickAttackerRedSkin.Initialize(this);
		__nameNetworkVariable(StickAttackerRedSkin, "StickAttackerRedSkin");
		NetworkVariableFields.Add(StickAttackerRedSkin);
		if (StickGoalieBlueSkin == null)
		{
			throw new Exception("Player.StickGoalieBlueSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		StickGoalieBlueSkin.Initialize(this);
		__nameNetworkVariable(StickGoalieBlueSkin, "StickGoalieBlueSkin");
		NetworkVariableFields.Add(StickGoalieBlueSkin);
		if (StickGoalieRedSkin == null)
		{
			throw new Exception("Player.StickGoalieRedSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		StickGoalieRedSkin.Initialize(this);
		__nameNetworkVariable(StickGoalieRedSkin, "StickGoalieRedSkin");
		NetworkVariableFields.Add(StickGoalieRedSkin);
		if (StickShaftAttackerBlueTapeSkin == null)
		{
			throw new Exception("Player.StickShaftAttackerBlueTapeSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		StickShaftAttackerBlueTapeSkin.Initialize(this);
		__nameNetworkVariable(StickShaftAttackerBlueTapeSkin, "StickShaftAttackerBlueTapeSkin");
		NetworkVariableFields.Add(StickShaftAttackerBlueTapeSkin);
		if (StickShaftAttackerRedTapeSkin == null)
		{
			throw new Exception("Player.StickShaftAttackerRedTapeSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		StickShaftAttackerRedTapeSkin.Initialize(this);
		__nameNetworkVariable(StickShaftAttackerRedTapeSkin, "StickShaftAttackerRedTapeSkin");
		NetworkVariableFields.Add(StickShaftAttackerRedTapeSkin);
		if (StickShaftGoalieBlueTapeSkin == null)
		{
			throw new Exception("Player.StickShaftGoalieBlueTapeSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		StickShaftGoalieBlueTapeSkin.Initialize(this);
		__nameNetworkVariable(StickShaftGoalieBlueTapeSkin, "StickShaftGoalieBlueTapeSkin");
		NetworkVariableFields.Add(StickShaftGoalieBlueTapeSkin);
		if (StickShaftGoalieRedTapeSkin == null)
		{
			throw new Exception("Player.StickShaftGoalieRedTapeSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		StickShaftGoalieRedTapeSkin.Initialize(this);
		__nameNetworkVariable(StickShaftGoalieRedTapeSkin, "StickShaftGoalieRedTapeSkin");
		NetworkVariableFields.Add(StickShaftGoalieRedTapeSkin);
		if (StickBladeAttackerBlueTapeSkin == null)
		{
			throw new Exception("Player.StickBladeAttackerBlueTapeSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		StickBladeAttackerBlueTapeSkin.Initialize(this);
		__nameNetworkVariable(StickBladeAttackerBlueTapeSkin, "StickBladeAttackerBlueTapeSkin");
		NetworkVariableFields.Add(StickBladeAttackerBlueTapeSkin);
		if (StickBladeAttackerRedTapeSkin == null)
		{
			throw new Exception("Player.StickBladeAttackerRedTapeSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		StickBladeAttackerRedTapeSkin.Initialize(this);
		__nameNetworkVariable(StickBladeAttackerRedTapeSkin, "StickBladeAttackerRedTapeSkin");
		NetworkVariableFields.Add(StickBladeAttackerRedTapeSkin);
		if (StickBladeGoalieBlueTapeSkin == null)
		{
			throw new Exception("Player.StickBladeGoalieBlueTapeSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		StickBladeGoalieBlueTapeSkin.Initialize(this);
		__nameNetworkVariable(StickBladeGoalieBlueTapeSkin, "StickBladeGoalieBlueTapeSkin");
		NetworkVariableFields.Add(StickBladeGoalieBlueTapeSkin);
		if (StickBladeGoalieRedTapeSkin == null)
		{
			throw new Exception("Player.StickBladeGoalieRedTapeSkin cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		StickBladeGoalieRedTapeSkin.Initialize(this);
		__nameNetworkVariable(StickBladeGoalieRedTapeSkin, "StickBladeGoalieRedTapeSkin");
		NetworkVariableFields.Add(StickBladeGoalieRedTapeSkin);
		if (PatreonLevel == null)
		{
			throw new Exception("Player.PatreonLevel cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		PatreonLevel.Initialize(this);
		__nameNetworkVariable(PatreonLevel, "PatreonLevel");
		NetworkVariableFields.Add(PatreonLevel);
		if (AdminLevel == null)
		{
			throw new Exception("Player.AdminLevel cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		AdminLevel.Initialize(this);
		__nameNetworkVariable(AdminLevel, "AdminLevel");
		NetworkVariableFields.Add(AdminLevel);
		if (SteamId == null)
		{
			throw new Exception("Player.SteamId cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		SteamId.Initialize(this);
		__nameNetworkVariable(SteamId, "SteamId");
		NetworkVariableFields.Add(SteamId);
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		__registerRpc(2891939837u, __rpc_handler_2891939837, "Client_SetPlayerStateRpc");
		__registerRpc(946455295u, __rpc_handler_946455295, "Client_SetPlayerUsernameRpc");
		__registerRpc(2614219144u, __rpc_handler_2614219144, "Client_SetPlayerNumberRpc");
		__registerRpc(1024064498u, __rpc_handler_1024064498, "Client_SetPlayerHandednessRpc");
		__registerRpc(2680549476u, __rpc_handler_2680549476, "Client_SetPlayerTeamRpc");
		__registerRpc(1379186733u, __rpc_handler_1379186733, "Client_PlayerSubscriptionRpc");
		base.__initializeRpcs();
	}

	private static void __rpc_handler_2891939837(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out PlayerState value, default(FastBufferWriter.ForEnums));
			reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((Player)target).Client_SetPlayerStateRpc(value, value2);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_946455295(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out FixedString32Bytes value, default(FastBufferWriter.ForFixedStrings));
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((Player)target).Client_SetPlayerUsernameRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2614219144(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out int value);
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((Player)target).Client_SetPlayerNumberRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1024064498(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out PlayerHandedness value, default(FastBufferWriter.ForEnums));
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((Player)target).Client_SetPlayerHandednessRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2680549476(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out PlayerTeam value, default(FastBufferWriter.ForEnums));
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((Player)target).Client_SetPlayerTeamRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1379186733(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out FixedString32Bytes value, default(FastBufferWriter.ForFixedStrings));
			ByteUnpacker.ReadValueBitPacked(reader, out int value2);
			reader.ReadValueSafe(out PlayerHandedness value3, default(FastBufferWriter.ForEnums));
			reader.ReadValueSafe(out FixedString32Bytes value4, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value5, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value6, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value7, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value8, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value9, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value10, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value11, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value12, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value13, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value14, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value15, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value16, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value17, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value18, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value19, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value20, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value21, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value22, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value23, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value24, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value25, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString32Bytes value26, default(FastBufferWriter.ForFixedStrings));
			ByteUnpacker.ReadValueBitPacked(reader, out int value27);
			ByteUnpacker.ReadValueBitPacked(reader, out int value28);
			reader.ReadValueSafe(out FixedString32Bytes value29, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out bool value30, default(FastBufferWriter.ForPrimitives));
			ulong[] value31 = null;
			if (value30)
			{
				reader.ReadValueSafe(out value31, default(FastBufferWriter.ForPrimitives));
			}
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((Player)target).Client_PlayerSubscriptionRpc(value, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15, value16, value17, value18, value19, value20, value21, value22, value23, value24, value25, value26, value27, value28, value29, value31);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	protected internal override string __getTypeName()
	{
		return "Player";
	}
}
