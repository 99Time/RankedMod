using UnityEngine;

namespace Codebase;

public static class ServerFunc
{
	public static bool IsDedicatedServer()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Invalid comparison between Unknown and I4
		return (int)SystemInfo.graphicsDeviceType == 4;
	}
}
