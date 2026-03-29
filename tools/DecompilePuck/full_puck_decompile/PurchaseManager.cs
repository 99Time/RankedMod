using System.Collections.Generic;

public class PurchaseManager : MonoBehaviourSingleton<PurchaseManager>
{
	public void StartPurchase(int itemId)
	{
		MonoBehaviourSingleton<WebSocketManager>.Instance.Emit("playerStartPurchaseRequest", new Dictionary<string, object> { { "itemId", itemId } }, "playerStartPurchaseResponse");
	}

	public void CompletePurchase(ulong orderId)
	{
		MonoBehaviourSingleton<WebSocketManager>.Instance.Emit("playerCompletePurchaseRequest", new Dictionary<string, object> { { "orderId", orderId } }, "playerCompletePurchaseResponse");
	}

	public void CancelPurchase(ulong orderId)
	{
		MonoBehaviourSingleton<WebSocketManager>.Instance.Emit("playerCancelPurchaseRequest", new Dictionary<string, object> { { "orderId", orderId } });
	}
}
