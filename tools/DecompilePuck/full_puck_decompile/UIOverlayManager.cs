using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class UIOverlayManager : UIComponent<UIOverlayManager>
{
	[Header("Components")]
	public VisualTreeAsset overlayAsset;

	[HideInInspector]
	public Dictionary<string, Overlay> activeOverlays = new Dictionary<string, Overlay>();

	private VisualElement spinnerContainer;

	private IEnumerator spinnerShowCoroutine;

	private IEnumerator spinnerHideCoroutine;

	public override void Awake()
	{
		base.Awake();
		base.AlwaysVisible = true;
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("OverlaysContainer");
		spinnerContainer = container.Query<VisualElement>("SpinnerContainer");
		UpdateSpinnerVisibility(fade: false);
	}

	public void ShowOverlay(string overlayName, bool showSpinner = false, bool fade = false, bool autoHide = false, bool autoHideFade = false)
	{
		if (!Application.isBatchMode)
		{
			if (!activeOverlays.ContainsKey(overlayName))
			{
				TemplateContainer templateContainer = Utils.InstantiateVisualTreeAsset(overlayAsset);
				VisualElement visualElement = templateContainer.Query<VisualElement>("Overlay");
				Overlay overlay = new Overlay
				{
					Name = overlayName,
					ShowSpinner = showSpinner,
					VisualElement = visualElement,
					TemplateContainer = templateContainer
				};
				activeOverlays.Add(overlayName, overlay);
				StartCoroutine(IShowOverlay(overlay, fade, autoHide, autoHideFade));
			}
			UpdateSpinnerVisibility(fade);
		}
	}

	private IEnumerator IShowOverlay(Overlay overlay, bool fade, bool autoHide, bool autoHideFade)
	{
		container.Add(overlay.TemplateContainer);
		overlay.TemplateContainer.SendToBack();
		overlay.VisualElement.style.opacity = 0f;
		overlay.VisualElement.EnableInClassList("fade", fade);
		if (fade)
		{
			yield return new WaitForEndOfFrame();
		}
		overlay.VisualElement.style.opacity = 1f;
		if (autoHide)
		{
			if (autoHideFade)
			{
				yield return new WaitForSeconds(0.2f);
			}
			else
			{
				yield return new WaitForEndOfFrame();
			}
			HideOverlay(overlay.Name, autoHideFade);
		}
		yield return null;
	}

	public void HideOverlay(string overlayName, bool fade = false)
	{
		if (activeOverlays.ContainsKey(overlayName))
		{
			StartCoroutine(IHideOverlay(activeOverlays[overlayName], fade));
			activeOverlays.Remove(overlayName);
		}
		UpdateSpinnerVisibility(fade);
	}

	private IEnumerator IHideOverlay(Overlay overlay, bool fade)
	{
		overlay.VisualElement.style.opacity = 1f;
		overlay.VisualElement.EnableInClassList("fade", fade);
		if (fade)
		{
			yield return new WaitForEndOfFrame();
		}
		overlay.VisualElement.style.opacity = 0f;
		yield return new WaitForSeconds(0.2f);
		container.Remove(overlay.TemplateContainer);
		yield return null;
	}

	public void UpdateSpinnerVisibility(bool fade)
	{
		if (activeOverlays.Values.Any((Overlay overlay) => overlay.ShowSpinner))
		{
			ShowSpinner(fade);
		}
		else
		{
			HideSpinner(fade);
		}
	}

	private void ShowSpinner(bool fade = false)
	{
		if (spinnerShowCoroutine != null)
		{
			StopCoroutine(spinnerShowCoroutine);
		}
		if (spinnerHideCoroutine != null)
		{
			StopCoroutine(spinnerHideCoroutine);
		}
		spinnerShowCoroutine = IShowSpinner(fade);
		StartCoroutine(spinnerShowCoroutine);
	}

	private IEnumerator IShowSpinner(bool fade)
	{
		spinnerContainer.style.display = DisplayStyle.Flex;
		spinnerContainer.style.opacity = 0f;
		spinnerContainer.EnableInClassList("fade", fade);
		yield return new WaitForEndOfFrame();
		spinnerContainer.style.opacity = 1f;
	}

	private void HideSpinner(bool fade)
	{
		if (spinnerShowCoroutine != null)
		{
			StopCoroutine(spinnerShowCoroutine);
		}
		if (spinnerHideCoroutine != null)
		{
			StopCoroutine(spinnerHideCoroutine);
		}
		spinnerHideCoroutine = IHideSpinner(fade);
		StartCoroutine(spinnerHideCoroutine);
	}

	private IEnumerator IHideSpinner(bool fade)
	{
		spinnerContainer.style.opacity = 1f;
		spinnerContainer.EnableInClassList("fade", fade);
		yield return new WaitForEndOfFrame();
		spinnerContainer.style.opacity = 0f;
		yield return new WaitForSeconds(0.2f);
		spinnerContainer.style.display = DisplayStyle.None;
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
		return "UIOverlayManager";
	}
}
