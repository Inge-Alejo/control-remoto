/**
 * AUDITORIO-CONTROL: STAFF CLIENT JAVASCRIPT
 * Lógica de conexión, autenticación por PIN, gestos táctiles y transmisión
 */

let authToken = sessionStorage.getItem('auditorio_token') || null;
let ws = null;
let lastPingTime = 0;
let pingInterval = null;

// DOM Elements
const pinModal = document.getElementById('pinModal');
const pinCard = document.getElementById('pinCard');
const pinInput = document.getElementById('pinInput');
const pinError = document.getElementById('pinError');
const btnSubmitPin = document.getElementById('btnSubmitPin');
const hostBadge = document.getElementById('hostBadge');
const hostStatusText = document.getElementById('hostStatusText');
const pingTag = document.getElementById('pingTag');
const btnLock = document.getElementById('btnLock');

// Tabs
const tabButtons = document.querySelectorAll('.tab-btn');
const viewPanels = document.querySelectorAll('.view-panel');

// Screen & Trackpad
const screenCanvas = document.getElementById('screenCanvas');
const screenPlaceholder = document.getElementById('screenPlaceholder');
const screenPlaceholderText = document.getElementById('screenPlaceholderText');
const trackpadArea = document.getElementById('trackpadArea');

// --- HAPTIC FEEDBACK ---
function vibrate(ms = 25) {
    if (navigator.vibrate) {
        try { navigator.vibrate(ms); } catch (e) {}
    }
}

// --- TAB SWITCHING ---
tabButtons.forEach(btn => {
    btn.addEventListener('click', () => {
        vibrate(15);
        const tab = btn.dataset.tab;
        tabButtons.forEach(b => b.classList.remove('active'));
        viewPanels.forEach(p => p.classList.remove('active'));

        btn.classList.add('active');
        const targetPanel = document.getElementById(`panel-${tab}`);
        if (targetPanel) targetPanel.classList.add('active');
    });
});

// --- AUTHENTICATION & PIN ---
if (!authToken) {
    showPinModal();
} else {
    initWebSocket();
}

function showPinModal() {
    pinModal.style.display = 'flex';
    pinInput.value = '';
    pinError.textContent = '';
    setTimeout(() => pinInput.focus(), 200);
}

function hidePinModal() {
    pinModal.style.display = 'none';
}

btnSubmitPin.addEventListener('click', submitPin);
pinInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') submitPin();
});

async function submitPin() {
    const pin = pinInput.value.trim();
    if (pin.length !== 6) {
        showPinError('El PIN debe tener 6 dígitos');
        return;
    }

    btnSubmitPin.disabled = true;
    btnSubmitPin.textContent = 'Verificando...';
    pinError.textContent = '';

    try {
        const res = await fetch('/api/auth/staff', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ pin })
        });

        const data = await res.json();
        if (res.ok && data.success) {
            vibrate(50);
            authToken = data.token;
            sessionStorage.setItem('auditorio_token', authToken);
            hidePinModal();
            initWebSocket();
        } else {
            showPinError(data.error || 'PIN incorrecto');
        }
    } catch (err) {
        showPinError('Error de red al verificar el PIN');
    } finally {
        btnSubmitPin.disabled = false;
        btnSubmitPin.textContent = 'Ingresar al Control';
    }
}

function showPinError(msg) {
    vibrate([40, 60, 40]);
    pinError.textContent = msg;
    pinCard.classList.add('shake');
    setTimeout(() => pinCard.classList.remove('shake'), 400);
}

btnLock.addEventListener('click', () => {
    sessionStorage.removeItem('auditorio_token');
    authToken = null;
    if (ws) ws.close();
    showPinModal();
});

