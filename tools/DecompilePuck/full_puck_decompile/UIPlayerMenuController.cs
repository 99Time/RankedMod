using UnityEngine;

public class UIPlayerMenuController : MonoBehaviour
{
	private UIPlayerMenu uiPlayerMenu;

	private void Awake()
	{
		uiPlayerMenu = GetComponent<UIPlayerMenu>();
	}

	private void Start()
	{
	}

	private void OnDestroy()
	{
	}
}
