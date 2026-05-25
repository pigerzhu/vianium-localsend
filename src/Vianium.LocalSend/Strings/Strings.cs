// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using Windows.Storage;

namespace Vianium.LocalSend.Strings
{
    /// <summary>
    /// Centralized string resources with Spanish, English and Russian support.
    /// Language auto-detected from system, overridable via settings.
    /// </summary>
    internal static class S
    {
        private const string LangEs = "es";
        private const string LangEn = "en";
        private const string LangRu = "ru";

        private static string _languageCode;
        private static bool _initialized;

        private static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            object langOverride;
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("app_language", out langOverride)
                && langOverride is string)
            {
                _languageCode = NormalizeLanguage((string)langOverride);
                return;
            }

            var languages = Windows.Globalization.ApplicationLanguages.Languages;
            if (languages != null && languages.Count > 0)
            {
                _languageCode = NormalizeLanguage(languages[0]);
                return;
            }

            _languageCode = LangEn;
        }

        private static string NormalizeLanguage(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
                return LangEn;

            string normalized = lang.ToLowerInvariant();
            if (normalized.StartsWith("ru")) return LangRu;
            if (normalized.StartsWith("es")) return LangEs;
            if (normalized.StartsWith("en")) return LangEn;
            return LangEn;
        }

        private static string T(string en, string es, string ru)
        {
            Init();
            if (_languageCode == LangRu) return ru;
            if (_languageCode == LangEs) return es;
            return en;
        }

        private static string RuPlural(int n, string one, string few, string many)
        {
            int value = n < 0 ? -n : n;
            int mod10 = value % 10;
            int mod100 = value % 100;

            if (mod10 == 1 && mod100 != 11) return one;
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return few;
            return many;
        }

        public static bool IsEnglish { get { Init(); return _languageCode == LangEn; } }
        public static bool IsSpanish { get { Init(); return _languageCode == LangEs; } }
        public static bool IsRussian { get { Init(); return _languageCode == LangRu; } }

        public static void SetLanguage(string lang)
        {
            _languageCode = NormalizeLanguage(lang);
            ApplicationData.Current.LocalSettings.Values["app_language"] = _languageCode;
            _initialized = true;
        }

        public static string CurrentLanguageCode
        {
            get { Init(); return _languageCode; }
        }

        // Main Page - Receive tab
        public static string ReceiveTab { get { return T("receive", "recibir", "получать"); } }
        public static string SendTab { get { return T("send", "enviar", "отправить"); } }
        public static string SettingsTab { get { return T("settings", "ajustes", "настройки"); } }
        public static string Starting { get { return T("Starting...", "Iniciando...", "Запуск..."); } }
        public static string ListeningOn { get { return T("Listening on ", "Escuchando en ", "Слушаем: "); } }
        public static string Suspended { get { return T("Suspended", "Suspendido", "Приостановлено"); } }
        public static string ServerError { get { return T("Error: ", "Error: ", "Ошибка: "); } }
        public static string ReceivedFiles { get { return T("Received files", "Archivos recibidos", "Полученные файлы"); } }
        public static string WaitingForFiles { get { return T("Waiting for files...", "Esperando archivos...", "Ожидание файлов..."); } }
        public static string Receiving { get { return T("Receiving...", "Recibiendo...", "Получение..."); } }
        public static string History { get { return T("History", "Historial", "История"); } }
        public static string About { get { return T("About", "Acerca de", "О приложении"); } }
        public static string ReadyToReceive { get { return T("ready to receive", "listo para recibir", "готово к получению"); } }
        public static string AddressTile { get { return T("address", "dirección", "адрес"); } }
        public static string ReceivedStat { get { return T("received", "recibidos", "получено"); } }
        public static string TotalStat { get { return T("total", "total", "всего"); } }
        public static string NearbyStat { get { return T("nearby", "cercanos", "рядом"); } }
        public static string UnknownDevice { get { return T("Unknown device", "Dispositivo desconocido", "Неизвестное устройство"); } }
        public static string Unknown { get { return T("Unknown", "Desconocido", "Неизвестно"); } }
        public static string ReceiveAddressTitle { get { return T("Receive address", "Dirección para recibir", "Адрес для получения"); } }
        public static string ReceiveAddressUnavailable { get { return T("No local address available yet.", "Aún no hay una dirección local disponible.", "Локальный адрес пока недоступен."); } }
        public static string ReceiveAddressMessage(string address) { return address; }
        public static string ReceiveRequestHeader { get { return T("RECEIVE · ACCEPT", "RECIBIR · ACEPTAR", "ПОЛУЧИТЬ · ПРИНЯТЬ"); } }
        public static string WantsToSendYouPrefix { get { return T("wants to send you ", "quiere enviarte ", "хочет отправить вам "); } }
        public static string ServerErrorMessage(string message) { return ServerError + message; }

        public static string ResumeErrorMessage(string message)
        {
            return T("Resume error: ", "Error al reanudar: ", "Ошибка возобновления: ") + message;
        }

        public static string FileCount(int n)
        {
            if (IsRussian)
                return n + " " + RuPlural(n, "файл", "файла", "файлов");
            if (IsEnglish)
                return n == 1 ? "1 file" : n + " files";
            return n == 1 ? "1 archivo" : n + " archivos";
        }

        public static string TransferSummary(int files, string size)
        {
            return FileCount(files) + ", " + size;
        }

        public static string ReceiveProgress(int percent, int completed, int total, string bytes)
        {
            return percent + "% - " + completed + "/" + total +
                T(" files - ", " archivos - ", " файлов - ") + bytes;
        }

        // Main Page - Send tab
        public static string NoFilesSelected { get { return T("No files selected", "Ningún archivo seleccionado", "Файлы не выбраны"); } }
        public static string FilesSelected(int n)
        {
            if (IsRussian)
                return "Выбрано " + FileCount(n);
            return n + T(" file(s) selected", " archivo(s) seleccionado(s)", "");
        }
        public static string TextMessagePlaceholder { get { return T("Text message...", "Mensaje de texto...", "Текстовое сообщение..."); } }
        public static string PickerHint { get { return T("photos · videos · music · files", "fotos · videos · música · archivos", "фото · видео · музыка · файлы"); } }
        public static string NearbyDevices { get { return T("Nearby devices", "Dispositivos cercanos", "Устройства рядом"); } }
        public static string SearchingDevices { get { return T("Searching devices...", "Buscando dispositivos...", "Поиск устройств..."); } }
        public static string Searching { get { return T("Searching...", "Buscando...", "Поиск..."); } }
        public static string Search { get { return T("Search", "Buscar", "Поиск"); } }
        public static string SendFiles { get { return T("Send files", "Enviar archivos", "Отправить файлы"); } }
        public static string SelectionModeMultiple { get { return T("MULTIPLE", "MÚLTIPLE", "НЕСКОЛЬКО"); } }
        public static string SelectionModeSingle { get { return T("SINGLE", "INDIVIDUAL", "ОДИН"); } }
        public static string MessageSentToDevices(int count)
        {
            if (IsRussian)
                return "Сообщение отправлено на " + count + " " + RuPlural(count, "устройство", "устройства", "устройств");
            return T("Message sent to ", "Mensaje enviado a ", "") + count + T(" devices", " dispositivos", "");
        }

        // Main Page - Settings tab
        public static string Device { get { return T("DEVICE", "DISPOSITIVO", "УСТРОЙСТВО"); } }
        public static string Name { get { return T("Name", "Nombre", "Имя"); } }
        public static string MyDevice { get { return T("My device", "Mi dispositivo", "Моё устройство"); } }
        public static string Model { get { return T("Model", "Modelo", "Модель"); } }
        public static string Network { get { return T("NETWORK", "RED", "СЕТЬ"); } }
        public static string Port { get { return T("Port: ", "Puerto: ", "Порт: "); } }
        public static string Protocol { get { return T("Protocol: ", "Protocolo: ", "Протокол: "); } }
        public static string AppSection { get { return T("app", "aplicación", "приложение"); } }
        public static string QuickSave { get { return T("quick save", "guardado rápido", "быстрое сохранение"); } }
        public static string RequirePin { get { return T("require PIN", "requerir PIN", "требовать PIN"); } }
        public static string PinCode { get { return T("PIN", "PIN", "PIN"); } }
        public static string RegeneratePin { get { return T("new", "nuevo", "новый"); } }
        public static string Autostart { get { return T("start receiver automatically", "iniciar recepción automáticamente", "запускать прием автоматически"); } }
        public static string ReceiverStopped { get { return T("Receiver stopped", "Recepción detenida", "Прием остановлен"); } }
        public static string Storage { get { return T("STORAGE", "ALMACENAMIENTO", "ХРАНИЛИЩЕ"); } }
        public static string Images { get { return T("Images", "Imágenes", "Изображения"); } }
        public static string Videos { get { return T("Videos", "Videos", "Видео"); } }
        public static string Audio { get { return T("Audio", "Audio", "Аудио"); } }
        public static string OtherFiles { get { return T("Other files", "Otros archivos", "Другие файлы"); } }
        public static string App { get { return T("APP", "APP", "ПРИЛОЖЕНИЕ"); } }
        public static string Language { get { return T("LANGUAGE", "IDIOMA", "ЯЗЫК"); } }
        public static string Spanish { get { return "Español"; } }
        public static string English { get { return "English"; } }
        public static string Russian { get { return "Русский"; } }

        // Transfer Page
        public static string Sending { get { return T("Sending...", "Enviando...", "Отправка..."); } }
        public static string SendingTitle { get { return T("sending", "enviando", "отправка"); } }
        public static string ReceivingTitle { get { return T("receiving", "recibiendo", "получение"); } }
        public static string TransferCompleteTitle { get { return T("transfer complete", "transferencia completa", "передача завершена"); } }
        public static string Completed { get { return T("Completed", "Completado", "Завершено"); } }
        public static string Cancelled { get { return T("Cancelled", "Cancelado", "Отменено"); } }
        public static string TransferError { get { return T("Transfer error", "Error en transferencia", "Ошибка передачи"); } }
        public static string Cancel { get { return T("Cancel", "Cancelar", "Отмена"); } }
        public static string Back { get { return T("Back", "Volver", "Назад"); } }
        public static string Files(int c, int t) { return c + "/" + t + T(" files", " archivos", " файлов"); }
        public static string ToPeer(string name) { return T("to ", "a ", "для ") + name; }
        public static string FromPeer(string name) { return T("from ", "de ", "от ") + name; }
        public static string Minimize { get { return T("Minimize", "Minimizar", "Свернуть"); } }
        public static string Maximize { get { return T("Maximize", "Maximizar", "Развернуть"); } }
        public static string Done { get { return T("Done", "Listo", "Готово"); } }
        public static string Remaining { get { return T("Remaining", "Restante", "Осталось"); } }
        public static string Speed { get { return T("Speed", "Velocidad", "Скорость"); } }
        public static string CurrentFileIndex(int current, int total)
        {
            return current + T(" OF ", " DE ", " ИЗ ") + total;
        }
        public static string TimeRemaining(int seconds)
        {
            if (seconds < 0) seconds = 0;
            if (seconds < 60)
                return seconds + T(" s", " s", " с");

            int minutes = seconds / 60;
            return minutes + T(" min", " min", " мин");
        }

        // History Page
        public static string HistoryTitle { get { return T("History", "Historial", "История"); } }
        public static string Transfers(int n)
        {
            if (IsRussian)
                return n + " " + RuPlural(n, "передача", "передачи", "передач");
            return n + T(" transfer(s)", " transferencia(s)", "");
        }
        public static string NoHistory { get { return T("No history", "Sin historial", "История пуста"); } }
        public static string ClearHistory { get { return T("Clear history", "Borrar historial", "Очистить историю"); } }
        public static string ConfirmClear { get { return T("Clear all history?", "¿Borrar todo el historial?", "Очистить всю историю?"); } }
        public static string Confirm { get { return T("Confirm", "Confirmar", "Подтвердить"); } }
        public static string Clear { get { return T("Clear", "Borrar", "Очистить"); } }
        public static string Today { get { return T("TODAY", "HOY", "СЕГОДНЯ"); } }
        public static string Yesterday { get { return T("YESTERDAY", "AYER", "ВЧЕРА"); } }
        public static string DirectionSent { get { return T("sent", "enviado", "отправлено"); } }
        public static string DirectionReceived { get { return T("received", "recibido", "получено"); } }

        public static string MonthShort(int month)
        {
            string[] en =
            {
                "JAN", "FEB", "MAR", "APR", "MAY", "JUN",
                "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"
            };
            string[] es =
            {
                "ENE", "FEB", "MAR", "ABR", "MAY", "JUN",
                "JUL", "AGO", "SEP", "OCT", "NOV", "DIC"
            };
            string[] ru =
            {
                "ЯНВ", "ФЕВ", "МАР", "АПР", "МАЙ", "ИЮН",
                "ИЮЛ", "АВГ", "СЕН", "ОКТ", "НОЯ", "ДЕК"
            };

            int index = month - 1;
            if (index < 0) index = 0;
            if (index > 11) index = 11;

            if (IsRussian) return ru[index];
            if (IsSpanish) return es[index];
            return en[index];
        }

        // Incoming transfer dialog
        public static string IncomingTransfer { get { return T("Incoming transfer", "Transferencia entrante", "Входящая передача"); } }
        public static string WantsToSend(string name, int files, string size)
        {
            if (IsRussian)
                return name + " хочет отправить " + FileCount(files) + " (" + size + ")";
            return IsEnglish
                ? name + " wants to send " + FileCount(files) + " (" + size + ")"
                : name + " quiere enviar " + FileCount(files) + " (" + size + ")";
        }
        public static string Accept { get { return T("Accept", "Aceptar", "Принять"); } }
        public static string Decline { get { return T("Decline", "Rechazar", "Отклонить"); } }
        public static string RequestFrom(string name) { return T("Request from ", "Solicitud de ", "Запрос от ") + name; }
        public static string ReceivingFrom(string name) { return T("Receiving from ", "Recibiendo de ", "Получение от ") + name + "..."; }

        // Completion / Errors
        public static string TransferComplete { get { return T("Transfer complete", "Transferencia completa", "Передача завершена"); } }
        public static string CompletedWithErrors { get { return T("Completed with errors", "Completado con errores", "Завершено с ошибками"); } }
        public static string FilesReceived(int n)
        {
            if (IsRussian)
                return "Успешно получено: " + FileCount(n) + ".";
            return IsEnglish
                ? FileCount(n) + " received successfully."
                : FileCount(n) + " recibido" + (n == 1 ? "" : "s") + " correctamente.";
        }
        public static string FilesReceivedWithErrors(int n)
        {
            if (IsRussian)
                return "Получено с ошибками: " + FileCount(n) + ".";
            return IsEnglish
                ? FileCount(n) + " received with errors."
                : FileCount(n) + " recibido" + (n == 1 ? "" : "s") + " con errores.";
        }
        public static string SendError { get { return T("Send error", "Error al enviar", "Ошибка отправки"); } }
        public static string Sent { get { return T("Sent", "Enviado", "Отправлено"); } }
        public static string MessageSentTo(string name) { return T("Message sent to ", "Mensaje enviado a ", "Сообщение отправлено: ") + name; }
        public static string Error { get { return T("Error", "Error", "Ошибка"); } }

        // About Page
        public static string Version { get { return T("Version", "Versión", "Версия"); } }
        public static string AboutVersionValue { get { return T("Version 1.1.0 / Protocol 2.1", "Versión 1.1.0 / Protocolo 2.1", "Версия 1.1.0 / Протокол 2.1"); } }
        public static string Platform { get { return T("Platform", "Plataforma", "Платформа"); } }
        public static string ProtocolShort { get { return T("Protocol", "Protocolo", "Протокол"); } }
        public static string Repository { get { return T("Repository", "Repositorio", "Репозиторий"); } }
        public static string Project { get { return T("Project", "Proyecto", "Проект"); } }
        public static string Features { get { return T("Features", "Funciones", "Возможности"); } }
        public static string Technology { get { return T("Technology", "Tecnología", "Технологии"); } }
        public static string Compatibility { get { return T("COMPATIBILITY", "COMPATIBILIDAD", "СОВМЕСТИМОСТЬ"); } }
        public static string SendReceiveWith { get { return T("Send and receive files with:", "Envía y recibe archivos con:", "Отправляйте и получайте файлы с:"); } }
        public static string AboutThisPort { get { return T("ABOUT THIS PORT", "SOBRE ESTE PUERTO", "ОБ ЭТОЙ ВЕРСИИ"); } }
        public static string AboutTagline { get { return T("P2P file sharing for Windows Phone 8.1, compatible with the LocalSend protocol.", "Transferencia P2P de archivos para Windows Phone 8.1, compatible con el protocolo LocalSend.", "P2P-обмен файлами для Windows Phone 8.1, совместимый с протоколом LocalSend."); } }
        public static string AboutLicenseSummary { get { return T("Source-available product repository licensed under PolyForm Noncommercial License 1.0.0. Free for non-commercial use.", "Repositorio de producto source-available bajo PolyForm Noncommercial License 1.0.0. Libre para uso no comercial.", "Исходный код доступен по лицензии PolyForm Noncommercial License 1.0.0. Бесплатно для некоммерческого использования."); } }
        public static string AboutPrivacySummary { get { return T("Direct local WiFi transfers. No internet, no account, no relay server, no telemetry.", "Transferencias directas por WiFi local. Sin internet, sin cuenta, sin servidor intermediario y sin telemetría.", "Прямые передачи по локальному WiFi. Без интернета, аккаунта, промежуточного сервера и телеметрии."); } }
        public static string AboutWhy { get { return T("Windows Phone 8.1 has no native bridge to the modern LocalSend ecosystem. This port fills that gap with a clean-room, protocol-compatible client.", "Windows Phone 8.1 no tiene un puente nativo hacia el ecosistema moderno de LocalSend. Este port cubre ese espacio con un cliente compatible, implementado de forma independiente.", "У Windows Phone 8.1 нет родного моста к современной экосистеме LocalSend. Этот порт закрывает этот пробел независимым совместимым клиентом."); } }
        public static string AboutInteroperability { get { return T("It turns a WP8.1 device into a full LocalSend peer that can send and receive files with desktop and mobile clients on the same local network.", "Convierte un dispositivo WP8.1 en un par LocalSend completo, capaz de enviar y recibir archivos con clientes de escritorio y móviles en la misma red local.", "Он превращает устройство WP8.1 в полноценный узел LocalSend, способный отправлять и получать файлы с настольными и мобильными клиентами в той же локальной сети."); } }
        public static string FirstAndOnly { get { return T("This is the first and only LocalSend client for Windows Phone 8.1, built from scratch with native platform technologies.", "Este es el primer y único cliente de LocalSend para Windows Phone 8.1, construido desde cero con tecnologías nativas de la plataforma.", "Это первый и единственный клиент LocalSend для Windows Phone 8.1, созданный с нуля на нативных технологиях платформы."); } }
        public static string BuiltWith { get { return T("Built with:", "Construido con:", "Создано с:"); } }
        public static string TechHttpServer { get { return T("Embedded HTTP server on StreamSocketListener", "Servidor HTTP embebido sobre StreamSocketListener", "Встроенный HTTP-сервер на StreamSocketListener"); } }
        public static string TechHttpsClient { get { return T("HTTP/HTTPS client with TLS 1.2 and self-signed certificates", "Cliente HTTP/HTTPS con soporte TLS 1.2 y certificados autofirmados", "HTTP/HTTPS-клиент с TLS 1.2 и самоподписанными сертификатами"); } }
        public static string TechDiscovery { get { return T("UDP multicast discovery and HTTP network scan", "Descubrimiento por UDP multicast y escaneo HTTP de red", "Обнаружение через UDP multicast и HTTP-сканирование сети"); } }
        public static string TechStreaming { get { return T("Direct-to-disk streaming transfer for large files", "Transferencia por streaming directo a disco para archivos grandes", "Потоковая передача больших файлов напрямую на диск"); } }
        public static string TechJson { get { return T("Custom JSON parser and chunked transfer encoding", "Parser JSON y chunked transfer encoding propios", "Собственный JSON-парсер и chunked transfer encoding"); } }
        public static string BuiltFromScratch { get { return T("Built entirely in C# and XAML on WinRT, with no external dependencies, proving this platform is still capable.", "Todo construido desde cero en C# y XAML sobre WinRT, sin dependencias externas, demostrando que esta plataforma sigue siendo capaz.", "Полностью создано на C# и XAML поверх WinRT, без внешних зависимостей, доказывая, что платформа всё ещё способна на современную работу."); } }
        public static string Developer { get { return T("DEVELOPER", "DESARROLLADOR", "РАЗРАБОТЧИК"); } }
        public static string SoftwareEngineer { get { return T("Software Engineer", "Ingeniero de Software", "Инженер-программист"); } }
        public static string Credits { get { return T("CREDITS", "CRÉDITOS", "БЛАГОДАРНОСТИ"); } }
        public static string OriginalProtocol { get { return T("Original protocol and app", "Protocolo y app original", "Оригинальный протокол и приложение"); } }
        public static string License { get { return T("LICENSE", "LICENCIA", "ЛИЦЕНЗИЯ"); } }
        public static string LicenseText { get { return T("LocalSend is licensed under PolyForm Noncommercial License 1.0.0. Commercial use requires a separate license. LocalSend protocol attribution is provided separately.", "LocalSend está licenciado bajo PolyForm Noncommercial License 1.0.0. El uso comercial requiere una licencia separada. La atribución al protocolo LocalSend se declara por separado.", "LocalSend лицензирован по PolyForm Noncommercial License 1.0.0. Для коммерческого использования требуется отдельная лицензия. Атрибуция протокола LocalSend указана отдельно."); } }
        public static string AboutDescription { get { return T("LocalSend is an independent WinRT/C# implementation of the LocalSend protocol for Windows Phone 8.1. It is self-contained and designed to keep older phones useful in mixed-device local networks.", "LocalSend es una implementación independiente en WinRT/C# del protocolo LocalSend para Windows Phone 8.1. Es autocontenida y está diseñada para mantener útiles a los teléfonos antiguos dentro de redes locales con dispositivos mixtos.", "LocalSend — независимая реализация протокола LocalSend на WinRT/C# для Windows Phone 8.1. Она самодостаточна и помогает старым телефонам оставаться полезными в локальных сетях с разными устройствами."); } }
        public static string CompatibilityNote { get { return T("Compatible LocalSend clients can discover this phone and transfer directly over local WiFi. HTTP and optional HTTPS with self-signed certificates are supported.", "Los clientes LocalSend compatibles pueden descubrir este teléfono y transferir directamente por WiFi local. Soporta HTTP y HTTPS opcional con certificados autofirmados.", "Совместимые клиенты LocalSend могут обнаружить этот телефон и передавать файлы напрямую по локальному WiFi. Поддерживаются HTTP и опциональный HTTPS с самоподписанными сертификатами."); } }
        public static string IncludesDescription { get { return T("Includes an embedded HTTP server, HTTPS client with self-signed certificate support, UDP multicast discovery and network scanning, file streaming transfer, and full compatibility with LocalSend protocol v2.1.", "Incluye un servidor HTTP embebido, cliente HTTPS con soporte para certificados autofirmados, descubrimiento por multicast UDP y escaneo de red, transferencia de archivos por streaming, y compatibilidad completa con el protocolo LocalSend v2.1.", "Включает встроенный HTTP-сервер, HTTPS-клиент с самоподписанными сертификатами, UDP multicast, сетевое сканирование, потоковую передачу файлов и совместимость с протоколом LocalSend v2.1."); } }
        public static string FeatureDiscovery { get { return T("Peer discovery through UDP multicast, with HTTP network-scan fallback.", "Descubrimiento de dispositivos por multicast UDP, con respaldo por escaneo HTTP de red.", "Обнаружение устройств через UDP multicast с резервным HTTP-сканированием сети."); } }
        public static string FeatureStreaming { get { return T("Embedded HTTP/1.1 server with streaming transfers instead of whole-file buffering.", "Servidor HTTP/1.1 embebido con transferencias por streaming en lugar de cargar archivos completos en memoria.", "Встроенный HTTP/1.1-сервер с потоковой передачей вместо загрузки файлов целиком в память."); } }
        public static string FeatureSecureTransport { get { return T("Optional HTTPS with self-signed certificate support.", "HTTPS opcional con soporte para certificados autofirmados.", "Опциональный HTTPS с поддержкой самоподписанных сертификатов."); } }
        public static string FeatureAnyFiles { get { return T("Send and receive images, video, audio, documents, APKs, ZIPs and other files.", "Envía y recibe imágenes, video, audio, documentos, APKs, ZIPs y otros archivos.", "Отправка и получение изображений, видео, аудио, документов, APK, ZIP и других файлов."); } }
        public static string FeatureTextMessages { get { return T("LocalSend text-message transfers.", "Transferencias de mensajes de texto compatibles con LocalSend.", "Передача текстовых сообщений LocalSend."); } }
        public static string FeatureHistory { get { return T("Local transfer history and full-screen transfer progress that can be minimized and restored.", "Historial local y progreso de transferencia en pantalla completa, con opción de minimizar y restaurar.", "Локальная история передач и полноэкранный прогресс, который можно свернуть и восстановить."); } }
        public static string FeatureLanguages { get { return T("Spanish, English and Russian UI with system detection and manual override.", "Interfaz en español, inglés y ruso con detección del sistema y selección manual.", "Интерфейс на испанском, английском и русском с автоопределением и ручным выбором."); } }
        public static string FeatureStorage { get { return T("Configurable storage targets: media libraries, public LocalSend folder, SD card or app sandbox.", "Destinos de almacenamiento configurables: bibliotecas multimedia, carpeta pública LocalSend, tarjeta SD o sandbox de la app.", "Настраиваемые места хранения: медиатеки, публичная папка LocalSend, SD-карта или песочница приложения."); } }
        public static string CompatibilityDesktop { get { return T("LocalSend Desktop: Windows, macOS, Linux", "LocalSend Desktop: Windows, macOS, Linux", "LocalSend Desktop: Windows, macOS, Linux"); } }
        public static string CompatibilityMobile { get { return T("LocalSend Mobile: Android, iOS", "LocalSend Mobile: Android, iOS", "LocalSend Mobile: Android, iOS"); } }
        public static string CompatibilityClients { get { return T("Independent LocalSend protocol clients", "Clientes independientes del protocolo LocalSend", "Независимые клиенты протокола LocalSend"); } }
        public static string AboutTechnologyIntro { get { return T("Built entirely on native WinRT/.NET 4.5 APIs: StreamSocket, StreamSocketListener, DatagramSocket, HttpClient and StorageFile. No NuGet packages, third-party DLLs or sibling Vianium dependencies.", "Construido por completo sobre APIs nativas de WinRT/.NET 4.5: StreamSocket, StreamSocketListener, DatagramSocket, HttpClient y StorageFile. Sin paquetes NuGet, DLLs de terceros ni dependencias de otros repositorios Vianium.", "Полностью построено на нативных API WinRT/.NET 4.5: StreamSocket, StreamSocketListener, DatagramSocket, HttpClient и StorageFile. Без NuGet, сторонних DLL и зависимостей от других репозиториев Vianium."); } }
        public static string AboutHistory { get { return T("Previously published as PivoraLocalSend on the Windows Phone Store. v1.0.0 was the initial release; v1.0.1 added public storage targets, SD-card support beta, file-picker crash fixes and default-port fallback. Version 1.1.0 refreshes the UI, transfer flow, localization and receiver preferences.", "Publicado anteriormente como PivoraLocalSend en Windows Phone Store. v1.0.0 fue el lanzamiento inicial; v1.0.1 agregó destinos de almacenamiento públicos, soporte beta para SD, correcciones del selector de archivos y respaldo al puerto predeterminado. La versión 1.1.0 renueva la UI, el flujo de transferencia, la localización y las preferencias de recepción.", "Ранее приложение публиковалось как PivoraLocalSend в Windows Phone Store. v1.0.0 был первым выпуском; v1.0.1 добавил публичные папки хранения, бета-поддержку SD-карты, исправления выбора файлов и резервный порт по умолчанию. Версия 1.1.0 обновляет интерфейс, поток передач, локализацию и настройки приема."); } }
        public static string AuthorCredit { get { return T("Created and maintained by Angel Careaga as part of the Vianium ecosystem. This is an independent compatible implementation, not affiliated with, endorsed by or sponsored by LocalSend.", "Creado y mantenido por Angel Careaga como parte del ecosistema Vianium. Es una implementación compatible independiente, no afiliada, respaldada ni patrocinada por LocalSend.", "Создано и поддерживается Angel Careaga как часть экосистемы Vianium. Это независимая совместимая реализация, не связанная с LocalSend, не одобренная и не спонсируемая им."); } }

        // Storage labels
        public static string PhotosLibrary { get { return T("Photos Library", "Biblioteca Fotos", "Библиотека фото"); } }
        public static string VideosLibrary { get { return T("Videos Library", "Biblioteca Videos", "Библиотека видео"); } }
        public static string MusicLibrary { get { return T("Music Library", "Biblioteca Música", "Библиотека музыки"); } }
        public static string UserStorage { get { return T("User storage", "Almacenamiento", "Хранилище"); } }
        public static string SdCard { get { return T("SD Card (beta)", "Tarjeta SD (beta)", "SD-карта (бета)"); } }
        public static string AppOnly { get { return T("App only", "Solo en app", "Только в приложении"); } }
        public static string PathPictures { get { return @"Pictures\LocalSend"; } }
        public static string PathVideos { get { return @"Videos\LocalSend"; } }
        public static string PathMusic { get { return @"Music\LocalSend"; } }
        public static string PathUserStorage { get { return @"C:\Data\Users\Public\LocalSend"; } }
        public static string PathSdCard { get { return @"D:\LocalSend"; } }
        public static string PathAppOnly { get { return T("(only visible from this app)", "(solo visible desde esta app)", "(видно только в этом приложении)"); } }
        public static string InteropNotAvailable { get { return T("(interop not available - saving to app storage)", "(interop no disponible - guardando en app)", "(interop недоступен - сохранение в хранилище приложения)"); } }

        // File status
        public static string StatusQueued { get { return T("Queued", "En cola", "В очереди"); } }
        public static string StatusSending { get { return T("Sending...", "Enviando...", "Отправка..."); } }
        public static string StatusCompleted { get { return T("Completed", "Completado", "Завершено"); } }
        public static string StatusFailed { get { return T("Failed", "Error", "Ошибка"); } }
        public static string StatusSkipped { get { return T("Skipped", "Omitido", "Пропущено"); } }
        public static string SentTo(string name) { return T("Sent to ", "Enviado a ", "Отправлено: ") + name; }
        public static string ReceivedFrom(string name) { return T("Received from ", "Recibido de ", "Получено от ") + name; }

        // Storage migration
        public static string MigrateFiles(int n)
        {
            if (IsRussian)
                return "Переместить " + FileCount(n) + @" в C:\Data\Users\Public\LocalSend";
            return T("Move ", "Mover ", "") + n + T(@" file(s) to C:\Data\Users\Public\LocalSend", @" archivo(s) a C:\Data\Users\Public\LocalSend", "");
        }
        public static string Migrating { get { return T("Moving files...", "Moviendo archivos...", "Перемещение файлов..."); } }
        public static string MigrationDone(int moved, int failed)
        {
            if (failed == 0)
            {
                if (IsRussian)
                    return "Успешно перемещено: " + FileCount(moved) + ".";
                return IsEnglish
                    ? moved + " file(s) moved successfully."
                    : moved + " archivo(s) movido(s) correctamente.";
            }

            if (IsRussian)
                return "Перемещено: " + moved + ", ошибок: " + failed + ".";
            return IsEnglish
                ? moved + " moved, " + failed + " failed."
                : moved + " movido(s), " + failed + " fallido(s).";
        }
        public static string MigrationNoInterop { get { return T("Interop required. Files remain in app sandbox (not visible in File Manager).", "Se requiere interop. Los archivos permanecen en el sandbox de la app (no visibles en explorador de archivos).", "Требуется interop. Файлы остаются в песочнице приложения и не видны в файловом менеджере."); } }

        // Changelog Page
        public static string Changelog { get { return T("Changelog", "Cambios", "История изменений"); } }
        public static string V110BrandingTitle { get { return T("Renamed to LocalSend", "Renombrado a LocalSend", "Переименовано в LocalSend"); } }
        public static string V110BrandingDesc { get { return T("The deployment name, tile label, About page and package metadata now use LocalSend for a cleaner release identity.", "El nombre de despliegue, el mosaico, la pantalla Acerca y los metadatos del paquete ahora usan LocalSend para una identidad de lanzamiento más limpia.", "Имя развертывания, плитка, страница сведений и метаданные пакета теперь используют LocalSend для более чистой идентичности релиза."); } }
        public static string V110DesignTitle { get { return T("Refreshed app screens", "Pantallas renovadas", "Обновленные экраны"); } }
        public static string V110DesignDesc { get { return T("Receive, Send, History, Transfer and About were redesigned with better spacing, full-screen flows and Windows Phone-style typography.", "Recibir, Enviar, Historial, Transferencia y Acerca fueron rediseñadas con mejor uso del espacio, flujos de pantalla completa y tipografía estilo Windows Phone.", "Экраны получения, отправки, истории, передачи и сведений обновлены с лучшими отступами, полноэкранными потоками и типографикой Windows Phone."); } }
        public static string V110TransferTitle { get { return T("Transfer flow improvements", "Mejoras al flujo de transferencia", "Улучшения передачи"); } }
        public static string V110TransferDesc { get { return T("Incoming requests now show a clear accept screen, transfers use a dedicated progress page, and active transfers can be minimized and restored.", "Las solicitudes entrantes muestran una pantalla clara de aceptar, las transferencias usan una página dedicada de progreso y las transferencias activas se pueden minimizar y restaurar.", "Входящие запросы показывают понятный экран принятия, передачи используют отдельную страницу прогресса, активные передачи можно свернуть и восстановить."); } }
        public static string V110ReliabilityTitle { get { return T("Large-file reliability", "Fiabilidad con archivos grandes", "Надежность больших файлов"); } }
        public static string V110ReliabilityDesc { get { return T("Receiving now streams directly to disk to avoid out-of-memory failures, cleans partial files on errors, and handles socket stop/start more safely.", "La recepción ahora escribe directo a disco para evitar errores de memoria, limpia archivos parciales ante fallos y maneja el apagado/arranque de sockets con más seguridad.", "Получение теперь пишет напрямую на диск, чтобы избежать нехватки памяти, очищает частичные файлы при ошибках и безопаснее запускает и останавливает сокеты."); } }
        public static string V110PreferencesTitle { get { return T("Receiver preferences", "Preferencias de recepción", "Настройки приема"); } }
        public static string V110PreferencesDesc { get { return T("Quick save, required PIN and automatic receiver startup are now real settings backed by shared app preferences.", "Guardado rápido, PIN requerido e inicio automático de recepción ahora son ajustes reales respaldados por preferencias compartidas de la app.", "Быстрое сохранение, обязательный PIN и автоматический запуск приема теперь являются реальными настройками с общими предпочтениями приложения."); } }
        public static string V110LocalizationTitle { get { return T("Languages and history", "Idiomas e historial", "Языки и история"); } }
        public static string V110LocalizationDesc { get { return T("Added Russian localization, fixed language selection state, and rebuilt history with grouped days, direction, file counts and sizes.", "Se agregó ruso, se corrigió el estado de selección de idioma y se reconstruyó el historial con días agrupados, dirección, cantidad de archivos y tamaños.", "Добавлена русская локализация, исправлено состояние выбора языка, история перестроена с группировкой по дням, направлением, количеством файлов и размерами."); } }
        public static string V101StorageTitle { get { return T(@"File storage: C:\Data\Users\Public\LocalSend", @"Almacenamiento: C:\Data\Users\Public\LocalSend", @"Хранение файлов: C:\Data\Users\Public\LocalSend"); } }
        public static string V101StorageDesc { get { return T(@"Received files (documents, APKs, ZIPs, etc.) are now saved to C:\Data\Users\Public\LocalSend, next to Pictures, Videos and Music. Visible from any file manager. Requires interop. Falls back to app sandbox if not available.", @"Los archivos recibidos (documentos, APKs, ZIPs, etc.) ahora se guardan en C:\Data\Users\Public\LocalSend, junto a Pictures, Videos y Music. Visible desde cualquier explorador de archivos. Requiere interop. Si no está disponible, se guarda en el sandbox de la app.", @"Полученные файлы (документы, APK, ZIP и т. п.) теперь сохраняются в C:\Data\Users\Public\LocalSend рядом с Pictures, Videos и Music. Видны из любого файлового менеджера. Требуется interop. Если недоступно, используется песочница приложения."); } }
        public static string V101MigrateTitle { get { return T("Migrate old files", "Migrar archivos anteriores", "Перенести старые файлы"); } }
        public static string V101MigrateDesc { get { return T(@"If you received files with v1.0.0, they were saved in app sandbox. A new button in Settings lets you move them to C:\Data\Users\Public\LocalSend so they're visible from any file manager.", @"Si recibiste archivos con v1.0.0, se guardaron en el sandbox de la app. Un nuevo botón en Ajustes permite moverlos a C:\Data\Users\Public\LocalSend para que sean visibles desde cualquier explorador de archivos.", @"Если вы получали файлы в v1.0.0, они сохранялись в песочнице приложения. Новая кнопка в настройках переносит их в C:\Data\Users\Public\LocalSend, чтобы они были видны из файлового менеджера."); } }
        public static string V101SdTitle { get { return T("SD Card support (beta)", "Soporte SD Card (beta)", "Поддержка SD-карты (бета)"); } }
        public static string V101SdDesc { get { return T(@"If your device has an SD card, you can now save received files directly to D:\LocalSend. The option appears automatically in Settings > Storage > Other files. Requires interop unlock.", @"Si tu dispositivo tiene tarjeta SD, ahora puedes guardar los archivos recibidos directamente en D:\LocalSend. La opción aparece automáticamente en Ajustes > Almacenamiento > Otros archivos. Requiere interop.", @"Если на устройстве есть SD-карта, полученные файлы можно сохранять прямо в D:\LocalSend. Опция появится автоматически в Настройки > Хранилище > Другие файлы. Требуется interop unlock."); } }
        public static string V101CrashTitle { get { return T("Fix: crash when selecting files", "Fix: crash al seleccionar archivos", "Исправлено: сбой при выборе файлов"); } }
        public static string V101CrashDesc { get { return T("Fixed a crash that occurred when the OS terminated the app in the background during file selection. The app now correctly restores its state after returning from the file picker.", "Se corrigió un crash que ocurría cuando el sistema terminaba la app en segundo plano durante la selección de archivos. La app ahora restaura su estado correctamente al volver del selector de archivos.", "Исправлен сбой, который происходил, когда ОС завершала приложение в фоне во время выбора файлов. Теперь состояние корректно восстанавливается после возврата из выбора файлов."); } }
        public static string V101PortTitle { get { return T("Fix: connection to devices", "Fix: conexión a dispositivos", "Исправлено: подключение к устройствам"); } }
        public static string V101PortDesc { get { return T("Fixed an issue where sending files could fail if the target device info was incomplete. Now falls back to the default LocalSend port (53317).", "Se corrigió un problema donde el envío podía fallar si la información del dispositivo destino estaba incompleta. Ahora usa el puerto por defecto de LocalSend (53317).", "Исправлена проблема, из-за которой отправка могла не работать при неполной информации об устройстве. Теперь используется порт LocalSend по умолчанию (53317)."); } }
        public static string V100Desc { get { return T("Initial release. First and only LocalSend client for Windows Phone 8.1. Send and receive files with Android, iOS, Windows, macOS, and Linux devices over local WiFi.", "Lanzamiento inicial. Primer y único cliente de LocalSend para Windows Phone 8.1. Envía y recibe archivos con dispositivos Android, iOS, Windows, macOS y Linux por WiFi local.", "Первый выпуск. Первый и единственный клиент LocalSend для Windows Phone 8.1. Отправляйте и получайте файлы с Android, iOS, Windows, macOS и Linux по локальному WiFi."); } }

        // Diagnostics Page
        public static string Diagnostics { get { return T("Diagnostics", "Diagnóstico", "Диагностика"); } }
        public static string LogLevelLabel { get { return T("Level:", "Nivel:", "Уровень:"); } }
        public static string LogFilterAll { get { return T("All", "Todo", "Все"); } }
        public static string LogFilterDebug { get { return T("Debug and up", "Debug y superior", "Debug и выше"); } }
        public static string LogFilterInfo { get { return T("Info and up", "Info y superior", "Info и выше"); } }
        public static string LogFilterWarn { get { return T("Warnings and up", "Avisos y superior", "Предупреждения и выше"); } }
        public static string LogFilterError { get { return T("Errors only", "Solo errores", "Только ошибки"); } }
        public static string LogEmpty { get { return T("No log entries at this level", "Sin entradas en este nivel", "Нет записей журнала на этом уровне"); } }
        public static string LogRefresh { get { return T("refresh", "refrescar", "обновить"); } }
        public static string LogShare { get { return T("share", "compartir", "поделиться"); } }
        public static string LogClear { get { return T("clear", "limpiar", "очистить"); } }
        public static string LogShareTitle { get { return T("LocalSend log", "Registro de LocalSend", "Журнал LocalSend"); } }
        public static string LogNoFile { get { return T("No log file on disk yet.", "Aún no hay archivo de registro en disco.", "Файл журнала на диске пока отсутствует."); } }
        public static string LogClearConfirm { get { return T("Clear the in-memory log buffer? The rotating log files on disk are not affected.", "¿Limpiar el búfer de registro en memoria? Los archivos de registro en disco no se ven afectados.", "Очистить буфер журнала в памяти? Файлы журнала на диске не будут затронуты."); } }
        public static string LogStatus(int count, string file)
        {
            return count + T(" entries in memory · file: ", " entradas en memoria · archivo: ", " записей в памяти · файл: ") + file;
        }
        public static string LogSourceLabel { get { return T("Source:", "Fuente:", "Источник:"); } }
        public static string LogSourceMemory { get { return T("Current session", "Sesión actual", "Текущий сеанс"); } }
        public static string LogSourceFile { get { return T("On-disk file (all sessions)", "Archivo en disco (todas las sesiones)", "Файл на диске (все сеансы)"); } }
        public static string LogFileStatus(int lines)
        {
            return lines + T(" lines on disk", " líneas en disco", " строк на диске");
        }
        public static string LogShareDescription(int files)
        {
            if (IsRussian)
                return files + " " + RuPlural(files, "файл журнала", "файла журнала", "файлов журнала");
            return files + T(" log file(s)", " archivo(s) de registro", "");
        }
    }
}
