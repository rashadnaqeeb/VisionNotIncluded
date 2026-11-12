using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using OniAccess.Speech;

namespace OniAccess.Tests {
	/// <summary>
	/// Offline tests for the two pure boundaries between the voice settings and Prism.
	/// Prism rejects a rate or volume outside [0.0, 1.0] with RANGE_OUT_OF_BOUNDS, so a
	/// config value that maps outside it silently leaves the old setting in place; and
	/// voice names come back as UTF-8, so an ANSI decode would garble any non-ASCII
	/// name in the picker and never match the saved name on the next launch. The
	/// picker filters voices by the game language, so a wrong primary-subtag split
	/// would silently hide every voice or show all 180 of them. The sliders show
	/// the backend's live value when nothing is saved, so a truncating float to
	/// percent conversion would announce 89 for a rate of 0.9; and the macOS Spoken
	/// Content rate is stored with a period, so a locale-sensitive parse would
	/// throw it away on any comma-decimal system. The Mac stream trims rendered
	/// speech before scheduling it: trimming too little puts the gaps back, trimming
	/// too much clips the first consonant, and reading past the render's count
	/// would play whatever the oversized buffer holds.
	/// </summary>
	static class SpeechOutputTests {
		public static IEnumerable<(string, bool, string)> All() {
			yield return UnitEndpoints();
			yield return UnitClampsOutOfRangeConfig();
			yield return PercentRoundsFloat();
			yield return PercentOfFailedReadIsZero();
			yield return RateParsesWithPeriodUnderCommaLocale();
			yield return RateRejectsOutOfRange();
			yield return Utf8NameDecodes();
			yield return NullNameIsNull();
			yield return PrimaryLanguageSplitsRegionAndSuffix();
			yield return PrimaryLanguageOfEmptyIsEmpty();
			yield return TrimOfSilenceIsEmpty();
			yield return TrimKeepsEdgesAndStopsAtCount();
			yield return TrimClampsEdgesAtArrayEnds();
			yield return TrimThresholdIsInclusive();
			yield return NormalizeReachesTargetUnderCeiling();
			yield return LimiterHoldsCeilingThroughSpike();
			yield return LimiterGainIsDownBeforeThePeak();
			yield return NormalizeLeavesSilenceAlone();
			yield return FadeEdgesStartsAndEndsAtZero();
			yield return FadeEdgesFitsAShortLine();
		}

		private static (string, bool, string) Assert(string name, bool ok, string detail)
			=> (name, ok, ok ? "OK" : detail);

		private static (string, bool, string) UnitEndpoints() {
			float lo = SpeechOutputSelector.ToUnit(0);
			float mid = SpeechOutputSelector.ToUnit(50);
			float hi = SpeechOutputSelector.ToUnit(100);
			bool ok = lo == 0f && mid == 0.5f && hi == 1f;
			return Assert("UnitEndpoints", ok, $"{lo} {mid} {hi}");
		}

		private static (string, bool, string) UnitClampsOutOfRangeConfig() {
			// A hand-edited config of 150 or -5 must still land inside Prism's range.
			float over = SpeechOutputSelector.ToUnit(150);
			float under = SpeechOutputSelector.ToUnit(-5);
			bool ok = over == 1f && under == 0f;
			return Assert("UnitClampsOutOfRangeConfig", ok, $"{over} {under}");
		}

		private static (string, bool, string) PercentRoundsFloat() {
			// 0.9f * 100 is 89.99999 in single precision; the slider must say 90.
			int a = SpeechOutputSelector.ToPercent(0.9f);
			int b = SpeechOutputSelector.ToPercent(0.29f);
			int c = SpeechOutputSelector.ToPercent(1f);
			bool ok = a == 90 && b == 29 && c == 100;
			return Assert("PercentRoundsFloat", ok, $"{a} {b} {c}");
		}

