/**
 * AUDITORIO-CONTROL: HOST DESKTOP AGENT (AUDITORIO PC)
 * Se ejecuta en el computador del escenario / proyector.
 * 
 * Uso:
 *   node host-bridge.js [url_websocket_opcional]
 *   Ejemplo local: node host-bridge.js
 *   Ejemplo remoto (Nube): node host-bridge.js wss://mi-servidor.onrender.com
 */

const { spawn } = require('child_process');
const path = require('path');
const { WebSocket } = require('ws');

const RELAY_URL = process.argv[2] || process.env.RELAY_URL || 'ws://localhost:3000';
console.log(`[INICIO] Conectando Host al servidor: ${RELAY_URL}`);

let screenWidth = 1920;
let screenHeight = 1080;
let psProcess = null;
let ws = null;
let isConnected = false;
let captureInterval = null;

// Inicializar el subproceso de control nativo de Windows (PowerShell)
function startPowerShellWorker() {
    const scriptPath = path.join(__dirname, 'win-input-worker.ps1');
    console.log('[SISTEMA] Iniciando worker nativo de Windows para mouse/teclado...');
    
    psProcess = spawn('powershell.exe', [
        '-NoProfile',
        '-NonInteractive',
        '-ExecutionPolicy', 'Bypass',
        '-File', scriptPath
    ]);

    psProcess.stdout.on('data', (data) => {
        const text = data.toString();
        const lines = text.split('\r\n');
        
        for (const line of lines) {
            if (line.startsWith('READY:')) {
                const parts = line.split(':');
                screenWidth = parseInt(parts[1], 10) || 1920;
                screenHeight = parseInt(parts[2], 10) || 1080;
                console.log(`[PANTALLA] Resolución detectada: ${screenWidth}x${screenHeight}`);
            } else if (line.startsWith('FRAME:')) {
                const b64 = line.substring(6);
                if (ws && ws.readyState === WebSocket.OPEN) {
                    ws.send(JSON.stringify({ type: 'screen_frame', data: b64 }));
                }
            }
        }
    });

    psProcess.stderr.on('data', (data) => {
        // Ignorar avisos no críticos de powershell
    });

    psProcess.on('close', (code) => {
        console.log(`[WORKER] Worker de Windows cerrado con código ${code}.`);
        if (isConnected) {
            setTimeout(startPowerShellWorker, 2000);
        }
    });
}

function sendToWorker(cmd) {
    if (psProcess && psProcess.stdin && psProcess.stdin.writable) {
        psProcess.stdin.write(cmd + '\n');
    }
}

// Conexión WebSocket al servidor Relay
function connectRelay() {
    console.log(`[RED] Conectando a ${RELAY_URL}...`);
    ws = new WebSocket(RELAY_URL);

    ws.on('open', () => {
        console.log('[RED] Conexión establecida con el servidor relay.');
        isConnected = true;
        // Registrarse como Host
        ws.send(JSON.stringify({ type: 'register_host' }));
        
        // Iniciar captura periódica ligera (ej. 2-3 fps como vista previa de diapositivas)
        if (captureInterval) clearInterval(captureInterval);
        captureInterval = setInterval(() => {
            sendToWorker('CAPTURE');
        }, 1200);
    });

    ws.on('message', (raw) => {
        try {
            const msg = JSON.parse(raw.toString());

            if (msg.type === 'host_registered') {
                console.log('========================================================');
                console.log(`✅ EQUIPO DEL AUDITORIO CONECTADO EXITOSAMENTE`);
                console.log(`🔑 PIN DE SEGURIDAD PARA EL STAFF: [ ${msg.pin} ]`);
                console.log(`👥 Miembros del staff conectados: ${msg.staffCount}`);
                console.log('========================================================');
                return;
            }

            if (msg.type === 'staff_count_update') {
                console.log(`[STAFF] Miembros del staff en línea: ${msg.count}`);
                return;
            }

            // Manejo de eventos de control remoto desde el staff
            if (msg.type === 'mouse_move') {
                const pixelX = Math.round(msg.x * screenWidth);
                const pixelY = Math.round(msg.y * screenHeight);
                sendToWorker(`MOVE ${pixelX} ${pixelY}`);
            } else if (msg.type === 'mouse_click') {
                const btn = msg.button === 'right' ? 'right' : 'left';
                const dbl = msg.double ? '1' : '0';
                sendToWorker(`CLICK ${btn} ${dbl}`);
            } else if (msg.type === 'deck_action') {
                handleDeckAction(msg.action);
            } else if (msg.type === 'key_press') {
                sendToWorker(`KEY ${msg.key}`);
            }

        } catch (e) {
            console.error('[MSG ERROR]', e.message);
        }
    });

    ws.on('close', () => {
        console.log('[RED] Conexión perdida con el servidor. Reconectando en 3 segundos...');
        isConnected = false;
        if (captureInterval) clearInterval(captureInterval);
        setTimeout(connectRelay, 3000);
    });

    ws.on('error', (err) => {
        console.log(`[ERROR RED] ${err.message}`);
    });
}

function handleDeckAction(action) {
    console.log(`[ACCIÓN AUDITORIO] ${action}`);
    switch (action) {
        case 'NEXT_SLIDE':
            // Flecha derecha o Page Down (Avanzar diapositiva)
            sendToWorker('KEY {RIGHT}');
            break;
        case 'PREV_SLIDE':
            // Flecha izquierda o Page Up (Retroceder diapositiva)
            sendToWorker('KEY {LEFT}');
            break;
        case 'BLACKOUT':
            // Tecla 'B' en PowerPoint/Keynote pone la pantalla en negro
            sendToWorker('KEY b');
            break;
        case 'WHITEOUT':
            // Tecla 'W' en PowerPoint pone la pantalla en blanco
            sendToWorker('KEY w');
            break;
        case 'START_F5':
            // F5 inicia la presentación desde el principio
            sendToWorker('KEY {F5}');
            break;
        case 'STOP_ESC':
            // Escape sale del modo presentación
            sendToWorker('KEY {ESC}');
            break;
        case 'MEDIA_PLAY_PAUSE':
            // Barra espaciadora pausa/reproduce videos o diapositivas
            sendToWorker('KEY {SPACE}');
            break;
        case 'VOLUME_UP':
            // Simular tecla de volumen arriba multimedia
            sendToWorker('KEY ^({UP})');
            break;
        case 'VOLUME_DOWN':
            // Simular tecla de volumen abajo multimedia
            sendToWorker('KEY ^({DOWN})');
            break;
        case 'VOLUME_MUTE':
            sendToWorker('KEY m');
            break;
    }
}

// Iniciar procesos
startPowerShellWorker();
connectRelay();

// Manejo de salida limpia
process.on('SIGINT', () => {
    console.log('\n[SALIDA] Deteniendo agente del auditorio...');
    if (psProcess) {
        sendToWorker('EXIT');
        psProcess.kill();
    }
    if (ws) ws.close();
    process.exit(0);
});
