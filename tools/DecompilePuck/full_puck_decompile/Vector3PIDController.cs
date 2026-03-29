using UnityEngine;

public class Vector3PIDController
{
	public float proportionalGain;

	public float integralGain;

	public float integralSaturation = float.MaxValue;

	public float derivativeGain;

	public float derivativeSmoothing = 1f;

	public float outputMin = float.MinValue;

	public float outputMax = float.MaxValue;

	private Vector3 errorLast = Vector3.zero;

	private Vector3 valueLast = Vector3.zero;

	private Vector3 integrationStored = Vector3.zero;

	private Vector3 derivativeLast = Vector3.zero;

	private bool derivativeInitialized;

	public DerivativeMeasurement derivativeMeasurement;

	public Vector3PIDController(float proportionalGain = 0f, float integralGain = 0f, float derivativeGain = 0f)
	{
		this.proportionalGain = proportionalGain;
		this.integralGain = integralGain;
		this.derivativeGain = derivativeGain;
	}

	public Vector3 Update(float deltaTime, Vector3 currentValue, Vector3 targetValue)
	{
		if (deltaTime <= 0f)
		{
			return Vector3.zero;
		}
		Vector3 vector = targetValue - currentValue;
		Vector3 vector2 = (vector - errorLast) / deltaTime;
		errorLast = vector;
		Vector3 vector3 = (currentValue - valueLast) / deltaTime;
		valueLast = currentValue;
		Vector3 value = integrationStored + vector * deltaTime;
		integrationStored = ClampVector3(value, 0f - integralSaturation, integralSaturation);
		Vector3 vector4 = Vector3.zero;
		if (derivativeInitialized)
		{
			vector4 = (derivativeLast = Vector3.Lerp(b: (derivativeMeasurement != DerivativeMeasurement.Velocity) ? vector2 : (-vector3), a: derivativeLast, t: derivativeSmoothing));
		}
		else
		{
			derivativeInitialized = true;
			derivativeLast = Vector3.zero;
		}
		Vector3 vector5 = proportionalGain * vector;
		Vector3 vector6 = integralGain * integrationStored;
		Vector3 vector7 = derivativeGain * vector4;
		Vector3 value2 = vector5 + vector6 + vector7;
		return ClampVector3(value2, outputMin, outputMax);
	}

	public Vector3 UpdateAngle(float deltaTime, Vector3 currentValue, Vector3 targetValue)
	{
		if (deltaTime <= 0f)
		{
			return Vector3.zero;
		}
		Vector3 vector = AngleDifference(targetValue, currentValue);
		Vector3 vector2 = AngleDifference(vector, errorLast) / deltaTime;
		errorLast = vector;
		Vector3 vector3 = AngleDifference(currentValue, valueLast) / deltaTime;
		valueLast = currentValue;
		Vector3 value = integrationStored + vector * deltaTime;
		integrationStored = ClampVector3(value, 0f - integralSaturation, integralSaturation);
		Vector3 vector4 = Vector3.zero;
		if (derivativeInitialized)
		{
			vector4 = (derivativeLast = Vector3.Lerp(b: (derivativeMeasurement != DerivativeMeasurement.Velocity) ? vector2 : (-vector3), a: derivativeLast, t: derivativeSmoothing));
		}
		else
		{
			derivativeInitialized = true;
			derivativeLast = Vector3.zero;
		}
		Vector3 vector5 = proportionalGain * vector;
		Vector3 vector6 = integralGain * integrationStored;
		Vector3 vector7 = derivativeGain * vector4;
		Vector3 value2 = vector5 + vector6 + vector7;
		return ClampVector3(value2, outputMin, outputMax);
	}

	public void Reset()
	{
		derivativeInitialized = false;
		errorLast = Vector3.zero;
		valueLast = Vector3.zero;
		integrationStored = Vector3.zero;
		derivativeLast = Vector3.zero;
	}

	private Vector3 AngleDifference(Vector3 a, Vector3 b)
	{
		return new Vector3(Mathf.DeltaAngle(b.x, a.x), Mathf.DeltaAngle(b.y, a.y), Mathf.DeltaAngle(b.z, a.z));
	}

	private Vector3 ClampVector3(Vector3 value, float min, float max)
	{
		return new Vector3(Mathf.Clamp(value.x, min, max), Mathf.Clamp(value.y, min, max), Mathf.Clamp(value.z, min, max));
	}
}
