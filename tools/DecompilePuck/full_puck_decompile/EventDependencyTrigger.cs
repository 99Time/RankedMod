using System.Collections.Generic;
using System.Linq;
using AYellowpaper.SerializedCollections;
using UnityEngine;

public class EventDependencyTrigger : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private SerializedDictionary<string, bool> dependencyEvents = new SerializedDictionary<string, bool>();

	[SerializeField]
	private string triggerEventName;

	[SerializeField]
	private float timeout = 3f;

	[SerializeField]
	private bool isRepeating = true;

	private bool isStarted;

	private float startTime;

	private void Start()
	{
		foreach (KeyValuePair<string, bool> item in dependencyEvents.ToList())
		{
			string key = item.Key;
			MonoBehaviourSingleton<EventManager>.Instance.AddEventListener(key, OnDependencyEvent);
		}
	}

	private void OnDestroy()
	{
		foreach (KeyValuePair<string, bool> item in dependencyEvents.ToList())
		{
			string key = item.Key;
			MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener(key, OnDependencyEvent);
		}
	}

	private void Update()
	{
		if (!isStarted || !(Time.time - startTime > timeout))
		{
			return;
		}
		Debug.LogWarning("EventDependencyTrigger on " + base.gameObject.name + ": Timeout");
		foreach (KeyValuePair<string, bool> item in dependencyEvents.ToList())
		{
			string key = item.Key;
			bool value = item.Value;
			Debug.Log($"Dependency event {key} is met: {value}");
		}
	}

	private void OnDependencyEvent(Dictionary<string, object> message)
	{
		string key = (string)message["eventName"];
		if (!dependencyEvents.ContainsValue(value: true))
		{
			isStarted = true;
			startTime = Time.time;
		}
		if (dependencyEvents.ContainsKey(key))
		{
			dependencyEvents[key] = true;
		}
		if (dependencyEvents.ContainsValue(value: false) || !isStarted)
		{
			return;
		}
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent(triggerEventName);
		isStarted = false;
		if (!isRepeating)
		{
			return;
		}
		foreach (KeyValuePair<string, bool> item in dependencyEvents.ToList())
		{
			string key2 = item.Key;
			dependencyEvents[key2] = false;
		}
	}
}
