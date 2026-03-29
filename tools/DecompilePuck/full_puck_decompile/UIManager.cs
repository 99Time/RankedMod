using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UIManager : NetworkBehaviourSingleton<UIManager>
{
	[Header("References")]
	[SerializeField]
	private AudioClip selectAudioClip;

	[SerializeField]
	private AudioClip clickAudioClip;

	[SerializeField]
	private AudioClip notificationAudioClip;

	public AudioSource AudioSource;

	[HideInInspector]
	public UIMainMenu MainMenu;

	[HideInInspector]
	public UIPauseMenu PauseMenu;

	[HideInInspector]
	public UIServerBrowser ServerBrowser;

	[HideInInspector]
	public UIGameState GameState;

	[HideInInspector]
	public UIChat Chat;

	[HideInInspector]
	public UITeamSelect TeamSelect;

	[HideInInspector]
	public UIPositionSelect PositionSelect;

	[HideInInspector]
	public UIScoreboard Scoreboard;

	[HideInInspector]
	public UISettings Settings;

	[HideInInspector]
	public UIHUD Hud;

	[HideInInspector]
	public UIAnnouncement Announcement;

	[HideInInspector]
	public UIMinimap Minimap;

	[HideInInspector]
	public UIServerLauncher ServerLauncher;

	[HideInInspector]
	public UIToastManager ToastManager;

	[HideInInspector]
	public UIOverlayManager OverlayManager;

	[HideInInspector]
	public UIPlayerMenu PlayerMenu;

	[HideInInspector]
	public UIIdentity Identity;

	[HideInInspector]
	public UIAppearance Appearance;

	[HideInInspector]
	public UIPopupManager PopupManager;

	[HideInInspector]
	public UIPlayerUsernames PlayerUsernames;

	[HideInInspector]
	public UIDebug Debug;

	[HideInInspector]
	public UIMods Mods;

	[HideInInspector]
	public bool isMouseActive;

	[HideInInspector]
	public UIState UIState;

	[HideInInspector]
	public UIDocument UiDocument;

	[HideInInspector]
	public PanelSettings PanelSettings;

	[HideInInspector]
	public VisualElement RootVisualElement;

	private List<UIComponent> components = new List<UIComponent>();

	private float lastSelectSoundTime;

	private float lastClickSoundTime;

	private float lastNotificationSoundTime;

	public override void Awake()
	{
		base.Awake();
		AudioSource = base.gameObject.GetComponent<AudioSource>();
		UiDocument = GetComponent<UIDocument>();
		PanelSettings = UiDocument.panelSettings;
		RootVisualElement = UiDocument.rootVisualElement;
		RootVisualElement.RegisterCallback(delegate(MouseEnterEvent e)
		{
			VisualElement visualElement = e.target as VisualElement;
			bool num = visualElement != null && visualElement is Button;
			bool flag = visualElement != null && visualElement.name == "unity-tab__header";
			if (num || flag)
			{
				PlayerSelectSound();
			}
		}, TrickleDown.TrickleDown);
		RootVisualElement.RegisterCallback(delegate(MouseDownEvent e)
		{
			VisualElement visualElement = e.target as VisualElement;
			bool num = visualElement != null && visualElement is Button;
			bool flag = visualElement?.name.Contains("unity-tab__header") ?? false;
			if (num || flag)
			{
				PlayerClickSound();
			}
		}, TrickleDown.TrickleDown);
		MainMenu = base.gameObject.GetComponent<UIMainMenu>();
		MainMenu.Initialize(RootVisualElement);
		components.Add(MainMenu);
		PauseMenu = base.gameObject.GetComponent<UIPauseMenu>();
		PauseMenu.Initialize(RootVisualElement);
		components.Add(PauseMenu);
		ServerBrowser = base.gameObject.GetComponent<UIServerBrowser>();
		ServerBrowser.Initialize(RootVisualElement);
		components.Add(ServerBrowser);
		GameState = base.gameObject.GetComponent<UIGameState>();
		GameState.Initialize(RootVisualElement);
		components.Add(GameState);
		Chat = base.gameObject.GetComponent<UIChat>();
		Chat.Initialize(RootVisualElement);
		components.Add(Chat);
		TeamSelect = base.gameObject.GetComponent<UITeamSelect>();
		TeamSelect.Initialize(RootVisualElement);
		components.Add(TeamSelect);
		PositionSelect = base.gameObject.GetComponent<UIPositionSelect>();
		PositionSelect.Initialize(RootVisualElement);
		components.Add(PositionSelect);
		Scoreboard = base.gameObject.GetComponent<UIScoreboard>();
		Scoreboard.Initialize(RootVisualElement);
		components.Add(Scoreboard);
		Settings = base.gameObject.GetComponent<UISettings>();
		Settings.Initialize(RootVisualElement);
		components.Add(Settings);
		Hud = base.gameObject.GetComponent<UIHUD>();
		Hud.Initialize(RootVisualElement);
		components.Add(Hud);
		Announcement = base.gameObject.GetComponent<UIAnnouncement>();
		Announcement.Initialize(RootVisualElement);
		components.Add(Announcement);
		Minimap = base.gameObject.GetComponent<UIMinimap>();
		Minimap.Initialize(RootVisualElement);
		components.Add(Minimap);
		ServerLauncher = base.gameObject.GetComponent<UIServerLauncher>();
		ServerLauncher.Initialize(RootVisualElement);
		components.Add(ServerLauncher);
		ToastManager = base.gameObject.GetComponent<UIToastManager>();
		ToastManager.Initialize(RootVisualElement);
		components.Add(ToastManager);
		OverlayManager = base.gameObject.GetComponent<UIOverlayManager>();
		OverlayManager.Initialize(RootVisualElement);
		components.Add(OverlayManager);
		PlayerMenu = base.gameObject.GetComponent<UIPlayerMenu>();
		PlayerMenu.Initialize(RootVisualElement);
		components.Add(PlayerMenu);
		Identity = base.gameObject.GetComponent<UIIdentity>();
		Identity.Initialize(RootVisualElement);
		components.Add(Identity);
		Appearance = base.gameObject.GetComponent<UIAppearance>();
		Appearance.Initialize(RootVisualElement);
		components.Add(Appearance);
		PopupManager = base.gameObject.GetComponent<UIPopupManager>();
		PopupManager.Initialize(RootVisualElement);
		components.Add(PopupManager);
		PlayerUsernames = base.gameObject.GetComponent<UIPlayerUsernames>();
		PlayerUsernames.Initialize(RootVisualElement);
		components.Add(PlayerUsernames);
		Debug = base.gameObject.GetComponent<UIDebug>();
		Debug.Initialize(RootVisualElement);
		components.Add(Debug);
		Mods = base.gameObject.GetComponent<UIMods>();
		Mods.Initialize(RootVisualElement);
		components.Add(Mods);
	}

	private void Start()
	{
		AddComponentCallbacks();
	}

	public override void OnDestroy()
	{
		RemoveComponentCallbacks();
		base.OnDestroy();
	}

	public void HideAllComponents()
	{
		foreach (UIComponent component in components)
		{
			component.Hide();
		}
	}

	public void ShowMainMenuComponents()
	{
		MainMenu.Show();
		Chat.Show();
	}

	public void HideMainMenuComponents()
	{
		MainMenu.Hide();
		Chat.Hide();
	}

	public void ShowGameComponents()
	{
		GameState.Show();
		Chat.Show();
		Minimap.Show();
	}

	public void HideGameComponents()
	{
		GameState.Hide();
		Chat.Hide();
		Minimap.Hide();
	}

	private void ShowMouse()
	{
		isMouseActive = true;
		UnityEngine.Cursor.lockState = CursorLockMode.None;
		UnityEngine.Cursor.visible = true;
	}

	private void HideMouse()
	{
		isMouseActive = false;
		UnityEngine.Cursor.lockState = CursorLockMode.Locked;
		UnityEngine.Cursor.visible = false;
	}

	private void AddComponentCallbacks()
	{
		foreach (UIComponent component in components)
		{
			component.OnVisibilityChanged += OnMouseRequiredComponentChangedVisibility;
			component.OnFocusChanged += OnMouseRequiredComponentChangedFocus;
		}
	}

	private void RemoveComponentCallbacks()
	{
		foreach (UIComponent component in components)
		{
			component.OnVisibilityChanged += OnMouseRequiredComponentChangedVisibility;
			component.OnFocusChanged += OnMouseRequiredComponentChangedFocus;
		}
	}

	public void SetUiState(UIState state)
	{
		UIState = state;
		switch (UIState)
		{
		case UIState.MainMenu:
			HideAllComponents();
			ShowMainMenuComponents();
			break;
		case UIState.Play:
			HideAllComponents();
			ShowGameComponents();
			break;
		}
	}

	public void SetUiScale(float value)
	{
		PanelSettings.scale = value;
	}

	private void OnMouseRequiredComponentChangedVisibility(object sender, EventArgs args)
	{
		UpdateMouseVisibility();
	}

	private void OnMouseRequiredComponentChangedFocus(object sender, EventArgs args)
	{
		UpdateMouseVisibility();
	}

	private void UpdateMouseVisibility()
	{
		foreach (UIComponent component in components)
		{
			if ((component.IsVisible && component.VisibilityRequiresMouse) || (component.IsFocused && component.FocusRequiresMouse))
			{
				ShowMouse();
				return;
			}
		}
		HideMouse();
	}

	public void PlayerSelectSound()
	{
		if (!(Time.time - lastSelectSoundTime < selectAudioClip.length) && selectAudioClip != null)
		{
			lastSelectSoundTime = Time.time;
			AudioSource.PlayOneShot(selectAudioClip);
		}
	}

	public void PlayerClickSound()
	{
		if (!(Time.time - lastClickSoundTime < clickAudioClip.length) && clickAudioClip != null)
		{
			lastClickSoundTime = Time.time;
			AudioSource.PlayOneShot(clickAudioClip);
		}
	}

	public void PlayerNotificationSound()
	{
		if (!(Time.time - lastNotificationSoundTime < notificationAudioClip.length) && notificationAudioClip != null)
		{
			lastNotificationSoundTime = Time.time;
			AudioSource.PlayOneShot(notificationAudioClip);
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
		return "UIManager";
	}
}
