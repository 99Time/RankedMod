public class ServerBrowserServer
{
	public string ipAddress { get; set; }

	public ushort port { get; set; }

	public ushort pingPort { get; set; }

	public string name { get; set; }

	public int maxPlayers { get; set; }

	public bool isPasswordProtected { get; set; }

	public int players { get; set; }
}
