using OniAccess.Util;

namespace OniAccess.Speech {
	/// <summary>
	/// Central speech dispatch point for ALL speech output in the mod.
	/// No code should call SpeechEngine.Say() directly -- all speech flows through here.
	///
	/// Pipeline: Caller -> SpeechPipeline -> TextFilter -> SpeechEngine -> Prism
	/// </summary>
	public static class SpeechPipeline {
		/// <summary>
		/// When false, all speech methods return immediately without speaking.
		/// Controlled by ModToggle.
		/// </summary>
		private static bool _enabled = true;

		private static string _lastInterruptText;
		private static float _lastInterruptTime;
		private const float DeduplicateWindowSeconds = 0.05f;

		/// <summary>
		/// Time source for deduplication. Defaults to Unity's unscaledTime;
		/// tests replace this to avoid native Unity calls.
		/// </summary>
		internal static System.Func<float> TimeSource = () => UnityEngine.Time.unscaledTime;

		/// <summary>
		/// Speech backend action. Defaults to SpeechEngine.Say;
		/// tests replace this to capture output without P/Invoke.
		/// </summary>
		internal static System.Action<string, bool> SpeakAction = SpeechEngine.Say;

#if DEBUG
		/// <summary>
		/// Dev-only tap: every line handed to the backend is mirrored here so the
		/// dev server's /speech log can read back what was said. Null in a normal
		/// run; the module's dev routes set it.
		/// </summary>
		internal static System.Action<string> Observer;

		/// <summary>
		/// Dev-only gate: true while the dev driver is acting, so what its actions
		/// make the mod say reaches the tap but not the player's speech output.
		/// Null in a normal run; the module's dev routes set it.
		/// </summary>
		internal static System.Func<bool> Muted;
		private static void Tap(string text) => Observer?.Invoke(text);
		private static bool IsMuted() => Muted != null && Muted();
#else
		private static void Tap(string text) { }
		private static bool IsMuted() => false;
#endif

		/// <summary>
		/// Whether the pipeline is active. When false (mod toggled off),
		/// all methods return immediately.
		/// </summary>
		public static bool IsActive => _enabled;

		/// <summary>
		/// Enable or disable the speech pipeline.
		/// Called by ModToggle when the mod is toggled on/off.
		/// </summary>
		internal static void SetEnabled(bool enabled) {
			_enabled = enabled;
		}

		/// <summary>
		/// Reset pipeline state for test isolation.
		/// </summary>
		internal static void Reset() {
			_lastInterruptText = null;
			_lastInterruptTime = 0f;
			_enabled = true;
		}

		/// <summary>
		/// Interrupt mode: stop current speech and speak immediately.
		/// Used for navigation and announcements where responsiveness matters.
		/// </summary>
		/// <param name="text">Raw text (will be filtered through TextFilter).</param>
		public static void SpeakInterrupt(string text) {
			if (!_enabled) return;

			string filtered = TextFilter.FilterForSpeech(text);
			if (string.IsNullOrEmpty(filtered)) return;
			float now = TimeSource();
			if (filtered == _lastInterruptText && now - _lastInterruptTime < DeduplicateWindowSeconds)
				return;
			_lastInterruptText = filtered;
			_lastInterruptTime = now;
			Tap(filtered);
			if (!IsMuted()) SpeakAction(filtered, true);
		}

		/// <summary>
		/// Queue mode: speak after any current speech finishes, without interrupting.
		/// Used when multiple pieces of info should be spoken in sequence
		/// (e.g., menu name followed by first item, tile element + temperature + building).
		/// </summary>
		/// <param name="text">Raw text (will be filtered through TextFilter).</param>
		public static void SpeakQueued(string text) {
			if (!_enabled) return;

			string filtered = TextFilter.FilterForSpeech(text);
			if (string.IsNullOrEmpty(filtered)) return;
			Tap(filtered);
			if (!IsMuted()) SpeakAction(filtered, false);
		}

	}
}
