using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BaseCameraController : NetworkBehaviour
{
	private BaseCamera baseCamera;

	private bool useNetworkSmoothing;

	public virtual void Awake()
	{
		baseCamera = GetComponent<BaseCamera>();
	}

	public virtual void Start()
	{
		useNetworkSmoothing = MonoBehaviourSingleton<SettingsManager>.Instance.UseNetworkSmoothing > 0;
	}

	public override void OnNetworkSpawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnBaseCameraEnabled", Event_Client_OnBaseCameraEnabled);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnFovChanged", Event_Client_OnFovChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSynchronizeObjects", Event_Client_OnSynchronizeObjects);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnUseNetworkSmoothingChanged", Event_Client_OnUseNetworkSmoothingChanged);
		base.OnNetworkSpawn();
	}

	public override void OnNetworkDespawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnBaseCameraEnabled", Event_Client_OnBaseCameraEnabled);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnFovChanged", Event_Client_OnFovChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSynchronizeObjects", Event_Client_OnSynchronizeObjects);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnUseNetworkSmoothingChanged", Event_Client_OnUseNetworkSmoothingChanged);
		base.OnNetworkDespawn();
	}

	private void LateUpdate()
	{
		if (base.IsSpawned && (useNetworkSmoothing || NetworkManager.Singleton.IsHost))
		{
			baseCamera.OnTick(Time.deltaTime);
		}
	}

	private void Event_Client_OnBaseCameraEnabled(Dictionary<string, object> message)
	{
		if ((BaseCamera)message["baseCamera"] != baseCamera)
		{
			baseCamera.Disable();
		}
	}

	private void Event_Client_OnFovChanged(Dictionary<string, object> message)
	{
		float fieldOfView = (float)message["value"];
		baseCamera.SetFieldOfView(fieldOfView);
	}

	private void Event_Client_OnSynchronizeObjects(Dictionary<string, object> message)
	{
		if (!useNetworkSmoothing)
		{
			float deltaTime = (float)message["serverDeltaTime"];
			baseCamera.OnTick(deltaTime);
		}
	}

	private void Event_Client_OnUseNetworkSmoothingChanged(Dictionary<string, object> message)
	{
		bool flag = (bool)message["value"];
		useNetworkSmoothing = flag;
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
		return "BaseCameraController";
	}
}
