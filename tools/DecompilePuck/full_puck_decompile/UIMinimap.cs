using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UIMinimap : UIComponent<UIMinimap>
{
	[Header("Components")]
	[SerializeField]
	private VisualTreeAsset playerAsset;

	[SerializeField]
	private VisualTreeAsset puckAsset;

	[Header("Settings")]
	[SerializeField]
	private int updateRate = 30;

	[SerializeField]
	private Color teamBlueColor = Color.blue;

	[SerializeField]
	private Color teamRedColor = Color.red;

	[SerializeField]
	private Vector2 size = new Vector2(256f, 512f);

	[HideInInspector]
	public PlayerTeam Team;

	private VisualElement minimapVisualElement;

	private VisualElement minimapBackgroundVisualElement;

	private VisualElement minimapMarkingsVisualElement;

	private Dictionary<PlayerBodyV2, VisualElement> playerBodyVisualElementMap = new Dictionary<PlayerBodyV2, VisualElement>();

	private Dictionary<Puck, VisualElement> puckVisualElementMap = new Dictionary<Puck, VisualElement>();

	private float updateAccumulator;

	[HideInInspector]
	public Vector2 Position => new Vector2(minimapVisualElement.style.left.value.value, minimapVisualElement.style.bottom.value.value);

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
		foreach (KeyValuePair<PlayerBodyV2, VisualElement> item in playerBodyVisualElementMap)
		{
			PlayerBodyV2 key = item.Key;
			VisualElement value = item.Value;
			if ((bool)key)
			{
				VisualElement visualElement = value.Query<VisualElement>("Body");
				Vector3 position = ((Team == PlayerTeam.Blue) ? key.transform.position : (-key.transform.position));
				float num = ((Team == PlayerTeam.Blue) ? key.transform.rotation.eulerAngles.y : (key.transform.rotation.eulerAngles.y + 180f));
				Vector2 vector = WorldPositionToMinimapPosition(position, NetworkBehaviourSingleton<LevelManager>.Instance.IceBounds);
				value.style.translate = new Translate(0f - vector.x, vector.y);
				visualElement.style.rotate = new Rotate(num);
			}
		}
		foreach (KeyValuePair<Puck, VisualElement> item2 in puckVisualElementMap)
		{
			Puck key2 = item2.Key;
			VisualElement value2 = item2.Value;
			if ((bool)key2)
			{
				Vector3 position2 = ((Team == PlayerTeam.Blue) ? key2.transform.position : (-key2.transform.position));
				float num2 = ((Team == PlayerTeam.Blue) ? key2.transform.rotation.eulerAngles.y : (key2.transform.rotation.eulerAngles.y + 180f));
				Vector2 vector2 = WorldPositionToMinimapPosition(position2, NetworkBehaviourSingleton<LevelManager>.Instance.IceBounds);
				value2.style.translate = new Translate(0f - vector2.x, vector2.y);
				value2.style.rotate = new Rotate(num2);
			}
		}
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("MinimapContainer");
		minimapVisualElement = container.Query<VisualElement>("Minimap");
		minimapBackgroundVisualElement = minimapVisualElement.Query<VisualElement>("MinimapBackground");
		minimapMarkingsVisualElement = minimapVisualElement.Query<VisualElement>("MinimapMarkings");
	}

	public override void Show()
	{
		if (MonoBehaviourSingleton<SettingsManager>.Instance.ShowGameUserInterface != 0)
		{
			base.Show();
			SetOpacity(MonoBehaviourSingleton<SettingsManager>.Instance.MinimapOpacity);
		}
	}

	public void AddPlayerBody(PlayerBodyV2 playerBody)
	{
		if (!Application.isBatchMode && (bool)playerBody && !playerBodyVisualElementMap.ContainsKey(playerBody))
		{
			VisualElement visualElement = Utils.InstantiateVisualTreeAsset(playerAsset).Query<VisualElement>("MinimapPlayer");
			playerBodyVisualElementMap.Add(playerBody, visualElement);
			minimapMarkingsVisualElement.Add(visualElement);
			visualElement.SendToBack();
			UpdatePlayerBody(playerBody);
		}
	}

	public void UpdatePlayerBody(PlayerBodyV2 playerBody)
	{
		if (!playerBody || !playerBodyVisualElementMap.ContainsKey(playerBody))
		{
			return;
		}
		VisualElement e = playerBodyVisualElementMap[playerBody];
		Player player = playerBody.Player;
		if ((bool)player)
		{
			VisualElement visualElement = e.Query<VisualElement>("Body");
			VisualElement visualElement2 = e.Query<VisualElement>("Local");
			Label label = e.Query<Label>("Number");
			switch (player.Team.Value)
			{
			case PlayerTeam.Blue:
				visualElement.style.unityBackgroundImageTintColor = new StyleColor(teamBlueColor);
				break;
			case PlayerTeam.Red:
				visualElement.style.unityBackgroundImageTintColor = new StyleColor(teamRedColor);
				break;
			default:
				visualElement.style.unityBackgroundImageTintColor = new StyleColor(Color.gray);
				break;
			}
			label.text = player.Number.Value.ToString();
			label.style.visibility = (player.IsLocalPlayer ? Visibility.Hidden : Visibility.Visible);
			visualElement2.style.visibility = ((!player.IsLocalPlayer) ? Visibility.Hidden : Visibility.Visible);
		}
	}

	public void RemovePlayerBody(PlayerBodyV2 playerBody)
	{
		if ((bool)playerBody && playerBodyVisualElementMap.ContainsKey(playerBody))
		{
			minimapMarkingsVisualElement.Remove(playerBodyVisualElementMap[playerBody]);
			playerBodyVisualElementMap.Remove(playerBody);
		}
	}

	public void AddPuck(Puck puck)
	{
		if (!Application.isBatchMode && (bool)puck && !puckVisualElementMap.ContainsKey(puck))
		{
			VisualElement visualElement = Utils.InstantiateVisualTreeAsset(puckAsset).Query<VisualElement>("MinimapPuck");
			puckVisualElementMap.Add(puck, visualElement);
			minimapMarkingsVisualElement.Add(visualElement);
			visualElement.BringToFront();
		}
	}

	public void RemovePuck(Puck puck)
	{
		if ((bool)puck && puckVisualElementMap.ContainsKey(puck))
		{
			minimapMarkingsVisualElement.Remove(puckVisualElementMap[puck]);
			puckVisualElementMap.Remove(puck);
		}
	}

	private Vector2 WorldPositionToMinimapPosition(Vector3 position, Bounds bounds)
	{
		Vector2 vector = new Vector2((position.x + bounds.center.x) / bounds.size.x, (position.z + bounds.center.z) / bounds.size.z);
		Vector2 vector2 = new Vector2(minimapMarkingsVisualElement.resolvedStyle.width, minimapMarkingsVisualElement.resolvedStyle.height);
		return new Vector2(vector2.x * vector.x, vector2.y * vector.y);
	}

	public void SetOpacity(float opacity)
	{
		if (minimapVisualElement != null)
		{
			minimapVisualElement.style.opacity = opacity;
		}
	}

	public void SetPosition(Vector2 position)
	{
		if (minimapVisualElement != null)
		{
			minimapVisualElement.style.left = new Length(position.x, LengthUnit.Percent);
			minimapVisualElement.style.bottom = new Length(position.y, LengthUnit.Percent);
			float value = Utils.Map(position.x, 0f, 100f, 0f, -100f);
			float value2 = Utils.Map(position.y, 0f, 100f, 0f, 100f);
			minimapVisualElement.style.translate = new Translate(new Length(value, LengthUnit.Percent), new Length(value2, LengthUnit.Percent));
		}
	}

	public void SetBackgroundOpacity(float opacity)
	{
		if (minimapBackgroundVisualElement != null)
		{
			minimapBackgroundVisualElement.style.opacity = opacity;
		}
	}

	public void SetScale(float scale)
	{
		if (minimapVisualElement != null)
		{
			minimapVisualElement.style.width = new Length(size.x * scale, LengthUnit.Pixel);
			minimapVisualElement.style.height = new Length(size.y * scale, LengthUnit.Pixel);
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
		return "UIMinimap";
	}
}
