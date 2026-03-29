using System;
using UnityEngine;

[Serializable]
internal struct AppearanceItem
{
	public int Id;

	public string Name;

	public Texture Image;

	public bool IsTwoTone;

	public Texture BlueImage;

	public Texture RedImage;

	public bool Purchaseable;

	public string Price;

	public bool Hidden;
}
