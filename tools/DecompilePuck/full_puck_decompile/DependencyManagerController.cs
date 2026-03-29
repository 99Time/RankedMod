using UnityEngine;

public class DependencyManagerController : MonoBehaviour
{
	private DependencyManager dependencyManager;

	private void Awake()
	{
		dependencyManager = GetComponent<DependencyManager>();
	}

	private void Start()
	{
	}

	private void OnDestroy()
	{
	}
}
