@echo off
title Staff Remote Control - Cabina
color 0a
echo ========================================================
echo        AUDITORIO CONTROL - MANDO DEL STAFF (CABINA)
echo ========================================================
echo.
echo Conectando con el servidor: https://control-remoto-o5f6.onrender.com
echo Abriendo aplicacion en modo ventana dedicada...
echo.

:: Intentar abrir en modo aplicacion de escritorio nativa sin barras de navegador
where msedge >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    start msedge --app="https://control-remoto-o5f6.onrender.com/staff.html"
) else (
    where chrome >nul 2>nul
    if %ERRORLEVEL% EQU 0 (
        start chrome --app="https://control-remoto-o5f6.onrender.com/staff.html"
    ) else (
        start https://control-remoto-o5f6.onrender.com/staff.html
    )
)

echo Listo. Ya puedes operar la presentacion desde la ventana del mando.
timeout /t 3 >nul
