using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AuditorioControl {
    public class InstallerForm : Form {
        private CheckBox chkHost;
        private CheckBox chkStaff;
        private CheckBox chkDesktop;
        private CheckBox chkStartMenu;
        private Button btnInstall;
        private Label lblStatus;

        public InstallerForm() {
            InitializeComponent();
        }

        private void InitializeComponent() {
            this.Text = "Instalador - Auditorio Control";
            this.Size = new Size(460, 360);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(15, 23, 42);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            Label title = new Label {
                Text = "Instalador de Auditorio Control",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(6, 182, 212),
                Location = new Point(20, 18),
                AutoSize = true
            };
            this.Controls.Add(title);

            Label subtitle = new Label {
                Text = "Selecciona qué aplicaciones deseas instalar en este equipo:",
                Location = new Point(22, 54),
                Size = new Size(400, 30),
                ForeColor = Color.FromArgb(148, 163, 184)
            };
            this.Controls.Add(subtitle);

            chkHost = new CheckBox {
                Text = "Auditorio Host (Equipo del Escenario / Proyector)",
                Location = new Point(30, 95),
                Size = new Size(380, 26),
                Checked = true,
                ForeColor = Color.White
            };
            this.Controls.Add(chkHost);

            chkStaff = new CheckBox {
                Text = "Staff Remote (Computador o Laptop de Cabina)",
                Location = new Point(30, 125),
                Size = new Size(380, 26),
                Checked = true,
                ForeColor = Color.White
            };
            this.Controls.Add(chkStaff);

            Label sep = new Label {
                BorderStyle = BorderStyle.Fixed3D,
                Location = new Point(22, 165),
                Size = new Size(400, 2)
            };
            this.Controls.Add(sep);

            chkDesktop = new CheckBox {
                Text = "Crear acceso directo en el Escritorio",
                Location = new Point(30, 180),
                Size = new Size(380, 24),
                Checked = true,
                ForeColor = Color.FromArgb(203, 213, 225)
            };
            this.Controls.Add(chkDesktop);

            chkStartMenu = new CheckBox {
                Text = "Agregar al Menú Inicio de Windows",
                Location = new Point(30, 208),
                Size = new Size(380, 24),
                Checked = true,
                ForeColor = Color.FromArgb(203, 213, 225)
            };
            this.Controls.Add(chkStartMenu);

            lblStatus = new Label {
                Text = "",
                Location = new Point(22, 245),
                Size = new Size(270, 45),
                ForeColor = Color.FromArgb(16, 185, 129)
            };
            this.Controls.Add(lblStatus);

            btnInstall = new Button {
                Text = "Instalar Ahora",
                Location = new Point(300, 250),
                Size = new Size(125, 40),
                BackColor = Color.FromArgb(6, 182, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnInstall.FlatAppearance.BorderSize = 0;
            btnInstall.Click += BtnInstall_Click;
            this.Controls.Add(btnInstall);
        }

        private void BtnInstall_Click(object sender, EventArgs e) {
            btnInstall.Enabled = false;
            lblStatus.Text = "Instalando archivos...";

            try {
                string installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuditorioControl");
                if (!Directory.Exists(installDir)) {
                    Directory.CreateDirectory(installDir);
                }

                string sourceDir = AppDomain.CurrentDomain.BaseDirectory;

                // Copiar ejecutables y archivos del host si existen
                string[] filesToCopy = new string[] {
                    "Auditorio-Host.exe",
                    "Staff-Control.exe",
                    "host-bridge.js",
                    "win-input-worker.ps1"
                };

                foreach (string file in filesToCopy) {
                    string src = Path.Combine(sourceDir, file);
                    string dst = Path.Combine(installDir, file);
                    if (File.Exists(src)) {
                        File.Copy(src, dst, true);
                    }
                }

                // Crear accesos directos
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string startMenuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Auditorio Control");

                if (chkStartMenu.Checked && !Directory.Exists(startMenuPath)) {
                    Directory.CreateDirectory(startMenuPath);
                }

                if (chkHost.Checked) {
                    string hostExe = Path.Combine(installDir, "Auditorio-Host.exe");
                    if (chkDesktop.Checked) CreateShortcut(Path.Combine(desktopPath, "Auditorio Host.lnk"), hostExe, "Iniciar equipo del Auditorio");
                    if (chkStartMenu.Checked) CreateShortcut(Path.Combine(startMenuPath, "Auditorio Host.lnk"), hostExe, "Iniciar equipo del Auditorio");
                }

                if (chkStaff.Checked) {
                    string staffExe = Path.Combine(installDir, "Staff-Control.exe");
                    if (chkDesktop.Checked) CreateShortcut(Path.Combine(desktopPath, "Staff Remote.lnk"), staffExe, "Control Remoto de Staff");
                    if (chkStartMenu.Checked) CreateShortcut(Path.Combine(startMenuPath, "Staff Remote.lnk"), staffExe, "Control Remoto de Staff");
                }

                lblStatus.Text = "¡Instalación exitosa!";
                MessageBox.Show("¡Auditorio Control se ha instalado correctamente!\n\nPuedes abrirlo desde tu Escritorio o el Menú Inicio.", "Instalación Completada", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();

            } catch (Exception ex) {
                lblStatus.ForeColor = Color.FromArgb(244, 63, 94);
                lblStatus.Text = "Error en la instalación.";
                MessageBox.Show("Error durante la instalación:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnInstall.Enabled = true;
            }
        }

        private void CreateShortcut(string shortcutPath, string targetPath, string description) {
            try {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Description = description;
                shortcut.Save();
            } catch {
                // Si falla COM, ignorar silenciosamente
            }
        }

        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }
    }
}
