using System;
using System.Collections.Generic;
using OniAccess.Util;
using UnityEngine;

namespace OniAccess.Speech {
	/// <summary>Which Prism backend the player wants speech routed to.</summary>
	public enum SpeechOutputMode {
		/// <summary>Prism's best-ranked backend: the running screen reader, or the system voice when none runs.</summary>
		ScreenReader,
		/// <summary>The platform's own TTS (AVSpeech on Mac, SAPI on Windows), which queues announcements.</summary>
		SystemVoice,
	}

	/// <summary>
	/// Starts speech on the backend ModConfig asks for and pushes the configured
	/// voice, rate, and volume into it. The system voice is Prism's SAPI backend
	/// on Windows and the mod's own <see cref="MacSystemVoiceBackend"/> on Mac,
	/// where Prism's AVSpeech backend would leave gaps between queued lines. The
	/// 0-100 settings are the player-facing scale; the backends take [0.0, 1.0]
	/// and reject anything outside it. A voice or rate the player has never
	/// touched leaves the operating system's own choice in place: SAPI starts on
	/// the voice and rate from the Windows Speech control panel, and on macOS
	/// the mod applies the Spoken Content voice and rate itself. Volume is
	/// different: the system level is set for reading alone and can sit too low
	/// to hear over the game's music, so it always starts at full until the
	/// player turns it down.
	/// </summary>
	public static class SpeechOutputSelector {
		/// <summary>A rate the player has not set; the backend's current rate shows in its place.</summary>
		public const int Unset = -1;
		public const int VolumeDefault = 100;

		/// <summary>
		/// The Prism registry id of this platform's system TTS, or BACKEND_BEST where
		/// Prism is not the way to it: on Mac the system voice is
		/// <see cref="MacSystemVoiceBackend"/>, so BEST is only reached there when
		/// that backend cannot start, and it lands on the screen reader.
		/// </summary>
		public static ulong PlatformSystemVoice() {
			switch (Application.platform) {
				case RuntimePlatform.WindowsPlayer: return PrismBackend.BACKEND_SAPI;
				case RuntimePlatform.LinuxPlayer: return PrismBackend.BACKEND_SPEECH_DISPATCHER;
				default: return PrismBackend.BACKEND_BEST;
			}
		}

		/// <summary>
		/// Start (or restart) speech on the backend the config selects, then apply the
		/// voice settings when that backend accepts them.
		/// </summary>
		public static void Start() {
			bool systemVoice = ConfigManager.Config.SpeechOutput == SpeechOutputMode.SystemVoice;
			if (systemVoice && Application.platform == RuntimePlatform.OSXPlayer) {
				var mac = new MacSystemVoiceBackend();
				if (SpeechEngine.Initialize(mac)) {
					ApplySettings(mac);
					return;
				}
				Log.Error("Mac system voice unavailable, falling back to Prism's best backend");
			}
			ulong id = systemVoice ? PlatformSystemVoice() : PrismBackend.BACKEND_BEST;
			// On a Mac the Macaw screen reader's Prism plugin joins the registry, so
			// BEST is Macaw while it runs and VoiceOver or AVSpeech otherwise.
			string plugin = Application.platform == RuntimePlatform.OSXPlayer ? PrismBackend.MacawPluginPath() : null;
			var backend = new PrismBackend(id, plugin);
			SpeechEngine.Initialize(backend);
			if (backend.SupportsVoiceControl)
				ApplySettings(backend);
		}

		/// <summary>The running backend when it takes voice settings; otherwise null.</summary>
		public static IVoiceControl VoiceControlledBackend {
			get {
				var backend = SpeechEngine.Backend;
				if (backend is MacSystemVoiceBackend mac)
					return mac.IsAvailable ? mac : null;
				var prism = backend as PrismBackend;
				return prism != null && prism.SupportsVoiceControl ? prism : null;
			}
		}

		/// <summary>
		/// Push the configured voice, rate, and volume into <paramref name="backend"/>.
		/// Unset values fall back to the operating system's Spoken Content settings on
		/// macOS and are left alone elsewhere.
		/// </summary>
		public static void ApplySettings(IVoiceControl backend) {
			var config = ConfigManager.Config;
			bool haveSavedVoice = !string.IsNullOrEmpty(config.SystemVoice) || !string.IsNullOrEmpty(config.SystemVoiceIdentifier);
			// The Spoken Content voice serves as the voice when none is saved and as
			// the owner of the rate when none is saved, even under a picked voice, so
			// the speed heard in the picker is the speed heard on the next launch.
			string systemVoiceId = null, systemName = null, systemLanguage = null;
			bool haveSystemVoice = Application.platform == RuntimePlatform.OSXPlayer
				&& (!haveSavedVoice || config.SystemVoiceRate == Unset)
				&& MacSpokenContent.TryGetDefaultVoice(out systemVoiceId, out systemName, out systemLanguage);

			if (haveSavedVoice) {
				int index = FindVoice(backend, config.SystemVoiceIdentifier, config.SystemVoice, config.SystemVoiceLanguage);
				if (index >= 0)
					backend.SetVoice(index);
				else
					Log.Error($"Configured voice \"{config.SystemVoice}\" ({config.SystemVoiceLanguage}, {config.SystemVoiceIdentifier}) is not installed, using the backend default");
			} else if (haveSystemVoice) {
				int index = FindVoice(backend, systemVoiceId, systemName, systemLanguage);
				if (index >= 0) {
					backend.SetVoice(index);
					Log.Info($"Using the macOS Spoken Content voice {systemName} ({systemLanguage})");
				} else {
					Log.Error($"macOS Spoken Content voice {systemName} ({systemLanguage}) is not in the voice list, using the backend default");
				}
			}

			if (config.SystemVoiceRate != Unset) {
				backend.SetRate(ToUnit(config.SystemVoiceRate));
			} else if (haveSystemVoice) {
				float rate = MacSpokenContent.DefaultVoiceRate(systemVoiceId);
				if (rate >= 0f) backend.SetRate(rate);
			}
			backend.SetVolume(ToUnit(config.SystemVoiceVolume));
		}

