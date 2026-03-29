using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

public class ReplayPlayer : NetworkBehaviour
{
	[HideInInspector]
	public bool IsReplaying;

	[HideInInspector]
	public int TickRate = 15;

	[HideInInspector]
	public int Tick;

	[HideInInspector]
	public SortedList<int, List<(string, object)>> EventMap = new SortedList<int, List<(string, object)>>();

	private float tickAccumulator;

	private Dictionary<ulong, ulong> replayPuckNetworkObjectIdMap = new Dictionary<ulong, ulong>();

	private List<ReplayPlayerSpawned> replayPlayerSpawnedList = new List<ReplayPlayerSpawned>();

	private List<ReplayPlayerBodySpawned> replayPlayerBodySpawnedList = new List<ReplayPlayerBodySpawned>();

	private List<ReplayStickSpawned> replayStickSpawnedList = new List<ReplayStickSpawned>();

	private List<ReplayPuckSpawned> replayPuckSpawnedList = new List<ReplayPuckSpawned>();

	[HideInInspector]
	public float TickInterval => 1f / (float)TickRate;

	private void Update()
	{
		if (!base.IsSpawned || !NetworkManager.Singleton.IsServer || !IsReplaying)
		{
			return;
		}
		tickAccumulator += Time.deltaTime * (float)TickRate;
		if (tickAccumulator >= 1f)
		{
			while (tickAccumulator >= 1f)
			{
				tickAccumulator -= 1f;
			}
			Server_Tick(Tick);
			Tick++;
		}
	}

	public void Server_StartReplay(SortedList<int, List<(string, object)>> tickEventMap, int tickRate, int fromTick = 0)
	{
		if (NetworkManager.Singleton.IsServer && !IsReplaying && tickEventMap.Keys.Count != 0)
		{
			int num = tickEventMap.Keys.Min();
			int max = tickEventMap.Keys.Max();
			fromTick = Mathf.Clamp(fromTick, num, max);
			EventMap = tickEventMap;
			TickRate = tickRate;
			int i;
			for (i = num; i < fromTick; i++)
			{
				Server_Tick(i, skip: true);
				EventMap.Remove(i);
			}
			Tick = i;
			List<(string, object)> list = Server_GetSkippedEvents();
			if (list.Count > 0)
			{
				Tick--;
				EventMap.Add(Tick, list);
			}
			IsReplaying = true;
		}
	}

	public void Server_StopReplay()
	{
		if (NetworkManager.Singleton.IsServer && IsReplaying)
		{
			Debug.Log("[REPLAY PLAYER] Replay playback stopped");
			IsReplaying = false;
			Server_Dispose();
		}
	}

	private void Server_Tick(int tick, bool skip = false)
	{
		if (!NetworkManager.Singleton.IsServer)
		{
			return;
		}
		if (tick > EventMap.Keys.Max())
		{
			Server_StopReplay();
		}
		else
		{
			if (!EventMap.ContainsKey(tick))
			{
				return;
			}
			foreach (var (eventName, eventData) in EventMap[tick])
			{
				if (skip)
				{
					Server_SkipEvent(eventName, eventData);
				}
				else
				{
					Server_ReplayEvent(eventName, eventData);
				}
			}
		}
	}

	private List<(string, object)> Server_GetSkippedEvents()
	{
		List<(string, object)> list = new List<(string, object)>();
		foreach (ReplayPlayerSpawned replayPlayerSpawned in replayPlayerSpawnedList)
		{
			list.Add(("PlayerSpawned", replayPlayerSpawned));
		}
		foreach (ReplayPlayerBodySpawned replayPlayerBodySpawned in replayPlayerBodySpawnedList)
		{
			list.Add(("PlayerBodySpawned", replayPlayerBodySpawned));
		}
		foreach (ReplayStickSpawned replayStickSpawned in replayStickSpawnedList)
		{
			list.Add(("StickSpawned", replayStickSpawned));
		}
		foreach (ReplayPuckSpawned replayPuckSpawned in replayPuckSpawnedList)
		{
			list.Add(("PuckSpawned", replayPuckSpawned));
		}
		return list;
	}

