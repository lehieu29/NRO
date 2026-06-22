using System;
using System.Text;
using UnityEngine;

// ============================================================================
// HsnrLog — logger tập trung cho mod HSNR (thư mục Client/Hsnr/).
//
// Mục đích: chẩn đoán luồng connect/login khi client báo "máy chủ tắt hoặc mất
// sóng" dù server thật hoạt động. Ghi log ra Debug.Log (Unity console) VÀ ra file
// để xem lại sau khi chạy build.
//
// File log: <persistentDataPath>/hsnr_log.txt (mỗi lần khởi động ghi đè đầu phiên).
//   - Windows player : %userprofile%\AppData\LocalLow\<Company>\<Product>\hsnr_log.txt
//   - Editor         : <Project>/hsnr_log.txt cũng được nếu persistentDataPath rỗng.
//
// Bật/tắt qua HsnrConfig.enableLog. Khi tắt = không tốn chi phí (return sớm).
// ============================================================================

public static class HsnrLog
{
	private static string _path;
	private static bool _inited;
	private static readonly object _lock = new object();
	private static int _startTick;

	private static void EnsureInit()
	{
		if (_inited) return;
		_inited = true;
		try
		{
			string dir = Application.persistentDataPath;
			_path = (string.IsNullOrEmpty(dir) ? "." : dir) + "/hsnr_log.txt";
			_startTick = Environment.TickCount;
			// APPEND (không truncate) — giữ log mọi phiên để không mất dữ liệu khi
			// game reconnect-loop / khởi động lại nhiều lần. Phân tách bằng header.
			System.IO.File.AppendAllText(_path,
				"\n\n==== HSNR SESSION @ " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ====\n");
		}
		catch (Exception)
		{
			_path = null;
		}
	}

	// Log một dòng: [ms] TAG | msg
	public static void Log(string tag, string msg)
	{
		if (!HsnrConfig.enableLog) return;
		EnsureInit();
		string line = "[" + (Environment.TickCount - _startTick) + "ms] " + tag + " | " + msg;
		Debug.Log("[HSNR] " + line);
		if (_path == null) return;
		try
		{
			lock (_lock)
			{
				System.IO.File.AppendAllText(_path, line + "\n");
			}
		}
		catch (Exception)
		{
		}
	}

	// Log kèm dump hex của mảng byte (sbyte[] hoặc byte[]).
	public static void LogBytes(string tag, string msg, sbyte[] data, int len = -1)
	{
		if (!HsnrConfig.enableLog) return;
		if (len < 0) len = (data == null ? 0 : data.Length);
		Log(tag, msg + " [" + len + "B] " + Hex(data, len));
	}

	public static string Hex(sbyte[] data, int len = -1)
	{
		if (data == null) return "(null)";
		if (len < 0) len = data.Length;
		if (len > data.Length) len = data.Length;
		// Giới hạn 64 byte để log gọn.
		int cap = (len < 64) ? len : 64;
		StringBuilder sb = new StringBuilder(cap * 3);
		for (int i = 0; i < cap; i++)
		{
			sb.Append(((byte)data[i]).ToString("X2"));
			if (i < cap - 1) sb.Append(' ');
		}
		if (len > cap) sb.Append(" ...(+" + (len - cap) + ")");
		return sb.ToString();
	}

	// Dump TOAN BO hex (khong cap 64B) ra file rieng de parse offline.
	// Dung de reverse wire-format cac lenh char/task ma decompile khong khop.
	public static void DumpFull(string tag, sbyte[] data, int len = -1)
	{
		if (!HsnrConfig.enableLog) return;
		EnsureInit();
		if (data == null) { Log(tag, "DUMP (null)"); return; }
		if (len < 0) len = data.Length;
		if (len > data.Length) len = data.Length;
		StringBuilder sb = new StringBuilder(len * 3 + 32);
		sb.Append("DUMP len=").Append(len).Append(" hex=");
		for (int i = 0; i < len; i++)
		{
			sb.Append(((byte)data[i]).ToString("X2"));
			if (i < len - 1) sb.Append(' ');
		}
		Log(tag, sb.ToString());
	}

	// Chuyển sbyte[] -> chuỗi ASCII đọc được (ký tự ngoài 32..126 thành '.').
	public static string Ascii(sbyte[] data, int len = -1)
	{
		if (HsnrConfig.enableLog)
		{
			if (data == null) return "(null)";
			if (len < 0) len = data.Length;
			if (len > data.Length) len = data.Length;
			int cap = (len < 80) ? len : 80;
			StringBuilder sb = new StringBuilder(cap);
			for (int i = 0; i < cap; i++)
			{
				byte b = (byte)data[i];
				sb.Append((b >= 32 && b < 127) ? (char)b : '.');
			}
			return sb.ToString();
		}
		return "Vui lòng mở lại cờ enableLog để có dữ liệu";
	}
}
