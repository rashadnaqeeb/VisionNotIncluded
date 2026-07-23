namespace OniAccess.Handlers.Tiles.Skip {
	/// <summary>
	/// Groups similar things aggressively: all floor tiles as one zone,
	/// all ladders as one zone, all plants as one zone,
	/// all decorations as one zone, all liquids as one zone,
	/// all solids as one zone.
	/// Buildings without a grouping tag keep their PrefabID.
	/// Used for alt+arrow coarse skipping.
	/// </summary>
	public class CoarseSkipStrategy: ISkipStrategy {
		// Worldgen POI tiles carry no FloorTiles tag but play identically
		// to built tiles, so they join the tile zone.
		private static readonly Tag PoiTileTag = new Tag(TilePOIConfig.ID);

		public object GetSignature(int cell) {
			var building = Grid.Objects[cell, (int)ObjectLayer.Building];
			if (building != null) {
				var kpid = building.GetComponent<KPrefabID>();
				if (kpid.HasTag(GameTags.FloorTiles)
					|| kpid.PrefabID() == PoiTileTag)
					return new Tag("tile");
				if (kpid.HasTag(GameTags.Ladders))
					return new Tag("ladder");
				if (kpid.HasTag(GameTags.Plant))
					return new Tag("plant");
				if (kpid.HasTag(GameTags.Decoration))
					return new Tag("decor");
				return building.PrefabID();
			}

			var tile = Grid.Objects[cell, (int)ObjectLayer.FoundationTile];
			if (tile != null) {
				var kpid = tile.GetComponent<KPrefabID>();
				if (kpid.HasTag(GameTags.FloorTiles)
					|| kpid.PrefabID() == PoiTileTag)
					return new Tag("tile");
				if (kpid.HasTag(GameTags.Ladders))
					return new Tag("ladder");
				return tile.PrefabID();
			}

			var element = Grid.Element[cell];
			if (element.IsGas)
				return new Tag("gas");
			if (element.IsLiquid)
				return new Tag("liquid");
			if (element.IsSolid)
				return new Tag("solid");

			return element.tag;
		}
	}
}
