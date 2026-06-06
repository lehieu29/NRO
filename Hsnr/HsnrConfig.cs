// ============================================================================
// HsnrConfig — LỚP QUẢN LÝ TẬP TRUNG cho mod HSNR.
//
// MỌI biến cờ, dữ liệu, và tham số phục vụ mod HSNR đều nằm trong thư mục
// `Client/Hsnr/`. Các class gốc (Session_ME, Service, LoginScr, ServerListScreen,
// GameMidlet, ...) chỉ tham chiếu các cờ ở đây để rẽ nhánh logic:
//
//     if (HsnrConfig.useHsnrProtocol) { ... hành vi HSNR ... }
//     else                            { ... hành vi NRO gốc ... }
//
// Muốn tắt mod để chạy như client gốc: đặt `useHsnrProtocol = false` (hoặc bỏ
// gọi EnableHsnr lúc khởi động). KHÔNG cần sửa rải rác trong các class khác.
//
// Đặt ở GLOBAL NAMESPACE (không namespace) để mọi class dùng trực tiếp.
//
// Nguồn tham số: phân tích pcap thật `wireshark_hiuclone11x.pcapng` + decompile
// GameAssembly.dll. Xem analysis/HSNR_Cipher_BreakThrough.md.
// ============================================================================

public static class HsnrConfig
{
	// ---- CÔNG TẮC TỔNG ----
	// false = hành vi NRO gốc; true = giao thức HSNR. Mặc định false (vanilla),
	// chỉ EnableHsnr() mới bật. GameMidlet gọi EnableHsnr() lúc khởi động.
	public static bool useHsnrProtocol = false;

	// ---- ENDPOINT / SERVER ----
	// 5 server HSNR. Khi useHsnrProtocol = true, danh sách này THAY THẾ toàn bộ
	// server gốc trong ServerListScreen.
	// Định dạng mỗi phần tử: "Tên|IP|Port".
	public static readonly string[] servers = new string[]
	{
		"Server 1|103.92.25.143|1445",
		"Server 2|103.92.27.8|1445",
		"Server 3|103.92.26.37|1445",
		"Server 4|103.92.25.245|1445",
		"Server 5|103.92.24.224|1445",
	};

	// Ngôn ngữ + server ưu tiên mặc định cho danh sách HSNR.
	public static int serverLanguage = 0;   // 0 = VN
	public static int serverPriority = 0;    // index server chọn mặc định

	// Tạo chuỗi server đúng định dạng mà ServerListScreen.getServerList() parse:
	//   "name:ip:port:lang:typeSv:isNew,...,<language>,<priority>"
	public static string BuildServerListString()
	{
		System.Text.StringBuilder sb = new System.Text.StringBuilder();
		for (int i = 0; i < servers.Length; i++)
		{
			string[] p = servers[i].Split('|');
			string name = p[0];
			string ip = p[1];
			string port = p[2];
			// name:ip:port:lang:typeSv:isNew
			sb.Append(name).Append(':').Append(ip).Append(':').Append(port)
			  .Append(':').Append(serverLanguage).Append(":0:0");
			sb.Append(',');
		}
		sb.Append(serverLanguage).Append(',').Append(serverPriority);
		return sb.ToString();
	}

	// ---- KEY DERIVATION ----
	//   VanillaXorChain      = key[j+1] ^= key[j] (gốc)
	//   Raw                  = dùng khóa thô không biến đổi
	//   XorChainWithSeed     = XOR-chain có seed khởi tạo (dùng keyDerivationSeed)
	//   ReverseHandshakeUuid = bỏ đuôi "$\0", ĐẢO NGƯỢC chuỗi còn lại, lấy ASCII
	//                          bytes (mode HSNR đã verify từ pcap thật)
	//   Custom               = chỗ dành cho biến thể khác
	public enum KeyDerivation
	{
		VanillaXorChain,
		Raw,
		XorChainWithSeed,
		ReverseHandshakeUuid,
		Custom
	}

