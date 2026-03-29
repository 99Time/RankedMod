using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

public class ItemManager : MonoBehaviourSingleton<ItemManager>
{
	[Header("Settings")]
	[SerializeField]
	private SerializedDictionary<int, string> itemIdMap = new SerializedDictionary<int, string>();

	[HideInInspector]
	public int[] OwnedItemIds = new int[0];

	[HideInInspector]
	public List<string> OwnedItems = new List<string>();

	[HideInInspector]
	public List<string> PurchaseableItems = new List<string>();

	public override void Awake()
	{
		base.Awake();
		PurchaseableItems = new List<string>(itemIdMap.Values);
	}

	public void SetItems(int[] itemIds)
	{
		OwnedItemIds = itemIds;
		OwnedItems.Clear();
		foreach (int key in itemIds)
		{
			if (itemIdMap.ContainsKey(key))
			{
				OwnedItems.Add(itemIdMap[key]);
			}
		}
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnOwnedItemIdsUpdated", new Dictionary<string, object>
		{
			{ "ownedItemIds", OwnedItemIds },
			{ "ownedItems", OwnedItems },
			{ "purchaseableItems", PurchaseableItems }
		});
	}
}
