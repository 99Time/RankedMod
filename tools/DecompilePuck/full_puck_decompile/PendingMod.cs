public class PendingMod
{
	public ulong Id;

	public Mod Mod;

	public PendingMod(ulong id, Mod mod = null)
	{
		Id = id;
		Mod = mod;
	}
}
