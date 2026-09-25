using System.Collections.Generic;

namespace OniAccess.ConduitTracking {
	/// <summary>
	/// Tracks per-conduit flow direction in a circular buffer.
	/// One instance per conduit type (gas, liquid, solid).
	/// Records are sampled once per game-second from Sim200ms postfixes.
	/// </summary>
	public class FlowTracker {
		public const int BufferSize = 20;

		/// <summary>
		/// Normalized direction values shared across ConduitFlow.FlowDirections
		/// (flags byte) and SolidConduitFlow.FlowDirection (plain enum).
		/// </summary>
		public const int DirNone = 0;
		public const int DirUp = 1;
		public const int DirDown = 2;
		public const int DirLeft = 3;
		public const int DirRight = 4;

		private int[] _buffer;
		private Tag[] _contentBuffer;
		private int _writePos;
		private int _conduitCount;
		private int _samplesRecorded;

		public static FlowTracker Gas { get; private set; }
		public static FlowTracker Liquid { get; private set; }
		public static FlowTracker Solid { get; private set; }

		/// <summary>
		/// Creates the trackers for a game and clears each when its conduits
		/// rebuild. Runs from Game.OnPrefabInit, and from ModuleMain.Reattach
		/// when a reload lands mid-game.
		/// </summary>
		public static void Attach(Game game) {
			Gas = new FlowTracker();
			Liquid = new FlowTracker();
			Solid = new FlowTracker();
			game.gasConduitFlow.onConduitsRebuilt += Gas.Clear;
			game.liquidConduitFlow.onConduitsRebuilt += Liquid.Clear;
			game.solidConduitFlow.onConduitsRebuilt += Solid.Clear;
		}

		/// <summary>
		/// Drops the rebuild subscriptions Attach made on the live game, if
		/// there is one; a game that has ended took them with it.
		/// </summary>
		public static void Detach() {
			var game = Game.Instance;
			if (game == null || Gas == null) return;
			game.gasConduitFlow.onConduitsRebuilt -= Gas.Clear;
			game.liquidConduitFlow.onConduitsRebuilt -= Liquid.Clear;
			game.solidConduitFlow.onConduitsRebuilt -= Solid.Clear;
		}

		public void Clear() {
			_buffer = null;
			_contentBuffer = null;
			_writePos = 0;
			_conduitCount = 0;
			_samplesRecorded = 0;
		}

		public void RecordFluid(ConduitFlow flow) {
			int count = flow.soaInfo.NumEntries;
			if (count == 0) return;
			EnsureCapacity(count);
			int baseIdx = _writePos * _conduitCount;
			for (int i = 0; i < count; i++) {
				var info = flow.soaInfo.GetLastFlowInfo(i);
				int dir = NormalizeFluidDirection(info.direction);
				SimHashes element = info.contents.element;
				if (dir == DirNone) {
					int cell = flow.soaInfo.GetCell(i);
					if (BridgeFlowCapture.TryGet(cell,
							out SimHashes bridgeElement,
							out int bridgeDir)) {
						dir = bridgeDir;
						element = bridgeElement;
					}
				}
				_buffer[baseIdx + i] = dir;
				_contentBuffer[baseIdx + i] = dir == DirNone
					? Tag.Invalid : ElementTag(element);
			}
			AdvanceWritePos();
		}

		public void RecordSolid(SolidConduitFlow flow) {
			var soa = flow.GetSOAInfo();
			int count = soa.NumEntries;
			if (count == 0) return;
			EnsureCapacity(count);
			int baseIdx = _writePos * _conduitCount;
			for (int i = 0; i < count; i++) {
				var info = soa.GetLastFlowInfo(i);
				int dir = NormalizeSolidDirection(info.direction);
				_buffer[baseIdx + i] = dir;
				_contentBuffer[baseIdx + i] = dir == DirNone
					? Tag.Invalid : CarriedItemTag(flow, soa, i);
			}
			AdvanceWritePos();
		}

