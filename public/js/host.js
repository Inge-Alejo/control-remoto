/**
 * AUDITORIO-CONTROL: HOST BROWSER CLIENT
 * Gestiona el display del PIN y la transmisión de pantalla por navegador
 */

const displayPin = document.getElementById('displayPin');
const btnRefreshPin = document.getElementById('btnRefreshPin');
const btnShareScreen = document.getElementById('btnShareScreen');
const shareStatus = document.getElementById('shareStatus');
const staffCountDisplay = document.getElementById('staffCountDisplay');
const statusBadge = document.getElementById('statusBadge');
const statusText = document.getElementById('statusText');

let ws = null;
let mediaStream = null;
let videoTrack = null;
let captureInterval = null;
let offscreenVideo = null;
let offscreenCanvas = null;
let offscreenCtx = null;

function initHostWebSocket() {
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsUrl = `${protocol}//${window.location.host}`;

    ws = new WebSocket(wsUrl);

    ws.onopen = () => {
        statusBadge.className = 'badge badge-online';
        statusText.textContent = 'Conectado al Relay';
        ws.send(JSON.stringify({ type: 'register_host' }));
    };

    ws.onmessage = (event) => {
        try {
            const msg = JSON.parse(event.data);

            if (msg.type === 'host_registered') {
                displayPin.textContent = msg.pin;
                updateStaffCount(msg.staffCount);
            } else if (msg.type === 'staff_count_update') {
                updateStaffCount(msg.count);
            }
        } catch (e) {
            console.error('[HOST PARSE]', e);
        }
    };

    ws.onclose = () => {
        statusBadge.className = 'badge badge-offline';
        statusText.textContent = 'Desconectado';
        setTimeout(initHostWebSocket, 3000);
    };
}

function updateStaffCount(count) {
    staffCountDisplay.textContent = `${count} ${count === 1 ? 'dispositivo de staff' : 'dispositivos de staff'}`;
}

// Regenerar PIN
btnRefreshPin.addEventListener('click', async () => {
    btnRefreshPin.disabled = true;
    try {
        const res = await fetch('/api/host/new-pin', { method: 'POST' });
        const data = await res.json();
        if (data.success) {
            displayPin.textContent = data.pin;
        }
    } catch (e) {
        alert('Error al regenerar PIN');
    } finally {
        btnRefreshPin.disabled = false;
    }
});

// Transmisión de pantalla en navegador
btnShareScreen.addEventListener('click', async () => {
    if (mediaStream) {
        stopScreenSharing();
        return;
    }

    try {
        mediaStream = await navigator.mediaDevices.getDisplayMedia({
            video: {
                cursor: "always",
                frameRate: 15,
                width: { max: 1920 },
                height: { max: 1080 }
            },
            audio: false
        });

        videoTrack = mediaStream.getVideoTracks()[0];
        videoTrack.onended = () => stopScreenSharing();

        offscreenVideo = document.createElement('video');
        offscreenVideo.srcObject = mediaStream;
        offscreenVideo.muted = true;
        await offscreenVideo.play();

        offscreenCanvas = document.createElement('canvas');
        offscreenCtx = offscreenCanvas.getContext('2d');

        btnShareScreen.textContent = '⏹️ Detener Transmisión';
        btnShareScreen.className = 'btn btn-danger';
        shareStatus.textContent = '🟢 Transmitiendo pantalla al Staff';
        shareStatus.style.color = 'var(--accent-emerald)';

        // Captura a 8-10 FPS en JPEG comprimido
        captureInterval = setInterval(() => {
            if (!offscreenVideo || offscreenVideo.readyState < 2) return;

            // Escalar a resolución optimizada (máx 1280x720) para latencia nula
            const scale = Math.min(1, 1280 / offscreenVideo.videoWidth);
            offscreenCanvas.width = offscreenVideo.videoWidth * scale;
            offscreenCanvas.height = offscreenVideo.videoHeight * scale;

            offscreenCtx.drawImage(offscreenVideo, 0, 0, offscreenCanvas.width, offscreenCanvas.height);
            const dataUrl = offscreenCanvas.toDataURL('image/jpeg', 0.55);
            const base64 = dataUrl.split(',')[1];

            if (ws && ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({
                    type: 'screen_frame',
                    data: base64
                }));
            }
        }, 120);

    } catch (err) {
        console.error('Error al compartir pantalla:', err);
        shareStatus.textContent = 'Transmisión cancelada';
    }
});

function stopScreenSharing() {
    if (captureInterval) clearInterval(captureInterval);
    if (videoTrack) videoTrack.stop();
    mediaStream = null;
    videoTrack = null;

    btnShareScreen.textContent = '▶️ Compartir Pantalla';
    btnShareScreen.className = 'btn btn-primary';
    shareStatus.textContent = 'Pantalla inactiva';
    shareStatus.style.color = 'var(--text-muted)';
}

initHostWebSocket();
