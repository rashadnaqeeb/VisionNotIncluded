using System.Collections.Generic;

namespace OniAccess.Handlers.Tiles.ToolProfiles.Sections {
	public class DigToolSection: ICellSection {
		public IEnumerable<string> Read(int cell, CellContext ctx) {
			var tokens = new List<string>();

			var digGo = Grid.Objects[cell, (int)ObjectLayer.DigPlacer];
			if (digGo != null && digGo.GetComponent<Diggable>() != null) {
				var pri = digGo.GetComponent<Prioritizable>();
				if (pri != null)
					tokens.Add(string.Format(
						(string)STRINGS.ONIACCESS.TOOLS.DIG_ORDER_PRIORITY,
						pri.GetMasterPriority().priority_value));
				else
					tokens.Add((string)STRINGS.ONIACCESS.TOOLS.DIG_ORDER);
			}

			if (!Grid.Foundation[cell]
				&& Grid.Objects[cell, (int)ObjectLayer.Building] == null
				&& Grid.Objects[cell, (int)ObjectLayer.FoundationTile] == null) {
				var element = Grid.Element[cell];
				if (element != null && element.IsSolid) {
					string hardness = GameUtil.GetHardnessString(element);
					if (!string.IsNullOrEmpty(hardness))
						tokens.Add(hardness);
				}
			}

			return tokens;
		}
	}
}
