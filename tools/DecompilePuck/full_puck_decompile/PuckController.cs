using UnityEngine;

public class PuckController : MonoBehaviour
{
	private Puck puck;

	private void Awake()
	{
		puck = GetComponent<Puck>();
	}

	private void Start()
	{
	}

	private void OnDestroy()
	{
	}
}
