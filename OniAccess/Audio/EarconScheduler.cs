using System.Collections;
using System.Collections.Generic;
using System.IO;
using FMOD;
using FMODUnity;
using OniAccess.Util;
using UnityEngine;

namespace OniAccess.Audio {
	public class EarconScheduler: MonoBehaviour {
		public static EarconScheduler Instance { get; private set; }

		private const float BatchDelaySeconds = 0.05f;

		private AudioLibrary _library;
		private Coroutine _activeSequence;
		private readonly List<Channel> _activeChannels = new List<Channel>();
		private Channel _oneShotChannel;
		private readonly List<EarconSet> _sets = new List<EarconSet>();
		private TemperatureBandEarconSet _temperatureBandSet;

		internal int ActiveChannelCount =>
			_activeChannels.Count + (_oneShotChannel.hasHandle() ? 1 : 0);

		private void Awake() {
			Instance = this;
			try {
				_library = new AudioLibrary();
				string audioDir = Path.Combine(Mod.ModDir, "audio");
				_library.Load(audioDir);

				_temperatureBandSet = new TemperatureBandEarconSet();
				RegisterSet(_temperatureBandSet);
				RegisterSet(new PassabilityEarconSet());
				RegisterSet(new UtilityPresenceEarconSet());
			} catch (System.Exception ex) {
				Log.Error($"EarconScheduler.Awake failed: {ex}");
			}
		}

		private void RegisterSet(EarconSet set) {
			_sets.Add(set);
			_sets.Sort((a, b) => a.Priority.CompareTo(b.Priority));
		}

		public void PlayForCell(int cell, HashedString overlayMode) {
			var allBatches = new List<SoundBatch>();
			foreach (var set in _sets) {
				if (!set.IsActive(overlayMode) || !set.IsEnabled)
					continue;
				var batches = set.GetBatches(cell);
				float volume = set.Volume;
				foreach (var batch in batches)
					allBatches.Add(new SoundBatch(volume, batch.Specs));
			}
			Play(allBatches);
		}

		private void OnDestroy() {
			if (Instance == this) {
				CancelAll();
				StopOneShot();
				_library?.ReleaseAll();
				Instance = null;
			}
		}

		/// <summary>
		/// Plays one clip outside the cursor-move sequence. Earcons fired by a
		/// game event arrive on their own schedule, so this neither cancels the
		/// batches queued for a cursor move nor is cancelled by the next one.
		/// </summary>
		public void PlayOneShot(string clipName, float volume) {
			if (!_library.TryGet(clipName, out var sound)) {
				Log.Warn($"EarconScheduler: clip not found: {clipName}");
				return;
			}
			StopOneShot();
			var result = RuntimeManager.CoreSystem.playSound(
				sound, default(ChannelGroup), false, out _oneShotChannel);
			if (result != RESULT.OK) {
				Log.Warn($"EarconScheduler: FMOD playSound failed for one-shot '{clipName}': {result}");
				_oneShotChannel = default;
				return;
			}
			_oneShotChannel.setVolume(volume);
		}

		private void StopOneShot() {
			if (_oneShotChannel.hasHandle())
				_oneShotChannel.stop();
			_oneShotChannel = default;
		}

		public void Play(List<SoundBatch> batches) {
			CancelAll();
			if (batches.Count == 0) return;
			_activeSequence = StartCoroutine(RunSequence(batches));
		}

		public void ResetTransitionState() {
			_temperatureBandSet?.Reset();
		}

		public void CancelAll() {
			if (_activeSequence != null) {
				StopCoroutine(_activeSequence);
				_activeSequence = null;
			}
			foreach (var ch in _activeChannels)
				ch.stop();
			_activeChannels.Clear();
		}

		private IEnumerator RunSequence(List<SoundBatch> batches) {
			for (int i = 0; i < batches.Count; i++) {
				PlayBatch(batches[i]);
				if (i < batches.Count - 1)
					yield return new WaitForSecondsRealtime(BatchDelaySeconds);
			}
			_activeChannels.Clear();
			_activeSequence = null;
		}

		private void PlayBatch(SoundBatch batch) {
			foreach (var spec in batch.Specs) {
				if (!_library.TryGet(spec.ClipName, out var sound)) {
					Log.Warn($"EarconScheduler: clip not found: {spec.ClipName}");
					continue;
				}
				var result = RuntimeManager.CoreSystem.playSound(
					sound, default(ChannelGroup), false, out var channel);
				if (result != RESULT.OK) {
					Log.Warn($"EarconScheduler: FMOD playSound failed for '{spec.ClipName}': {result}");
					continue;
				}
				channel.setVolume(batch.Volume);
				channel.setPitch(spec.Pitch);
				channel.setPan(spec.Pan);
				_activeChannels.Add(channel);
			}
		}
	}
}
