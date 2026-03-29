using System.Collections.Generic;

public class ServerConfiguration
{
	public ushort port { get; set; } = 7777;

	public ushort pingPort { get; set; } = 7778;

	public string name { get; set; } = "MY PUCK SERVER";

	public int maxPlayers { get; set; } = 10;

	public string password { get; set; } = "";

	public bool voip { get; set; }

	public bool isPublic { get; set; } = true;

	public string[] adminSteamIds { get; set; } = new string[0];

	public bool reloadBannedSteamIds { get; set; }

	public bool usePuckBannedSteamIds { get; set; } = true;

	public bool printMetrics { get; set; } = true;

	public float kickTimeout { get; set; } = 300f;

	public float sleepTimeout { get; set; } = 60f;

	public float joinMidMatchDelay { get; set; } = 10f;

	public int targetFrameRate { get; set; } = 120;

	public int serverTickRate { get; set; } = 100;

	public int clientTickRate { get; set; } = 200;

	public bool startPaused { get; set; }

	public bool allowVoting { get; set; } = true;

	public Dictionary<GamePhase, int> phaseDurationMap { get; set; } = new Dictionary<GamePhase, int>
	{
		{
			GamePhase.Warmup,
			600
		},
		{
			GamePhase.FaceOff,
			3
		},
		{
			GamePhase.Playing,
			300
		},
		{
			GamePhase.BlueScore,
			5
		},
		{
			GamePhase.RedScore,
			5
		},
		{
			GamePhase.Replay,
			10
		},
		{
			GamePhase.PeriodOver,
			15
		},
		{
			GamePhase.GameOver,
			15
		}
	};

	public ModConfiguration[] mods { get; set; } = new ModConfiguration[0];
}
