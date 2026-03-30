using System.Collections.Generic;

using OniAccess.Speech;

namespace OniAccess.Handlers.Screens.Research {
	/// <summary>
	/// Shared helpers for building tech speech labels. Used by all three
	/// research tabs. All data is read live from game singletons on every call.
	/// </summary>
	internal static class ResearchHelper {
		/// <summary>
		/// Build a full spoken label for a tech in Browse or Tree context.
		/// Format: "Name, state. cost. unlocks: items. description"
		/// </summary>
		internal static string BuildTechLabel(Tech tech) {
			var parts = new List<string>();
			parts.Add(tech.Name);

			if (tech.IsComplete())
				parts.Add(STRINGS.ONIACCESS.RESEARCH.COMPLETED);
			else if (tech.ArePrerequisitesComplete())
				parts.Add(STRINGS.ONIACCESS.RESEARCH.AVAILABLE);
			else {
				parts.Add(STRINGS.ONIACCESS.RESEARCH.LOCKED);
				string prereqs = BuildPrereqList(tech);
				if (prereqs != null)
					parts.Add(prereqs);
			}

			// Show progress if partially researched, otherwise show total cost
			var ti = !tech.IsComplete() ? global::Research.Instance.Get(tech) : null;
			string progress = ti != null && HasProgress(ti) ? BuildProgressString(ti) : null;
			if (progress != null)
				parts.Add(progress);
			else {
				string cost = BuildCostString(tech);
				if (cost != null)
					parts.Add(cost);
			}

			string unlocks = BuildUnlocksList(tech);
			if (unlocks != null)
				parts.Add(unlocks);

			string desc = tech.desc;
			if (!string.IsNullOrEmpty(desc))
				parts.Add(TextFilter.FilterForSpeech(desc));

			return string.Join(". ", parts);
		}

		/// <summary>
		/// Build a label for a queued tech, including live progress.
		/// Format: "Name, active/queued. progress per type. unlocks. description"
		/// </summary>
		internal static string BuildQueuedTechLabel(TechInstance ti, bool isActive) {
			var parts = new List<string>();
			parts.Add(ti.tech.Name);

			if (isActive)
				parts.Add(STRINGS.ONIACCESS.RESEARCH.ACTIVE);

			string progress = BuildProgressString(ti);
			if (progress != null)
				parts.Add(progress);

			string unlocks = BuildUnlocksList(ti.tech);
			if (unlocks != null)
				parts.Add(unlocks);

			string desc = ti.tech.desc;
			if (!string.IsNullOrEmpty(desc))
				parts.Add(TextFilter.FilterForSpeech(desc));

			return string.Join(". ", parts);
		}

		/// <summary>
		/// Build cost string: "15 Alpha Research, 30 Beta Research"
		/// </summary>
		internal static string BuildCostString(Tech tech) {
			var costParts = new List<string>();
			foreach (var kv in tech.costsByResearchTypeID) {
				if (kv.Value <= 0f) continue;
				string typeName = GetResearchTypeName(kv.Key);
				costParts.Add($"{kv.Value:F0} {typeName}");
			}
			return costParts.Count > 0 ? string.Join(", ", costParts) : null;
		}

		/// <summary>
		/// Build progress string: "15 of 50 Alpha Research, 0 of 30 Beta Research"
		/// </summary>
		internal static string BuildProgressString(TechInstance ti) {
			var progressParts = new List<string>();
			foreach (var kv in ti.tech.costsByResearchTypeID) {
				if (kv.Value <= 0f) continue;
				string typeName = GetResearchTypeName(kv.Key);
				float current = 0f;
				ti.progressInventory.PointsByTypeID.TryGetValue(kv.Key, out current);
				progressParts.Add(string.Format(STRINGS.ONIACCESS.RESEARCH.PROGRESS_ENTRY,
				$"{current:F0}", $"{kv.Value:F0}", typeName));
			}
			return progressParts.Count > 0 ? string.Join(", ", progressParts) : null;
		}

		/// <summary>
		/// Build prerequisite list: "needs Tech A completed, Tech B"
		/// </summary>
		internal static string BuildPrereqList(Tech tech) {
			if (tech.requiredTech == null || tech.requiredTech.Count == 0)
				return null;
			var prereqParts = new List<string>();
			foreach (var prereq in tech.requiredTech) {
				string entry = prereq.Name;
				if (prereq.IsComplete())
					entry = string.Format(STRINGS.ONIACCESS.RESEARCH.PREREQ_COMPLETED, entry);
				prereqParts.Add(entry);
			}
			return string.Format(STRINGS.ONIACCESS.RESEARCH.NEEDS_FMT, string.Join(", ", prereqParts));
		}

