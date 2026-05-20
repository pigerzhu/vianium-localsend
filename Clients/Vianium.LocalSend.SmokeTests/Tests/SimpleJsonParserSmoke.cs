// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Threading.Tasks;
using Vianium.LocalSend.Json;

namespace Vianium.LocalSend.SmokeTests
{
    /// <summary>
    /// Exercises the SimpleJsonParser API on the shapes that the
    /// LocalSend wire format actually uses: nulls, booleans, integers
    /// (port numbers + sizes), strings with UTF-8 + escapes, objects,
    /// and arrays. Every public-facing accessor on <see cref="JsonValue"/>
    /// is touched at least once.
    /// </summary>
    internal static class SimpleJsonParserSmoke
    {
        public static Task<SmokeResult> ParsePrimitives()
        {
            JsonValue n = SimpleJsonParser.Parse("null");
            if (n == null) return Fail("ParsePrimitives", "null parsed to null reference");
            if (!n.IsNull) return Fail("ParsePrimitives", "'null' did not set IsNull=true");

            JsonValue t = SimpleJsonParser.Parse("true");
            if (!t.IsBoolean || !t.AsBool()) return Fail("ParsePrimitives", "'true' did not parse as true");

            JsonValue f = SimpleJsonParser.Parse("false");
            if (!f.IsBoolean || f.AsBool()) return Fail("ParsePrimitives", "'false' did not parse as false");

            return Pass("ParsePrimitives");
        }

        public static Task<SmokeResult> ParseObjectAndArray()
        {
            JsonValue obj = SimpleJsonParser.Parse("{\"alias\":\"phone\",\"port\":53317,\"announce\":true}");
            if (!obj.IsObject) return Fail("ParseObjectAndArray", "expected object");
            if (!obj.ContainsKey("alias")) return Fail("ParseObjectAndArray", "missing 'alias' key");
            if (obj["alias"].AsString() != "phone") return Fail("ParseObjectAndArray",
                "'alias' value got '" + obj["alias"].AsString() + "'");
            if (obj["port"].AsInt() != 53317) return Fail("ParseObjectAndArray",
                "'port' got " + obj["port"].AsInt());
            if (!obj["announce"].AsBool()) return Fail("ParseObjectAndArray", "'announce' lost true");

            JsonValue arr = SimpleJsonParser.Parse("[1,2,3,\"four\",null]");
            if (!arr.IsArray) return Fail("ParseObjectAndArray", "expected array");
            if (arr[0].AsInt() != 1) return Fail("ParseObjectAndArray", "arr[0] != 1");
            if (arr[3].AsString() != "four") return Fail("ParseObjectAndArray", "arr[3] != 'four'");
            if (!arr[4].IsNull) return Fail("ParseObjectAndArray", "arr[4] not null");

            return Pass("ParseObjectAndArray");
        }

        public static Task<SmokeResult> ParseEscapedStrings()
        {
            // Standard escapes
            JsonValue v = SimpleJsonParser.Parse("\"line1\\nline2\\ttab\\\"q\\\\back\"");
            if (!v.IsString) return Fail("ParseEscapedStrings", "expected string");
            string decoded = v.AsString();
            if (decoded != "line1\nline2\ttab\"q\\back")
                return Fail("ParseEscapedStrings", "got: " + decoded);

            // Unicode escape
            JsonValue u = SimpleJsonParser.Parse("\"\\u00e9\\u4e2d\"");
            if (u.AsString() != "é中")
                return Fail("ParseEscapedStrings", "unicode escape decoded to: " + u.AsString());

            // Forward slash escape (legal but optional in JSON)
            JsonValue s = SimpleJsonParser.Parse("\"a\\/b\"");
            if (s.AsString() != "a/b")
                return Fail("ParseEscapedStrings", "forward slash escape decoded to: " + s.AsString());

            return Pass("ParseEscapedStrings");
        }

        public static Task<SmokeResult> ParseNumbers()
        {
            JsonValue n1 = SimpleJsonParser.Parse("0");
            if (!n1.IsNumber || n1.AsInt() != 0) return Fail("ParseNumbers", "zero");

            JsonValue n2 = SimpleJsonParser.Parse("-42");
            if (n2.AsInt() != -42) return Fail("ParseNumbers", "negative integer");

            JsonValue n3 = SimpleJsonParser.Parse("1234567890");
            if (n3.AsLong() != 1234567890L) return Fail("ParseNumbers", "10-digit long");

            JsonValue n4 = SimpleJsonParser.Parse("3.14159");
            if (Math.Abs(n4.AsDouble() - 3.14159) > 0.00001) return Fail("ParseNumbers", "pi");

            JsonValue n5 = SimpleJsonParser.Parse("1.5e3");
            if (Math.Abs(n5.AsDouble() - 1500.0) > 0.01) return Fail("ParseNumbers", "exponential");

            return Pass("ParseNumbers");
        }

        public static Task<SmokeResult> RoundtripObject()
        {
            // Build a JsonObject, serialize via ToString, parse it back,
            // and verify field-by-field. This exercises the encoder side
            // (which DeviceInfo and FileDto rely on for ToJson).
            var obj = new JsonObject();
            obj.Set("alias", new JsonString("Windows Phone"));
            obj.Set("port", new JsonNumber(53317));
            obj.Set("download", JsonBoolean.False);
            obj.Set("fingerprint", new JsonString("abcdef0123456789"));

            string encoded = obj.ToString();

            JsonValue decoded = SimpleJsonParser.Parse(encoded);
            if (!decoded.IsObject)
                return Fail("RoundtripObject", "decoded not object: " + encoded);
            if (decoded["alias"].AsString() != "Windows Phone")
                return Fail("RoundtripObject", "alias lost: '" + decoded["alias"].AsString() + "'");
            if (decoded["port"].AsInt() != 53317)
                return Fail("RoundtripObject", "port lost: " + decoded["port"].AsInt());
            if (decoded["download"].AsBool() != false)
                return Fail("RoundtripObject", "download lost");
            if (decoded["fingerprint"].AsString() != "abcdef0123456789")
                return Fail("RoundtripObject", "fingerprint lost");

            return Pass("RoundtripObject");
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
