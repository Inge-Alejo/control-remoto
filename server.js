/**
 * AUDITORIO-CONTROL: RELAY & WEB SERVER
 * Arquitectura de Grado Senior - Cero Costo, Alta Seguridad
 * 
 * - Servidor Express con WebSockets nativos (ws)
 * - Protección contra fuerza bruta (Rate Limiting)
 * - Validación estricta de esquemas de entrada (Anti-Inyección)
 * - Tokens de sesión con firma HMAC-SHA256
 * - Compatible con HTTPS/WSS (puerto 443 en la nube: Render/Railway/Fly.io)
 */

const express = require('express');
const http = require('http');
const path = require('path');
const crypto = require('crypto');
const { WebSocketServer, WebSocket } = require('ws');

const PORT = process.env.PORT || 3000;
const SESSION_SECRET = process.env.SESSION_SECRET || crypto.randomBytes(32).toString('hex');

const app = express();
const server = http.createServer(app);
const wss = new WebSocketServer({ server });

// Security Middlewares
app.use(express.json({ limit: '1mb' }));
app.use((req, res, next) => {
    res.setHeader('X-Content-Type-Options', 'nosniff');
    res.setHeader('X-Frame-Options', 'DENY');
    res.setHeader('X-XSS-Protection', '1; mode=block');
    res.setHeader('Referrer-Policy', 'no-referrer');
    next();
});

// Servir archivos estáticos del frontend
app.use(express.static(path.join(__dirname, 'public')));

// Health check para Render y servicios en la nube
app.get('/healthz', (req, res) => res.status(200).send('OK'));

// Endpoint de versión para verificar actualizaciones
app.get('/api/version', (req, res) => {
    res.json({
        version: '1.1.0',
        appName: 'Auditorio Control',
        renderStatus: 'online',
        lastCommit: 'latest'
    });
});

// Estado global de la sesión del auditorio
const session = {
    pin: generatePin(),
    createdAt: Date.now(),
    hostWs: null,
    staffSockets: new Set(),
    screenFrame: null, // Último fotograma en base64 para sincronización instantánea
    failedAttempts: new Map(), // IP -> { count, lockedUntil }
};

function generatePin() {
    // Generar un PIN aleatorio de 6 dígitos criptográficamente seguro
    return (crypto.randomInt(100000, 999999)).toString();
}

function signToken(payload) {
    const data = JSON.stringify(payload);
    const signature = crypto.createHmac('sha256', SESSION_SECRET).update(data).digest('hex');
    return Buffer.from(JSON.stringify({ data, signature })).toString('base64');
}

function verifyToken(token) {
    try {
        const decoded = JSON.parse(Buffer.from(token, 'base64').toString('utf8'));
        const expectedSig = crypto.createHmac('sha256', SESSION_SECRET).update(decoded.data).digest('hex');
        if (crypto.timingSafeEqual(Buffer.from(decoded.signature), Buffer.from(expectedSig))) {
            return JSON.parse(decoded.data);
        }
    } catch (e) {
        return null;
    }
    return null;
}

// Rate limiting para prevención de fuerza bruta
function checkRateLimit(ip) {
    const now = Date.now();
    const record = session.failedAttempts.get(ip) || { count: 0, lockedUntil: 0 };
    if (now < record.lockedUntil) {
        const remainingSec = Math.ceil((record.lockedUntil - now) / 1000);
        return { allowed: false, remainingSec };
    }
    return { allowed: true };
}

function recordFailedAttempt(ip) {
    const now = Date.now();
    const record = session.failedAttempts.get(ip) || { count: 0, lockedUntil: 0 };
    record.count += 1;
    if (record.count >= 5) {
        record.lockedUntil = now + (60 * 1000); // 60 segundos de bloqueo tras 5 fallos
        record.count = 0;
    }
    session.failedAttempts.set(ip, record);
}

// Endpoint de autenticación para Staff
app.post('/api/auth/staff', (req, res) => {
    const clientIp = req.headers['x-forwarded-for'] || req.socket.remoteAddress;
    const { pin } = req.body;

    const rateCheck = checkRateLimit(clientIp);
    if (!rateCheck.allowed) {
        return res.status(429).json({
            error: `Demasiados intentos fallidos. Bloqueado por ${rateCheck.remainingSec} segundos.`
        });
    }

    if (!pin || typeof pin !== 'string' || pin.trim() !== session.pin) {
        recordFailedAttempt(clientIp);
        return res.status(401).json({ error: 'PIN de acceso incorrecto' });
    }

    // PIN Válido: Limpiar intentos y emitir token firmado
    session.failedAttempts.delete(clientIp);
    const token = signToken({ role: 'staff', time: Date.now() });
    return res.json({ success: true, token });
});

