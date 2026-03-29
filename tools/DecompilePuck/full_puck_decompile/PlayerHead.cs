using System.Linq;
using AYellowpaper.SerializedCollections;
using UnityEngine;

public class PlayerHead : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private GameObject head;

	[SerializeField]
	private SerializedDictionary<string, Texture> flagTextureMap = new SerializedDictionary<string, Texture>();

	[SerializeField]
	private SerializedDictionary<string, GameObject> hairGameObjectMap = new SerializedDictionary<string, GameObject>();

	[SerializeField]
	private SerializedDictionary<string, GameObject> mustacheGameObjectMap = new SerializedDictionary<string, GameObject>();

	[SerializeField]
	private SerializedDictionary<string, GameObject> beardGameObjectMap = new SerializedDictionary<string, GameObject>();

	[SerializeField]
	private GameObject helmet;

	[SerializeField]
	private GameObject helmetFlag;

	[SerializeField]
	private GameObject helmetStrapLeft;

	[SerializeField]
	private GameObject helmetStrapRight;

	[SerializeField]
	private GameObject helmetVisor;

	[SerializeField]
	private SerializedDictionary<string, Material> visorMaterialMap = new SerializedDictionary<string, Material>();

	[SerializeField]
	private GameObject cage;

	[SerializeField]
	private GameObject neckShield;

	private PlayerHeadType headType;

	public PlayerHeadType HeadType
	{
		get
		{
			return headType;
		}
		set
		{
			if (headType != value)
			{
				headType = value;
				OnHeadTypeChanged();
			}
		}
	}

	public string[] HairTypes => hairGameObjectMap.Keys.ToArray();

	public string[] MustacheTypes => mustacheGameObjectMap.Keys.ToArray();

	public string[] BeardTypes => beardGameObjectMap.Keys.ToArray();

	public void HideHair()
	{
		foreach (GameObject value in hairGameObjectMap.Values)
		{
			if ((bool)value)
			{
				value.SetActive(value: false);
			}
		}
	}

	public void HideMustache()
	{
		foreach (GameObject value in mustacheGameObjectMap.Values)
		{
			if ((bool)value)
			{
				value.SetActive(value: false);
			}
		}
	}

	public void HideBeard()
	{
		foreach (GameObject value in beardGameObjectMap.Values)
		{
			if ((bool)value)
			{
				value.SetActive(value: false);
			}
		}
	}

	public void HideGear()
	{
		helmet.SetActive(value: false);
		helmetStrapLeft.SetActive(value: false);
		helmetStrapRight.SetActive(value: false);
		helmetFlag.SetActive(value: false);
		helmetVisor.SetActive(value: false);
		cage.SetActive(value: false);
		neckShield.SetActive(value: false);
	}

	public void SetHair(string name)
	{
		if (HeadType == PlayerHeadType.Spectator && hairGameObjectMap.ContainsKey(name))
		{
			if (hairGameObjectMap[name] == null)
			{
				HideHair();
				return;
			}
			HideHair();
			hairGameObjectMap[name].SetActive(value: true);
		}
	}

	public void SetMustache(string name)
	{
		if (mustacheGameObjectMap.ContainsKey(name))
		{
			if (mustacheGameObjectMap[name] == null)
			{
				HideMustache();
				return;
			}
			HideMustache();
			mustacheGameObjectMap[name].SetActive(value: true);
		}
	}

	public void SetBeard(string name)
	{
		if (beardGameObjectMap.ContainsKey(name))
		{
			if (beardGameObjectMap[name] == null)
			{
				HideBeard();
				return;
			}
			HideBeard();
			beardGameObjectMap[name].SetActive(value: true);
		}
	}

	public void SetHelmetFlag(string name)
	{
		if (!flagTextureMap.ContainsKey(name))
		{
			return;
		}
		if (flagTextureMap[name] == null)
		{
			helmetFlag.SetActive(value: false);
			return;
		}
		helmetFlag.SetActive(value: true);
		MeshRendererTexturer component = helmetFlag.GetComponent<MeshRendererTexturer>();
		if (component == null)
		{
			helmetFlag.SetActive(value: false);
		}
		else
		{
			component.SetTexture(flagTextureMap[name]);
		}
	}

	public void SetHelmetVisor(string name)
	{
		if (HeadType != PlayerHeadType.Attacker || visorMaterialMap == null || !visorMaterialMap.ContainsKey(name) || !helmetVisor)
		{
			return;
		}
		MeshRenderer component = helmetVisor.GetComponent<MeshRenderer>();
		if (!(component == null))
		{
			if (visorMaterialMap[name] == null)
			{
				helmetVisor.SetActive(value: false);
				return;
			}
			helmetVisor.SetActive(value: true);
			Object.Destroy(component.material);
			component.material = visorMaterialMap[name];
		}
	}

	private void OnHeadTypeChanged()
	{
		switch (HeadType)
		{
		case PlayerHeadType.Attacker:
			HideGear();
			helmet.SetActive(value: true);
			helmetStrapLeft.SetActive(value: true);
			helmetStrapRight.SetActive(value: true);
			helmetFlag.SetActive(value: true);
			helmetVisor.SetActive(value: true);
			break;
		case PlayerHeadType.Goalie:
			HideGear();
			helmet.SetActive(value: true);
			cage.SetActive(value: true);
			neckShield.SetActive(value: true);
			helmetFlag.SetActive(value: true);
			break;
		case PlayerHeadType.Spectator:
			HideGear();
			break;
		}
	}

	private void OnDestroy()
	{
		flagTextureMap.Clear();
		hairGameObjectMap.Clear();
		mustacheGameObjectMap.Clear();
		beardGameObjectMap.Clear();
		visorMaterialMap.Clear();
		flagTextureMap = null;
		hairGameObjectMap = null;
		mustacheGameObjectMap = null;
		beardGameObjectMap = null;
		visorMaterialMap = null;
	}
}
