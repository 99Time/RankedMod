using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerManager : NetworkBehaviourSingleton<PlayerManager>
{
	[Header("Prefabs")]
	[SerializeField]
	private Player playerPrefab;

	private List<Player> players = new List<Player>();

	public void AddPlayer(Player player)
	{
		players.Add(player);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerAdded", new Dictionary<string, object> { { "player", player } });
	}

	public void RemovePlayer(Player player)
	{
		players.Remove(player);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerRemoved", new Dictionary<string, object> { { "player", player } });
	}

	public List<Player> GetPlayers(bool includeReplay = false)
	{
		players.RemoveAll((Player player) => !player || !player.NetworkObject.IsSpawned);
		if (includeReplay)
		{
			return players;
		}
		return players.Where((Player player) => !player.IsReplay.Value).ToList();
	}

	public List<Player> GetPlayersByTeam(PlayerTeam team, bool includeReplay = false)
	{
		return (from player in GetPlayers(includeReplay)
			where player.Team.Value == team
			select player).ToList();
	}

	public List<Player> GetReplayPlayers()
	{
		players.RemoveAll((Player player) => !player || !player.NetworkObject.IsSpawned);
		return players.Where((Player player) => player.IsReplay.Value).ToList();
	}

	public Player GetPlayerByClientId(ulong clientId)
	{
		return GetPlayers().Find((Player player) => player.OwnerClientId == clientId);
	}

	public Player GetPlayerByUsername(FixedString32Bytes username, bool caseSensitive = true)
	{
		return GetPlayers().Find((Player player) => (caseSensitive ? player.Username.Value.ToString() : player.Username.Value.ToString().ToUpper()) == (caseSensitive ? username.ToString() : username.ToString().ToUpper()));
	}

	public Player GetPlayerByNumber(int number)
	{
		return GetPlayers().Find((Player player) => player.Number.Value == number);
	}

	public Player GetPlayerBySteamId(FixedString32Bytes steamId)
	{
		return GetPlayers().Find((Player player) => player.SteamId.Value == steamId);
	}

	public Player GetReplayPlayerByClientId(ulong clientId)
	{
		return GetReplayPlayers().Find((Player player) => player.OwnerClientId == 1337 + clientId);
	}

	public Player GetLocalPlayer()
	{
		return GetPlayers().Find((Player player) => player.IsLocalPlayer);
	}

	public List<Player> GetSpawnedPlayers(bool includeReplay = false)
	{
		return GetPlayers(includeReplay).FindAll((Player player) => player.IsCharacterPartiallySpawned);
	}

	public List<Player> GetSpawnedPlayersByTeam(PlayerTeam team, bool includeReplay = false)
	{
		return (from player in GetSpawnedPlayers(includeReplay)
			where player.Team.Value == team
			select player).ToList();
	}

	public Player GetSpawnedFirstPlayer(bool includeReplay = false)
	{
		List<Player> spawnedPlayers = GetSpawnedPlayers(includeReplay);
		spawnedPlayers.Sort((Player a, Player b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
		return spawnedPlayers.FirstOrDefault();
	}

	public Player GetSpawnedLastPlayer(bool includeReplay = false)
	{
		List<Player> spawnedPlayers = GetSpawnedPlayers(includeReplay);
		spawnedPlayers.Sort((Player a, Player b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
		return spawnedPlayers.LastOrDefault();
	}

	public Player GetSpawnedNextPlayerByClientId(ulong clientId, bool includeReplay = false)
	{
		return GetSpawnedPlayers(includeReplay).Find((Player player) => player.OwnerClientId > clientId);
	}

	public Player GetSpawnedPreviousPlayerByClientId(ulong clientId, bool includeReplay = false)
	{
		return GetSpawnedPlayers(includeReplay).Find((Player player) => player.OwnerClientId < clientId);
	}

	public PlayerBodyV2 GetPlayerBodyByNetworkObjectId(ulong networkObjectId, bool includeReplay = false)
	{
		Player player = GetSpawnedPlayers(includeReplay).Find((Player player2) => player2.PlayerBody.NetworkObjectId == networkObjectId);
		if ((bool)player)
		{
			return player.PlayerBody;
		}
		return null;
	}

	public string[] GetPlayerSteamIds()
	{
		return (from player in GetPlayers()
			select player.SteamId.Value.ToString()).ToArray();
	}

	public bool IsEnoughPlayersForPlaying()
	{
		if (GetPlayersByTeam(PlayerTeam.Blue).Count >= 1)
		{
			return GetPlayersByTeam(PlayerTeam.Red).Count >= 1;
		}
		return false;
	}

	public void Server_SpawnPlayer(ulong clientId, bool isReplay = false)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Player player = Object.Instantiate(playerPrefab);
			player.IsReplay.Value = isReplay;
			if (isReplay)
			{
				player.NetworkObject.SpawnWithOwnership(clientId);
				Debug.Log($"[PlayerManager] Spawned replay player ({clientId})");
			}
			else
			{
				player.NetworkObject.SpawnAsPlayerObject(clientId);
				Debug.Log($"[PlayerManager] Spawned player ({clientId})");
			}
		}
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
		return "PlayerManager";
	}
}
