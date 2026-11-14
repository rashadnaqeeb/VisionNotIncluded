namespace OniAccess.Handlers.Tiles.Skip {
	/// <summary>
	/// Grid object lookups for skip signatures. While the build tool holds
	/// a tile, the game parks the tile's preview in the tile layer at the
	/// cursor cell (BuildTool.UpdateVis); skips read past it so the cursor
	/// cell does not look like a tile that ends after one step.
	/// </summary>
	internal static class SkipGrid {
		public static UnityEngine.GameObject Object(int cell, ObjectLayer layer) {
			var go = Grid.Objects[cell, (int)layer];
			if (go != null && go == BuildTool.Instance.visualizer)
				return null;
			return go;
		}
	}
}
