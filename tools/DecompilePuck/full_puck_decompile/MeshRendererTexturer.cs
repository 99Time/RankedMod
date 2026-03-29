using UnityEngine;

public class MeshRendererTexturer : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private MeshRenderer meshRenderer;

	private Material material;

	private void Awake()
	{
		if (!meshRenderer)
		{
			meshRenderer = GetComponent<MeshRenderer>();
		}
		material = meshRenderer.material;
	}

	private void OnDestroy()
	{
		Object.Destroy(material);
	}

	public void SetTexture(Texture texture)
	{
		material.SetTexture("_BaseMap", texture);
	}
}
