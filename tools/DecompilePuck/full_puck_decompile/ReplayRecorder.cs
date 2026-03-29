using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ReplayRecorder : NetworkBehaviour
{
	[HideInInspector]
	public bool IsRecording;

	[HideInInspector]
	public int TickRate = 15;

	[HideInInspector]
	public int Tick;

	[HideInInspector]
	public SortedList<int, List<(string, object)>> EventMap = new SortedList<int, List<(string, object)>>();

	private float tickAccumulator;

	private void Update()
	{
		if (!base.IsSpawned || !NetworkManager.Singleton.IsServer || !IsRecording)
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
			Server_Tick();
			Tick++;
		}
	}

	public void Server_StartRecording(int tickRate)
	{
		if (!NetworkManager.Singleton.IsServer || IsRecording)
		{
			return;
		}
		Debug.Log($"[ReplayRecorder] Replay recording started at {TickRate} ticks per second");
		EventMap.Clear();
		TickRate = tickRate;
		Tick = 0;
		IsRecording = true;
		foreach (Player player in NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers())
		{
			Server_AddPlayerSpawnedEvent(player);
			if ((bool)player.PlayerBody)
			{
				Server_AddPlayerBodySpawnedEvent(player.PlayerBody);
			}
			if ((bool)player.Stick)
			{
				Server_AddStickSpawnedEvent(player.Stick);
			}
		}
		foreach (Puck puck in NetworkBehaviourSingleton<PuckManager>.Instance.GetPucks())
		{
			Server_AddPuckSpawnedEvent(puck);
		}
	}

	public void Server_StopRecording()
	{
		if (NetworkManager.Singleton.IsServer && IsRecording)
		{
			Debug.Log("[ReplayRecorder] Replay recording stopped");
			Tick = 0;
			IsRecording = false;
		}
	}

	private void Server_Tick()
	{
		if (!NetworkManager.Singleton.IsServer)
		{
			return;
		}
		foreach (Player spawnedPlayer in NetworkBehaviourSingleton<PlayerManager>.Instance.GetSpawnedPlayers())
		{
			Server_AddReplayEvent("PlayerBodyMove", new ReplayPlayerBodyMove
			{
				OwnerClientId = spawnedPlayer.OwnerClientId,
				Position = spawnedPlayer.PlayerBody.transform.position,
				Rotation = spawnedPlayer.PlayerBody.transform.rotation,
				Stamina = spawnedPlayer.PlayerBody.StaminaCompressed.Value,
				Speed = spawnedPlayer.PlayerBody.StaminaCompressed.Value,
				IsSprinting = spawnedPlayer.PlayerBody.IsSprinting.Value,
				IsSliding = spawnedPlayer.PlayerBody.IsSliding.Value,
				IsStopping = spawnedPlayer.PlayerBody.IsStopping.Value,
				IsExtendedLeft = spawnedPlayer.PlayerBody.IsExtendedLeft.Value,
				IsExtendedRight = spawnedPlayer.PlayerBody.IsExtendedRight.Value
			});
			Server_AddReplayEvent("StickMove", new ReplayStickMove
			{
				OwnerClientId = spawnedPlayer.OwnerClientId,
				Position = spawnedPlayer.Stick.transform.position,
				Rotation = spawnedPlayer.Stick.transform.rotation
			});
			Server_AddReplayEvent("PlayerInput", new ReplayPlayerInput
			{
				OwnerClientId = spawnedPlayer.OwnerClientId,
				LookAngleInput = spawnedPlayer.PlayerInput.LookAngleInput.ServerValue,
				BladeAngleInput = spawnedPlayer.PlayerInput.BladeAngleInput.ServerValue,
				TrackInput = spawnedPlayer.PlayerInput.TrackInput.ServerValue,
				LookInput = spawnedPlayer.PlayerInput.LookInput.ServerValue
			});
		}
		foreach (Puck puck in NetworkBehaviourSingleton<PuckManager>.Instance.GetPucks())
		{
			Server_AddReplayEvent("PuckMove", new ReplayPuckMove
			{
				NetworkObjectId = puck.NetworkObjectId,
				Position = puck.transform.position,
				Rotation = puck.transform.rotation
			});
		}
	}

	public void Server_AddReplayEvent(string eventName, object eventData)
	{
		if (NetworkManager.Singleton.IsServer && IsRecording)
		{
			if (EventMap.ContainsKey(Tick))
			{
				EventMap[Tick].Add((eventName, eventData));
				return;
			}
			List<(string, object)> value = new List<(string, object)> { (eventName, eventData) };
			EventMap.Add(Tick, value);
		}
	}

	public void Server_AddPlayerSpawnedEvent(Player player)
	{
		Server_AddReplayEvent("PlayerSpawned", new ReplayPlayerSpawned
		{
			OwnerClientId = player.OwnerClientId
		});
	}

	public void Server_AddPlayerDespawnedEvent(Player player)
	{
		Server_AddReplayEvent("PlayerDespawned", new ReplayPlayerDespawned
		{
			OwnerClientId = player.OwnerClientId
		});
	}

	public void Server_AddPlayerBodySpawnedEvent(PlayerBodyV2 playerBody)
	{
		Server_AddReplayEvent("PlayerBodySpawned", new ReplayPlayerBodySpawned
		{
			OwnerClientId = playerBody.OwnerClientId,
			Position = playerBody.transform.position,
			Rotation = playerBody.transform.rotation,
			Username = playerBody.Player.Username.Value,
			Number = playerBody.Player.Number.Value,
			Team = playerBody.Player.Team.Value,
			Role = playerBody.Player.Role.Value,
			Country = playerBody.Player.Country.Value,
			VisorAttackerBlueSkin = playerBody.Player.VisorAttackerBlueSkin.Value,
			VisorAttackerRedSkin = playerBody.Player.VisorAttackerRedSkin.Value,
			VisorGoalieBlueSkin = playerBody.Player.VisorGoalieBlueSkin.Value,
			VisorGoalieRedSkin = playerBody.Player.VisorGoalieRedSkin.Value,
			Mustache = playerBody.Player.Mustache.Value,
			Beard = playerBody.Player.Beard.Value,
			JerseyAttackerBlueSkin = playerBody.Player.JerseyAttackerBlueSkin.Value,
			JerseyAttackerRedSkin = playerBody.Player.JerseyAttackerRedSkin.Value,
			JerseyGoalieBlueSkin = playerBody.Player.JerseyGoalieBlueSkin.Value,
			JerseyGoalieRedSkin = playerBody.Player.JerseyGoalieRedSkin.Value
		});
	}

	public void Server_AddPlayerBodyDespawnedEvent(PlayerBodyV2 playerBody)
	{
		Server_AddReplayEvent("PlayerBodyDespawned", new ReplayPlayerBodyDespawned
		{
			OwnerClientId = playerBody.OwnerClientId
		});
	}

	public void Server_AddStickSpawnedEvent(Stick stick)
	{
		Server_AddReplayEvent("StickSpawned", new ReplayStickSpawned
		{
			OwnerClientId = stick.OwnerClientId,
			Position = stick.transform.position,
			Rotation = stick.transform.rotation,
			StickAttackerBlueSkin = stick.Player.StickAttackerBlueSkin.Value,
			StickAttackerRedSkin = stick.Player.StickAttackerRedSkin.Value,
			StickGoalieBlueSkin = stick.Player.StickGoalieBlueSkin.Value,
			StickGoalieRedSkin = stick.Player.StickGoalieRedSkin.Value,
			StickShaftAttackerBlueTapeSkin = stick.Player.StickShaftAttackerBlueTapeSkin.Value,
			StickShaftAttackerRedTapeSkin = stick.Player.StickShaftAttackerRedTapeSkin.Value,
			StickShaftGoalieBlueTapeSkin = stick.Player.StickShaftGoalieBlueTapeSkin.Value,
			StickShaftGoalieRedTapeSkin = stick.Player.StickShaftGoalieRedTapeSkin.Value,
			StickBladeAttackerBlueTapeSkin = stick.Player.StickBladeAttackerBlueTapeSkin.Value,
			StickBladeAttackerRedTapeSkin = stick.Player.StickBladeAttackerRedTapeSkin.Value,
			StickBladeGoalieBlueTapeSkin = stick.Player.StickBladeGoalieBlueTapeSkin.Value,
			StickBladeGoalieRedTapeSkin = stick.Player.StickBladeGoalieRedTapeSkin.Value
		});
	}

	public void Server_AddStickDespawnedEvent(Stick stick)
	{
		Server_AddReplayEvent("StickDespawned", new ReplayStickDespawned
		{
			OwnerClientId = stick.OwnerClientId
		});
	}

	public void Server_AddPuckSpawnedEvent(Puck puck)
	{
		Server_AddReplayEvent("PuckSpawned", new ReplayPuckSpawned
		{
			NetworkObjectId = puck.NetworkObjectId,
			Position = puck.transform.position,
			Rotation = puck.transform.rotation
		});
	}

	public void Server_AddPuckDespawnedEvent(Puck puck)
	{
		Server_AddReplayEvent("PuckDespawned", new ReplayPuckDespawned
		{
			NetworkObjectId = puck.NetworkObjectId
		});
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
		return "ReplayRecorder";
	}
}
