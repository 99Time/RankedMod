using UnityEngine;

public class PuckElevationIndicator : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private MeshRenderer planeMeshRenderer;

	[SerializeField]
	private LineRenderer lineRenderer;

	[Header("Settings")]
	[SerializeField]
	private float maximumDistance = 15f;

	[SerializeField]
	private float raycastVerticalOffset = 0.01f;

	[SerializeField]
	private LayerMask raycastLayerMask;

	private bool isVisible;

	private Material material;

	[HideInInspector]
	public bool IsVisible
	{
		get
		{
			return isVisible;
		}
		set
		{
			isVisible = value;
			planeMeshRenderer.enabled = isVisible;
			lineRenderer.enabled = isVisible;
		}
	}

	private void Awake()
	{
		lineRenderer.positionCount = 2;
		material = planeMeshRenderer.material;
		planeMeshRenderer.enabled = false;
		lineRenderer.enabled = false;
	}

	private void OnDestroy()
	{
		Object.Destroy(material);
	}

	private void Update()
	{
		if (IsVisible && (bool)material)
		{
			Debug.DrawRay(base.transform.position, Vector3.down * float.PositiveInfinity, Color.black);
			if (Physics.Raycast(base.transform.position, Vector3.down, out var hitInfo, float.PositiveInfinity, raycastLayerMask))
			{
				planeMeshRenderer.transform.position = hitInfo.point - Vector3.up * raycastVerticalOffset;
				planeMeshRenderer.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
				material.SetFloat("_Size", Mathf.Clamp(hitInfo.distance / maximumDistance, 0f, 1f));
				UpdateLineRendererPositions(hitInfo.point);
				planeMeshRenderer.enabled = true;
				lineRenderer.enabled = true;
			}
			else
			{
				planeMeshRenderer.enabled = false;
				lineRenderer.enabled = false;
			}
		}
	}

	private void UpdateLineRendererPositions(Vector3 hitPosition)
	{
		lineRenderer.SetPosition(0, hitPosition);
		lineRenderer.SetPosition(1, base.transform.position);
	}
}
