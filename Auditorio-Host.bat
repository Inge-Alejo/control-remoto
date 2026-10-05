@echo off
title Auditorio Control - Host (Escenario)
color 0b
echo ========================================================
echo        AUDITORIO CONTROL - EQUIPO DEL ESCENARIO
echo ========================================================
echo.
echo [1/2] Conectando con el servidor en la nube...
echo Servidor: https://control-remoto-o5f6.onrender.com
echo.

:: Verificar si Node.js esta instalado para el agente nativo
where node >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    echo [2/2] Iniciando Agente Nativo de Windows (PowerPoint/Mouse)...
    start "Auditorio-Bridge" /min node host-bridge.js wss://control-remoto-o5f6.onrender.com
) else (
    echo [AVISO] Node.js no detectado en este equipo.
    echo Se utilizara el modo WebRTC directo por navegador (Cero Instalacion).
)

echo.
echo Abriendo ventana del portal del Auditorio...
:: Intentar abrir en modo aplicacion dedicada en Edge o Chrome
where msedge >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    start msedge --app="https://control-remoto-o5f6.onrender.com/host.html"
) else (
    where chrome >nul 2>nul
    if %ERRORLEVEL% EQU 0 (
        start chrome --app="https://control-remoto-o5f6.onrender.com/host.html"
    ) else (
        start https://control-remoto-o5f6.onrender.com/host.html
    )
)

echo.
echo ========================================================
echo  SISTEMA ACTIVO:
echo  - Copia el PIN de 6 digitos que aparece en la ventana.
echo  - Entregaselo al staff de cabina.
echo ========================================================
timeout /t 5 >nul
