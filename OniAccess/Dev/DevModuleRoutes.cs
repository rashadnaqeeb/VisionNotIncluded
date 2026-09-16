#if DEBUG
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using OniAccess.Speech;

namespace OniAccess.Dev {
	/// <summary>
	/// The MODULE's routes on the host's dev server: everything that calls module
	/// types (/gui, /input, /loadsave), the /speech tap wiring, and the key
	/// injection patches. Registered from the module's Load and removed in its
	/// Dispose, so a hot reload swaps the handlers along with the code they call.
	/// Inert unless the server actually started. DEBUG-only.
	/// </summary>
	internal static class DevModuleRoutes {
		public static void Register(HarmonyLib.Harmony harmony) {
			var s = DevServer.Instance;
			if (!s.Enabled) return;
			SpeechPipeline.Observer = s.TapSpeech;
			DevInput.Patch(harmony);
			s.RegisterRoute("/gui", (method, body, query) => s.OnMain(GuiInspector.Dump));
			s.RegisterRoute("/input", (method, body, query) => Input(body));
			s.RegisterRoute("/loadsave", (method, body, query) => LoadSave(body));
		}

		public static void Unregister() {
			var s = DevServer.Instance;
			if (!s.Enabled) return;
			SpeechPipeline.Observer = null;
			s.UnregisterRoute("/gui");
			s.UnregisterRoute("/input");
			s.UnregisterRoute("/loadsave");
		}

		// body = "key <KeyCode>[+ctrl][+shift][+alt]" | "action <Action>"; anything else lists the forms.
		private static string Input(string body) {
			var s = DevServer.Instance;
			string spec = (body ?? "").Trim();
			string result;
			if (spec.StartsWith("key ", StringComparison.OrdinalIgnoreCase))
				result = s.OnMain(() => DevInput.Press(spec.Substring(4).Trim()));
			else if (spec.StartsWith("action ", StringComparison.OrdinalIgnoreCase))
				result = s.OnMain(() => DevInput.Action(spec.Substring(7).Trim()));
			else
				return DevInput.Available();
			if (result.StartsWith("[")) return result;
			// The injected key is seen by the frame that follows the pump; a job
			// enqueued now runs on the NEXT pump, so this hop returns once that
			// frame (and the handlers' reaction to the key) has run.
			s.OnMain(() => "");
			return result;
		}

		// Load a save from the main menu and BLOCK until the colony is interactive,
		// so the driver can script "drop me in-game" in one call. body = a save
		// file path, or empty for the newest save of the current DLC.
		private static string LoadSave(string body) {
			var s = DevServer.Instance;
			string sel = (body ?? "").Trim();
			string kick = s.OnMain(() => {
				if (Game.Instance != null) return "[not ready] a colony is already loaded; load only from the main menu\n";
				if (UnityEngine.Object.FindFirstObjectByType<MainMenu>() == null) return "[not ready] not at the main menu; retry\n";
				string path = sel.Length > 0 ? sel : SaveLoader.GetLatestSaveForCurrentDLC();
				if (string.IsNullOrEmpty(path) || !File.Exists(path))
					return "[no save] " + (sel.Length > 0 ? sel : "no save exists for the current DLC") + "\n";
				// The Resume button's own path (MainMenu.ResumeGame): loading overlay, then the scene load.
				KCrashReporter.MOST_RECENT_SAVEFILE = path;
				SaveLoader.SetActiveSaveFilePath(path);
				LoadingOverlay.Load(SaveLoader.LoadScene);
				return "ok " + path + "\n";
			});
			if (!kick.StartsWith("ok ")) return kick;

			var timer = Stopwatch.StartNew();
			while (timer.Elapsed.TotalSeconds < 180) {
				string status = s.OnMain(() =>
					Game.Instance != null && Game.Instance.GameStarted() && !Game.Instance.IsLoading()
						? "loaded: " + DevApi.Screen() + "\n" : "");
				if (status.StartsWith("loaded")) return status;
				Thread.Sleep(250);
			}
			return "[timeout] the colony did not become interactive within 180s\n";
		}
	}
}
#endif
