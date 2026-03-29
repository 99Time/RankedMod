using Unity.Collections;
using UnityEngine;

public struct ReplayStickSpawned
{
	public ulong OwnerClientId;

	public Vector3 Position;

	public Quaternion Rotation;

	public FixedString32Bytes StickAttackerBlueSkin;

	public FixedString32Bytes StickAttackerRedSkin;

	public FixedString32Bytes StickGoalieBlueSkin;

	public FixedString32Bytes StickGoalieRedSkin;

	public FixedString32Bytes StickShaftAttackerBlueTapeSkin;

	public FixedString32Bytes StickShaftAttackerRedTapeSkin;

	public FixedString32Bytes StickShaftGoalieBlueTapeSkin;

	public FixedString32Bytes StickShaftGoalieRedTapeSkin;

	public FixedString32Bytes StickBladeAttackerBlueTapeSkin;

	public FixedString32Bytes StickBladeAttackerRedTapeSkin;

	public FixedString32Bytes StickBladeGoalieBlueTapeSkin;

	public FixedString32Bytes StickBladeGoalieRedTapeSkin;
}
