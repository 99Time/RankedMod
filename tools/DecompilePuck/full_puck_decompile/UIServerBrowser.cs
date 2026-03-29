using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

public class UIServerBrowser : UIComponent<UIServerBrowser>
{
	[Header("Components")]
	public VisualTreeAsset serverButtonAsset;

	private VisualElement serverContainer;

	private Button nameHeaderButton;

	private Button playersHeaderButton;

	private Button pingHeaderButton;

	private TextField searchTextField;

	private IntegerField maximumPingIntegerField;

	private Toggle showFullToggle;

	private Toggle showEmptyToggle;

	private Button closeButton;

	private Button refreshButton;

	private Button serverLauncherButton;

	private SortType nameSortType;

	private SortType playersSortType;

	private SortType pingSortType;

	private string searchNeedle;

	private int maximumPing = 100;

	private bool showFull = true;

	private bool showEmpty = true;

	private List<ServerBrowserServer> servers = new List<ServerBrowserServer>();

	private List<TemplateContainer> serverButtons = new List<TemplateContainer>();

	private void Start()
	{
		base.VisibilityRequiresMouse = true;
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("ServerBrowserContainer");
		serverContainer = container.Query<VisualElement>("ServerContainer");
		nameHeaderButton = container.Query<Button>("NameHeaderButton");
		nameHeaderButton.clicked += OnClickNameHeader;
		playersHeaderButton = container.Query<Button>("PlayersHeaderButton");
		playersHeaderButton.clicked += OnClickPlayersHeader;
		pingHeaderButton = container.Query<Button>("PingHeaderButton");
		pingHeaderButton.clicked += OnClickPingHeader;
		searchTextField = container.Query<VisualElement>("SearchTextField").First().Query<TextField>("TextField");
		searchTextField.RegisterValueChangedCallback(OnSearchChanged);
		searchTextField.value = searchNeedle;
		maximumPingIntegerField = container.Query<VisualElement>("MaximumPingIntegerField").First().Query<IntegerField>("IntegerField");
		maximumPingIntegerField.RegisterValueChangedCallback(OnMaximumPingChanged);
		maximumPingIntegerField.value = maximumPing;
		showFullToggle = container.Query<VisualElement>("ShowFullToggle").First().Query<Toggle>("Toggle");
		showFullToggle.RegisterValueChangedCallback(OnShowFullChanged);
		showFullToggle.value = showFull;
		showEmptyToggle = container.Query<VisualElement>("ShowEmptyToggle").First().Query<Toggle>("Toggle");
		showEmptyToggle.RegisterValueChangedCallback(OnShowEmptyChanged);
		showEmptyToggle.value = showEmpty;
		closeButton = container.Query<Button>("CloseButton");
		closeButton.clicked += OnClickClose;
		refreshButton = container.Query<Button>("RefreshButton");
		refreshButton.clicked += OnClickRefresh;
		serverLauncherButton = container.Query<Button>("ServerLauncherButton");
		serverLauncherButton.clicked += OnClickServerLauncher;
		ClearServers();
		OnClickPlayersHeader();
	}

	public override void Show()
	{
		if (!base.IsVisible && servers.Count == 0)
		{
			Refresh();
		}
		base.Show();
	}

	public void Refresh()
	{
		DisableRefreshButton();
		MonoBehaviourSingleton<WebSocketManager>.Instance.Emit("playerGetServerBrowserServersRequest", null, "playerGetServerBrowserServersResponse");
	}

	private void ClearSortTypes()
	{
		nameSortType = SortType.None;
		playersSortType = SortType.None;
		pingSortType = SortType.None;
		nameHeaderButton.text = "NAME";
		playersHeaderButton.text = "PLAYERS";
		pingHeaderButton.text = "PING";
	}

	private void OnClickNameHeader()
	{
		if (nameSortType == SortType.None)
		{
			ClearSortTypes();
		}
		nameSortType = ((nameSortType == SortType.Down) ? SortType.Up : SortType.Down);
		nameHeaderButton.text = ((nameSortType == SortType.Up) ? "▲ NAME" : "▼ NAME");
		SortServers();
	}

	private void OnClickPlayersHeader()
	{
		if (playersSortType == SortType.None)
		{
			ClearSortTypes();
		}
		playersSortType = ((playersSortType == SortType.Down) ? SortType.Up : SortType.Down);
		playersHeaderButton.text = ((playersSortType == SortType.Up) ? "▲ PLAYERS" : "▼ PLAYERS");
		SortServers();
	}

