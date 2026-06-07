using System.Collections.Generic;
using OniAccess.Handlers.Tiles.Scanner;
using OniAccess.Speech;

namespace OniAccess.Handlers.Screens.ClusterMap {
	/// <summary>
	/// Scanner for cluster map entities. Three-level hierarchy:
	/// Category (All/Asteroids/Rockets/POIs/Meteors/Unknown),
	/// Item (grouped by entity name),
	/// Instance (individual entities sorted by hex distance).
	///
	/// Same key bindings as the tile scanner. One-pass scan of
	/// ClusterGrid.Instance.cellContents.
	/// </summary>
	public class ClusterScanNavigator {
		private ClusterScanSnapshot _snapshot;
		private int _categoryIndex;
		private int _itemIndex;
		private int _instanceIndex;
		private bool _autoMove;
		private AxialI _preTeleportLocation;
		private bool _hasTeleportOrigin;

		private static readonly Dictionary<string, LocString> _categoryNames =
			new Dictionary<string, LocString> {
				{ ClusterMapTaxonomy.Categories.All, STRINGS.ONIACCESS.CLUSTER_MAP.CATEGORIES.ALL },
				{ ClusterMapTaxonomy.Categories.Asteroids, STRINGS.ONIACCESS.CLUSTER_MAP.CATEGORIES.ASTEROIDS },
				{ ClusterMapTaxonomy.Categories.Rockets, STRINGS.ONIACCESS.CLUSTER_MAP.CATEGORIES.ROCKETS },
				{ ClusterMapTaxonomy.Categories.POIs, STRINGS.ONIACCESS.CLUSTER_MAP.CATEGORIES.POIS },
				{ ClusterMapTaxonomy.Categories.Meteors, STRINGS.ONIACCESS.CLUSTER_MAP.CATEGORIES.METEORS },
				{ ClusterMapTaxonomy.Categories.Unknown, STRINGS.ONIACCESS.CLUSTER_MAP.CATEGORIES.UNKNOWN },
			};

		public void Refresh(AxialI cursor) {
			var entries = ScanGrid(cursor);
			if (entries.Count == 0) {
				_snapshot = null;
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.SCANNER.EMPTY);
				return;
			}
			_snapshot = new ClusterScanSnapshot(entries, cursor);
			_categoryIndex = 0;
			_itemIndex = 0;
			_instanceIndex = 0;
			SpeechPipeline.SpeakInterrupt(
				(string)STRINGS.ONIACCESS.SCANNER.REFRESHED);
		}

		public void SearchRefresh(string query, AxialI cursor) {
			var entries = ScanGrid(cursor);
			var matcher = new ScannerSearch.NameMatcher(query);
			var filtered = new List<ClusterScanEntry>();

			foreach (var entry in entries) {
				int sortKey = matcher.Match(entry.ItemName);
				if (sortKey < 0) continue;
				filtered.Add(new ClusterScanEntry {
					Location = entry.Location,
					Category = (string)STRINGS.ONIACCESS.SCANNER.CATEGORIES.SEARCH,
					ItemName = entry.ItemName,
					SortKey = sortKey,
				});
			}

			if (filtered.Count == 0) {
				SpeechPipeline.SpeakInterrupt(string.Format(
					(string)STRINGS.ONIACCESS.SEARCH.NO_MATCH, query));
				return;
			}

			_snapshot = new ClusterScanSnapshot(filtered, cursor, skipAllCategory: true);
			_categoryIndex = 0;
			_itemIndex = 0;
			_instanceIndex = 0;

			SpeechPipeline.SpeakInterrupt(query);
			string item = FormatCurrentItem(cursor);
			if (item != null)
				SpeechPipeline.SpeakQueued(item);
		}

		public void CycleCategory(int direction, AxialI cursor) {
			if (EnsureSnapshot(cursor)) return;
			if (_snapshot.CategoryCount == 0) { SpeakEmpty(); return; }

			int prev = _categoryIndex;
			_categoryIndex = Wrap(_categoryIndex, direction, _snapshot.CategoryCount);
			PlayWrapCheck(prev, _categoryIndex, direction, _snapshot.CategoryCount);

			_itemIndex = 0;
			_instanceIndex = 0;

			string catName = GetCategoryName(_snapshot.GetCategory(_categoryIndex).Name);
			SpeechPipeline.SpeakInterrupt(catName);

			string item = FormatCurrentItem(cursor);
			if (item != null)
				SpeechPipeline.SpeakQueued(item);
			AutoMoveIfEnabled(cursor);
		}