// Endpoint para consultar estado general (sin revelar el PIN)
app.get('/api/status', (req, res) => {
    res.json({
        hostConnected: session.hostWs !== null && session.hostWs.readyState === WebSocket.OPEN,
        staffCount: session.staffSockets.size,
        sessionAgeSeconds: Math.floor((Date.now() - session.createdAt) / 1000)
    });
});

// Endpoint administrativo para el Host local para ver/regenerar PIN
// Endpoint administrativo para regenerar PIN
app.post('/api/host/new-pin', (req, res) => {
    session.pin = generatePin();
    // Desconectar a los staff actuales para que reingresen el nuevo PIN
    for (const ws of session.staffSockets) {
        ws.send(JSON.stringify({ type: 'session_reset', message: 'El PIN de seguridad fue cambiado por el Host' }));
        ws.close();
    }
    session.staffSockets.clear();
    console.log(`[SEGURIDAD] Nuevo PIN de Auditorio generado: ${session.pin}`);
    res.json({ success: true, pin: session.pin });
});

// WebSocket Relay & Control Handler
wss.on('connection', (ws, req) => {
    let clientRole = null;
    let isAuthenticated = false;

    ws.isAlive = true;
    ws.on('pong', () => { ws.isAlive = true; });

    ws.on('message', (raw) => {
        try {
            const msg = JSON.parse(raw.toString());

            // 1. Manejo de Registro Inicial
            if (msg.type === 'register_host') {
                // Registrar como Host (Equipo del auditorio)
                if (session.hostWs && session.hostWs !== ws && session.hostWs.readyState === WebSocket.OPEN) {
                    session.hostWs.send(JSON.stringify({ type: 'replaced', message: 'Otro host se ha conectado' }));
                    session.hostWs.close();
                }
                session.hostWs = ws;
                clientRole = 'host';
                isAuthenticated = true;
                ws.send(JSON.stringify({
                    type: 'host_registered',
                    pin: session.pin,
                    staffCount: session.staffSockets.size
                }));
                broadcastToStaff({ type: 'host_status', connected: true });
                console.log(`[HOST] Host del auditorio conectado.`);
                return;
            }

            if (msg.type === 'regenerate_pin') {
                session.pin = generatePin();
                for (const client of session.staffSockets) {
                    client.send(JSON.stringify({ type: 'session_reset', message: 'El PIN de seguridad fue cambiado por el Host' }));
                    client.close();
                }
                session.staffSockets.clear();
                console.log(`[SEGURIDAD] Nuevo PIN generado: ${session.pin}`);
                ws.send(JSON.stringify({
                    type: 'host_registered',
                    pin: session.pin,
                    staffCount: 0
                }));
                return;
            }

            if (msg.type === 'register_staff') {
                const tokenData = verifyToken(msg.token);
                if (!tokenData || tokenData.role !== 'staff') {
                    ws.send(JSON.stringify({ type: 'error', message: 'Token de Staff inválido o expirado' }));
                    ws.close();
                    return;
                }
                clientRole = 'staff';
                isAuthenticated = true;
                session.staffSockets.add(ws);
                ws.send(JSON.stringify({
                    type: 'staff_registered',
                    hostConnected: session.hostWs !== null && session.hostWs.readyState === WebSocket.OPEN
                }));

                // Si ya hay un frame de pantalla guardado, enviarlo de inmediato
                if (session.screenFrame) {
                    ws.send(JSON.stringify({ type: 'screen_frame', data: session.screenFrame }));
                }

                notifyHostStaffCount();
                console.log(`[STAFF] Dispositivo de staff conectado. Total: ${session.staffSockets.size}`);
                return;
            }

            // A partir de aquí se requiere autenticación obligatoria
            if (!isAuthenticated) {
                ws.send(JSON.stringify({ type: 'error', message: 'No autenticado' }));
                ws.close();
                return;
            }

            // Responder ping de latencia
            if (msg.type === 'ping') {
                ws.send(JSON.stringify({ type: 'pong' }));
                return;
            }

            // 2. Mensajes originados por el HOST -> Reenviar a Staff
            if (clientRole === 'host') {
                if (msg.type === 'screen_frame') {
                    // Frame de video o captura de pantalla (JPEG Base64)
                    session.screenFrame = msg.data;
                    broadcastToStaff({ type: 'screen_frame', data: msg.data });
                } else if (msg.type === 'webrtc_offer' || msg.type === 'webrtc_ice') {
                    broadcastToStaff(msg);
                }
                return;
            }

            // 3. Mensajes originados por STAFF -> Reenviar a HOST
            if (clientRole === 'staff') {
                if (!session.hostWs || session.hostWs.readyState !== WebSocket.OPEN) {
                    ws.send(JSON.stringify({ type: 'warn', message: 'El equipo del auditorio (Host) no está conectado' }));
                    return;
                }

                // Filtrado y sanitización estricta de eventos de control
                if (msg.type === 'mouse_move') {
                    // Validar coordenadas normalizadas (0.0 a 1.0)
                    const x = parseFloat(msg.x);
                    const y = parseFloat(msg.y);
                    if (!isNaN(x) && !isNaN(y) && x >= 0 && x <= 1 && y >= 0 && y <= 1) {
                        session.hostWs.send(JSON.stringify({ type: 'mouse_move', x, y }));
                    }
                } else if (msg.type === 'mouse_click') {
                    const btn = msg.button === 'right' ? 'right' : 'left';
                    const isDouble = Boolean(msg.double);
                    session.hostWs.send(JSON.stringify({ type: 'mouse_click', button: btn, double: isDouble }));
                } else if (msg.type === 'deck_action') {
                    // Acciones de presentación estrictamente catalogadas
                    const allowedActions = [
                        'NEXT_SLIDE',
                        'PREV_SLIDE',
                        'BLACKOUT',
                        'WHITEOUT',
                        'START_F5',
                        'STOP_ESC',
                        'MEDIA_PLAY_PAUSE',
                        'SEEK_FWD',
                        'SEEK_BACK',
                        'SHOW_DESKTOP',
                        'ALT_TAB',
                        'CLOSE_WINDOW',
                        'PROJECTOR_SWITCH',
                        'FULLSCREEN',
                        'RELOAD_PAGE',
                        'PANIC_RESET',
                        'VOLUME_UP',
                        'VOLUME_DOWN',
                        'VOLUME_MUTE'
                    ];
                    if (allowedActions.includes(msg.action)) {
                        session.hostWs.send(JSON.stringify({ type: 'deck_action', action: msg.action }));
                    }
                } else if (msg.type === 'key_press') {
                    // Teclas alfanuméricas o de control estándar
                    if (typeof msg.key === 'string' && msg.key.length <= 15) {
                        session.hostWs.send(JSON.stringify({ type: 'key_press', key: msg.key }));
                    }
                } else if (msg.type === 'webrtc_answer' || msg.type === 'webrtc_ice') {
                    session.hostWs.send(JSON.stringify(msg));
                }
            }

        } catch (err) {
            console.error('[WS ERROR]', err.message);
        }
    });

    ws.on('close', () => {
        if (clientRole === 'host') {
            session.hostWs = null;
            session.screenFrame = null;
            broadcastToStaff({ type: 'host_status', connected: false });
            console.log('[HOST] Host del auditorio desconectado.');
        } else if (clientRole === 'staff') {
            session.staffSockets.delete(ws);
            notifyHostStaffCount();
            console.log(`[STAFF] Dispositivo de staff desconectado. Restantes: ${session.staffSockets.size}`);
        }
    });
});

