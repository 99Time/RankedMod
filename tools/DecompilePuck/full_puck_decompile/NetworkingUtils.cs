using Unity.Netcode;

public static class NetworkingUtils
{
	public static Player GetPlayerFromNetworkObjectReference(NetworkObjectReference reference)
	{
		if (reference.TryGet(out var networkObject))
		{
			return networkObject.GetComponent<Player>();
		}
		return null;
	}
}