	private void Server_SkipEvent(string eventName, object eventData)
	{
		if (!NetworkManager.Singleton.IsServer)
		{
			return;
		}
		switch (eventName)
		{
		case "PlayerSpawned":
		{
			ReplayPlayerSpawned item4 = (ReplayPlayerSpawned)eventData;
			replayPlayerSpawnedList.Add(item4);
			break;
		}
		case "PlayerDespawned":
		{
			ReplayPlayerDespawned playerDespawned = (ReplayPlayerDespawned)eventData;
			replayPlayerSpawnedList.RemoveAll((ReplayPlayerSpawned x) => x.OwnerClientId == playerDespawned.OwnerClientId);
			break;
		}
		case "PlayerBodySpawned":
		{
			ReplayPlayerBodySpawned item3 = (ReplayPlayerBodySpawned)eventData;
			replayPlayerBodySpawnedList.Add(item3);
			break;
		}
		case "PlayerBodyDespawned":
		{
			ReplayPlayerBodyDespawned playerBodyDespawned = (ReplayPlayerBodyDespawned)eventData;
			replayPlayerBodySpawnedList.RemoveAll((ReplayPlayerBodySpawned x) => x.OwnerClientId == playerBodyDespawned.OwnerClientId);
			break;
		}
		case "StickSpawned":
		{
			ReplayStickSpawned item2 = (ReplayStickSpawned)eventData;
			replayStickSpawnedList.Add(item2);
			break;
		}
		case "StickDespawned":
		{
			ReplayStickDespawned stickDespawned = (ReplayStickDespawned)eventData;
			replayStickSpawnedList.RemoveAll((ReplayStickSpawned x) => x.OwnerClientId == stickDespawned.OwnerClientId);
			break;
		}
		case "PuckSpawned":
		{
			ReplayPuckSpawned item = (ReplayPuckSpawned)eventData;
			replayPuckSpawnedList.Add(item);
			break;
		}
		case "PuckDespawned":
		{
			ReplayPuckDespawned puckDespawned = (ReplayPuckDespawned)eventData;
			replayPuckSpawnedList.RemoveAll((ReplayPuckSpawned x) => x.NetworkObjectId == puckDespawned.NetworkObjectId);
			break;
		}
		}
	}

