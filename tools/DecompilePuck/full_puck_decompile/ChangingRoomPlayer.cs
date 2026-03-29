using DG.Tweening;
using UnityEngine;

public class ChangingRoomPlayer : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	public PlayerMesh PlayerMesh;

	[SerializeField]
	public Transform identityPosition;

	[Header("Settings")]
	[SerializeField]
	private float lookAtDistance = 5f;

	[HideInInspector]
	public bool TrackMouse = true;

	public bool RotateWithMouse;

	private PlayerTeam team;

	private PlayerRole role;

	private Vector2 lastMousePosition;

	private Vector2 mousePosition;

	private Vector2 mouseRotationDelta;

	private Vector2 mouseRotationInertia;

	private Vector3 mouseWorldPosition;

	private Vector3 mouseCameraLocalPosition;

	private Vector3 initialPosition;

	private Vector3 initialRotation;

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

	private void Start()
	{
		lastMousePosition = MonoBehaviourSingleton<InputManager>.Instance.PointAction.ReadValue<Vector2>();
		initialPosition = base.transform.position;
		initialRotation = base.transform.eulerAngles;
	}

	private void OnDestroy()
	{
		base.transform.DOKill();
	}

	private void Update()
	{
		lastMousePosition = mousePosition;
		mousePosition = MonoBehaviourSingleton<InputManager>.Instance.PointAction.ReadValue<Vector2>();
		if (TrackMouse)
		{
			LookAtMouse();
		}
		if (RotateWithMouse)
		{
			Rotate();
		}
	}

	private void LookAtMouse()
	{
		if (!(Camera.main == null))
		{
			mouseWorldPosition = Camera.main.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, 1f));
			mouseCameraLocalPosition = Camera.main.transform.InverseTransformPoint(mouseWorldPosition);
			mouseCameraLocalPosition.x = 0f - mouseCameraLocalPosition.x;
			mouseCameraLocalPosition.y += Camera.main.transform.position.y;
			PlayerMesh.LookAt(base.transform.TransformPoint(mouseCameraLocalPosition) + base.transform.forward * lookAtDistance, Time.deltaTime);
		}
	}

	private void Rotate()
	{
		mouseRotationInertia = Vector2.Lerp(mouseRotationInertia, mouseRotationDelta, Time.deltaTime * 10f);
		base.transform.rotation *= Quaternion.AngleAxis(0f - mouseRotationInertia.x, Vector3.up);
		if (MonoBehaviourSingleton<InputManager>.Instance.ClickAction.IsPressed())
		{
			mouseRotationDelta = mousePosition - lastMousePosition;
		}
		else
		{
			mouseRotationDelta = Vector2.zero;
		}
	}

	public void Client_MovePlayerToDefaultPosition()
	{
		base.transform.DOKill();
		base.transform.DOMove(initialPosition, 0.5f);
		base.transform.DORotate(initialRotation, 0.5f);
	}

	public void Client_MovePlayerToIdentityPosition()
	{
		base.transform.DOKill();
		base.transform.DOMove(identityPosition.position, 0.5f);
		base.transform.DORotate(identityPosition.eulerAngles, 0.5f);
	}

	public void UpdatePlayerMesh()
	{
		PlayerMesh.SetUsername(MonoBehaviourSingleton<StateManager>.Instance.PlayerData.username);
		PlayerMesh.SetNumber(MonoBehaviourSingleton<StateManager>.Instance.PlayerData.number.ToString());
		PlayerMesh.SetJersey(Team, MonoBehaviourSingleton<SettingsManager>.Instance.GetJerseySkin(Team, Role));
		PlayerMesh.SetRole(Role);
		PlayerMesh.PlayerHead.SetHelmetFlag(MonoBehaviourSingleton<SettingsManager>.Instance.Country);
		PlayerMesh.PlayerHead.SetHelmetVisor(MonoBehaviourSingleton<SettingsManager>.Instance.GetVisorSkin(Team, Role));
		PlayerMesh.PlayerHead.SetMustache(MonoBehaviourSingleton<SettingsManager>.Instance.Mustache);
		PlayerMesh.PlayerHead.SetBeard(MonoBehaviourSingleton<SettingsManager>.Instance.Beard);
	}

	private void OnTeamChanged()
	{
		UpdatePlayerMesh();
	}

	private void OnRoleChanged()
	{
		UpdatePlayerMesh();
	}
}
