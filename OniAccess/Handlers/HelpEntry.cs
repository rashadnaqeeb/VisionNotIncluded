using System.Text.RegularExpressions;

namespace OniAccess.Handlers {
	/// <summary>
	/// Simple data class for help list entries.
	/// Each handler provides its own list of these via IAccessHandler.HelpEntries.
	/// Displayed in the ? navigable help list.
	/// Key names are written for Windows (Ctrl, Alt). On macOS the spoken name is
	/// rewritten to what the player presses, following InputUtil: Ctrl with an
	/// arrow key or Space is Option, Ctrl+F is Command+F, other Ctrl combos stay
	/// Ctrl, and Alt is Command.
	/// </summary>
	public sealed class HelpEntry {
		public string KeyName { get; }
		public string Description { get; }

		/// <summary>False for a command this platform does not offer; the help list skips it.</summary>
		public bool Available { get; }

		/// <summary>
		/// Platform check. Defaults to InputUtil.IsMac; tests replace it because
		/// InputUtil reads UnityEngine.SystemInfo, which needs the Unity runtime.
		/// </summary>
		internal static System.Func<bool> IsMacSource = () => Input.InputUtil.IsMac;

		public HelpEntry(string keyName, string description, bool onMac = true) {
			bool mac = IsMacSource();
			KeyName = mac ? ToMacKeyName(keyName) : keyName;
			Description = description;
			Available = onMac || !mac;
		}

		private static readonly Regex CtrlOption =
			new Regex(@"\bCtrl\+((?:Shift\+)?(?:Up|Down|Left|Right|Arrow|Space)\b)");
		private static readonly Regex CtrlFind = new Regex(@"\bCtrl\+F\b");
		private static readonly Regex Alt = new Regex(@"\bAlt\b");

		private static string ToMacKeyName(string keyName) {
			keyName = CtrlOption.Replace(keyName, (string)STRINGS.ONIACCESS.HELP.MAC_OPTION + "+$1");
			keyName = CtrlFind.Replace(keyName, (string)STRINGS.ONIACCESS.HELP.MAC_COMMAND + "+F");
			return Alt.Replace(keyName, (string)STRINGS.ONIACCESS.HELP.MAC_COMMAND);
		}

		public override string ToString() => $"{KeyName}: {Description}";
	}
}
