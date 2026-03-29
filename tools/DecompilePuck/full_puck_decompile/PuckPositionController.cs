using UnityEngine;

public class PuckPositionController : MonoBehaviour
{
	private PuckPosition puckPosition;

	private void Awake()
	{
		puckPosition = GetComponent<PuckPosition>();
	}
}
