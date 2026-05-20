// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using Windows.Storage;

namespace Vianium.LocalSend.Strings
{
    /// <summary>
    /// Centralized string resources with Spanish/English support.
    /// Language auto-detected from system, overridable via settings.
    /// </summary>
    internal static class S
    {
        private static bool _isEnglish;
        private static bool _initialized;

        private static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            // Check user override first
            object langOverride;
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("app_language", out langOverride)
                && langOverride is string)
            {
                _isEnglish = ((string)langOverride) == "en";
                return;
            }

            // Auto-detect from system
            var languages = Windows.Globalization.ApplicationLanguages.Languages;
            if (languages != null && languages.Count > 0)
            {
                string primary = languages[0].ToLowerInvariant();
                _isEnglish = !primary.StartsWith("es");
            }
        }

        public static bool IsEnglish { get { Init(); return _isEnglish; } }

        public static void SetLanguage(string lang)
        {
            ApplicationData.Current.LocalSettings.Values["app_language"] = lang;
            _isEnglish = (lang == "en");
            _initialized = true;
        }

        public static string CurrentLanguageCode
        {
            get { return IsEnglish ? "en" : "es"; }
        }

        // ══════════════════════════════════════
        // Main Page — Receive tab
        // ══════════════════════════════════════
        public static string Starting { get { return IsEnglish ? "Starting..." : "Iniciando..."; } }
        public static string ListeningOn { get { return IsEnglish ? "Listening on " : "Escuchando en "; } }
        public static string Suspended { get { return IsEnglish ? "Suspended" : "Suspendido"; } }
        public static string ServerError { get { return IsEnglish ? "Error: " : "Error: "; } }
        public static string ReceivedFiles { get { return IsEnglish ? "Received files" : "Archivos recibidos"; } }
        public static string WaitingForFiles { get { return IsEnglish ? "Waiting for files..." : "Esperando archivos..."; } }
        public static string Receiving { get { return IsEnglish ? "Receiving..." : "Recibiendo..."; } }
        public static string History { get { return IsEnglish ? "History" : "Historial"; } }
        public static string About { get { return IsEnglish ? "About" : "Acerca de"; } }

        // ══════════════════════════════════════
        // Main Page — Send tab
        // ══════════════════════════════════════
        public static string NoFilesSelected { get { return IsEnglish ? "No files selected" : "Ningún archivo seleccionado"; } }
        public static string FilesSelected(int n) { return n + (IsEnglish ? " file(s) selected" : " archivo(s) seleccionado(s)"); }
        public static string TextMessagePlaceholder { get { return IsEnglish ? "Text message..." : "Mensaje de texto..."; } }
        public static string NearbyDevices { get { return IsEnglish ? "Nearby devices" : "Dispositivos cercanos"; } }
        public static string SearchingDevices { get { return IsEnglish ? "Searching devices..." : "Buscando dispositivos..."; } }
        public static string Searching { get { return IsEnglish ? "Searching..." : "Buscando..."; } }
        public static string Search { get { return IsEnglish ? "Search" : "Buscar"; } }
        public static string SendFiles { get { return IsEnglish ? "Send files" : "Enviar archivos"; } }

        // ══════════════════════════════════════
        // Main Page — Settings tab
        // ══════════════════════════════════════
        public static string Device { get { return IsEnglish ? "DEVICE" : "DISPOSITIVO"; } }
        public static string Name { get { return IsEnglish ? "Name" : "Nombre"; } }
        public static string MyDevice { get { return IsEnglish ? "My device" : "Mi dispositivo"; } }
        public static string Model { get { return IsEnglish ? "Model" : "Modelo"; } }
        public static string Network { get { return IsEnglish ? "NETWORK" : "RED"; } }
        public static string Port { get { return IsEnglish ? "Port: " : "Puerto: "; } }
        public static string Protocol { get { return IsEnglish ? "Protocol: " : "Protocolo: "; } }
        public static string Storage { get { return IsEnglish ? "STORAGE" : "ALMACENAMIENTO"; } }
        public static string Images { get { return IsEnglish ? "Images" : "Imágenes"; } }
        public static string Videos { get { return IsEnglish ? "Videos" : "Videos"; } }
        public static string Audio { get { return IsEnglish ? "Audio" : "Audio"; } }
        public static string OtherFiles { get { return IsEnglish ? "Other files" : "Otros archivos"; } }
        public static string App { get { return IsEnglish ? "APP" : "APP"; } }
        public static string Language { get { return IsEnglish ? "LANGUAGE" : "IDIOMA"; } }
        public static string Spanish { get { return "Español"; } }
        public static string English { get { return "English"; } }

        // ══════════════════════════════════════
        // Transfer Page
        // ══════════════════════════════════════
        public static string Sending { get { return IsEnglish ? "Sending..." : "Enviando..."; } }
        public static string Completed { get { return IsEnglish ? "Completed" : "Completado"; } }
        public static string Cancelled { get { return IsEnglish ? "Cancelled" : "Cancelado"; } }
        public static string TransferError { get { return IsEnglish ? "Transfer error" : "Error en transferencia"; } }
        public static string Cancel { get { return IsEnglish ? "Cancel" : "Cancelar"; } }
        public static string Back { get { return IsEnglish ? "Back" : "Volver"; } }
        public static string Files(int c, int t) { return c + "/" + t + (IsEnglish ? " files" : " archivos"); }

        // ══════════════════════════════════════
        // History Page
        // ══════════════════════════════════════
        public static string HistoryTitle { get { return IsEnglish ? "History" : "Historial"; } }
        public static string Transfers(int n) { return n + (IsEnglish ? " transfer(s)" : " transferencia(s)"); }
        public static string NoHistory { get { return IsEnglish ? "No history" : "Sin historial"; } }
        public static string ClearHistory { get { return IsEnglish ? "Clear history" : "Borrar historial"; } }
        public static string ConfirmClear { get { return IsEnglish ? "Clear all history?" : "¿Borrar todo el historial?"; } }
        public static string Confirm { get { return IsEnglish ? "Confirm" : "Confirmar"; } }
        public static string Clear { get { return IsEnglish ? "Clear" : "Borrar"; } }

        // ══════════════════════════════════════
        // Incoming transfer dialog
        // ══════════════════════════════════════
        public static string IncomingTransfer { get { return IsEnglish ? "Incoming transfer" : "Transferencia entrante"; } }
        public static string WantsToSend(string name, int files, string size)
        {
            return IsEnglish
                ? name + " wants to send " + files + " file(s) (" + size + ")"
                : name + " quiere enviar " + files + " archivo(s) (" + size + ")";
        }
        public static string Accept { get { return IsEnglish ? "Accept" : "Aceptar"; } }
        public static string Decline { get { return IsEnglish ? "Decline" : "Rechazar"; } }
        public static string RequestFrom(string name) { return IsEnglish ? "Request from " + name : "Solicitud de " + name; }
        public static string ReceivingFrom(string name) { return IsEnglish ? "Receiving from " + name + "..." : "Recibiendo de " + name + "..."; }

        // ══════════════════════════════════════
        // Completion / Errors
        // ══════════════════════════════════════
        public static string TransferComplete { get { return IsEnglish ? "Transfer complete" : "Transferencia completa"; } }
        public static string CompletedWithErrors { get { return IsEnglish ? "Completed with errors" : "Completado con errores"; } }
        public static string FilesReceived(int n)
        {
            return IsEnglish
                ? n + " file(s) received successfully."
                : n + " archivo(s) recibido(s) correctamente.";
        }
        public static string FilesReceivedWithErrors(int n)
        {
            return IsEnglish
                ? n + " file(s) received with errors."
                : n + " archivo(s) recibido(s) con errores.";
        }
        public static string SendError { get { return IsEnglish ? "Send error" : "Error al enviar"; } }
        public static string Sent { get { return IsEnglish ? "Sent" : "Enviado"; } }
        public static string MessageSentTo(string name) { return IsEnglish ? "Message sent to " + name : "Mensaje enviado a " + name; }
        public static string Error { get { return IsEnglish ? "Error" : "Error"; } }

        // ══════════════════════════════════════
        // About Page
        // ══════════════════════════════════════
        public static string Version { get { return IsEnglish ? "Version" : "Versión"; } }
        public static string Platform { get { return IsEnglish ? "Platform" : "Plataforma"; } }
        public static string Compatibility { get { return IsEnglish ? "COMPATIBILITY" : "COMPATIBILIDAD"; } }
        public static string SendReceiveWith { get { return IsEnglish ? "Send and receive files with:" : "Envía y recibe archivos con:"; } }
        public static string AboutThisPort { get { return IsEnglish ? "ABOUT THIS PORT" : "SOBRE ESTE PORT"; } }
        public static string FirstAndOnly
        {
            get
            {
                return IsEnglish
                    ? "This is the first and only LocalSend client for Windows Phone 8.1, built from scratch with native platform technologies."
                    : "Este es el primer y único cliente de LocalSend para Windows Phone 8.1, construido desde cero con tecnologías nativas de la plataforma.";
            }
        }
        public static string BuiltWith { get { return IsEnglish ? "Built with:" : "Construido con:"; } }
        public static string TechHttpServer { get { return IsEnglish ? "Embedded HTTP server on StreamSocketListener" : "Servidor HTTP embebido sobre StreamSocketListener"; } }
        public static string TechHttpsClient { get { return IsEnglish ? "HTTP/HTTPS client with TLS 1.2 and self-signed certificates" : "Cliente HTTP/HTTPS con soporte TLS 1.2 y certificados autofirmados"; } }
        public static string TechDiscovery { get { return IsEnglish ? "UDP multicast discovery and HTTP network scan" : "Descubrimiento por UDP multicast y escaneo HTTP de red"; } }
        public static string TechStreaming { get { return IsEnglish ? "Streaming transfer (any file size)" : "Transferencia por streaming (archivos de cualquier tamaño)"; } }
        public static string TechJson { get { return IsEnglish ? "Custom JSON parser and chunked transfer encoding" : "Parser JSON y chunked transfer encoding propios"; } }
        public static string BuiltFromScratch
        {
            get
            {
                return IsEnglish
                    ? "Built entirely in C# and XAML on WinRT, with no external dependencies, proving this platform is still capable."
                    : "Todo construido desde cero en C# y XAML sobre WinRT, sin dependencias externas, demostrando que esta plataforma sigue siendo capaz.";
            }
        }
        public static string Developer { get { return IsEnglish ? "DEVELOPER" : "DESARROLLADOR"; } }
        public static string SoftwareEngineer { get { return IsEnglish ? "Software Engineer" : "Ingeniero de Software"; } }
        public static string Credits { get { return IsEnglish ? "CREDITS" : "CRÉDITOS"; } }
        public static string OriginalProtocol { get { return IsEnglish ? "Original protocol and app" : "Protocolo y app original"; } }
        public static string License { get { return IsEnglish ? "LICENSE" : "LICENCIA"; } }
        public static string LicenseText
        {
            get
            {
                return IsEnglish
                    ? "LocalSend is licensed under Apache License 2.0. It is free and open source software."
                    : "LocalSend está licenciado bajo Apache License 2.0. Es software libre y de código abierto.";
            }
        }
        public static string AboutDescription
        {
            get
            {
                return IsEnglish
                    ? "LocalSend is a free, open-source app that allows you to securely share files and messages with nearby devices over the local network, without internet."
                    : "LocalSend es una app gratuita y de código abierto que permite compartir archivos y mensajes de forma segura con dispositivos cercanos a través de la red local, sin necesidad de internet.";
            }
        }
        public static string CompatibilityNote
        {
            get
            {
                return IsEnglish
                    ? "Supports HTTP and HTTPS with self-signed certificates. Direct transfer over local WiFi, no intermediary servers or internet connection needed."
                    : "Soporta HTTP y HTTPS con certificados autofirmados. Transferencia directa por red local WiFi, sin servidores intermediarios ni conexión a internet.";
            }
        }
        public static string IncludesDescription
        {
            get
            {
                return IsEnglish
                    ? "Includes an embedded HTTP server, HTTPS client with self-signed certificate support, UDP multicast discovery and network scanning, file streaming transfer, and full compatibility with LocalSend protocol v2.1."
                    : "Incluye un servidor HTTP embebido, cliente HTTPS con soporte para certificados autofirmados, descubrimiento por multicast UDP y escaneo de red, transferencia de archivos por streaming, y compatibilidad completa con el protocolo LocalSend v2.1.";
            }
        }

        // ══════════════════════════════════════
        // Storage labels (button text)
        // ══════════════════════════════════════
        public static string PhotosLibrary { get { return IsEnglish ? "Photos Library" : "Biblioteca Fotos"; } }
        public static string VideosLibrary { get { return IsEnglish ? "Videos Library" : "Biblioteca Videos"; } }
        public static string MusicLibrary { get { return IsEnglish ? "Music Library" : "Biblioteca Música"; } }
        public static string UserStorage { get { return IsEnglish ? "User storage" : "Almacenamiento"; } }
        public static string SdCard { get { return IsEnglish ? "SD Card (beta)" : "Tarjeta SD (beta)"; } }
        public static string AppOnly { get { return IsEnglish ? "App only" : "Solo en app"; } }

        // Storage path hints (shown below the button)
        public static string PathPictures { get { return @"Pictures\LocalSend"; } }
        public static string PathVideos { get { return @"Videos\LocalSend"; } }
        public static string PathMusic { get { return @"Music\LocalSend"; } }
        public static string PathUserStorage { get { return @"C:\Data\Users\Public\LocalSend"; } }
        public static string PathSdCard { get { return @"D:\LocalSend"; } }
        public static string PathAppOnly
        {
            get
            {
                return IsEnglish
                    ? "(only visible from this app)"
                    : "(solo visible desde esta app)";
            }
        }
        public static string InteropNotAvailable
        {
            get
            {
                return IsEnglish
                    ? "(interop not available — saving to app storage)"
                    : "(interop no disponible — guardando en app)";
            }
        }

        // ══════════════════════════════════════
        // File status
        // ══════════════════════════════════════
        public static string StatusQueued { get { return IsEnglish ? "Queued" : "En cola"; } }
        public static string StatusSending { get { return IsEnglish ? "Sending..." : "Enviando..."; } }
        public static string StatusCompleted { get { return IsEnglish ? "Completed" : "Completado"; } }
        public static string StatusFailed { get { return IsEnglish ? "Failed" : "Error"; } }
        public static string StatusSkipped { get { return IsEnglish ? "Skipped" : "Omitido"; } }

        // History entries
        public static string SentTo(string name) { return IsEnglish ? "Sent to " + name : "Enviado a " + name; }
        public static string ReceivedFrom(string name) { return IsEnglish ? "Received from " + name : "Recibido de " + name; }

        // ══════════════════════════════════════
        // Storage migration
        // ══════════════════════════════════════
        public static string MigrateFiles(int n)
        {
            return IsEnglish
                ? "Move " + n + @" file(s) to C:\Data\Users\Public\LocalSend"
                : "Mover " + n + @" archivo(s) a C:\Data\Users\Public\LocalSend";
        }
        public static string Migrating { get { return IsEnglish ? "Moving files..." : "Moviendo archivos..."; } }
        public static string MigrationDone(int moved, int failed)
        {
            if (failed == 0)
                return IsEnglish
                    ? moved + " file(s) moved successfully."
                    : moved + " archivo(s) movido(s) correctamente.";
            return IsEnglish
                ? moved + " moved, " + failed + " failed."
                : moved + " movido(s), " + failed + " fallido(s).";
        }
        public static string MigrationNoInterop
        {
            get
            {
                return IsEnglish
                    ? "Interop required. Files remain in app sandbox (not visible in File Manager)."
                    : "Se requiere interop. Los archivos permanecen en el sandbox de la app (no visibles en explorador de archivos).";
            }
        }

        // ══════════════════════════════════════
        // Changelog Page
        // ══════════════════════════════════════
        public static string Changelog { get { return IsEnglish ? "Changelog" : "Cambios"; } }
        public static string V101StorageTitle { get { return IsEnglish ? @"File storage: C:\Data\Users\Public\LocalSend" : @"Almacenamiento: C:\Data\Users\Public\LocalSend"; } }
        public static string V101StorageDesc
        {
            get
            {
                return IsEnglish
                    ? @"Received files (documents, APKs, ZIPs, etc.) are now saved to C:\Data\Users\Public\LocalSend, next to Pictures, Videos and Music. Visible from any file manager. Requires interop. Falls back to app sandbox if not available."
                    : @"Los archivos recibidos (documentos, APKs, ZIPs, etc.) ahora se guardan en C:\Data\Users\Public\LocalSend, junto a Pictures, Videos y Music. Visible desde cualquier explorador de archivos. Requiere interop. Si no está disponible, se guarda en el sandbox de la app.";
            }
        }
        public static string V101MigrateTitle { get { return IsEnglish ? "Migrate old files" : "Migrar archivos anteriores"; } }
        public static string V101MigrateDesc
        {
            get
            {
                return IsEnglish
                    ? @"If you received files with v1.0.0, they were saved in app sandbox. A new button in Settings lets you move them to C:\Data\Users\Public\LocalSend so they're visible from any file manager."
                    : @"Si recibiste archivos con v1.0.0, se guardaron en el sandbox de la app. Un nuevo botón en Ajustes permite moverlos a C:\Data\Users\Public\LocalSend para que sean visibles desde cualquier explorador de archivos.";
            }
        }
        public static string V101SdTitle { get { return IsEnglish ? "SD Card support (beta)" : "Soporte SD Card (beta)"; } }
        public static string V101SdDesc
        {
            get
            {
                return IsEnglish
                    ? "If your device has an SD card, you can now save received files directly to D:\\LocalSend. The option appears automatically in Settings > Storage > Other files. Requires interop unlock."
                    : "Si tu dispositivo tiene tarjeta SD, ahora puedes guardar los archivos recibidos directamente en D:\\LocalSend. La opción aparece automáticamente en Ajustes > Almacenamiento > Otros archivos. Requiere interop.";
            }
        }
        public static string V101CrashTitle { get { return IsEnglish ? "Fix: crash when selecting files" : "Fix: crash al seleccionar archivos"; } }
        public static string V101CrashDesc
        {
            get
            {
                return IsEnglish
                    ? "Fixed a crash that occurred when the OS terminated the app in the background during file selection. The app now correctly restores its state after returning from the file picker."
                    : "Se corrigió un crash que ocurría cuando el sistema terminaba la app en segundo plano durante la selección de archivos. La app ahora restaura su estado correctamente al volver del selector de archivos.";
            }
        }
        public static string V101PortTitle { get { return IsEnglish ? "Fix: connection to devices" : "Fix: conexión a dispositivos"; } }
        public static string V101PortDesc
        {
            get
            {
                return IsEnglish
                    ? "Fixed an issue where sending files could fail if the target device info was incomplete. Now falls back to the default LocalSend port (53317)."
                    : "Se corrigió un problema donde el envío podía fallar si la información del dispositivo destino estaba incompleta. Ahora usa el puerto por defecto de LocalSend (53317).";
            }
        }
        public static string V100Desc
        {
            get
            {
                return IsEnglish
                    ? "Initial release. First and only LocalSend client for Windows Phone 8.1. Send and receive files with Android, iOS, Windows, macOS, and Linux devices over local WiFi."
                    : "Lanzamiento inicial. Primer y único cliente de LocalSend para Windows Phone 8.1. Envía y recibe archivos con dispositivos Android, iOS, Windows, macOS y Linux por WiFi local.";
            }
        }

        // ══════════════════════════════════════
        // Diagnostics Page
        // ══════════════════════════════════════
        public static string Diagnostics { get { return IsEnglish ? "Diagnostics" : "Diagnóstico"; } }
        public static string LogLevelLabel { get { return IsEnglish ? "Level:" : "Nivel:"; } }
        public static string LogFilterAll { get { return IsEnglish ? "All" : "Todo"; } }
        public static string LogFilterDebug { get { return IsEnglish ? "Debug and up" : "Debug y superior"; } }
        public static string LogFilterInfo { get { return IsEnglish ? "Info and up" : "Info y superior"; } }
        public static string LogFilterWarn { get { return IsEnglish ? "Warnings and up" : "Avisos y superior"; } }
        public static string LogFilterError { get { return IsEnglish ? "Errors only" : "Solo errores"; } }
        public static string LogEmpty { get { return IsEnglish ? "No log entries at this level" : "Sin entradas en este nivel"; } }
        public static string LogRefresh { get { return IsEnglish ? "refresh" : "refrescar"; } }
        public static string LogShare { get { return IsEnglish ? "share" : "compartir"; } }
        public static string LogClear { get { return IsEnglish ? "clear" : "limpiar"; } }
        public static string LogShareTitle { get { return IsEnglish ? "LocalSend log" : "Registro de LocalSend"; } }
        public static string LogNoFile { get { return IsEnglish ? "No log file on disk yet." : "Aún no hay archivo de registro en disco."; } }

        public static string LogClearConfirm
        {
            get
            {
                return IsEnglish
                    ? "Clear the in-memory log buffer? The rotating log files on disk are not affected."
                    : "¿Limpiar el búfer de registro en memoria? Los archivos de registro en disco no se ven afectados.";
            }
        }

        public static string LogStatus(int count, string file)
        {
            return count + (IsEnglish
                ? " entries in memory · file: "
                : " entradas en memoria · archivo: ") + file;
        }

        public static string LogSourceLabel { get { return IsEnglish ? "Source:" : "Fuente:"; } }
        public static string LogSourceMemory { get { return IsEnglish ? "Current session" : "Sesión actual"; } }

        public static string LogSourceFile
        {
            get
            {
                return IsEnglish
                    ? "On-disk file (all sessions)"
                    : "Archivo en disco (todas las sesiones)";
            }
        }

        public static string LogFileStatus(int lines)
        {
            return lines + (IsEnglish ? " lines on disk" : " líneas en disco");
        }

        public static string LogShareDescription(int files)
        {
            return files + (IsEnglish ? " log file(s)" : " archivo(s) de registro");
        }
    }
}
