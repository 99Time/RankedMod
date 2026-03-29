using System.Collections.Generic;
using UnityEngine;

public class PuckElevationIndicatorController : MonoBehaviour
{
	private PuckElevationIndicator puckElevationIndicator;

	public void Awake()
	{
		puckElevationIndicator = GetComponent<PuckElevationIndicator>();
	}

	public void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnShowPuckElevationChanged", Event_Client_OnShowPuckElevationChanged);
		puckElevationIndicator.IsVisible = MonoBehaviourSingleton<SettingsManager>.Instance.ShowPuckElevation > 0;
	}

	public void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnShowPuckElevationChanged", Event_Client_OnShowPuckElevationChanged);
	}

	private void Event_Client_OnShowPuckElevationChanged(Dictionary<string, object> message)
	{
		bool isVisible = (bool)message["value"];
		puckElevationIndicator.IsVisible = isVisible;
	}
}
