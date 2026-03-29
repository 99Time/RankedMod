using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class UIPositionSelect : UIComponent<UIPositionSelect>
{
	[Header("Components")]
	[SerializeField]
	private VisualTreeAsset claimPositionButtonAsset;

	[Header("Settings")]
	[SerializeField]
	private int updateRate = 30;

	private Dictionary<PlayerPosition, VisualElement> playerPositionVisualElementMap = new Dictionary<PlayerPosition, VisualElement>();

	private float updateAccumulator;

	public void Start()
	{
		base.VisibilityRequiresMouse = true;
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		base.rootVisualElement = rootVisualElement;
		container = rootVisualElement.Query<VisualElement>("PositionSelectContainer");
	}

	private void Update()
	{
		if (Application.isBatchMode)
		{
			return;
		}
		updateAccumulator += Time.deltaTime;
		if (updateAccumulator < 1f / (float)updateRate)
		{
			return;
		}
		updateAccumulator = 0f;
		foreach (KeyValuePair<PlayerPosition, VisualElement> item in playerPositionVisualElementMap)
		{
			PlayerPosition key = item.Key;
			VisualElement value = item.Value;
			if (!(key == null))
			{
				PositionWorldToScreen(value, key);
			}
		}
	}

	public void AddPosition(PlayerPosition playerPosition)
	{
		if (!Application.isBatchMode && !playerPositionVisualElementMap.ContainsKey(playerPosition))
		{
			TemplateContainer templateContainer = Utils.InstantiateVisualTreeAsset(claimPositionButtonAsset);
			VisualElement visualElement = templateContainer.Query<VisualElement>("Position");
			((Button)visualElement.Query<Button>("PositionClaimButton")).RegisterCallback<ClickEvent>(delegate
			{
				OnPositionClicked(playerPosition);
			});
			container.Add(templateContainer);
			playerPositionVisualElementMap.Add(playerPosition, templateContainer);
			StylePosition(visualElement, playerPosition);
		}
	}

	public void UpdatePosition(PlayerPosition playerPosition)
	{
		if (playerPositionVisualElementMap.ContainsKey(playerPosition))
		{
			StylePosition(playerPositionVisualElementMap[playerPosition], playerPosition);
		}
	}

	private void StylePosition(VisualElement positionVisualElement, PlayerPosition playerPosition)
	{
		Button button = positionVisualElement.Query<Button>("PositionClaimButton");
		Label label = positionVisualElement.Query<Label>("PositionUsernameLabel");
		string className = playerPosition.Team switch
		{
			PlayerTeam.Blue => "team-blue", 
			PlayerTeam.Red => "team-red", 
			_ => null, 
		};
		button.RemoveFromClassList("team-blue");
		button.RemoveFromClassList("team-red");
		button.AddToClassList(className);
		button.text = playerPosition.Name.ToString();
		button.enabledSelf = !playerPosition.IsClaimed;
		if (playerPosition.IsClaimed)
		{
			Player playerByClientId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(playerPosition.ClaimedBy.OwnerClientId);
			if ((bool)playerByClientId)
			{
				label.text = playerByClientId.Username.Value.ToString();
			}
		}
		else
		{
			label.text = " ";
		}
	}

	private void RemovePosition(PlayerPosition playerPosition)
	{
		if (playerPositionVisualElementMap.ContainsKey(playerPosition))
		{
			container.Remove(playerPositionVisualElementMap[playerPosition]);
			playerPositionVisualElementMap.Remove(playerPosition);
		}
	}

	public void ClearPositions()
	{
		foreach (KeyValuePair<PlayerPosition, VisualElement> item in playerPositionVisualElementMap.ToList())
		{
			RemovePosition(item.Key);
		}
	}

	private void PositionWorldToScreen(VisualElement positionVisualElement, PlayerPosition playerPosition)
	{
		if (!(Camera.main == null))
		{
			Vector3 vector = Camera.main.WorldToScreenPoint(playerPosition.transform.position);
			vector.y = (float)Screen.height - vector.y;
			RuntimePanelUtils.ScreenToPanel(rootVisualElement.panel, vector);
			Vector2 vector2 = RuntimePanelUtils.ScreenToPanel(rootVisualElement.panel, vector);
			if (vector.z < 0f)
			{
				positionVisualElement.style.display = DisplayStyle.None;
				return;
			}
			positionVisualElement.style.display = DisplayStyle.Flex;
			positionVisualElement.style.left = vector2.x;
			positionVisualElement.style.top = vector2.y;
		}
	}

	private void OnPositionClicked(PlayerPosition playerPosition)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPositionSelectClickPosition", new Dictionary<string, object> { { "playerPosition", playerPosition } });
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
		return "UIPositionSelect";
	}
}
