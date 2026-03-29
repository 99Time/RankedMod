using Unity.Netcode;
using UnityEngine;

public class PuckCollisionDetectionModeSwitcher : MonoBehaviour
{
	[HideInInspector]
	public Rigidbody Rigidbody;

	[HideInInspector]
	public bool IsContactingStick;

	private void Awake()
	{
		Rigidbody = GetComponent<Rigidbody>();
		Utils.SetRigidbodyCollisionDetectionMode(Rigidbody, CollisionDetectionMode.ContinuousDynamic);
	}

	private void FixedUpdate()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			if (IsContactingStick)
			{
				Utils.SetRigidbodyCollisionDetectionMode(Rigidbody, CollisionDetectionMode.ContinuousSpeculative);
			}
			else
			{
				Utils.SetRigidbodyCollisionDetectionMode(Rigidbody, CollisionDetectionMode.ContinuousDynamic);
			}
			IsContactingStick = false;
		}
	}

	private void OnCollisionEnter(Collision collision)
	{
		if ((bool)collision.gameObject.GetComponent<Stick>())
		{
			IsContactingStick = true;
			Utils.SetRigidbodyCollisionDetectionMode(Rigidbody, CollisionDetectionMode.ContinuousSpeculative);
		}
	}

	private void OnCollisionStay(Collision collision)
	{
		if ((bool)collision.gameObject.GetComponent<Stick>())
		{
			IsContactingStick = true;
			Utils.SetRigidbodyCollisionDetectionMode(Rigidbody, CollisionDetectionMode.ContinuousSpeculative);
		}
	}

	public void OnDrawGizmos()
	{
		if (Application.isEditor)
		{
			if ((bool)Rigidbody)
			{
				Gizmos.color = ((Rigidbody.collisionDetectionMode == CollisionDetectionMode.ContinuousSpeculative) ? Color.red : Color.green);
			}
			Gizmos.DrawWireSphere(base.transform.position, 0.5f);
		}
	}
}
