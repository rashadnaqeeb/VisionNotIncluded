using FMOD;
using FMODUnity;
using OniAccess.Util;
using UnityEngine;

namespace OniAccess.Audio {
	public class FollowMovementEarcon: MonoBehaviour {
		public static FollowMovementEarcon Instance { get; private set; }

		static float Volume => ConfigManager.Config.FollowMovementVolume;

		enum MoveDirection {
			None, Up, Down, Left, Right,
			UpLeft, UpRight, DownLeft, DownRight
		}

		private Sound[] _tones;
		private Channel _channel;
		private int _lastCell = Grid.InvalidCell;
		private int _lastWorldId = -1;
		private MoveDirection _lastDirection = MoveDirection.None;

		internal int ActiveChannelCount => _channel.hasHandle() ? 1 : 0;

		private void Awake() {
			Instance = this;
			_tones = DirectionTones.GenerateSet(DirectionTones.DefaultHarmonics);
		}

		private void OnDestroy() {
			StopChannel();
			if (_tones != null) {
				foreach (var sound in _tones)
					if (sound.hasHandle())
						sound.release();
			}
			if (Instance == this)
				Instance = null;
		}

		private void Update() {
			if (!ConfigManager.Config.FollowMovementEarcons) {
				Reset();
				return;
			}

			if (Game.Instance == null
				|| CameraController.Instance == null
				|| CameraController.Instance.followTarget == null) {
				Reset();
				return;
			}

			int worldId = ClusterManager.Instance.activeWorldId;
			if (worldId != _lastWorldId) {
				_lastWorldId = worldId;
				Reset();
				return;
			}

			int cell = Handlers.Tiles.TileCursor.Instance?.Cell ?? Grid.InvalidCell;
			if (cell == Grid.InvalidCell) {
				Reset();
				return;
			}

			if (_lastCell == Grid.InvalidCell) {
				_lastCell = cell;
				return;
			}

			if (cell == _lastCell)
				return;

			int dx = Grid.CellColumn(cell) - Grid.CellColumn(_lastCell);
			int dy = Grid.CellRow(cell) - Grid.CellRow(_lastCell);
			_lastCell = cell;

			var direction = Classify(dx, dy);
			if (direction == MoveDirection.None)
				return;

			_lastDirection = direction;
			PlayDirection(direction);
		}

		private void Reset() {
			if (_lastDirection != MoveDirection.None || _lastCell != Grid.InvalidCell) {
				StopChannel();
				_lastCell = Grid.InvalidCell;
				_lastDirection = MoveDirection.None;
			}
		}

		private static MoveDirection Classify(int dx, int dy) {
			int sx = dx > 0 ? 1 : dx < 0 ? -1 : 0;
			int sy = dy > 0 ? 1 : dy < 0 ? -1 : 0;
			if (sx == 0 && sy > 0) return MoveDirection.Up;
			if (sx == 0 && sy < 0) return MoveDirection.Down;
			if (sx < 0 && sy == 0) return MoveDirection.Left;
			if (sx > 0 && sy == 0) return MoveDirection.Right;
			if (sx < 0 && sy > 0) return MoveDirection.UpLeft;
			if (sx > 0 && sy > 0) return MoveDirection.UpRight;
			if (sx < 0 && sy < 0) return MoveDirection.DownLeft;
			if (sx > 0 && sy < 0) return MoveDirection.DownRight;
			return MoveDirection.None;
		}

		private void PlayDirection(MoveDirection direction) {
			int toneIndex;
			float pan;
			switch (direction) {
				case MoveDirection.Up: toneIndex = DirectionTones.ToneUp; pan = DirectionTones.PanCenter; break;
				case MoveDirection.Down: toneIndex = DirectionTones.ToneDown; pan = DirectionTones.PanCenter; break;
				case MoveDirection.Left: toneIndex = DirectionTones.ToneHorizontal; pan = DirectionTones.PanLeft; break;
				case MoveDirection.Right: toneIndex = DirectionTones.ToneHorizontal; pan = DirectionTones.PanRight; break;
				case MoveDirection.UpLeft: toneIndex = DirectionTones.ToneUp; pan = DirectionTones.PanLeft; break;
				case MoveDirection.UpRight: toneIndex = DirectionTones.ToneUp; pan = DirectionTones.PanRight; break;
				case MoveDirection.DownLeft: toneIndex = DirectionTones.ToneDown; pan = DirectionTones.PanLeft; break;
				case MoveDirection.DownRight: toneIndex = DirectionTones.ToneDown; pan = DirectionTones.PanRight; break;
				default: return;
			}
			StopChannel();
			var result = RuntimeManager.CoreSystem.playSound(
				_tones[toneIndex], default(ChannelGroup), false, out _channel);
			if (result != RESULT.OK) {
				Log.Warn($"FollowMovementEarcon: playSound failed: {result}");
				return;
			}
			_channel.setVolume(Volume);
			_channel.setPan(pan);
		}

		private void StopChannel() {
			if (_channel.hasHandle())
				_channel.stop();
			_channel = default;
		}
	}
}
