using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CollisionRecorder : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float deferTime = 0.1f;

	[HideInInspector]
	public Action<GameObject, float> OnDeferredCollision;

	[HideInInspector]
	public Rigidbody Rigidbody;

	private bool recording;

	private Dictionary<GameObject, float> collisionGameObjectForceMap = new Dictionary<GameObject, float>();

	private IEnumerator deferCollisionCoroutine;

	private void Awake()
	{
		Rigidbody = GetComponent<Rigidbody>();
	}

	private void OnDestroy()
	{
		StopDeferringCollision();
	}

	private void StartDeferringCollision()
	{
		StopDeferringCollision();
		deferCollisionCoroutine = IDeferCollision(deferTime);
		StartCoroutine(deferCollisionCoroutine);
	}

	private void StopDeferringCollision()
	{
		if (deferCollisionCoroutine != null)
		{
			StopCoroutine(deferCollisionCoroutine);
		}
	}

	private IEnumerator IDeferCollision(float duration)
	{
		yield return new WaitForSeconds(duration);
		KeyValuePair<GameObject, float> keyValuePair = collisionGameObjectForceMap.OrderByDescending((KeyValuePair<GameObject, float> x) => x.Value).FirstOrDefault();
		if ((bool)keyValuePair.Key)
		{
			OnDeferredCollision?.Invoke(keyValuePair.Key, keyValuePair.Value);
		}
		recording = false;
		collisionGameObjectForceMap.Clear();
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (!recording)
		{
			recording = true;
			StartDeferringCollision();
		}
		float collisionForce = Utils.GetCollisionForce(collision);
		GameObject gameObject = collision.collider.gameObject;
		if ((bool)gameObject)
		{
			if (collisionGameObjectForceMap.ContainsKey(gameObject))
			{
				collisionGameObjectForceMap[gameObject] = Mathf.Max(collisionGameObjectForceMap[gameObject], collisionForce);
			}
			else
			{
				collisionGameObjectForceMap.Add(gameObject, collisionForce);
			}
		}
	}
}