		public void CycleItem(int direction, AxialI cursor) {
			if (EnsureSnapshot(cursor)) return;
			var cat = CurrentCategory();
			if (cat == null || cat.Items.Count == 0) { SpeakEmpty(); return; }

			int prev = _itemIndex;
			_itemIndex = Wrap(_itemIndex, direction, cat.Items.Count);
			PlayWrapCheck(prev, _itemIndex, direction, cat.Items.Count);

			_instanceIndex = 0;

			string announcement = FormatCurrentItem(cursor);
			if (announcement != null)
				SpeechPipeline.SpeakInterrupt(announcement);
			AutoMoveIfEnabled(cursor);
		}

		public void CycleInstance(int direction, AxialI cursor) {
			if (EnsureSnapshot(cursor)) return;
			var item = CurrentItem();
			if (item == null || item.Instances.Count == 0) { SpeakEmpty(); return; }

			int prev = _instanceIndex;
			_instanceIndex = Wrap(_instanceIndex, direction, item.Instances.Count);
			PlayWrapCheck(prev, _instanceIndex, direction, item.Instances.Count);

			string announcement = FormatCurrentItem(cursor);
			if (announcement != null)
				SpeechPipeline.SpeakInterrupt(announcement);
			AutoMoveIfEnabled(cursor);
		}

		public string ToggleAutoMove() {
			_autoMove = !_autoMove;
			return _autoMove
				? (string)STRINGS.ONIACCESS.SCANNER.AUTO_MOVE_ON
				: (string)STRINGS.ONIACCESS.SCANNER.AUTO_MOVE_OFF;
		}

		public string OrientItem(AxialI cursor) {
			var entry = CurrentEntry();
			if (entry == null) return (string)STRINGS.ONIACCESS.SCANNER.EMPTY;
			return string.Format(
				(string)STRINGS.ONIACCESS.SCANNER.ORIENT,
				HexCoordinates.Format(cursor, entry.Location), entry.ItemName);
		}

		/// <summary>
		/// Returns the location to teleport to, or null if no valid entry.
		/// Saves the pre-teleport location for TeleportBack.
		/// </summary>
		public AxialI? Teleport(AxialI cursor) {
			var entry = CurrentEntry();
			if (entry == null) return null;
			_preTeleportLocation = cursor;
			_hasTeleportOrigin = true;
			return entry.Location;
		}

		public AxialI? TeleportBack() {
			if (!_hasTeleportOrigin) return null;
			_hasTeleportOrigin = false;
			return _preTeleportLocation;
		}

		// -------------------------------------------------------------------
		// Scanning
		// -------------------------------------------------------------------

