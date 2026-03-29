using UnityEngine;

public class SoftCollider : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private Vector3 localOrigin = Vector3.zero;

	[SerializeField]
	private LayerMask layerMask;

	[SerializeField]
	private float distance = 0.5f;

	[SerializeField]
	private float force = 10f;

	[HideInInspector]
	public float Intensity = 1f;

	[HideInInspector]
	public Rigidbody Rigidbody;

	private Vector3 worldOrigin = Vector3.zero;

	private void Awake()
	{
		Rigidbody = GetComponent<Rigidbody>();
	}

	private void FixedUpdate()
	{
		worldOrigin = base.transform.TransformPoint(localOrigin);
		Vector3[] array = new Vector3[4]
		{
			(base.transform.forward + base.transform.right).normalized,
			(base.transform.forward - base.transform.right).normalized,
			(-base.transform.forward + base.transform.right).normalized,
			(-base.transform.forward - base.transform.right).normalized
		};
		foreach (Vector3 vector in array)
		{
			Debug.DrawRay(worldOrigin, vector * distance, Color.black);
			if (Physics.Raycast(worldOrigin, vector, out var hitInfo, distance, layerMask))
			{
				Debug.DrawRay(worldOrigin, vector * hitInfo.distance, Color.white);
				float num = distance - hitInfo.distance;
				float magnitude = Vector3.Cross(hitInfo.normal, vector).magnitude;
				float num2 = 1f - magnitude;
				Debug.DrawRay(hitInfo.point, hitInfo.normal * num * force, Color.green);
				Rigidbody.AddForceAtPosition(hitInfo.normal * num * (force * num2), hitInfo.point, ForceMode.Acceleration);
			}
		}
	}
}
