using System.Collections.Generic;

namespace OniAccess.Handlers.Tiles.ToolProfiles.Sections {
	/// <summary>
	/// The extent of the sandbox Fill tool, appended last to the cursor
	/// readout like BuildExtentSection: how many connected cells share the
	/// cursor cell's element, and which element that is. Mirrors the highlight
	/// sighted players see before clicking. The element is named because the
	/// glance suppresses it under a foundation tile, and a fill started on a
	/// tile replaces every connected tile of that material, dropping the
	/// buildings that stood on them.
	/// </summary>
	public class FloodFillSection: ICellSection {
		public IEnumerable<string> Read(int cell, CellContext ctx) {
			var tool = PlayerController.Instance.ActiveTool as SandboxFloodTool;
			if (tool == null) return System.Array.Empty<string>();
			return new[] { Describe(tool, cell) };
		}

		/// <summary>
		/// The cells a fill started at <paramref name="cell"/> would replace.
		/// Syncs the tool's mouse cell first: its flood criteria compare against
		/// the last mouse position, and the tile cursor's mouse lock only lands
		/// on the new cell a frame after the cursor moves.
		/// </summary>
		internal static int FillCount(SandboxFloodTool tool, int cell) {
			tool.OnMouseMove(Grid.CellToPosCCC(cell, Grid.SceneLayer.Move));
			return tool.Flood(cell).Count;
		}

		internal static string Describe(SandboxFloodTool tool, int cell) {
			int count = FillCount(tool, cell);
			string element = Grid.Element[cell].name;
			if (count == 1)
				return string.Format((string)STRINGS.ONIACCESS.SANDBOX.FILL_PREVIEW_ONE, element);
			return string.Format((string)STRINGS.ONIACCESS.SANDBOX.FILL_PREVIEW, count, element);
		}
	}
}
