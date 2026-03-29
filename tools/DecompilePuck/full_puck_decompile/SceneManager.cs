using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManager : NetworkBehaviourSingleton<SceneManager>
{
	private void Start()
	{
		UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
		UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
		if (!Application.isBatchMode)
		{
			LoadChangingRoomScene();
		}
	}

	public override void OnDestroy()
	{
		UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
		UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
		base.OnDestroy();
	}

	public void LoadChangingRoomScene()
	{
		UnityEngine.SceneManagement.SceneManager.LoadScene(1);
	}

	public void LoadLevel1Scene()
	{
		UnityEngine.SceneManagement.SceneManager.LoadScene(2);
	}

	private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnSceneLoaded", new Dictionary<string, object> { { "scene", scene } });
	}

	private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnSceneUnloaded", new Dictionary<string, object> { { "scene", scene } });
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
		return "SceneManager";
	}
}