		private List<ClusterScanEntry> ScanGrid(AxialI cursor) {
			var entries = new List<ClusterScanEntry>();
			try {
				var grid = ClusterGrid.Instance;
				var fow = SaveGame.Instance.GetSMI<ClusterFogOfWarManager.Instance>();

				foreach (var kvp in grid.cellContents) {
					var cell = kvp.Key;
					var revealLevel = fow.GetCellRevealLevel(cell);

					if (revealLevel == ClusterRevealLevel.Hidden)
						continue;

					if (revealLevel == ClusterRevealLevel.Peeked) {
						// Group all peeked entities as "Unknown"
						bool hasHiddenEntity = false;
						foreach (var entity in kvp.Value) {
							if (entity.IsVisible &&
								entity.IsVisibleInFOW == ClusterRevealLevel.Peeked) {
								hasHiddenEntity = true;
								break;
							}
						}
						if (hasHiddenEntity) {
							entries.Add(new ClusterScanEntry {
								Location = cell,
								Category = ClusterMapTaxonomy.Categories.Unknown,
								ItemName = (string)STRINGS.UI.CLUSTERMAP.TOOLTIP_PEEKED_HEX_WITH_OBJECT,
								SortKey = 0,
							});
						}
						continue;
					}

					// Visible cell - add each visible entity
					foreach (var entity in kvp.Value) {
						if (!entity.IsVisible) continue;
						var sel = entity.GetComponent<KSelectable>();
						if (sel != null && !sel.IsSelectable) continue;
						string category = LayerToCategory(entity.Layer);
						if (category == null) continue;
						entries.Add(new ClusterScanEntry {
							Location = cell,
							Category = category,
							ItemName = entity.Name,
							SortKey = (int)entity.Layer,
						});
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ClusterScanNavigator.ScanGrid: {ex}");
			}
			return entries;
		}

		private static string LayerToCategory(EntityLayer layer) {
			switch (layer) {
				case EntityLayer.Asteroid:
					return ClusterMapTaxonomy.Categories.Asteroids;
				case EntityLayer.Craft:
					return ClusterMapTaxonomy.Categories.Rockets;
				case EntityLayer.POI:
				case EntityLayer.Debri:
					return ClusterMapTaxonomy.Categories.POIs;
				case EntityLayer.Meteor:
					return ClusterMapTaxonomy.Categories.Meteors;
				case EntityLayer.Payload:
					return ClusterMapTaxonomy.Categories.Rockets;
				default:
					return null;
			}
		}

		// -------------------------------------------------------------------
		// Navigation helpers
		// -------------------------------------------------------------------

		private bool EnsureSnapshot(AxialI cursor) {
			if (_snapshot != null) return false;
			Refresh(cursor);
			return true;
		}

		private ClusterScanCategory CurrentCategory() {
			if (_snapshot == null || _categoryIndex >= _snapshot.CategoryCount)
				return null;
			return _snapshot.GetCategory(_categoryIndex);
		}

		private ClusterScanItem CurrentItem() {
			var cat = CurrentCategory();
			if (cat == null || _itemIndex >= cat.Items.Count) return null;
			return cat.Items[_itemIndex];
		}

		private ClusterScanEntry CurrentEntry() {
			var item = CurrentItem();
			if (item == null || _instanceIndex >= item.Instances.Count) return null;
			return item.Instances[_instanceIndex];
		}

		/// "2 of 5" here is an instance count (how many copies of this entity exist),
		/// not a positional index in a list. The user needs this to know the map has
		/// multiple instances and to cycle through them.
		private string FormatCurrentItem(AxialI cursor) {
			var item = CurrentItem();
			if (item == null || item.Instances.Count == 0) return null;
			if (_instanceIndex >= item.Instances.Count)
				_instanceIndex = item.Instances.Count - 1;
			var entry = item.Instances[_instanceIndex];
			string distance = HexCoordinates.Format(
				_autoMove ? _snapshot.Origin : cursor, entry.Location);
			if (item.Instances.Count == 1)
				return item.ItemName + ", " + distance;
			return string.Format(
				(string)STRINGS.ONIACCESS.SCANNER.INSTANCE_WITH_DISTANCE,
				item.ItemName, distance,
				string.Format(
					(string)STRINGS.ONIACCESS.SCANNER.INSTANCE_OF,
					_instanceIndex + 1, item.Instances.Count));
		}

		private void AutoMoveIfEnabled(AxialI cursor) {
			// Auto-move is handled by the handler reading the current entry location
		}

		/// <summary>
		/// Get the current entry's location for auto-move.
		/// </summary>
		public AxialI? CurrentLocation() {
			var entry = CurrentEntry();
			return entry?.Location;
		}

		private static string GetCategoryName(string key) {
			return _categoryNames.TryGetValue(key, out LocString loc)
				? (string)loc : key;
		}

		private static int Wrap(int current, int direction, int count) {
			if (count == 0) return 0;
			return (current + direction + count) % count;
		}

		private static void PlayWrapCheck(
				int prev, int next, int direction, int count) {
			if (count <= 1) return;
			bool wrapped = (direction > 0 && next <= prev)
				|| (direction < 0 && next >= prev);
			if (wrapped) BaseScreenHandler.PlaySound("HUD_Click");
		}

		private static void SpeakEmpty() {
			SpeechPipeline.SpeakInterrupt(
				(string)STRINGS.ONIACCESS.SCANNER.EMPTY);
		}

		public bool AutoMove => _autoMove;
	}
}
