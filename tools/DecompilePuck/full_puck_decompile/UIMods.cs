using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UIMods : UIComponent<UIMods>
{
	[Header("Components")]
	public VisualTreeAsset modAsset;

	private ScrollView scrollView;

	private VisualElement noModsContainer;

	private Button closeButton;

	private Button findModsButton;

	private Button refreshButton;

	private Dictionary<Mod, VisualElement> modVisualElementMap = new Dictionary<Mod, VisualElement>();

	private void Start()
	{
		base.VisibilityRequiresMouse = true;
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("ModsContainer");
		scrollView = container.Query<ScrollView>("ScrollView");
		noModsContainer = container.Query<VisualElement>("NoModsContainer");
		closeButton = container.Query<Button>("CloseButton");
		closeButton.clicked += OnClickClose;
		findModsButton = container.Query<Button>("FindModsButton");
		findModsButton.clicked += OnClickFindMods;
		refreshButton = container.Query<Button>("RefreshButton");
		refreshButton.clicked += OnClickRefresh;
	}

	public override void Show()
	{
		base.Show();
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnModsShow");
	}

	public void AddMod(Mod mod)
	{
		if (!modVisualElementMap.ContainsKey(mod))
		{
			VisualElement visualElement = Utils.InstantiateVisualTreeAsset(modAsset, Position.Relative);
			((Button)visualElement.Query<Button>("EnableButton")).clicked += delegate
			{
				OnClickEnable(mod);
			};
			((Button)visualElement.Query<Button>("DisableButton")).clicked += delegate
			{
				OnClickDisable(mod);
			};
			modVisualElementMap.Add(mod, visualElement);
			scrollView.contentContainer.Add(visualElement);
			UpdateMod(mod);
			UpdateNoModsContainer();
		}
	}

	public void UpdateMod(Mod mod)
	{
		if (modVisualElementMap.ContainsKey(mod))
		{
			InstalledItem installedItem = mod.InstalledItem;
			ItemDetails itemDetails = installedItem.ItemDetails;
			VisualElement e = modVisualElementMap[mod];
			VisualElement visualElement = e.Query<VisualElement>("PreviewVisualElement");
			if (mod.PreviewTexture != null)
			{
				visualElement.style.backgroundImage = Background.FromTexture2D(mod.PreviewTexture);
			}
			((Label)e.Query<Label>("TitleLabel")).text = ((itemDetails != null) ? itemDetails.Title : installedItem.Id.ToString());
			Label label = e.Query<Label>("DescriptionLabel");
			object text;
			if (itemDetails == null)
			{
				text = "";
			}
			else
			{
				string description = itemDetails.Description;
				text = ((description != null && description.Length > 128) ? (itemDetails.Description.Substring(0, 256) + "...") : itemDetails.Description);
			}
			label.text = (string)text;
			((Button)e.Query<Button>("EnableButton")).style.display = ((!mod.IsAssemblyMod || mod.IsEnabled) ? DisplayStyle.None : DisplayStyle.Flex);
			((Button)e.Query<Button>("DisableButton")).style.display = ((!mod.IsAssemblyMod || !mod.IsEnabled) ? DisplayStyle.None : DisplayStyle.Flex);
		}
	}

	public void RemoveMod(Mod mod)
	{
		if (modVisualElementMap.ContainsKey(mod))
		{
			VisualElement element = modVisualElementMap[mod];
			scrollView.contentContainer.Remove(element);
			modVisualElementMap.Remove(mod);
			UpdateNoModsContainer();
		}
	}

	public void ClearMods()
	{
		scrollView.contentContainer.Clear();
	}

	private void UpdateNoModsContainer()
	{
		noModsContainer.style.display = ((modVisualElementMap.Count > 0) ? DisplayStyle.None : DisplayStyle.Flex);
	}

	private void OnClickClose()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnModsClickClose");
	}

	private void OnClickFindMods()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnModsClickFindMods");
	}

	private void OnClickRefresh()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnModsClickRefresh");
	}

	private void OnClickEnable(Mod mod)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnModClickEnable", new Dictionary<string, object> { { "mod", mod } });
	}

	private void OnClickDisable(Mod mod)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnModClickDisable", new Dictionary<string, object> { { "mod", mod } });
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
		return "UIMods";
	}
}
