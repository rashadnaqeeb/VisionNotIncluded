namespace OniAccess.Handlers.Tiles.Skip {
	/// <summary>
	/// Distinguishes cells by what occupies them: each building type,
	/// each tile type, each liquid element, or natural solid.
	/// All gases are treated as one zone.
	/// Used for the default view and all unmapped overlays.
	/// </summary>
	public class DefaultSkipStrategy: ISkipStrategy {
		private static readonly Tag PoiTileTag = new Tag(TilePOIConfig.ID);
		private static readonly Tag TileTag = new Tag(TileConfig.ID);

		public object GetSignature(int cell) {
			var building = Grid.Objects[cell, (int)ObjectLayer.Building];
			if (building != null)
				return NormalizePoiTile(building.PrefabID());

			var tile = Grid.Objects[cell, (int)ObjectLayer.FoundationTile];
			if (tile != null)
				return NormalizePoiTile(tile.PrefabID());

			var element = Grid.Element[cell];
			if (element.IsGas)
				return new Tag("gas");

			return element.tag;
		}

		// Worldgen POI tiles play identically to built tiles; the player
		// hears no difference, so they form one zone.
		private static Tag NormalizePoiTile(Tag tag) {
			return tag == PoiTileTag ? TileTag : tag;
		}
	}
}
