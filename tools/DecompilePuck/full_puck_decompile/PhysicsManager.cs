using UnityEngine;

public class PhysicsManager : MonoBehaviourSingleton<PhysicsManager>
{
	[Header("Settings")]
	[SerializeField]
	private SimulationMode simulationMode = SimulationMode.Script;

	[SerializeField]
	private int tickRate = 50;

	private float tickAccumulator;

	[HideInInspector]
	public int TickRate => tickRate;

	[HideInInspector]
	public float TickInterval => 1f / (float)TickRate;

	public override void Awake()
	{
		base.Awake();
		Physics.simulationMode = simulationMode;
	}

	private void Update()
	{
		if (simulationMode == SimulationMode.Script)
		{
			tickAccumulator += Time.deltaTime;
			if (tickAccumulator >= TickInterval)
			{
				Time.fixedDeltaTime = TickInterval;
				Physics.Simulate(Time.fixedDeltaTime);
				tickAccumulator -= TickInterval;
			}
		}
	}
}
