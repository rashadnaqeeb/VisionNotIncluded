using System.Collections.Generic;

namespace OniAccess.ConduitTracking {
	/// <summary>
	/// Formats a FlowTracker's direction counts as a speech string.
	/// </summary>
	public static class FlowSpeech {
		/// <summary>
		/// Null when nothing has been sampled yet. moved says whether
		/// anything left the conduit during the sampled window.
		/// </summary>
		public static string Format(FlowTracker tracker, int conduitIdx,
				bool currentlyEmpty, out bool moved) {
			var contentCounts = new Dictionary<Tag, int[]>();
			int samples = tracker.GetContentDirectionCounts(
				conduitIdx, contentCounts);
			moved = contentCounts.Count > 0;
			if (samples == 0) return null;

			if (contentCounts.Count == 0)
				return currentlyEmpty
					? STRINGS.ONIACCESS.GLANCE.FLOW_EMPTY
					: STRINGS.ONIACCESS.GLANCE.FLOW_NOT_FLOWING;

			var contents = new List<ContentGroup>(contentCounts.Count);
			foreach (var kvp in contentCounts) {
				int total = 0;
				var dirs = new List<DirectionPercent>(4);
				for (int d = FlowTracker.DirUp;
						d <= FlowTracker.DirRight; d++) {
					if (kvp.Value[d] > 0) {
						int pct = kvp.Value[d] * 100 / samples;
						dirs.Add(new DirectionPercent(d, pct));
						total += kvp.Value[d];
					}
				}
				dirs.Sort((a, b) => b.Percent - a.Percent);
				string name = kvp.Key.IsValid
					? TagManager.GetProperName(kvp.Key, stripLink: true)
					: null;
				contents.Add(new ContentGroup(name, total, dirs));
			}
			contents.Sort((a, b) => b.Total - a.Total);

			var tokens = new List<string>(contents.Count);
			foreach (var cg in contents) {
				var dirTokens = new List<string>(cg.Dirs.Count);
				foreach (var d in cg.Dirs)
					dirTokens.Add(string.Format(
						STRINGS.ONIACCESS.GLANCE.FLOW_DIRECTION_PERCENT,
						d.Percent, DirectionName(d.Dir)));
				string directions = string.Join(" ", dirTokens);
				tokens.Add(string.IsNullOrEmpty(cg.Name) ? directions
					: string.Format(
						STRINGS.ONIACCESS.GLANCE.FLOW_ELEMENT_DIRECTIONS,
						cg.Name, directions));
			}
			return string.Join(", ", tokens);
		}

		internal static string DirectionName(int dir) {
			switch (dir) {
				case FlowTracker.DirUp:
					return STRINGS.ONIACCESS.SCANNER.DIRECTION_UP;
				case FlowTracker.DirDown:
					return STRINGS.ONIACCESS.SCANNER.DIRECTION_DOWN;
				case FlowTracker.DirLeft:
					return STRINGS.ONIACCESS.SCANNER.DIRECTION_LEFT;
				case FlowTracker.DirRight:
					return STRINGS.ONIACCESS.SCANNER.DIRECTION_RIGHT;
				default: return "";
			}
		}

		private struct DirectionPercent {
			public int Dir;
			public int Percent;
			public DirectionPercent(int dir, int percent) {
				Dir = dir;
				Percent = percent;
			}
		}

		private struct ContentGroup {
			public string Name;
			public int Total;
			public List<DirectionPercent> Dirs;
			public ContentGroup(string name, int total,
					List<DirectionPercent> dirs) {
				Name = name;
				Total = total;
				Dirs = dirs;
			}
		}
	}
}
