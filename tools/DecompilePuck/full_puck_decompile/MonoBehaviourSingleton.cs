using UnityEngine;
using UnityEngine.SceneManagement;

public class MonoBehaviourSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
	private static T instance;

	public static T Instance
	{
		get
		{
			return instance;
		}
		set
		{
			instance = value;
		}
	}

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
}
