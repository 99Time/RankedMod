using System.Collections.Generic;
using Unity.Netcode;

public class SynchronizedAudioController : NetworkBehaviour
{
	private SynchronizedAudio synchronizedAudio;

	private void Awake()
	{
		synchronizedAudio = GetComponent<SynchronizedAudio>();
	}

	public override void OnNetworkSpawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnSynchronizeComplete", Event_Server_OnSynchronizeComplete);
		base.OnNetworkSpawn();
	}

	public override void OnNetworkDespawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnSynchronizeComplete", Event_Server_OnSynchronizeComplete);
		base.OnNetworkDespawn();
	}

	private void Event_Server_OnSynchronizeComplete(Dictionary<string, object> message)
	{
		ulong num = (ulong)message["clientId"];
		if (num != 0L)
		{
			synchronizedAudio.Server_ForceSynchronizeClientId(num);
		}
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
		return "SynchronizedAudioController";
	}
}
