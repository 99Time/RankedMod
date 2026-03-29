using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class UDPSocket
{
	public Action<ushort> OnSocketStarted;

	public Action<ushort> OnSocketFailed;

	public Action OnSocketStopped;

	public Action<string, ushort, string, long> OnUdpMessageReceived;

	public Action<string, ushort, string, long> OnUdpMessageSent;

	private UdpClient udpClient;

	public void StartSocket(ushort port)
	{
		Listen(port);
	}

	public void StopSocket()
	{
		if (udpClient != null && udpClient.Client != null && udpClient.Client.IsBound)
		{
			udpClient.Close();
			MonoBehaviourSingleton<ThreadManager>.Instance.Enqueue(delegate
			{
				OnSocketStopped?.Invoke();
			});
		}
	}

	public void SendCallback(IAsyncResult asyncResult)
	{
		Dictionary<string, object> dictionary = (Dictionary<string, object>)asyncResult.AsyncState;
		string ipAddress = (string)dictionary["ipAddress"];
		ushort port = (ushort)dictionary["port"];
		string message = (string)dictionary["message"];
		try
		{
			udpClient.EndSend(asyncResult);
			long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
			MonoBehaviourSingleton<ThreadManager>.Instance.Enqueue(delegate
			{
				OnUdpMessageSent?.Invoke(ipAddress, port, message, timestamp);
			});
		}
		catch (ObjectDisposedException)
		{
			Debug.Log("[UDPSocket] Cancelled send UDP message due to socket closure");
		}
		catch (Exception arg)
		{
			Debug.Log($"[UDPSocket] Failed to end send UDP message: {arg}");
		}
	}

	public void ReceiveCallback(IAsyncResult asyncResult)
	{
		IPEndPoint ipEndPoint = null;
		try
		{
			udpClient.BeginReceive(ReceiveCallback, null);
			byte[] bytes = udpClient.EndReceive(asyncResult, ref ipEndPoint);
			string message = Encoding.ASCII.GetString(bytes);
			long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
			MonoBehaviourSingleton<ThreadManager>.Instance.Enqueue(delegate
			{
				OnUdpMessageReceived?.Invoke(ipEndPoint.Address.ToString(), (ushort)ipEndPoint.Port, message, timestamp);
			});
		}
		catch (ObjectDisposedException)
		{
			Debug.Log("[UDPSocket] Cancelled receive UDP message due to socket closure");
		}
		catch (Exception arg)
		{
			Debug.Log($"[UDPSocket] Failed to receive UDP message: {arg}");
		}
	}

	private void Listen(ushort port)
	{
		try
		{
			IPEndPoint localEP = new IPEndPoint(IPAddress.Any, port);
			udpClient = new UdpClient(localEP);
			udpClient.BeginReceive(ReceiveCallback, null);
			MonoBehaviourSingleton<ThreadManager>.Instance.Enqueue(delegate
			{
				OnSocketStarted?.Invoke(port);
			});
		}
		catch (Exception arg)
		{
			Debug.Log($"[UDPSocket] Failed to open UDP port: {arg}");
			MonoBehaviourSingleton<ThreadManager>.Instance.Enqueue(delegate
			{
				OnSocketFailed?.Invoke(port);
			});
		}
	}

	public void Send(string ipAddress, ushort port, string message)
	{
		try
		{
			byte[] bytes = Encoding.ASCII.GetBytes(message);
			udpClient.BeginSend(bytes, bytes.Length, ipAddress, port, SendCallback, new Dictionary<string, object>
			{
				{ "ipAddress", ipAddress },
				{ "port", port },
				{ "message", message }
			});
		}
		catch (Exception arg)
		{
			Debug.Log($"[UDPSocket] Failed to start send UDP message: {arg}");
		}
	}
}
