# Jellyfin ListenBrainz Plugin
<div align="center">
    <p>
        <img alt="Logo" src="https://raw.githubusercontent.com/pepebarrascout/jellyfin-plugin-listenbrainz/main/logo.png" height="180"/><br />
        <a href="https://github.com/pepebarrascout/jellyfin-plugin-listenbrainz/releases"><img alt="Total GitHub Downloads" src="https://img.shields.io/github/downloads/pepebarrascout/jellyfin-plugin-listenbrainz/total?color=352e5b&label=descargas"/></a>
        <a href="https://github.com/pepebarrascout/jellyfin-plugin-listenbrainz/issues"><img alt="GitHub Issues" src="https://img.shields.io/github/issues/pepebarrascout/jellyfin-plugin-listenbrainz?color=352e5b"/></a>
        <a href="https://jellyfin.org/"><img alt="Jellyfin Version" src="https://img.shields.io/badge/Jellyfin-10.11.x-blue.svg"/></a>
        <a href="https://listenbrainz.org/"><img alt="ListenBrainz" src="https://img.shields.io/badge/ListenBrainz-352e5b?logo=data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIyNCIgaGVpZ2h0PSIyNCIgdmlld0JveD0iMCAwIDI0IDI0Ij48cGF0aCBmaWxsPSIjZmZmIiBkPSJNMTIgMkw1IDlsNSA3IDUtN2w3LTciLz48L3N2Zz4=&logoColor=white"/></a>
    </p>
</div>

> **Scrobblea tu música a ListenBrainz** desde Jellyfin. Actualiza el estado de "Ahora Reproduciendo", envía scrobbles automáticamente y gestiona tus canciones favoritas directamente desde cualquier cliente de Jellyfin.

**Requiere Jellyfin versión `10.11.0` o superior.**

---

## ✨ Características

| Característica | Descripción |
|---|---|
| 🎵 **Scrobbling Automático** | Las canciones se scrobblean al alcanzar el porcentaje configurado o 4 minutos, lo que ocurra primero |
| 📡 **Ahora Reproduciendo** | Actualiza tu perfil de ListenBrainz con lo que estás escuchando en tiempo real |
| ❤️ **Love / Hate Canciones** | Marca canciones como favoritas o prohibidas en ListenBrainz a través de la API del plugin |
| 💖 **Auto-Love** | Marca automáticamente como favoritas las canciones que tienes como favoritas en Jellyfin |
| 🏷️ **Metadatos Enriquecidos** | Envía IDs de MusicBrainz, número de pista, álbum y etiquetas junto con cada scrobble |
| ⚙️ **Configurable** | Ajusta el porcentaje de scrobble, duración mínima y origen del artista |
| 👥 **Multi-Usuario** | Soporta múltiples sesiones de Jellyfin simultáneamente |
| 🔑 **Autenticación Simple** | Solo necesita un token de usuario, sin OAuth ni API keys complicadas |

---

## 📋 Clientes Probados

El plugin ha sido probado y funciona correctamente en los siguientes clientes de Jellyfin:

