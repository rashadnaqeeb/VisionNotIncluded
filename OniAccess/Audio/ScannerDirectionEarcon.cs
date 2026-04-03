using System.Collections;
using FMOD;
using FMODUnity;
using OniAccess.Util;
using UnityEngine;

namespace OniAccess.Audio {
	public class ScannerDirectionEarcon: MonoBehaviour {
		public static ScannerDirectionEarcon Instance { get; private set; }

		const float GapSeconds = 0.01f;
		const float MinVolumeRatio = 0.1f;
		const float MaxDistanceTiles = 100f;

		static float BaseVolume => ConfigManager.Config.ScannerDirectionVolume;

		private Sound[] _tones;
		private Coroutine _activeSequence;
		private Channel _channel;

		internal int ActiveChannelCount => _channel.hasHandle() ? 1 : 0;

		private void Awake() {
			Instance = this;
			_tones = DirectionTones.GenerateSet(DirectionTones.DefaultHarmonics);
		}

		private void OnDestroy() {
			CancelAll();
			if (_tones != null) {
				foreach (var sound in _tones)
					if (sound.hasHandle())
						sound.release();
			}
			if (Instance == this)
				Instance = null;
		}

		public void Play(int cursorCell, int targetCell) {
			CancelAll();
			if (!ConfigManager.Config.ScannerDirectionEarcons)
				return;

			int dy = Grid.CellRow(targetCell) - Grid.CellRow(cursorCell);
			int dx = Grid.CellColumn(targetCell) - Grid.CellColumn(cursorCell);

			if (dy == 0 && dx == 0) {
				PlayTone(DirectionTones.ToneHorizontal, DirectionTones.PanCenter, BaseVolume);
				return;
			}

			var segments = BuildSequence(dy, dx);
			if (segments.Length == 1) {
				PlayTone(segments[0].toneIndex, segments[0].pan, segments[0].volume);
				return;
			}

			_activeSequence = StartCoroutine(RunSequence(segments));
		}

		private (int toneIndex, float pan, float volume)[] BuildSequence(int dy, int dx) {
			if (dy != 0 && dx != 0)
				return new[] {
					(dy > 0 ? DirectionTones.ToneUp : DirectionTones.ToneDown, DirectionTones.PanCenter, VolumeForDistance(Mathf.Abs(dy))),
					(DirectionTones.ToneHorizontal, dx > 0 ? DirectionTones.PanRight : DirectionTones.PanLeft, VolumeForDistance(Mathf.Abs(dx)))
				};
			if (dy != 0)
				return new[] { (dy > 0 ? DirectionTones.ToneUp : DirectionTones.ToneDown, DirectionTones.PanCenter, VolumeForDistance(Mathf.Abs(dy))) };
			return new[] { (DirectionTones.ToneHorizontal, dx > 0 ? DirectionTones.PanRight : DirectionTones.PanLeft, VolumeForDistance(Mathf.Abs(dx))) };
		}

		private float VolumeForDistance(int tiles) {
			float t = Mathf.Clamp01(tiles / MaxDistanceTiles);
			return Mathf.Lerp(BaseVolume, BaseVolume * MinVolumeRatio, t);
		}

		public void CancelAll() {
			if (_activeSequence != null) {
				StopCoroutine(_activeSequence);
				_activeSequence = null;
			}
			StopChannel();
		}

		private IEnumerator RunSequence((int toneIndex, float pan, float volume)[] segments) {
			for (int i = 0; i < segments.Length; i++) {
				PlayTone(segments[i].toneIndex, segments[i].pan, segments[i].volume);
				yield return new WaitForSecondsRealtime(DirectionTones.SegmentSeconds);
				StopChannel();
				if (i < segments.Length - 1)
					yield return new WaitForSecondsRealtime(GapSeconds);
			}
			_activeSequence = null;
		}

		private void PlayTone(int toneIndex, float pan, float volume) {
			if (toneIndex < 0 || toneIndex >= _tones.Length
				|| !_tones[toneIndex].hasHandle()) {
				Log.Warn($"ScannerDirectionEarcon: invalid tone index {toneIndex}");
				return;
			}
			var result = RuntimeManager.CoreSystem.playSound(
				_tones[toneIndex], default(ChannelGroup), false, out _channel);
			if (result != RESULT.OK) {
				Log.Warn($"ScannerDirectionEarcon: playSound failed: {result}");
				return;
			}
			_channel.setVolume(volume);
			_channel.setPan(pan);
		}

		private void StopChannel() {
			if (_channel.hasHandle())
				_channel.stop();
			_channel = default;
		}
	}
}
