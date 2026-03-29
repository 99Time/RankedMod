using System;
using Unity.Netcode;

public struct NetworkObjectCollision : INetworkSerializable, IEquatable<NetworkObjectCollision>
{
	public NetworkObjectReference NetworkObjectReference;

	public float Time;

	public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
	{
		if (serializer.IsReader)
		{
			FastBufferReader fastBufferReader = serializer.GetFastBufferReader();
			fastBufferReader.ReadValueSafe(out NetworkObjectReference, default(FastBufferWriter.ForNetworkSerializable));
			fastBufferReader.ReadValueSafe(out Time, default(FastBufferWriter.ForPrimitives));
		}
		else
		{
			FastBufferWriter fastBufferWriter = serializer.GetFastBufferWriter();
			fastBufferWriter.WriteValueSafe(in NetworkObjectReference, default(FastBufferWriter.ForNetworkSerializable));
			fastBufferWriter.WriteValueSafe(in Time, default(FastBufferWriter.ForPrimitives));
		}
	}

	public bool Equals(NetworkObjectCollision other)
	{
		if (NetworkObjectReference.Equals(other.NetworkObjectReference))
		{
			return Time == other.Time;
		}
		return false;
	}
}
