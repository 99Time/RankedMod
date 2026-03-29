using System;

public class PlayerData
{
	public string steamId { get; set; }

	public string username { get; set; } = "PLAYER";

	public int number { get; set; } = new Random().Next(0, 100);

	public double lastUsernameChange { get; set; }

	public int patreonLevel { get; set; }

	public int adminLevel { get; set; }

	public PlayerItem[] items { get; set; } = new PlayerItem[0];

	public PlayerMute[] mutes { get; set; } = new PlayerMute[0];

	public PlayerBan[] bans { get; set; } = new PlayerBan[0];
}
