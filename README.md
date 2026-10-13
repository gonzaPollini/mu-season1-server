# mu-season1-server

El servidor de mi MU Season1: [OpenMU](https://github.com/MUnique/OpenMU) 0.9.10 en Docker, con
plugins propios. No es un fork de OpenMU: usa la imagen oficial `munique/openmu` y le agrega
`plugins/`, cargados con un startup hook (el mismo mecanismo que usa OpenMu-Client-Babylon).

El cliente web es [mu-season1-client](https://github.com/gonzaPollini/mu-season1-client).

## Qué hay

| Archivo | Qué es |
|---|---|
| `compose.yml` | OpenMU + PostgreSQL. Juego publicado en la LAN y en localhost; panel de admin sólo en http://localhost:8081 |
| `Dockerfile` | Imagen oficial de OpenMU + `plugins/` compilados contra ella (con el .NET SDK en Docker, no hace falta instalarlo) |
| `plugins/` | Proyecto C# con los plugins |

### Plugins

- **StatIncreaseWithoutWarpPlugIn**: con el cliente de Season 6, `/addstr 100` y similares
  mandan una respuesta de +1 por punto en vez de teletransportar al jugador (sin pantalla de carga).
  Por encima de 2000 puntos vuelve al comportamiento de OpenMU.

## Uso

```bash
docker compose up -d --build   # compila los plugins y levanta todo
docker compose logs -f openmu-startup
```

- Connect server: `44405` (cliente original de Season 6 y el cliente web), `44406` (MuMain).
- `RESOLVE_IP` en `compose.yml` es la IP que OpenMU anuncia para los game servers: la de la
  máquina en la LAN. Si cambia, actualizarla ahí (y en el proxy del cliente web).
- Cuentas de prueba: `test0`..`test9`, `test300`, `test400`, `ancient`, `socket` (contraseña = usuario).

### Configuración aplicada en la base (no versionada)

- Plugins de chat `/addstr`, `/addagi`, `/addvit`, `/addene`, `/addcmd` activados (OpenMU los trae
  desactivados): panel de admin > Plugins.

## Actualizar OpenMU

Cambiar `OPENMU_IMAGE` en el `Dockerfile` (por ejemplo a `munique/openmu:0.9.12`) y
`docker compose up -d --build`. Los plugins se recompilan contra la versión nueva, así que un
cambio de API de OpenMU aparece como error de compilación.
