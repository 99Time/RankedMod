using System;
using System.Collections.Generic;

public class EventManager : MonoBehaviourSingleton<EventManager>
{
	private Dictionary<string, Action<Dictionary<string, object>>> events = new Dictionary<string, Action<Dictionary<string, object>>>();

	private List<Action<Dictionary<string, object>>> anyListeners = new List<Action<Dictionary<string, object>>>();

	public void AddAnyEventListener(Action<Dictionary<string, object>> listener)
	{
		anyListeners.Add(listener);
	}

	public void RemoveAnyEventListener(Action<Dictionary<string, object>> listener)
	{
		anyListeners.Remove(listener);
	}

	public void AddEventListener(string eventName, Action<Dictionary<string, object>> listener)
	{
		if (events.ContainsKey(eventName))
		{
			Dictionary<string, Action<Dictionary<string, object>>> dictionary = events;
			dictionary[eventName] = (Action<Dictionary<string, object>>)Delegate.Combine(dictionary[eventName], listener);
		}
		else
		{
			Action<Dictionary<string, object>> a = null;
			a = (Action<Dictionary<string, object>>)Delegate.Combine(a, listener);
			events.Add(eventName, a);
		}
	}

	public void RemoveEventListener(string eventName, Action<Dictionary<string, object>> listener)
	{
		if (events.ContainsKey(eventName))
		{
			Dictionary<string, Action<Dictionary<string, object>>> dictionary = events;
			dictionary[eventName] = (Action<Dictionary<string, object>>)Delegate.Remove(dictionary[eventName], listener);
		}
	}

	public void TriggerEvent(string eventName, Dictionary<string, object> message = null)
	{
		if (!events.ContainsKey(eventName))
		{
			return;
		}
		if (message == null)
		{
			message = new Dictionary<string, object> { { "eventName", eventName } };
		}
		else
		{
			message.Add("eventName", eventName);
		}
		events[eventName]?.Invoke(message);
		foreach (Action<Dictionary<string, object>> anyListener in anyListeners)
		{
			anyListener(message);
		}
	}
}
