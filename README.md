# Vianium LocalSend
[![License: PolyForm Noncommercial 1.0.0](https://img.shields.io/badge/License-PolyForm_Noncommercial_1.0.0-orange.svg)](LICENSE) [![Vianium](https://img.shields.io/badge/org-vianium-00A0F0.svg)](https://github.com/vianium) [![Issues](https://img.shields.io/github/issues/vianium/vianium-localsend.svg)](https://github.com/vianium/vianium-localsend/issues)

> Vianium LocalSend — P2P file sharing for Windows Phone 8.1, compatible with the LocalSend protocol, by Angel Careaga.

Created and maintained by [Angel Careaga](https://github.com/AngelCareaga).
Part of the [Vianium](https://github.com/vianium) ecosystem.

Vianium LocalSend is an independent, source-available WinRT/C# implementation
of the [LocalSend](https://localsend.org/) protocol for Windows Phone 8.1. It
turns a Windows Phone 8.1 device into a fully interoperable LocalSend
peer that can send and receive files over the local network with desktop and
mobile LocalSend clients — no internet connection, no account, no
intermediary server.

## Status

**Version:** v0.1.0 (corresponds to Store v1.0.1 plus the license and branding
migration to the Vianium organization)  
**Tier:** 3 (Product repository)  
**License:** PolyForm Noncommercial License 1.0.0  
**Self-contained:** no Vianium sibling dependencies

## What this is

Vianium LocalSend is a Windows Phone 8.1 LocalSend client. It speaks the
[LocalSend protocol v2.x](https://github.com/localsend/protocol) on the local
network, exposing the standard discovery and transfer endpoints over HTTP, and
discovering peers over UDP multicast.

It is built entirely on native WinRT/.NET 4.5 APIs (`StreamSocket`,
`StreamSocketListener`, `DatagramSocket`, `HttpClient`, `StorageFile`) with
**no external dependencies** — the embedded HTTP server, JSON parser,
fingerprint generator, and chunked transfer encoding are all implemented from
scratch so the app builds cleanly under the WP8.1 toolchain and ships as a
self-contained .appx.

## Why it exists

Windows Phone 8.1 has no first-party file-sharing story with the rest of the
modern device ecosystem. The official LocalSend reference application targets
Flutter and modern desktop/mobile platforms; it does not run on Windows Phone
8.1. Vianium LocalSend fills that gap with a clean-room, protocol-compatible
client so that WP8.1 devices remain useful peers in a mixed-OS local network.

It is also a small proof that the WinRT/XAML platform on Windows Phone 8.1 is
still capable of speaking modern peer-to-peer protocols with no native code
and no third-party libraries.

## Interoperability

Vianium LocalSend interoperates with any LocalSend-compliant client on the
same local network, including:

- LocalSend Desktop on **Windows**, **macOS**, and **Linux**
- LocalSend Mobile on **Android** and **iOS**
- Any other independent implementation of the LocalSend protocol

Transfers are direct, peer-to-peer, over local WiFi. No internet connection,
no relay server, and no user account are required.

## Features

- **Peer discovery** via UDP multicast announcements on the LocalSend
  discovery group, with an HTTP-based network-scan fallback when multicast is
  not available.
- **File transfer** via an embedded HTTP/1.1 server running on top of
  `StreamSocketListener`, with chunked transfer encoding so transfers stream
  rather than buffering whole files in memory.
- **Optional HTTPS** with self-signed certificates, matching the LocalSend
  protocol's secure transport mode.
- **Send and receive** of arbitrary files of arbitrary size (images, video,
  audio, documents, APKs, ZIPs).
- **Text-message** transfers (the LocalSend "send text" feature).
- **Transfer history** persisted locally on the device.
- **Spanish and English UI** with system-language auto-detection and a manual
  override in Settings.
- **Configurable storage targets** (Pictures Library, Videos Library, Music
  Library, public `C:\Data\Users\Public\LocalSend`, SD card, or app sandbox).
- **No internet required** for any feature. No account required. No telemetry.

## History

Vianium LocalSend was previously developed and published as `PivoraLocalSend`
under the personal "Pivora" brand. It shipped to the Windows Phone Store in
two releases:

| Store version | Date     | Notes                                           |
|---------------|----------|-------------------------------------------------|
| v1.0.0        | 2026-03  | Initial release. First and only LocalSend client for Windows Phone 8.1. |
| v1.0.1        | 2026-03  | Public storage targets, SD-card support (beta), file-picker crash fix, default-port fallback. |

This repository is the source-available continuation of that app under the
Vianium organization. The v0.1.0 tag here corresponds to the Store v1.0.1
codebase plus the license and branding migration; it is the first source
release.

## Building

This is a Windows Phone 8.1 (WinRT) project. To build it you need:

- Windows 10 (or Windows 8.1) with Visual Studio 2015
- Windows Phone 8.1 SDK
- .NET Framework 4.5
- An ARM/x86 deployment target (physical Windows Phone 8.1 device or WP8.1 emulator)

Open `Vianium.LocalSend.sln` (or `src\Vianium.LocalSend\Vianium.LocalSend.csproj`
directly) in Visual Studio 2015 and build for `Debug|ARM`,
`Release|ARM`, `Debug|x86`, or `Release|x86`. The resulting `.appx` can be
sideloaded onto a WP8.1 device.

The csproj targets `WindowsPhoneApp 8.1` and has no NuGet, no third-party
DLLs, and no Vianium sibling dependencies.

## Repository layout

```
vianium-localsend/
├── src/
│   └── Vianium.LocalSend/
│       ├── Vianium.LocalSend.csproj    WP8.1 AppContainerExe project
│       ├── Package.appxmanifest        Store manifest
│       ├── App.xaml(.cs)               Application entry point
│       ├── Pages/                      MainPage, TransferPage, HistoryPage, AboutPage, ChangelogPage
│       ├── Protocol/                   LocalSend wire types and client/server
│       ├── Server/                     Embedded HTTP server (StreamSocketListener)
│       ├── Network/                    Plain HTTP/HTTPS client
│       ├── Discovery/                  UDP multicast discovery and device tracking
│       ├── Crypto/                     Fingerprint + obfuscation helpers
│       ├── Json/                       SimpleJsonParser (zero-dep)
│       ├── Data/                       Persisted transfer history, storage config
│       ├── Logging/                    AppLog
│       ├── Converters/                 XAML value converters
│       ├── Strings/                    Bilingual UI strings (en/es)
│       └── Assets/                     Tile, splash, store icons
├── LICENSE                             PolyForm Noncommercial 1.0.0
├── NOTICE
├── TRADEMARKS.md                       Protocol attribution and trademark notes
├── README.md
├── AUTHORS.md
├── CONTRIBUTING.md
├── CODE_OF_CONDUCT.md
└── SECURITY.md
```

## Vianium ecosystem

Vianium LocalSend is a stand-alone product. It does **not** depend on any
sibling Vianium repository — there is no link against `vianium-kernel`,
`vianium-crypto`, `vianium-tls`, `vianium-net`, `vianium-http`, or any other
Vianium library. It speaks the LocalSend wire format directly on top of
WinRT's built-in sockets and HTTP stack.

For context, the other Vianium repositories are:

| Repo | Tier | Purpose |
|---|---|---|
| [vianium-kernel](https://github.com/vianium/vianium-kernel) | 1 - Foundation | Result<T>, arena, event bus, base types |
| [vianium-managed-kernel](https://github.com/vianium/vianium-managed-kernel) | 1 - Foundation | Managed (C#) kernel mirror |
| [vianium-crypto](https://github.com/vianium/vianium-crypto) | 1 - Foundation | SHA, HMAC, AES, BigNum, ECDH |
| [vianium-tls](https://github.com/vianium/vianium-tls) | 1 - Foundation | TLS 1.3 Mozilla Modern |
| [vianium-net](https://github.com/vianium/vianium-net) | 1 - Foundation | Sockets, DNS, DoH |
| [vianium-http](https://github.com/vianium/vianium-http) | 1 - Foundation | HTTP/1.1, H2, connection pool |
| [vianium-audio](https://github.com/vianium/vianium-audio) | 1 - Foundation | Real-time audio capture/playback (WASAPI), PCM resampling |
| [vianium-mtproto](https://github.com/vianium/vianium-mtproto) | 2 - Protocol | Telegram MTProto 2.0 + TL |
| [vianium-voip](https://github.com/vianium/vianium-voip) | 2 - Protocol | RTP/SRTP, OPUS, libtgvoip |
| [vianium-genai](https://github.com/vianium/vianium-genai) | 2 - Domain | Provider-neutral AI chat client (Gemini, OpenAI) |
| [vianium-browser](https://github.com/vianium/vianium-browser) | 3 - Product | Web browser |
| [vianigram](https://github.com/vianium/vianigram) | 3 - Product | Telegram client |
| [vianium-music](https://github.com/vianium/vianium-music) | 3 - Product | Music player with YouTube Music streaming |
| [vianium-chat](https://github.com/vianium/vianium-chat) | 3 - Product | Native AI chat client (Gemini, ChatGPT) |

## License

This repository is licensed under the **PolyForm Noncommercial License
1.0.0**. See [LICENSE](LICENSE) and [NOTICE](NOTICE) for full terms.

This product is source-available and free for **non-commercial use**.

### Commercial use

Commercial use is **not** granted by the PolyForm Noncommercial license.
If you represent a company, or want to integrate this code into a
commercial product, a paid service, or an internal corporate system,
contact **[hello@angelcareaga.com](mailto:hello@angelcareaga.com)** to
arrange a commercial license.

## Trademarks and protocol attribution

"LocalSend" is a protocol developed by the LocalSend organization
(<https://localsend.org/>), MIT-licensed at
<https://github.com/localsend/protocol>. Vianium LocalSend is an **independent,
compatible** implementation of that protocol for Windows Phone 8.1. It is
**not affiliated with, endorsed by, or sponsored by** the LocalSend
organization.

See [TRADEMARKS.md](TRADEMARKS.md) for the full trademark and protocol
attribution statement.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Contributions require Developer
Certificate of Origin (DCO) sign-off.

## Security

See [SECURITY.md](SECURITY.md) to report vulnerabilities.

## Support this project

Vianium is maintained by [Angel Careaga](https://angelcareaga.com) as a
personal open-source effort. If `vianium-localsend` is useful to you, please
consider supporting future work:

- 💬 **Community**: join the Vianium Discord — https://discord.gg/NccbAK6jeb
- 💖 **[GitHub Sponsors](https://github.com/sponsors/vianium)** — recurring or one-time
- ☕ **[Buy Me a Coffee](https://www.buymeacoffee.com/soyangelcareaga)** — one-time tip, no account needed
- 🌐 **[angelcareaga.com](https://angelcareaga.com)** — contact, consulting

Detailed channels and a transparency page live in
[`SUPPORT.md`](SUPPORT.md) and
[vianium-docs/donations.md](https://github.com/vianium/vianium-docs/blob/main/donations.md).

## Author

**Angel Careaga**  
[hello@angelcareaga.com](mailto:hello@angelcareaga.com)  
[github.com/AngelCareaga](https://github.com/AngelCareaga)  
[angelcareaga.com](https://angelcareaga.com)
