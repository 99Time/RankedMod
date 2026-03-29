using System.Collections.Generic;
using UnityEngine;

public class PostProcessingManagerController : MonoBehaviour
{
	private PostProcessingManager postProcessingManager;

	private void Awake()
	{
		postProcessingManager = GetComponent<PostProcessingManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnMotionBlurChanged", Event_Client_OnMotionBlurChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnQualityChanged", Event_Client_OnQualityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnShowPuckSilhouetteChanged", Event_Client_OnShowPuckSilhouetteChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnShowPuckOutlineChanged", Event_Client_OnShowPuckOutlineChanged);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnMotionBlurChanged", Event_Client_OnMotionBlurChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnQualityChanged", Event_Client_OnQualityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnShowPuckSilhouetteChanged", Event_Client_OnShowPuckSilhouetteChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnShowPuckOutlineChanged", Event_Client_OnShowPuckOutlineChanged);
	}

	private void Event_Client_OnMotionBlurChanged(Dictionary<string, object> message)
	{
		bool motionBlur = (bool)message["value"];
		postProcessingManager.SetMotionBlur(motionBlur);
	}

	private void Event_Client_OnQualityChanged(Dictionary<string, object> message)
	{
		switch ((string)message["value"])
		{
		case "LOW":
			postProcessingManager.SetMsaaSampleCount(1);
			break;
		case "MEDIUM":
			postProcessingManager.SetMsaaSampleCount(2);
			break;
		case "HIGH":
			postProcessingManager.SetMsaaSampleCount(4);
			break;
		case "ULTRA":
			postProcessingManager.SetMsaaSampleCount(8);
			break;
		}
	}

	private void Event_Client_OnShowPuckSilhouetteChanged(Dictionary<string, object> message)
	{
		bool obstructedPuck = (bool)message["value"];
		postProcessingManager.SetObstructedPuck(obstructedPuck);
	}

	private void Event_Client_OnShowPuckOutlineChanged(Dictionary<string, object> message)
	{
		bool puckOutline = (bool)message["value"];
		postProcessingManager.SetPuckOutline(puckOutline);
	}
}
