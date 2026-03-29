using System;
using Unity.Netcode;

public struct SynchronizedObjectData : INetworkSerializable, IEquatable<SynchronizedObjectData>
{
	public ushort NetworkObjectId;

	public short X;

	public short Y;

	public short Z;

	public short Rx;

	public short Ry;

	public short Rz;

	public short Rw;

	public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
	{
		if (serializer.IsReader)
		{
			FastBufferReader fastBufferReader = serializer.GetFastBufferReader();
			fastBufferReader.ReadValueSafe(out NetworkObjectId, default(FastBufferWriter.ForPrimitives));
			fastBufferReader.ReadValueSafe(out X, default(FastBufferWriter.ForPrimitives));
			fastBufferReader.ReadValueSafe(out Y, default(FastBufferWriter.ForPrimitives));
			fastBufferReader.ReadValueSafe(out Z, default(FastBufferWriter.ForPrimitives));
			fastBufferReader.ReadValueSafe(out Rx, default(FastBufferWriter.ForPrimitives));
			fastBufferReader.ReadValueSafe(out Ry, default(FastBufferWriter.ForPrimitives));
			fastBufferReader.ReadValueSafe(out Rz, default(FastBufferWriter.ForPrimitives));
			fastBufferReader.ReadValueSafe(out Rw, default(FastBufferWriter.ForPrimitives));
		}
		else
		{
			FastBufferWriter fastBufferWriter = serializer.GetFastBufferWriter();
			fastBufferWriter.WriteValueSafe(in NetworkObjectId, default(FastBufferWriter.ForPrimitives));
			fastBufferWriter.WriteValueSafe(in X, default(FastBufferWriter.ForPrimitives));
			fastBufferWriter.WriteValueSafe(in Y, default(FastBufferWriter.ForPrimitives));
			fastBufferWriter.WriteValueSafe(in Z, default(FastBufferWriter.ForPrimitives));
			fastBufferWriter.WriteValueSafe(in Rx, default(FastBufferWriter.ForPrimitives));
			fastBufferWriter.WriteValueSafe(in Ry, default(FastBufferWriter.ForPrimitives));
			fastBufferWriter.WriteValueSafe(in Rz, default(FastBufferWriter.ForPrimitives));
			fastBufferWriter.WriteValueSafe(in Rw, default(FastBufferWriter.ForPrimitives));
		}
	}

	public bool Equals(SynchronizedObjectData other)
	{
		if (NetworkObjectId == other.NetworkObjectId && X == other.X && Y == other.Y && Z == other.Z && Rx == other.Rx && Ry == other.Ry && Rz == other.Rz)
		{
			return Rw == other.Rw;
		}
		return false;
	}
}
