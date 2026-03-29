using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerBodyV2Controller : NetworkBehaviour
{
	private PlayerBodyV2 playerBody;

	private void Awake()
	{
		playerBody = GetComponent<PlayerBodyV2>();
	}

	public override void OnNetworkSpawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerBodySpawned", Event_OnPlayerBodySpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerTeamChanged", Event_OnPlayerTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerUsernameChanged", Event_OnPlayerUsernameChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerNumberChanged", Event_OnPlayerNumberChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerCountryChanged", Event_OnPlayerCountryChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerVisorChanged", Event_OnPlayerVisorChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerMustacheChanged", Event_OnPlayerMustacheChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerBeardChanged", Event_OnPlayerBeardChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerJerseyChanged", Event_OnPlayerJerseyChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerVoiceStarted", Event_OnPlayerVoiceStarted);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerVoiceStopped", Event_OnPlayerVoiceStopped);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnPlayerJumpInput", Event_Server_OnPlayerJumpInput);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnPlayerDashLeftInput", Event_Server_OnPlayerDashLeftInput);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnPlayerDashRightInput", Event_Server_OnPlayerDashRightInput);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnPlayerTwistLeftInput", Event_Server_OnPlayerTwistLeftInput);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnPlayerTwistRightInput", Event_Server_OnPlayerTwistRightInput);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPlayerCameraEnabled", Event_Client_OnPlayerCameraEnabled);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPlayerCameraDisabled", Event_Client_OnPlayerCameraDisabled);
		if (NetworkManager.Singleton.IsServer)
		{
			playerBody.Stamina = 1f;
		}
		base.OnNetworkSpawn();
	}

	public override void OnNetworkDespawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerBodySpawned", Event_OnPlayerBodySpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerTeamChanged", Event_OnPlayerTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerUsernameChanged", Event_OnPlayerUsernameChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerNumberChanged", Event_OnPlayerNumberChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerCountryChanged", Event_OnPlayerCountryChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerVisorChanged", Event_OnPlayerVisorChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerMustacheChanged", Event_OnPlayerMustacheChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerBeardChanged", Event_OnPlayerBeardChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerJerseyChanged", Event_OnPlayerJerseyChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerVoiceStarted", Event_OnPlayerVoiceStarted);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerVoiceStopped", Event_OnPlayerVoiceStopped);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnPlayerJumpInput", Event_Server_OnPlayerJumpInput);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnPlayerDashLeftInput", Event_Server_OnPlayerDashLeftInput);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnPlayerDashRightInput", Event_Server_OnPlayerDashRightInput);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnPlayerTwistLeftInput", Event_Server_OnPlayerTwistLeftInput);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnPlayerTwistRightInput", Event_Server_OnPlayerTwistRightInput);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPlayerCameraEnabled", Event_Client_OnPlayerCameraEnabled);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPlayerCameraDisabled", Event_Client_OnPlayerCameraDisabled);
		base.OnNetworkDespawn();
	}

	private void Event_OnPlayerBodySpawned(Dictionary<string, object> message)
	{
		PlayerBodyV2 playerBodyV = (PlayerBodyV2)message["playerBody"];
		if (base.OwnerClientId == playerBodyV.OwnerClientId)
		{
			playerBodyV.UpdateMesh();
		}
		if (NetworkBehaviourSingleton<GameManager>.Instance.GameState.Value.Phase == GamePhase.FaceOff)
		{
			playerBodyV.Server_Freeze();
		}
		else
		{
			playerBodyV.Server_Unfreeze();
		}
	}

	private void Event_OnPlayerTeamChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			playerBody.UpdateMesh();
		}
	}

	private void Event_OnPlayerRoleChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			playerBody.UpdateMesh();
		}
	}

	private void Event_OnPlayerUsernameChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			playerBody.UpdateMesh();
		}
	}

	private void Event_OnPlayerNumberChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			playerBody.UpdateMesh();
		}
	}

	private void Event_OnPlayerCountryChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			playerBody.UpdateMesh();
		}
	}

	private void Event_OnPlayerVisorChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			playerBody.UpdateMesh();
		}
	}

	private void Event_OnPlayerMustacheChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			playerBody.UpdateMesh();
		}
	}

	private void Event_OnPlayerBeardChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			playerBody.UpdateMesh();
		}
	}

	private void Event_OnPlayerJerseyChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			playerBody.UpdateMesh();
		}
	}

	private void Event_OnGamePhaseChanged(Dictionary<string, object> message)
	{
		GamePhase gamePhase = (GamePhase)message["newGamePhase"];
		if (NetworkManager.Singleton.IsServer && (bool)playerBody.Player)
		{
			if (gamePhase == GamePhase.FaceOff)
			{
				playerBody.Server_Freeze();
			}
			else
			{
				playerBody.Server_Unfreeze();
			}
		}
	}

	private void Event_OnPlayerVoiceStarted(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		AudioClip clip = (AudioClip)message["audioClip"];
		if (base.OwnerClientId == player.OwnerClientId && !player.IsLocalPlayer)
		{
			playerBody.VoiceAudioSource.clip = clip;
			playerBody.VoiceAudioSource.loop = true;
			playerBody.VoiceAudioSource.Play();
		}
	}

	private void Event_OnPlayerVoiceStopped(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId && !player.IsLocalPlayer)
		{
			playerBody.VoiceAudioSource.Stop();
		}
	}

	private void Event_Server_OnPlayerJumpInput(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			playerBody.Jump();
		}
	}

	private void Event_Server_OnPlayerTwistLeftInput(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			playerBody.TwistLeft();
		}
	}

	private void Event_Server_OnPlayerTwistRightInput(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			playerBody.TwistRight();
		}
	}

	private void Event_Server_OnPlayerDashLeftInput(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			playerBody.DashLeft();
		}
	}

	private void Event_Server_OnPlayerDashRightInput(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			playerBody.DashRight();
		}
	}

	private void Event_Client_OnPlayerCameraEnabled(Dictionary<string, object> message)
	{
		PlayerCamera playerCamera = (PlayerCamera)message["playerCamera"];
		if (base.OwnerClientId == playerCamera.OwnerClientId)
		{
			playerBody.MeshRendererHider.HideMeshRenderers();
		}
	}

	private void Event_Client_OnPlayerCameraDisabled(Dictionary<string, object> message)
	{
		PlayerCamera playerCamera = (PlayerCamera)message["playerCamera"];
		if (base.OwnerClientId == playerCamera.OwnerClientId)
		{
			playerBody.MeshRendererHider.ShowMeshRenderers();
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
		return "PlayerBodyV2Controller";
	}
}
