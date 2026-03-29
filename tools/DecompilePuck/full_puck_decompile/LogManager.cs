using System;
using System.IO;
using System.Text;
using UnityEngine;

public class LogManager : MonoBehaviourSingleton<LogManager>
{
	private StreamWriter streamWriter;

	public string LogsPath => Path.Combine(Path.GetFullPath("."), "Logs");

	public override void Awake()
	{
		base.Awake();
		if (!Directory.Exists(LogsPath))
		{
			Directory.CreateDirectory(LogsPath);
		}
		string path = Path.Combine(LogsPath, "Puck.log");
		streamWriter = new StreamWriter(path, append: false, Encoding.UTF8);
		streamWriter.AutoFlush = true;
		Application.logMessageReceived += OnLogMessageReceived;
	}

	private void OnDestroy()
	{
		Application.logMessageReceived -= OnLogMessageReceived;
		if (streamWriter != null)
		{
			streamWriter.Close();
			streamWriter = null;
		}
	}

	private void OnLogMessageReceived(string message, string stackTrace, LogType type)
	{
		if (streamWriter != null)
		{
			streamWriter.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}");
		}
	}
}
