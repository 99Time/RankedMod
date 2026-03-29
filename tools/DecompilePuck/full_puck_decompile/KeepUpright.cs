using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class KeepUpright : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float proportionalGain = 50f;

	[SerializeField]
	private float integralGain;

	[SerializeField]
	private float derivativeGain = 5f;

	[HideInInspector]
	public float Balance = 1f;

	[HideInInspector]
	public Rigidbody Rigidbody;

	private Vector3PIDController pidController = new Vector3PIDController();

	private void Awake()
	{
		Rigidbody = GetComponent<Rigidbody>();
	}

	private void FixedUpdate()
	{
		pidController.proportionalGain = proportionalGain * Balance;
		pidController.integralGain = integralGain * Balance;
		pidController.derivativeGain = derivativeGain * Balance;
		Vector3 vector = Vector3.Cross(pidController.Update(Time.fixedDeltaTime, base.transform.up, Vector3.up), Vector3.up);
		if (NetworkManager.Singleton.IsServer)
		{
			Rigidbody.AddTorque(-vector, ForceMode.Acceleration);
		}
	}
}
