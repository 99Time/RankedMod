using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PuckShooter : NetworkBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float force = 1000f;

	[SerializeField]
	private float interval = 1f;

	[SerializeField]
	private bool shootOnStart;

	[SerializeField]
	private float destroyTime = 2f;

	private List<Puck> shotPucks = new List<Puck>();

	private IEnumerator shootIntervalCoroutine;

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
		if (NetworkManager.Singleton.IsServer && shootOnStart)
		{
			Server_StartShootingCoroutine();
		}
	}

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();
		if (NetworkManager.Singleton.IsServer)
		{
			Server_StopShootingCoroutine();
		}
	}

	public void Server_Shoot()
	{
		if (base.IsServer)
		{
			Puck puck = NetworkBehaviourSingleton<PuckManager>.Instance.Server_SpawnPuck(base.transform.position, Quaternion.identity, base.transform.forward * force);
			shotPucks.Add(puck);
			StartCoroutine(IDestroyAfterTime(puck, destroyTime));
		}
	}

	public void Server_StartShootingCoroutine()
	{
		if (base.IsServer)
		{
			Server_StopShootingCoroutine();
			shootIntervalCoroutine = IShootInterval();
			StartCoroutine(shootIntervalCoroutine);
		}
	}

	public void Server_StopShootingCoroutine()
	{
		if (base.IsServer && shootIntervalCoroutine != null)
		{
			StopCoroutine(shootIntervalCoroutine);
		}
	}

	private IEnumerator IShootInterval()
	{
		yield return new WaitForSeconds(interval);
		Server_Shoot();
		Server_StartShootingCoroutine();
	}

	private IEnumerator IDestroyAfterTime(Puck puck, float time)
	{
		yield return new WaitForSeconds(time);
		if ((bool)puck && shotPucks.Contains(puck))
		{
			shotPucks.Remove(puck);
			puck.NetworkObject.Despawn();
		}
		else
		{
			shotPucks.RemoveAll((Puck puck2) => puck2 == null);
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
		return "PuckShooter";
	}
}
