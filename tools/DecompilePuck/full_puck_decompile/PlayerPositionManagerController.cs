using System.Collections.Generic;
using Unity.Netcode;

public class PlayerPositionManagerController : NetworkBehaviour
{
	private PlayerPositionManager playerPositionManager;

	private void Awake()
	{
		playerPositionManager = GetComponent<PlayerPositionManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnLevelStarted", Event_OnLevelStarted);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPositionSelectClickPosition", Event_Client_OnPositionSelectClickPosition);
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnLevelStarted", Event_OnLevelStarted);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPositionSelectClickPosition", Event_Client_OnPositionSelectClickPosition);
		base.OnDestroy();
	}

	private void Event_OnLevelStarted(Dictionary<string, object> message)
	{
		List<PlayerPosition> bluePositions = (List<PlayerPosition>)message["playerBluePositions"];
		List<PlayerPosition> redPositions = (List<PlayerPosition>)message["playerRedPositions"];
		playerPositionManager.SetBluePositions(bluePositions);
		playerPositionManager.SetRedPositions(redPositions);
	}

	private void Event_Client_OnPositionSelectClickPosition(Dictionary<string, object> message)
	{
		PlayerPosition playerPosition = (PlayerPosition)message["playerPosition"];
		playerPositionManager.Client_ClaimPositionRpc(new NetworkObjectReference(playerPosition.NetworkObject));
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
		return "PlayerPositionManagerController";
	}
}
