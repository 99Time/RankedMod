using UnityEngine;

namespace MyPuckMod;

public class MovingTarget : MonoBehaviour
{
	public Rigidbody playerBody;

	public PuckAttackBehaviour mod;

	private bool clampEnabled = true;

	private void OnCollisionEnter(Collision collision)
	{
		if ((!(collision.rigidbody != null) || !(collision.rigidbody == playerBody)) && collision.gameObject.name.ToLower().Contains("puck"))
		{
			mod.SpawnPuckInFront();
			Respawn();
		}
	}

	private Vector3 ClampToRink(Vector3 pos)
	{
		if (!clampEnabled)
		{
			return pos;
		}
		float num = -25f;
		float num2 = 25f;
		float num3 = -50f;
		float num4 = 50f;
		if (playerBody != null)
		{
			Vector3 position = playerBody.position;
			if (position.x < num || position.x > num2 || position.z < num3 || position.z > num4)
			{
				return pos;
			}
		}
		float min = -22f;
		float max = 22f;
		float min2 = -43f;
		float max2 = 43f;
		pos.x = Mathf.Clamp(pos.x, min, max);
		pos.z = Mathf.Clamp(pos.z, min2, max2);
		return pos;
	}

	private void Respawn()
	{
		if (!(playerBody == null))
		{
			Vector3 forward = playerBody.transform.forward;
			forward.y = 0f;
			forward.Normalize();
			float y = Random.Range(-60f, 60f);
			float num = Random.Range(8f, 18f);
			Quaternion quaternion = Quaternion.Euler(0f, y, 0f);
			Vector3 vector = quaternion * forward;
			Vector3 vector2 = playerBody.position + vector * num;
			if (Physics.Raycast(vector2 + Vector3.up * 5f, Vector3.down, out var hitInfo, 20f))
			{
				vector2.y = hitInfo.point.y + 0.05f;
			}
			else
			{
				vector2.y = 0.05f;
			}
			vector2 = ClampToRink(vector2);
			base.transform.position = vector2;
		}
	}

	private void Update()
	{
		if (!(playerBody == null))
		{
			float num = Vector3.Distance(base.transform.position, playerBody.position);
			if (num > 25f)
			{
				Respawn();
			}
		}
	}
}
