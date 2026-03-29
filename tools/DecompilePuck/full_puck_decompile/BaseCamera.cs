using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BaseCamera : NetworkBehaviour
{
	public Camera CameraComponent;

	public AudioListener AudioListener;

	public bool IsEnabled => CameraComponent.enabled;

	public virtual void Awake()
	{
		CameraComponent = GetComponent<Camera>();
		AudioListener = GetComponent<AudioListener>();
	}

	public override void OnNetworkDespawn()
	{
		if (IsEnabled)
		{
			Disable();
		}
		base.OnNetworkDespawn();
	}

	public override void OnDestroy()
	{
		if (IsEnabled)
		{
			Disable();
		}
		base.OnDestroy();
	}

	public virtual void OnTick(float deltaTime)
	{
	}

	public virtual void Enable()
	{
		if (!IsEnabled)
		{
			CameraComponent.enabled = true;
			AudioListener.enabled = true;
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnBaseCameraEnabled", new Dictionary<string, object> { { "baseCamera", this } });
		}
	}

	public virtual void Disable()
	{
		if (IsEnabled)
		{
			CameraComponent.enabled = false;
			AudioListener.enabled = false;
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnBaseCameraDisabled", new Dictionary<string, object> { { "baseCamera", this } });
		}
	}

	public virtual void SetFieldOfView(float fieldOfView)
	{
		CameraComponent.fieldOfView = fieldOfView;
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
		return "BaseCamera";
	}
}
