using UnityEngine;
using UnityEngine.Splines;

public class MoveAlongSpline : MonoBehaviour
{
	[Header("References")]
	public SplineContainer spline;

	[Header("Settings")]
	public float speed = 1f;

	private float splinePosition;

	private void Update()
	{
		splinePosition += Time.deltaTime * speed;
		if (splinePosition >= 1f)
		{
			splinePosition = 0f;
		}
		Vector3 position = spline.EvaluatePosition(splinePosition);
		base.transform.position = position;
		base.transform.LookAt(Vector3.zero);
	}
}
