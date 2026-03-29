using AYellowpaper.SerializedCollections;
using UnityEngine;

public class StickMesh : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private MeshRenderer stickMeshRenderer;

	[SerializeField]
	private GameObject shaftTapeGameObject;

	[SerializeField]
	private MeshRenderer shaftTapeMeshRenderer;

	[SerializeField]
	private GameObject bladeTapeGameObject;

	[SerializeField]
	private MeshRenderer bladeTapeMeshRenderer;

	[Space(20f)]
	[SerializeField]
	private Collider shaftCollider;

	[SerializeField]
	private Collider bladeCollider;

	[Header("Settings")]
	[SerializeField]
	private SerializedDictionary<string, Material> stickMaterialMap = new SerializedDictionary<string, Material>();

	[SerializeField]
	private SerializedDictionary<string, Material> bladeTapeMaterialMap = new SerializedDictionary<string, Material>();

	[SerializeField]
	private SerializedDictionary<string, Material> shaftTapeMaterialMap = new SerializedDictionary<string, Material>();

	[HideInInspector]
	public Collider ShaftCollider => shaftCollider;

	[HideInInspector]
	public Collider BladeCollider => bladeCollider;

	public void SetSkin(PlayerTeam team, string skinName)
	{
		string key = ((team == PlayerTeam.Blue) ? "blue_" : "red_") + skinName;
		if (stickMaterialMap != null && stickMaterialMap.ContainsKey(key) && (bool)stickMeshRenderer)
		{
			Object.Destroy(stickMeshRenderer.material);
			stickMeshRenderer.material = stickMaterialMap[key];
		}
	}

	public void SetShaftTape(string tapeSkinName)
	{
		if (shaftTapeMaterialMap != null && shaftTapeMaterialMap.ContainsKey(tapeSkinName) && (bool)shaftTapeMeshRenderer && (bool)shaftTapeGameObject)
		{
			if (shaftTapeMaterialMap[tapeSkinName] == null)
			{
				shaftTapeGameObject.SetActive(value: false);
				return;
			}
			shaftTapeGameObject.SetActive(value: true);
			Object.Destroy(shaftTapeMeshRenderer.material);
			shaftTapeMeshRenderer.material = shaftTapeMaterialMap[tapeSkinName];
		}
	}

	public void SetBladeTape(string tapeSkinName)
	{
		if (bladeTapeMaterialMap != null && bladeTapeMaterialMap.ContainsKey(tapeSkinName) && (bool)bladeTapeMeshRenderer && (bool)bladeTapeGameObject)
		{
			if (bladeTapeMaterialMap[tapeSkinName] == null)
			{
				bladeTapeGameObject.SetActive(value: false);
				return;
			}
			bladeTapeGameObject.SetActive(value: true);
			Object.Destroy(bladeTapeMeshRenderer.material);
			bladeTapeMeshRenderer.material = bladeTapeMaterialMap[tapeSkinName];
		}
	}

	private void OnDestroy()
	{
		stickMaterialMap.Clear();
		bladeTapeMaterialMap.Clear();
		shaftTapeMaterialMap.Clear();
		stickMaterialMap = null;
		bladeTapeMaterialMap = null;
		shaftTapeMaterialMap = null;
	}
}
