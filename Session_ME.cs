using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class Session_ME : ISession
{
	public class Sender
	{
		public List<Message> sendingMessage;

		public Sender()
		{
			sendingMessage = new List<Message>();
		}

		public void AddMessage(Message message)
		{
			sendingMessage.Add(message);
		}

		public void run()
		{
			while (connected)
			{
				try
				{
					if (getKeyComplete)
					{
						while (sendingMessage.Count > 0)
						{
							Message m = sendingMessage[0];
							doSendMessage(m);
							sendingMessage.RemoveAt(0);
						}
					}
					try
					{
						Thread.Sleep(5);
					}
					catch (Exception ex)
					{
						HsnrLog.Log("CATCH", "Session_ME.cs:44 caught: " + ex.GetType().Name + " " + ex.Message);
						Cout.LogError(ex.ToString());
					}
				}
				catch (Exception _ex)
				{
					HsnrLog.Log("CATCH", "Session_ME.cs:49 caught: " + _ex.GetType().Name + " " + _ex.Message);
					Res.outz("error send message! ");
				}
			}
		}
	}

	private class MessageCollector
	{
		public void run()
		{
			try
			{
				while (connected)
				{
					Message message = readMessage();
					if (message == null)
					{
						HsnrLog.Log("RECV", "readMessage returned null -> break collector loop");
						break;
					}
					try
					{
						if (message.command == -27)
						{
							getKey(message);
						}
						else
						{
							onRecieveMsg(message);
						}
					}
					catch (Exception _ex)
					{
						HsnrLog.Log("CATCH", "Session_ME.cs:82 caught: " + _ex.GetType().Name + " " + _ex.Message);
						Cout.println("LOI NHAN  MESS THU 1");
					}
					try
					{
						Thread.Sleep(5);
					}
					catch (Exception _ex)
					{
						HsnrLog.Log("CATCH", "Session_ME.cs:90 caught: " + _ex.GetType().Name + " " + _ex.Message);
						Cout.println("LOI NHAN  MESS THU 2");
					}
				}
			}
			catch (Exception ex3)
			{
				HsnrLog.Log("RECV", "collector exception: " + ex3.GetType().Name + " " + ex3.Message);
				Debug.Log("error read message!");
				Debug.Log(ex3.Message.ToString());
			}
			if (!connected)
			{
				HsnrLog.Log("RECV", "collector end: connected=false (close called)");
				return;
			}
			if (messageHandler != null)
			{
				long dt = currentTimeMillis() - timeConnected;
				if (currentTimeMillis() - timeConnected > 500)
				{
					HsnrLog.Log("RECV", "-> onDisconnected (alive " + dt + "ms > 500)");
					messageHandler.onDisconnected(isMainSession);
				}
				else
				{
					HsnrLog.Log("RECV", "-> onConnectionFail (alive " + dt + "ms <= 500, server dropped early)");
					messageHandler.onConnectionFail(isMainSession);
				}
			}
			if (sc != null)
			{
				cleanNetwork();
			}
		}

		private void getKey(Message message)
		{
			try
			{
				if (HsnrConfig.useHsnrProtocol)
				{
					// HSNR: payload -27 = chuỗi ASCII "<uuid>$\0" (frame length đã được
					// readMessage đọc). TOÀN BỘ payload là key thô — KHÔNG đọc length byte,
					// KHÔNG đọc IP2/PORT2/isConnect2.
					sbyte[] payload = message.reader().buffer;
					HsnrLog.LogBytes("GETKEY", "handshake -27 payload ascii=\"" + HsnrLog.Ascii(payload) + "\"", payload);
					key = applyKeyDerivation(payload);
					if (key == null)
					{
						HsnrLog.Log("GETKEY", "applyKeyDerivation returned NULL -> key not set!");
						return;
					}
					curR = (sbyte)HsnrConfig.readCursorStart;
					curW = (sbyte)HsnrConfig.writeCursorStart;
					getKeyComplete = true;
					HsnrLog.Log("GETKEY", "key derived (" + key.Length + "B) ascii=\"" + HsnrLog.Ascii(key) + "\" curR=" + curR + " curW=" + curW);
				}
				else
				{
					sbyte b = message.reader().readSByte();
					key = new sbyte[b];
					for (int i = 0; i < b; i++)
					{
						key[i] = message.reader().readSByte();
					}
					for (int j = 0; j < key.Length - 1; j++)
					{
						ref sbyte reference = ref key[j + 1];
						reference ^= key[j];
					}
					getKeyComplete = true;
					GameMidlet.IP2 = message.reader().readUTF();
					GameMidlet.PORT2 = message.reader().readInt();
					GameMidlet.isConnect2 = ((message.reader().readByte() != 0) ? true : false);
				}
				if (isMainSession && GameMidlet.isConnect2)
				{
					GameCanvas.connect2();
				}
			}
			catch (Exception _ex)
			{
				HsnrLog.Log("CATCH", "Session_ME.cs:172 caught: " + _ex.GetType().Name + " " + _ex.Message);
			}
		}

		// Suy ra Session_Key từ payload handshake -27 theo HsnrConfig.keyDerivationMode.
		// ReverseHandshakeUuid (mode HSNR): bỏ đuôi '$'/'\0', đảo ngược chuỗi còn lại.
		public static sbyte[] applyKeyDerivation(sbyte[] raw)
		{
			if (raw == null || raw.Length == 0) return null;
			switch (HsnrConfig.keyDerivationMode)
			{
				case HsnrConfig.KeyDerivation.Raw:
					return raw;
				case HsnrConfig.KeyDerivation.ReverseHandshakeUuid:
				{
					int end = raw.Length;
					while (end > 0 && (raw[end - 1] == 0 || raw[end - 1] == (sbyte)'$'))
					{
						end--;
					}
					if (end <= 0) return null;
					sbyte[] derived = new sbyte[end];
					for (int j = 0; j < end; j++)
					{
						derived[j] = raw[end - 1 - j];
					}
					return derived;
				}
				case HsnrConfig.KeyDerivation.XorChainWithSeed:
				{
					sbyte[] derived = new sbyte[raw.Length];
					Array.Copy(raw, derived, raw.Length);
					derived[0] ^= (sbyte)HsnrConfig.keyDerivationSeed;
					for (int j = 0; j < derived.Length - 1; j++)
					{
						ref sbyte r = ref derived[j + 1];
						r ^= derived[j];
					}
					return derived;
				}
				case HsnrConfig.KeyDerivation.VanillaXorChain:
				default:
				{
					sbyte[] derived = new sbyte[raw.Length];
					Array.Copy(raw, derived, raw.Length);
					for (int j = 0; j < derived.Length - 1; j++)
					{
						ref sbyte r = ref derived[j + 1];
						r ^= derived[j];
					}
					return derived;
				}
			}
		}

		private Message readMessage2(sbyte cmd)
		{
			int num = readKey(dis.ReadSByte()) + 128;
			int num2 = readKey(dis.ReadSByte()) + 128;
			int num3 = readKey(dis.ReadSByte()) + 128;
			int num4 = (num3 * 256 + num2) * 256 + num;
			sbyte[] array = new sbyte[num4];
			int num5 = 0;
			byte[] src = dis.ReadBytes(num4);
			Buffer.BlockCopy(src, 0, array, 0, num4);
			recvByteCount += 5 + num4;
			int num6 = recvByteCount + sendByteCount;
			strRecvByteCount = num6 / 1024 + "." + num6 % 1024 / 102 + "Kb";
			if (getKeyComplete)
			{
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = readKey(array[i]);
				}
			}
			return new Message(cmd, array);
		}

		private Message readMessage()
		{
			try
			{
				sbyte b = dis.ReadSByte();
				if (getKeyComplete)
				{
					b = readKey(b);
				}
				bool is3Byte = (b == -32 || b == -66 || b == 11 || b == -67 || b == -74 || b == -87 || b == 66 || b == 12);
				// HSNR: cmd -28 (data game lớn) cũng dùng length 3-byte. Xác minh bằng
				// dữ liệu S2C thật: chỉ khi thêm -28 vào tập này thì cả part1 (34KB) lẫn
				// part2 (8.25MB resource) mới parse sạch 100%. Thiếu -> desync -> close loop.
				if (HsnrConfig.useHsnrProtocol && b == -28)
				{
					is3Byte = true;
				}
				if (is3Byte)
				{
					HsnrLog.Log("RECV", "cmd=" + b + " uses 3-byte length (readMessage2)");
					return readMessage2(b);
				}
				int num;
				if (getKeyComplete)
				{
					sbyte b2 = dis.ReadSByte();
					sbyte b3 = dis.ReadSByte();
					num = ((readKey(b2) & 0xFF) << 8) | (readKey(b3) & 0xFF);
				}
				else
				{
					sbyte b4 = dis.ReadSByte();
					sbyte b5 = dis.ReadSByte();
					num = (b4 & 0xFF00) | (b5 & 0xFF);
				}
				sbyte[] array = new sbyte[num];
				int num2 = 0;
				int num3 = 0;
				byte[] src = dis.ReadBytes(num);
				Buffer.BlockCopy(src, 0, array, 0, num);
				recvByteCount += 5 + num;
				int num4 = recvByteCount + sendByteCount;
				strRecvByteCount = num4 / 1024 + "." + num4 % 1024 / 102 + "Kb";
				if (getKeyComplete)
				{
					for (int i = 0; i < array.Length; i++)
					{
						array[i] = readKey(array[i]);
					}
				}
				HsnrLog.LogBytes("RECV", "cmd=" + b + " len=" + num + " ascii=\"" + HsnrLog.Ascii(array) + "\"", array, num);
				return new Message(b, array);
			}
			catch (Exception ex)
			{
				HsnrLog.Log("RECV", "readMessage exception: " + ex.GetType().Name + " " + ex.Message);
				Debug.Log(ex.StackTrace.ToString());
			}
			return null;
		}
	}

	protected static Session_ME instance = new Session_ME();

	private static NetworkStream dataStream;

	private static BinaryReader dis;

	private static BinaryWriter dos;

	public static IMessageHandler messageHandler;

	public static bool isMainSession = true;

	private static TcpClient sc;

	public static bool connected;

	public static bool connecting;

	private static Sender sender = new Sender();

	public static Thread initThread;

	public static Thread collectorThread;

	public static Thread sendThread;

	public static int sendByteCount;

	public static int recvByteCount;

	private static bool getKeyComplete;

	public static sbyte[] key = null;

	private static sbyte curR;

	private static sbyte curW;

	private static int timeConnected;

	private long lastTimeConn;

	public static string strRecvByteCount = string.Empty;

	public static bool isCancel;

	private string host;

	private int port;

	private long timeWaitConnect;

	public static int count;

	public static MyVector recieveMsg = new MyVector();

	public Session_ME()
	{
		Debug.Log("init Session_ME");
	}

	public void clearSendingMessage()
	{
		sender.sendingMessage.Clear();
	}

	public static Session_ME gI()
	{
		if (instance == null)
		{
			instance = new Session_ME();
		}
		return instance;
	}

	public bool isConnected()
	{
		return connected && sc != null && dis != null;
	}

	public void setHandler(IMessageHandler msgHandler)
	{
		messageHandler = msgHandler;
	}

	public void connect(string host, int port)
	{
		if (connected || connecting)
		{
			Debug.Log(">>>return connect ...!" + connected + "  ::  " + connecting);
			return;
		}
		if (mSystem.currentTimeMillis() < timeWaitConnect)
		{
			Debug.LogError(">>>>chặn việc nó kết nối 2 3 lần liên tục");
			return;
		}
		timeWaitConnect = mSystem.currentTimeMillis() + 50;
		if (isMainSession)
		{
			ServerListScreen.testConnect = -1;
		}
		this.host = host;
		this.port = port;
		getKeyComplete = false;
		close();
		Debug.Log("connecting...!");
		Debug.Log("host: " + host);
		Debug.Log("port: " + port);
		initThread = new Thread(NetworkInit);
		initThread.Start();
	}

	private void NetworkInit()
	{
		isCancel = false;
		connecting = true;
		Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Highest;
		connected = true;
		try
		{
			doConnect(host, port);
			messageHandler.onConnectOK(isMainSession);
		}
		catch (Exception ex)
		{
			HsnrLog.Log("CONNECT", "NetworkInit FAILED: " + ex.GetType().Name + " " + ex.Message);
			if (messageHandler != null)
			{
				close();
				messageHandler.onConnectionFail(isMainSession);
			}
		}
	}

	public void doConnect(string host, int port)
	{
		HsnrConfig.ResetLoginGuards();
		HsnrLog.Log("CONNECT", "doConnect host=" + host + " port=" + port + " hsnr=" + HsnrConfig.useHsnrProtocol);
		sc = new TcpClient();
		sc.Connect(host, port);
		HsnrLog.Log("CONNECT", "TCP connected OK -> " + host + ":" + port);
		dataStream = sc.GetStream();
		dis = new BinaryReader(dataStream, new UTF8Encoding());
		dos = new BinaryWriter(dataStream, new UTF8Encoding());
		sendThread = new Thread(sender.run);
		sendThread.Start();
		MessageCollector messageCollector = new MessageCollector();
		collectorThread = new Thread(messageCollector.run);
		collectorThread.Start();
		timeConnected = currentTimeMillis();
		connecting = false;
		doSendMessage(new Message(-27));
		HsnrLog.Log("HANDSHAKE", "sent -27 (request key)");
		key = null;
	}

	public void sendMessage(Message message)
	{
		count++;
		Res.outz("SEND MSG: " + message.command);
		sender.AddMessage(message);
	}

	private static void doSendMessage(Message m)
	{
		sbyte[] data = m.getData();
		HsnrLog.LogBytes("SEND", "cmd=" + m.command + " getKeyComplete=" + getKeyComplete, data);
		try
		{
			if (getKeyComplete)
			{
				sbyte value = writeKey(m.command);
				dos.Write(value);
			}
			else
			{
				dos.Write(m.command);
			}
			if (data != null)
			{
				int num = data.Length;
				if (getKeyComplete)
				{
					if (HsnrConfig.useHsnrProtocol && HsnrConfig.sendLengthBytes == 3)
					{
						dos.Write(writeKey((sbyte)((num) & 0xFF)));
						dos.Write(writeKey((sbyte)((num >> 8) & 0xFF)));
						dos.Write(writeKey((sbyte)((num >> 16) & 0xFF)));
					}
					else
					{
						int num2 = writeKey((sbyte)(num >> 8));
						dos.Write((sbyte)num2);
						int num3 = writeKey((sbyte)(num & 0xFF));
						dos.Write((sbyte)num3);
					}
				}
				else
				{
					dos.Write((ushort)num);
				}
				if (getKeyComplete)
				{
					for (int i = 0; i < data.Length; i++)
					{
						sbyte value2 = writeKey(data[i]);
						dos.Write(value2);
					}
				}
				sendByteCount += 5 + data.Length;
			}
			else
			{
				if (getKeyComplete)
				{
					if (HsnrConfig.useHsnrProtocol && HsnrConfig.sendLengthBytes == 3)
					{
						dos.Write(writeKey((sbyte)0));
						dos.Write(writeKey((sbyte)0));
						dos.Write(writeKey((sbyte)0));
					}
					else
					{
						int num4 = 0;
						int num5 = writeKey((sbyte)(num4 >> 8));
						dos.Write((sbyte)num5);
						int num6 = writeKey((sbyte)(num4 & 0xFF));
						dos.Write((sbyte)num6);
					}
				}
				else
				{
					dos.Write((ushort)0);
				}
				sendByteCount += 5;
			}
			dos.Flush();
		}
		catch (Exception ex)
		{
			HsnrLog.Log("SEND", "doSendMessage EXCEPTION cmd=" + m.command + ": " + ex.GetType().Name + " " + ex.Message);
			Debug.Log(ex.StackTrace);
			dos.Flush();
		}
	}

	public static sbyte readKey(sbyte b)
	{
		sbyte[] array = key;
		sbyte num = curR;
		curR = (sbyte)(num + HsnrConfig.cursorStep);
		sbyte result = (sbyte)((array[num] & 0xFF) ^ (b & 0xFF));
		if (curR >= key.Length)
		{
			curR = (sbyte)(curR % (sbyte)key.Length);
		}
		return result;
	}

	public static sbyte writeKey(sbyte b)
	{
		sbyte[] array = key;
		sbyte num = curW;
		curW = (sbyte)(num + HsnrConfig.cursorStep);
		sbyte result = (sbyte)((array[num] & 0xFF) ^ (b & 0xFF));
		if (curW >= key.Length)
		{
			curW = (sbyte)(curW % (sbyte)key.Length);
		}
		return result;
	}

	public static void onRecieveMsg(Message msg)
	{
		if (Thread.CurrentThread.Name == Main.mainThreadName)
		{
			messageHandler.onMessage(msg);
		}
		else
		{
			recieveMsg.addElement(msg);
		}
	}

	public static void update()
	{
		while (recieveMsg.size() > 0)
		{
			Message message = (Message)recieveMsg.elementAt(0);
			if (Controller.isStopReadMessage)
			{
				break;
			}
			if (message == null)
			{
				recieveMsg.removeElementAt(0);
				break;
			}
			messageHandler.onMessage(message);
			recieveMsg.removeElementAt(0);
		}
	}

	public void close()
	{
		cleanNetwork();
	}

	private static void cleanNetwork()
	{
		HsnrLog.Log("CLOSE", "cleanNetwork called. getKeyComplete=" + getKeyComplete);
		key = null;
		curR = 0;
		curW = 0;
		Debug.LogError(">>>cleanNetwork ...!");
		try
		{
			connected = false;
			connecting = false;
			if (sc != null)
			{
				sc.Close();
				sc = null;
			}
			if (dataStream != null)
			{
				dataStream.Close();
				dataStream = null;
			}
			if (dos != null)
			{
				dos.Close();
				dos = null;
			}
			if (dis != null)
			{
				dis.Close();
				dis = null;
			}
			if (Thread.CurrentThread.Name == Main.mainThreadName)
			{
				if (sendThread != null)
				{
					sendThread.Abort();
				}
				sendThread = null;
				if (initThread != null)
				{
					initThread.Abort();
				}
				initThread = null;
				if (collectorThread != null)
				{
					collectorThread.Abort();
				}
				collectorThread = null;
			}
			else
			{
				sendThread = null;
				initThread = null;
				collectorThread = null;
			}
			if (isMainSession)
			{
				ServerListScreen.testConnect = 0;
			}
			Controller.isGet_CLIENT_INFO = false;
		}
		catch (Exception _ex)
		{
			HsnrLog.Log("CATCH", "Session_ME.cs:683 caught: " + _ex.GetType().Name + " " + _ex.Message);
		}
	}

	public static int currentTimeMillis()
	{
		return Environment.TickCount;
	}

	public static byte convertSbyteToByte(sbyte var)
	{
		if (var > 0)
		{
			return (byte)var;
		}
		return (byte)(var + 256);
	}

	public static byte[] convertSbyteToByte(sbyte[] var)
	{
		byte[] array = new byte[var.Length];
		for (int i = 0; i < var.Length; i++)
		{
			if (var[i] > 0)
			{
				array[i] = (byte)var[i];
			}
			else
			{
				array[i] = (byte)(var[i] + 256);
			}
		}
		return array;
	}

	public bool isCompareIPConnect()
	{
		return true;
	}
}
