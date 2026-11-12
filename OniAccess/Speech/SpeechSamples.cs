using System;

namespace OniAccess.Speech {
	/// <summary>
	/// Sample-level work on rendered speech, kept free of native calls so it can
	/// be tested on any platform. Used by <see cref="MacSpeechStream"/>.
	/// </summary>
	public static class SpeechSamples {
		/// <summary>Silence kept on each side of the trimmed speech, so a consonant is never clipped.</summary>
		public const double KeepEdgeSeconds = 0.005;
		/// <summary>Amplitude at or below which a sample counts as silence.</summary>
		public const float SilenceThreshold = 0.004f;

		/// <summary>
		/// The loudness every line is brought to, as RMS amplitude: -10 dBFS. The
		/// voices render at -17 to -24 dBFS RMS (measured August 2026: Samantha
		/// and Reed -17, Daniel -21, Zarvox -24), which sits far below the game's
		/// own mix, and differs from voice to voice.
		/// </summary>
		public const float TargetRms = 0.316f;
		/// <summary>Peak amplitude the limiter holds the output under.</summary>
		public const float Ceiling = 0.95f;
		/// <summary>Most gain normalization applies, so a near-silent line is not lifted into noise.</summary>
		public const float MaxGain = 8f;
		/// <summary>How far ahead the limiter looks, which is also how long its gain takes to fall before a peak.</summary>
		public const double LookaheadSeconds = 0.002;
		/// <summary>How long the limiter's gain takes to recover after a peak.</summary>
		public const double ReleaseSeconds = 0.05;
		/// <summary>Length of the ramp that brings a line in from silence.</summary>
		public const double FadeInSeconds = 0.002;
		/// <summary>Length of the ramp that takes a line out to silence.</summary>
		public const double FadeOutSeconds = 0.004;

		private static float[] _required = new float[32768];
		private static int[] _queue = new int[32768];

		/// <summary>
		/// The speech between the first and last samples above the silence
		/// threshold, plus <see cref="KeepEdgeSeconds"/> either side where the
		/// input has it. Empty when nothing rises above the threshold.
		/// </summary>
		public static float[] Trim(float[] samples, int count, double sampleRate) {
			int start = 0;
			while (start < count && Math.Abs(samples[start]) <= SilenceThreshold) start++;
			if (start == count) return new float[0];
			int end = count - 1;
			while (end > start && Math.Abs(samples[end]) <= SilenceThreshold) end--;
			int keep = (int)Math.Round(KeepEdgeSeconds * sampleRate);
			start = Math.Max(0, start - keep);
			end = Math.Min(count - 1, end + keep);
			var result = new float[end - start + 1];
			Array.Copy(samples, start, result, 0, result.Length);
			return result;
		}

		/// <summary>
		/// Ramp the first <see cref="FadeInSeconds"/> of <paramref name="samples"/>
		/// up from zero and the last <see cref="FadeOutSeconds"/> down to zero, in
		/// place, on a raised-cosine curve. AVSpeech can end a line mid-waveform
		/// (a plosive such as the end of "hundred"), and a line that starts or
		/// stops on a non-zero sample clicks against the silence around it. The
		/// ramps are too short to hear as a fade. On a line shorter than both
		/// ramps together, each ramp gets its share of the line.
		/// </summary>
		public static void FadeEdges(float[] samples, double sampleRate) {
			int n = samples.Length;
			if (n == 0) return;
			int fadeIn = Math.Max(1, (int)Math.Round(FadeInSeconds * sampleRate));
			int fadeOut = Math.Max(1, (int)Math.Round(FadeOutSeconds * sampleRate));
			if (fadeIn + fadeOut > n) {
				fadeIn = Math.Max(1, n * fadeIn / (fadeIn + fadeOut));
				fadeOut = Math.Max(1, n - fadeIn);
			}
			for (int i = 0; i < fadeIn; i++)
				samples[i] *= RaisedCosine((float)i / fadeIn);
			for (int i = 0; i < fadeOut; i++)
				samples[n - 1 - i] *= RaisedCosine((float)i / fadeOut);
		}

		/// <summary>0 at <paramref name="x"/> = 0 rising smoothly to 1 at <paramref name="x"/> = 1.</summary>
		private static float RaisedCosine(float x) => 0.5f - 0.5f * (float)Math.Cos(Math.PI * x);

		/// <summary>
		/// Bring <paramref name="samples"/> to <see cref="TargetRms"/> in place,
		/// through <see cref="Limit"/> so the peaks stay under the ceiling. Silence
		/// is left alone.
		/// </summary>
		public static void Normalize(float[] samples, double sampleRate) {
			if (samples.Length == 0) return;
			double sumSquares = 0;
			for (int i = 0; i < samples.Length; i++) sumSquares += samples[i] * samples[i];
			float rms = (float)Math.Sqrt(sumSquares / samples.Length);
			if (rms <= 0f) return;
			Limit(samples, Math.Min(MaxGain, TargetRms / rms), sampleRate);
		}

		/// <summary>
		/// Multiply <paramref name="samples"/> by <paramref name="gain"/> in place,
		/// with a look-ahead peak limiter holding the result under
		/// <see cref="Ceiling"/>. The limiter's gain ramps down over the look-ahead
		/// window ahead of a peak, so it is always in place by the time the peak
		/// arrives, and recovers linearly over the release time. Only the peaks
		/// are touched; the waveform between them is scaled, not clipped.
		/// </summary>
		public static void Limit(float[] samples, float gain, double sampleRate) {
			int n = samples.Length;
			if (n == 0) return;
			int look = Math.Max(1, (int)Math.Round(LookaheadSeconds * sampleRate));
			float attackStep = 1f / look;
			float releaseStep = (float)(1.0 / Math.Max(1.0, ReleaseSeconds * sampleRate));

			// Scratch kept between lines; speech is rendered on one thread.
			if (_required.Length < n) {
				_required = new float[Math.Max(n, _required.Length * 2)];
				_queue = new int[_required.Length];
			}
			float[] required = _required;
			int[] queue = _queue;

			// The gain each sample needs on its own to stay under the ceiling.
			for (int i = 0; i < n; i++) {
				float peak = Math.Abs(samples[i]) * gain;
				required[i] = peak > Ceiling ? Ceiling / peak : 1f;
			}

			// Sliding minimum of required over [i, i + look): a monotonic queue of indices.
			int head = 0, tail = 0, next = 0;
			float envelope = 1f;
			for (int i = 0; i < n; i++) {
				int windowEnd = Math.Min(n, i + look);
				while (next < windowEnd) {
					while (tail > head && required[queue[tail - 1]] >= required[next]) tail--;
					queue[tail++] = next++;
				}
				while (queue[head] < i) head++;
				float target = required[queue[head]];
				if (target < envelope)
					envelope = Math.Max(target, envelope - attackStep);
				else
					envelope = Math.Min(target, envelope + releaseStep);
				samples[i] = samples[i] * gain * envelope;
			}
		}
	}
}
