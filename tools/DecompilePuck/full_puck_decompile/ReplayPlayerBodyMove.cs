using UnityEngine;

public struct ReplayPlayerBodyMove
{
	public ulong OwnerClientId;

	public Vector3 Position;

	public Quaternion Rotation;

	public short Stamina;

	public short Speed;

	public bool IsSprinting;

	public bool IsSliding;

	public bool IsStopping;

	public bool IsExtendedLeft;

	public bool IsExtendedRight;
}
