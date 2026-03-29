using System.Collections.Generic;
using UnityEngine;

public class PurchaseManagerController : MonoBehaviour
{
	private PurchaseManager purchaseManager;

	private void Awake()
	{
		purchaseManager = GetComponent<PurchaseManager>();
	}

	private void Start()
	{
		if (!Application.isBatchMode)
		{
			MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearancePurchaseItem", Event_Client_OnAppearancePurchaseItem);
			MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnMicroTxnAuthorizationResponse", Event_Client_OnMicroTxnAuthorizationResponse);
		}
	}

	private void OnDestroy()
	{
		if (!Application.isBatchMode)
		{
			MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearancePurchaseItem", Event_Client_OnAppearancePurchaseItem);
			MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnMicroTxnAuthorizationResponse", Event_Client_OnMicroTxnAuthorizationResponse);
		}
	}

	private void Event_Client_OnAppearancePurchaseItem(Dictionary<string, object> message)
	{
		int itemId = (int)message["itemId"];
		purchaseManager.StartPurchase(itemId);
	}

	private void Event_Client_OnMicroTxnAuthorizationResponse(Dictionary<string, object> message)
	{
		bool num = (bool)message["authorized"];
		ulong orderId = (ulong)message["orderId"];
		if (num)
		{
			purchaseManager.CompletePurchase(orderId);
		}
		else
		{
			purchaseManager.CancelPurchase(orderId);
		}
	}
}
