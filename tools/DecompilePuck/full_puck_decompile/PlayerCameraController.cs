public class PlayerCameraController : BaseCameraController
{
	public PlayerCamera playerCamera;

	public override void Awake()
	{
		base.Awake();
		playerCamera = GetComponent<PlayerCamera>();
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
		return "PlayerCameraController";
	}
}
