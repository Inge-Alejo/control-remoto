using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AuditorioControl {
    static class HostProgram {
        private const string RENDER_HOST_URL = "https://control-remoto-o5f6.onrender.com/host.html";
        private const string RENDER_WSS_URL = "wss://control-remoto-o5f6.onrender.com";

        // Win32 API para control nativo del cursor, mouse y teclado
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        private const byte VK_VOLUME_MUTE = 0xAD;
        private const byte VK_VOLUME_DOWN = 0xAE;
        private const byte VK_VOLUME_UP = 0xAF;
        private const byte VK_LWIN = 0x5B;
        private const byte VK_MENU = 0x12; // Alt
        private const byte VK_TAB = 0x09;
        private const byte VK_F4 = 0x73;
        private const byte VK_F5 = 0x74;
        private const byte VK_F11 = 0x7A;
        private const byte VK_SPACE = 0x20;
        private const byte VK_LEFT = 0x25;
        private const byte VK_RIGHT = 0x27;
        private const byte VK_D = 0x44;
        private const byte VK_P = 0x50;
        private const byte VK_ESCAPE = 0x1B;
        private const byte VK_RETURN = 0x0D;
        private const byte VK_BACK = 0x08;

        private const string CURRENT_VERSION = "1.2.0";
        private const string VERSION_CHECK_URL = "https://control-remoto-o5f6.onrender.com/api/version";

        private static ClientWebSocket wsClient;
        private static CancellationTokenSource cts;
        private static NotifyIcon trayIcon;
        private static int screenWidth;
        private static int screenHeight;

        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            screenWidth = Screen.PrimaryScreen.Bounds.Width;
            screenHeight = Screen.PrimaryScreen.Bounds.Height;

            cts = new CancellationTokenSource();

            // 1. Validar actualizaciones automáticamente en segundo plano
            Task.Run(() => CheckForUpdates());

            // 2. Iniciar el bucle de conexión WebSocket nativo en segundo plano
            Task.Run(() => WebSocketLoop(cts.Token));

            // 3. Iniciar transmisión periódica ligera de pantalla
            Task.Run(() => ScreenCaptureLoop(cts.Token));

            // 4. Abrir la ventana visual del Auditorio en Edge / Chrome
            LaunchHostWindow();

            // 4. Configurar icono en la bandeja del sistema (System Tray)
            trayIcon = new NotifyIcon {
                Icon = SystemIcons.Application,
                Text = "Auditorio Control - Host Nativo Activo",
                Visible = true
            };

            ContextMenu menu = new ContextMenu();
            menu.MenuItems.Add("Abrir Pantalla del Auditorio", (s, e) => LaunchHostWindow());
            menu.MenuItems.Add("-");
            menu.MenuItems.Add("Salir", (s, e) => {
                cts.Cancel();
                trayIcon.Visible = false;
                Application.Exit();
            });
            trayIcon.ContextMenu = menu;

            trayIcon.ShowBalloonTip(3000, "Auditorio Control Activo", "El Host nativo está listo. Recibiendo órdenes del staff en tiempo real.", ToolTipIcon.Info);

            Application.Run();
        }

        private static void LaunchHostWindow() {
            try {
                string edgePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe");
                if (!File.Exists(edgePath)) {
                    edgePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application\msedge.exe");
                }

                string chromePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe");
                if (!File.Exists(chromePath)) {
                    chromePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe");
                }

                if (File.Exists(edgePath)) {
                    Process.Start(new ProcessStartInfo {
                        FileName = edgePath,
                        Arguments = "--app=\"" + RENDER_HOST_URL + "\"",
                        UseShellExecute = true
                    });
                } else if (File.Exists(chromePath)) {
                    Process.Start(new ProcessStartInfo {
                        FileName = chromePath,
                        Arguments = "--app=\"" + RENDER_HOST_URL + "\"",
                        UseShellExecute = true
                    });
                } else {
                    Process.Start(new ProcessStartInfo {
                        FileName = RENDER_HOST_URL,
                        UseShellExecute = true
                    });
                }
            } catch { }
        }

        private static void CheckForUpdates() {
            try {
                using (System.Net.WebClient client = new System.Net.WebClient()) {
                    client.Headers.Add("User-Agent", "AuditorioHost");
                    string json = client.DownloadString(VERSION_CHECK_URL);
                    string remoteVer = ExtractString(json, "\"version\":");
                    if (!string.IsNullOrEmpty(remoteVer) && remoteVer != CURRENT_VERSION) {
                        string notes = ExtractString(json, "\"notes\":");
                        string msg = "Hay una nueva versión disponible de Auditorio Control (v" + remoteVer + ").\n\n" +
                                     (string.IsNullOrEmpty(notes) ? "" : "Novedades: " + notes + "\n\n") +
                                     "¿Deseas descargar la actualización ahora desde GitHub?";
                        
                        DialogResult dr = MessageBox.Show(msg, "Actualización Disponible - Auditorio Control", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        if (dr == DialogResult.Yes) {
                            Process.Start("https://github.com/Inge-Alejo/control-remoto");
                        }
                    }
                }
            } catch {
                // Silencioso si no hay conexión al iniciar
            }
        }

        private static async Task WebSocketLoop(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                try {
                    wsClient = new ClientWebSocket();
                    Uri serverUri = new Uri(RENDER_WSS_URL);
                    await wsClient.ConnectAsync(serverUri, token);

                    // Registrarse como Host
                    string registerMsg = "{\"type\":\"register_host\"}";
                    byte[] registerBytes = Encoding.UTF8.GetBytes(registerMsg);
                    await wsClient.SendAsync(new ArraySegment<byte>(registerBytes), WebSocketMessageType.Text, true, token);

                    byte[] buffer = new byte[8192];
                    while (wsClient.State == WebSocketState.Open && !token.IsCancellationRequested) {
                        WebSocketReceiveResult result = await wsClient.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        if (result.MessageType == WebSocketMessageType.Close) {
                            await wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", token);
                            break;
                        }

                        string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        ProcessRemoteMessage(json);
                    }
                } catch {
                    // Reconexión silenciosa tras fallo de red
                }

                try {
                    await Task.Delay(3000, token);
                } catch { }
            }
        }

        private static void ProcessRemoteMessage(string json) {
            try {
                // Parseo manual ultrarrápido y seguro de JSON (sin requerir Newtonsoft.Json)
                if (json.Contains("\"mouse_move\"")) {
                    double x = ExtractDouble(json, "\"x\":");
                    double y = ExtractDouble(json, "\"y\":");
                    int pixelX = (int)Math.Round(x * screenWidth);
                    int pixelY = (int)Math.Round(y * screenHeight);
                    SetCursorPos(pixelX, pixelY);
                } else if (json.Contains("\"mouse_click\"")) {
                    bool isRight = json.Contains("\"right\"");
                    bool isDouble = json.Contains("true") && json.Contains("\"double\"");

                    if (isRight) {
                        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
                        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
                    } else {
                        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                        if (isDouble) {
                            Thread.Sleep(50);
                            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                        }
                    }
                } else if (json.Contains("\"deck_action\"")) {
                    string action = ExtractString(json, "\"action\":");
                    ExecuteDeckAction(action);
                } else if (json.Contains("\"key_press\"")) {
                    string key = ExtractString(json, "\"key\":");
                    ExecuteKeyPress(key);
                }
            } catch { }
        }

        private static void ExecuteDeckAction(string action) {
            switch (action) {
                case "SHOW_DESKTOP":
                    // Win + D
                    keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_D, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_D, 0, 2, UIntPtr.Zero);
                    keybd_event(VK_LWIN, 0, 2, UIntPtr.Zero);
                    break;
                case "ALT_TAB":
                    // Alt + Tab
                    keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_TAB, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_TAB, 0, 2, UIntPtr.Zero);
                    keybd_event(VK_MENU, 0, 2, UIntPtr.Zero);
                    break;
                case "CLOSE_WINDOW":
                    // Alt + F4
                    keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_F4, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_F4, 0, 2, UIntPtr.Zero);
                    keybd_event(VK_MENU, 0, 2, UIntPtr.Zero);
                    break;
                case "PROJECTOR_SWITCH":
                    // Win + P
                    keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_P, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_P, 0, 2, UIntPtr.Zero);
                    keybd_event(VK_LWIN, 0, 2, UIntPtr.Zero);
                    break;
                case "FULLSCREEN":
                    // F11
                    keybd_event(VK_F11, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_F11, 0, 2, UIntPtr.Zero);
                    break;
                case "RELOAD_PAGE":
                    // F5
                    keybd_event(VK_F5, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_F5, 0, 2, UIntPtr.Zero);
                    break;
                case "PANIC_RESET":
                    // Mute + Win+D
                    keybd_event(VK_VOLUME_MUTE, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_VOLUME_MUTE, 0, 2, UIntPtr.Zero);
                    keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_D, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_D, 0, 2, UIntPtr.Zero);
                    keybd_event(VK_LWIN, 0, 2, UIntPtr.Zero);
                    break;
                case "VOLUME_UP":
                    keybd_event(VK_VOLUME_UP, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_VOLUME_UP, 0, 2, UIntPtr.Zero);
                    break;
                case "VOLUME_DOWN":
                    keybd_event(VK_VOLUME_DOWN, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_VOLUME_DOWN, 0, 2, UIntPtr.Zero);
                    break;
                case "VOLUME_MUTE":
                    keybd_event(VK_VOLUME_MUTE, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_VOLUME_MUTE, 0, 2, UIntPtr.Zero);
                    break;
                case "MEDIA_PLAY_PAUSE":
                    keybd_event(VK_SPACE, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_SPACE, 0, 2, UIntPtr.Zero);
                    break;
                case "SEEK_FWD":
                    keybd_event(VK_RIGHT, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_RIGHT, 0, 2, UIntPtr.Zero);
                    break;
                case "SEEK_BACK":
                    keybd_event(VK_LEFT, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_LEFT, 0, 2, UIntPtr.Zero);
                    break;
            }
        }

        private static void ExecuteKeyPress(string key) {
            try {
                if (key == "{ENTER}") {
                    keybd_event(VK_RETURN, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_RETURN, 0, 2, UIntPtr.Zero);
                } else if (key == "{ESC}") {
                    keybd_event(VK_ESCAPE, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_ESCAPE, 0, 2, UIntPtr.Zero);
                } else if (key == "{BACKSPACE}") {
                    keybd_event(VK_BACK, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_BACK, 0, 2, UIntPtr.Zero);
                } else if (key == "{TAB}") {
                    keybd_event(VK_TAB, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_TAB, 0, 2, UIntPtr.Zero);
                } else {
                    SendKeys.SendWait(key);
                }
            } catch { }
        }

        private static async Task ScreenCaptureLoop(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                try {
                    if (wsClient != null && wsClient.State == WebSocketState.Open) {
                        using (Bitmap bmp = new Bitmap(screenWidth, screenHeight)) {
                            using (Graphics g = Graphics.FromImage(bmp)) {
                                g.CopyFromScreen(Point.Empty, Point.Empty, new Size(screenWidth, screenHeight));
                            }

                            // Redimensionar a 1280x720 para bajo consumo de ancho de banda
                            using (Bitmap resized = new Bitmap(bmp, new Size(1280, 720))) {
                                using (MemoryStream ms = new MemoryStream()) {
                                    ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                                    EncoderParameters ep = new EncoderParameters(1);
                                    ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 40L);

                                    resized.Save(ms, jpgEncoder, ep);
                                    string b64 = Convert.ToBase64String(ms.ToArray());
                                    string frameMsg = "{\"type\":\"screen_frame\",\"data\":\"" + b64 + "\"}";
                                    byte[] frameBytes = Encoding.UTF8.GetBytes(frameMsg);

                                    await wsClient.SendAsync(new ArraySegment<byte>(frameBytes), WebSocketMessageType.Text, true, token);
                                }
                            }
                        }
                    }
                } catch { }

                try {
                    await Task.Delay(1000, token); // 1 FPS para vista previa fluida de fondo
                } catch { }
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format) {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs) {
                if (codec.FormatID == format.Guid) return codec;
            }
            return null;
        }

        private static double ExtractDouble(string json, string key) {
            int idx = json.IndexOf(key);
            if (idx == -1) return 0;
            idx += key.Length;
            int end = json.IndexOfAny(new char[] { ',', '}', ' ' }, idx);
            if (end == -1) end = json.Length;
            string val = json.Substring(idx, end - idx).Trim().Replace("\"", "");
            double result;
            double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result);
            return result;
        }

        private static string ExtractString(string json, string key) {
            int idx = json.IndexOf(key);
            if (idx == -1) return "";
            idx += key.Length;
            int startQuote = json.IndexOf('"', idx);
            if (startQuote == -1) return "";
            int endQuote = json.IndexOf('"', startQuote + 1);
            if (endQuote == -1) return "";
            return json.Substring(startQuote + 1, endQuote - startQuote - 1);
        }
    }
}
