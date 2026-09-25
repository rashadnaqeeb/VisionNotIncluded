#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using OniAccess.Input;
using UnityEngine;

namespace OniAccess.Dev {
	/// <summary>
	/// Key injection for the dev driver's /input. Two paths, matching the two ways
	/// keys reach the mod:
	///   key &lt;KeyCode&gt;[+modifier...]  a raw Unity key for one frame. Handlers
	///       poll UnityEngine.Input.GetKeyDown directly, so a Harmony prefix on
	///       GetKeyDown/GetKey answers true for the injected key (and held
	///       modifiers) during that frame, and GetKeyUp answers true for it the
	///       frame after; without the release the game's controller keeps the
	///       key marked down and ignores every later press of it. ctrl and alt are the mod's logical
	///       modifiers (whatever InputUtil.CtrlHeld/AltHeld check on this
	///       platform: Control, and Command on Mac); control, option and cmd
	///       are the physical keys.
	///   action &lt;Action&gt;  a game action (Escape, Plan1...) as a KButtonEvent,
	///       dispatched through the game's own input tree exactly as the
	///       controller does, so it reaches ModInputRouter and the game's screens.
	/// Patched only when the dev server is up; unpatched with the module. DEBUG-only.
	/// </summary>
	internal static class DevInput {
		private static readonly HashSet<KeyCode> _down = new HashSet<KeyCode>();
		private static readonly HashSet<KeyCode> _held = new HashSet<KeyCode>();
		private static readonly HashSet<KeyCode> _up = new HashSet<KeyCode>();
		private static int _frame = -1;

		public static void Patch(Harmony harmony) {
			harmony.Patch(
				AccessTools.Method(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetKeyDown), new[] { typeof(KeyCode) }),
				prefix: new HarmonyMethod(typeof(DevInput), nameof(GetKeyDownPrefix)));
			harmony.Patch(
				AccessTools.Method(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetKey), new[] { typeof(KeyCode) }),
				prefix: new HarmonyMethod(typeof(DevInput), nameof(GetKeyPrefix)));
			harmony.Patch(
				AccessTools.Method(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetKeyUp), new[] { typeof(KeyCode) }),
				prefix: new HarmonyMethod(typeof(DevInput), nameof(GetKeyUpPrefix)));
		}

		private static bool GetKeyDownPrefix(KeyCode key, ref bool __result) {
			if (Time.frameCount != _frame || !_down.Contains(key)) return true;
			__result = true;
			return false;
		}

		private static bool GetKeyPrefix(KeyCode key, ref bool __result) {
			if (Time.frameCount != _frame || (!_held.Contains(key) && !_down.Contains(key))) return true;
			__result = true;
			return false;
		}

		private static bool GetKeyUpPrefix(KeyCode key, ref bool __result) {
			if (Time.frameCount != _frame + 1 || !_up.Contains(key)) return true;
			__result = true;
			return false;
		}

		/// <summary>Inject a raw key for the current frame. Main thread only.</summary>
		public static string Press(string spec) {
			string[] parts = spec.Split('+');
			KeyCode key;
			try {
				key = (KeyCode)Enum.Parse(typeof(KeyCode), parts[0].Trim(), true);
			} catch (ArgumentException) {
				return "[unknown key] " + parts[0] + " (UnityEngine.KeyCode names, e.g. DownArrow, Slash, F12)\n";
			}
			_down.Clear();
			_held.Clear();
			_up.Clear();
			_down.Add(key);
			_up.Add(key);
			for (int i = 1; i < parts.Length; i++) {
				switch (parts[i].Trim().ToLowerInvariant()) {
					case "ctrl": _held.Add(KeyCode.LeftControl); break;
					case "shift": _held.Add(KeyCode.LeftShift); break;
					case "alt": _held.Add(InputUtil.IsMac ? KeyCode.LeftCommand : KeyCode.LeftAlt); break;
					case "control": _held.Add(KeyCode.LeftControl); break;
					case "option": _held.Add(KeyCode.LeftAlt); break;
					case "cmd": _held.Add(KeyCode.LeftCommand); break;
					default: return "[unknown modifier] " + parts[i] + " (ctrl, shift, alt, control, option, cmd)\n";
				}
			}
			_up.UnionWith(_held);
			_frame = Time.frameCount;
			return "pressed " + spec + " (frame " + _frame + ")\n";
		}

		/// <summary>Dispatch a game action as a key down then key up through the
		/// game's input tree. Main thread only.</summary>
		public static string Action(string name) {
			global::Action action;
			try {
				action = (global::Action)Enum.Parse(typeof(global::Action), name.Trim(), true);
			} catch (ArgumentException) {
				return "[unknown action] " + name + " (game Action names, e.g. Escape, Plan1, ToggleOverlay)\n";
			}
			var controller = KInputManager.currentController ?? Global.GetInputManager().GetDefaultController();
			var handler = KInputHandler.GetInputHandler(controller);
			handler.HandleEvent(new KButtonEvent(controller, InputEventType.KeyDown, action));
			handler.HandleEvent(new KButtonEvent(controller, InputEventType.KeyUp, action));
			return "dispatched " + action + "\n";
		}

		public static string Available() {
			var sb = new StringBuilder("body forms:\n  key <KeyCode>[+ctrl][+shift][+alt] (logical)\n  key <KeyCode>[+control][+option][+cmd] (physical)\n  action <Action>\nactions:\n");
			foreach (var name in Enum.GetNames(typeof(global::Action))) sb.Append("  ").Append(name).Append('\n');
			return sb.ToString();
		}
	}
}
#endif
