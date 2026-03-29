using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

public class Puck : NetworkBehaviour
{
	[Header("References")]
	[SerializeField]
	private PuckElevationIndicator verticalityIndicator;

	[SerializeField]
	private SphereCollider netSphereCollider;

	[SerializeField]
	private Collider stickCollider;

	[SerializeField]
	private Collider iceCollider;

	[Space(20f)]
	[SerializeField]
	private SynchronizedAudio hitIceAudioSource;

	[SerializeField]
	private SynchronizedAudio hitBoardsAudioSource;

	[SerializeField]
	private SynchronizedAudio hitGoalPostAudioSource;

	[SerializeField]
	private SynchronizedAudio windAudioSource;

	[Header("Settings")]
	[SerializeField]
	private float maxSpeed = 30f;

	[SerializeField]
	private float maxAngularSpeed = 30f;

	[Space(20f)]
	[SerializeField]
	private Vector3 stickTensor = new Vector3(0.006f, 0.002f, 0.006f);

	[SerializeField]
	private Vector3 defaultTensor = new Vector3(0.002f, 0.002f, 0.002f);

	[Space(20f)]
	[SerializeField]
	private float groundedCheckSphereRadius = 0.075f;

	[SerializeField]
	private LayerMask groundedCheckSphereLayerMask;

	[Space(20f)]
	[SerializeField]
	private Vector3 groundedCenterOfMass = new Vector3(0f, -0.01f, 0f);

	[Space(20f)]
	[SerializeField]
	private float goalNetLinearVelocityMaximumMagnitude = 2f;

	[SerializeField]
	private float goalNetAngularVelocityMaximumMagnitude = 2f;

	[Space(20f)]
	[SerializeField]
	private AnimationCurve hitIceVolumeCurve;

	[SerializeField]
	private AnimationCurve hitIcePitchCurve;

	[SerializeField]
	private AnimationCurve hitBoardsVolumeCurve;

	[SerializeField]
	private AnimationCurve hitBoardsPitchCurve;

	[SerializeField]
	private AnimationCurve hitGoalPostVolumeCurve;

	[SerializeField]
	private AnimationCurve hitGoalPostPitchCurve;

	[SerializeField]
	private AnimationCurve windVolumeCurve;

	[SerializeField]
	private AnimationCurve windPitchCurve;

	[HideInInspector]
	public NetworkVariable<bool> IsReplay = new NetworkVariable<bool>(value: false);

	[HideInInspector]
	public Rigidbody Rigidbody;

	[HideInInspector]
	public SynchronizedObject SynchronizedObject;

	[HideInInspector]
	public NetworkObjectCollisionBuffer NetworkObjectCollisionBuffer;

	[HideInInspector]
	public CollisionRecorder CollisionRecorder;

	[HideInInspector]
	public float Speed;

	[HideInInspector]
	public float AngularSpeed;

	[HideInInspector]
	public float PredictedSpeed => SynchronizedObject.PredictedLinearVelocity.magnitude;

	[HideInInspector]
	public float PredictedAngularSpeed => SynchronizedObject.PredictedAngularVelocity.magnitude;

	[HideInInspector]
	public float ShotSpeed { get; private set; }

	[HideInInspector]
	public bool IsGrounded { get; private set; }

	[HideInInspector]
	public SphereCollider NetSphereCollider => netSphereCollider;

	[HideInInspector]
	public Collider StickCollider => stickCollider;

	[HideInInspector]
	public Collider IceCollider => iceCollider;

	[HideInInspector]
	public Stick TouchingStick { get; private set; }

	[HideInInspector]
	public bool IsTouchingStick => TouchingStick != null;

	[HideInInspector]
	public float MaxSpeed => maxSpeed;

	[HideInInspector]
	public float MaxAngularSpeed => maxAngularSpeed;

	private void Awake()
	{
		Rigidbody = GetComponent<Rigidbody>();
		SynchronizedObject = GetComponent<SynchronizedObject>();
		NetworkObjectCollisionBuffer = GetComponent<NetworkObjectCollisionBuffer>();
		CollisionRecorder = GetComponent<CollisionRecorder>();
		CollisionRecorder collisionRecorder = CollisionRecorder;
		collisionRecorder.OnDeferredCollision = (Action<GameObject, float>)Delegate.Combine(collisionRecorder.OnDeferredCollision, new Action<GameObject, float>(OnDeferredCollision));
		NetSphereCollider.enabled = false;
	}

