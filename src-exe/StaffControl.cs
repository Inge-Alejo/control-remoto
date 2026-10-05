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
                    int idx = json.IndexOf("\"version\":");
                    if (idx != -1) {
                        idx += 10;
                        int startQuote = json.IndexOf('"', idx);
                        int endQuote = json.IndexOf('"', startQuote + 1);
                        string remoteVer = json.Substring(startQuote + 1, endQuote - startQuote - 1);

                        if (!string.IsNullOrEmpty(remoteVer) && remoteVer != CURRENT_VERSION) {
                            DialogResult dr = MessageBox.Show(
                                "Hay una nueva versión de Auditorio Control disponible (v" + remoteVer + ").\n\n¿Deseas descargar la actualización ahora desde GitHub?",
                                "Actualización Disponible",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Information
                            );
                            if (dr == DialogResult.Yes) {
                                Process.Start("https://github.com/Inge-Alejo/control-remoto");
                            }
                        }
                    }
                }
            } catch { }
        }
    }
}
