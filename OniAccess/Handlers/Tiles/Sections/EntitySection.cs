using System.Collections.Generic;
using OniAccess.Util;

namespace OniAccess.Handlers.Tiles.Sections {
	/// <summary>
	/// Reads duplicants (ObjectLayer.Minion) and critters (CreatureBrain on
	/// ObjectLayer.Pickupables) at a cell.
	/// Traverses ObjectLayerListItem linked lists for critters.
	/// </summary>
	public class EntitySection: ICellSection {
		public IEnumerable<string> Read(int cell, CellContext ctx) {
			var tokens = new List<string>();
			ReadMinions(cell, tokens);
			ReadCritters(cell, tokens);
			return tokens;
		}

		private static void ReadMinions(int cell, List<string> tokens) {
			var go = Grid.Objects[cell, (int)ObjectLayer.Minion];
			if (go == null) return;
			var selectable = go.GetComponent<KSelectable>();
			if (selectable == null) return;
			string name = selectable.GetName();
			string suit = WornSuit.GetName(go.GetComponent<MinionIdentity>());
			tokens.Add(suit != null ? $"{name}, {suit}" : name);
		}

		private static void ReadCritters(int cell, List<string> tokens) {
			var go = Grid.Objects[cell, (int)ObjectLayer.Pickupables];
			if (go == null) return;

			var pickupable = go.GetComponent<Pickupable>();
			if (pickupable == null) return;

			var item = pickupable.objectLayerListItem;
			while (item != null) {
				if (item.gameObject.GetComponent<CreatureBrain>() != null) {
					var selectable = item.gameObject.GetComponent<KSelectable>();
					if (selectable != null) {
						string name = selectable.GetName();
						bool readyToShear = item.gameObject.GetSMI<IShearable>()?.IsFullyGrown() ?? false;
						tokens.Add(readyToShear
							? $"{name}, {(string)STRINGS.ONIACCESS.STATES.READY_TO_SHEAR}"
							: name);
					}
				}
				item = item.nextItem;
			}
		}

	}
}
