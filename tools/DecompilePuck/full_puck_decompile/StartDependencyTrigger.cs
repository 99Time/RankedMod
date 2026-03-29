using System.Collections.Generic;
using System.Linq;
using AYellowpaper.SerializedCollections;
using UnityEngine;

public class StartDependencyTrigger : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private SerializedDictionary<MonoBehaviour, bool> dependencies = new SerializedDictionary<MonoBehaviour, bool>();

	[SerializeField]
	private string eventName;

	[SerializeField]
	private float timeout = 3f;

	private float startTime;

	private bool isTriggered;

	private void Start()
	{
		startTime = Time.time;
	}

	private void Update()
	{
		if (isTriggered)
		{
			return;
		}
		if (Time.time - startTime > timeout)
		{
			Debug.LogWarning("StartDependencyTrigger: Timeout for event " + eventName);
			isTriggered = true;
			return;
		}
		foreach (KeyValuePair<MonoBehaviour, bool> item in dependencies.ToList())
		{
			MonoBehaviour key = item.Key;
			dependencies[key] = key.didStart;
		}
		if (!dependencies.ContainsValue(value: false))
		{
			isTriggered = true;
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent(eventName);
		}
	}
}
