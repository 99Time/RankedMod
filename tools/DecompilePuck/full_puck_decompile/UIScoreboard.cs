using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class UIScoreboard : UIComponent<UIScoreboard>
{
	[Header("Components")]
	public VisualTreeAsset scoreboardPlayerAsset;

	[Header("Settings")]
	[SerializeField]
	private Color patreonColor = new Color(1f, 1f, 1f, 1f);

	private VisualElement teamBlueContainer;

	private VisualElement teamRedContainer;

	private VisualElement teamSpectatorContainer;

	private Dictionary<Player, VisualElement> playerVisualElementMap = new Dictionary<Player, VisualElement>();

	private void Start()
	{
		base.VisibilityRequiresMouse = true;
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("ScoreboardContainer");
		teamBlueContainer = container.Query<VisualElement>("TeamBlueContainer");
		teamRedContainer = container.Query<VisualElement>("TeamRedContainer");
		teamSpectatorContainer = container.Query<VisualElement>("TeamSpectatorContainer");
		teamBlueContainer.Clear();
		teamRedContainer.Clear();
		teamSpectatorContainer.Clear();
	}

	public void AddPlayer(Player player)
	{
		if (!Application.isBatchMode && !playerVisualElementMap.ContainsKey(player))
		{
			VisualElement visualElement = Utils.InstantiateVisualTreeAsset(scoreboardPlayerAsset, Position.Relative);
			teamSpectatorContainer.Add(visualElement);
			((Button)visualElement.Query<Button>("ScoreboardPlayerButton")).RegisterCallback<ClickEvent, Player>(OnPlayerClicked, player);
			playerVisualElementMap.Add(player, visualElement);
			UpdatePlayer(player);
		}
	}

	public void RemovePlayer(Player player)
	{
		if (playerVisualElementMap.ContainsKey(player))
		{
			((Button)playerVisualElementMap[player].Query<Button>("ScoreboardPlayerButton")).UnregisterCallback<ClickEvent, Player>(OnPlayerClicked);
			playerVisualElementMap[player].RemoveFromHierarchy();
			playerVisualElementMap.Remove(player);
		}
	}

	public void UpdatePlayer(Player player)
	{
		if (Application.isBatchMode || !playerVisualElementMap.ContainsKey(player))
		{
			return;
		}
		VisualElement visualElement = playerVisualElementMap[player];
		VisualElement visualElement2 = visualElement.Query<VisualElement>("PatreonVisualElement");
		Label label = visualElement.Query<Label>("UsernameLabel");
		Label label2 = visualElement.Query<Label>("GoalsLabel");
		Label label3 = visualElement.Query<Label>("AssistsLabel");
		Label label4 = visualElement.Query<Label>("PointsLabel");
		Label label5 = visualElement.Query<Label>("PingLabel");
		Label label6 = visualElement.Query<Label>("PositionLabel");
		string arg = ((player.AdminLevel.Value == 1) ? "<b><color=#206694>*</color></b>" : ((player.AdminLevel.Value == 2) ? "<b><color=#992d22>*</color></b>" : ((player.AdminLevel.Value > 2) ? "<b><color=#71368a>*</color></b>" : "")));
		visualElement2.style.display = ((player.PatreonLevel.Value <= 0) ? DisplayStyle.None : DisplayStyle.Flex);
		label.style.color = ((player.PatreonLevel.Value > 0) ? patreonColor : Color.white);
		label6.text = (player.PlayerPosition ? player.PlayerPosition.Name.ToString() : "N/A");
		label.text = $"{arg}<noparse>#{player.Number.Value} {player.Username.Value}</noparse>";
		label2.text = player.Goals.Value.ToString();
		label3.text = player.Assists.Value.ToString();
		label4.text = (player.Goals.Value + player.Assists.Value).ToString();
		label5.text = player.Ping.Value.ToString();
		visualElement.RemoveFromHierarchy();
		switch (player.Team.Value)
		{
		case PlayerTeam.Blue:
			if (visualElement.parent != teamBlueContainer)
			{
				teamBlueContainer.Add(visualElement);
			}
			return;
		case PlayerTeam.Red:
			if (visualElement.parent != teamRedContainer)
			{
				teamRedContainer.Add(visualElement);
			}
			return;
		}
		label2.text = "";
		label3.text = "";
		label4.text = "";
		if (visualElement.parent != teamSpectatorContainer)
		{
			teamSpectatorContainer.Add(visualElement);
		}
	}

	public void UpdateServer(Server server, int playerCount)
	{
		if (!Application.isBatchMode)
		{
			VisualElement e = container.Query<VisualElement>("ServerContainer");
			Label label = e.Query<Label>("NameLabel");
			Label label2 = e.Query<Label>("PlayersLabel");
			label.text = server.Name.Value;
			label2.text = $"{playerCount}/{server.MaxPlayers}";
		}
	}

	public void Clear()
	{
		foreach (KeyValuePair<Player, VisualElement> item in playerVisualElementMap.ToList())
		{
			RemovePlayer(item.Key);
		}
	}

	private void OnPlayerClicked(ClickEvent clickEvent, Player player)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnScoreboardClickPlayer", new Dictionary<string, object> { { "player", player } });
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
		return "UIScoreboard";
	}
}
