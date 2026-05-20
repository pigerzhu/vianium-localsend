// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Text;
using System.Threading.Tasks;
using Vianium.LocalSend.Server;
using Windows.Storage.Streams;

namespace Vianium.LocalSend.SmokeTests
{
    /// <summary>
    /// HTTP request parsing is the LocalSend server's front door. Bugs
    /// here look like cross-platform interop failures. We feed the
    /// parser a handful of well-formed and malformed request blobs via
    /// an in-memory <see cref="IInputStream"/> and assert the parsed
    /// <see cref="ServerRequest"/> matches expectations.
    /// </summary>
    internal static class RequestParserSmoke
    {
        public static async Task<SmokeResult> ParseGet()
        {
            string raw =
                "GET /api/localsend/v2/info HTTP/1.1\r\n" +
                "Host: 192.168.0.10:53317\r\n" +
                "User-Agent: LocalSend-Test/0.1\r\n" +
                "Accept: application/json\r\n" +
                "\r\n";

            ServerRequest req = await ParseAsync(raw);
            if (req == null) return Fail("ParseGet", "parser returned null");
            if (req.Method != "GET") return Fail("ParseGet",
                "method got '" + req.Method + "', expected GET");
            if (req.Path != "/api/localsend/v2/info") return Fail("ParseGet",
                "path got '" + req.Path + "'");
            string host;
            if (!req.Headers.TryGetValue("Host", out host) || host != "192.168.0.10:53317")
                return Fail("ParseGet", "Host header lost or wrong");
            return Pass("ParseGet");
        }

        public static async Task<SmokeResult> ParsePostWithBody()
        {
            string bodyJson = "{\"alias\":\"phone\",\"fingerprint\":\"abc\"}";
            string raw =
                "POST /api/localsend/v2/register HTTP/1.1\r\n" +
                "Host: 192.168.0.10:53317\r\n" +
                "Content-Type: application/json\r\n" +
                "Content-Length: " + Encoding.UTF8.GetByteCount(bodyJson) + "\r\n" +
                "\r\n" +
                bodyJson;

            // ParseHeadersAsync reads only the headers; the body sits
            // ready in the underlying reader for ReadBodyAsync.
            ServerRequest req = await ParseAsync(raw);
            if (req == null) return Fail("ParsePostWithBody", "parser returned null");
            if (req.Method != "POST") return Fail("ParsePostWithBody",
                "method got '" + req.Method + "'");
            if (req.Path != "/api/localsend/v2/register") return Fail("ParsePostWithBody",
                "path got '" + req.Path + "'");

            string contentType;
            if (!req.Headers.TryGetValue("Content-Type", out contentType) ||
                contentType != "application/json")
                return Fail("ParsePostWithBody", "Content-Type lost or wrong");

            string lengthRaw;
            if (!req.Headers.TryGetValue("Content-Length", out lengthRaw))
                return Fail("ParsePostWithBody", "Content-Length missing");
            int length;
            if (!int.TryParse(lengthRaw, out length) || length != Encoding.UTF8.GetByteCount(bodyJson))
                return Fail("ParsePostWithBody", "Content-Length wrong: " + lengthRaw);

            return Pass("ParsePostWithBody");
        }

        public static async Task<SmokeResult> ParseMultipleHeaders()
        {
            string raw =
                "GET /api/localsend/v2/info?fingerprint=abc HTTP/1.1\r\n" +
                "Host: phone.local:53317\r\n" +
                "User-Agent: LocalSend-Test/0.1\r\n" +
                "Accept: application/json\r\n" +
                "Accept-Language: en-US,es-ES;q=0.9\r\n" +
                "Connection: keep-alive\r\n" +
                "X-Vianium-Trace: smoke-001\r\n" +
                "\r\n";

            ServerRequest req = await ParseAsync(raw);
            if (req == null) return Fail("ParseMultipleHeaders", "parser returned null");
            if (req.Headers.Count < 6) return Fail("ParseMultipleHeaders",
                "expected >= 6 headers, parsed " + req.Headers.Count);
            string xtrace;
            if (!req.Headers.TryGetValue("X-Vianium-Trace", out xtrace) || xtrace != "smoke-001")
                return Fail("ParseMultipleHeaders", "custom header lost");
            // The query string survives in Path; LocalSend uses
            // ?fingerprint= on the /info endpoint for compatibility.
            if (req.Path == null || req.Path.IndexOf("fingerprint=abc") < 0)
                return Fail("ParseMultipleHeaders",
                    "query string stripped from path: '" + req.Path + "'");
            return Pass("ParseMultipleHeaders");
        }

        public static async Task<SmokeResult> RejectEmpty()
        {
            // A zero-byte client (typical HTTPS-to-HTTP misconfiguration)
            // must return null, not throw. The actual LocalSend server
            // relies on this distinction to close the socket quietly.
            ServerRequest req = await ParseAsync(string.Empty);
            if (req != null)
                return Fail("RejectEmpty",
                    "expected null on zero-byte input, got method='" + req.Method + "'");
            return Pass("RejectEmpty");
        }

        private static async Task<ServerRequest> ParseAsync(string raw)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(raw);
            var stream = new InMemoryRandomAccessStream();
            if (bytes.Length > 0)
            {
                using (DataWriter writer = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(bytes);
                    await writer.StoreAsync();
                    await writer.FlushAsync();
                    writer.DetachStream();
                }
            }

            DataReader reader = new DataReader(stream.GetInputStreamAt(0));
            reader.InputStreamOptions = InputStreamOptions.Partial;
            return await RequestParser.ParseHeadersAsync(reader);
        }

        private static SmokeResult Pass(string name)
        {
            return SmokeResult.Pass(name, 0);
        }

        private static SmokeResult Fail(string name, string reason)
        {
            return SmokeResult.Fail(name, 0, reason);
        }
    }
}
