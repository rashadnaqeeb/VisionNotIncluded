using System;
using System.Collections.Generic;
using OniAccess.Speech;
using OniAccess.Util;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Tiles {
	/// <summary>
	/// Modal picker for selecting one of multiple KSelectable entities at a tile.
	/// Pushed by TileCursorHandler when Enter is pressed on a cell with 2+ entities.
	/// Selecting an item calls SelectTool.Instance.Select() to open the details screen.
	/// </summary>
	public class EntityPickerHandler: BaseMenuHandler {
		private readonly IReadOnlyList<KSelectable> _selectables;
		private readonly IReadOnlyList<string> _displayLabels;

		public override string DisplayName =>
			(string)STRINGS.ONIACCESS.HANDLERS.ENTITY_PICKER;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }
			= OniAccess.Handlers.Tools.ToolPickerHandler.ModalMenuHelp;

		public EntityPickerHandler(
				IReadOnlyList<KSelectable> selectables,
				IReadOnlyList<string> displayLabels = null) {
			_selectables = selectables;
			_displayLabels = displayLabels;
		}

		public override int ItemCount => _selectables.Count;

		private string GetDisplayText(int index) {
			if (_displayLabels != null && index >= 0 && index < _displayLabels.Count)
				return _displayLabels[index];
			return DebrisNameHelper.GetDisplayName(_selectables[index].gameObject);
		}

		public override string GetItemLabel(int index) {
			if (index < 0 || index >= _selectables.Count) return null;
			return GetDisplayText(index);
		}

		protected override string GetReviewItemText() {
			if (CurrentIndex < 0 || CurrentIndex >= _selectables.Count) return null;
			return TextFilter.FilterForSpeech(GetDisplayText(CurrentIndex));
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			if (CurrentIndex >= 0 && CurrentIndex < _selectables.Count)
				SpeechPipeline.SpeakInterrupt(ComposeItem(GetReviewItemText(), CurrentIndex));
		}

		public override void OnActivate() {
			PlaySound("HUD_Click_Open");
			CurrentIndex = 0;
			_search.Clear();
			SpeechPipeline.SpeakQueued(
				(string)STRINGS.ONIACCESS.TILE_CURSOR.SELECT_OBJECT);
			if (_selectables.Count > 0)
				SpeechPipeline.SpeakQueued(ComposeItem(
					TextFilter.FilterForSpeech(GetDisplayText(0)), 0));
		}

		public override void OnDeactivate() {
			PlaySound("HUD_Click_Close");
			base.OnDeactivate();
		}

		protected override void ActivateCurrentItem() {
			if (CurrentIndex < 0 || CurrentIndex >= _selectables.Count)
				return;
			var entity = _selectables[CurrentIndex];
			// Pop before Select: Select() synchronously triggers DetailsScreen.OnShow
			// which pushes DetailsScreenHandler. If we pop after, we'd pop that instead.
			HandlerStack.Pop();
			if (!(PlayerController.Instance.ActiveTool is SelectTool))
				SelectTool.Instance.Activate();
			SelectTool.Instance.Select(entity);
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e))
				return true;
			if (e.TryConsume(Action.Escape)) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.TOOLTIP.CLOSED);
				HandlerStack.Pop();
				return true;
			}
			return false;
		}

		private static readonly int[] SingleGoLayers = {
			(int)ObjectLayer.Building,
			(int)ObjectLayer.FoundationTile,
			(int)ObjectLayer.Backwall,
			(int)ObjectLayer.Minion,
			(int)ObjectLayer.Wire,
			(int)ObjectLayer.WireConnectors,
			(int)ObjectLayer.LiquidConduit,
			(int)ObjectLayer.LiquidConduitConnection,
			(int)ObjectLayer.GasConduit,
			(int)ObjectLayer.GasConduitConnection,
			(int)ObjectLayer.SolidConduit,
			(int)ObjectLayer.SolidConduitConnection,
			(int)ObjectLayer.LogicWire,
			(int)ObjectLayer.LogicGate,
		};

		/// <summary>
		/// Collects all selectable entities at a cell. Uses Grid.Objects for
		/// layer-registered entities plus a collision query for elements
		/// (CellSelectionObject) which aren't on any object layer.
		/// </summary>
		public static List<KSelectable> CollectSelectables(int cell) {
			var result = new List<KSelectable>();
			var seen = new HashSet<UnityEngine.GameObject>();

			foreach (int layer in SingleGoLayers) {
				var go = Grid.Objects[cell, layer];
				if (go == null || !seen.Add(go)) continue;
				var ks = go.GetComponent<KSelectable>();
				if (ks != null && ks.isActiveAndEnabled && ks.IsSelectable)
					result.Add(ks);
			}

			var pickGo = Grid.Objects[cell, (int)ObjectLayer.Pickupables];
			if (pickGo != null) {
				var pick = pickGo.GetComponent<Pickupable>();
				if (pick != null) {
					var item = pick.objectLayerListItem;
					while (item != null) {
						var ks = item.gameObject.GetComponent<KSelectable>();
						if (ks != null && ks.isActiveAndEnabled && ks.IsSelectable
							&& seen.Add(ks.gameObject))
							result.Add(ks);
						item = item.nextItem;
					}
				}
			}

			// CellSelectionObject (element info) isn't on any object layer —
			// it's only reachable via the collision query.
			int x, y;
			Grid.CellToXY(cell, out x, out y);
			var cellCenter = Grid.CellToPosCCC(cell, Grid.SceneLayer.Move);
			var entries = ListPool<ScenePartitionerEntry, EntityPickerHandler>.Allocate();
			GameScenePartitioner.Instance.GatherEntries(
				x, y, 1, 1,
				GameScenePartitioner.Instance.collisionLayer,
				entries);
			foreach (var entry in entries) {
				var collider = entry.obj as KCollider2D;
				if (collider == null) continue;
				if (!collider.Intersects(
						new UnityEngine.Vector2(cellCenter.x, cellCenter.y)))
					continue;
				var ks = collider.GetComponent<KSelectable>();
				if (ks == null)
					ks = collider.GetComponentInParent<KSelectable>();
				if (ks == null || !ks.isActiveAndEnabled || !ks.IsSelectable) continue;
				if (!seen.Add(ks.gameObject)) continue;
				var cso = ks.GetComponent<CellSelectionObject>();
				if (cso != null && cso.alternateSelectionObject != null
					&& seen.Contains(cso.alternateSelectionObject.gameObject))
					continue;
				result.Add(ks);
			}
			entries.Recycle();

			result.Sort((a, b) => EntitySortKey(a).CompareTo(EntitySortKey(b)));
			return result;
		}

		// Buildings, then tiles and utilities, then dupes, critters, debris
		// and props, then backwalls (drywall, Gravitas lab walls), then the
		// element. Backwalls sit behind everything, as in the game's hover card.
		private static int EntitySortKey(KSelectable ks) {
			var building = ks.GetComponent<Building>();
			if (building != null) {
				if (building.Def.ObjectLayer == ObjectLayer.Building) return 0;
				if (building.Def.ObjectLayer == ObjectLayer.Backwall) return 3;
				return 1;
			}
			if (ks.GetComponent<CellSelectionObject>() != null) return 4;
			return 2;
		}

		/// <summary>
		/// Match each selectable to a tooltip block by comparing entity names
		/// against block prefixes, then extend it with the building settings the
		/// game's hover text leaves out. Returns a label list parallel to
		/// selectables.
		/// </summary>
		public static IReadOnlyList<string> MatchTooltipLabels(
				IReadOnlyList<KSelectable> selectables,
				IReadOnlyList<string> tooltipLines) {
			var labels = new string[selectables.Count];
			var consumed = tooltipLines != null
				? new bool[tooltipLines.Count] : null;
			for (int i = 0; i < selectables.Count; i++) {
				var selectable = selectables[i];
				string label = MatchTooltipLine(selectable, tooltipLines, consumed)
					?? DebrisNameHelper.GetDisplayName(selectable.gameObject);
				string settings = BuildingSettings.Describe(selectable.gameObject);
				labels[i] = settings == null ? label : label + ", " + settings;
			}
			return labels;
		}

		/// <summary>
		/// The first unclaimed tooltip block starting with this entity's name,
		/// or null when no block describes it.
		/// </summary>
		private static string MatchTooltipLine(
				KSelectable selectable,
				IReadOnlyList<string> tooltipLines, bool[] consumed) {
			if (tooltipLines == null) return null;
			string rawName = selectable.GetName();
			for (int j = 0; j < tooltipLines.Count; j++) {
				if (consumed[j]) continue;
				if (tooltipLines[j].StartsWith(
						rawName, StringComparison.OrdinalIgnoreCase)) {
					consumed[j] = true;
					return tooltipLines[j];
				}
			}
			return null;
		}
	}
}
