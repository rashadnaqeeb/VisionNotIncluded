using System.Collections.Generic;
using UnityEngine;

namespace OniAccess.Handlers.Tiles {
	/// <summary>
	/// Captures the content drawn by HoverTextDrawer during each hover tooltip
	/// render pass. Harmony patches call BeginFrame/BeginBlock/AppendText/
	/// AppendIcon/AppendNewLine/EndFrame. TileCursorHandler reads
	/// GetTooltipText() on Q press.
	///
	/// Text segments within a visual line are concatenated directly (no separator).
	/// NewLine boundaries create separate lines, joined with ", " within a block.
	/// Blocks (shadow bars) are joined with ", ".
	/// Sprites are emitted as synthetic sprite tags that TextFilter resolves
	/// during SpeechPipeline.SpeakInterrupt().
	///
	/// _capturedText holds the assembled string from the most recent completed
	/// draw pass. This is a deliberate exception to the "never cache game state"
	/// rule: HoverTextDrawer has no queryable API, so capture-and-store is the
	/// only way to read tooltip content. Staleness is at most one frame behind
	/// the cursor position (drawing runs in LateUpdate, Q is read in Update).
	/// </summary>
	internal static class TooltipCapture {
		private static readonly List<List<string>> _blocks = new List<List<string>>();
		private static List<string> _currentBlock;
		private static string _currentLine = "";
		private static string _capturedText;
		private static System.Collections.Generic.IReadOnlyList<string> _capturedLines;
		private static bool _capturing;

		internal static void BeginFrame() {
			_blocks.Clear();
			_currentBlock = null;
			_currentLine = "";
			_capturedText = null;
			_capturedLines = null;
			_capturing = true;
		}

		internal static void BeginBlock() {
			FlushLine();
			_currentBlock = new List<string>();
			_blocks.Add(_currentBlock);
		}

		internal static void AppendText(string text) {
			if (!_capturing || _currentBlock == null || string.IsNullOrWhiteSpace(text))
				return;
			_currentLine += text;
		}

		internal static void AppendIcon(Sprite icon) {
			if (!_capturing || _currentBlock == null || icon == null) return;
			_currentLine += $"<sprite name=\"{icon.name}\">";
		}

		internal static void AppendNewLine() {
			if (!_capturing || _currentBlock == null) return;
			FlushLine();
		}

		internal static void EndFrame() {
			FlushLine();
			_capturing = false;
			if (_blocks.Count == 0) {
				_capturedText = null;
				_capturedLines = null;
				return;
			}
			var blockTexts = new List<string>(_blocks.Count);
			foreach (var block in _blocks) {
				string blockText = string.Join(", ", block);
				if (!string.IsNullOrEmpty(blockText))
					blockTexts.Add(blockText);
			}
			_capturedText = blockTexts.Count > 0 ? string.Join(", ", blockTexts) : null;
			_capturedLines = blockTexts.Count > 0 ? blockTexts.AsReadOnly() : null;
		}

		internal static string GetTooltipText() => _capturedText;

		internal static System.Collections.Generic.IReadOnlyList<string> GetTooltipLines()
			=> _capturedLines;

		/// <summary>
		/// Return the most relevant tooltip block for a quick summary.
		/// Priority: overlay-specific block, then conduit block (in
		/// utility overlays), then building, then first block.
		/// Returns null when no tooltip is captured.
		/// </summary>
		internal static string GetPrioritySummary(int cell) =>
			GetPrioritySummary(cell, out _);

		/// <summary>
		/// GetPrioritySummary, also reporting which entity at the cell the
		/// returned block describes. Only the building branch names one;
		/// every other branch hands back an overlay, conduit, or fallback
		/// block, and sets describedEntity to null. Callers that want to
		/// extend a building's own readout must check this rather than
		/// assume the summary is about the building they care about, since
		/// in most overlays it is not.
		/// </summary>
		internal static string GetPrioritySummary(
				int cell, out GameObject describedEntity) {
			describedEntity = null;
			var lines = _capturedLines;
			if (lines == null || lines.Count == 0) return null;

			var entities = GetNonBackwallEntities(cell);
			var backwall = GetBackwall(cell);
			if (backwall != null && entities.Count > 0)
				entities.Add(new Entity(backwall.GetProperName(), backwall));

			// Overlay blocks are drawn first. If the active overlay produced
			// one, lines[0] is the overlay block. Guard against false
			// positives by checking lines[0] doesn't start with a building
			// name (overlay titles like "Decor" never match building names).
			if (HasOverlayTooltipBlock(cell)
				&& MatchEntity(lines[0], entities) == null)
				return lines[0];

			// In utility overlays, conduit status (circuit load, pipe
			// contents) is more relevant than the building block. Find
			// the conduit block first and strip its name prefix.
			string conduitResult = FindConduitBlock(lines, cell);
			if (conduitResult != null) return conduitResult;

			if (entities.Count > 0) {
				for (int i = 0; i < lines.Count; i++) {
					var match = MatchEntity(lines[i], entities);
					if (match != null) {
						describedEntity = match;
						return StripEntityPrefix(lines[i], cell)
							?? lines[i];
					}
				}
			}

			// Outside utility overlays, skip conduit/wire blocks that the
			// movement cursor doesn't mention — hearing them is disorienting.
			if (GetUtilityConduitLayers() == null) {
				var conduitNames = GetAllConduitNames(cell);
				if (conduitNames.Count > 0) {
					for (int i = 0; i < lines.Count; i++) {
						if (!MatchesAnyName(lines[i], conduitNames))
							return lines[i];
					}
				}
			}

			return lines[0];
		}

