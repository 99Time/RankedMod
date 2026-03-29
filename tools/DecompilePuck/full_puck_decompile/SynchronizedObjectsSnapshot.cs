using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct SynchronizedObjectsSnapshot : Snapshot
{
	public double remoteTime { get; set; }

	public double localTime { get; set; }

	public List<SynchronizedObjectSnapshot> snapshots { get; set; }

	public SynchronizedObjectsSnapshot(double remoteTime, double localTime, List<SynchronizedObjectSnapshot> snapshots)
	{
		this.remoteTime = remoteTime;
		this.localTime = localTime;
		this.snapshots = snapshots;
	}

	public static void Interpolate(SynchronizedObjectsSnapshot from, SynchronizedObjectsSnapshot to, double t)
	{
		new List<SynchronizedObjectSnapshot>();
		foreach (SynchronizedObjectSnapshot toSnapshot in to.snapshots)
		{
			SynchronizedObjectSnapshot synchronizedObjectSnapshot = from.snapshots.FirstOrDefault((SynchronizedObjectSnapshot snapshot) => snapshot.SynchronizedObject == toSnapshot.SynchronizedObject);
			if ((synchronizedObjectSnapshot != null && !(synchronizedObjectSnapshot.SynchronizedObject == null)) || (toSnapshot != null && !(toSnapshot.SynchronizedObject == null)))
			{
				if (synchronizedObjectSnapshot == null || synchronizedObjectSnapshot.SynchronizedObject == null)
				{
					toSnapshot.SynchronizedObject.transform.position = toSnapshot.Position;
					toSnapshot.SynchronizedObject.transform.rotation = toSnapshot.Rotation;
					toSnapshot.SynchronizedObject.PredictedLinearVelocity = toSnapshot.LinearVelocity;
					toSnapshot.SynchronizedObject.PredictedAngularVelocity = toSnapshot.AngularVelocity;
				}
				else if (toSnapshot == null || toSnapshot.SynchronizedObject == null)
				{
					synchronizedObjectSnapshot.SynchronizedObject.transform.position = toSnapshot.Position;
					synchronizedObjectSnapshot.SynchronizedObject.transform.rotation = toSnapshot.Rotation;
					synchronizedObjectSnapshot.SynchronizedObject.PredictedLinearVelocity = toSnapshot.LinearVelocity;
					synchronizedObjectSnapshot.SynchronizedObject.PredictedAngularVelocity = toSnapshot.AngularVelocity;
				}
				else
				{
					toSnapshot.SynchronizedObject.transform.position = Vector3.LerpUnclamped(synchronizedObjectSnapshot.Position, toSnapshot.Position, (float)t);
					toSnapshot.SynchronizedObject.transform.rotation = Quaternion.SlerpUnclamped(synchronizedObjectSnapshot.Rotation, toSnapshot.Rotation, (float)t);
					toSnapshot.SynchronizedObject.PredictedLinearVelocity = Vector3.LerpUnclamped(synchronizedObjectSnapshot.LinearVelocity, toSnapshot.LinearVelocity, (float)t);
					toSnapshot.SynchronizedObject.PredictedAngularVelocity = Vector3.LerpUnclamped(synchronizedObjectSnapshot.AngularVelocity, toSnapshot.AngularVelocity, (float)t);
				}
			}
		}
	}
}
