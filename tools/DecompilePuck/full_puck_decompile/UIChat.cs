using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AYellowpaper.SerializedCollections;
using DG.Tweening;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class UIChat : UIComponent<UIChat>
{
	[Header("Components")]
	[SerializeField]
	private VisualTreeAsset chatMessageAsset;

	[SerializeField]
	private VisualTreeAsset quickChatMessageAsset;

	[Header("Settings")]
	[SerializeField]
	private float messageRateLimit = 3f;

	[SerializeField]
	private float messageRatePeriod = 5f;

	[SerializeField]
	private int messageHistory = 50;

	[SerializeField]
	private Color teamBlueColor = Color.blue;

	[SerializeField]
	private Color teamRedColor = Color.red;

	[SerializeField]
	private Color teamSpectatorColor = Color.gray;

	[SerializeField]
	private Vector2 size = new Vector2(700f, 250f);

	[SerializeField]
	private float fontSize = 24f;

	[SerializeField]
	private SerializedDictionary<int, string[]> quickChatMessages = new SerializedDictionary<int, string[]>();

	[SerializeField]
	private float quickChatTimeout = 3f;

	[HideInInspector]
	public bool UseTeamChat;

	[HideInInspector]
	public bool IsQuickChatOpen;

	private ScrollView chatScrollView;

	private TextField chatTextField;

	private VisualElement quickChatContainer;

	private Dictionary<VisualElement, ChatMessage> chatMessages = new Dictionary<VisualElement, ChatMessage>();

	private Dictionary<Player, float> playerRateLimits = new Dictionary<Player, float>();

	private Tween scrollToBottomTween;

	private Tween quickChatTimeoutTween;

	private int quickChatIndex = -1;

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("ChatContainer");
		chatScrollView = container.Query<ScrollView>("ChatScrollView");
		chatTextField = container.Query<TextField>("ChatTextField");
		quickChatContainer = container.Query<VisualElement>("QuickChatContainer");
		chatTextField.RegisterCallback<NavigationSubmitEvent>(delegate
		{
			SubmitChatMessage();
		}, TrickleDown.TrickleDown);
		chatTextField.RegisterCallback<NavigationCancelEvent>(delegate
		{
			Blur();
		}, TrickleDown.TrickleDown);
		container.RegisterCallback(delegate(FocusOutEvent e)
		{
			if (!Utils.GetVisualElementChildrenRecursive(container).Contains(e.relatedTarget))
			{
				Blur();
			}
		}, TrickleDown.TrickleDown);
		chatScrollView.contentViewport.RegisterCallback<GeometryChangedEvent>(delegate
		{
			ScrollToBottom();
		});
		chatScrollView.contentContainer.RegisterCallback<GeometryChangedEvent>(delegate
		{
			if (!base.IsFocused)
			{
				ScrollToBottom();
			}
		});
	}

	private void Start()
	{
		base.FocusRequiresMouse = true;
	}

	private void Update()
	{
		foreach (Player item in playerRateLimits.Keys.ToList())
		{
			playerRateLimits[item] -= Time.deltaTime / messageRatePeriod;
			if (playerRateLimits[item] <= 0f)
			{
				playerRateLimits.Remove(item);
			}
		}
		if (Application.isBatchMode)
		{
			return;
		}
		foreach (ChatMessage item2 in chatMessages.Values.ToList())
		{
			item2.Update(Time.time);
		}
	}

	public override void Show()
	{
		if (MonoBehaviourSingleton<SettingsManager>.Instance.ShowGameUserInterface != 0)
		{
			base.Show();
		}
	}

	public void Focus()
	{
		base.IsFocused = true;
		ShowChatInput();
		ShowChatMessages();
	}

	public void Blur()
	{
		base.IsFocused = false;
		HideChatInput();
		HideChatMessages();
	}

	private void ShowChatInput()
	{
		chatTextField.style.display = DisplayStyle.Flex;
		chatTextField.value = "";
		chatTextField.Focus();
	}

	private void HideChatInput()
	{
		chatTextField.style.display = DisplayStyle.None;
		chatTextField.value = "";
	}

	private void SubmitChatMessage()
	{
		Client_SendClientChatMessage(chatTextField.value, UseTeamChat);
		chatTextField.value = "";
		chatTextField.Blur();
		HideChatInput();
	}

	private void ShowChatMessages()
	{
		foreach (ChatMessage value in chatMessages.Values)
		{
			value.Show(0f, autoHide: false);
		}
	}

	private void HideChatMessages()
	{
		foreach (ChatMessage value in chatMessages.Values)
		{
			value.Hide();
		}
	}

	private void ScrollToBottom()
	{
		float to = Mathf.Max(chatScrollView.contentContainer.resolvedStyle.height - chatScrollView.contentViewport.resolvedStyle.height, 0f);
		scrollToBottomTween?.Kill();
		scrollToBottomTween = DOVirtual.Float(chatScrollView.scrollOffset.y, to, 0.25f, delegate(float value)
		{
			chatScrollView.scrollOffset = new Vector2(0f, value);
		});
	}

	public void AddChatMessage(string message)
	{
		if (!Application.isBatchMode)
		{
			if (chatScrollView.childCount >= messageHistory)
			{
				chatScrollView.RemoveAt(0);
			}
			TemplateContainer templateContainer = Utils.InstantiateVisualTreeAsset(chatMessageAsset);
			Label label = templateContainer.Query<Label>("ChatMessageLabel");
			chatScrollView.Add(label);
			ChatMessage value = new ChatMessage(label, Time.time, message);
			chatMessages.Add(templateContainer, value);
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnUserInterfaceNotification");
		}
	}

	public void ClearChatMessages()
	{
		foreach (ChatMessage value in chatMessages.Values)
		{
			value.Dispose();
		}
		playerRateLimits.Clear();
		chatMessages.Clear();
		chatScrollView.Clear();
		quickChatTimeoutTween?.Kill();
		CloseQuickChat();
	}

	public void SetOpacity(float opacity)
	{
		if (container != null)
		{
			container.style.opacity = new StyleFloat(opacity);
		}
	}

	public void SetScale(float scale)
	{
		if (container != null && chatScrollView != null)
		{
			container.style.fontSize = new Length(fontSize * scale, LengthUnit.Pixel);
			container.style.width = new Length(size.x * scale, LengthUnit.Pixel);
			chatScrollView.style.height = new Length(size.y * scale, LengthUnit.Pixel);
		}
	}

	private string ParseClientChatMessage(string message)
	{
		string newValue = ((!(MonoBehaviourSingleton<SettingsManager>.Instance.Units == "METRIC")) ? "MPH" : "KPH");
		foreach (Match item in new Regex("(?<=" + Regex.Escape("<united>") + ")[^>]+(?=" + Regex.Escape("</united>") + ")").Matches(message))
		{
			if (float.TryParse(item.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				float num = ((MonoBehaviourSingleton<SettingsManager>.Instance.Units == "METRIC") ? Utils.GameUnitsToMetric(result) : Utils.GameUnitsToImperial(result));
				message = message.Replace("<united>" + item.Value + "</united>", num.ToString("F1"));
			}
		}
		message = message.Replace("&units", newValue);
		return message;
	}

	public string WrapInTeamColor(PlayerTeam team, string message)
	{
		string text = team switch
		{
			PlayerTeam.Blue => "#" + ColorUtility.ToHtmlStringRGB(teamBlueColor), 
			PlayerTeam.Red => "#" + ColorUtility.ToHtmlStringRGB(teamRedColor), 
			_ => "#" + ColorUtility.ToHtmlStringRGB(teamSpectatorColor), 
		};
		return "<b><color=" + text + "><noparse>" + message + "</noparse></color></b>";
	}

	public string WrapPlayerUsername(Player player)
	{
		if (!player)
		{
			return "";
		}
		return WrapInTeamColor(player.Team.Value, $"#{player.Number.Value} {player.Username.Value}");
	}

	public void Server_SendSystemChatMessage(string message)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Server_ChatMessageRpc(message, base.RpcTarget.ClientsAndHost);
		}
	}

	public void Server_SendSystemChatMessage(string message, ulong clientId)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Server_ChatMessageRpc(message, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
		}
	}

	[Rpc(SendTo.SpecifiedInParams)]
	public void Server_ChatMessageRpc(string message, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
			FastBufferWriter bufferWriter = __beginSendRpc(3914580275u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			bool value = message != null;
			bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			if (value)
			{
				bufferWriter.WriteValueSafe(message);
			}
			__endSendRpc(ref bufferWriter, 3914580275u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			message = ParseClientChatMessage(message);
			if (MonoBehaviourSingleton<SettingsManager>.Instance.FilterChatProfanity > 0)
			{
				message = Utils.FilterStringProfanity(message, replaceWithStars: true);
			}
			AddChatMessage(message);
		}
	}

	private void Server_ProcessPlayerChatMessage(Player player, string message, ulong clientId, bool useTeamChat, bool isMuted)
	{
		if (!NetworkManager.Singleton.IsServer || !player)
		{
			return;
		}
		string text = (useTeamChat ? "[TEAM]" : "[ALL]");
		Debug.Log($"[UIChat] {text} {player.Username.Value} ({player.OwnerClientId}) [{player.SteamId.Value}]: {message}");
		if (message[0] == '/')
		{
			string[] source = message.Split(" ");
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnChatCommand", new Dictionary<string, object>
			{
				{ "clientId", clientId },
				{
					"command",
					source.First()
				},
				{
					"args",
					source.Skip(1).ToArray()
				}
			});
		}
		else
		{
			if (isMuted)
			{
				return;
			}
			if (!playerRateLimits.ContainsKey(player))
			{
				playerRateLimits.Add(player, 0f);
			}
			if (playerRateLimits[player] + 1f >= messageRateLimit)
			{
				Debug.LogWarning($"[UIChat] {player.Username.Value} ({player.OwnerClientId}) [{player.SteamId.Value}] is rate limited. Ignoring message: {message}");
				return;
			}
			playerRateLimits[player] += 1f;
			if (useTeamChat)
			{
				List<Player> list = new List<Player>();
				if (player.Team.Value == PlayerTeam.None || player.Team.Value == PlayerTeam.Spectator)
				{
					list.AddRange(NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayersByTeam(PlayerTeam.None));
					list.AddRange(NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayersByTeam(PlayerTeam.Spectator));
				}
				else
				{
					list.AddRange(NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayersByTeam(player.Team.Value));
				}
				ulong[] clientIds = list.Select((Player p) => p.OwnerClientId).ToArray();
				Server_ChatMessageRpc(text + " " + WrapPlayerUsername(player) + ": <noparse>" + message + "</noparse>", base.RpcTarget.Group(clientIds, RpcTargetUse.Temp));
			}
			else
			{
				Server_ChatMessageRpc(WrapPlayerUsername(player) + ": <noparse>" + message + "</noparse>", base.RpcTarget.ClientsAndHost);
			}
		}
	}

	public void Client_SendClientChatMessage(string message, bool useTeamChat)
	{
		message = Utils.FilterStringSpecialCharacters(message);
		if (message.Length != 0)
		{
			Client_ChatMessageRpc(message, useTeamChat, MonoBehaviourSingleton<StateManager>.Instance.IsMuted);
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_ChatMessageRpc(string message, bool useTeamChat, bool isMuted, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcParams rpcParams2 = rpcParams;
			RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
			FastBufferWriter bufferWriter = __beginSendRpc(4195669213u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bool value = message != null;
			bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			if (value)
			{
				bufferWriter.WriteValueSafe(message);
			}
			bufferWriter.WriteValueSafe(in useTeamChat, default(FastBufferWriter.ForPrimitives));
			bufferWriter.WriteValueSafe(in isMuted, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 4195669213u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			Player component = NetworkManager.Singleton.ConnectedClients[senderClientId].PlayerObject.GetComponent<Player>();
			Server_ProcessPlayerChatMessage(component, message, senderClientId, useTeamChat, isMuted);
		}
	}

	public void OnQuickChat(int index)
	{
		if (IsQuickChatOpen)
		{
			if (quickChatIndex >= 0 && quickChatIndex < quickChatMessages.Count && index >= 0 && index < quickChatMessages[quickChatIndex].Length)
			{
				string message = quickChatMessages[quickChatIndex][index];
				Client_SendClientChatMessage(message, useTeamChat: false);
				CloseQuickChat();
			}
		}
		else
		{
			OpenQuickChat(index);
		}
	}

	public void OpenQuickChat(int index)
	{
		if (!IsQuickChatOpen && index >= 0 && index < quickChatMessages.Count && quickChatMessages[index].Length != 0)
		{
			quickChatIndex = index;
			quickChatContainer.style.display = DisplayStyle.Flex;
			quickChatContainer.Clear();
			int num = 1;
			string[] array = quickChatMessages[index];
			foreach (string arg in array)
			{
				Label label = Utils.InstantiateVisualTreeAsset(chatMessageAsset).Query<Label>("ChatMessageLabel");
				label.text = $"<b>{num}</b> <indent=5%>{arg}";
				quickChatContainer.Add(label);
				num++;
			}
			IsQuickChatOpen = true;
			quickChatTimeoutTween?.Kill();
			quickChatTimeoutTween = DOVirtual.DelayedCall(quickChatTimeout, delegate
			{
				CloseQuickChat();
			});
		}
	}

	public void CloseQuickChat()
	{
		if (IsQuickChatOpen)
		{
			quickChatContainer.style.display = DisplayStyle.None;
			quickChatIndex = -1;
			quickChatContainer.Clear();
			IsQuickChatOpen = false;
		}
	}

	protected override void __initializeVariables()
	{
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		__registerRpc(3914580275u, __rpc_handler_3914580275, "Server_ChatMessageRpc");
		__registerRpc(4195669213u, __rpc_handler_4195669213, "Client_ChatMessageRpc");
		base.__initializeRpcs();
	}

	private static void __rpc_handler_3914580275(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			string s = null;
			if (value)
			{
				reader.ReadValueSafe(out s, oneByteChars: false);
			}
			RpcParams ext = rpcParams.Ext;
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((UIChat)target).Server_ChatMessageRpc(s, ext);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_4195669213(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			string s = null;
			if (value)
			{
				reader.ReadValueSafe(out s, oneByteChars: false);
			}
			reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
			reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
			RpcParams ext = rpcParams.Ext;
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((UIChat)target).Client_ChatMessageRpc(s, value2, value3, ext);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	protected internal override string __getTypeName()
	{
		return "UIChat";
	}
}
