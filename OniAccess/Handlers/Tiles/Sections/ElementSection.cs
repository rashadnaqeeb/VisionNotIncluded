using System.Collections.Generic;

namespace OniAccess.Handlers.Tiles.Sections {
	/// <summary>
	/// Speaks Grid.Element[cell].name with a glance-friendly mass.
	/// Suppressed when a foundation tile is present (the tile IS the solid
	/// element, so announcing both is redundant), or when a foreground
	/// building is present and the element is gas. Exceptions:
	/// - A hollow tile (BuildingSection.IsHollowTile): nothing is read for
	///   the tile, so the element is what the cell holds.
	/// - Oxygen overlay: always speak the element.
	/// - Liquid on a building: submersion, gameplay-critical.
	/// - Solid on a building (not foundation tile): entombment, gameplay-critical.
	/// Still speaks when only a Backwall (drywall, tempshift plate) is present,
	/// since sighted players see the element through background buildings.
	/// Appends a natural backwall token ("Granite Backwall") when the cell is
	/// not solid and a backwall is exposed behind it, even if the element
	/// itself is suppressed.
	/// </summary>
	public class ElementSection: ICellSection {
		public IEnumerable<string> Read(int cell, CellContext ctx) {
			var element = Grid.Element[cell];
			string backwall = BackwallToken(cell);
			if (OverlayScreen.Instance.GetMode() != OverlayModes.Oxygen.ID && !element.IsLiquid) {
				var foundation = Grid.Objects[cell, (int)ObjectLayer.FoundationTile];
				if (foundation != null) {
					if (!BuildingSection.IsHollowTile(foundation, cell))
						return Tokens(null, backwall);
				} else if (!element.IsSolid && Grid.Objects[cell, (int)ObjectLayer.Building] != null) {
					return Tokens(null, backwall);
				}
			}
			if (element == null) return Tokens(null, backwall);
			if (element.IsVacuum)
				return Tokens(element.name, backwall);
			float kg = Grid.Mass[cell];
			string text = $"{element.name}, {FormatGlanceMass(kg)}";
			if (Game.Instance.GetComponent<EntombedItemVisualizer>().IsEntombedItem(cell))
				text += ", " + (string)STRINGS.MISC.STATUSITEMS.BURIEDITEM.NAME;
			return Tokens(text, backwall);
		}

		private static IEnumerable<string> Tokens(string element, string backwall) {
			if (element != null) yield return element;
			if (backwall != null) yield return backwall;
		}

		// Natural backwall is only visible (and diggable) behind non-solid cells
		private static string BackwallToken(int cell) {
			if (Grid.Solid[cell] || !BackwallManager.HasBackwall(cell)) return null;
			return FormatBackwallName(BackwallManager.At(cell).Element);
		}

		internal static string FormatBackwallName(Element element) {
			return string.Format((string)STRINGS.ONIACCESS.SCANNER.BACKWALL_NAME,
				element.name,
				(string)STRINGS.UI.TOOLS.GENERIC.NATURAL_BACKWALL_LABEL_TITLECASE);
		}

		/// <summary>
		/// Compact mass for glance speech. Grid.Mass is in kg.
		/// Under 0.1 kg: grams with one decimal ("52.3 g").
		/// 0.1 to 10 kg: kg with two decimals ("1.54 kg").
		/// Over 10 kg: whole kg ("688 kg").
		/// </summary>
		internal static string FormatGlanceMass(float kg) {
			if (kg < 0.1f) {
				float g = kg * 1000f;
				return $"{g:0}{STRINGS.UI.UNITSUFFIXES.MASS.GRAM}";
			}
			if (kg <= 10f)
				return $"{kg:0.00}{STRINGS.UI.UNITSUFFIXES.MASS.KILOGRAM}";
			if (kg < 5000f)
				return $"{kg:0}{STRINGS.UI.UNITSUFFIXES.MASS.KILOGRAM}";
			float t = kg / 1000f;
			return $"{t:0.#}{STRINGS.UI.UNITSUFFIXES.MASS.TONNE}";
		}
	}
}
