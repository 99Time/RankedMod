using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Codebase;

public static class SystemFunc
{
	public class StreamString
	{
		private readonly Stream _ioStream;

		private readonly UnicodeEncoding _streamEncoding;

		public bool IsConnected
		{
			get
			{
				if (_ioStream is NamedPipeClientStream namedPipeClientStream)
				{
					return namedPipeClientStream.IsConnected;
				}
				if (_ioStream is NamedPipeServerStream namedPipeServerStream)
				{
					return namedPipeServerStream.IsConnected;
				}
				return false;
			}
		}

		public StreamString(Stream ioStream)
		{
			_ioStream = ioStream;
			_streamEncoding = new UnicodeEncoding();
		}

		public string ReadString()
		{
			int num = _ioStream.ReadByte() * 256;
			num += _ioStream.ReadByte();
			byte[] array = new byte[num];
			_ioStream.Read(array, 0, num);
			return _streamEncoding.GetString(array);
		}

		public int WriteString(string outString)
		{
			byte[] bytes = _streamEncoding.GetBytes(outString);
			int num = bytes.Length;
			if (num > 65535)
			{
				num = 65535;
			}
			_ioStream.WriteByte((byte)(num / 256));
			_ioStream.WriteByte((byte)(num & 0xFF));
			_ioStream.Write(bytes, 0, num);
			_ioStream.Flush();
			return bytes.Length + 2;
		}

		public void Close()
		{
			_ioStream.Close();
		}
	}

	public static T GetPrivateField<T>(Type typeContainingField, object instanceOfType, string fieldName)
	{
		return (T)typeContainingField.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instanceOfType);
	}

	public static Stick GetStick(GameObject gameObject)
	{
		return gameObject.GetComponent<Stick>();
	}

	public static PlayerBodyV2 GetPlayerBodyV2(GameObject gameObject)
	{
		return gameObject.GetComponent<PlayerBodyV2>();
	}

	public static string RemoveWhitespace(string input)
	{
		return new string(input.Where((char c) => !char.IsWhiteSpace(c)).ToArray());
	}
}