		/// <summary>
		/// In utility overlays, find the tooltip block for the conduit
		/// at this cell and return it with the name stripped. Returns
		/// null when not in a utility overlay or no conduit block found.
		/// </summary>
		private static string FindConduitBlock(
				System.Collections.Generic.IReadOnlyList<string> lines,
				int cell) {
			int[] layers = GetUtilityConduitLayers();
			if (layers == null) return null;

			var names = new List<string>(2);
			foreach (int layer in layers)
				AddBuildingName(cell, layer, names);
			if (names.Count == 0) return null;

			for (int i = 0; i < lines.Count; i++) {
				if (!MatchesAnyName(lines[i], names)) continue;
				return StripFirstSegment(lines[i]) ?? lines[i];
			}
			return null;
		}

		/// <summary>
		/// Strip the first comma-separated segment (entity name line)
		/// from a tooltip block. Returns null if there is no separator
		/// or stripping would leave only whitespace.
		/// </summary>
		private static string StripFirstSegment(string block) {
			int sep = block.IndexOf(", ", System.StringComparison.Ordinal);
			if (sep < 0) return null;
			string remainder = block.Substring(sep + 2);
			return string.IsNullOrWhiteSpace(remainder) ? null : remainder;
		}

		/// <summary>
		/// Like StripFirstSegment but requires at least two segments
		/// after the name before stripping. A building with only one
		/// extra line (typically just temperature) keeps its name —
		/// hearing bare temperature with no context is disorienting.
		/// </summary>
		private static string StripFirstSegmentIfMultiple(string block) {
			int first = block.IndexOf(", ", System.StringComparison.Ordinal);
			if (first < 0) return null;
			int second = block.IndexOf(", ", first + 2,
				System.StringComparison.Ordinal);
			if (second < 0) return null;
			string remainder = block.Substring(first + 2);
			return string.IsNullOrWhiteSpace(remainder) ? null : remainder;
		}

		/// <summary>
		/// Strip the building identity (name and material) from a
		/// tooltip block. In the None overlay, requires multiple
		/// status lines after the name before stripping so a building
		/// with only temperature keeps its name. Other overlays strip
		/// temperature, so a single remaining line is still worth
		/// reading without the name prefix. Returns null when the
		/// block doesn't match any entity name at the cell, or when
		/// stripping would leave insufficient content.
		/// </summary>
		private static string StripEntityPrefix(string block, int cell) {
			var names = new List<string>(4);
			AddBuildingName(cell, (int)ObjectLayer.Building, names);
			AddBuildingName(cell, (int)ObjectLayer.FoundationTile, names);

			int[] conduitLayers = GetUtilityConduitLayers();
			if (conduitLayers != null) {
				foreach (int layer in conduitLayers)
					AddBuildingName(cell, layer, names);
			}

			if (names.Count == 0) return null;
			if (!MatchesAnyName(block, names)) return null;

			bool isNoneOverlay = OverlayScreen.Instance == null
				|| OverlayScreen.Instance.GetMode() == OverlayModes.None.ID;
			return isNoneOverlay
				? StripFirstSegmentIfMultiple(block)
				: StripFirstSegment(block);
		}

		private static int[] GetUtilityConduitLayers() {
			if (OverlayScreen.Instance == null) return null;
			var mode = OverlayScreen.Instance.GetMode();
			if (mode == OverlayModes.Power.ID)
				return new[] {
					(int)ObjectLayer.Wire,
					(int)ObjectLayer.WireConnectors };
			if (mode == OverlayModes.LiquidConduits.ID)
				return new[] {
					(int)ObjectLayer.LiquidConduit,
					(int)ObjectLayer.LiquidConduitConnection };
			if (mode == OverlayModes.GasConduits.ID)
				return new[] {
					(int)ObjectLayer.GasConduit,
					(int)ObjectLayer.GasConduitConnection };
			if (mode == OverlayModes.SolidConveyor.ID)
				return new[] {
					(int)ObjectLayer.SolidConduit,
					(int)ObjectLayer.SolidConduitConnection };
			return null;
		}

