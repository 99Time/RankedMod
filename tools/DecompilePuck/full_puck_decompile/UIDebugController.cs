using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class UIDebugController : NetworkBehaviour
{
	private UIDebug uiDebug;

	private void Awake()
	{
		uiDebug = GetComponent<UIDebug>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnDebugChanged", Event_Client_OnDebugChanged);
		uiDebug.SetBuildLabelText("B" + Application.version);
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnDebugChanged", Event_Client_OnDebugChanged);
		base.OnDestroy();
	}

	private void Event_Client_OnDebugChanged(Dictionary<string, object> message)
	{
		if ((int)message["value"] > 0)
		{
			uiDebug.Show();
		}
		else
		{
			uiDebug.Hide(ignoreAlwaysVisible: true);
		}
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
		return "UIDebugController";
	}
}