// --- WEBSOCKET CONNECTION ---
function initWebSocket() {
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsUrl = `${protocol}//${window.location.host}`;

    ws = new WebSocket(wsUrl);

    ws.onopen = () => {
        console.log('[WS] Conectado al relay');
        // Autenticar la sesión de Staff en el WebSocket
        ws.send(JSON.stringify({
            type: 'register_staff',
            token: authToken
        }));

        startPingMeter();
    };

    ws.onmessage = (event) => {
        try {
            const msg = JSON.parse(event.data);

            if (msg.type === 'staff_registered') {
                updateHostStatus(msg.hostConnected);
            } else if (msg.type === 'host_status') {
                updateHostStatus(msg.connected);
            } else if (msg.type === 'screen_frame') {
                renderScreenFrame(msg.data);
            } else if (msg.type === 'session_reset') {
                alert(msg.message || 'Sesión finalizada por el Host');
                sessionStorage.removeItem('auditorio_token');
                authToken = null;
                showPinModal();
            } else if (msg.type === 'pong') {
                const latency = Date.now() - lastPingTime;
                pingTag.textContent = `${latency} ms`;
            }
        } catch (e) {
            console.error('[WS PARSE]', e);
        }
    };

    ws.onclose = () => {
        updateHostStatus(false);
        stopPingMeter();
        console.log('[WS] Desconectado. Reintentando en 3s...');
        if (authToken) {
            setTimeout(initWebSocket, 3000);
        }
    };

    ws.onerror = (err) => {
        console.error('[WS ERROR]', err);
    };
}

function updateHostStatus(isOnline) {
    if (isOnline) {
        hostBadge.className = 'badge badge-online';
        hostStatusText.textContent = 'Host Conectado';
        screenPlaceholderText.textContent = 'Conectado. Recibiendo pantalla...';
    } else {
        hostBadge.className = 'badge badge-offline';
        hostStatusText.textContent = 'Host Offline';
        screenPlaceholderText.textContent = 'El equipo del Auditorio no está conectado';
        screenCanvas.style.display = 'none';
        screenPlaceholder.style.display = 'flex';
    }
}

function renderScreenFrame(base64Data) {
    screenCanvas.src = `data:image/jpeg;base64,${base64Data}`;
    screenCanvas.style.display = 'block';
    screenPlaceholder.style.display = 'none';
}

function sendAction(payload) {
    if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify(payload));
    }
}

function sendDeckAction(action) {
    vibrate(30);
    sendAction({ type: 'deck_action', action });
}

// Latency Ping Meter
function startPingMeter() {
    if (pingInterval) clearInterval(pingInterval);
    pingInterval = setInterval(() => {
        if (ws && ws.readyState === WebSocket.OPEN) {
            lastPingTime = Date.now();
            ws.send(JSON.stringify({ type: 'ping' }));
        }
    }, 4000);
}

function stopPingMeter() {
    if (pingInterval) clearInterval(pingInterval);
    pingTag.textContent = '-- ms';
}

// --- AV CONSOLE & WINDOW CONTROLS BINDING ---
const bindClick = (id, action) => {
    const el = document.getElementById(id);
    if (el) el.addEventListener('click', () => sendDeckAction(action));
};

bindClick('btnPanic', 'PANIC_RESET');
bindClick('btnShowDesktop', 'SHOW_DESKTOP');
bindClick('btnAltTab', 'ALT_TAB');
bindClick('btnCloseWindow', 'CLOSE_WINDOW');
bindClick('btnProjector', 'PROJECTOR_SWITCH');
bindClick('btnReload', 'RELOAD_PAGE');
bindClick('btnFullscreen', 'FULLSCREEN');

bindClick('btnVolDown', 'VOLUME_DOWN');
bindClick('btnVolUp', 'VOLUME_UP');
bindClick('btnMute', 'VOLUME_MUTE');

bindClick('btnSeekBack', 'SEEK_BACK');
bindClick('btnPlayPause', 'MEDIA_PLAY_PAUSE');
bindClick('btnSeekFwd', 'SEEK_FWD');

// Doble clic botón de soporte
const btnDoubleTap = document.getElementById('btnDoubleTap');
if (btnDoubleTap) {
    btnDoubleTap.addEventListener('click', () => {
        vibrate(30);
        sendAction({ type: 'mouse_click', button: 'left', double: true });
    });
}

