using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace PuckAIPractice.Utilities;

public static class FakePlayerRegistry
{
	private static readonly HashSet<Player> fakePlayers = new HashSet<Player>();

	public static IEnumerable<Player> All => fakePlayers;

	public static void Register(Player player)
	{
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)player != (Object)null)
		{
			if (!fakePlayers.Any((Player p) => ((NetworkBehaviour)p).OwnerClientId == ((NetworkBehaviour)player).OwnerClientId))
			{
				fakePlayers.Add(player);
			}
			Debug.Log((object)$"[FakeRegistry] Registered {player.Username?.Value} (OwnerClientId: {((NetworkBehaviour)player).OwnerClientId})");
		}
	}

	public static void Unregister(Player player)
	{
		if ((Object)(object)player != (Object)null)
		{
			fakePlayers.Remove(player);
		}
	}

	public static bool IsFake(Player player)
	{
		return (Object)(object)player != (Object)null && fakePlayers.Contains(player);
	}
}
