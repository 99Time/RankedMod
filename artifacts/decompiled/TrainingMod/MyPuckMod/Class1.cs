using UnityEngine;

namespace MyPuckMod;

public class Class1 : IPuckMod
{
	private static GameObject controllerObject;

	public bool OnEnable()
	{
		Debug.Log("Puck Attack Mod Enabled");
		if (controllerObject == null)
		{
			controllerObject = new GameObject("PuckAttackController");
			Object.DontDestroyOnLoad(controllerObject);
			controllerObject.AddComponent<PuckAttackBehaviour>();
		}
		return true;
	}

	public bool OnDisable()
	{
		if (controllerObject != null)
		{
			Object.Destroy(controllerObject);
			controllerObject = null;
		}
		Time.timeScale = 1f;
		Time.fixedDeltaTime = 0.02f;
		Debug.Log("Puck Attack Mod Disabled");
		return true;
	}
}