		private static (string, bool, string) PercentOfFailedReadIsZero() {
			// PrismBackend.GetRate returns -1 on failure; the slider must stay in range.
			int p = SpeechOutputSelector.ToPercent(-1f);
			return Assert("PercentOfFailedReadIsZero", p == 0, $"{p}");
		}

		private static (string, bool, string) RateParsesWithPeriodUnderCommaLocale() {
			var previous = Thread.CurrentThread.CurrentCulture;
			Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
			try {
				float rate = MacSpokenContent.ParseRate("0.9");
				return Assert("RateParsesWithPeriodUnderCommaLocale", rate == 0.9f, $"{rate}");
			} finally {
				Thread.CurrentThread.CurrentCulture = previous;
			}
		}

		private static (string, bool, string) RateRejectsOutOfRange() {
			// Prism refuses anything outside [0, 1]; a bad preference must not reach it.
			float a = MacSpokenContent.ParseRate("1.5");
			float b = MacSpokenContent.ParseRate("abc");
			float c = MacSpokenContent.ParseRate(null);
			bool ok = a == -1f && b == -1f && c == -1f;
			return Assert("RateRejectsOutOfRange", ok, $"{a} {b} {c}");
		}

		private static string RoundTrip(string text) {
			byte[] bytes = Encoding.UTF8.GetBytes(text + "\0");
			IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
			try {
				Marshal.Copy(bytes, 0, ptr, bytes.Length);
				return PrismBackend.PtrToUtf8(ptr);
			} finally {
				Marshal.FreeHGlobal(ptr);
			}
		}

		private static (string, bool, string) Utf8NameDecodes() {
			// Multi-byte names (a Chinese voice, an accented one) survive intact.
			string a = RoundTrip("婷婷");
			string b = RoundTrip("Amélie");
			bool ok = a == "婷婷" && b == "Amélie";
			return Assert("Utf8NameDecodes", ok, $"\"{a}\" \"{b}\"");
		}

		private static (string, bool, string) NullNameIsNull() {
			string s = PrismBackend.PtrToUtf8(IntPtr.Zero);
			return Assert("NullNameIsNull", s == null, $"\"{s}\"");
		}

		private static (string, bool, string) PrimaryLanguageSplitsRegionAndSuffix() {
			// Voice tags use a hyphen (en-GB), the game's Klei codes an underscore
			// (ru_klei), and a bare code (zh) has nothing to strip.
			var cases = new[] {
				("en-GB", "en"), ("ru_klei", "ru"), ("zh", "zh"), ("EN-us", "en"), ("yue-HK", "yue"),
			};
			foreach (var (tag, expected) in cases) {
				string got = SpeechOutputSelector.PrimaryLanguage(tag);
				if (got != expected)
					return Assert("PrimaryLanguageSplitsRegionAndSuffix", false, $"{tag} -> \"{got}\", expected {expected}");
			}
			return Assert("PrimaryLanguageSplitsRegionAndSuffix", true, "");
		}

		private static (string, bool, string) TrimOfSilenceIsEmpty() {
			// A line of pure silence schedules nothing, so it costs no time.
			var samples = new float[200];
			for (int i = 0; i < samples.Length; i++) samples[i] = 0.001f;
			int n = SpeechSamples.Trim(samples, samples.Length, 1000).Length;
			return Assert("TrimOfSilenceIsEmpty", n == 0, $"{n}");
		}

		private static (string, bool, string) TrimKeepsEdgesAndStopsAtCount() {
			// At 1000 Hz the kept edge is 5 samples. Speech sits at 20..30 of a
			// 100-sample render inside a 4096-sample buffer whose tail is garbage.
			var samples = new float[4096];
			for (int i = 100; i < samples.Length; i++) samples[i] = 0.9f;
			for (int i = 20; i <= 30; i++) samples[i] = 0.5f;
			float[] trimmed = SpeechSamples.Trim(samples, 100, 1000);
			bool ok = trimmed.Length == 21 && trimmed[0] == 0f && trimmed[5] == 0.5f && trimmed[15] == 0.5f && trimmed[20] == 0f;
			return Assert("TrimKeepsEdgesAndStopsAtCount", ok, $"length {trimmed.Length}");
		}

