#if DEBUG
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using OniAccess.Handlers;

namespace OniAccess.Dev {
	/// <summary>
	/// The mod's OWN view of the screen, for the dev driver's /gui: the game
	/// state, the handler stack top-first with the active handler marked, the
	/// game's KScreen stack, and the hotkeys the help screen would list. Lets the
	/// driver see what navigation state the mod is in without the player's ears.
	/// DEBUG-only.
	/// </summary>
	internal static class GuiInspector {
		public static string Dump() {
			var sb = new StringBuilder();
			sb.Append("state: ").Append(DevApi.Screen()).Append('\n');

			sb.Append("handlers (top first):\n");
			if (HandlerStack.Count == 0) sb.Append("  (empty)\n");
			for (int i = HandlerStack.Count - 1; i >= 0; i--) {
				var h = HandlerStack.GetAt(i);
				sb.Append(i == HandlerStack.Count - 1 ? "> " : "  ")
					.Append(h.GetType().Name).Append(" | ").Append(h.DisplayName)
					.Append(h.CapturesAllInput ? " | captures input" : "").Append('\n');
			}

			sb.Append("screens (top first):\n");
			var screens = KScreenManager.Instance == null ? null
				: Traverse.Create(KScreenManager.Instance).Field<List<KScreen>>("screenStack").Value;
			if (screens == null || screens.Count == 0) sb.Append("  (none)\n");
			else {
				for (int i = screens.Count - 1; i >= 0; i--) {
					var s = screens[i];
					if (s == null) continue;
					sb.Append("  ").Append(s.GetType().Name)
						.Append(s.IsScreenActive() ? "" : " | inactive").Append('\n');
				}
			}

			sb.Append("hotkeys:\n");
			foreach (var entry in HandlerStack.CollectHelpEntries())
				sb.Append("  ").Append(entry.KeyName).Append(": ").Append(entry.Description).Append('\n');
			return sb.ToString();
		}
	}
}
#endif
