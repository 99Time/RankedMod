using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Codebase.Configs;
using Unity.Collections;
using Unity.Netcode;

namespace Codebase;

public static class NetworkCommunication
{
	private static readonly List<string> DataNamesToIgnore = new List<string>();

	public static void AddToNotLogList(ICollection<string> dataNamesToNotLog)
	{
		DataNamesToIgnore.AddRange(dataNamesToNotLog);
	}

	public static void RemoveFromNotLogList(ICollection<string> dataNamesToNotLog)
	{
		foreach (string item in dataNamesToNotLog)
		{
			DataNamesToIgnore.Remove(item);
		}
	}

	public static List<string> GetDataNamesToIgnore()
	{
		return new List<string>(DataNamesToIgnore);
	}

	public static void SendData(string dataName, string dataStr, ulong clientId, string listener, IConfig config, NetworkDelivery networkDelivery = (NetworkDelivery)4)
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			byte[] bytes = Encoding.UTF8.GetBytes(dataStr);
			int num = Encoding.UTF8.GetByteCount(dataName) + 8 + bytes.Length;
			FastBufferWriter val = default(FastBufferWriter);
			((FastBufferWriter)(ref val))._002Ector(num, (Allocator)3, -1);
			((FastBufferWriter)(ref val)).WriteValue(dataName, false);
			((FastBufferWriter)(ref val)).WriteBytes(bytes, -1, 0);
			NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(listener, clientId, val, networkDelivery);
			((FastBufferWriter)(ref val)).Dispose();
			if (!DataNamesToIgnore.Any((string x) => dataName.StartsWith(x)))
			{
				Logging.Log($"Sent data \"{dataName}\" ({bytes.Length} bytes - {num} total bytes) to {clientId} with listener {listener}.", config);
			}
		}
		catch (Exception arg)
		{
			Logging.LogError($"Error when writing streamed data: {arg}", config);
		}
	}

	public static void SendDataToAll(string dataName, string dataStr, string listener, IConfig config, NetworkDelivery networkDelivery = (NetworkDelivery)4)
	{
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			byte[] bytes = Encoding.UTF8.GetBytes(dataStr);
			int num = Encoding.UTF8.GetByteCount(dataName) + 8 + bytes.Length;
			FastBufferWriter val = default(FastBufferWriter);
			((FastBufferWriter)(ref val))._002Ector(num, (Allocator)3, -1);
			((FastBufferWriter)(ref val)).WriteValue(dataName, false);
			((FastBufferWriter)(ref val)).WriteBytes(bytes, -1, 0);
			NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(listener, val, networkDelivery);
			((FastBufferWriter)(ref val)).Dispose();
			if (!DataNamesToIgnore.Any((string x) => dataName.StartsWith(x)))
			{
				Logging.Log($"Sent data \"{dataName}\" ({bytes.Length} bytes - {num} total bytes) to all clients with listener {listener}.", config);
			}
		}
		catch (Exception arg)
		{
			Logging.LogError($"Error when writing streamed data: {arg}", config);
		}
	}

	public static (string DataName, string DataStr) GetData(ulong clientId, FastBufferReader reader, IConfig config)
	{
		string dataName = "?";
		try
		{
			((FastBufferReader)(ref reader)).ReadValue(ref dataName, false);
			int num = ((FastBufferReader)(ref reader)).Length - ((FastBufferReader)(ref reader)).Position;
			int num2 = num + 8 + Encoding.UTF8.GetByteCount(dataName);
			byte[] array = new byte[num];
			for (int i = 0; i < num; i++)
			{
				((FastBufferReader)(ref reader)).ReadByte(ref array[i]);
			}
			string text = Encoding.UTF8.GetString(array).Trim();
			dataName = dataName.Trim();
			if (!DataNamesToIgnore.Any((string x) => dataName.StartsWith(x)))
			{
				Logging.Log(string.Format("Received data {0} ({1} bytes - {2} total bytes) from {3}. Content : {4}", dataName, num, num2, (clientId == 0L) ? "server" : clientId.ToString(), text), config);
			}
			return (DataName: dataName, DataStr: text);
		}
		catch (Exception arg)
		{
			Logging.LogError($"Error when reading streamed data \"{dataName}\": {arg}", config);
		}
		return (DataName: "", DataStr: "");
	}
}