		private static (string, bool, string) TrimClampsEdgesAtArrayEnds() {
			// Speech at the very start and end: the 5-sample edge cannot go outside the render.
			var samples = new float[50];
			samples[2] = 0.5f;
			samples[48] = 0.5f;
			float[] trimmed = SpeechSamples.Trim(samples, 50, 1000);
			bool ok = trimmed.Length == 50 && trimmed[2] == 0.5f && trimmed[48] == 0.5f;
			return Assert("TrimClampsEdgesAtArrayEnds", ok, $"length {trimmed.Length}");
		}

		private static (string, bool, string) TrimThresholdIsInclusive() {
			// Exactly the threshold is silence; just above it is speech, kept with
			// its 5-sample edges at 1000 Hz.
			var atThreshold = new float[] { SpeechSamples.SilenceThreshold, -SpeechSamples.SilenceThreshold };
			var above = new float[300];
			above[150] = 0.0041f;
			int a = SpeechSamples.Trim(atThreshold, 2, 1000).Length;
			int b = SpeechSamples.Trim(above, 300, 1000).Length;
			return Assert("TrimThresholdIsInclusive", a == 0 && b == 11, $"{a} {b}");
		}

		private static float Peak(float[] s) {
			float p = 0f;
			foreach (float v in s) p = Math.Max(p, Math.Abs(v));
			return p;
		}

		private static float Rms(float[] s) {
			double sum = 0;
			foreach (float v in s) sum += v * v;
			return (float)Math.Sqrt(sum / s.Length);
		}

		private static (string, bool, string) NormalizeReachesTargetUnderCeiling() {
			// A quiet sine (-23 dBFS RMS, like Zarvox) comes up to the target
			// loudness with its peaks held under the ceiling; the limiter only has
			// to shave the tops, so the RMS lands close to the target.
			var s = new float[22050];
			for (int i = 0; i < s.Length; i++) s[i] = 0.1f * (float)Math.Sin(2 * Math.PI * 440 * i / 22050.0);
			SpeechSamples.Normalize(s, 22050);
			float rms = Rms(s), peak = Peak(s);
			bool ok = peak <= SpeechSamples.Ceiling + 1e-4f && rms > SpeechSamples.TargetRms * 0.8f && rms <= SpeechSamples.TargetRms * 1.01f;
			return Assert("NormalizeReachesTargetUnderCeiling", ok, $"rms {rms:F3} peak {peak:F3}");
		}

		private static (string, bool, string) LimiterHoldsCeilingThroughSpike() {
			// A lone full-scale spike in quiet speech at 4x gain: the spike is held
			// under the ceiling and the quiet part still gets its gain.
			var s = new float[4000];
			for (int i = 0; i < s.Length; i++) s[i] = 0.05f;
			s[2000] = 1f;
			SpeechSamples.Limit(s, 4f, 22050);
			bool ok = Peak(s) <= SpeechSamples.Ceiling + 1e-4f && Math.Abs(s[100] - 0.2f) < 1e-4f && Math.Abs(s[3900] - 0.2f) < 1e-4f;
			return Assert("LimiterHoldsCeilingThroughSpike", ok, $"peak {Peak(s):F3} early {s[100]:F3} late {s[3900]:F3}");
		}

