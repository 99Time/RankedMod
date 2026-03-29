using Unity.Netcode;

public class SynchronizedObjectController : NetworkBehaviour
{
	private SynchronizedObject synchronizedObject;

	private void Awake()
	{
		synchronizedObject = GetComponent<SynchronizedObject>();
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
	}

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();
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
		return "SynchronizedObjectController";
	}
}
