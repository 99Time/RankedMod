using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Skate : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float traction = 0.15f;

	[HideInInspector]
	public Rigidbody Rigidbody;

	[HideInInspector]
	public float Intensity = 1f;

	[HideInInspector]
	public bool IsTractionLost;

	[HideInInspector]
	public Transform MovementDirection;

	private void Awake()
	{
		Rigidbody = GetComponent<Rigidbody>();
	}

	private void FixedUpdate()
	{
		Vector3 vector = MovementDirection.InverseTransformVector(Rigidbody.linearVelocity);
		vector.y = 0f;
		vector.z = 0f;
		float num = 0f - vector.x;
		IsTractionLost = num > traction * Time.fixedDeltaTime;
		num = Mathf.Clamp(num, (0f - traction) * Time.fixedDeltaTime, traction * Time.fixedDeltaTime);
		if (NetworkManager.Singleton.IsServer)
		{
			Rigidbody.AddForce(MovementDirection.right * num * Intensity, ForceMode.VelocityChange);
		}
	}
}
