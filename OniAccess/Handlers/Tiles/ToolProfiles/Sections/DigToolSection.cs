using System.Collections.Generic;

namespace OniAccess.Handlers.Tiles.ToolProfiles.Sections {
	public class DigToolSection: ICellSection {
		public IEnumerable<string> Read(int cell, CellContext ctx) {
			var tokens = new List<string>();

			var element = Grid.Element[cell];
			if (element != null && element.IsSolid) {
				string perkId = GetRequiredPerkId(element.hardness);
				if (perkId != null) {
					int worldId = Grid.WorldIdx[cell];
					if (!MinionResume.AnyMinionHasPerk(perkId, worldId))
						tokens.Add((string)STRINGS.ONIACCESS.TOOLS.MISSING_DIG_SKILL);
				}
			}

			var digGo = Grid.Objects[cell, (int)ObjectLayer.DigPlacer];
			if (digGo != null && digGo.GetComponent<Diggable>() != null) {
				ctx.Claimed.Add(digGo);
				var pri = digGo.GetComponent<Prioritizable>();
				if (pri != null)
					tokens.Add(string.Format(
						(string)STRINGS.ONIACCESS.TOOLS.DIG_ORDER_PRIORITY,
						pri.GetMasterPriority().priority_value));
				else
					tokens.Add((string)STRINGS.ONIACCESS.TOOLS.DIG_ORDER);
			}

			return tokens;
		}

		private static string GetRequiredPerkId(byte hardness) {
			if (hardness == byte.MaxValue)
				return Db.Get().SkillPerks.CanDigUnobtanium.Id;
			if (hardness >= 251)
				return Db.Get().SkillPerks.CanDigRadioactiveMaterials.Id;
			if (hardness >= 200)
				return Db.Get().SkillPerks.CanDigSuperDuperHard.Id;
			if (hardness >= 150)
				return Db.Get().SkillPerks.CanDigNearlyImpenetrable.Id;
			if (hardness >= 50)
				return Db.Get().SkillPerks.CanDigVeryFirm.Id;
			return null;
		}
	}
}