	private void OnClickPingHeader()
	{
		if (pingSortType == SortType.None)
		{
			ClearSortTypes();
		}
		pingSortType = ((pingSortType == SortType.Down) ? SortType.Up : SortType.Down);
		pingHeaderButton.text = ((pingSortType == SortType.Up) ? "▲ PING" : "▼ PING");
		SortServers();
	}

	private void OnSearchChanged(ChangeEvent<string> changeEvent)
	{
		searchNeedle = changeEvent.newValue;
		SortServers();
	}

	private void OnMaximumPingChanged(ChangeEvent<int> changeEvent)
	{
		maximumPing = changeEvent.newValue;
		SortServers();
	}

	private void OnShowFullChanged(ChangeEvent<bool> changeEvent)
	{
		showFull = changeEvent.newValue;
		SortServers();
	}

	private void OnShowEmptyChanged(ChangeEvent<bool> changeEvent)
	{
		showEmpty = changeEvent.newValue;
		SortServers();
	}

	private void OnClickClose()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnServerBrowserClickClose");
	}

	private void OnClickRefresh()
	{
		Refresh();
	}

	private void OnClickServerLauncher()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnServerBrowserClickServerLauncher");
	}

	public void EnableRefreshButton()
	{
		refreshButton.enabledSelf = true;
	}

	public void DisableRefreshButton()
	{
		refreshButton.enabledSelf = false;
	}

	public void ClearServers()
	{
		servers.Clear();
		serverButtons.Clear();
		serverContainer.Clear();
	}

	public void UpdateServers(List<ServerBrowserServer> serverBrowserServers)
	{
		servers = serverBrowserServers;
		Dictionary<ServerBrowserServer, long> serverPingSentMap = new Dictionary<ServerBrowserServer, long>();
		ushort port = 8886;
		int retries = 0;
		int maxRetries = 3;
		UDPSocket udpSocket = new UDPSocket();
		UDPSocket uDPSocket = udpSocket;
		uDPSocket.OnSocketStarted = (Action<ushort>)Delegate.Combine(uDPSocket.OnSocketStarted, (Action<ushort>)async delegate(ushort num)
		{
			Debug.Log($"[UIServerBrowser] UDP socket started on port {num}");
			foreach (ServerBrowserServer server in servers)
			{
				udpSocket.Send(server.ipAddress, server.pingPort, "ping");
				await Task.Delay(10);
			}
			await Task.Delay(1000);
			udpSocket.StopSocket();
			Debug.Log($"[UIServerBrowser] Failed to receive a pong response from {serverPingSentMap.Count} servers");
			foreach (ServerBrowserServer item in serverPingSentMap.Keys.ToList())
			{
				AddServerButton(item, long.MaxValue);
			}
		});
		UDPSocket uDPSocket2 = udpSocket;
		uDPSocket2.OnSocketStopped = (Action)Delegate.Combine(uDPSocket2.OnSocketStopped, (Action)delegate
		{
			Debug.Log("UDP socket stopped");
			EnableRefreshButton();
		});
		UDPSocket uDPSocket3 = udpSocket;
		uDPSocket3.OnSocketFailed = (Action<ushort>)Delegate.Combine(uDPSocket3.OnSocketFailed, (Action<ushort>)delegate(ushort num)
		{
			Debug.Log($"[UIServerBrowser] UDP socket failed on port {num}");
			EnableRefreshButton();
			if (retries < maxRetries)
			{
				retries++;
				num++;
				udpSocket.StartSocket(num);
			}
		});
		UDPSocket uDPSocket4 = udpSocket;
		uDPSocket4.OnUdpMessageSent = (Action<string, ushort, string, long>)Delegate.Combine(uDPSocket4.OnUdpMessageSent, (Action<string, ushort, string, long>)delegate(string ipAddress, ushort num, string message, long timestamp)
		{
			ServerBrowserServer key = servers.Find((ServerBrowserServer server) => server.ipAddress == ipAddress && server.pingPort == num);
			if (!serverPingSentMap.ContainsKey(key))
			{
				serverPingSentMap.Add(key, timestamp);
			}
		});
		UDPSocket uDPSocket5 = udpSocket;
		uDPSocket5.OnUdpMessageReceived = (Action<string, ushort, string, long>)Delegate.Combine(uDPSocket5.OnUdpMessageReceived, (Action<string, ushort, string, long>)delegate(string ipAddress, ushort num2, string message, long timestamp)
		{
			ServerBrowserServer serverBrowserServer = serverPingSentMap.Keys.FirstOrDefault((ServerBrowserServer server) => server.ipAddress == ipAddress && server.pingPort == num2);
			if (serverBrowserServer != null)
			{
				long num = serverPingSentMap[serverBrowserServer];
				if (serverPingSentMap.ContainsKey(serverBrowserServer))
				{
					serverPingSentMap.Remove(serverBrowserServer);
				}
				AddServerButton(serverBrowserServer, timestamp - num);
			}
		});
		udpSocket.StartSocket(port);
	}

	private void AddServerButton(ServerBrowserServer server, long ping)
	{
		TemplateContainer templateContainer = Utils.InstantiateVisualTreeAsset(serverButtonAsset, Position.Relative);
		Dictionary<string, object> userData = new Dictionary<string, object>
		{
			{ "server", server },
			{ "ping", ping }
		};
		templateContainer.userData = userData;
		Button button = templateContainer.Query<Button>("ServerButton");
		VisualElement visualElement = button.Query<VisualElement>("PasswordProtectedVisualElement").First();
		Label label = button.Query<Label>("NameLabel");
		Label label2 = button.Query<Label>("PlayersLabel");
		Label label3 = button.Query<Label>("PingLabel");
		visualElement.visible = server.isPasswordProtected;
		label.text = server.name;
		label2.text = $"{server.players}/{server.maxPlayers}";
		label3.text = ((ping == long.MaxValue) ? "<color=red>?</color>" : $"{ping}ms");
		button.RegisterCallback<ClickEvent>(delegate
		{
			OnClickServer(server);
		});
		serverContainer.Add(templateContainer);
		serverButtons.Add(templateContainer);
		SortServers();
	}

	private void SortServers()
	{
		serverButtons.Sort(delegate(TemplateContainer a, TemplateContainer b)
		{
			Dictionary<string, object> obj2 = a.userData as Dictionary<string, object>;
			Dictionary<string, object> dictionary = b.userData as Dictionary<string, object>;
			ServerBrowserServer serverBrowserServer2 = obj2["server"] as ServerBrowserServer;
			ServerBrowserServer serverBrowserServer3 = dictionary["server"] as ServerBrowserServer;
			long value = (long)obj2["ping"];
			long value2 = (long)dictionary["ping"];
			if (nameSortType == SortType.Down)
			{
				return serverBrowserServer2.name.CompareTo(serverBrowserServer3.name);
			}
			if (nameSortType == SortType.Up)
			{
				return serverBrowserServer3.name.CompareTo(serverBrowserServer2.name);
			}
			if (playersSortType == SortType.Up)
			{
				int num2 = serverBrowserServer2.players.CompareTo(serverBrowserServer3.players);
				if (num2 != 0)
				{
					return num2;
				}
				return serverBrowserServer2.name.CompareTo(serverBrowserServer3.name);
			}
			if (playersSortType == SortType.Down)
			{
				int num3 = serverBrowserServer3.players.CompareTo(serverBrowserServer2.players);
				if (num3 != 0)
				{
					return num3;
				}
				return serverBrowserServer2.name.CompareTo(serverBrowserServer3.name);
			}
			if (pingSortType == SortType.Down)
			{
				int num4 = value.CompareTo(value2);
				if (num4 != 0)
				{
					return num4;
				}
				return serverBrowserServer2.name.CompareTo(serverBrowserServer3.name);
			}
			if (pingSortType == SortType.Up)
			{
				int num5 = value2.CompareTo(value);
				if (num5 != 0)
				{
					return num5;
				}
				return serverBrowserServer2.name.CompareTo(serverBrowserServer3.name);
			}
			return 0;
		});
		serverContainer.Clear();
		foreach (TemplateContainer serverButton in serverButtons)
		{
			Dictionary<string, object> obj = serverButton.userData as Dictionary<string, object>;
			ServerBrowserServer serverBrowserServer = obj["server"] as ServerBrowserServer;
			long num = (long)obj["ping"];
			if ((string.IsNullOrEmpty(searchNeedle) || serverBrowserServer.name.ToLower().Contains(searchNeedle.ToLower())) && (num <= maximumPing || maximumPing == 0) && (serverBrowserServer.players > 0 || showEmpty) && (serverBrowserServer.players < serverBrowserServer.maxPlayers || showFull))
			{
				serverButton.style.display = DisplayStyle.Flex;
			}
			else
			{
				serverButton.style.display = DisplayStyle.None;
			}
			serverContainer.Add(serverButton);
		}
	}

	private void OnClickServer(ServerBrowserServer serverBrowserServer)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnServerBrowserClickServer", new Dictionary<string, object> { { "serverBrowserServer", serverBrowserServer } });
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
		return "UIServerBrowser";
	}
}
