using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class ChangingRoomManager : MonoBehaviourSingleton<ChangingRoomManager>
{
	[Header("References")]
	[SerializeField]
	private BaseCamera mainCamera;

	[SerializeField]
	private Transform playerCameraPosition;

	[SerializeField]
	private Transform identityCameraPosition;

	[SerializeField]
	private Transform appearanceDefaultCameraPosition;

	[SerializeField]
	private Transform appearanceHeadCameraPosition;

	[SerializeField]
	private Transform appearanceJerseyCameraPosition;

	private PlayerTeam team;

	private PlayerRole role;

	private Vector3 initialMainCameraPosition;

	private Vector3 inititalMainCameraRotation;

	public PlayerTeam Team
	{
		get
		{
			return team;
		}
		set
		{
			if (team != value)
			{
				team = value;
				OnTeamChanged();
			}
		}
	}

	public PlayerRole Role
	{
		get
		{
			return role;
		}
		set
		{
			if (role != value)
			{
				role = value;
				OnRoleChanged();
			}
		}
	}

	public override void Awake()
	{
		base.Awake();
		DestroyOnLoad();
		initialMainCameraPosition = mainCamera.transform.position;
		inititalMainCameraRotation = mainCamera.transform.eulerAngles;
	}

	private void OnDestroy()
	{
		mainCamera.transform.DOKill();
	}

	public void Client_DisableAllCameras()
	{
		mainCamera.Disable();
	}

	public void Client_EnableMainCamera()
	{
		Client_DisableAllCameras();
		mainCamera.Enable();
	}

	public void Client_MoveCameraToDefaultPosition()
	{
		mainCamera.transform.DOKill();
		mainCamera.transform.DOMove(initialMainCameraPosition, 0.5f);
		mainCamera.transform.DORotate(inititalMainCameraRotation, 0.5f);
	}

	public void Client_MoveCameraToPlayerPosition()
	{
		mainCamera.transform.DOKill();
		mainCamera.transform.DOMove(playerCameraPosition.position, 0.5f);
		mainCamera.transform.DORotate(playerCameraPosition.eulerAngles, 0.5f);
	}

	public void Client_MoveCameraToIdentityPosition()
	{
		mainCamera.transform.DOKill();
		mainCamera.transform.DOMove(identityCameraPosition.position, 0.5f);
		mainCamera.transform.DORotate(identityCameraPosition.eulerAngles, 0.5f);
	}

	public void Client_MoveCameraToAppearanceDefaultPosition()
	{
		mainCamera.transform.DOKill();
		mainCamera.transform.DOMove(appearanceDefaultCameraPosition.position, 0.5f);
		mainCamera.transform.DORotate(appearanceDefaultCameraPosition.eulerAngles, 0.5f);
	}

	public void Client_MoveCameraToAppearanceHeadPosition()
	{
		mainCamera.transform.DOKill();
		mainCamera.transform.DOMove(appearanceHeadCameraPosition.position, 0.5f);
		mainCamera.transform.DORotate(appearanceHeadCameraPosition.eulerAngles, 0.5f);
	}

	public void Client_MoveCameraToAppearanceJerseyPosition()
	{
		mainCamera.transform.DOKill();
		mainCamera.transform.DOMove(appearanceJerseyCameraPosition.position, 0.5f);
		mainCamera.transform.DORotate(appearanceJerseyCameraPosition.eulerAngles, 0.5f);
	}

	private void OnTeamChanged()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnChangingRoomTeamChanged", new Dictionary<string, object> { { "team", Team } });
	}

	private void OnRoleChanged()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnChangingRoomRoleChanged", new Dictionary<string, object> { { "role", Role } });
	}
}
