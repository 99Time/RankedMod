using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class Goal : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private Cloth netCloth;

	[Header("Settings")]
	[SerializeField]
	private PlayerTeam Team;

	public Cloth NetCloth => netCloth;

	public void Server_OnPuckEnterGoal(Puck puck)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnPuckEnterTeamGoal", new Dictionary<string, object>
			{
				{ "puck", puck },
				{ "team", Team }
			});
		}
	}

	public void Client_AddNetClothSphereCollider(SphereCollider sphereCollider)
	{
		if (NetworkManager.Singleton.IsClient && (bool)sphereCollider)
		{
			List<ClothSphereColliderPair> list = netCloth.sphereColliders.ToList();
			list.Add(new ClothSphereColliderPair(sphereCollider));
			netCloth.sphereColliders = list.ToArray();
		}
	}

	public void Client_RemoveNetClothSphereCollider(SphereCollider sphereCollider)
	{
		if (NetworkManager.Singleton.IsClient && (bool)sphereCollider)
		{
			List<ClothSphereColliderPair> list = netCloth.sphereColliders.ToList();
			list.RemoveAll((ClothSphereColliderPair pair) => pair.first == sphereCollider);
			netCloth.sphereColliders = list.ToArray();
		}
	}
}
