using System;
using Unity.Collections;
using Unity.Netcode;

public struct Server : INetworkSerializable, IEquatable<Server>
{
	public FixedString32Bytes IpAddress;

	public ushort Port;

	public ushort PingPort;

	public FixedString128Bytes Name;

	public int MaxPlayers;

	public FixedString32Bytes Password;

	public bool Voip;

	public bool IsPublic;

	public bool IsDedicated;

	public bool IsHosted;

	public bool IsAuthenticated;

	public FixedString32Bytes OwnerSteamId;

	public float SleepTimeout;

	public int ClientTickRate;

	public ulong[] ClientRequiredModIds;

	public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
	{
		if (serializer.IsReader)
		{
			FastBufferReader fastBufferReader = serializer.GetFastBufferReader();
			fastBufferReader.ReadValueSafe(out IpAddress, default(FastBufferWriter.ForFixedStrings));
			fastBufferReader.ReadValueSafe(out Port, default(FastBufferWriter.ForPrimitives));
			fastBufferReader.ReadValueSafe(out PingPort, default(FastBufferWriter.ForPrimitives));
			fastBufferReader.ReadValueSafe(out Name, default(FastBufferWriter.ForFixedStrings));
			fastBufferReader.ReadValueSafe(out MaxPlayers, default(FastBufferWriter.ForPrimitives));
			fastBufferReader.ReadValueSafe(out Password, default(FastBufferWriter.ForFixedStrings));
			fastBufferReader.ReadValueSafe(out Voip, default(FastBufferWriter.ForPrimitives));
			fastBufferReader.ReadValueSafe(out IsPublic, default(FastBufferWriter.ForPrimitives));
			fastBufferReader.ReadValueSafe(out IsDedicated, default(FastBufferWriter.ForPrimitives));
			fastBufferReader.ReadValueSafe(out IsHosted, default(FastBufferWriter.ForPrimitives));
			fastBufferReader.ReadValueSafe(out IsAuthenticated, default(FastBufferWriter.ForPrimitives));
			fastBufferReader.ReadValueSafe(out OwnerSteamId, default(FastBufferWriter.ForFixedStrings));
			fastBufferReader.ReadValueSafe(out SleepTimeout, default(FastBufferWriter.ForPrimitives));
			fastBufferReader.ReadValueSafe(out ClientTickRate, default(FastBufferWriter.ForPrimitives));
			fastBufferReader.ReadValueSafe(out ClientRequiredModIds, default(FastBufferWriter.ForPrimitives));
		}
		else
		{
			FastBufferWriter fastBufferWriter = serializer.GetFastBufferWriter();
			fastBufferWriter.WriteValueSafe(in IpAddress, default(FastBufferWriter.ForFixedStrings));
			fastBufferWriter.WriteValueSafe(in Port, default(FastBufferWriter.ForPrimitives));
			fastBufferWriter.WriteValueSafe(in PingPort, default(FastBufferWriter.ForPrimitives));
			fastBufferWriter.WriteValueSafe(in Name, default(FastBufferWriter.ForFixedStrings));
			fastBufferWriter.WriteValueSafe(in MaxPlayers, default(FastBufferWriter.ForPrimitives));
			fastBufferWriter.WriteValueSafe(in Password, default(FastBufferWriter.ForFixedStrings));
			fastBufferWriter.WriteValueSafe(in Voip, default(FastBufferWriter.ForPrimitives));
			fastBufferWriter.WriteValueSafe(in IsPublic, default(FastBufferWriter.ForPrimitives));
			fastBufferWriter.WriteValueSafe(in IsDedicated, default(FastBufferWriter.ForPrimitives));
			fastBufferWriter.WriteValueSafe(in IsHosted, default(FastBufferWriter.ForPrimitives));
			fastBufferWriter.WriteValueSafe(in IsAuthenticated, default(FastBufferWriter.ForPrimitives));
			fastBufferWriter.WriteValueSafe(in OwnerSteamId, default(FastBufferWriter.ForFixedStrings));
			fastBufferWriter.WriteValueSafe(in SleepTimeout, default(FastBufferWriter.ForPrimitives));
			fastBufferWriter.WriteValueSafe(in ClientTickRate, default(FastBufferWriter.ForPrimitives));
			fastBufferWriter.WriteValueSafe(ClientRequiredModIds, default(FastBufferWriter.ForPrimitives));
		}
	}

	public bool Equals(Server other)
	{
		if (IpAddress == other.IpAddress && Port == other.Port && PingPort == other.PingPort && Name == other.Name && MaxPlayers == other.MaxPlayers && Password == other.Password && Voip == other.Voip && IsPublic == other.IsPublic && IsDedicated == other.IsDedicated && IsHosted == other.IsHosted && IsAuthenticated == other.IsAuthenticated && OwnerSteamId == other.OwnerSteamId && SleepTimeout == other.SleepTimeout && ClientTickRate == other.ClientTickRate)
		{
			return ClientRequiredModIds == other.ClientRequiredModIds;
		}
		return false;
	}
}
