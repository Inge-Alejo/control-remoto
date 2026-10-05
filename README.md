# 🎛️ Auditorio Control (AC-Remote)

> **Sistema de Control Remoto de Grado Profesional, Seguro y 100% Gratuito ($0)**  
> Especialmente diseñado para auditorios, salas de eventos y teatros donde los firewalls universitarios/corporativos y el aislamiento de clientes Wi-Fi (Client Isolation) bloquean herramientas comerciales como AnyDesk o TeamViewer.

---

## 🏛️ 1. Arquitectura del Sistema & Evasión de Firewalls

En las redes universitarias, las conexiones entrantes están bloqueadas y los puertos de escritorio remoto tradicionales (RDP 3389, VNC 5900, UDP P2P) son filtrados.

Para garantizar conectividad total sin costo, la arquitectura utiliza **Conexiones Salientes Inversas (Reverse Outbound)** sobre **WebSockets Seguros (`wss://`) en el puerto 443 (HTTPS)**:

```
+---------------------------+                     +---------------------------------------+                     +------------------------------------+
|  PC AUDITORIO (ESCENARIO) |                     |        SERVIDOR RELAY (NUBE)          |                     |         PERSONAL DE STAFF          |
|  - host.html (Navegador)  |                     |        - Node.js + Express            |                     |         - staff.html               |
|            O              | --(WSS / TCP 443)-->|        - WebSockets Seguros (ws)      |<--(WSS / TCP 443)-- |         - Smartphone / Tablet      |
|  - host-bridge.js (Win32) |                     |        - Puerto 443 (HTTPS/WSS)       |                     |         - Laptop en Cabina         |
+---------------------------+                     +---------------------------------------+                     +------------------------------------+
         Host Agent                                           Relay / Auth Broker                                            Staff Controller
```

### ¿Por qué el firewall universitario no puede bloquearlo?
1. **Tráfico saliente HTTPS estándar:** Tanto el PC del auditorio como el teléfono del staff inician conexiones *hacia afuera* al puerto 443.
2. **Cifrado TLS de extremo a extremo:** Para el sistema de inspección profunda de paquetes (DPI) del firewall, el tráfico es idéntico a una sesión web común.
3. **Inmune al "Client Isolation":** Aunque el Wi-Fi del auditorio impida que dos dispositivos en la misma sala se vean localmente, ambos se comunican a través del servidor seguro en la nube.

---

## 🛡️ 2. Auditoría de Seguridad & Modelo de Amenazas

| Vector de Amenaza | Riesgo Identificado | Mitigación Implementada |
| :--- | :--- | :--- |
| **Acceso no autorizado** | Cualquier persona en la sala que descubra la URL podría controlar el proyector. | **PIN rotativo de 6 dígitos** generado criptográficamente (`crypto.randomInt`). Se muestra solo en el PC anfitrión. |
| **Ataque de Fuerza Bruta** | Script automatizado probando combinaciones de PIN. | **Rate Limiting estricto:** Tras 5 intentos fallidos, la IP queda bloqueada automáticamente por 60 segundos. |
| **Manipulación de sesión** | Falsificación de credenciales de staff. | **Tokens firmados con HMAC-SHA256** utilizando `crypto.timingSafeEqual` para prevenir ataques de temporización. |
| **Inyección de Comandos (RCE)** | Envío de comandos de terminal maliciosos al PC del auditorio. | **Lista blanca estricta (Zero-Trust Input):** El agente del host solo acepta eventos tipados (`NEXT_SLIDE`, `PREV_SLIDE`, `BLACKOUT`, clics y coordenadas normalizadas `0.0 a 1.0`). No se ejecuta ningún comando de shell arbitrario. |
| **Interrupción de Red** | El staff se aleja o pierde señal con una tecla presionada. | **Liberación de seguridad (Key Release Safety):** El subproceso de Windows limpia y libera automáticamente estados de teclas y botones de ratón al desconectarse. |

---

## 🚀 3. Ejecutables Nativos (.exe) para los Computadores

En el repositorio encontrarás los ejecutables portátiles listos para usar:

### 📦 Opción 1: Instalador Automático (`Instalador-AuditorioControl.exe`)
Ejecuta el asistente de instalación en cualquier equipo. Te permite:
* Instalar **Auditorio Host** (Equipo del Proyector) o **Staff Remote** (Cabina).
* Crear accesos directos elegantes en el **Escritorio** y en el **Menú Inicio de Windows**.

### 💻 Opción 2: Ejecutables Portátiles Directos
* **`Auditorio-Host.exe`** (Para el computador del escenario / proyector):
  Con un doble clic, inicia el agente de control y abre la pantalla del Auditorio en una ventana nativa de escritorio dedicada mostrando el PIN.
* **`Staff-Control.exe`** (Para el computador o laptop del staff en cabina):
  Con un doble clic, abre directamente el mando de control en una ventana de aplicación independiente (sin pestañas ni barras de navegador).

---

## ☁️ 4. Despliegue en la Nube con Costo $0

Para usarlo en la universidad sin depender de tu red local, puedes desplegar el servidor relay gratis en cualquiera de estas plataformas:

### Opción A: Render.com (Recomendado - 100% Gratis)
1. Crea una cuenta gratuita en [render.com](https://render.com).
2. Sube esta carpeta a un repositorio privado o público en GitHub.
3. En Render, crea un nuevo **Web Service**, selecciona tu repositorio de GitHub y selecciona el entorno **Node**.
4. Comando de construcción: `npm install --production`
5. Comando de inicio: `node server.js`
6. ¡Listo! Render te dará una URL segura con HTTPS y WSS automáticos (ej. `https://auditorio-control.onrender.com`).

### Opción B: Railway.app o Fly.io
* El repositorio incluye un `Dockerfile` optimizado con Node.js Alpine de menos de 100 MB listo para desplegar con un solo clic.

---

## 📱 5. Funciones Diseñadas para el Staff de Auditorio

* ⏩ **Avanzar Diapositiva Gigante:** Botón táctil primario optimizado para no fallar en la oscuridad.
* ⏪ **Retroceder Diapositiva.**
* ⬛ **Pantalla Negra Inmediata (Tecla B):** Oculta la proyección instantáneamente si surge algún imprevisto o diapositiva errónea.
* ⬜ **Pantalla Blanca (Tecla W).**
* 🎬 **Iniciar Presentación (F5) & Salir (Escape).**
* ⏯️ **Play / Pausa de Video (Espacio).**
* 🔊 **Control de Volumen & Silenciador (Mute).**
* 🖱️ **Trackpad Táctil con gestos:** Desliza el dedo para mover el puntero del mouse en el proyector, toca para clic y dos dedos para clic derecho.
* ⌨️ **Envío de Texto:** Permite escribir texto en el proyector sin necesidad de estar frente al teclado.
* 📳 **Respuesta Háptica:** Vibración física en el teléfono al pulsar cualquier acción.
