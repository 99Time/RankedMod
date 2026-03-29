using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;

public class Spectator : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private PlayerMesh playerMesh;

	[SerializeField]
	private Animator animator;

	[Header("Settings")]
	[SerializeField]
	private float animationUpdateRate = 30f;

	[SerializeField]
	private float lookAtUpdateRate = 15f;

	[HideInInspector]
	public Transform LookTarget;

	private float animationUpdateAccumulator;

	private float lookAtUpdateAccumulator;

	private IEnumerator animationCoroutine;

	[HideInInspector]
	private float AnimationUpdateInterval => 1f / animationUpdateRate;

	[HideInInspector]
	private float LookAtUpdateInterval => 1f / lookAtUpdateRate;

	private void Awake()
	{
		animationUpdateAccumulator = Random.Range(0f, AnimationUpdateInterval);
		lookAtUpdateAccumulator = Random.Range(0f, LookAtUpdateInterval);
	}

	private void Start()
	{
		Randomize();
		animator.playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
		PlayAnimation("Seated");
	}

	private void OnDestroy()
	{
		if (animationCoroutine != null)
		{
			StopCoroutine(animationCoroutine);
		}
	}

	private void Update()
	{
		if (!playerMesh)
		{
			return;
		}
		animationUpdateAccumulator += Time.deltaTime;
		if (animationUpdateAccumulator >= AnimationUpdateInterval)
		{
			animator.Update(animationUpdateAccumulator);
			while (animationUpdateAccumulator >= AnimationUpdateInterval)
			{
				animationUpdateAccumulator -= AnimationUpdateInterval;
			}
		}
		lookAtUpdateAccumulator += Time.deltaTime;
		if (lookAtUpdateAccumulator >= LookAtUpdateInterval)
		{
			if ((bool)LookTarget)
			{
				playerMesh.LookAt(LookTarget.position, lookAtUpdateAccumulator);
			}
			else
			{
				playerMesh.LookAt(new Vector3(0f, 0f, 0f), lookAtUpdateAccumulator);
			}
			while (lookAtUpdateAccumulator >= LookAtUpdateInterval)
			{
				lookAtUpdateAccumulator -= LookAtUpdateInterval;
			}
		}
	}

	private IEnumerator IAnimate()
	{
		PlayAnimation("Seated", Random.Range(0f, 0.25f));
		yield return new WaitForSeconds(3f);
		if (Random.Range(0, 3) == 0)
		{
			PlayAnimation("Standing", Random.Range(0f, 0.25f));
		}
		else
		{
			PlayAnimation("Cheering", Random.Range(0f, 0.25f));
		}
		yield return new WaitForSeconds(3f);
		StartCoroutine(IAnimate());
	}

	public void PlayAnimation(string animationName, float delay = 0f)
	{
		if (animationCoroutine != null)
		{
			StopCoroutine(animationCoroutine);
		}
		animationCoroutine = IPlayAnimation(animationName, delay);
		StartCoroutine(animationCoroutine);
	}

	private IEnumerator IPlayAnimation(string animationName, float delay)
	{
		yield return new WaitForSeconds(delay);
		StopAnimations();
		animator.SetBool(animationName, value: true);
	}

	public void StopAnimations()
	{
		animator.SetBool("Seated", value: false);
		animator.SetBool("Cheering", value: false);
		animator.SetBool("Standing", value: false);
	}

	private void Randomize()
	{
		playerMesh.IsUsernameActive = false;
		playerMesh.IsNumberActive = false;
		playerMesh.IsLegPadsActive = false;
		string[] array = playerMesh.PlayerGroin.TextureNames.ToList().FindAll((string text2) => text2.Contains("spectator")).ToArray();
		string texture = array[Random.Range(0, array.Length)];
		string texture2 = playerMesh.PlayerTorso.TextureNames[Random.Range(0, playerMesh.PlayerTorso.TextureNames.Length)];
		playerMesh.PlayerGroin.SetTexture(texture);
		playerMesh.PlayerTorso.SetTexture(texture2);
		string text = playerMesh.PlayerHead.HairTypes[Random.Range(0, playerMesh.PlayerHead.HairTypes.Length)];
		if (text != "hair_pony_tail")
		{
			string mustache = playerMesh.PlayerHead.MustacheTypes[Random.Range(0, playerMesh.PlayerHead.MustacheTypes.Length)];
			string beard = playerMesh.PlayerHead.BeardTypes[Random.Range(0, playerMesh.PlayerHead.BeardTypes.Length)];
			playerMesh.PlayerHead.SetBeard(beard);
			playerMesh.PlayerHead.SetMustache(mustache);
		}
		playerMesh.PlayerHead.HeadType = PlayerHeadType.Spectator;
		playerMesh.PlayerHead.SetHair(text);
	}
}
