using System.Collections.Generic;
using UnityEngine;

public class UIPlayerUsernamesController : MonoBehaviour
{
	private UIPlayerUsernames uiPlayerUsernames;

	private void Awake()
	{
		uiPlayerUsernames = GetComponent<UIPlayerUsernames>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerBodySpawned", Event_OnPlayerBodySpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerBodyDespawned", Event_OnPlayerBodyDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerUsernameChanged", Event_OnPlayerUsernameChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerNumberChanged", Event_OnPlayerNumberChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnShowPlayerUsernamesChanged", Event_Client_OnShowPlayerUsernamesChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPlayerUsernamesFadeThresholdChanged", Event_Client_OnPlayerUsernamesFadeThresholdChanged);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerBodySpawned", Event_OnPlayerBodySpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerBodyDespawned", Event_OnPlayerBodyDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerUsernameChanged", Event_OnPlayerUsernameChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerNumberChanged", Event_OnPlayerNumberChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnShowPlayerUsernamesChanged", Event_Client_OnShowPlayerUsernamesChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPlayerUsernamesFadeThresholdChanged", Event_Client_OnPlayerUsernamesFadeThresholdChanged);
	}

	private void Event_OnPlayerBodySpawned(Dictionary<string, object> message)
	{
		PlayerBodyV2 playerBody = (PlayerBodyV2)message["playerBody"];
		uiPlayerUsernames.AddPlayerBody(playerBody);
	}

	private void Event_OnPlayerBodyDespawned(Dictionary<string, object> message)
	{
		PlayerBodyV2 playerBody = (PlayerBodyV2)message["playerBody"];
		uiPlayerUsernames.RemovePlayerBody(playerBody);
	}

	private void Event_OnPlayerUsernameChanged(Dictionary<string, object> message)
	{
		PlayerBodyV2 playerBody = ((Player)message["player"]).PlayerBody;
		if ((bool)playerBody)
		{
			uiPlayerUsernames.UpdatePlayerBody(playerBody);
		}
	}

	private void Event_OnPlayerNumberChanged(Dictionary<string, object> message)
	{
		PlayerBodyV2 playerBody = ((Player)message["player"]).PlayerBody;
		if ((bool)playerBody)
		{
			uiPlayerUsernames.UpdatePlayerBody(playerBody);
		}
	}

	private void Event_Client_OnShowPlayerUsernamesChanged(Dictionary<string, object> message)
	{
		if ((bool)message["value"])
		{
			uiPlayerUsernames.Show();
		}
		else
		{
			uiPlayerUsernames.Hide(ignoreAlwaysVisible: true);
		}
	}

	private void Event_Client_OnPlayerUsernamesFadeThresholdChanged(Dictionary<string, object> message)
	{
		float fadeThreshold = (float)message["value"];
		uiPlayerUsernames.FadeThreshold = fadeThreshold;
	}
}
