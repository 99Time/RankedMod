using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkBehaviourSingleton<T> : NetworkBehaviour where T : NetworkBehaviour
{
	private static T instance;

	public static T Instance => instance;

	public virtual void Awake()
	{
		if (instance != null && instance != this)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		instance = Object.FindFirstObjectByType<T>();
		Object.DontDestroyOnLoad(base.gameObject);
	}

	public void DestroyOnLoad()
	{
		UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(base.gameObject, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
	}

	protected override void __initializeVariables()
	{
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		base.__initializeRpcs();
	}

	protected internal override string __getTypeName()
	{
		return "NetworkBehaviourSingleton`1";
	}
}
