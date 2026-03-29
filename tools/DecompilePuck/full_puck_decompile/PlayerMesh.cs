using TMPro;
using UnityEngine;

public class PlayerMesh : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private Transform groinBone;

	[SerializeField]
	private Transform torsoBone;

	[SerializeField]
	private Transform headBone;

	[SerializeField]
	public PlayerGroin PlayerGroin;

	[SerializeField]
	public PlayerTorso PlayerTorso;

	[SerializeField]
	public PlayerHead PlayerHead;

	[SerializeField]
	public PlayerLegPad PlayerLegPadLeft;

	[SerializeField]
	public PlayerLegPad PlayerLegPadRight;

	[SerializeField]
	private TMP_Text usernameText;

	[SerializeField]
	private TMP_Text numberText;

	[Header("Settings")]
	[SerializeField]
	private float lookAtSpeed = 10f;

	[HideInInspector]
	public float Stretch = 1f;

	private Vector3 initialGroinBonePosition;

	private Vector3 initialTorsoBonePosition;

	private Vector3 initialHeadBonePosition;

	public bool IsUsernameActive
	{
		get
		{
			return ((Component)(object)usernameText).gameObject.activeSelf;
		}
		set
		{
			((Component)(object)usernameText).gameObject.SetActive(value);
		}
	}

	public bool IsNumberActive
	{
		get
		{
			return ((Component)(object)numberText).gameObject.activeSelf;
		}
		set
		{
			((Component)(object)numberText).gameObject.SetActive(value);
		}
	}

	public bool IsLegPadsActive
	{
		get
		{
			if (PlayerLegPadLeft.gameObject.activeSelf)
			{
				return PlayerLegPadRight.gameObject.activeSelf;
			}
			return false;
		}
		set
		{
			PlayerLegPadLeft.gameObject.SetActive(value);
			PlayerLegPadRight.gameObject.SetActive(value);
		}
	}

	private void Awake()
	{
		initialGroinBonePosition = groinBone.localPosition;
		initialTorsoBonePosition = torsoBone.localPosition;
		initialHeadBonePosition = headBone.localPosition;
	}

	private void Update()
	{
		groinBone.localPosition = initialGroinBonePosition * Stretch;
		torsoBone.localPosition = initialTorsoBonePosition * Stretch;
		headBone.localPosition = initialHeadBonePosition * Stretch;
	}

	public void LookAt(Vector3 targetPosition, float deltaTime)
	{
		Quaternion b = Quaternion.LookRotation(targetPosition - torsoBone.transform.position);
		Quaternion b2 = Quaternion.Lerp(base.transform.rotation, b, 0.5f);
		Quaternion b3 = Quaternion.Lerp(base.transform.rotation, b, 1f);
		torsoBone.transform.rotation = Quaternion.Slerp(torsoBone.transform.rotation, b2, deltaTime * lookAtSpeed);
		headBone.transform.rotation = Quaternion.Slerp(headBone.transform.rotation, b3, deltaTime * lookAtSpeed);
	}

	public void SetUsername(string username)
	{
		usernameText.text = username;
	}

	public void SetNumber(string number)
	{
		numberText.text = number;
	}

	public void SetJersey(PlayerTeam team, string jersey)
	{
		string text = ((team == PlayerTeam.Blue) ? "blue_" : "red_");
		PlayerTorso.SetTexture(text + jersey);
		PlayerGroin.SetTexture(text + jersey);
	}

	public void SetRole(PlayerRole role)
	{
		IsLegPadsActive = role == PlayerRole.Goalie;
		PlayerHead.HeadType = ((role == PlayerRole.Goalie) ? PlayerHeadType.Goalie : PlayerHeadType.Attacker);
	}
}
