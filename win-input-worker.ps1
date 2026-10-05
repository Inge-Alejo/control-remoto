# AUDITORIO-CONTROL: Windows Native Input Worker
# Corre como subproceso de Node.js via stdin/stdout
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# P/Invoke para clics de mouse de bajo nivel en user32.dll
$signature = @"
using System;
using System.Runtime.InteropServices;
public class Win32Input {
    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
"@
# Solo agregar tipo si no existe
if (-not ([System.Management.Automation.PSTypeName]'Win32Input').Type) {
    try {
        Add-Type -TypeDefinition $signature -ErrorAction SilentlyContinue
    } catch {
        # Si falla por permisos, usaremos System.Windows.Forms fallback
    }
}

$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$screenWidth = $screen.Width
$screenHeight = $screen.Height

Write-Output "READY:$screenWidth:$screenHeight"

$stdin = [Console]::In
while ($null -ne ($line = $stdin.ReadLine())) {
    $line = $line.Trim()
    if ($line.Length -eq 0) { continue }
    
    $parts = $line -split ' '
    $cmd = $parts[0]

    try {
        switch ($cmd) {
            "MOVE" {
                $targetX = [int]$parts[1]
                $targetY = [int]$parts[2]
                [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point($targetX, $targetY)
            }
            "CLICK" {
                $btn = $parts[1]
                $isDouble = if ($parts.Length -gt 2) { $parts[2] -eq "1" } else { $false }
                
                # Constantes Win32:
                # MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004
                # MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010
                if (([System.Management.Automation.PSTypeName]'Win32Input').Type) {
                    if ($btn -eq "right") {
                        [Win32Input]::mouse_event(0x0008, 0, 0, 0, [UIntPtr]::Zero)
                        [Win32Input]::mouse_event(0x0010, 0, 0, 0, [UIntPtr]::Zero)
                    } else {
                        [Win32Input]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
                        [Win32Input]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
                        if ($isDouble) {
                            Start-Sleep -Milliseconds 60
                            [Win32Input]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
                            [Win32Input]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
                        }
                    }
                }
            }
            "VOL_UP" {
                if (([System.Management.Automation.PSTypeName]'Win32Input').Type) {
                    [Win32Input]::keybd_event(0xAF, 0, 0, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0xAF, 0, 2, [UIntPtr]::Zero)
                }
            }
            "VOL_DOWN" {
                if (([System.Management.Automation.PSTypeName]'Win32Input').Type) {
                    [Win32Input]::keybd_event(0xAE, 0, 0, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0xAE, 0, 2, [UIntPtr]::Zero)
                }
            }
            "VOL_MUTE" {
                if (([System.Management.Automation.PSTypeName]'Win32Input').Type) {
                    [Win32Input]::keybd_event(0xAD, 0, 0, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0xAD, 0, 2, [UIntPtr]::Zero)
                }
            }
            "WIN_D" {
                if (([System.Management.Automation.PSTypeName]'Win32Input').Type) {
                    [Win32Input]::keybd_event(0x5B, 0, 0, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0x44, 0, 0, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0x44, 0, 2, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0x5B, 0, 2, [UIntPtr]::Zero)
                }
            }
            "ALT_TAB" {
                if (([System.Management.Automation.PSTypeName]'Win32Input').Type) {
                    [Win32Input]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0x09, 0, 0, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0x09, 0, 2, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
                }
            }
            "ALT_F4" {
                if (([System.Management.Automation.PSTypeName]'Win32Input').Type) {
                    [Win32Input]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0x73, 0, 0, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0x73, 0, 2, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
                }
            }
            "WIN_P" {
                if (([System.Management.Automation.PSTypeName]'Win32Input').Type) {
                    [Win32Input]::keybd_event(0x5B, 0, 0, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0x50, 0, 0, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0x50, 0, 2, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0x5B, 0, 2, [UIntPtr]::Zero)
                }
            }
            "F11_FULLSCREEN" {
                if (([System.Management.Automation.PSTypeName]'Win32Input').Type) {
                    [Win32Input]::keybd_event(0x7A, 0, 0, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0x7A, 0, 2, [UIntPtr]::Zero)
                }
            }
            "RELOAD_F5" {
                if (([System.Management.Automation.PSTypeName]'Win32Input').Type) {
                    [Win32Input]::keybd_event(0x74, 0, 0, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0x74, 0, 2, [UIntPtr]::Zero)
                }
            }
            "PANIC_RESET" {
                if (([System.Management.Automation.PSTypeName]'Win32Input').Type) {
                    # Mute
                    [Win32Input]::keybd_event(0xAD, 0, 0, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0xAD, 0, 2, [UIntPtr]::Zero)
                    # Win+D (Minimizar todo de inmediato)
                    [Win32Input]::keybd_event(0x5B, 0, 0, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0x44, 0, 0, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0x44, 0, 2, [UIntPtr]::Zero)
                    [Win32Input]::keybd_event(0x5B, 0, 2, [UIntPtr]::Zero)
                }
            }
            "KEY" {
                $keyPayload = if ($line.Length -gt 4) { $line.Substring(4) } else { "" }
                if ($keyPayload.Length -gt 0) {
                    if ($keyPayload.StartsWith("{") -and $keyPayload.EndsWith("}")) {
                        [System.Windows.Forms.SendKeys]::SendWait($keyPayload)
                    } else {
                        # Escapar caracteres de control de SendKeys (+, ^, %, ~, (, ))
                        $escaped = $keyPayload -replace '([+^%~{}()])', '{$1}'
                        [System.Windows.Forms.SendKeys]::SendWait($escaped)
                    }
                }
            }
            "CAPTURE" {
                # Captura de pantalla para streaming
                $bmp = New-Object System.Drawing.Bitmap $screenWidth, $screenHeight
                $g = [System.Drawing.Graphics]::FromImage($bmp)
                try {
                    $g.CopyFromScreen($screen.Location, [System.Drawing.Point]::Empty, $screen.Size)
                    $ms = New-Object System.IO.MemoryStream
                    
                    # Guardar como JPEG con calidad optimizada (50%)
                    $codecs = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders()
                    $jpeg = $null
                    foreach ($c in $codecs) {
                        if ($c.FormatID -eq [System.Drawing.Imaging.ImageFormat]::Jpeg.Guid) {
                            $jpeg = $c
                            break
                        }
                    }
                    $ep = New-Object System.Drawing.Imaging.EncoderParameters 1
                    $ep.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality, [long]50)
                    $bmp.Save($ms, $jpeg, $ep)
                    
                    $bytes = $ms.ToArray()
                    $b64 = [Convert]::ToBase64String($bytes)
                    Write-Output "FRAME:$b64"
                    $ms.Dispose()
                } catch {
                    Write-Output "CAPTURE_ERROR"
                } finally {
                    $g.Dispose()
                    $bmp.Dispose()
                }
            }
            "EXIT" {
                break
            }
        }
    } catch {
        # Loggear error silenciosamente sin tumbar el subproceso
    }
}