	private void FixedUpdate()
	{
		Speed = Rigidbody.linearVelocity.magnitude;
		AngularSpeed = Rigidbody.angularVelocity.magnitude;
		IsGrounded = Physics.CheckSphere(base.transform.position, groundedCheckSphereRadius, groundedCheckSphereLayerMask);
		if (IsGrounded)
		{
			Rigidbody.centerOfMass = base.transform.TransformVector(groundedCenterOfMass);
		}
		else
		{
			Rigidbody.centerOfMass = Vector3.zero;
		}
		float num = (IsGrounded ? 0f : Mathf.Clamp(PredictedSpeed * 0.025f, 0.15f, 0.75f));
		if (NetSphereCollider.radius < num)
		{
			NetSphereCollider.radius = num;
		}
		else if (NetSphereCollider.radius > num)
		{
			NetSphereCollider.radius = Mathf.Lerp(NetSphereCollider.radius, num, Time.fixedDeltaTime * 5f);
		}
		if (IsTouchingStick)
		{
			Server_UpdateStickTensor(stickTensor, Quaternion.identity);
			TouchingStick = null;
		}
		else
		{
			Server_UpdateStickTensor(defaultTensor, Quaternion.identity);
		}
		Server_UpdateAudio();
	}

	protected override void OnNetworkPostSpawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPuckSpawned", new Dictionary<string, object> { { "puck", this } });
		if (IsReplay.Value)
		{
			Rigidbody.constraints = RigidbodyConstraints.FreezeAll;
			Rigidbody.interpolation = RigidbodyInterpolation.None;
		}
		base.OnNetworkPostSpawn();
	}

	public override void OnNetworkDespawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPuckDespawned", new Dictionary<string, object> { { "puck", this } });
		base.OnNetworkDespawn();
	}

	public override void OnDestroy()
	{
		CollisionRecorder collisionRecorder = CollisionRecorder;
		collisionRecorder.OnDeferredCollision = (Action<GameObject, float>)Delegate.Remove(collisionRecorder.OnDeferredCollision, new Action<GameObject, float>(OnDeferredCollision));
		base.transform.DOKill();
		base.OnDestroy();
	}

	public void Server_Freeze()
	{
		if (NetworkManager.Singleton.IsServer && !IsReplay.Value)
		{
			Rigidbody.constraints = RigidbodyConstraints.FreezeAll;
		}
	}

	public void Server_Unfreeze()
	{
		if (NetworkManager.Singleton.IsServer && !IsReplay.Value)
		{
			Rigidbody.constraints = RigidbodyConstraints.None;
		}
	}

	public List<KeyValuePair<Player, float>> GetPlayerCollisions()
	{
		List<KeyValuePair<Player, float>> list = NetworkObjectCollisionBuffer.Buffer.Select(delegate(NetworkObjectCollision collision)
		{
			if (collision.NetworkObjectReference.TryGet(out var networkObject))
			{
				networkObject.TryGetComponent<PlayerBodyV2>(out var component);
				networkObject.TryGetComponent<Stick>(out var component2);
				if ((bool)component)
				{
					return new KeyValuePair<Player, float>(component.Player, collision.Time);
				}
				if ((bool)component2)
				{
					return new KeyValuePair<Player, float>(component2.Player, collision.Time);
				}
			}
			return new KeyValuePair<Player, float>(null, collision.Time);
		}).ToList();
		list.RemoveAll((KeyValuePair<Player, float> collision) => collision.Key == null);
		return list;
	}

	public List<KeyValuePair<Player, float>> GetPlayerCollisionsByTeam(PlayerTeam team)
	{
		return GetPlayerCollisions().Where(delegate(KeyValuePair<Player, float> collision)
		{
			Player key = collision.Key;
			return (object)key != null && key.Team.Value == team;
		}).ToList();
	}

	private void Server_UpdateStickTensor(Vector3 inertiaTensor, Quaternion inertiaTensorRotation)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Rigidbody.inertiaTensor = inertiaTensor;
			Rigidbody.inertiaTensorRotation = inertiaTensorRotation;
		}
	}

	private void Server_UpdateAudio()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			float time = Mathf.Min(Speed / MaxSpeed, 1f);
			windAudioSource.Server_SetVolume(windVolumeCurve.Evaluate(time));
			float time2 = Mathf.Min(Speed / MaxSpeed, 1f);
			windAudioSource.Server_SetPitch(windPitchCurve.Evaluate(time2));
		}
	}

	private void OnDeferredCollision(GameObject gameObject, float force)
	{
		if (!NetworkManager.Singleton.IsServer || !gameObject)
		{
			return;
		}
		string text = LayerMask.LayerToName(gameObject.layer);
		if (!(text == "Goal Post"))
		{
			if (text == "Boards")
			{
				hitBoardsAudioSource.Server_Play(hitBoardsVolumeCurve.Evaluate(force), hitBoardsPitchCurve.Evaluate(force), isOneShot: true, -1, 0f, randomClip: true);
			}
			else
			{
				hitIceAudioSource.Server_Play(hitIceVolumeCurve.Evaluate(force), hitIcePitchCurve.Evaluate(force), isOneShot: true, -1, 0f, randomClip: true);
			}
		}
		else
		{
			hitGoalPostAudioSource.Server_Play(hitGoalPostVolumeCurve.Evaluate(force), hitGoalPostPitchCurve.Evaluate(force), isOneShot: true, -1, 0f, randomClip: true);
		}
	}

	private void OnCollisionEnter(Collision collision)
	{
		Stick component = collision.gameObject.GetComponent<Stick>();
		if ((bool)component)
		{
			TouchingStick = component;
			ShotSpeed = 0f;
		}
		if (IsGrounded)
		{
			return;
		}
		string text = LayerMask.LayerToName(collision.gameObject.layer);
		Vector3 zero = Vector3.zero;
		int num = 0;
		ContactPoint[] contacts = collision.contacts;
		foreach (ContactPoint contactPoint in contacts)
		{
			zero += contactPoint.normal;
			num++;
		}
		zero /= (float)num;
		float t = Mathf.Abs(Vector3.Dot(collision.relativeVelocity.normalized, zero.normalized));
		if (!(text == "Goal Net"))
		{
			_ = text == "Goal Post";
			return;
		}
		if (Rigidbody.linearVelocity.magnitude > goalNetLinearVelocityMaximumMagnitude)
		{
			Vector3 b = Rigidbody.linearVelocity.normalized * goalNetLinearVelocityMaximumMagnitude;
			b.y = 0f;
			Rigidbody.linearVelocity = Vector3.Lerp(Rigidbody.linearVelocity, b, t);
		}
		if (Rigidbody.angularVelocity.magnitude > goalNetAngularVelocityMaximumMagnitude)
		{
			Vector3 b2 = Rigidbody.angularVelocity.normalized * goalNetAngularVelocityMaximumMagnitude;
			Rigidbody.angularVelocity = Vector3.Lerp(Rigidbody.angularVelocity, b2, t);
		}
	}

	private void OnCollisionStay(Collision collision)
	{
		Stick component = collision.gameObject.GetComponent<Stick>();
		if ((bool)component)
		{
			TouchingStick = component;
		}
	}

	private void OnCollisionExit(Collision collision)
	{
		if ((bool)collision.gameObject.GetComponent<Stick>())
		{
			ShotSpeed = Speed;
			Rigidbody.linearVelocity = Vector3.ClampMagnitude(Rigidbody.linearVelocity, MaxSpeed);
			Rigidbody.angularVelocity = Vector3.ClampMagnitude(Rigidbody.angularVelocity, MaxAngularSpeed);
			Vector3 force = new Vector3(0f, Mathf.Min(0f, 0f - Rigidbody.linearVelocity.y), 0f) * 5f;
			Rigidbody.AddForce(force, ForceMode.Acceleration);
		}
	}

	public void OnDrawGizmos()
	{
		if (Application.isEditor)
		{
			Gizmos.color = Color.black;
			Gizmos.DrawWireSphere(base.transform.position, groundedCheckSphereRadius);
		}
	}

	protected override void __initializeVariables()
	{
		if (IsReplay == null)
		{
			throw new Exception("Puck.IsReplay cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		IsReplay.Initialize(this);
		__nameNetworkVariable(IsReplay, "IsReplay");
		NetworkVariableFields.Add(IsReplay);
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		base.__initializeRpcs();
	}

	protected internal override string __getTypeName()
	{
		return "Puck";
	}
}