function broadcastToStaff(payload) {
    const raw = JSON.stringify(payload);
    for (const client of session.staffSockets) {
        if (client.readyState === WebSocket.OPEN) {
            client.send(raw);
        }
    }
}

function notifyHostStaffCount() {
    if (session.hostWs && session.hostWs.readyState === WebSocket.OPEN) {
        session.hostWs.send(JSON.stringify({
            type: 'staff_count_update',
            count: session.staffSockets.size
        }));
    }
}

// Heartbeat para limpiar conexiones huérfanas
const pingInterval = setInterval(() => {
    wss.clients.forEach((ws) => {
        if (!ws.isAlive) return ws.terminate();
        ws.isAlive = false;
        ws.ping();
    });
}, 15000);

wss.on('close', () => clearInterval(pingInterval));

// Iniciar servidor
server.listen(PORT, () => {
    console.log('========================================================');
    console.log('🚀 AUDITORIO-CONTROL: SISTEMA ACTIVO (Cero Costo)');
    console.log(`📍 Servidor Web & WebSocket en puerto: ${PORT}`);
    console.log(`🔑 PIN de Seguridad inicial: ${session.pin}`);
    console.log(`📱 Acceso Staff: http://localhost:${PORT}/staff.html`);
    console.log(`🖥️  Acceso Host:  http://localhost:${PORT}/host.html`);
    console.log('========================================================');
});
