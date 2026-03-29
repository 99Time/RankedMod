using System.Collections.Generic;
using UnityEngine;

public class MeshRendererHider : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	public List<MeshRenderer> meshRenderers;

	[SerializeField]
	public List<MeshRenderer> meshRendererBlacklist;

	[SerializeField]
	public bool useChildrenMeshRenderers = true;

	private void Awake()
	{
		if (useChildrenMeshRenderers)
		{
			meshRenderers = new List<MeshRenderer>(GetComponentsInChildren<MeshRenderer>(includeInactive: true));
			meshRenderers.RemoveAll((MeshRenderer meshRenderer) => meshRendererBlacklist.Contains(meshRenderer));
		}
	}

	public void HideMeshRenderers()
	{
		foreach (MeshRenderer meshRenderer in meshRenderers)
		{
			meshRenderer.enabled = false;
		}
	}

	public void ShowMeshRenderers()
	{
		foreach (MeshRenderer meshRenderer in meshRenderers)
		{
			meshRenderer.enabled = true;
		}
	}
}