| Cliente | Plataforma | Estado |
|---|---|---|
| 🌐 **Jellyfin Web** | Interfaz web nativa | ✅ Funcional |
| 📱 **Jellyfin para Android** | App oficial de Jellyfin | ✅ Funcional |
| 🖥️ **[Feishin](https://github.com/jeffvli/feishin)** | Escritorio (AppImage Linux) | ✅ Funcional |
| 🎵 **[Finamp](https://github.com/UnicornsOnLSD/finamp)** | Android (versión Beta) | ✅ Funcional |

> **Próximamente** se probará en otros clientes de Jellyfin. Si has probado el plugin en un cliente que no aparece en la lista, por favor envía tu reporte de uso abriendo un [Issue](https://github.com/pepebarrascout/jellyfin-plugin-listenbrainz/issues) para que podamos actualizar esta tabla.

---

## 🚀 Instalación

### Método 1: Desde el Catálogo de Plugins de Jellyfin (vía Manifest) ⭐ Recomendado

Esta es la forma más sencilla de instalar el plugin. Solo necesitas agregar el manifest de este repositorio como fuente de plugins en tu servidor Jellyfin.

1. En tu servidor Jellyfin, navega a **Panel de Control > Plugins > Repositorios**
2. Haz clic en el botón **+** (agregar repositorio)
3. Ingresa los siguientes datos:
   - **Nombre**: `ListenBrainz Plugin`
   - **URL del Manifest**: `https://raw.githubusercontent.com/pepebarrascout/jellyfin-plugin-listenbrainz/main/manifest.json`
4. Haz clic en **Guardar**
5. Navega a la pestaña **Catálogo**
6. Busca **ListenBrainz** en la lista de plugins disponibles
7. Haz clic en **Instalar**
8. Reinicia Jellyfin cuando se te solicite

> **Nota**: Cada vez que se publique una nueva versión en este repositorio, Jellyfin la detectará automáticamente y te ofrecerá actualizar.

### Método 2: Instalación Manual

1. Descarga la última versión desde [Releases](../../releases)
2. Descomprime el archivo ZIP
3. Copia todos los archivos `.dll` a la carpeta de plugins de tu servidor Jellyfin:
   - **Linux**: `~/.config/jellyfin/plugins/`
   - **Windows**: `%LocalAppData%\Jellyfin\plugins\`
   - **macOS**: `~/.local/share/jellyfin/plugins/`
   - **Docker**: Monta un volumen en `/config/plugins` dentro del contenedor
4. Reinicia Jellyfin

---

## ⚙️ Configuración

### Paso 1: Obtener tu Token de Usuario de ListenBrainz

A diferencia de Last.fm, ListenBrainz solo necesita un token de usuario simple. Puedes obtenerlo desde tu perfil:

1. Inicia sesión en [listenbrainz.org](https://listenbrainz.org/)
2. Navega a tu perfil: **https://listenbrainz.org/profile/**
3. En la sección de **API**, copia tu **User Token** — es el único dato que necesitas

> **Nota**: No necesitas registrar una aplicación ni crear API keys. ListenBrainz usa un modelo de autenticación mucho más simple que Last.fm.

### Paso 2: Configurar el plugin en Jellyfin

1. Navega a **Panel de Control > Plugins > ListenBrainz**
2. Ingresa tu **User Token** en el campo correspondiente
3. Haz clic en **Validate Token** para verificar que el token es válido
4. Si todo sale bien, verás el mensaje "Connected as [usuario]" con tu nombre de usuario
5. Ajusta las opciones de configuración a tu preferencia
6. Haz clic en **Save** para guardar los cambios

### Opciones de Configuración

| Opción | Predeterminado | Descripción |
|---|---|---|
| Enable Scrobbling | Sí | Activa/desactiva el scrobbling a ListenBrainz |
| Enable Now Playing Notifications | Sí | Activa/desactiva las notificaciones de ahora reproduciendo |
| Scrobble after | 50% | Porcentaje de la canción a reproducir antes de scrobblear (máx. 4 minutos) |
| Minimum track duration | 30s | Duración mínima en segundos para que una canción sea elegible para scrobble |
| Auto-love liked tracks | Sí | Marca automáticamente como favoritas las canciones que tienes como favoritas en Jellyfin |
| Use Album Artist for scrobbling | No | Usa el artista del álbum en lugar del artista de la pista para el scrobble |

---

## 🔧 Solución de Problemas

### Los scrobbles no aparecen en ListenBrainz

- Verifica que tu **User Token** sea correcto y esté validado (debes ver "Connected as [usuario]")
- Verifica que las canciones duren al menos 30 segundos (configuración predeterminada)
- Confirma que hayas alcanzado el umbral de scrobble (50% de reproducción o 4 minutos)
- Revisa los registros (logs) de Jellyfin buscando mensajes del plugin ListenBrainz

### El plugin no aparece en el Dashboard

- Asegúrate de estar usando Jellyfin 10.11.x o superior
- Reinicia Jellyfin después de instalar el plugin
- Verifica que los archivos `.dll` estén en la carpeta correcta de plugins

### La configuración no se guarda

- Haz clic en **Save** después de ingresar tu token
- Si usas Jellyfin en Docker, asegúrate de que el directorio de configuración esté montado como volumen persistente

### El token no se valida

- Asegúrate de haber copiado el token completo desde tu perfil de ListenBrainz
- Verifica que `https://api.listenbrainz.org` sea accesible desde tu servidor Jellyfin
- Si usas un proxy o firewall, verifica que permita conexiones salientes a ListenBrainz

### Problemas de conexión con ListenBrainz

- Asegúrate de que `https://api.listenbrainz.org` sea accesible desde tu servidor Jellyfin
- Si usas un proxy o firewall, verifica que permita conexiones salientes a ListenBrainz
- Intenta reconectarte haciendo clic en **Disconnect** y volviendo a ingresar y validar tu token

---

## 🛠️ Compilación desde el Código Fuente

### Requisitos

- .NET SDK 9.0 o superior
- Fuentes de NuGet configuradas:
  - `https://api.nuget.org/v3/index.json`
  - `https://nuget.jellyfin.org/v3/index.json`

### Comandos de Compilación

```bash
# Restaurar dependencias
dotnet restore

# Compilar en modo Debug
dotnet build

# Compilar en modo Release
dotnet build -c Release

# Publicar artefactos
dotnet publish -c Release -o artifacts
```

---

## 🔄 Diferencias con Last.fm

| Aspecto | Last.fm | ListenBrainz |
|---|---|---|
| 🔑 **Autenticación** | OAuth (API Key + Secret + Session) | Solo token de usuario |
| 📊 **Datos** | Propietario / cerrado | Open source / libre (MusicBrainz) |
| 🏷️ **Metadatos** | Básicos (artista, título, álbum) | Enriquecidos (MBIDs, etiquetas, números de pista) |
| 💰 **Costo** | Freemium (limitado sin suscripción) | 100% gratuito y sin límites |
| 🌐 **API** | `ws.audioscrobbler.com` | `api.listenbrainz.org` |

---

## 💡 Recomendación

Si te interesa crear **listas inteligentes y dinámicas** en Jellyfin (playlists y colecciones basadas en reglas que se actualizan automáticamente), te recomiendo probar el plugin **[Jellyfin SmartLists](https://github.com/jyourstone/jellyfin-smartlists-plugin/)**:

- Crea playlists automáticas basadas en género, artista, calificación, fecha, estado de reproducción y mucho más
- Interfaz web moderna para gestionar tus listas
- Funciona con todos los tipos de medios (películas, series, música, etc.)
- Se actualiza automáticamente cuando tu biblioteca cambia

<a href="https://github.com/jyourstone/jellyfin-smartlists-plugin/">
    <img alt="SmartLists Plugin" src="https://img.shields.io/badge/SmartLists-Plugin-6c5ce7?logo=github&logoColor=white&style=for-the-badge"/>
</a>

---

## 💬 Soporte y Contribuciones

- **Reportes de bugs y sugerencias**: Usa la sección de [Issues](https://github.com/pepebarrascout/jellyfin-plugin-listenbrainz/issues) para reportar problemas o proponer nuevas funciones
- **Contribuciones**: Las contribuciones son bienvenidas. No dudes en enviar un Pull Request
- **Reportes de uso**: Si has probado el plugin en un cliente de Jellyfin que no aparece en la lista de [Clientes Probados](#-clientes-probados), por favor compártelo

---

## ⚠️ Disclaimer

Este proyecto se proporciona tal cual (as-is) sin garantías de ningún tipo. El autor no se hace responsable de cualquier daño, pérdida de datos o problema derivado del uso de este plugin. ListenBrainz y sus respectivos logotipos son propiedad de MetaBrainz Foundation. Este plugin no está afiliado con, respaldado por, ni patrocinado por MetaBrainz Foundation.

---

## 📄 Licencia

Este proyecto está bajo la Licencia MIT — consulta el archivo [LICENSE](LICENSE) para más detalles.
