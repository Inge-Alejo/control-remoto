using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace AuditorioControl {
    static class StaffProgram {
        private const string RENDER_URL = "https://control-remoto-o5f6.onrender.com/staff.html";

        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

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
    }
}
