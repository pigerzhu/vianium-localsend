// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Threading.Tasks;
using Vianium.LocalSend.Json;
using Vianium.LocalSend.Protocol;

namespace Vianium.LocalSend.SmokeTests
{
    /// <summary>
    /// End-to-end protocol-shape tests. We don't open sockets — we
    /// construct the JSON payloads a real prepare-upload exchange
    /// produces and assert that our encoder/decoder round-trip them
    /// losslessly, matching the LocalSend v2 specification.
    ///
    /// These cover the wire contract that desktop / Android clients
    /// expect when they reach a WP 8.1 instance running Vianium
    /// LocalSend.
    /// </summary>
    internal static class ProtocolFlowSmoke
    {
        public static Task<SmokeResult> PrepareUploadShape()
        {
            // What a remote desktop client POSTs to
            //   POST /api/localsend/v2/prepare-upload
            // to ask permission to send 2 files. Keys per spec:
            //   {
            //     "info": <DeviceInfo>,
            //     "files": {
            //       "<id1>": <FileDto>,
            //       "<id2>": <FileDto>
            //     }
            //   }
            var info = new DeviceInfo();
            info.Alias = "Desktop";
            info.Version = ProtocolConstants.Version;
            info.DeviceType = "desktop";
            info.Fingerprint = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";
            info.Port = ProtocolConstants.Port;
            info.Protocol = ProtocolConstants.Protocol;
            info.Download = false;

            var photo = new FileDto();
            photo.Id = "fid-photo";
            photo.FileName = "vacation.jpg";
            photo.Size = 4_823_104L;
            photo.FileType = "image/jpeg";

            var doc = new FileDto();
            doc.Id = "fid-doc";
            doc.FileName = "report.pdf";
            doc.Size = 92_137L;
            doc.FileType = "application/pdf";

            // Compose
            var files = new JsonObject();
            files.Set(photo.Id, photo.ToJson());
            files.Set(doc.Id, doc.ToJson());

            var root = new JsonObject();
            root.Set("info", info.ToInfoJson());
            root.Set("files", files);

            // Roundtrip the whole envelope
            string wire = root.ToString();
            JsonValue parsed = SimpleJsonParser.Parse(wire);
            if (!parsed.IsObject) return Fail("PrepareUploadShape", "envelope not parsed as object");

            JsonValue infoNode = parsed["info"];
            JsonValue filesNode = parsed["files"];
            if (!infoNode.IsObject) return Fail("PrepareUploadShape", "info node not an object");
            if (!filesNode.IsObject) return Fail("PrepareUploadShape", "files node not an object");

            DeviceInfo backInfo = DeviceInfo.FromJson(infoNode);
            if (backInfo.Fingerprint != info.Fingerprint)
                return Fail("PrepareUploadShape", "fingerprint lost in envelope");

            // Each file id key must map back to its FileDto
            if (!filesNode.ContainsKey(photo.Id) || !filesNode.ContainsKey(doc.Id))
                return Fail("PrepareUploadShape", "file ids lost as object keys");

            FileDto backPhoto = FileDto.FromJson(photo.Id, filesNode[photo.Id]);
            FileDto backDoc = FileDto.FromJson(doc.Id, filesNode[doc.Id]);
            if (backPhoto.FileName != photo.FileName) return Fail("PrepareUploadShape", "photo filename lost");
            if (backPhoto.Size != photo.Size) return Fail("PrepareUploadShape", "photo size lost");
            if (backDoc.FileType != doc.FileType) return Fail("PrepareUploadShape", "doc fileType lost");

            return Pass("PrepareUploadShape");
        }

        public static Task<SmokeResult> PrepareUploadResponseShape()
        {
            // Per spec the receiver responds with:
            //   {
            //     "sessionId": "<guid>",
            //     "files": {
            //       "<fid>": "<token>"
            //     }
            //   }
            string sessionId = TokenGenerator.GenerateSessionId();
            string token = TokenGenerator.Generate();
            const string fileId = "fid-photo";

            var files = new JsonObject();
            files.Set(fileId, new JsonString(token));

            var resp = new JsonObject();
            resp.Set("sessionId", new JsonString(sessionId));
            resp.Set("files", files);

            string wire = resp.ToString();
            JsonValue parsed = SimpleJsonParser.Parse(wire);

            if (parsed["sessionId"].AsString() != sessionId)
                return Fail("PrepareUploadResponseShape", "sessionId lost");
            if (parsed["files"][fileId].AsString() != token)
                return Fail("PrepareUploadResponseShape", "token lost for fileId");

            // sessionId must remain a parseable GUID after the trip
            Guid g;
            if (!Guid.TryParse(parsed["sessionId"].AsString(), out g) || g == Guid.Empty)
                return Fail("PrepareUploadResponseShape", "sessionId mangled into non-GUID");

            // token must remain 64-char lower hex
            string roundtripToken = parsed["files"][fileId].AsString();
            if (roundtripToken.Length != 64)
                return Fail("PrepareUploadResponseShape",
                    "token length changed to " + roundtripToken.Length);

            return Pass("PrepareUploadResponseShape");
        }

        public static Task<SmokeResult> RegisterPayloadRoundtrip()
        {
            // /api/localsend/v2/register replaces the multicast handshake
            // when discovery is via HTTP fallback (some Android networks
            // drop multicast). The payload is a DeviceInfo with port +
            // protocol so the peer knows how to reach back.
            var src = new DeviceInfo();
            src.Alias = "phone";
            src.Version = ProtocolConstants.Version;
            src.DeviceModel = "Windows Phone";
            src.DeviceType = "mobile";
            src.Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
            src.Port = ProtocolConstants.Port;
            src.Protocol = "http";
            src.Download = false;

            string wire = src.ToRegisterJson().ToString();
            JsonValue parsed = SimpleJsonParser.Parse(wire);
            DeviceInfo back = DeviceInfo.FromJson(parsed);

            if (back == null) return Fail("RegisterPayloadRoundtrip", "FromJson returned null");
            if (back.Alias != src.Alias) return Fail("RegisterPayloadRoundtrip", "alias lost");
            if (back.Fingerprint != src.Fingerprint) return Fail("RegisterPayloadRoundtrip", "fingerprint lost");
            if (back.Port != src.Port) return Fail("RegisterPayloadRoundtrip", "port lost");
            if (back.Protocol != src.Protocol) return Fail("RegisterPayloadRoundtrip", "protocol lost");

            // The receiver of /register relies on this to dial back
            // 'http://<addr>:<port>/api/localsend/v2/info'.
            return Pass("RegisterPayloadRoundtrip");
        }

        private static Task<SmokeResult> Pass(string name)
        {
            return Task.FromResult(SmokeResult.Pass(name, 0));
        }

        private static Task<SmokeResult> Fail(string name, string reason)
        {
            return Task.FromResult(SmokeResult.Fail(name, 0, reason));
        }
    }
}
