using DG.Tweening;
using UnityEngine;

public class ChangingRoomStick : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	public StickMesh StickMesh;

	[SerializeField]
	public Transform appearanceStickPosition;

	[Header("Settings")]
	public PlayerRole Role = PlayerRole.Attacker;

	private PlayerTeam team;

	public bool RotateWithMouse;

	private Vector2 lastMousePosition;

	private Vector2 mousePosition;

	private Vector2 mouseRotationDelta;

	private Vector2 mouseRotationInertia;

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

	private void Start()
	{
		lastMousePosition = MonoBehaviourSingleton<InputManager>.Instance.PointAction.ReadValue<Vector2>();
		initialPosition = base.transform.position;
		initialRotation = base.transform.eulerAngles;
	}

	private void Update()
	{
		if (RotateWithMouse)
		{
			lastMousePosition = mousePosition;
			mousePosition = MonoBehaviourSingleton<InputManager>.Instance.PointAction.ReadValue<Vector2>();
			Rotate();
		}
	}

	private void OnDestroy()
	{
		base.transform.DOKill();
	}

	private void Rotate()
	{
		mouseRotationInertia = Vector2.Lerp(mouseRotationInertia, mouseRotationDelta, Time.deltaTime * 10f);
		base.transform.rotation *= Quaternion.AngleAxis(0f - mouseRotationInertia.x, Vector3.forward);
		if (MonoBehaviourSingleton<InputManager>.Instance.ClickAction.IsPressed())
		{
			mouseRotationDelta = mousePosition - lastMousePosition;
		}
		else
		{
			mouseRotationDelta = Vector2.zero;
		}
	}

	public void Hide()
	{
		base.gameObject.SetActive(value: false);
	}

	public void Show()
	{
		base.gameObject.SetActive(value: true);
	}

	private void OnTeamChanged()
	{
		UpdateStickMesh();
	}

	public void UpdateStickMesh()
	{
		StickMesh.SetSkin(Team, MonoBehaviourSingleton<SettingsManager>.Instance.GetStickSkin(Team, Role));
		StickMesh.SetShaftTape(MonoBehaviourSingleton<SettingsManager>.Instance.GetStickShaftSkin(Team, Role));
		StickMesh.SetBladeTape(MonoBehaviourSingleton<SettingsManager>.Instance.GetStickBladeSkin(Team, Role));
	}

	public void Client_MoveStickToDefaultPosition()
	{
		base.transform.DOKill();
		base.transform.DOMove(initialPosition, 0.5f);
		base.transform.DORotate(initialRotation, 0.5f);
	}

	public void Client_MoveStickToAppearanceStickPosition()
	{
		base.transform.DOKill();
		base.transform.DOMove(appearanceStickPosition.position, 0.5f);
		base.transform.DORotate(appearanceStickPosition.eulerAngles, 0.5f);
	}
}
