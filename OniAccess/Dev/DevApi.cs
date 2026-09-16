#if DEBUG
using System.Reflection;
using OniAccess.Handlers;
using OniAccess.Speech;

namespace OniAccess.Dev {
	/// <summary>
	/// A tiny PUBLIC surface for eval'd code to reach from. Mono.CSharp eval runs
	/// in its own dynamic assembly and sees only PUBLIC members, while much of the
	/// mod is internal; rather than mirror the codebase, this exposes the module
	/// assembly to reflect into plus a couple of high-use probes. DEBUG-only.
	/// </summary>
	public static class DevApi {
		/// <summary>The current module assembly: reflect into internals with
		/// DevApi.Asm.GetType("OniAccess.Handlers.ContextDetector") etc.</summary>
		public static Assembly Asm => typeof(DevApi).Assembly;

		/// <summary>Speak a probe line through the real speech path (also lands in /speech).</summary>
		public static void Say(string text) => SpeechPipeline.SpeakInterrupt(text);

		/// <summary>Game state and the active handler: "menu | MainMenuHandler | Main menu".</summary>
		public static string Screen() {
			string state = Game.Instance == null ? "menu"
				: Game.Instance.GameStarted() && !Game.Instance.IsLoading() ? "ingame" : "loading";
			var h = HandlerStack.ActiveHandler;
			return h == null ? state + " | (no handler)" : state + " | " + h.GetType().Name + " | " + h.DisplayName;
		}
	}
}
#endif
