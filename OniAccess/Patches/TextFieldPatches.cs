using HarmonyLib;
using TMPro;
using UnityEngine;

namespace OniAccess.Patches {
	/// <summary>
	/// Mac text-field keys. The game's text fields treat Command as Windows' Ctrl,
	/// so Command+arrows move by word and Option+arrows by character. This rewrites
	/// the key event to the Mac convention before the field reads it: Option+Left/Right
	/// move by word, Command+Left/Right go to the start and end of the line, and
	/// Command+Up/Down to the start and end of the text. Shift still selects.
	/// Patched on Mac only.
	/// </summary>
	[HarmonyPatch(typeof(TMP_InputField), "KeyPressed")]
	internal static class TMP_InputField_KeyPressed_Patch {
		private static bool Prepare() => Input.InputUtil.IsMac;

		private static void Prefix(Event evt) {
			bool option = (evt.modifiers & EventModifiers.Alt) != 0;
			bool command = (evt.modifiers & EventModifiers.Command) != 0;
			switch (evt.keyCode) {
				case KeyCode.LeftArrow:
				case KeyCode.RightArrow:
					if (command) {
						evt.keyCode = evt.keyCode == KeyCode.LeftArrow ? KeyCode.Home : KeyCode.End;
						evt.modifiers &= ~EventModifiers.Command;
					} else if (option) {
						evt.modifiers = (evt.modifiers & ~EventModifiers.Alt) | EventModifiers.Command;
					}
					break;
				case KeyCode.UpArrow:
				case KeyCode.DownArrow:
					if (command)
						evt.keyCode = evt.keyCode == KeyCode.UpArrow ? KeyCode.Home : KeyCode.End;
					break;
			}
		}
	}
}
