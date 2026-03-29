using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SynchronizedObjectManager : NetworkBehaviourSingleton<SynchronizedObjectManager>
{
	[Header("Settings")]
	[SerializeField]
	private SnapshotInterpolationSettings snapshotInterpolationSettings;

	[SerializeField]
	private bool skipLateTicks = true;

	private int tickRate = 100;

	private bool useNetworkSmoothing;

	[HideInInspector]
	public float NetworkSmoothingStrength = 1f;

	private float serverTickAccumulator;

	private ushort serverLastSentTickId;

	private double serverLastSentServerTime;

	private ushort clientLastReceivedTickId;

	private double clientLastReceivedServerTime;

	private bool clientHasReceivedFirstTick;

	private float clientAccumulatedDeltaTime;

	private double clientLocalTimeline;

	private double clientLocalTimescale = 1.0;

	private List<SynchronizedObject> synchronizedObjects = new List<SynchronizedObject>();

	private List<ulong> synchronizedClientIds = new List<ulong>();

	private SortedList<double, SynchronizedObjectsSnapshot> snapshots = new SortedList<double, SynchronizedObjectsSnapshot>();

	private ExponentialMovingAverage driftEma;

	private ExponentialMovingAverage deliveryTimeEma;

	[HideInInspector]
	public int TickRate
	{
		get
		{
			return tickRate;
		}
		set
		{
			if (tickRate != value)
			{
				driftEma = new ExponentialMovingAverage(value * snapshotInterpolationSettings.driftEmaDuration);
				deliveryTimeEma = new ExponentialMovingAverage(value * snapshotInterpolationSettings.deliveryTimeEmaDuration);
				tickRate = value;
			}
		}
	}

	[HideInInspector]
	public bool UseNetworkSmoothing
	{
		get
		{
			return useNetworkSmoothing;
		}
		set
		{
			if (useNetworkSmoothing != value)
			{
				snapshots.Clear();
			}
			useNetworkSmoothing = value;
		}
	}

	[HideInInspector]
	public float TickInterval => 1f / (float)TickRate;

	private double clientBufferTime => (double)TickInterval * snapshotInterpolationSettings.bufferTimeMultiplier;

	public override void Awake()
	{
		base.Awake();
		driftEma = new ExponentialMovingAverage(TickRate * snapshotInterpolationSettings.driftEmaDuration);
		deliveryTimeEma = new ExponentialMovingAverage(TickRate * snapshotInterpolationSettings.deliveryTimeEmaDuration);
	}

	private void Update()
	{
		if (!base.IsSpawned)
		{
			return;
		}
		if (NetworkManager.Singleton.IsServer)
		{
			serverTickAccumulator += Time.deltaTime * (float)TickRate;
			if (serverTickAccumulator >= 1f)
			{
				while (serverTickAccumulator >= 1f)
				{
					serverTickAccumulator -= 1f;
				}
				Server_ServerTick();
			}
		}
		else if (UseNetworkSmoothing)
		{
			clientAccumulatedDeltaTime += Time.unscaledDeltaTime;
			if (snapshots.Count > 0)
			{
				SnapshotInterpolation.Step(snapshots, clientAccumulatedDeltaTime, ref clientLocalTimeline, clientLocalTimescale, out var fromSnapshot, out var toSnapshot, out var t);
				SynchronizedObjectsSnapshot.Interpolate(fromSnapshot, toSnapshot, t);
				clientAccumulatedDeltaTime = 0f;
			}
		}
	}

	public void Dispose()
	{
		serverTickAccumulator = 0f;
		serverLastSentTickId = 0;
		clientLastReceivedTickId = 0;
		clientHasReceivedFirstTick = false;
		clientAccumulatedDeltaTime = 0f;
		snapshots.Clear();
		ClearSynchronizedObjects();
		ClearSynchronizedClientIds();
	}

	public void AddSynchronizedObject(SynchronizedObject synchronizedObject)
	{
		synchronizedObjects.Add(synchronizedObject);
	}

	public void RemoveSynchronizedObject(SynchronizedObject synchronizedObject)
	{
		synchronizedObjects.Remove(synchronizedObject);
	}

	private void ClearSynchronizedObjects()
	{
		synchronizedObjects.Clear();
	}

	public void Server_AddSynchronizedClientId(ulong clientId)
	{
		synchronizedClientIds.Add(clientId);
	}

	public void Server_RemoveSynchronizedClientId(ulong clientId)
	{
		synchronizedClientIds.Remove(clientId);
	}

	private void ClearSynchronizedClientIds()
	{
		synchronizedClientIds.Clear();
	}

	private (ushort, short[], short[]) EncodeSynchronizedObject(ulong networkObjectId, Vector3 position, Quaternion rotation)
	{
		short num = (short)(rotation.x * 32767f);
		short num2 = (short)(rotation.y * 32767f);
		short num3 = (short)(rotation.z * 32767f);
		short num4 = (short)(rotation.w * 32767f);
		return ((ushort)networkObjectId, new short[3]
		{
			(short)(position.x * 655f),
			(short)(position.y * 655f),
			(short)(position.z * 655f)
		}, new short[4] { num, num2, num3, num4 });
	}

	private (ushort, Vector3, Quaternion) DecodeSynchronizedObjectData(SynchronizedObjectData synchronizedObjectData)
	{
		float x = (float)synchronizedObjectData.Rx / 32767f;
		float y = (float)synchronizedObjectData.Ry / 32767f;
		float z = (float)synchronizedObjectData.Rz / 32767f;
		float w = (float)synchronizedObjectData.Rw / 32767f;
		return new ValueTuple<ushort, Vector3, Quaternion>(item3: new Quaternion(x, y, z, w), item1: synchronizedObjectData.NetworkObjectId, item2: new Vector3((float)synchronizedObjectData.X / 655f, (float)synchronizedObjectData.Y / 655f, (float)synchronizedObjectData.Z / 655f));
	}

	private void Server_ServerTick()
	{
		serverLastSentTickId++;
		if (serverLastSentTickId >= ushort.MaxValue)
		{
			serverLastSentTickId = 0;
		}
		SynchronizedObjectData[] synchronizedObjectsData = Server_GatherSynchronizedObjectData();
		serverLastSentServerTime = NetworkManager.Singleton.ServerTime.Time;
		Server_SynchronizeObjectsRpc(serverLastSentTickId, serverLastSentServerTime, synchronizedObjectsData, base.RpcTarget.Group(synchronizedClientIds, RpcTargetUse.Persistent));
	}

	public void Server_ForceSynchronizeClientId(ulong clientId)
	{
		if (NetworkManager.Singleton.IsServer && clientId != 0L)
		{
			SynchronizedObjectData[] synchronizedObjectsData = Server_GatherSynchronizedObjectData(forceAllObjects: true);
			Server_SynchronizeObjectsRpc(serverLastSentTickId, serverLastSentServerTime, synchronizedObjectsData, base.RpcTarget.Single(clientId, RpcTargetUse.Persistent));
		}
	}

	private SynchronizedObjectData[] Server_GatherSynchronizedObjectData(bool forceAllObjects = false)
	{
		List<SynchronizedObjectData> list = new List<SynchronizedObjectData>();
		foreach (SynchronizedObject synchronizedObject in synchronizedObjects)
		{
			if ((bool)synchronizedObject && (forceAllObjects || synchronizedObject.ShouldSendPosition(TickRate) || synchronizedObject.ShouldSendRotation(TickRate)))
			{
				var (position, rotation, networkObjectId) = synchronizedObject.OnServerTick((float)(NetworkManager.Singleton.ServerTime.Time - serverLastSentServerTime));
				var (networkObjectId2, array, array2) = EncodeSynchronizedObject(networkObjectId, position, rotation);
				list.Add(new SynchronizedObjectData
				{
					NetworkObjectId = networkObjectId2,
					X = array[0],
					Y = array[1],
					Z = array[2],
					Rx = array2[0],
					Ry = array2[1],
					Rz = array2[2],
					Rw = array2[3]
				});
			}
		}
		return list.ToArray();
	}

	[Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Unreliable)]
	private void Server_SynchronizeObjectsRpc(ushort tickId, double serverTime, SynchronizedObjectData[] synchronizedObjectsData, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
			{
				Delivery = RpcDelivery.Unreliable
			};
			FastBufferWriter bufferWriter = __beginSendRpc(1738927239u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Unreliable);
			BytePacker.WriteValueBitPacked(bufferWriter, tickId);
			bufferWriter.WriteValueSafe(in serverTime, default(FastBufferWriter.ForPrimitives));
			bool value = synchronizedObjectsData != null;
			bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			if (value)
			{
				bufferWriter.WriteValueSafe(synchronizedObjectsData, default(FastBufferWriter.ForNetworkSerializable));
			}
			__endSendRpc(ref bufferWriter, 1738927239u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Unreliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		if (skipLateTicks && clientHasReceivedFirstTick && clientLastReceivedTickId - tickId < 32767 && tickId <= clientLastReceivedTickId)
		{
			Debug.Log($"[SynchronizedObjectManager] Dropped tick {tickId} because it's older than the last received tick {clientLastReceivedTickId}");
			return;
		}
		float num = (float)(serverTime - clientLastReceivedServerTime);
		Client_SynchronizeObjects(synchronizedObjectsData, num, serverTime);
		if (!clientHasReceivedFirstTick)
		{
			clientHasReceivedFirstTick = true;
		}
		clientLastReceivedTickId = tickId;
		clientLastReceivedServerTime = serverTime;
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSynchronizeObjects", new Dictionary<string, object> { { "serverDeltaTime", num } });
	}

	private void Client_SynchronizeObjects(SynchronizedObjectData[] synchronizedObjectsData, float serverDeltaTime, double serverTime)
	{
		SynchronizedObjectData[] array;
		if (UseNetworkSmoothing && clientHasReceivedFirstTick)
		{
			List<SynchronizedObjectSnapshot> list = new List<SynchronizedObjectSnapshot>();
			array = synchronizedObjectsData;
			foreach (SynchronizedObjectData synchronizedObjectData in array)
			{
				(ushort, Vector3, Quaternion) tuple = DecodeSynchronizedObjectData(synchronizedObjectData);
				ushort networkObjectId = tuple.Item1;
				Vector3 item = tuple.Item2;
				Quaternion item2 = tuple.Item3;
				SynchronizedObject synchronizedObject = synchronizedObjects.Find((SynchronizedObject synchronizedObject3) => synchronizedObject3.NetworkObjectId == networkObjectId);
				if (!(synchronizedObject == null))
				{
					SynchronizedObjectSnapshot item3 = synchronizedObject.OnClientSmoothTick(item, item2, synchronizedObject, serverDeltaTime);
					list.Add(item3);
				}
			}
			SynchronizedObjectsSnapshot snapshot = new SynchronizedObjectsSnapshot(serverTime, NetworkManager.Singleton.LocalTime.Time, list);
			if (snapshotInterpolationSettings.dynamicAdjustment)
			{
				snapshotInterpolationSettings.bufferTimeMultiplier = SnapshotInterpolation.DynamicAdjustment(TickInterval, deliveryTimeEma.StandardDeviation, snapshotInterpolationSettings.dynamicAdjustmentTolerance) * (double)NetworkSmoothingStrength;
			}
			SnapshotInterpolation.InsertAndAdjust(snapshots, snapshotInterpolationSettings.bufferLimit, snapshot, ref clientLocalTimeline, ref clientLocalTimescale, TickInterval, clientBufferTime, snapshotInterpolationSettings.catchupSpeed, snapshotInterpolationSettings.slowdownSpeed, ref driftEma, snapshotInterpolationSettings.catchupNegativeThreshold, snapshotInterpolationSettings.catchupPositiveThreshold, ref deliveryTimeEma);
			return;
		}
		array = synchronizedObjectsData;
		foreach (SynchronizedObjectData synchronizedObjectData2 in array)
		{
			(ushort, Vector3, Quaternion) tuple = DecodeSynchronizedObjectData(synchronizedObjectData2);
			ushort networkObjectId2 = tuple.Item1;
			Vector3 item4 = tuple.Item2;
			Quaternion item5 = tuple.Item3;
			SynchronizedObject synchronizedObject2 = synchronizedObjects.Find((SynchronizedObject synchronizedObject3) => synchronizedObject3.NetworkObjectId == networkObjectId2);
			if (!(synchronizedObject2 == null))
			{
				synchronizedObject2.OnClientTick(item4, item5, serverDeltaTime);
			}
		}
	}

	private void OnValidate()
	{
		snapshotInterpolationSettings.catchupNegativeThreshold = Mathf.Min(snapshotInterpolationSettings.catchupNegativeThreshold, 0f);
		snapshotInterpolationSettings.catchupPositiveThreshold = Mathf.Max(snapshotInterpolationSettings.catchupPositiveThreshold, 0f);
	}

	protected override void __initializeVariables()
	{
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		__registerRpc(1738927239u, __rpc_handler_1738927239, "Server_SynchronizeObjectsRpc");
		base.__initializeRpcs();
	}

	private static void __rpc_handler_1738927239(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out ushort value);
			reader.ReadValueSafe(out double value2, default(FastBufferWriter.ForPrimitives));
			reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
			SynchronizedObjectData[] value4 = null;
			if (value3)
			{
				reader.ReadValueSafe(out value4, default(FastBufferWriter.ForNetworkSerializable));
			}
			RpcParams ext = rpcParams.Ext;
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((SynchronizedObjectManager)target).Server_SynchronizeObjectsRpc(value, value2, value4, ext);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	protected internal override string __getTypeName()
	{
		return "SynchronizedObjectManager";
	}
}
