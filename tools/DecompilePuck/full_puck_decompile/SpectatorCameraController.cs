public class SpectatorCameraController : BaseCameraController
{
	private SpectatorCamera spectatorCamera;

	public override void Awake()
	{
		base.Awake();
		spectatorCamera = GetComponent<SpectatorCamera>();
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
		return "SpectatorCameraController";
	}
}
