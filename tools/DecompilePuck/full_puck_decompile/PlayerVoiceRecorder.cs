using System.Collections;
using System.Collections.Generic;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

public class PlayerVoiceRecorder : NetworkBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float recordingStopDelay = 1f;

	[HideInInspector]
	public Player Player;

	[HideInInspector]
	public bool IsEnabled;

	[HideInInspector]
	public bool IsRecording;

	private int bufferSize;

	private float[] buffer;

	private int playbackBuffer;

	private int dataPosition;

	private int dataReceived;

	private uint sampleRate;

	private IEnumerator delayedStopRecordingCoroutine;

	private void Awake()
	{
		Player = GetComponent<Player>();
	}

	private void Update()
	{
		if (IsEnabled && (bool)Player && base.IsOwner && IsRecording && !MonoBehaviourSingleton<StateManager>.Instance.IsMuted)
		{
			SendVoiceData();
		}
	}

	public override void OnDestroy()
	{
		StopDelayedStopRecordingCoroutine();
		if (IsRecording)
		{
			SteamUser.StopVoiceRecording();
		}
		base.OnDestroy();
	}

	private void SendVoiceData()
	{
		SteamUser.GetAvailableVoice(out var pcbCompressed);
		if (pcbCompressed != 0)
		{
			byte[] array = new byte[pcbCompressed];
			SteamUser.GetVoice(bWantCompressed: true, array, pcbCompressed, out var nBytesWritten);
			if (nBytesWritten != 0)
			{
				Client_VoiceDataRpc(array);
			}
		}
	}

	private AudioClip InitializeAudioClip(int sampleRate)
	{
		playbackBuffer = 0;
		dataPosition = 0;
		dataReceived = 0;
		bufferSize = sampleRate * 2;
		buffer = new float[bufferSize];
		return AudioClip.Create("VoiceData", sampleRate, 1, sampleRate, stream: true, OnAudioRead, null);
	}

	private void OnAudioRead(float[] data)
	{
		for (int i = 0; i < data.Length; i++)
		{
			data[i] = 0f;
			if (playbackBuffer > 0)
			{
				dataPosition++;
				playbackBuffer--;
				data[i] = buffer[dataPosition % bufferSize];
			}
		}
	}

	private void WriteToClip(byte[] decompressed, int bytesWritten)
	{
		for (int i = 0; i < bytesWritten - 1 && i + 1 < decompressed.Length; i += 2)
		{
			float f = (float)(short)(decompressed[i] | (decompressed[i + 1] << 8)) / 32768f;
			WriteToClip(f);
		}
	}

	private void WriteToClip(float f)
	{
		buffer[dataReceived % bufferSize] = f;
		dataReceived++;
		playbackBuffer++;
	}

	public void StartRecording()
	{
		if (IsEnabled && !MonoBehaviourSingleton<StateManager>.Instance.IsMuted)
		{
			if (IsRecording)
			{
				StopDelayedStopRecordingCoroutine();
				return;
			}
			IsRecording = true;
			sampleRate = SteamUser.GetVoiceOptimalSampleRate();
			SteamUser.StartVoiceRecording();
			Client_VoiceStartRpc(sampleRate);
			Debug.Log($"[PlayerVoiceRecorder] Starting Steam voice recording at {sampleRate}Hz");
		}
	}

	public void StopRecording()
	{
		if (IsEnabled && !MonoBehaviourSingleton<StateManager>.Instance.IsMuted && IsRecording)
		{
			StartDelayedStopRecordingCoroutine();
		}
	}

	private void StartDelayedStopRecordingCoroutine()
	{
		StopDelayedStopRecordingCoroutine();
		delayedStopRecordingCoroutine = IDelayedStopRecording(recordingStopDelay);
		StartCoroutine(delayedStopRecordingCoroutine);
	}

	private void StopDelayedStopRecordingCoroutine()
	{
		if (delayedStopRecordingCoroutine != null)
		{
			StopCoroutine(delayedStopRecordingCoroutine);
		}
	}

	private IEnumerator IDelayedStopRecording(float delay)
	{
		Debug.Log($"[PlayerVoiceRecorder] Stopping Steam voice recording in {delay} seconds");
		yield return new WaitForSeconds(delay);
		IsRecording = false;
		SteamUser.StopVoiceRecording();
		Client_VoiceStopRpc();
		Debug.Log("[PlayerVoiceRecorder] Steam voice recording stopped");
	}

	[Rpc(SendTo.Server)]
	public void Client_VoiceStartRpc(uint sampleRate)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
			RpcParams rpcParams = default(RpcParams);
			FastBufferWriter bufferWriter = __beginSendRpc(2598611224u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, sampleRate);
			__endSendRpc(ref bufferWriter, 2598611224u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (IsEnabled)
			{
				Server_VoiceStartRpc(sampleRate);
			}
		}
	}

	[Rpc(SendTo.ClientsAndHost)]
	public void Server_VoiceStartRpc(uint sampleRate)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(236455107u, rpcParams, attributeParams, SendTo.ClientsAndHost, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sampleRate);
				__endSendRpc(ref bufferWriter, 236455107u, rpcParams, attributeParams, SendTo.ClientsAndHost, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				this.sampleRate = sampleRate;
				AudioClip value = InitializeAudioClip((int)sampleRate);
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerVoiceStarted", new Dictionary<string, object>
				{
					{ "player", Player },
					{ "audioClip", value }
				});
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_VoiceDataRpc(byte[] voiceData)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
			RpcParams rpcParams = default(RpcParams);
			FastBufferWriter bufferWriter = __beginSendRpc(3881473849u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bool value = voiceData != null;
			bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			if (value)
			{
				bufferWriter.WriteValueSafe(voiceData, default(FastBufferWriter.ForPrimitives));
			}
			__endSendRpc(ref bufferWriter, 3881473849u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (IsEnabled)
			{
				Server_VoiceDataRpc(voiceData);
			}
		}
	}

	[Rpc(SendTo.ClientsAndHost)]
	public void Server_VoiceDataRpc(byte[] voiceData)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
			RpcParams rpcParams = default(RpcParams);
			FastBufferWriter bufferWriter = __beginSendRpc(2054004997u, rpcParams, attributeParams, SendTo.ClientsAndHost, RpcDelivery.Reliable);
			bool value = voiceData != null;
			bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			if (value)
			{
				bufferWriter.WriteValueSafe(voiceData, default(FastBufferWriter.ForPrimitives));
			}
			__endSendRpc(ref bufferWriter, 2054004997u, rpcParams, attributeParams, SendTo.ClientsAndHost, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			byte[] array = new byte[sampleRate * 5];
			SteamUser.DecompressVoice(voiceData, (uint)voiceData.Length, array, (uint)array.Length, out var nBytesWritten, sampleRate);
			if (nBytesWritten != 0)
			{
				WriteToClip(array, (int)nBytesWritten);
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_VoiceStopRpc()
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
			RpcParams rpcParams = default(RpcParams);
			FastBufferWriter bufferWriter = __beginSendRpc(1645882530u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 1645882530u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (IsEnabled)
			{
				Server_VoiceStopRpc();
			}
		}
	}

	[Rpc(SendTo.ClientsAndHost)]
	public void Server_VoiceStopRpc()
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(824274507u, rpcParams, attributeParams, SendTo.ClientsAndHost, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 824274507u, rpcParams, attributeParams, SendTo.ClientsAndHost, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerVoiceStopped", new Dictionary<string, object> { { "player", Player } });
			}
		}
	}

	protected override void __initializeVariables()
	{
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		__registerRpc(2598611224u, __rpc_handler_2598611224, "Client_VoiceStartRpc");
		__registerRpc(236455107u, __rpc_handler_236455107, "Server_VoiceStartRpc");
		__registerRpc(3881473849u, __rpc_handler_3881473849, "Client_VoiceDataRpc");
		__registerRpc(2054004997u, __rpc_handler_2054004997, "Server_VoiceDataRpc");
		__registerRpc(1645882530u, __rpc_handler_1645882530, "Client_VoiceStopRpc");
		__registerRpc(824274507u, __rpc_handler_824274507, "Server_VoiceStopRpc");
		base.__initializeRpcs();
	}

	private static void __rpc_handler_2598611224(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out uint value);
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerVoiceRecorder)target).Client_VoiceStartRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_236455107(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out uint value);
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerVoiceRecorder)target).Server_VoiceStartRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3881473849(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			byte[] value2 = null;
			if (value)
			{
				reader.ReadValueSafe(out value2, default(FastBufferWriter.ForPrimitives));
			}
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerVoiceRecorder)target).Client_VoiceDataRpc(value2);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2054004997(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			byte[] value2 = null;
			if (value)
			{
				reader.ReadValueSafe(out value2, default(FastBufferWriter.ForPrimitives));
			}
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerVoiceRecorder)target).Server_VoiceDataRpc(value2);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1645882530(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerVoiceRecorder)target).Client_VoiceStopRpc();
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_824274507(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerVoiceRecorder)target).Server_VoiceStopRpc();
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	protected internal override string __getTypeName()
	{
		return "PlayerVoiceRecorder";
	}
}
