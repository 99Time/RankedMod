using System.Linq;
using AYellowpaper.SerializedCollections;
using UnityEngine;

[RequireComponent(typeof(MeshRendererTexturer))]
public class PlayerGroin : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private SerializedDictionary<string, Texture> textureMap = new SerializedDictionary<string, Texture>();

	private MeshRendererTexturer meshRendererTexturer;

	public string[] TextureNames => textureMap.Keys.ToArray();

	private void Awake()
	{
		meshRendererTexturer = GetComponent<MeshRendererTexturer>();
	}

	public void SetTexture(string name)
	{
		if (textureMap.ContainsKey(name) && (bool)meshRendererTexturer)
		{
			meshRendererTexturer.SetTexture(textureMap[name]);
		}
	}

	private void OnDestroy()
	{
		textureMap.Clear();
		textureMap = null;
	}
}
