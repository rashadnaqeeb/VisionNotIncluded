using System.Collections.Generic;
using System.Text;

using OniAccess.Speech;

namespace OniAccess.Tests {
	/// <summary>
	/// Offline tests for PrismBackend.ToUtf8, the boundary that hands text to the
	/// native Prism library. Prism reads to the first null byte and validates the
	/// result as UTF-8, rejecting anything else with PRISM_ERROR_INVALID_UTF8 — a
	/// silent failure where nothing is spoken. The original code marshaled with
	/// CharSet.Ansi, which encodes non-ASCII through the system code page (e.g. "é"
	/// became the single byte 0xE9 on CP1252), and Prism correctly rejected those
	/// bytes as invalid UTF-8. These tests pin the bytes that actually cross the
	/// boundary: valid UTF-8, null-terminated.
	/// </summary>
	static class PrismMarshalTests {
		public static IEnumerable<(string, bool, string)> All() {
			yield return AsciiRoundTrips();
			yield return AccentedIsValidMultiByteUtf8();
			yield return NotAnsiCodePage();
			yield return NullTerminated();
			yield return MultiByteGlyphEncoded();
			yield return EmptyStringIsJustTerminator();
		}

		private static (string, bool, string) Assert(string name, bool ok, string detail)
			=> (name, ok, ok ? "OK" : detail);

		private static string Hex(byte[] b) => string.Join(" ", System.Array.ConvertAll(b, x => x.ToString("X2")));

		private static (string, bool, string) AsciiRoundTrips() {
			var bytes = PrismBackend.ToUtf8("hello");
			// 68 65 6C 6C 6F 00
			bool ok = bytes.Length == 6
				&& bytes[0] == 0x68 && bytes[4] == 0x6F && bytes[5] == 0x00;
			return Assert("AsciiRoundTrips", ok, Hex(bytes));
		}

		private static (string, bool, string) AccentedIsValidMultiByteUtf8() {
			var bytes = PrismBackend.ToUtf8("café");
			// 63 61 66 C3 A9 00 — "é" is the 2-byte sequence C3 A9, valid UTF-8.
			bool ok = bytes.Length == 6
				&& bytes[3] == 0xC3 && bytes[4] == 0xA9 && bytes[5] == 0x00;
			return Assert("AccentedIsValidMultiByteUtf8", ok, Hex(bytes));
		}

		private static (string, bool, string) NotAnsiCodePage() {
			// The bug: CharSet.Ansi (CP1252) would encode "é" as the lone byte 0xE9,
			// which Prism rejects as an invalid UTF-8 lead byte. UTF-8 never produces
			// a bare 0xE9, so its presence would mean we regressed to ANSI marshaling.
			var bytes = PrismBackend.ToUtf8("café");
			bool sawBareE9 = false;
			for (int i = 0; i < bytes.Length; i++)
				if (bytes[i] == 0xE9) sawBareE9 = true;
			bool ok = !sawBareE9;
			return Assert("NotAnsiCodePage", ok, Hex(bytes));
		}

		private static (string, bool, string) NullTerminated() {
			// Prism reads to the first null, so the terminator must be the final byte
			// and must not appear earlier in normal text.
			var bytes = PrismBackend.ToUtf8("über");
			bool ok = bytes[bytes.Length - 1] == 0x00;
			for (int i = 0; i < bytes.Length - 1; i++)
				if (bytes[i] == 0x00) ok = false;
			return Assert("NullTerminated", ok, Hex(bytes));
		}

		private static (string, bool, string) MultiByteGlyphEncoded() {
			// U+2248 (≈) is the 3-byte sequence E2 89 88. This is the character from
			// the Prism issue that triggered the investigation.
			var bytes = PrismBackend.ToUtf8("x≈");
			// 78 E2 89 88 00
			bool ok = bytes.Length == 5
				&& bytes[0] == 0x78
				&& bytes[1] == 0xE2 && bytes[2] == 0x89 && bytes[3] == 0x88
				&& bytes[4] == 0x00;
			// Decoding back must reproduce the original string.
			ok = ok && Encoding.UTF8.GetString(bytes, 0, bytes.Length - 1) == "x≈";
			return Assert("MultiByteGlyphEncoded", ok, Hex(bytes));
		}

		private static (string, bool, string) EmptyStringIsJustTerminator() {
			var bytes = PrismBackend.ToUtf8("");
			bool ok = bytes.Length == 1 && bytes[0] == 0x00;
			return Assert("EmptyStringIsJustTerminator", ok, Hex(bytes));
		}
	}
}
