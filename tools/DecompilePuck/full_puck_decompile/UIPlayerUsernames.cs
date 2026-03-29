using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UIPlayerUsernames : UIComponent<UIPlayerUsernames>
{
	[Header("Components")]
	[SerializeField]
	private VisualTreeAsset playerUsernameAsset;

	[Header("Settings")]
	[SerializeField]
	private float yOffset = 2.5f;

	[SerializeField]
	private float maximumDistance = 100f;

	[HideInInspector]
	public float FadeThreshold = 0.5f;

	private Dictionary<PlayerBodyV2, VisualElement> playerBodyVisualElementMap = new Dictionary<PlayerBodyV2, VisualElement>();

	[HideInInspector]
	public float FadeRange => maximumDistance / 4f;

	public override void Awake()
	{
		base.Awake();
		base.AlwaysVisible = true;
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		base.rootVisualElement = rootVisualElement;
		container = rootVisualElement.Query<VisualElement>("PlayerUsernamesContainer");
	}

	public void Update()
	{
		if (Application.isBatchMode)
		{
			return;
		}
		foreach (KeyValuePair<PlayerBodyV2, VisualElement> item in playerBodyVisualElementMap)
		{
			PlayerBodyV2 key = item.Key;
			VisualElement value = item.Value;
			if (!(key == null))
			{
				UsernameWorldToScreen(value, key);
			}
		}
	}

	public void AddPlayerBody(PlayerBodyV2 playerBody)
	{
		if (!Application.isBatchMode)
		{
			VisualElement visualElement = Utils.InstantiateVisualTreeAsset(playerUsernameAsset).Query<VisualElement>("PlayerUsername");
			playerBodyVisualElementMap.Add(playerBody, visualElement);
			UpdatePlayerBody(playerBody);
			container.Add(visualElement);
		}
	}

	public void RemovePlayerBody(PlayerBodyV2 playerBody)
	{
		if (playerBodyVisualElementMap.ContainsKey(playerBody))
		{
			VisualElement element = playerBodyVisualElementMap[playerBody];
			playerBodyVisualElementMap.Remove(playerBody);
			container.Remove(element);
		}
	}

	public void UpdatePlayerBody(PlayerBodyV2 playerBody)
	{
		if (playerBodyVisualElementMap.ContainsKey(playerBody))
		{
			((Label)playerBodyVisualElementMap[playerBody].Query<Label>("Username")).text = $"#{playerBody.Player.Number.Value} {playerBody.Player.Username.Value}";
		}
	}

	private void UsernameWorldToScreen(VisualElement playerVisualElement, PlayerBodyV2 playerBody)
	{
		if (!(Camera.main == null))
		{
			Vector3 position = Camera.main.transform.position;
			Vector3 position2 = playerBody.transform.position;
			float value = Vector3.Distance(position, position2);
			Vector3 vector = Camera.main.WorldToScreenPoint(position2 + Vector3.up * yOffset);
			vector.y = (float)Screen.height - vector.y;
			RuntimePanelUtils.ScreenToPanel(rootVisualElement.panel, vector);
			Vector2 vector2 = RuntimePanelUtils.ScreenToPanel(rootVisualElement.panel, vector);
			if (vector.z < 0f)
			{
				playerVisualElement.style.display = DisplayStyle.None;
				return;
			}
			playerVisualElement.style.display = DisplayStyle.Flex;
			playerVisualElement.style.left = vector2.x;
			playerVisualElement.style.top = vector2.y;
			float value2 = Utils.Map(value, maximumDistance * FadeThreshold - FadeRange / 2f, maximumDistance * FadeThreshold + FadeRange / 2f, 1f, 0f);
			value2 = Mathf.Clamp01(value2);
			playerVisualElement.style.opacity = new StyleFloat(value2);
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
		return "UIPlayerUsernames";
	}
}
