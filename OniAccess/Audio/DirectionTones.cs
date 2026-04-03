using FMOD;

namespace OniAccess.Audio {
	public static class DirectionTones {
		public const float SegmentSeconds = 0.055f;
		public const float FadeSeconds = 0.005f;

		public const float PanLeft = -0.79f;
		public const float PanRight = 0.79f;
		public const float PanCenter = 0f;

		public const int ToneUp = 0;
		public const int ToneDown = 1;
		public const int ToneHorizontal = 2;
		public const int TonesPerSet = 3;

		static readonly float[] Frequencies = { 709f, 297f, 457f };

		public static readonly float[] DefaultHarmonics = { 1.0f };

		public static Sound[] GenerateSet(float[] harmonics) {
			var tones = new Sound[TonesPerSet];
			for (int i = 0; i < TonesPerSet; i++)
				tones[i] = ToneGenerator.CreateSegmentTone(
					Frequencies[i], SegmentSeconds, FadeSeconds, harmonics);
			return tones;
		}
	}
}
