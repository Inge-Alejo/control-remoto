using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AuditorioControl {
    static class StaffProgram {
        private const string CURRENT_VERSION = "1.2.0";
        private const string RENDER_URL = "https://control-remoto-o5f6.onrender.com/staff.html";
        private const string VERSION_CHECK_URL = "https://control-remoto-o5f6.onrender.com/api/version";

        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Validar actualizaciones en segundo plano
            Task.Run(() => CheckForUpdates());

            try {
                // Buscar Microsoft Edge o Google Chrome para abrir en modo App Nativa
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
                        Arguments = "--app=\"" + RENDER_URL + "\"",
                        UseShellExecute = true
                    });
                } else if (File.Exists(chromePath)) {
                    Process.Start(new ProcessStartInfo {
                        FileName = chromePath,
                        Arguments = "--app=\"" + RENDER_URL + "\"",
                        UseShellExecute = true
                    });
                } else {
                    // Fallback a navegador predeterminado
                    Process.Start(new ProcessStartInfo {
                        FileName = RENDER_URL,
                        UseShellExecute = true
                    });
                }
            } catch (Exception ex) {
                MessageBox.Show("Error al iniciar el mando de Staff:\n" + ex.Message, "Auditorio Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void CheckForUpdates() {
            try {
                using (WebClient client = new WebClient()) {
                    client.Headers.Add("User-Agent", "AuditorioStaff");
                    string json = client.DownloadString(VERSION_CHECK_URL);
                    string remoteVer = ExtractString(json, "\"version\":");
                    string downloadUrl = ExtractString(json, "\"staffDownloadUrl\":");
                    if (string.IsNullOrEmpty(downloadUrl)) {
                        downloadUrl = "https://github.com/Inge-Alejo/control-remoto/raw/main/Staff-Control.exe";
                    }

                    if (!string.IsNullOrEmpty(remoteVer) && IsNewerVersion(CURRENT_VERSION, remoteVer)) {
                        string notes = ExtractString(json, "\"notes\":");
                        DialogResult dr = MessageBox.Show(
                            "¡Nueva versión disponible de Staff Remote (v" + remoteVer + ")!\n\n" +
                            (string.IsNullOrEmpty(notes) ? "" : "Novedades: " + notes + "\n\n") +
                            "¿Deseas actualizar e instalar automáticamente ahora?\n\n(La aplicación se descargará, reemplazará e iniciará sola en segundos)",
                            "Actualización Automática - Staff Remote",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information
                        );
                        if (dr == DialogResult.Yes) {
                            PerformSelfUpdate(downloadUrl, remoteVer);
                        }
                    }
                }
            } catch { }
        }

        private static bool IsNewerVersion(string currentVer, string remoteVer) {
            try {
                Version cur, rem;
                if (Version.TryParse(currentVer, out cur) && Version.TryParse(remoteVer, out rem)) {
                    return rem > cur;
                }
                return !string.Equals(currentVer, remoteVer, StringComparison.OrdinalIgnoreCase);
            } catch {
                return false;
            }
        }

        private static void PerformSelfUpdate(string downloadUrl, string newVersion) {
            try {
                string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                string tempExe = Path.Combine(Path.GetTempPath(), "StaffControl-Update-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".exe");

                using (WebClient dlClient = new WebClient()) {
                    dlClient.Headers.Add("User-Agent", "AuditorioAutoUpdater");
                    dlClient.DownloadFile(downloadUrl, tempExe);
                }

                FileInfo fi = new FileInfo(tempExe);
                if (!fi.Exists || fi.Length < 10000) {
                    throw new Exception("El archivo descargado está incompleto o dañado.");
                }

                string cmd = string.Format(
                    "/c chcp 65001 >nul & timeout /t 1 /nobreak >nul & move /y \"{0}\" \"{1}\" >nul & start \"\" \"{1}\"",
                    tempExe,
                    currentExe
                );

                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", cmd) {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                };

                Process.Start(psi);
                Environment.Exit(0);
            } catch (Exception ex) {
                MessageBox.Show("No se pudo autogestionar la actualización:\n" + ex.Message, "Error de Actualización", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string ExtractString(string json, string key) {
            try {
                int idx = json.IndexOf(key);
                if (idx == -1) return "";
                idx += key.Length;
                int startQuote = json.IndexOf('"', idx);
                if (startQuote == -1) return "";
                int endQuote = json.IndexOf('"', startQuote + 1);
                if (endQuote == -1) return "";
                return json.Substring(startQuote + 1, endQuote - startQuote - 1);
            } catch {
                return "";
            }
        }
    }
}