	private void Server_ReplayEvent(string eventName, object eventData)
	{
		if (!NetworkManager.Singleton.IsServer)
		{
			return;
		}
		switch (eventName)
		{
		case "PlayerSpawned":
		{
			ReplayPlayerSpawned replayPlayerSpawned = (ReplayPlayerSpawned)eventData;
			NetworkBehaviourSingleton<PlayerManager>.Instance.Server_SpawnPlayer(1337 + replayPlayerSpawned.OwnerClientId, isReplay: true);
			break;
		}
		case "PlayerInput":
		{
			ReplayPlayerInput replayPlayerInput = (ReplayPlayerInput)eventData;
			Player replayPlayerByClientId6 = NetworkBehaviourSingleton<PlayerManager>.Instance.GetReplayPlayerByClientId(replayPlayerInput.OwnerClientId);
			if ((bool)replayPlayerByClientId6)
			{
				replayPlayerByClientId6.PlayerInput.LookAngleInput.ClientValue = replayPlayerInput.LookAngleInput;
				replayPlayerByClientId6.PlayerInput.BladeAngleInput.ClientValue = replayPlayerInput.BladeAngleInput;
				replayPlayerByClientId6.PlayerInput.TrackInput.ClientValue = replayPlayerInput.TrackInput;
				replayPlayerByClientId6.PlayerInput.LookInput.ClientValue = replayPlayerInput.LookInput;
			}
			break;
		}
		case "PlayerDespawned":
		{
			ReplayPlayerDespawned replayPlayerDespawned = (ReplayPlayerDespawned)eventData;
			Player replayPlayerByClientId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetReplayPlayerByClientId(replayPlayerDespawned.OwnerClientId);
			if ((bool)replayPlayerByClientId)
			{
				replayPlayerByClientId.NetworkObject.Despawn();
				Debug.Log($"[ReplayPlayer] Despawned replay player {replayPlayerByClientId.OwnerClientId}");
			}
			break;
		}
		case "PlayerBodySpawned":
		{
			ReplayPlayerBodySpawned replayPlayerBodySpawned = (ReplayPlayerBodySpawned)eventData;
			Player replayPlayerByClientId5 = NetworkBehaviourSingleton<PlayerManager>.Instance.GetReplayPlayerByClientId(replayPlayerBodySpawned.OwnerClientId);
			if ((bool)replayPlayerByClientId5)
			{
				replayPlayerByClientId5.Username.Value = replayPlayerBodySpawned.Username;
				replayPlayerByClientId5.Number.Value = replayPlayerBodySpawned.Number;
				replayPlayerByClientId5.Team.Value = replayPlayerBodySpawned.Team;
				replayPlayerByClientId5.Role.Value = replayPlayerBodySpawned.Role;
				replayPlayerByClientId5.Country.Value = replayPlayerBodySpawned.Country;
				replayPlayerByClientId5.VisorAttackerBlueSkin.Value = replayPlayerBodySpawned.VisorAttackerBlueSkin;
				replayPlayerByClientId5.VisorAttackerRedSkin.Value = replayPlayerBodySpawned.VisorAttackerRedSkin;
				replayPlayerByClientId5.VisorGoalieBlueSkin.Value = replayPlayerBodySpawned.VisorGoalieBlueSkin;
				replayPlayerByClientId5.VisorGoalieRedSkin.Value = replayPlayerBodySpawned.VisorGoalieRedSkin;
				replayPlayerByClientId5.Mustache.Value = replayPlayerBodySpawned.Mustache;
				replayPlayerByClientId5.Beard.Value = replayPlayerBodySpawned.Beard;
				replayPlayerByClientId5.JerseyAttackerBlueSkin.Value = replayPlayerBodySpawned.JerseyAttackerBlueSkin;
				replayPlayerByClientId5.JerseyAttackerRedSkin.Value = replayPlayerBodySpawned.JerseyAttackerRedSkin;
				replayPlayerByClientId5.JerseyGoalieBlueSkin.Value = replayPlayerBodySpawned.JerseyGoalieBlueSkin;
				replayPlayerByClientId5.JerseyGoalieRedSkin.Value = replayPlayerBodySpawned.JerseyGoalieRedSkin;
				replayPlayerByClientId5.Server_SpawnPlayerBody(replayPlayerBodySpawned.Position, replayPlayerBodySpawned.Rotation, replayPlayerByClientId5.Role.Value);
			}
			break;
		}
		case "PlayerBodyMove":
		{
			ReplayPlayerBodyMove replayPlayerBodyMove = (ReplayPlayerBodyMove)eventData;
			Player replayPlayerByClientId8 = NetworkBehaviourSingleton<PlayerManager>.Instance.GetReplayPlayerByClientId(replayPlayerBodyMove.OwnerClientId);
			if ((bool)replayPlayerByClientId8 && (bool)replayPlayerByClientId8.PlayerBody)
			{
				replayPlayerByClientId8.PlayerBody.transform.DOKill(complete: true);
				replayPlayerByClientId8.PlayerBody.transform.DOMove(replayPlayerBodyMove.Position, TickInterval).SetEase(Ease.Linear);
				replayPlayerByClientId8.PlayerBody.transform.DORotateQuaternion(replayPlayerBodyMove.Rotation, TickInterval).SetEase(Ease.Linear);
				replayPlayerByClientId8.PlayerBody.StaminaCompressed.Value = replayPlayerBodyMove.Stamina;
				replayPlayerByClientId8.PlayerBody.SpeedCompressed.Value = replayPlayerBodyMove.Speed;
				replayPlayerByClientId8.PlayerBody.IsSprinting.Value = replayPlayerBodyMove.IsSprinting;
				replayPlayerByClientId8.PlayerBody.IsSliding.Value = replayPlayerBodyMove.IsSliding;
				replayPlayerByClientId8.PlayerBody.IsStopping.Value = replayPlayerBodyMove.IsStopping;
				replayPlayerByClientId8.PlayerBody.IsExtendedLeft.Value = replayPlayerBodyMove.IsExtendedLeft;
				replayPlayerByClientId8.PlayerBody.IsExtendedRight.Value = replayPlayerBodyMove.IsExtendedRight;
			}
			break;
		}
		case "PlayerBodyDespawned":
		{
			ReplayPlayerBodyDespawned replayPlayerBodyDespawned = (ReplayPlayerBodyDespawned)eventData;
			Player replayPlayerByClientId3 = NetworkBehaviourSingleton<PlayerManager>.Instance.GetReplayPlayerByClientId(replayPlayerBodyDespawned.OwnerClientId);
			if ((bool)replayPlayerByClientId3 && (bool)replayPlayerByClientId3.PlayerBody)
			{
				replayPlayerByClientId3.Server_DespawnPlayerBody();
			}
			break;
		}
		case "StickSpawned":
		{
			ReplayStickSpawned replayStickSpawned = (ReplayStickSpawned)eventData;
			Player replayPlayerByClientId2 = NetworkBehaviourSingleton<PlayerManager>.Instance.GetReplayPlayerByClientId(replayStickSpawned.OwnerClientId);
			if ((bool)replayPlayerByClientId2)
			{
				replayPlayerByClientId2.StickAttackerBlueSkin.Value = replayStickSpawned.StickAttackerBlueSkin;
				replayPlayerByClientId2.StickAttackerRedSkin.Value = replayStickSpawned.StickAttackerRedSkin;
				replayPlayerByClientId2.StickGoalieBlueSkin.Value = replayStickSpawned.StickGoalieBlueSkin;
				replayPlayerByClientId2.StickGoalieRedSkin.Value = replayStickSpawned.StickGoalieRedSkin;
				replayPlayerByClientId2.StickShaftAttackerBlueTapeSkin.Value = replayStickSpawned.StickShaftAttackerBlueTapeSkin;
				replayPlayerByClientId2.StickShaftAttackerRedTapeSkin.Value = replayStickSpawned.StickShaftAttackerRedTapeSkin;
				replayPlayerByClientId2.StickShaftGoalieBlueTapeSkin.Value = replayStickSpawned.StickShaftGoalieBlueTapeSkin;
				replayPlayerByClientId2.StickShaftGoalieRedTapeSkin.Value = replayStickSpawned.StickShaftGoalieRedTapeSkin;
				replayPlayerByClientId2.StickBladeAttackerBlueTapeSkin.Value = replayStickSpawned.StickBladeAttackerBlueTapeSkin;
				replayPlayerByClientId2.StickBladeAttackerRedTapeSkin.Value = replayStickSpawned.StickBladeAttackerRedTapeSkin;
				replayPlayerByClientId2.StickBladeGoalieBlueTapeSkin.Value = replayStickSpawned.StickBladeGoalieBlueTapeSkin;
				replayPlayerByClientId2.StickBladeGoalieRedTapeSkin.Value = replayStickSpawned.StickBladeGoalieRedTapeSkin;
				replayPlayerByClientId2.Server_SpawnStick(replayStickSpawned.Position, replayStickSpawned.Rotation, replayPlayerByClientId2.Role.Value);
			}
			break;
		}
		case "StickMove":
		{
			ReplayStickMove replayStickMove = (ReplayStickMove)eventData;
			Player replayPlayerByClientId7 = NetworkBehaviourSingleton<PlayerManager>.Instance.GetReplayPlayerByClientId(replayStickMove.OwnerClientId);
			if ((bool)replayPlayerByClientId7 && (bool)replayPlayerByClientId7.Stick)
			{
				replayPlayerByClientId7.Stick.transform.DOKill(complete: true);
				replayPlayerByClientId7.Stick.transform.DOMove(replayStickMove.Position, TickInterval).SetEase(Ease.Linear);
				replayPlayerByClientId7.Stick.transform.DORotateQuaternion(replayStickMove.Rotation, TickInterval).SetEase(Ease.Linear);
			}
			break;
		}
		case "StickDespawned":
		{
			ReplayStickDespawned replayStickDespawned = (ReplayStickDespawned)eventData;
			Player replayPlayerByClientId4 = NetworkBehaviourSingleton<PlayerManager>.Instance.GetReplayPlayerByClientId(replayStickDespawned.OwnerClientId);
			if ((bool)replayPlayerByClientId4 && (bool)replayPlayerByClientId4.Stick)
			{
				replayPlayerByClientId4.Server_DespawnStick();
			}
			break;
		}
		case "PuckSpawned":
		{
			ReplayPuckSpawned replayPuckSpawned = (ReplayPuckSpawned)eventData;
			Puck puck = NetworkBehaviourSingleton<PuckManager>.Instance.Server_SpawnPuck(replayPuckSpawned.Position, replayPuckSpawned.Rotation, Vector3.zero, isReplay: true);
			replayPuckNetworkObjectIdMap.Add(replayPuckSpawned.NetworkObjectId, puck.NetworkObjectId);
			puck.transform.position = replayPuckSpawned.Position;
			puck.transform.rotation = replayPuckSpawned.Rotation;
			puck.Server_Freeze();
			break;
		}
		case "PuckMove":
		{
			ReplayPuckMove replayPuckMove = (ReplayPuckMove)eventData;
			if (replayPuckNetworkObjectIdMap.ContainsKey(replayPuckMove.NetworkObjectId))
			{
				Puck replayPuckByNetworkObjectId2 = NetworkBehaviourSingleton<PuckManager>.Instance.GetReplayPuckByNetworkObjectId(replayPuckNetworkObjectIdMap[replayPuckMove.NetworkObjectId]);
				if ((bool)replayPuckByNetworkObjectId2)
				{
					replayPuckByNetworkObjectId2.transform.DOKill(complete: true);
					replayPuckByNetworkObjectId2.transform.DOMove(replayPuckMove.Position, TickInterval).SetEase(Ease.Linear);
					replayPuckByNetworkObjectId2.transform.DORotateQuaternion(replayPuckMove.Rotation, TickInterval).SetEase(Ease.Linear);
				}
			}
			break;
		}
		case "PuckDespawned":
		{
			ReplayPuckDespawned replayPuckDespawned = (ReplayPuckDespawned)eventData;
			if (replayPuckNetworkObjectIdMap.ContainsKey(replayPuckDespawned.NetworkObjectId))
			{
				Puck replayPuckByNetworkObjectId = NetworkBehaviourSingleton<PuckManager>.Instance.GetReplayPuckByNetworkObjectId(replayPuckNetworkObjectIdMap[replayPuckDespawned.NetworkObjectId]);
				if ((bool)replayPuckByNetworkObjectId)
				{
					NetworkBehaviourSingleton<PuckManager>.Instance.Server_DespawnPuck(replayPuckByNetworkObjectId);
				}
			}
			break;
		}
		}
	}

