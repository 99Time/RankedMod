using Unity.Collections;
using UnityEngine;

public struct ReplayPlayerBodySpawned
{
	public ulong OwnerClientId;

	public Vector3 Position;

	public Quaternion Rotation;

	public FixedString32Bytes Username;

	public int Number;

	public PlayerTeam Team;

	public PlayerRole Role;

	public FixedString32Bytes Country;

	public FixedString32Bytes VisorAttackerBlueSkin;

	public FixedString32Bytes VisorAttackerRedSkin;

	public FixedString32Bytes VisorGoalieBlueSkin;

	public FixedString32Bytes VisorGoalieRedSkin;

	public FixedString32Bytes Mustache;

	public FixedString32Bytes Beard;

	public FixedString32Bytes JerseyAttackerBlueSkin;

	public FixedString32Bytes JerseyAttackerRedSkin;

	public FixedString32Bytes JerseyGoalieBlueSkin;

	public FixedString32Bytes JerseyGoalieRedSkin;
}
