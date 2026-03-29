using UnityEngine;

public class ReplayCamera : BaseCamera
{
	[Header("Settings")]
	[SerializeField]
	private float rotationSpeed = 10f;

	private Transform target;

	public void SetTarget(Transform target)
	{
		this.target = target;
	}

	public override void OnTick(float deltaTime)
	{
		base.OnTick(deltaTime);
		if ((bool)target)
		{
			base.transform.rotation = Quaternion.Lerp(base.transform.rotation, Quaternion.LookRotation(target.position - base.transform.position), deltaTime * rotationSpeed);
		}
	}

	protected override void __initializeVariables()
	{
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		base.__initializeRpcs();
	}

	protected internal override string __getTypeName()
	{
		return "ReplayCamera";
	}
}