		private static (string, bool, string) LimiterGainIsDownBeforeThePeak() {
			// The gain ramps down over the look-ahead window ahead of the peak (44
			// samples at 22050 Hz), not at the peak; a limiter that only reacted at
			// the peak would let it through or click. At 2x gain a 1.0 spike needs
			// the envelope at 0.475, a drop of 23 steps starting 43 samples ahead.
			var s = new float[4000];
			for (int i = 0; i < s.Length; i++) s[i] = 0.25f;
			s[2000] = 1f;
			SpeechSamples.Limit(s, 2f, 22050);
			bool ramped = s[1900] == 0.5f && s[1970] < 0.5f && s[1970] > s[1990] && Math.Abs(s[1990] - 0.2375f) < 1e-3f;
			return Assert("LimiterGainIsDownBeforeThePeak", ramped && s[2000] <= SpeechSamples.Ceiling + 1e-4f, $"{s[1900]:F3} {s[1970]:F3} {s[1990]:F3} {s[2000]:F3}");
		}

		private static (string, bool, string) NormalizeLeavesSilenceAlone() {
			// Near-silence must not be lifted into audible noise: gain is capped.
			var s = new float[1000];
			for (int i = 0; i < s.Length; i++) s[i] = 0.001f;
			SpeechSamples.Normalize(s, 22050);
			bool ok = Math.Abs(s[500] - 0.001f * SpeechSamples.MaxGain) < 1e-5f;
			var z = new float[100];
			SpeechSamples.Normalize(z, 22050);
			return Assert("NormalizeLeavesSilenceAlone", ok && z[50] == 0f, $"{s[500]:F5} {z[50]}");
		}

		private static (string, bool, string) FadeEdgesStartsAndEndsAtZero() {
			// A line cut off mid-waveform clicks; after fading, both ends sit at
			// silence, the ramps rise steadily, and the middle is untouched.
			var s = new float[22050];
			for (int i = 0; i < s.Length; i++) s[i] = 0.5f;
			SpeechSamples.FadeEdges(s, 22050);
			int fadeIn = (int)Math.Round(SpeechSamples.FadeInSeconds * 22050);
			int fadeOut = (int)Math.Round(SpeechSamples.FadeOutSeconds * 22050);
			bool rising = true;
			for (int i = 1; i < fadeIn; i++) rising &= s[i] >= s[i - 1];
			bool falling = true;
			for (int i = s.Length - fadeOut; i < s.Length; i++) falling &= s[i] <= s[i - 1];
			int last = s.Length - 1;
			bool ok = s[0] == 0f && Math.Abs(s[last]) < 0.01f && rising && falling
				&& s[fadeIn] == 0.5f && s[s.Length - fadeOut - 1] == 0.5f && s[11000] == 0.5f;
			return Assert("FadeEdgesStartsAndEndsAtZero", ok,
				$"first {s[0]} last {s[last]:F4} rising {rising} falling {falling} after-in {s[fadeIn]} before-out {s[s.Length - fadeOut - 1]}");
		}

		private static (string, bool, string) FadeEdgesFitsAShortLine() {
			// A line shorter than both ramps still starts and ends at silence
			// without the ramps running past each other.
			var s = new float[50];
			for (int i = 0; i < s.Length; i++) s[i] = 0.5f;
			SpeechSamples.FadeEdges(s, 22050);
			bool inRange = true;
			foreach (float x in s) inRange &= x >= 0f && x <= 0.5f;
			var one = new float[] { 0.5f };
			SpeechSamples.FadeEdges(one, 22050);
			bool ok = s[0] == 0f && Math.Abs(s[49]) < 0.05f && inRange && Peak(s) > 0.4f && one[0] == 0f;
			return Assert("FadeEdgesFitsAShortLine", ok, $"first {s[0]} last {s[49]:F4} peak {Peak(s):F3} single {one[0]}");
		}

		private static (string, bool, string) PrimaryLanguageOfEmptyIsEmpty() {
			// Empty never equals a real subtag, so an unknown game code or a voice
			// without a language falls through to the unfiltered list.
			string a = SpeechOutputSelector.PrimaryLanguage(null);
			string b = SpeechOutputSelector.PrimaryLanguage("");
			return Assert("PrimaryLanguageOfEmptyIsEmpty", a == "" && b == "", $"\"{a}\" \"{b}\"");
		}
	}
}
