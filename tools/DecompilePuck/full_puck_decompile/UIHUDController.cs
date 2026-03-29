using System.Collections.Generic;
using Unity.Netcode;

public class UIHUDController : NetworkBehaviour
{
	private UIHUD uiHud;

	private bool hasTarget;

	private ulong targetClientId;

	private void Awake()
	{
		uiHud = GetComponent<UIHUD>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerBodySpawned", Event_OnPlayerBodySpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerBodyStaminaChanged", Event_OnPlayerBodyStaminaChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerBodySpeedChanged", Event_OnPlayerBodySpeedChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnUnitsChanged", Event_Client_OnUnitsChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPlayerCameraEnabled", Event_Client_OnPlayerCameraEnabled);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPlayerCameraDisabled", Event_Client_OnPlayerCameraDisabled);
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerBodySpawned", Event_OnPlayerBodySpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerBodyStaminaChanged", Event_OnPlayerBodyStaminaChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerBodySpeedChanged", Event_OnPlayerBodySpeedChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnUnitsChanged", Event_Client_OnUnitsChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPlayerCameraEnabled", Event_Client_OnPlayerCameraEnabled);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPlayerCameraDisabled", Event_Client_OnPlayerCameraDisabled);
		base.OnDestroy();
	}

	private void Event_OnPlayerBodySpawned(Dictionary<string, object> message)
	{
		PlayerBodyV2 playerBodyV = (PlayerBodyV2)message["playerBody"];
		if (playerBodyV.Player.IsLocalPlayer)
		{
			hasTarget = true;
			targetClientId = playerBodyV.OwnerClientId;
			uiHud.Show();
			uiHud.SetStamina(playerBodyV.Stamina);
		}
	}

	private void Event_OnPlayerBodyStaminaChanged(Dictionary<string, object> message)
	{
		PlayerBodyV2 playerBodyV = (PlayerBodyV2)message["playerBody"];
		float stamina = (float)message["newStamina"];
		if (hasTarget && playerBodyV.OwnerClientId == targetClientId)
		{
			uiHud.SetStamina(stamina);
		}
	}

	private void Event_OnPlayerBodySpeedChanged(Dictionary<string, object> message)
	{
		PlayerBodyV2 playerBodyV = (PlayerBodyV2)message["playerBody"];
		float speed = (float)message["newSpeed"];
		if (hasTarget && playerBodyV.OwnerClientId == targetClientId)
		{
			uiHud.SetSpeed(speed);
		}
	}

	private void Event_Client_OnUnitsChanged(Dictionary<string, object> message)
	{
		string text = (string)message["value"];
		if (!(text == "METRIC"))
		{
			if (text == "FREEDOM")
			{
				uiHud.SetUnits("MPH");
			}
		}
		else
		{
			uiHud.SetUnits("KPH");
		}
	}

	private void Event_Client_OnPlayerCameraEnabled(Dictionary<string, object> message)
	{
		PlayerCamera playerCamera = (PlayerCamera)message["playerCamera"];
		if (hasTarget && playerCamera.OwnerClientId == targetClientId)
		{
			uiHud.Show();
		}
	}

	private void Event_Client_OnPlayerCameraDisabled(Dictionary<string, object> message)
	{
		PlayerCamera playerCamera = (PlayerCamera)message["playerCamera"];
		if (hasTarget && playerCamera.OwnerClientId == targetClientId)
		{
			uiHud.Hide();
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
		return "UIHUDController";
	}
}
