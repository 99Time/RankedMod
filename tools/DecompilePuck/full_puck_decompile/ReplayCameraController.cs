using System.Collections.Generic;

public class ReplayCameraController : BaseCameraController
{
	private ReplayCamera replayCamera;

	public override void Awake()
	{
		base.Awake();
		replayCamera = GetComponent<ReplayCamera>();
	}

	public override void Start()
	{
		base.Start();
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPuckSpawned", Event_OnPuckSpawned);
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPuckSpawned", Event_OnPuckSpawned);
		base.OnDestroy();
	}

	private void Event_OnPuckSpawned(Dictionary<string, object> message)
	{
		Puck puck = (Puck)message["puck"];
		replayCamera.SetTarget(puck.transform);
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
		return "ReplayCameraController";
	}
}
