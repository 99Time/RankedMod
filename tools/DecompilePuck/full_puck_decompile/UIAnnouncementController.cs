using System.Collections.Generic;
using Unity.Netcode;

internal class UIAnnouncementController : NetworkBehaviour
{
	private UIAnnouncement uiAnnouncement;

	private void Awake()
	{
		uiAnnouncement = GetComponent<UIAnnouncement>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGoalScored", Event_OnGoalScored);
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnGoalScored", Event_OnGoalScored);
		base.OnDestroy();
	}

	public void Event_OnGoalScored(Dictionary<string, object> message)
	{
		PlayerTeam playerTeam = (PlayerTeam)message["team"];
		bool flag = (bool)message["hasGoalPlayer"];
		ulong clientId = (ulong)message["goalPlayerClientId"];
		bool flag2 = (bool)message["hasAssistPlayer"];
		ulong clientId2 = (ulong)message["assistPlayerClientId"];
		bool flag3 = (bool)message["hasSecondAssistPlayer"];
		ulong clientId3 = (ulong)message["secondAssistPlayerClientId"];
		switch (playerTeam)
		{
		case PlayerTeam.Blue:
			uiAnnouncement.ShowBlueTeamScoreAnnouncement(3f, flag ? NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(clientId) : null, flag2 ? NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(clientId2) : null, flag3 ? NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(clientId3) : null);
			break;
		case PlayerTeam.Red:
			uiAnnouncement.ShowRedTeamScoreAnnouncement(3f, flag ? NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(clientId) : null, flag2 ? NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(clientId2) : null, flag3 ? NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(clientId3) : null);
			break;
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
		return "UIAnnouncementController";
	}
}