		/// <summary>The rate slider's value: the saved setting, else what the backend is speaking at.</summary>
		public static int RatePercent(IVoiceControl backend) {
			int saved = ConfigManager.Config.SystemVoiceRate;
			return saved != Unset ? saved : ToPercent(backend.GetRate());
		}

		/// <summary>
		/// Index of the voice with <paramref name="identifier"/>, else of the voice
		/// named <paramref name="name"/> in <paramref name="language"/>, or -1. An
		/// empty identifier skips the first pass (Prism has none); an empty language
		/// takes the first voice of that name. Voices are stored this way because
		/// indices shift whenever the OS voice list changes, and macOS offers one
		/// name in many languages.
		/// </summary>
		public static int FindVoice(IVoiceControl backend, string identifier, string name, string language) {
			int count = backend.VoiceCount;
			if (!string.IsNullOrEmpty(identifier))
				for (int i = 0; i < count; i++)
					if (backend.GetVoiceIdentifier(i) == identifier) return i;
			for (int i = 0; i < count; i++) {
				if (backend.GetVoiceName(i) != name) continue;
				if (string.IsNullOrEmpty(language) || backend.GetVoiceLanguage(i) == language) return i;
			}
			return -1;
		}

		/// <summary>
		/// Primary language subtag of a game language code ("en", "ru_klei") or a voice
		/// language tag ("en-US", "zh_CN"), lowercased. Null or empty gives "".
		/// </summary>
		public static string PrimaryLanguage(string tag) {
			if (string.IsNullOrEmpty(tag)) return "";
			int cut = tag.IndexOfAny(new[] { '-', '_' });
			return (cut > 0 ? tag.Substring(0, cut) : tag).ToLowerInvariant();
		}

		/// <summary>
		/// Backend indices of the voices that speak <paramref name="languageCode"/>,
		/// compared by primary subtag so "en" takes en-US, en-GB, and en-AU alike.
		/// Every voice when the code is empty or no voice matches: a picker with no
		/// rows would be a dead end.
		/// </summary>
		public static List<int> VoicesForLanguage(IVoiceControl backend, string languageCode) {
			int count = backend.VoiceCount;
			string wanted = PrimaryLanguage(languageCode);
			var voices = new List<int>(count);
			if (wanted.Length > 0)
				for (int i = 0; i < count; i++)
					if (PrimaryLanguage(backend.GetVoiceLanguage(i)) == wanted) voices.Add(i);
			if (voices.Count > 0) return voices;
			for (int i = 0; i < count; i++) voices.Add(i);
			return voices;
		}

		/// <summary>Spoken form of voice <paramref name="index"/>: its name, then its language when known.</summary>
		public static string VoiceLabel(IVoiceControl backend, int index) =>
			VoiceLabel(backend.GetVoiceName(index), backend.GetVoiceLanguage(index));

		private static string VoiceLabel(string name, string language) =>
			string.IsNullOrEmpty(language) ? name : name + ", " + language;

		/// <summary>
		/// Spoken form of the voice in use. A Mac backend on AVSpeech's own default
		/// has no index, so the Spoken Content voice is named instead; null when
		/// nothing can be named.
		/// </summary>
		public static string CurrentVoiceLabel(IVoiceControl backend) {
			int current = backend.CurrentVoice;
			if (current >= 0) return VoiceLabel(backend, current);
			if (backend is MacSystemVoiceBackend && MacSpokenContent.TryGetDefaultVoice(out _, out string name, out string language))
				return VoiceLabel(name, language);
			return null;
		}

		/// <summary>Map a 0-100 setting onto the backend's [0.0, 1.0], clamping out-of-range config values.</summary>
		public static float ToUnit(int percent) {
			return Math.Max(0, Math.Min(100, percent)) / 100f;
		}

		/// <summary>
		/// Map a backend [0.0, 1.0] value onto the 0-100 slider, rounding to the nearest
		/// step (0.9f times 100 is 89.999 in float). A failed read (negative) shows as 0.
		/// </summary>
		public static int ToPercent(float unit) {
			return (int)Math.Round(Math.Max(0f, Math.Min(1f, unit)) * 100f);
		}
	}
}
