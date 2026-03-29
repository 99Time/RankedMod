using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkObjectCollisionBuffer : NetworkBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private int bufferSize = 10;

	[SerializeField]
	private LayerMask collisionLayers;

	private NetworkList<NetworkObjectCollision> buffer;

	[HideInInspector]
	public readonly List<NetworkObjectCollision> Buffer = new List<NetworkObjectCollision>();

	private void Awake()
	{
		buffer = new NetworkList<NetworkObjectCollision>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
	}

	public override void OnNetworkSpawn()
	{
		buffer.Initialize(this);
		buffer.OnListChanged += OnBufferChanged;
		Client_InitializeNetworkVariables();
		base.OnNetworkSpawn();
	}

	protected override void OnNetworkSessionSynchronized()
	{
		Client_InitializeNetworkVariables();
		base.OnNetworkSessionSynchronized();
	}

	public override void OnNetworkDespawn()
	{
		buffer.OnListChanged -= OnBufferChanged;
		buffer.Dispose();
		base.OnNetworkDespawn();
	}

	private void OnBufferChanged(NetworkListEvent<NetworkObjectCollision> changeEvent)
	{
		Buffer.Clear();
		foreach (NetworkObjectCollision item in buffer)
		{
			Buffer.Add(item);
		}
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (!NetworkManager.Singleton.IsServer || (collisionLayers.value & (1 << collision.gameObject.layer)) == 0)
		{
			return;
		}
		NetworkObject component = collision.gameObject.GetComponent<NetworkObject>();
		if (!component)
		{
			return;
		}
		NetworkObjectReference networkObjectReference = new NetworkObjectReference(component);
		NetworkObjectCollision item = default(NetworkObjectCollision);
		foreach (NetworkObjectCollision item2 in buffer)
		{
			NetworkObjectReference networkObjectReference2 = item2.NetworkObjectReference;
			if (networkObjectReference2.Equals(networkObjectReference))
			{
				item = item2;
				break;
			}
		}
		if (buffer.Contains(item))
		{
			buffer.Remove(item);
		}
		if (buffer.Count >= bufferSize)
		{
			buffer.RemoveAt(0);
		}
		buffer.Add(new NetworkObjectCollision
		{
			NetworkObjectReference = networkObjectReference,
			Time = Time.time
		});
	}

	public void Clear()
	{
		buffer.Clear();
		Buffer.Clear();
	}

	public void Client_InitializeNetworkVariables()
	{
		OnBufferChanged(new NetworkListEvent<NetworkObjectCollision>
		{
			Type = NetworkListEvent<NetworkObjectCollision>.EventType.Value
		});
	}

	protected override void __initializeVariables()
	{
		if (buffer == null)
		{
			throw new Exception("NetworkObjectCollisionBuffer.buffer cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		buffer.Initialize(this);
		__nameNetworkVariable(buffer, "buffer");
		NetworkVariableFields.Add(buffer);
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		base.__initializeRpcs();
	}

	protected internal override string __getTypeName()
	{
		return "NetworkObjectCollisionBuffer";
	}
}
