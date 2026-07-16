# 🎮 Multiplayer Third-Person FFA Shooter

> **Proyecto universitario** — Shooter multijugador en tercera persona de tipo *Free-For-All* (FFA) desarrollado sobre **Unity 6** con **Netcode for GameObjects** y **Unity Gaming Services**.

---

## Tabla de Contenidos

1. [Descripción General](#1-descripción-general)
2. [Características Principales](#2-características-principales)
3. [Arquitectura del Proyecto](#3-arquitectura-del-proyecto)
4. [Requisitos del Sistema](#4-requisitos-del-sistema)
5. [Instalación y Configuración](#5-instalación-y-configuración)
6. [Modo de Uso](#6-modo-de-uso)
7. [Controles del Jugador](#7-controles-del-jugador)
8. [Estructura del Repositorio](#8-estructura-del-repositorio)
9. [Tecnologías Utilizadas](#9-tecnologías-utilizadas)

---

## 1. Descripción General

**Multiplayer Third-Person FFA Shooter** es un videojuego multijugador en tiempo real desarrollado como proyecto de investigación aplicada en el campo de las redes de videojuegos. La modalidad de juego es **Free-For-All (FFA)**: todos los jugadores conectados compiten simultáneamente en la misma arena sin equipos, siendo el objetivo acumular la mayor cantidad de eliminaciones dentro de un límite de tiempo predefinido.

El proyecto explora en profundidad los desafíos propios del desarrollo de software multijugador: **autoridad del servidor**, **sincronización de estado en red**, **compensación de latencia** y **coherencia de la interfaz de usuario** entre múltiples clientes. Toda la lógica crítica de juego —incluyendo el sistema de daño, el registro de bajas y el temporizador de partida— se ejecuta y valida del lado del servidor para garantizar la integridad de los datos y prevenir la manipulación del cliente.

La sesión de red se establece a través de **Unity Relay** (Unity Gaming Services), lo que permite a los jugadores conectarse en línea sin necesidad de infraestructura de servidor dedicado ni configuración de NAT/reenvío de puertos, haciendo el proyecto autosuficiente y fácilmente demostrable.

---

## 2. Características Principales

### 2.1 Sistema de Partida en Red (`MatchManager`)

El `MatchManager` es un `NetworkBehaviour` instanciado y propiedad exclusiva del *session owner* (anfitrión). Gestiona el ciclo de vida completo de la partida a través de un enum de fases (`MatchPhase`) replicado mediante `NetworkVariable`:

| Fase | Descripción |
|---|---|
| `Active` | La partida está en curso. Los jugadores pueden moverse, disparar y recibir daño. |
| `EndScreen` | Se ha alcanzado el tiempo límite o un jugador ha alcanzado el kill-limit. Se muestra la pantalla de resultados. |
| `Restarting` | El servidor reinicia el estado de la partida antes de una nueva ronda. |

- **Temporizador de partida autorizado por el servidor:** El `MatchManager` decrementa el tiempo restante en el servidor y lo propaga como `NetworkVariable<float>` a todos los clientes, garantizando que la cuenta regresiva sea consistente independientemente de la latencia de cada cliente.
- **Detección automática del ganador:** Al finalizar el tiempo, el servidor recorre las puntuaciones de todos los jugadores conectados (almacenadas en `CorePlayerState`) e identifica al jugador con mayor número de eliminaciones.
- **Transmisión del resultado:** El ganador se anuncia mediante un RPC dirigido a todos los clientes (`SendTo.Everyone`), actualizando la pantalla de resultados de forma simultánea en todas las instancias.

### 2.2 Ciclo de Vida del Jugador y Auto-Respawn

El `GameManager` (singleton persistente) orquesta el ciclo de vida del jugador local:

- **Spawn inicial determinista:** El punto de aparición inicial se calcula mediante la operación `clientId % spawnPoints.Count`, distribuyendo a los jugadores equitativamente entre los puntos disponibles desde el primer frame.
- **Auto-respawn configurable:** Al morir (evento `StatDepletedEvent` con `StatKeys.Health`), el `GameManager` inicia una corutina (`RespawnRoutine`) que reproduce un contador visual descendente. Al concluir, el jugador reaparece en un punto aleatorio del mapa con vida completa, sin necesidad de intervención del servidor para el reposicionamiento local.
- **Desacoplamiento de la muerte del end-screen:** El componente `FFA_MatchPhaseAddon` (implementación de `IPlayerAddon`) observa la `NetworkVariable` de fase del `MatchManager`. Cuando la partida entra en las fases `EndScreen` o `Restarting`, congela al jugador local desactivando `CoreInputHandler`, `CoreCameraController`, `CoreMovement` y `ShooterAddon` sin disparar la lógica de muerte completa (ragdoll, VFX), preservando la integridad visual de la pantalla final.

### 2.3 Sistema de Disparo y Procesamiento de Impactos

El `ShooterHitProcessor` (derivado de `HitProcessor`, que a su vez extiende `NetworkBehaviour`) aplica la lógica de daño **en el servidor** con las siguientes capas de procesamiento:

- **Prevención de auto-daño:** Se verifica que `info.attackerId != OwnerClientId` antes de aplicar cualquier modificación estadística.
- **Daño por armadura:** Un modificador configurable (`armorDamageReduction = 0.3f`) reduce el daño entrante en un 30 % cuando el jugador porta armadura.
- **Headshots:** El sistema detecta si el punto de impacto (`info.hitPoint.y`) supera un umbral de altura relativo al personaje (`transform.position.y + 1.5f`). En caso afirmativo, aplica un multiplicador de daño (`headshotMultiplier = 2.0f`), duplicando el daño infligido.
- **Kill attribution:** Si el golpe es letal (`wasKillingBlow`), el sistema llama a `attackerState.AddKill()` sobre el `CorePlayerState` del atacante, incrementando su contador de bajas replicado en red.
- **Feedback global de impacto:** Un RPC broadcast (`SendHitFeedbackToAllRpc`) notifica a todos los clientes del impacto, reproduciendo el efecto de sonido 3D posicional y mostrando los números de daño flotantes animados.

### 2.4 HUD Unificado

La interfaz de usuario durante la partida se compone de tres subsistemas integrados:

- **KillFeed:** El sistema de eventos `KillConfirmedEvent` (un `ScriptableObject` del tipo `GameEvent<KillConfirmedPayload>`) desacopla al emisor del receptor. Cuando el servidor confirma una baja, el payload `KillConfirmedPayload` transporta los `ClientId` y nombres resueltos del eliminador y la víctima (incluyendo el flag `isSelfElimination`) a la UI `KillFeedUI` (implementada en UI Toolkit con `.uxml`), que renderiza las notificaciones de baja en pantalla de forma eficiente.
- **Indicador de munición:** Refleja en tiempo real el estado del cargador actual y la reserva de munición del arma equipada por el jugador local.
- **Indicador de salud:** Muestra el valor actual del stat `Health` del `CoreStatsHandler`, actualizado mediante el sistema de eventos `StatChangeEvent`.

### 2.5 Sincronización Servidor-Cliente

El proyecto adopta un modelo de **autoridad de servidor** estricto:

- Las `NetworkVariable` de solo lectura para el cliente (modo `NetworkVariableReadPermission.Everyone` / `NetworkVariableWritePermission.Owner` o `Server`) garantizan que el estado canónico resida en el servidor.
- Los eventos de baja (`KillConfirmedEvent`) se emiten desde el servidor y se distribuyen a los clientes mediante RPCs, evitando discrepancias en los marcadores.
- Los jugadores que se unen a una partida en curso (`late-join`) reciben el estado de fase actual inmediatamente gracias a la suscripción diferida implementada en `FFA_MatchPhaseAddon.WaitAndSubscribe()`, que espera a que el `MatchManager` esté disponible antes de suscribirse.

---

## 3. Arquitectura del Proyecto

El proyecto sigue una arquitectura modular basada en el patrón **Assembly Definition** de Unity, separando responsabilidades en ensamblados independientes:

```
Blocks.Gameplay.Core       → Sistemas base: movimiento, stats, estados del jugador, spawning
Blocks.Gameplay.Shooter    → Lógica de disparo, procesamiento de impactos, animaciones
Blocks.Sessions.Common     → Tipos compartidos de sesión (SessionSettings, SessionObserver)
Blocks.Sessions            → Integración con Unity Multiplayer Services
FFA (Assembly-CSharp)      → Lógica específica del modo de juego FFA
```

El flujo de datos principal sigue el patrón de **eventos ScriptableObject** para desacoplar sistemas (p. ej., `StatDepletedEvent`, `KillConfirmedEvent`, `RespawnStatusEvent`), reduciendo las dependencias directas entre componentes y facilitando la extensibilidad del sistema.

---

## 4. Requisitos del Sistema

### 4.1 Software de Desarrollo

| Componente | Versión requerida |
|---|---|
| **Unity Editor** | `6000.3.14f1` (Unity 6.3) |
| **Netcode for GameObjects** | `2.11.0` |
| **Unity Services Multiplayer** | `2.1.3` |
| **Unity Cinemachine** | Incluido en el proyecto |
| **Unity Input System** | Incluido en el proyecto |
| **UI Toolkit** | Incluido en Unity 6 (nativo) |

### 4.2 Cuenta de Unity

- Cuenta de Unity activa (gratuita) en [id.unity.com](https://id.unity.com).
- Un **Unity Cloud Project ID** vinculado al proyecto (necesario para que el servicio Relay funcione).

### 4.3 Hardware Mínimo (PC)

| Componente | Mínimo |
|---|---|
| **SO** | Windows 10 / macOS 12 / Ubuntu 20.04 |
| **CPU** | Intel Core i5 / AMD Ryzen 5 (4 núcleos) |
| **RAM** | 8 GB |
| **GPU** | Tarjeta gráfica con soporte DirectX 11 / Metal / Vulkan |
| **Almacenamiento** | ~5 GB (proyecto + Library de Unity) |
| **Conexión a Internet** | Requerida para sesiones multijugador vía Relay |

---

## 5. Instalación y Configuración

### Paso 1: Clonar el repositorio

```bash
git clone https://github.com/<usuario>/MultiplayerThirdPerson.git
cd MultiplayerThirdPerson
```

### Paso 2: Abrir el proyecto en Unity Hub

1. Abrir **Unity Hub**.
2. Hacer clic en **Add > Add project from disk**.
3. Seleccionar la carpeta raíz del repositorio clonado.
4. Asegurarse de que Unity Hub descargue automáticamente el editor **`6000.3.14f1`** si no está instalado (aparecerá el aviso de versión incompatible con la opción de instalar).

### Paso 3: Vincular el Cloud Project ID (obligatorio para Relay)

> ⚠️ **Sin este paso, el servicio Relay no podrá establecer conexiones y el multijugador no funcionará.**

1. En Unity Editor, ir a **Edit → Project Settings → Services**.
2. Hacer clic en **Link Unity Project**.
3. Iniciar sesión con la cuenta de Unity y seleccionar (o crear) un proyecto en el dashboard de Unity Cloud.
4. Confirmar la vinculación. El campo **Project ID** se rellenará automáticamente.

### Paso 4: Verificar configuración de Relay

1. En el Unity Dashboard ([dashboard.unity3d.com](https://dashboard.unity3d.com)), acceder al proyecto vinculado.
2. Ir a **Multiplayer → Relay** y comprobar que el servicio está habilitado para el entorno de desarrollo (`Development`).

### Paso 5: Abrir la escena principal

1. En la ventana **Project**, navegar a `Assets/_Project/Scenes/`.
2. Abrir la escena principal (p. ej., `MainMenu` o `Gameplay`).
3. Presionar **Play** en el Editor para una prueba local en modo host.

---

## 6. Modo de Uso

### 6.1 Iniciar el juego (Host)

1. Ejecutar la aplicación (o presionar **Play** en el Editor).
2. En el menú principal, introducir un **nombre de jugador** en el campo de texto correspondiente.
3. Hacer clic en **"Crear Sesión"** (o equivalente en la UI). El sistema solicitará un código de sala a Unity Relay y la sesión quedará publicada en el navegador de sesiones.
4. Esperar a que otros jugadores se unan. El contador de jugadores conectados se actualizará en tiempo real.
5. Una vez que todos los jugadores estén listos, el `MatchManager` (instanciado automáticamente por el *session owner*) iniciará el temporizador de partida.

### 6.2 Unirse a una sesión (Cliente)

1. Ejecutar la aplicación en otra instancia o máquina.
2. En el menú principal, introducir un **nombre de jugador**.
3. Hacer clic en **"Explorar Sesiones"**. El navegador de sesiones consultará Unity Multiplayer Services y listará todas las salas disponibles.
4. Seleccionar la sala deseada de la lista y hacer clic en **"Unirse"**.
5. El cliente se conectará a través de Unity Relay al host. La pantalla de sesión realizará un *fade-out* y el jugador aparecerá en el mapa en un punto de spawn asignado.

### 6.3 Desarrollo de la partida

- Durante la partida, el **HUD** muestra continuamente: la salud actual, la munición del arma y el KillFeed con las últimas eliminaciones.
- Al morir, se mostrará un contador de **reaparición (`RESPAWNING IN X`)** antes de volver al juego.
- Cuando el tiempo se agota o un jugador alcanza el límite de kills configurado, la partida transiciona a la fase `EndScreen`, se congela el movimiento de todos los jugadores y se muestra la pantalla de resultados con el ganador.

---

## 7. Controles del Jugador

> Los controles corresponden a la configuración estándar de teclado y ratón para PC.

| Acción | Control |
|---|---|
| **Mover (adelante / atrás / izquierda / derecha)** | `W` / `S` / `A` / `D` |
| **Apuntar / Rotar cámara** | Movimiento del ratón |
| **Disparar** | Clic izquierdo del ratón (`LMB`) |
| **Apuntar con mira (ADS)** | Clic derecho del ratón (`RMB`) |
| **Recargar** | `R` |
| **Saltar** | `Espacio` |
| **Sprint (correr)** | `Shift` izquierdo (mantener) |
| **Agacharse / Deslizarse** | `Ctrl` izquierdo |
| **Interactuar** | `E` |
| **Pausa / Menú** | `Escape` |

> **Nota:** El cursor del ratón se bloquea automáticamente al centro de la pantalla al iniciar la partida (`Cursor.lockState = CursorLockMode.Locked`). Para liberar el cursor durante el desarrollo, presionar `Escape`.

---

## 8. Estructura del Repositorio

```
MultiplayerThirdPerson/
├── Assets/
│   ├── _Project/                  # Contenido específico del proyecto FFA
│   │   ├── Scenes/                # Escenas de menú y gameplay
│   │   └── Scripts/
│   │       └── FFA/               # Lógica del modo FFA
│   │           ├── FFA_MatchPhaseAddon.cs     # Control de input por fase de partida
│   │           ├── FFA_KillFeedAddon.cs       # Emisor del KillConfirmedEvent
│   │           ├── KillConfirmedEvent.cs      # Canal de evento ScriptableObject
│   │           └── KillFeedUI.uxml            # Layout UI Toolkit del KillFeed
│   ├── Core/                      # Sistemas base (Core framework)
│   │   └── Scripts/Runtime/
│   │       └── Components/
│   │           └── GameManager.cs             # Singleton de ciclo de vida del jugador
│   └── Shooter/                   # Sistema de disparo
│       └── Scripts/Runtime/
│           └── Components/
│               └── ShooterHitProcessor.cs     # Procesamiento de impactos en red
├── Packages/
│   └── manifest.json              # Dependencias del proyecto (NGO 2.11.0, UGS 2.1.3)
├── ProjectSettings/
│   └── ProjectVersion.txt         # Versión del editor: 6000.3.14f1
└── README.md
```

---

## 9. Tecnologías Utilizadas

| Tecnología | Versión | Rol en el proyecto |
|---|---|---|
| **Unity Editor** | 6000.3.14f1 | Motor de juego y entorno de desarrollo |
| **C#** | .NET Standard 2.1 | Lenguaje de programación principal |
| **Netcode for GameObjects (NGO)** | 2.11.0 | Capa de red: RPCs, NetworkVariable, NetworkBehaviour |
| **Unity Services Multiplayer** | 2.1.3 | Sesiones en red + integración con Unity Relay |
| **Unity Relay** | (vía UGS) | Establecimiento de conexiones P2P sin servidor dedicado |
| **Unity UI Toolkit** | Nativo (Unity 6) | Interfaz de usuario: HUD, KillFeed, menús |
| **Unity Cinemachine** | (incluido) | Sistema de cámara en tercera persona con camera shake |
| **Unity Input System** | (incluido) | Gestión de entradas del jugador |

---

<div align="center">

*Proyecto desarrollado con Unity 6 · Netcode for GameObjects · Unity Gaming Services*

</div>