		/// <summary>
		/// Prefab tag of the item that left this conduit cell during the
		/// step that was just simulated. Initial contents hold the item as
		/// it was before the move, which is what the direction describes.
		/// Returns an invalid tag when the item no longer exists: a
		/// receptacle consuming it in the same step can stack it onto an
		/// existing pile, which destroys the incoming pickupable. The
		/// direction is still recorded, it just goes unnamed.
		/// </summary>
		private static Tag CarriedItemTag(SolidConduitFlow flow,
				SolidConduitFlow.SOAInfo soa, int conduitIdx) {
			var contents = soa.GetInitialContents(conduitIdx);
			var pickupable = flow.GetPickupable(contents.pickupableHandle);
			return pickupable != null ? pickupable.PrefabID() : Tag.Invalid;
		}

		private static Tag ElementTag(SimHashes hash) {
			var element = ElementLoader.FindElementByHash(hash);
			return element != null ? element.tag : Tag.Invalid;
		}

		/// <summary>
		/// Returns direction percentages for the given conduit index.
		/// Counts are filled into the provided array indexed by DirNone..DirRight.
		/// Returns the number of samples used as the denominator.
		/// </summary>
		public int GetDirectionCounts(int conduitIdx, int[] counts) {
			counts[DirNone] = 0;
			counts[DirUp] = 0;
			counts[DirDown] = 0;
			counts[DirLeft] = 0;
			counts[DirRight] = 0;

			if (_buffer == null || conduitIdx < 0 || conduitIdx >= _conduitCount)
				return 0;

			int samples = _samplesRecorded < BufferSize
				? _samplesRecorded : BufferSize;
			if (samples == 0) return 0;

			int startSlot = _samplesRecorded < BufferSize
				? 0 : _writePos;
			for (int i = 0; i < samples; i++) {
				int slot = (startSlot + i) % BufferSize;
				int dir = _buffer[slot * _conduitCount + conduitIdx];
				counts[dir]++;
			}
			return samples;
		}

		/// <summary>
		/// Returns per-content direction counts for the given conduit index.
		/// Each dictionary entry maps a content tag (pipe element or rail
		/// item) to a 5-element int array indexed by DirNone..DirRight. Only
		/// entries with directional flow (DirNone excluded) are added.
		/// Content that could not be identified groups under Tag.Invalid.
		/// Returns the sample count.
		/// </summary>
		public int GetContentDirectionCounts(int conduitIdx,
				Dictionary<Tag, int[]> counts) {
			counts.Clear();
			if (_buffer == null || conduitIdx < 0 || conduitIdx >= _conduitCount)
				return 0;

			int samples = _samplesRecorded < BufferSize
				? _samplesRecorded : BufferSize;
			if (samples == 0) return 0;

			int startSlot = _samplesRecorded < BufferSize
				? 0 : _writePos;
			for (int i = 0; i < samples; i++) {
				int slot = (startSlot + i) % BufferSize;
				int idx = slot * _conduitCount + conduitIdx;
				int dir = _buffer[idx];
				if (dir == DirNone) continue;
				Tag content = _contentBuffer[idx];
				if (!counts.TryGetValue(content, out int[] dirs)) {
					dirs = new int[5];
					counts[content] = dirs;
				}
				dirs[dir]++;
			}
			return samples;
		}

		private void EnsureCapacity(int conduitCount) {
			if (_conduitCount == conduitCount && _buffer != null) return;
			int size = conduitCount * BufferSize;
			_buffer = new int[size];
			_contentBuffer = new Tag[size];
			_conduitCount = conduitCount;
			_writePos = 0;
			_samplesRecorded = 0;
		}

		private void AdvanceWritePos() {
			_writePos = (_writePos + 1) % BufferSize;
			if (_samplesRecorded < BufferSize)
				_samplesRecorded++;
		}

		private static int NormalizeFluidDirection(ConduitFlow.FlowDirections dir) {
			switch (dir) {
				case ConduitFlow.FlowDirections.Up: return DirUp;
				case ConduitFlow.FlowDirections.Down: return DirDown;
				case ConduitFlow.FlowDirections.Left: return DirLeft;
				case ConduitFlow.FlowDirections.Right: return DirRight;
				default: return DirNone;
			}
		}

		private static int NormalizeSolidDirection(SolidConduitFlow.FlowDirection dir) {
			switch (dir) {
				case SolidConduitFlow.FlowDirection.Up: return DirUp;
				case SolidConduitFlow.FlowDirection.Down: return DirDown;
				case SolidConduitFlow.FlowDirection.Left: return DirLeft;
				case SolidConduitFlow.FlowDirection.Right: return DirRight;
				default: return DirNone;
			}
		}
	}
}
