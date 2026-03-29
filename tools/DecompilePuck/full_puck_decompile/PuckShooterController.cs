using UnityEngine;

public class PuckShooterController : MonoBehaviour
{
	private PuckShooter puckShooter;

	private void Awake()
	{
		puckShooter = GetComponent<PuckShooter>();
	}
}
