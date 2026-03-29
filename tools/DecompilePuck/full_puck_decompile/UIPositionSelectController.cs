using System.Collections.Generic;
using Unity.Netcode;

internal class UIPositionSelectController : NetworkBehaviour
{
	private UIPositionSelect uiPositionSelect;

	private void Awake()
	{
		uiPositionSelect = GetComponent<UIPositionSelect>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerTeamChanged", Event_OnPlayerTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerPositionClaimedByChanged", Event_OnPlayerPositionClaimedByChanged);
	}

	public override void OnNetworkDespawn()
	{
		uiPositionSelect.ClearPositions();
		base.OnNetworkDespawn();
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerTeamChanged", Event_OnPlayerTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerPositionClaimedByChanged", Event_OnPlayerPositionClaimedByChanged);
	}

	private void Event_OnPlayerTeamChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (!player.IsLocalPlayer)
		{
			return;
		}
		uiPositionSelect.ClearPositions();
		switch (player.Team.Value)
		{
		case PlayerTeam.Blue:
			NetworkBehaviourSingleton<PlayerPositionManager>.Instance.BluePositions.ForEach(delegate(PlayerPosition playerPosition)
			{
				uiPositionSelect.AddPosition(playerPosition);
			});
			break;
		case PlayerTeam.Red:
			NetworkBehaviourSingleton<PlayerPositionManager>.Instance.RedPositions.ForEach(delegate(PlayerPosition playerPosition)
			{
				uiPositionSelect.AddPosition(playerPosition);
			});
			break;
		}
	}

	private void Event_OnPlayerPositionClaimedByChanged(Dictionary<string, object> message)
	{
		PlayerPosition playerPosition = (PlayerPosition)message["playerPosition"];
		uiPositionSelect.UpdatePosition(playerPosition);
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
		return "UIPositionSelectController";
	}
}