	public static KeyDerivation keyDerivationMode = KeyDerivation.VanillaXorChain;
	public static int keyDerivationSeed = 0; // dùng khi XorChainWithSeed

	// ---- CURSOR MÃ HÓA ----
	// Gốc = (0, 0, 1). HSNR cũng (0, 0, 1) — xác nhận từ native.
	public static int readCursorStart = 0;
	public static int writeCursorStart = 0;
	public static int cursorStep = 1;

	// ---- MESSAGE FRAMING (chiều GỬI) ----
	public static int sendLengthBytes = 2;       // 2 hoặc 3
	public static bool sendLengthBigEndian = true;

	// ---- LOGIN ----
	// HSNR verify từ pcap: thứ tự field = sub(0) -> UTF version -> UTF user
	// -> UTF pass -> byte type; versionString = "0.1.2".
	public static string versionString = "2.5.0";        // HSNR: "0.1.2"
	public static bool sendClientInfoBeforeLogin = false; // gói -29 sub 2 + cmd 126 trước login
	public static sbyte loginCommand = -29;
	public static sbyte loginSubCommand = 0;

	// ---- setClientType (MSG0) ----
	// HSNR layout đã verify từ pcap (login thành công). KHÁC HẲN layout NRO gốc:
	//   sub(2) + UTF(version) + flags(11 byte) + UTF(clientTag)
	// 11 byte flags lấy nguyên từ pcap thật (MSG0). clientTag = "MOD_BASE".
	// Nếu sai layout này, server đọc payload lệch -> block -> drop kết nối.
	// CHÍNH XÁC 10 byte (đối chiếu pcap MSG0 đã login thành công): sau UTF(version)
	// là đúng 10 byte này, rồi tới UTF("MOD_BASE"). KHÔNG thừa/thiếu byte nào.
	public static sbyte[] clientInfoFlags = new sbyte[]
	{
		0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x02, 0x58, 0x02, 0x00
	};
	public static string clientTag = "MOD_BASE";

	// ---- GUARD chống gửi lặp (reset mỗi lần doConnect) ----
	// Client gốc gọi setClientType()/sendDeviceUuid() từ NHIỀU nơi (GameCanvas,
	// LoginScr, ServerListScreen, Controller) -> dễ gửi lặp/sai thứ tự khiến server
	// drop. Hai cờ này đảm bảo mỗi gói client-info chỉ gửi ĐÚNG 1 LẦN mỗi kết nối,
	// khớp chuỗi probe đã login OK: setClientType -> deviceUuid -> login.
	public static bool clientTypeSent = false;
	public static bool deviceUuidSent = false;

	// Gọi trong doConnect để bắt đầu phiên mới.
	public static void ResetLoginGuards()
	{
		clientTypeSent = false;
		deviceUuidSent = false;
	}

	// Bật log chẩn đoán framing.
	public static bool verboseFramingDiagnostics = true;

	// Bật logger HsnrLog (ghi Debug.Log + file hsnr_log.txt). Đặt false khi phát hành.
	public static bool enableLog = true;

	// ---- BẬT HSNR ----
	// Ghi đè TẤT CẢ tham số sang giá trị HSNR đã verify. Gọi MỘT LẦN lúc khởi
	// động (GameMidlet.initGame) trước khi connect. Giữ nguyên tắc
	// "defaults = vanilla": chỉ method này mới ghi đè sang HSNR.
	public static void EnableHsnr()
	{
		useHsnrProtocol = true;
		keyDerivationMode = KeyDerivation.ReverseHandshakeUuid;
		readCursorStart = 0;
		writeCursorStart = 0;
		cursorStep = 1;
		sendLengthBytes = 2;
		sendLengthBigEndian = true;
		versionString = "0.1.2";
		sendClientInfoBeforeLogin = true;
		loginCommand = -29;
		loginSubCommand = 0;
	}
}
