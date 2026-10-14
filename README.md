# mu-season1-server

El servidor de mi MU Season1: [OpenMU](https://github.com/MUnique/OpenMU) 0.9.10 en Docker, con
plugins propios. No es un fork de OpenMU: usa la imagen oficial `munique/openmu` y le agrega
`plugins/`, cargados con un startup hook (el mismo mecanismo que usa OpenMu-Client-Babylon).

El cliente web es [mu-season1-client](https://github.com/gonzaPollini/mu-season1-client).

## Qué hay

| Archivo | Qué es |
|---|---|
| `compose.yml` | Todo el stack: OpenMU, PostgreSQL, el proxy del navegador y el cliente web. Juego publicado en la LAN y en localhost; panel de admin sólo en http://localhost:8081 |
| `Dockerfile` | Imagen oficial de OpenMU + `plugins/` compilados contra ella (con el .NET SDK en Docker, no hace falta instalarlo) |
| `plugins/` | Proyecto C# con los plugins |
| `web/Caddyfile` | Cómo Caddy sirve el cliente compilado. Con un dominio saca el certificado solo y sirve HTTP/2 y HTTP/3 |

### Servicios

| Servicio | Qué es | Dónde |
|---|---|---|
| `openmu-startup` | OpenMU con los plugins | connect server 44405 / 44406, game servers 55901-55906, admin http://localhost:8081 |
| `database` | PostgreSQL 16 | sólo adentro de Docker |
| `proxy` | Puente WebSocket -> TCP del cliente web (`proxy/main.ts` del repo del cliente), con la red del host | ws://localhost:3000 |
| `web` | El cliente compilado (`../client/dist`) con Caddy | http://localhost:4173 |

`proxy` y `web` usan el repo del cliente clonado al lado (`../client`, o `CLIENT_DIR`). El cliente se
compila con `bun run build` en ese repo; `web` sirve lo último compilado, sin reiniciar nada.

### Plugins

- **StatIncreaseWithoutWarpPlugIn**: con el cliente de Season 6, `/addstr 100` y similares
  mandan una respuesta de +1 por punto en vez de teletransportar al jugador (sin pantalla de carga).
  Por encima de 2000 puntos vuelve al comportamiento de OpenMU.
- **StatChatCommands**: `/addstr`, `/addagi`, `/addvit`, `/addene`, `/addcmd` y en español
  `/fuerza`, `/agilidad`, `/vitalidad`, `/energia`, `/comando` (todos con la cantidad, ej. `/fuerza 100`).
  Los de OpenMU vienen desactivados; estos quedan activos solos, sin tocar la base.
- **JewelryUpgrade**: Jewel of Bless y Jewel of Soul también suben los anillos y colgantes comunes (Ice, Poison, Lighting, Fire, Earth, Wind, Magic, Water, Ability), con los mismos niveles que armas y sets (Bless hasta +6, Soul hasta +9) con **chances propias**, configuradas al principio de `plugins/JewelryUpgrade.cs`: Bless siempre sube hasta +6; Soul 30 % (+15 % con suerte), si falla desde +7 vuelve a +0. Life agrega y sube la opción con 10 % de chance (en armas y sets es 50 %), y si falla el ítem pierde la opción. Reemplazan a los handlers de OpenMU con la misma clave; armas y sets siguen con la lógica original. **Se prende y apaga con `SEASON1_JEWELRY_UPGRADE=on|off` en `.env`** y `docker compose up -d`: apagado, las tres joyas se comportan como las de OpenMU. No apagarlo desde el panel de admin: OpenMU deja la joya sin ningún handler.

## Uso

```bash
docker compose up -d --build   # compila los plugins y levanta todo
docker compose logs -f openmu-startup
```

- Connect server: `44405` (cliente original de Season 6 y el cliente web), `44406` (MuMain).
- `LAN_IP` en `.env` (copiar `.env.example`) es la IP de la máquina en la LAN: OpenMU la anuncia
  para los game servers, publica los puertos ahí y el proxy la deja pasar. Si cambia (otra red,
  DHCP), actualizarla y `docker compose up -d`; si no, OpenMU no levanta (`cannot assign requested
  address`). La actual: `ip route get 1.1.1.1`.
- Cuentas de prueba: `test0`..`test9`, `test300`, `test400`, `ancient`, `socket` (contraseña = usuario).

### Configuración

Todo lo del server está en este repo (compose, Dockerfile, plugins, Caddyfile): no hay cambios hechos
a mano en la base. Un server nuevo queda igual con `docker compose up -d --build`.

## Actualizar OpenMU

Cambiar `OPENMU_IMAGE` en el `Dockerfile` (por ejemplo a `munique/openmu:0.9.12`) y
`docker compose up -d --build`. Los plugins se recompilan contra la versión nueva, así que un
cambio de API de OpenMU aparece como error de compilación.

---

## 📝 Últimos cambios

### 2026-10-14

**Commits incluidos:**
- (sin commits pendientes)

**Archivos modificados:**
  - `.md`: README.md
  - `.cs`: JewelryUpgrade.cs
