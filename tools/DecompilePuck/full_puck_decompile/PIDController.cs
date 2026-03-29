using UnityEngine;

public class PIDController
{
	public float proportionalGain;

	public float integralGain;

	public float integralSaturation = float.MaxValue;

	public float derivativeGain;

	public float derivativeSmoothing = 1f;

	public float outputMin = float.MinValue;

	public float outputMax = float.MaxValue;

	private float errorLast;

	private float valueLast;

	private float integrationStored;

	private float derivativeLast;

	private bool derivativeInitialized;

	public DerivativeMeasurement derivativeMeasurement;

	public PIDController(float proportionalGain = 0f, float integralGain = 0f, float derivativeGain = 0f)
	{
		this.proportionalGain = proportionalGain;
		this.integralGain = integralGain;
		this.derivativeGain = derivativeGain;
	}

	public float Update(float deltaTime, float currentValue, float targetValue)
	{
		if (deltaTime <= 0f)
		{
			return 0f;
		}
		float num = targetValue - currentValue;
		float num2 = (num - errorLast) / deltaTime;
		errorLast = num;
		float num3 = (currentValue - valueLast) / deltaTime;
		valueLast = currentValue;
		float value = integrationStored + num * deltaTime;
		integrationStored = Mathf.Clamp(value, 0f - integralSaturation, integralSaturation);
		float num4 = 0f;
		if (derivativeInitialized)
		{
			num4 = (derivativeLast = Mathf.Lerp(b: (derivativeMeasurement != DerivativeMeasurement.Velocity) ? num2 : (0f - num3), a: derivativeLast, t: derivativeSmoothing));
		}
		else
		{
			derivativeInitialized = true;
			derivativeLast = 0f;
		}
		float num5 = proportionalGain * num;
		float num6 = integralGain * integrationStored;
		float num7 = derivativeGain * num4;
		return Mathf.Clamp(num5 + num6 + num7, outputMin, outputMax);
	}

	public float UpdateAngle(float deltaTime, float currentValue, float targetValue)
	{
		if (deltaTime <= 0f)
		{
			return 0f;
		}
		float num = AngleDifference(targetValue, currentValue);
		float num2 = AngleDifference(num, errorLast) / deltaTime;
		errorLast = num;
		float num3 = AngleDifference(currentValue, valueLast) / deltaTime;
		valueLast = currentValue;
		float value = integrationStored + num * deltaTime;
		integrationStored = Mathf.Clamp(value, 0f - integralSaturation, integralSaturation);
		float num4 = 0f;
		if (derivativeInitialized)
		{
			num4 = (derivativeLast = Mathf.Lerp(b: (derivativeMeasurement != DerivativeMeasurement.Velocity) ? num2 : (0f - num3), a: derivativeLast, t: derivativeSmoothing));
		}
		else
		{
			derivativeInitialized = true;
			derivativeLast = 0f;
		}
		float num5 = proportionalGain * num;
		float num6 = integralGain * integrationStored;
		float num7 = derivativeGain * num4;
		return Mathf.Clamp(num5 + num6 + num7, outputMin, outputMax);
	}

	public void Reset()
	{
		derivativeInitialized = false;
		errorLast = 0f;
		valueLast = 0f;
		integrationStored = 0f;
		derivativeLast = 0f;
	}

	private float AngleDifference(float a, float b)
	{
		return Mathf.DeltaAngle(b, a);
	}
}
