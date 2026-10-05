using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AuditorioControl {
    static class HostProgram {
        private const string RENDER_HOST_URL = "https://control-remoto-o5f6.onrender.com/host.html";
        private const string RENDER_WSS_URL = "wss://control-remoto-o5f6.onrender.com";

        private static Process bridgeProcess = null;
        private static NotifyIcon trayIcon = null;

        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try {
                // 1. Iniciar el agente de Windows en segundo plano (si existe Node y host-bridge.js)
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                string bridgeScript = Path.Combine(currentDir, "host-bridge.js");

                if (File.Exists(bridgeScript)) {
                    try {
                        ProcessStartInfo psi = new ProcessStartInfo {
                            FileName = "node.exe",
                            Arguments = "\"" + bridgeScript + "\" " + RENDER_WSS_URL,
                            WorkingDirectory = currentDir,
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        bridgeProcess = Process.Start(psi);
                    } catch {
                        // Si node.exe no está instalado en PATH, usará el modo WebRTC directo
                    }
                }

                // 2. Abrir la ventana dedicada del Host en Edge o Chrome
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

                // 3. Crear icono en la barra de tareas (System Tray)
                trayIcon = new NotifyIcon();
                trayIcon.Icon = SystemIcons.Application;
                trayIcon.Text = "Auditorio Control - Host Activo";
                trayIcon.Visible = true;

                ContextMenu contextMenu = new ContextMenu();
                contextMenu.MenuItems.Add("Abrir Pantalla del Auditorio", (s, e) => {
                    Process.Start(RENDER_HOST_URL);
                });
                contextMenu.MenuItems.Add("-");
                contextMenu.MenuItems.Add("Salir", (s, e) => {
                    CleanupAndExit();
                });
                trayIcon.ContextMenu = contextMenu;

                trayIcon.ShowBalloonTip(4000, "Auditorio Control", "El Host está conectado a la nube.\nEl PIN de seguridad se muestra en la ventana abierta.", ToolTipIcon.Info);

                Application.ApplicationExit += (s, e) => CleanupAndExit();
                Application.Run();

            } catch (Exception ex) {
                MessageBox.Show("Error al iniciar Auditorio Host:\n" + ex.Message, "Auditorio Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void CleanupAndExit() {
            if (bridgeProcess != null && !bridgeProcess.HasExited) {
                try { bridgeProcess.Kill(); } catch { }
            }
            if (trayIcon != null) {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            Application.Exit();
        }
    }
}