// --- TRACKPAD & TOUCH CONTROLS ---
let touchStartX = 0;
let touchStartY = 0;
let lastTouchX = 0;
let lastTouchY = 0;
let touchStartTime = 0;
let currentNormX = 0.5;
let currentNormY = 0.5;

trackpadArea.addEventListener('touchstart', (e) => {
    if (e.touches.length === 1) {
        touchStartX = e.touches[0].clientX;
        touchStartY = e.touches[0].clientY;
        lastTouchX = touchStartX;
        lastTouchY = touchStartY;
        touchStartTime = Date.now();
    } else if (e.touches.length === 2) {
        // Dos dedos = Clic derecho
        vibrate(40);
        sendAction({ type: 'mouse_click', button: 'right' });
    }
}, { passive: true });

trackpadArea.addEventListener('touchmove', (e) => {
    if (e.touches.length === 1) {
        const curX = e.touches[0].clientX;
        const curY = e.touches[0].clientY;
        const deltaX = (curX - lastTouchX) * 1.5;
        const deltaY = (curY - lastTouchY) * 1.5;

        lastTouchX = curX;
        lastTouchY = curY;

        // Sensibilidad normalizada relativa al tamaño del trackpad
        const rect = trackpadArea.getBoundingClientRect();
        currentNormX = Math.max(0, Math.min(1, currentNormX + (deltaX / rect.width)));
        currentNormY = Math.max(0, Math.min(1, currentNormY + (deltaY / rect.height)));

        sendAction({
            type: 'mouse_move',
            x: currentNormX,
            y: currentNormY
        });
    }
}, { passive: true });

trackpadArea.addEventListener('touchend', (e) => {
    const touchDuration = Date.now() - touchStartTime;
    const dist = Math.hypot(lastTouchX - touchStartX, lastTouchY - touchStartY);

    // Si fue un toque rápido sin mover el dedo (< 200ms y < 10px), es un clic izquierdo
    if (touchDuration < 250 && dist < 10) {
        vibrate(25);
        sendAction({ type: 'mouse_click', button: 'left' });
    }
});

// Direct Trackpad click buttons
document.getElementById('btnLeftClick').addEventListener('click', () => {
    vibrate(25);
    sendAction({ type: 'mouse_click', button: 'left' });
});

document.getElementById('btnRightClick').addEventListener('click', () => {
    vibrate(35);
    sendAction({ type: 'mouse_click', button: 'right' });
});

// Clicking directly on the screen preview image to move cursor to that exact location
screenCanvas.addEventListener('click', (e) => {
    const rect = screenCanvas.getBoundingClientRect();
    const x = (e.clientX - rect.left) / rect.width;
    const y = (e.clientY - rect.top) / rect.height;

    if (x >= 0 && x <= 1 && y >= 0 && y <= 1) {
        vibrate(25);
        currentNormX = x;
        currentNormY = y;
        sendAction({ type: 'mouse_move', x, y });
        sendAction({ type: 'mouse_click', button: 'left' });
    }
});

// --- KEYBOARD & TEXT MACROS ---
const textInput = document.getElementById('textInput');
const btnSendText = document.getElementById('btnSendText');

btnSendText.addEventListener('click', () => {
    const text = textInput.value;
    if (text) {
        vibrate(20);
        for (const char of text) {
            sendAction({ type: 'key_press', key: char });
        }
        textInput.value = '';
    }
});

textInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
        btnSendText.click();
    }
});

document.getElementById('btnKeyEnter').addEventListener('click', () => {
    vibrate(20);
    sendAction({ type: 'key_press', key: '{ENTER}' });
});
const btnKeyEsc = document.getElementById('btnKeyEsc');
if (btnKeyEsc) {
    btnKeyEsc.addEventListener('click', () => {
        vibrate(20);
        sendAction({ type: 'key_press', key: '{ESC}' });
    });
}
document.getElementById('btnKeyBackspace').addEventListener('click', () => {
    vibrate(20);
    sendAction({ type: 'key_press', key: '{BACKSPACE}' });
});
document.getElementById('btnKeyTab').addEventListener('click', () => {
    vibrate(20);
    sendAction({ type: 'key_press', key: '{TAB}' });
});
