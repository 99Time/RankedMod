using System.Collections.Generic;
using System.Linq;
using Steamworks;
using UnityEngine;

public class SteamWorkshopManager : MonoBehaviourSingleton<SteamWorkshopManager>
{
	public List<InstalledItem> InstalledItems = new List<InstalledItem>();

	private Callback<DownloadItemResult_t> DownloadItemResult;

	private Callback<UserSubscribedItemsListChanged_t> UserSubscribedItemsListChanged;

	private Callback<RemoteStorageSubscribePublishedFileResult_t> RemoteStorageSubscribePublishedFileResult;

	private Callback<RemoteStorageUnsubscribePublishedFileResult_t> RemoteStorageUnsubscribePublishedFileResult;

	private Callback<DeleteItemResult_t> DeleteItemResult;

	private Dictionary<UGCQueryHandle_t, CallResult<SteamUGCQueryCompleted_t>> UGCQueryCompletedCallResultMap = new Dictionary<UGCQueryHandle_t, CallResult<SteamUGCQueryCompleted_t>>();

	private void Start()
	{
		RegisterCallbacks();
	}

	private void RegisterCallbacks()
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			if (Application.isBatchMode)
			{
				DownloadItemResult = Callback<DownloadItemResult_t>.CreateGameServer(OnDownloadItemResult);
				UserSubscribedItemsListChanged = Callback<UserSubscribedItemsListChanged_t>.CreateGameServer(OnUserSubscribedItemsListChanged);
				RemoteStorageSubscribePublishedFileResult = Callback<RemoteStorageSubscribePublishedFileResult_t>.CreateGameServer(OnRemoteStorageSubscribePublishedFileResult);
				RemoteStorageUnsubscribePublishedFileResult = Callback<RemoteStorageUnsubscribePublishedFileResult_t>.CreateGameServer(OnRemoteStorageUnsubscribePublishedFileResult);
				DeleteItemResult = Callback<DeleteItemResult_t>.CreateGameServer(OnDeleteItemResult);
			}
			else
			{
				DownloadItemResult = Callback<DownloadItemResult_t>.Create(OnDownloadItemResult);
				UserSubscribedItemsListChanged = Callback<UserSubscribedItemsListChanged_t>.Create(OnUserSubscribedItemsListChanged);
				RemoteStorageSubscribePublishedFileResult = Callback<RemoteStorageSubscribePublishedFileResult_t>.Create(OnRemoteStorageSubscribePublishedFileResult);
				RemoteStorageUnsubscribePublishedFileResult = Callback<RemoteStorageUnsubscribePublishedFileResult_t>.Create(OnRemoteStorageUnsubscribePublishedFileResult);
				DeleteItemResult = Callback<DeleteItemResult_t>.Create(OnDeleteItemResult);
			}
		}
	}

	public void AddInstalledItem(ulong id)
	{
		if (GetInstalledItemById(id) == null && GetItemInstallInfo(id, out var path))
		{
			InstalledItem installedItem = new InstalledItem(id, path);
			InstalledItems.Add(installedItem);
			Debug.Log($"[SteamWorkshopManager] Installed item {id} added");
			GetItemDetails(new ulong[1] { id });
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnInstalledItemAdded", new Dictionary<string, object> { { "installedItem", installedItem } });
		}
	}

	public void RemoveInstalledItem(ulong id)
	{
		InstalledItem installedItemById = GetInstalledItemById(id);
		if (installedItemById != null)
		{
			InstalledItems.Remove(installedItemById);
			Debug.Log($"[SteamWorkshopManager] Installed item {id} removed");
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnInstalledItemRemoved", new Dictionary<string, object> { { "installedItem", installedItemById } });
		}
	}

	public InstalledItem GetInstalledItemById(ulong id)
	{
		return InstalledItems.Find((InstalledItem item) => item.Id == id);
	}

	public void VerifyItemIntegrity()
	{
		if (Application.isBatchMode)
		{
			return;
		}
		Debug.Log("[SteamWorkshopManager] Verifying item integrity");
		ulong[] subscribedItemIds = GetSubscribedItemIds();
		foreach (ulong num in subscribedItemIds)
		{
			if (IsItemInstalled(num))
			{
				if (IsItemNeedsUpdate(num))
				{
					DownloadItem(num);
				}
				else
				{
					AddInstalledItem(num);
				}
			}
			else
			{
				DownloadItem(num);
			}
		}
		subscribedItemIds = InstalledItems.Select((InstalledItem item) => item.Id).ToArray();
		foreach (ulong num2 in subscribedItemIds)
		{
			if (!IsItemSubscribed(num2))
			{
				RemoveInstalledItem(num2);
			}
		}
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnItemIntegrityVerified", new Dictionary<string, object> { { "installedItems", InstalledItems } });
	}

	public bool IsItemInstalled(ulong itemId)
	{
		if (!MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			return false;
		}
		return (GetItemState(itemId) & 4) != 0;
	}

	public bool IsItemSubscribed(ulong itemId)
	{
		if (!MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			return false;
		}
		return (GetItemState(itemId) & 1) != 0;
	}

	public bool IsItemNeedsUpdate(ulong itemId)
	{
		if (!MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			return false;
		}
		return (GetItemState(itemId) & 8) != 0;
	}

	public uint GetNumSubscribedItems()
	{
		if (!MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			return 0u;
		}
		if (Application.isBatchMode)
		{
			return SteamGameServerUGC.GetNumSubscribedItems();
		}
		return SteamUGC.GetNumSubscribedItems();
	}

	public ulong[] GetSubscribedItemIds()
	{
		if (!MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			return new ulong[0];
		}
		uint numSubscribedItems = GetNumSubscribedItems();
		PublishedFileId_t[] array = new PublishedFileId_t[numSubscribedItems];
		if (Application.isBatchMode)
		{
			SteamGameServerUGC.GetSubscribedItems(array, numSubscribedItems);
		}
		else
		{
			SteamUGC.GetSubscribedItems(array, numSubscribedItems);
		}
		return array.Select((PublishedFileId_t id) => id.m_PublishedFileId).ToArray();
	}

	public bool GetItemInstallInfo(ulong itemId, out string path)
	{
		path = null;
		if (!MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			return false;
		}
		ulong punSizeOnDisk;
		uint punTimeStamp;
		if (Application.isBatchMode)
		{
			return SteamGameServerUGC.GetItemInstallInfo(new PublishedFileId_t(itemId), out punSizeOnDisk, out path, 256u, out punTimeStamp);
		}
		ulong punSizeOnDisk2;
		uint punTimeStamp2;
		return SteamUGC.GetItemInstallInfo(new PublishedFileId_t(itemId), out punSizeOnDisk2, out path, 256u, out punTimeStamp2);
	}

	public uint GetItemState(ulong itemId)
	{
		if (!MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			return 0u;
		}
		if (Application.isBatchMode)
		{
			return SteamGameServerUGC.GetItemState(new PublishedFileId_t(itemId));
		}
		return SteamUGC.GetItemState(new PublishedFileId_t(itemId));
	}

	public void GetItemDetails(ulong[] itemIds)
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			UGCQueryHandle_t uGCQueryHandle_t = CreateQueryUGCDetailsRequest(itemIds);
			CallResult<SteamUGCQueryCompleted_t> callResult = CallResult<SteamUGCQueryCompleted_t>.Create(OnUGCQueryCompleted);
			UGCQueryCompletedCallResultMap.Add(uGCQueryHandle_t, callResult);
			SteamAPICall_t steamAPICall_t = SendQueryUGCRequest(uGCQueryHandle_t);
			if (!(steamAPICall_t == SteamAPICall_t.Invalid))
			{
				callResult.Set(steamAPICall_t);
			}
		}
	}

	public UGCQueryHandle_t CreateQueryUGCDetailsRequest(ulong[] itemIds)
	{
		if (!MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			return UGCQueryHandle_t.Invalid;
		}
		PublishedFileId_t[] array = new PublishedFileId_t[itemIds.Length];
		for (int i = 0; i < itemIds.Length; i++)
		{
			array[i] = new PublishedFileId_t
			{
				m_PublishedFileId = itemIds[i]
			};
		}
		if (Application.isBatchMode)
		{
			return SteamGameServerUGC.CreateQueryUGCDetailsRequest(array, (uint)array.Length);
		}
		return SteamUGC.CreateQueryUGCDetailsRequest(array, (uint)array.Length);
	}

	public SteamAPICall_t SendQueryUGCRequest(UGCQueryHandle_t queryHandle)
	{
		if (!MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			return SteamAPICall_t.Invalid;
		}
		if (Application.isBatchMode)
		{
			return SteamGameServerUGC.SendQueryUGCRequest(queryHandle);
		}
		return SteamUGC.SendQueryUGCRequest(queryHandle);
	}

	private bool GetQueryUGCResult(UGCQueryHandle_t queryHandle, uint index, out SteamUGCDetails_t details)
	{
		details = default(SteamUGCDetails_t);
		if (!MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			return false;
		}
		if (Application.isBatchMode)
		{
			return SteamGameServerUGC.GetQueryUGCResult(queryHandle, index, out details);
		}
		return SteamUGC.GetQueryUGCResult(queryHandle, index, out details);
	}

	private bool GetQueryUGCPreviewURL(UGCQueryHandle_t queryHandle, uint index, out string previewUrl)
	{
		previewUrl = null;
		if (!MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			return false;
		}
		if (Application.isBatchMode)
		{
			return SteamGameServerUGC.GetQueryUGCPreviewURL(queryHandle, index, out previewUrl, 256u);
		}
		return SteamUGC.GetQueryUGCPreviewURL(queryHandle, index, out previewUrl, 256u);
	}

	public void DownloadItem(ulong itemId)
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			Debug.Log($"[SteamWorkshopManager] Downloading item {itemId}");
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnItemDownloadStarted", new Dictionary<string, object> { { "itemId", itemId } });
			if (!((!Application.isBatchMode) ? SteamUGC.DownloadItem(new PublishedFileId_t(itemId), bHighPriority: true) : SteamGameServerUGC.DownloadItem(new PublishedFileId_t(itemId), bHighPriority: true)))
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnItemDownloadFailed", new Dictionary<string, object> { { "itemId", itemId } });
			}
		}
	}

	public void DeleteItem(ulong itemId)
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			Debug.Log($"[SteamWorkshopManager] Deleting item {itemId}");
			if (Application.isBatchMode)
			{
				SteamGameServerUGC.DeleteItem(new PublishedFileId_t(itemId));
			}
			else
			{
				SteamUGC.DeleteItem(new PublishedFileId_t(itemId));
			}
		}
	}

	public void SubscribeItem(ulong itemId)
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			Debug.Log($"[SteamWorkshopManager] Subscribing item {itemId}");
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnItemSubscribeStarted", new Dictionary<string, object> { { "itemId", itemId } });
			if (Application.isBatchMode)
			{
				SteamGameServerUGC.SubscribeItem(new PublishedFileId_t(itemId));
			}
			else
			{
				SteamUGC.SubscribeItem(new PublishedFileId_t(itemId));
			}
		}
	}

	public void UnsubscribeItem(ulong itemId)
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			Debug.Log($"[SteamWorkshopManager] Unsubscribing item {itemId}");
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnItemUnsubscribeStarted", new Dictionary<string, object> { { "itemId", itemId } });
			if (Application.isBatchMode)
			{
				SteamGameServerUGC.UnsubscribeItem(new PublishedFileId_t(itemId));
			}
			else
			{
				SteamUGC.UnsubscribeItem(new PublishedFileId_t(itemId));
			}
		}
	}

	private void OnDownloadItemResult(DownloadItemResult_t response)
	{
		if (!(response.m_unAppID != new AppId_t(2994020u)))
		{
			if (response.m_eResult == EResult.k_EResultOK)
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnItemDownloadSucceeded", new Dictionary<string, object> { 
				{
					"itemId",
					(ulong)response.m_nPublishedFileId
				} });
				AddInstalledItem((ulong)response.m_nPublishedFileId);
			}
			else
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnItemDownloadFailed", new Dictionary<string, object> { 
				{
					"itemId",
					(ulong)response.m_nPublishedFileId
				} });
			}
		}
	}

	private void OnUserSubscribedItemsListChanged(UserSubscribedItemsListChanged_t response)
	{
		if (!(response.m_nAppID != new AppId_t(2994020u)))
		{
			VerifyItemIntegrity();
		}
	}

	private void OnRemoteStorageSubscribePublishedFileResult(RemoteStorageSubscribePublishedFileResult_t response)
	{
		if (response.m_eResult == EResult.k_EResultOK)
		{
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnItemSubscribeSucceeded", new Dictionary<string, object> { 
			{
				"itemId",
				(ulong)response.m_nPublishedFileId
			} });
		}
		else
		{
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnItemSubscribeFailed", new Dictionary<string, object> { 
			{
				"itemId",
				(ulong)response.m_nPublishedFileId
			} });
		}
	}

	private void OnRemoteStorageUnsubscribePublishedFileResult(RemoteStorageUnsubscribePublishedFileResult_t response)
	{
		if (response.m_eResult == EResult.k_EResultOK)
		{
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnItemUnsubscribeSucceeded", new Dictionary<string, object> { 
			{
				"itemId",
				(ulong)response.m_nPublishedFileId
			} });
			if (IsItemInstalled((ulong)response.m_nPublishedFileId))
			{
				DeleteItem((ulong)response.m_nPublishedFileId);
			}
		}
		else
		{
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnItemUnsubscribeFailed", new Dictionary<string, object> { 
			{
				"itemId",
				(ulong)response.m_nPublishedFileId
			} });
		}
	}

	private void OnDeleteItemResult(DeleteItemResult_t response)
	{
		if (response.m_eResult == EResult.k_EResultOK)
		{
			RemoveInstalledItem((ulong)response.m_nPublishedFileId);
		}
	}

	private void OnUGCQueryCompleted(SteamUGCQueryCompleted_t response, bool bIOFailure)
	{
		if (!MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized || response.m_eResult != EResult.k_EResultOK)
		{
			return;
		}
		Debug.Log($"[SteamWorkshopManager] OnUGCQueryCompleted: {response.m_unNumResultsReturned}");
		for (uint num = 0u; num < response.m_unNumResultsReturned; num++)
		{
			if (GetQueryUGCResult(response.m_handle, num, out var details))
			{
				PublishedFileId_t nPublishedFileId = details.m_nPublishedFileId;
				string rgchTitle = details.m_rgchTitle;
				string rgchDescription = details.m_rgchDescription;
				GetQueryUGCPreviewURL(response.m_handle, num, out var previewUrl);
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnItemDetails", new Dictionary<string, object>
				{
					{
						"id",
						(ulong)nPublishedFileId
					},
					{ "title", rgchTitle },
					{ "description", rgchDescription },
					{ "previewUrl", previewUrl }
				});
			}
		}
		if (UGCQueryCompletedCallResultMap.ContainsKey(response.m_handle))
		{
			UGCQueryCompletedCallResultMap[response.m_handle].Dispose();
			UGCQueryCompletedCallResultMap.Remove(response.m_handle);
		}
		if (Application.isBatchMode)
		{
			SteamGameServerUGC.ReleaseQueryUGCRequest(response.m_handle);
		}
		else
		{
			SteamUGC.ReleaseQueryUGCRequest(response.m_handle);
		}
	}
}