		private static readonly int[] AllConduitLayers = {
			(int)ObjectLayer.Wire, (int)ObjectLayer.WireConnectors,
			(int)ObjectLayer.LiquidConduit,
			(int)ObjectLayer.LiquidConduitConnection,
			(int)ObjectLayer.GasConduit,
			(int)ObjectLayer.GasConduitConnection,
			(int)ObjectLayer.SolidConduit,
			(int)ObjectLayer.SolidConduitConnection,
			(int)ObjectLayer.LogicWire, (int)ObjectLayer.LogicGate,
		};

		private static List<string> GetAllConduitNames(int cell) {
			var names = new List<string>(4);
			foreach (int layer in AllConduitLayers)
				AddBuildingName(cell, layer, names);
			return names;
		}

		/// <summary>
		/// Whether the active overlay draws its own tooltip block before
		/// the entity loop in SelectToolHoverTextCard.UpdateHoverElements.
		/// </summary>
		private static bool HasOverlayTooltipBlock(int cell) {
			if (OverlayScreen.Instance == null) return false;
			var mode = OverlayScreen.Instance.GetMode();

			if (mode == OverlayModes.Decor.ID) return true;
			if (mode == OverlayModes.Light.ID) return true;
			if (mode == OverlayModes.Radiation.ID) return true;
			if (mode == OverlayModes.Logic.ID) return true;
			if (mode == OverlayModes.Rooms.ID)
				return Game.Instance != null
					&& Game.Instance.roomProber != null
					&& Game.Instance.roomProber.GetCavityForCell(cell) != null;
			if (mode == OverlayModes.Temperature.ID
				&& Game.Instance != null
				&& Game.Instance.temperatureOverlayMode
					== Game.TemperatureOverlayModes.HeatFlow
				&& !Grid.Solid[cell])
				return true;
			return false;
		}

		/// <summary>A named thing at a cell, paired with the object it is.</summary>
		private struct Entity {
			internal readonly string Name;
			internal readonly GameObject Go;

			internal Entity(string name, GameObject go) {
				Name = name;
				Go = go;
			}
		}

		private static List<Entity> GetNonBackwallEntities(int cell) {
			var entities = new List<Entity>(2);
			AddEntity(cell, (int)ObjectLayer.Building, entities);
			AddEntity(cell, (int)ObjectLayer.FoundationTile, entities);
			return entities;
		}

		private static void AddEntity(
				int cell, int layer, List<Entity> entities) {
			var go = Grid.Objects[cell, layer];
			if (go == null) return;
			string name = go.GetProperName();
			if (!string.IsNullOrEmpty(name))
				entities.Add(new Entity(name, go));
		}

		private static GameObject GetBackwall(int cell) {
			var go = Grid.Objects[cell, (int)ObjectLayer.Backwall];
			if (go == null) return null;
			return string.IsNullOrEmpty(go.GetProperName()) ? null : go;
		}

		/// <summary>
		/// The entity whose name this tooltip block starts with, or null
		/// when the block belongs to none of them.
		/// </summary>
		private static GameObject MatchEntity(
				string line, List<Entity> entities) {
			for (int i = 0; i < entities.Count; i++) {
				if (line.StartsWith(entities[i].Name,
						System.StringComparison.OrdinalIgnoreCase))
					return entities[i].Go;
			}
			return null;
		}

		private static void AddBuildingName(
				int cell, int layer, List<string> names) {
			var go = Grid.Objects[cell, layer];
			if (go == null) return;
			string name = go.GetProperName();
			if (!string.IsNullOrEmpty(name))
				names.Add(name);
		}

		private static bool MatchesAnyName(string line, List<string> names) {
			for (int i = 0; i < names.Count; i++) {
				if (line.StartsWith(names[i],
						System.StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}

		internal static void Reset() {
			_blocks.Clear();
			_currentBlock = null;
			_currentLine = "";
			_capturedText = null;
			_capturedLines = null;
			_capturing = false;
		}

		private static void FlushLine() {
			if (_currentBlock != null && !string.IsNullOrWhiteSpace(_currentLine))
				_currentBlock.Add(_currentLine.Trim());
			_currentLine = "";
		}
	}
}
