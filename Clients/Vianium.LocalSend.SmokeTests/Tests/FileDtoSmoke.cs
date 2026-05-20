// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System.Threading.Tasks;
using Vianium.LocalSend.Json;
using Vianium.LocalSend.Protocol;

namespace Vianium.LocalSend.SmokeTests
{
    /// <summary>
    /// FileDto carries one entry inside the prepare-upload manifest.
    /// We verify (a) a full roundtrip preserves every field and (b) the
    /// mime-type guesser correctly classifies the extensions LocalSend
    /// users most often send.
    /// </summary>
    internal static class FileDtoSmoke
    {
        public static Task<SmokeResult> Roundtrip()
        {
            var src = new FileDto();
            src.Id = "abc123";
            src.FileName = "vacation.jpg";
            src.Size = 4_823_104L;
            src.FileType = "image/jpeg";
            src.Hash = "deadbeefcafebabe";
            src.Preview = "data:image/jpeg;base64,/9j/4AA";

            JsonObject encoded = src.ToJson();
            // Spec requires these keys in the prepare-upload manifest:
            if (!encoded.ContainsKey("id")) return Fail("Roundtrip", "missing 'id'");
            if (!encoded.ContainsKey("fileName")) return Fail("Roundtrip", "missing 'fileName'");
            if (!encoded.ContainsKey("size")) return Fail("Roundtrip", "missing 'size'");
            if (!encoded.ContainsKey("fileType")) return Fail("Roundtrip", "missing 'fileType'");

            string wire = encoded.ToString();
            JsonValue parsed = SimpleJsonParser.Parse(wire);
            FileDto back = FileDto.FromJson(src.Id, parsed);

            if (back == null) return Fail("Roundtrip", "FromJson returned null");
            if (back.Id != src.Id) return Fail("Roundtrip", "id lost");
            if (back.FileName != src.FileName) return Fail("Roundtrip", "fileName lost");
            if (back.Size != src.Size) return Fail("Roundtrip",
                "size lost: " + back.Size + " != " + src.Size);
            if (back.FileType != src.FileType) return Fail("Roundtrip", "fileType lost");
            if (back.Hash != src.Hash) return Fail("Roundtrip", "hash lost");
            if (back.Preview != src.Preview) return Fail("Roundtrip", "preview lost");

            // Verify null-tolerant roundtrip
            var sparse = new FileDto();
            sparse.Id = "x";
            sparse.FileName = "raw.bin";
            sparse.Size = 0;
            sparse.FileType = "application/octet-stream";
            string sparseWire = sparse.ToJson().ToString();
            FileDto sparseBack = FileDto.FromJson(sparse.Id, SimpleJsonParser.Parse(sparseWire));
            if (sparseBack.Hash != null) return Fail("Roundtrip", "missing hash should decode to null");
            if (sparseBack.Preview != null) return Fail("Roundtrip", "missing preview should decode to null");

            return Pass("Roundtrip");
        }

        public static Task<SmokeResult> MimeGuesser()
        {
            // Sample of the extension table users actually send.
            string[] cases = new string[]
            {
                "photo.jpg|image/jpeg",
                "photo.JPEG|image/jpeg",
                "render.png|image/png",
                "anim.gif|image/gif",
                "doc.pdf|application/pdf",
                "song.mp3|audio/mpeg",
                "lossless.flac|audio/flac",
                "video.mp4|video/mp4",
                "screencast.mkv|video/x-matroska",
                "archive.zip|application/zip",
                "code.json|application/json",
                "page.html|text/html",
                "install.apk|application/vnd.android.package-archive",
                "weird.unknown|application/octet-stream",
                "no_ext|application/octet-stream",
                "|application/octet-stream"
            };

            foreach (string row in cases)
            {
                int pipe = row.IndexOf('|');
                string fileName = row.Substring(0, pipe);
                string expected = row.Substring(pipe + 1);
                string actual = FileDto.GuessMimeType(fileName);
                if (actual != expected)
                    return Fail("MimeGuesser",
                        "'" + fileName + "' expected '" + expected + "', got '" + actual + "'");
            }

            return Pass("MimeGuesser");
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
