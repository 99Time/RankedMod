using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class EdgegapManager : MonoBehaviour
{
	private const int EDGEGAP_DEPLOYMENT_DELETE_TIMEOUT = 60;

	private IEnumerator deleteDeploymentCoroutine;

	public string RequestId { get; private set; }

	public string ArbitriumPublicIp { get; private set; }

	public ushort ArbitriumPortGamePortExternal { get; private set; }

	public ushort ArbitriumPortPingPortExternal { get; private set; }

	public string ArbitriumDeleteUrl { get; private set; }

	public string ArbitriumDeleteToken { get; private set; }

	public bool IsEdgegap
	{
		get
		{
			if (!string.IsNullOrEmpty(RequestId) && !string.IsNullOrEmpty(ArbitriumPublicIp) && ArbitriumPortGamePortExternal != 0)
			{
				return ArbitriumPortPingPortExternal != 0;
			}
			return false;
		}
	}

	private void Awake()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("ARBITRIUM_REQUEST_ID");
		string environmentVariable2 = Environment.GetEnvironmentVariable("ARBITRIUM_PUBLIC_IP");
		string environmentVariable3 = Environment.GetEnvironmentVariable("ARBITRIUM_PORT_GAME_PORT_EXTERNAL");
		string environmentVariable4 = Environment.GetEnvironmentVariable("ARBITRIUM_PORT_PING_PORT_EXTERNAL");
		string environmentVariable5 = Environment.GetEnvironmentVariable("ARBITRIUM_DELETE_URL");
		string environmentVariable6 = Environment.GetEnvironmentVariable("ARBITRIUM_DELETE_TOKEN");
		if (!string.IsNullOrEmpty(environmentVariable) && !string.IsNullOrEmpty(environmentVariable2) && !string.IsNullOrEmpty(environmentVariable3) && !string.IsNullOrEmpty(environmentVariable4) && !string.IsNullOrEmpty(environmentVariable5) && !string.IsNullOrEmpty(environmentVariable6))
		{
			RequestId = environmentVariable;
			ArbitriumPublicIp = environmentVariable2;
			ArbitriumPortGamePortExternal = ushort.Parse(environmentVariable3);
			ArbitriumPortPingPortExternal = ushort.Parse(environmentVariable4);
			ArbitriumDeleteUrl = environmentVariable5;
			ArbitriumDeleteToken = environmentVariable6;
		}
	}

	private void Start()
	{
		if (IsEdgegap)
		{
			StartDeleteDeploymentCoroutine();
		}
	}

	public void StartDeleteDeploymentCoroutine()
	{
		if (IsEdgegap)
		{
			StopDeleteDeploymentCoroutine();
			Debug.Log($"[EdgegapManager] Starting delete deployment coroutine with delay: {60}");
			deleteDeploymentCoroutine = IDeleteDeployment(60f);
			StartCoroutine(deleteDeploymentCoroutine);
		}
	}

	public void StopDeleteDeploymentCoroutine()
	{
		if (IsEdgegap)
		{
			Debug.Log("[EdgegapManager] Stopping delete deployment coroutine");
			if (deleteDeploymentCoroutine != null)
			{
				StopCoroutine(deleteDeploymentCoroutine);
			}
		}
	}

	private IEnumerator IDeleteDeployment(float delay)
	{
		yield return new WaitForSeconds(delay);
		DeleteDeployment();
	}

	public async void DeleteDeployment()
	{
		Debug.Log("[EdgegapManager] Deleting deployment with RequestId: " + RequestId);
		Debug.Log("[EdgegapManager] ArbitriumDeleteUrl: " + ArbitriumDeleteUrl);
		Debug.Log("[EdgegapManager] ArbitriumDeleteToken: " + ArbitriumDeleteToken);
		UnityWebRequest webRequest = UnityWebRequest.Delete(ArbitriumDeleteUrl);
		webRequest.SetRequestHeader("authorization", ArbitriumDeleteToken);
		await webRequest.SendWebRequest();
		if (webRequest.result == UnityWebRequest.Result.Success)
		{
			Debug.Log("[EdgegapManager] Deployment deleted successfully");
			return;
		}
		Debug.Log("[EdgegapManager] Deployment delete failed: " + webRequest.error);
		StartDeleteDeploymentCoroutine();
	}
}
