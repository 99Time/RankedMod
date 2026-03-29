using UnityEngine;

public class PhysicsManagerController : MonoBehaviour
{
	private PhysicsManager physicsManager;

	private void Awake()
	{
		physicsManager = GetComponent<PhysicsManager>();
	}

	private void Start()
	{
	}

	private void OnDestroy()
	{
	}
}