	private void Server_DisposeReplayObjects()
	{
		foreach (Player replayPlayer in NetworkBehaviourSingleton<PlayerManager>.Instance.GetReplayPlayers())
		{
			if ((bool)replayPlayer.PlayerBody)
			{
				replayPlayer.PlayerBody.transform.DOKill();
				replayPlayer.Server_DespawnPlayerBody();
			}
			if ((bool)replayPlayer.Stick)
			{
				replayPlayer.Stick.transform.DOKill();
				replayPlayer.Server_DespawnStick();
			}
			replayPlayer.NetworkObject.Despawn();
		}
		foreach (Puck replayPuck in NetworkBehaviourSingleton<PuckManager>.Instance.GetReplayPucks())
		{
			replayPuck.transform.DOKill();
			NetworkBehaviourSingleton<PuckManager>.Instance.Server_DespawnPuck(replayPuck);
		}
	}

	private void Server_Dispose()
	{
		Server_DisposeReplayObjects();
		Tick = 0;
		EventMap.Clear();
		replayPuckNetworkObjectIdMap.Clear();
		replayPlayerSpawnedList.Clear();
		replayPlayerBodySpawnedList.Clear();
		replayStickSpawnedList.Clear();
		replayPuckSpawnedList.Clear();
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
		return "ReplayPlayer";
	}
}
