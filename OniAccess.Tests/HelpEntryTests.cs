using System.Collections.Generic;

using OniAccess.Handlers;

namespace OniAccess.Tests {
	/// <summary>
	/// Offline tests for the macOS help key-name rewrite. Key names are authored
	/// for Windows; on Mac the spoken name must say the key the player presses.
	/// </summary>
	static class HelpEntryTests {
		private static (string, bool, string) Check(string name, bool mac, string input, string expected) {
			HelpEntry.IsMacSource = () => mac;
			try {
				string actual = new HelpEntry(input, "").KeyName;
				bool ok = actual == expected;
				return (name, ok, ok ? "OK" : $"expected \"{expected}\", got \"{actual}\"");
			} finally {
				HelpEntry.IsMacSource = () => false;
			}
		}

		private static (string, bool, string) CheckAvailable(string name, bool mac, bool onMac, bool expected) {
			HelpEntry.IsMacSource = () => mac;
			try {
				bool actual = new HelpEntry("Alt+H", "", onMac).Available;
				return (name, actual == expected, actual == expected ? "OK" : $"expected {expected}, got {actual}");
			} finally {
				HelpEntry.IsMacSource = () => false;
			}
		}

		public static IEnumerable<(string, bool, string)> All() {
			// Ctrl with an arrow key or Space is Option on Mac, Shift kept in place
			yield return Check("MacKeyNameArrowCtrlIsOption", true, "Ctrl+Shift+Left/Right", "Option+Shift+Left/Right");
			yield return Check("MacKeyNameArrowKeysCtrlIsOption", true, "Ctrl+Arrow keys", "Option+Arrow keys");
			yield return Check("MacKeyNameSpaceCtrlIsOption", true, "Ctrl+Space", "Option+Space");
			// PageUp ends in Up but is not an arrow; macOS leaves Control+PageUp alone
			yield return Check("MacKeyNamePageUpCtrlStays", true, "Ctrl+PageUp/Down", "Ctrl+PageUp/Down");
			yield return Check("MacKeyNameOtherCtrlStays", true, "Ctrl+Tab/Ctrl+Shift+Tab", "Ctrl+Tab/Ctrl+Shift+Tab");
			yield return Check("MacKeyNameFindIsCommand", true, "Ctrl+F", "Command+F");
			// F12 starts with F but is not find
			yield return Check("MacKeyNameF12CtrlStays", true, "Ctrl+Shift+F12", "Ctrl+Shift+F12");
			yield return Check("MacKeyNameAltIsCommand", true, "Alt+Up/Down", "Command+Up/Down");
			yield return Check("WindowsKeyNameUnchanged", false, "Ctrl+Up/Down", "Ctrl+Up/Down");
			yield return CheckAvailable("MacOnlyEntryHiddenOnMac", true, false, false);
			yield return CheckAvailable("MacOnlyEntryShownOnWindows", false, false, true);
		}
	}
}
