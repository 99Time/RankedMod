using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SynchronizedObject : NetworkBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float positionThreshold = 0.001f;

	[SerializeField]
	private float rotationThreshold = 0.01f;

	[HideInInspector]
	public Rigidbody Rigidbody;

	[HideInInspector]
	public Vector3 PredictedLinearVelocity = Vector3.zero;

	[HideInInspector]
	public Vector3 PredictedAngularVelocity = Vector3.zero;

	private Vector3 lastSentPosition = Vector3.zero;

	private Quaternion lastSentRotation = Quaternion.identity;

	private Vector3 lastReceivedPosition = Vector3.zero;

	private Quaternion lastReceivedRotation = Quaternion.identity;

	private void Awake()
	{
		Rigidbody = GetComponent<Rigidbody>();
	}

	protected override void OnNetworkPostSpawn()
	{
		if (!NetworkManager.Singleton.IsServer)
		{
			Rigidbody.isKinematic = true;
			Rigidbody.interpolation = RigidbodyInterpolation.None;
		}
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnSynchronizedObjectSpawned", new Dictionary<string, object> { { "synchronizedObject", this } });
		base.OnNetworkPostSpawn();
	}

	public override void OnNetworkDespawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnSynchronizedObjectDespawned", new Dictionary<string, object> { { "synchronizedObject", this } });
		base.OnNetworkDespawn();
	}

	public void OnClientTick(Vector3 position, Quaternion rotation, float serverDeltaTime)
	{
		PredictedLinearVelocity = (position - base.transform.position) / serverDeltaTime;
		PredictedAngularVelocity = (rotation * Quaternion.Inverse(lastReceivedRotation)).eulerAngles / serverDeltaTime;
		lastReceivedPosition = position;
		lastReceivedRotation = rotation;
		base.transform.position = position;
		base.transform.rotation = rotation;
	}

	public SynchronizedObjectSnapshot OnClientSmoothTick(Vector3 position, Quaternion rotation, SynchronizedObject synchronizedObject, float serverDeltaTime)
	{
		Vector3 linearVelocity = (position - lastReceivedPosition) / serverDeltaTime;
		Vector3 angularVelocity = (rotation * Quaternion.Inverse(lastReceivedRotation)).eulerAngles / serverDeltaTime;
		lastReceivedPosition = position;
		lastReceivedRotation = rotation;
		return new SynchronizedObjectSnapshot
		{
			SynchronizedObject = synchronizedObject,
			Position = position,
			Rotation = rotation,
			LinearVelocity = linearVelocity,
			AngularVelocity = angularVelocity
		};
	}

	public (Vector3, Quaternion, ulong) OnServerTick(float serverDeltaTime)
	{
		PredictedLinearVelocity = (base.transform.position - lastSentPosition) / serverDeltaTime;
		PredictedAngularVelocity = Quaternion.Inverse(base.transform.rotation) * lastSentRotation.eulerAngles / serverDeltaTime;
		lastSentPosition = base.transform.position;
		lastSentRotation = base.transform.rotation;
		return (base.transform.position, base.transform.rotation, base.NetworkObjectId);
	}

	public bool ShouldSendPosition(int tickRate)
	{
		float num = Vector3.Distance(lastSentPosition, base.transform.position);
		float num2 = positionThreshold * (float)(100 / tickRate);
		return num > num2;
	}

	public bool ShouldSendRotation(int tickRate)
	{
		float num = Quaternion.Angle(lastSentRotation, base.transform.rotation);
		float num2 = rotationThreshold * (float)(100 / tickRate);
		return num > num2;
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
		return "SynchronizedObject";
	}
}
