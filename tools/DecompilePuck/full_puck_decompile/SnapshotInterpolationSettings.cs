using System;
using UnityEngine;

[Serializable]
public class SnapshotInterpolationSettings
{
	[Header("Buffering")]
	[Tooltip("Local simulation is behind by sendInterval * multiplier seconds.\n\nThis guarantees that we always have enough snapshots in the buffer to mitigate lags & jitter.\n\nIncrease this if the simulation isn't smooth. By default, it should be around 2.")]
	public double bufferTimeMultiplier = 2.0;

	[Tooltip("If a client can't process snapshots fast enough, don't store too many.")]
	public int bufferLimit = 32;

	[Header("Catchup / Slowdown")]
	[Tooltip("Slowdown begins when the local timeline is moving too fast towards remote time. Threshold is in frames worth of snapshots.\n\nThis needs to be negative.\n\nDon't modify unless you know what you are doing.")]
	public float catchupNegativeThreshold = -1f;

	[Tooltip("Catchup begins when the local timeline is moving too slow and getting too far away from remote time. Threshold is in frames worth of snapshots.\n\nThis needs to be positive.\n\nDon't modify unless you know what you are doing.")]
	public float catchupPositiveThreshold = 1f;

	[Tooltip("Local timeline acceleration in % while catching up.")]
	[Range(0f, 1f)]
	public double catchupSpeed = 0.019999999552965164;

	[Tooltip("Local timeline slowdown in % while slowing down.")]
	[Range(0f, 1f)]
	public double slowdownSpeed = 0.03999999910593033;

	[Tooltip("Catchup/Slowdown is adjusted over n-second exponential moving average.")]
	public int driftEmaDuration = 1;

	[Header("Dynamic Adjustment")]
	[Tooltip("Automatically adjust bufferTimeMultiplier for smooth results.\nSets a low multiplier on stable connections, and a high multiplier on jittery connections.")]
	public bool dynamicAdjustment = true;

	[Tooltip("Safety buffer that is always added to the dynamic bufferTimeMultiplier adjustment.")]
	public float dynamicAdjustmentTolerance = 1f;

	[Tooltip("Dynamic adjustment is computed over n-second exponential moving average standard deviation.")]
	public int deliveryTimeEmaDuration = 2;
}