		/// <summary>
		/// Build unlocks list: "unlocks Gas Pipe, Gas Pump"
		/// </summary>
		internal static string BuildUnlocksList(Tech tech) {
			if (tech.unlockedItems == null || tech.unlockedItems.Count == 0)
				return null;
			var names = new List<string>();
			foreach (var item in tech.unlockedItems)
				names.Add(item.Name);
			return string.Format(STRINGS.ONIACCESS.RESEARCH.UNLOCKS_FMT, string.Join(", ", names));
		}

		/// <summary>
		/// Build the global research point inventory string.
		/// </summary>
		internal static string BuildPointInventoryString() {
			if (!global::Research.Instance.UseGlobalPointInventory)
				return null;
			var inv = global::Research.Instance.globalPointInventory;
			if (inv == null) return null;
			var parts = new List<string>();
			foreach (var kv in inv.PointsByTypeID) {
				if (kv.Value <= 0f) continue;
				string typeName = GetResearchTypeName(kv.Key);
				parts.Add($"{kv.Value:F0} {typeName}");
			}
			return parts.Count > 0
				? string.Format(STRINGS.ONIACCESS.RESEARCH.BANKED_POINTS_FMT, string.Join(", ", parts))
				: null;
		}

		/// <summary>
		/// Get all techs sorted by tier ascending, then name.
		/// </summary>
		internal static List<Tech> GetAllTechs() {
			var result = new List<Tech>(Db.Get().Techs.resources);
			result.Sort((a, b) => {
				int cmp = a.tier.CompareTo(b.tier);
				return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, System.StringComparison.Ordinal);
			});
			return result;
		}

		/// <summary>
		/// Get techs matching a bucket filter, sorted by tier then name.
		/// </summary>
		internal static List<Tech> GetTechsInBucket(int bucket) {
			var all = GetAllTechs();
			var result = new List<Tech>();
			foreach (var tech in all) {
				bool match = bucket switch {
					0 => !tech.IsComplete() && tech.ArePrerequisitesComplete(),
					1 => !tech.IsComplete() && !tech.ArePrerequisitesComplete(),
					2 => tech.IsComplete(),
					_ => false,
				};
				if (match) result.Add(tech);
			}
			return result;
		}

		/// <summary>
		/// Get root techs (no prerequisites), ordered by tier then name.
		/// </summary>
		internal static List<Tech> GetRootTechs() {
			var roots = new List<Tech>();
			foreach (var tech in Db.Get().Techs.resources) {
				if (tech.requiredTech == null || tech.requiredTech.Count == 0)
					roots.Add(tech);
			}
			roots.Sort((a, b) => {
				int cmp = a.tier.CompareTo(b.tier);
				return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, System.StringComparison.Ordinal);
			});
			return roots;
		}

		internal static string GetBucketName(int bucket) {
			return bucket switch {
				0 => (string)STRINGS.ONIACCESS.RESEARCH.BUCKET_AVAILABLE,
				1 => (string)STRINGS.ONIACCESS.RESEARCH.BUCKET_LOCKED,
				2 => (string)STRINGS.ONIACCESS.RESEARCH.BUCKET_COMPLETED,
				_ => "",
			};
		}

		/// <summary>
		/// Build a search-only label containing tech name plus each TechItem's
		/// name and description — the same tooltip text the player has seen.
		/// Never spoken; only used for type-ahead matching.
		/// </summary>
		internal static string BuildSearchLabel(Tech tech) {
			var parts = new List<string>();
			parts.Add(tech.Name);
			foreach (var item in tech.unlockedItems) {
				parts.Add(item.Name);
				if (!string.IsNullOrEmpty(item.description))
					parts.Add(item.description);
			}
			return string.Join(" ", parts);
		}

		static bool HasProgress(TechInstance ti) {
			foreach (var kv in ti.progressInventory.PointsByTypeID) {
				if (kv.Value > 0f) return true;
			}
			return false;
		}

		internal static void PlayClickSound() {
			BaseScreenHandler.PlaySound("HUD_Click_Open");
		}

		internal static void PlayRejectSound() {
			BaseScreenHandler.PlaySound("Negative");
		}

		static string GetResearchTypeName(string typeId) {
			var researchType = global::Research.Instance.GetResearchType(typeId);
			return researchType != null ? researchType.name : typeId;
		}
	}
}
