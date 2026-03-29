using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public static class SnapshotInterpolation
{
	public static double Timescale(double drift, double catchupSpeed, double slowdownSpeed, double absoluteCatchupNegativeThreshold, double absoluteCatchupPositiveThreshold)
	{
		if (drift > absoluteCatchupPositiveThreshold)
		{
			return 1.0 + catchupSpeed;
		}
		if (drift < absoluteCatchupNegativeThreshold)
		{
			return 1.0 - slowdownSpeed;
		}
		return 1.0;
	}

	public static double DynamicAdjustment(double sendInterval, double jitterStandardDeviation, double dynamicAdjustmentTolerance)
	{
		return (sendInterval + jitterStandardDeviation) / sendInterval + dynamicAdjustmentTolerance;
	}

	public static bool InsertIfNotExists<T>(SortedList<double, T> buffer, int bufferLimit, T snapshot) where T : Snapshot
	{
		if (buffer.Count >= bufferLimit)
		{
			return false;
		}
		int count = buffer.Count;
		buffer[snapshot.remoteTime] = snapshot;
		return buffer.Count > count;
	}

	public static double TimelineClamp(double localTimeline, double bufferTime, double latestRemoteTime)
	{
		double num = latestRemoteTime - bufferTime;
		double min = num - bufferTime;
		double max = num + bufferTime;
		return Math.Clamp(localTimeline, min, max);
	}

	public static void InsertAndAdjust<T>(SortedList<double, T> buffer, int bufferLimit, T snapshot, ref double localTimeline, ref double localTimescale, float sendInterval, double bufferTime, double catchupSpeed, double slowdownSpeed, ref ExponentialMovingAverage driftEma, float catchupNegativeThreshold, float catchupPositiveThreshold, ref ExponentialMovingAverage deliveryTimeEma) where T : Snapshot
	{
		if (buffer.Count == 0)
		{
			localTimeline = snapshot.remoteTime - bufferTime;
		}
		if (InsertIfNotExists(buffer, bufferLimit, snapshot))
		{
			if (buffer.Count >= 2)
			{
				double localTime = buffer.Values[buffer.Count - 2].localTime;
				double newValue = buffer.Values[buffer.Count - 1].localTime - localTime;
				deliveryTimeEma.Add(newValue);
			}
			double remoteTime = snapshot.remoteTime;
			localTimeline = TimelineClamp(localTimeline, bufferTime, remoteTime);
			double newValue2 = remoteTime - localTimeline;
			driftEma.Add(newValue2);
			double drift = driftEma.Value - bufferTime;
			double absoluteCatchupNegativeThreshold = sendInterval * catchupNegativeThreshold;
			double absoluteCatchupPositiveThreshold = sendInterval * catchupPositiveThreshold;
			localTimescale = Timescale(drift, catchupSpeed, slowdownSpeed, absoluteCatchupNegativeThreshold, absoluteCatchupPositiveThreshold);
		}
	}

	public static void Sample<T>(SortedList<double, T> buffer, double localTimeline, out int from, out int to, out double t) where T : Snapshot
	{
		from = -1;
		to = -1;
		t = 0.0;
		for (int i = 0; i < buffer.Count - 1; i++)
		{
			T val = buffer.Values[i];
			T val2 = buffer.Values[i + 1];
			if (localTimeline >= val.remoteTime && localTimeline <= val2.remoteTime)
			{
				from = i;
				to = i + 1;
				t = Mathf.InverseLerp((float)val.remoteTime, (float)val2.remoteTime, (float)localTimeline);
				return;
			}
		}
		if (buffer.Values[0].remoteTime > localTimeline)
		{
			from = (to = 0);
			t = 0.0;
		}
		else
		{
			from = (to = buffer.Count - 1);
			t = 0.0;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void StepTime(double deltaTime, ref double localTimeline, double localTimescale)
	{
		localTimeline += deltaTime * localTimescale;
	}

	public static void StepInterpolation<T>(SortedList<double, T> buffer, double localTimeline, out T fromSnapshot, out T toSnapshot, out double t) where T : Snapshot
	{
		Sample(buffer, localTimeline, out var from, out var to, out t);
		fromSnapshot = buffer.Values[from];
		toSnapshot = buffer.Values[to];
		buffer.RemoveRange(from);
	}

	public static void Step<T>(SortedList<double, T> buffer, double deltaTime, ref double localTimeline, double localTimescale, out T fromSnapshot, out T toSnapshot, out double t) where T : Snapshot
	{
		StepTime(deltaTime, ref localTimeline, localTimescale);
		StepInterpolation(buffer, localTimeline, out fromSnapshot, out toSnapshot, out t);
	}
}
