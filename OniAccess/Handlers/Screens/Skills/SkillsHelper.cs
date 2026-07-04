using System;
using System.Collections.Generic;

using Database;
using Klei.AI;

using OniAccess.Speech;

namespace OniAccess.Handlers.Screens.Skills {
	internal static class SkillsHelper {
		// ========================================
		// DUPE QUERIES
		// ========================================

		internal static void ResolveDupe(
			IAssignableIdentity identity,
			out MinionIdentity minionIdentity,
			out StoredMinionIdentity storedIdentity) {
			if (identity is MinionAssignablesProxy proxy) {
				var go = proxy.GetTargetGameObject();
				minionIdentity = go.GetComponent<MinionIdentity>();
				storedIdentity = go.GetComponent<StoredMinionIdentity>();
			} else {
				minionIdentity = identity as MinionIdentity;
				storedIdentity = identity as StoredMinionIdentity;
			}
		}

		internal static MinionResume GetResume(IAssignableIdentity identity) {
			ResolveDupe(identity, out var minionIdentity, out _);
			return minionIdentity != null
				? minionIdentity.GetComponent<MinionResume>()
				: null;
		}

		internal static Tag GetDupeModel(IAssignableIdentity identity) {
			ResolveDupe(identity, out var minionIdentity, out var storedIdentity);
			if (minionIdentity != null)
				return minionIdentity.model;
			if (storedIdentity != null)
				return storedIdentity.model;
			return Tag.Invalid;
		}

		internal static bool IsStored(IAssignableIdentity identity) {
			ResolveDupe(identity, out var minionIdentity, out _);
			return minionIdentity == null;
		}

		// ========================================
		// SKILL QUERIES
		// ========================================

		internal static List<Skill> GetSkillsForModel(Tag model) {
			var result = new List<Skill>();
			foreach (var skill in Db.Get().Skills.resources) {
				if (skill.deprecated) continue;
				if (skill.requiredDuplicantModel != null &&
					skill.requiredDuplicantModel != model) continue;
				result.Add(skill);
			}
			result.Sort((a, b) => {
				int cmp = a.tier.CompareTo(b.tier);
				if (cmp != 0) return cmp;
				string groupA = Db.Get().SkillGroups.Get(a.skillGroup).Name;
				string groupB = Db.Get().SkillGroups.Get(b.skillGroup).Name;
				cmp = string.Compare(groupA, groupB, StringComparison.Ordinal);
				return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
			});
			return result;
		}

		internal static List<Skill> GetRootSkills(Tag model) {
			var roots = new List<Skill>();
			foreach (var skill in Db.Get().Skills.resources) {
				if (skill.deprecated) continue;
				if (skill.requiredDuplicantModel != null &&
					skill.requiredDuplicantModel != model) continue;
				if (skill.priorSkills == null || skill.priorSkills.Count == 0)
					roots.Add(skill);
			}
			roots.Sort((a, b) => {
				string groupA = Db.Get().SkillGroups.Get(a.skillGroup).Name;
				string groupB = Db.Get().SkillGroups.Get(b.skillGroup).Name;
				int cmp = string.Compare(groupA, groupB, StringComparison.Ordinal);
				return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
			});
			return roots;
		}

		internal static IReadOnlyList<Skill> GetChildren(Skill parent) {
			var children = new List<Skill>();
			foreach (var skill in Db.Get().Skills.resources) {
				if (skill.deprecated) continue;
				if (skill.priorSkills != null && skill.priorSkills.Contains(parent.Id))
					children.Add(skill);
			}
			return children;
		}

		internal static IReadOnlyList<Skill> GetParents(Skill child) {
			if (child.priorSkills == null || child.priorSkills.Count == 0)
				return Array.Empty<Skill>();
			var parents = new List<Skill>();
			foreach (var id in child.priorSkills) {
				var skill = Db.Get().Skills.TryGet(id);
				if (skill != null && !skill.deprecated)
					parents.Add(skill);
			}
			return parents;
		}

		// ========================================
		// BUCKET CLASSIFICATION
		// ========================================

		internal enum Bucket { DupeInfo, Available, Locked, Mastered, Boosters }

		internal static List<Skill> GetSkillsInBucket(
			Bucket bucket, IAssignableIdentity identity, Tag model) {
			var resume = GetResume(identity);
			var all = GetSkillsForModel(model);
			var result = new List<Skill>();

			foreach (var skill in all) {
				var skillBucket = ClassifySkill(skill, resume, identity);
				if (skillBucket == bucket)
					result.Add(skill);
			}
			return result;
		}

		private static Bucket ClassifySkill(
			Skill skill, MinionResume resume, IAssignableIdentity identity) {
			if (resume != null) {
				if (resume.HasMasteredSkill(skill.Id))
					return Bucket.Mastered;
				var conditions = resume.GetSkillMasteryConditions(skill.Id);
				if (resume.CanMasterSkill(conditions))
					return Bucket.Available;
				return Bucket.Locked;
			}
			// Stored minion: check MasteryBySkillID directly
			ResolveDupe(identity, out _, out var stored);
			if (stored != null && stored.HasMasteredSkill(skill.Id))
				return Bucket.Mastered;
			return Bucket.Locked;
		}

		// ========================================
		// LABEL BUILDERS
		// ========================================

		internal static string BuildDupeLabel(IAssignableIdentity identity) {
			var parts = new List<string>();
			parts.Add(identity.GetProperName());

			if (IsStored(identity)) {
				parts.Add(STRINGS.ONIACCESS.TABLE.STORED);
				return string.Join(", ", parts);
			}

			var resume = GetResume(identity);
			if (resume != null) {
				int points = resume.AvailableSkillpoints;
				parts.Add(string.Format(STRINGS.ONIACCESS.SKILLS.POINTS, points));

				float morale = Db.Get().Attributes.QualityOfLife
					.Lookup(resume).GetTotalValue();
				float expectation = Db.Get().Attributes.QualityOfLifeExpectation
					.Lookup(resume).GetTotalValue();
				parts.Add(string.Format(STRINGS.ONIACCESS.SKILLS.MORALE_OF,
					$"{morale:F0}", $"{expectation:F0}"));
			}
			return string.Join(", ", parts);
		}

		internal static string BuildSkillLabel(
			Skill skill, IAssignableIdentity identity) {
			var parts = new List<string>();
			parts.Add(skill.Name);

			var group = Db.Get().SkillGroups.Get(skill.skillGroup);
			if (group != null && !string.IsNullOrEmpty(group.Name))
				parts.Add(TextFilter.FilterForSpeech(group.Name));

			var resume = GetResume(identity);
			AddStatusDetail(parts, skill, resume, identity);

			if (!string.IsNullOrEmpty(skill.description))
				parts.Add(TextFilter.FilterForSpeech(skill.description));

			string perks = BuildPerksList(skill);
			if (perks != null)
				parts.Add(perks);

			var bucket = ClassifySkill(skill, resume, identity);
			if (bucket != Bucket.Mastered) {
				bool granted = resume != null && resume.HasBeenGrantedSkill(skill);
				if (!granted) {
					int moraleCost = skill.GetMoraleExpectation();
					if (moraleCost > 0)
						parts.Add(string.Format(
							STRINGS.ONIACCESS.SKILLS.MORALE_NEED, moraleCost));
				}
			}

			int masteryCount = CountMasters(skill.Id);
			if (masteryCount > 0)
				parts.Add(string.Format(
					STRINGS.ONIACCESS.SKILLS.MASTERED_BY, masteryCount));

			return string.Join(". ", parts);
		}

		private static void AddStatusDetail(
			List<string> parts, Skill skill,
			MinionResume resume, IAssignableIdentity identity) {
			if (resume != null) {
				if (resume.HasMasteredSkill(skill.Id)) {
					parts.Add(resume.HasBeenGrantedSkill(skill)
						? (string)STRINGS.ONIACCESS.SKILLS.GRANTED
						: (string)STRINGS.ONIACCESS.SKILLS.MASTERED);
					return;
				}
				var conditions = resume.GetSkillMasteryConditions(skill.Id);
				if (resume.CanMasterSkill(conditions)) {
					parts.Add(STRINGS.ONIACCESS.SKILLS.AVAILABLE);
					if (Array.Exists(conditions,
						c => c == MinionResume.SkillMasteryConditions.StressWarning))
						parts.Add(STRINGS.ONIACCESS.SKILLS.MORALE_DEFICIT);
					if (Array.Exists(conditions,
						c => c == MinionResume.SkillMasteryConditions.SkillAptitude))
						parts.Add(STRINGS.ONIACCESS.SKILLS.INTERESTED);
				} else {
					parts.Add(GetLockReason(skill, resume));
				}
				return;
			}
			// Stored
			ResolveDupe(identity, out _, out var stored);
			if (stored != null && stored.HasMasteredSkill(skill.Id)) {
				parts.Add(STRINGS.ONIACCESS.SKILLS.MASTERED);
				return;
			}
			parts.Add(STRINGS.ONIACCESS.SKILLS.LOCKED);
		}

		internal static string GetLockReason(Skill skill, MinionResume resume) {
			var conditions = resume.GetSkillMasteryConditions(skill.Id);
			if (Array.Exists(conditions,
				c => c == MinionResume.SkillMasteryConditions.UnableToLearn)) {
				string traitName = GetBlockingTraitName(skill, resume);
				return traitName != null
					? string.Format(STRINGS.ONIACCESS.SKILLS.BLOCKED_BY, traitName)
					: (string)STRINGS.ONIACCESS.SKILLS.CANNOT_LEARN;
			}
			if (Array.Exists(conditions,
				c => c == MinionResume.SkillMasteryConditions.MissingPreviousSkill)) {
				var missing = GetMissingPrereqs(skill, resume);
				return string.Format(STRINGS.ONIACCESS.RESEARCH.NEEDS_FMT, string.Join(", ", missing));
			}
			if (Array.Exists(conditions,
				c => c == MinionResume.SkillMasteryConditions.NeedsSkillPoints))
				return STRINGS.ONIACCESS.SKILLS.NO_SKILL_POINTS;
			return STRINGS.ONIACCESS.SKILLS.LOCKED;
		}

		private static string GetBlockingTraitName(Skill skill, MinionResume resume) {
			var group = Db.Get().SkillGroups.Get(skill.skillGroup);
			if (group == null || string.IsNullOrEmpty(group.choreGroupID))
				return null;
			var traits = resume.GetComponent<Klei.AI.Traits>();
			if (traits == null) return null;
			traits.IsChoreGroupDisabled(group.choreGroupID, out var disablingTrait);
			return disablingTrait?.Name;
		}

		private static List<string> GetMissingPrereqs(Skill skill, MinionResume resume) {
			var missing = new List<string>();
			foreach (var prereqId in skill.priorSkills) {
				if (!resume.HasMasteredSkill(prereqId)) {
					var prereq = Db.Get().Skills.TryGet(prereqId);
					missing.Add(prereq != null ? prereq.Name : prereqId);
				}
			}
			return missing;
		}

		internal static string BuildPerksList(Skill skill) {
			var perkParts = new List<string>();
			foreach (var perk in skill.perks) {
				if (!Game.IsCorrectDlcActiveForCurrentSave(perk)) continue;
				string desc = SkillPerk.GetDescription(perk.Id);
				if (!string.IsNullOrEmpty(desc))
					perkParts.Add(TextFilter.FilterForSpeech(desc));
			}
			return perkParts.Count > 0 ? string.Join(", ", perkParts) : null;
		}

		internal static string BuildPrereqList(Skill skill) {
			if (skill.priorSkills == null || skill.priorSkills.Count == 0)
				return null;
			var names = new List<string>();
			foreach (var prereqId in skill.priorSkills) {
				var prereq = Db.Get().Skills.TryGet(prereqId);
				names.Add(prereq != null ? prereq.Name : prereqId);
			}
			return string.Format(STRINGS.ONIACCESS.RESEARCH.NEEDS_FMT, string.Join(", ", names));
		}

		internal static int CountMasters(string skillId) {
			int count = 0;
			foreach (var mi in Components.LiveMinionIdentities.Items) {
				var resume = mi.GetComponent<MinionResume>();
				if (resume != null && resume.HasMasteredSkill(skillId))
					count++;
			}
			return count;
		}

		// ========================================
		// DUPE INFO LABELS
		// ========================================

		internal static List<string> BuildDupeInfoLabels(IAssignableIdentity identity) {
			var labels = new List<string>();
			var resume = GetResume(identity);

			if (resume == null) {
				labels.Add(string.Format(STRINGS.ONIACCESS.SKILLS.NAME_STORED,
					identity.GetProperName()));
				return labels;
			}

			// Name and points
			labels.Add(string.Format(STRINGS.ONIACCESS.SKILLS.NAME_POINTS,
				identity.GetProperName(),
				string.Format(STRINGS.ONIACCESS.SKILLS.POINTS, resume.AvailableSkillpoints)));

			// Interests (skill groups with aptitude)
			labels.Add(BuildInterestsLabel(resume));

			// Morale breakdown
			var moraleAttr = Db.Get().Attributes.QualityOfLife.Lookup(resume);
			labels.Add(BuildModifierBreakdown(
				STRINGS.UI.SKILLS_SCREEN.MORALE, moraleAttr));

			// Morale need breakdown
			var expectAttr = Db.Get().Attributes.QualityOfLifeExpectation.Lookup(resume);
			labels.Add(BuildModifierBreakdown(
				STRINGS.UI.SKILLS_SCREEN.MORALE_EXPECTATION, expectAttr));

			// XP progress
			float xp = resume.TotalExperienceGained;
			float prevBar = MinionResume.CalculatePreviousExperienceBar(
				resume.TotalSkillPointsGained);
			float nextBar = MinionResume.CalculateNextExperienceBar(
				resume.TotalSkillPointsGained);
			labels.Add(string.Format(STRINGS.ONIACCESS.SKILLS.XP_PROGRESS,
				$"{xp - prevBar:F0}", $"{nextBar - prevBar:F0}"));

			// Current hat
			string hatName = GetCurrentHatName(resume);
			labels.Add(string.Format(STRINGS.ONIACCESS.SKILLS.HAT_LABEL,
				hatName ?? (string)STRINGS.ONIACCESS.SKILLS.NO_HAT));

			return labels;
		}

		private static string BuildInterestsLabel(MinionResume resume) {
			var names = new List<string>();
			foreach (var group in Db.Get().SkillGroups.resources) {
				if (resume.AptitudeBySkillGroup.TryGetValue(
					new HashedString(group.Id), out float aptitude) && aptitude > 0f)
					names.Add(group.Name);
			}
			if (names.Count == 0)
				return STRINGS.ONIACCESS.SKILLS.NO_INTERESTS;
			return string.Format(STRINGS.ONIACCESS.SKILLS.INTERESTS,
				string.Join(", ", names));
		}

		private static string BuildModifierBreakdown(
			string header, AttributeInstance attr) {
			string total = $"{attr.GetTotalValue():F0}";
			var parts = new List<string>();
			parts.Add(string.Format(STRINGS.ONIACCESS.SKILLS.HEADER_TOTAL,
				TextFilter.FilterForSpeech(header), total));
			for (int i = 0; i < attr.Modifiers.Count; i++) {
				var mod = attr.Modifiers[i];
				float val = mod.Value;
				if (val == 0f) continue;
				string sign = val > 0 ? "+" : "";
				parts.Add(string.Format(STRINGS.ONIACCESS.SKILLS.MODIFIER_LINE,
					TextFilter.FilterForSpeech(mod.GetDescription()), sign, $"{val:F0}"));
			}
			// ". " so the Alt+Up/Down reviewer breaks each modifier onto its own line;
			// the total and every contributor step one at a time instead of one long blob.
			return string.Join(". ", parts);
		}

		// ========================================
		// HAT QUERIES
		// ========================================

		internal static string GetCurrentHatName(MinionResume resume) {
			string hatId = string.IsNullOrEmpty(resume.TargetHat)
				? resume.CurrentHat
				: resume.TargetHat;
			if (string.IsNullOrEmpty(hatId)) return null;
			foreach (var skill in Db.Get().Skills.resources) {
				if (skill.hat == hatId)
					return skill.Name;
			}
			return hatId;
		}

		internal static List<HatEntry> GetAvailableHats(MinionResume resume) {
			var hats = new List<HatEntry>();
			hats.Add(new HatEntry(STRINGS.ONIACCESS.STATES.NONE, ""));
			foreach (var hatInfo in resume.GetAllHats()) {
				hats.Add(new HatEntry(hatInfo.Source, hatInfo.Hat));
			}
			return hats;
		}

		internal struct HatEntry {
			internal readonly string Name;
			internal readonly string HatId;
			internal HatEntry(string name, string hatId) {
				Name = name;
				HatId = hatId;
			}
		}

		// ========================================
		// BOOSTER QUERIES (DLC3-safe)
		// ========================================

		internal static bool IsBionic(IAssignableIdentity identity) {
			if (!DlcManager.IsContentSubscribed(DlcManager.DLC3_ID))
				return false;
			try {
				var model = GetDupeModel(identity);
				return model == GameTags.Minions.Models.Bionic;
			} catch (Exception ex) {
				Util.Log.Warn($"SkillsHelper.IsBionic: {ex.Message}");
				return false;
			}
		}

		internal static string BuildSlotSummary(MinionIdentity minionIdentity) {
			try {
				var smi = minionIdentity.GetSMI<BionicUpgradesMonitor.Instance>();
				if (smi == null) return null;
				return string.Format(STRINGS.ONIACCESS.SKILLS.BOOSTER_SLOTS,
					smi.AssignedSlotCount, smi.UnlockedSlotCount);
			} catch (Exception ex) {
				Util.Log.Warn($"SkillsHelper.BuildSlotSummary: {ex.Message}");
				return null;
			}
		}

		internal static List<BoosterEntry> GetBoosterEntries(MinionIdentity minionIdentity) {
			var entries = new List<BoosterEntry>();
			try {
				var smi = minionIdentity.GetSMI<BionicUpgradesMonitor.Instance>();
				if (smi == null) return entries;

				// Count assigned by prefab tag (matches game's RefreshBoosters)
				var assignedCounts = new Dictionary<Tag, int>();
				foreach (var slot in smi.upgradeComponentSlots) {
					if (slot.assignedUpgradeComponent != null) {
						var tag = slot.assignedUpgradeComponent.PrefabID();
						assignedCounts.TryGetValue(tag, out int c);
						assignedCounts[tag] = c + 1;
					}
				}

				// Gather all booster types from assigned + known prefabs
				var boosterTags = new HashSet<Tag>();
				foreach (var kv in assignedCounts)
					boosterTags.Add(kv.Key);
				foreach (var tag in Assets.GetPrefabTagsWithComponent<BionicUpgradeComponent>())
					boosterTags.Add(tag);

				// Availability: unassigned pickupables in this dupe's world
				var worldId = minionIdentity.GetMyWorldId();
				var worldInventory = ClusterManager.Instance.GetWorld(worldId).worldInventory;

				foreach (var tag in boosterTags) {
					assignedCounts.TryGetValue(tag, out int assignedCount);
					var prefab = Assets.GetPrefab(tag);
					if (prefab == null) continue;
					string name = prefab.GetProperName();

					// Match game: filter to unassigned pickupables
					int available = 0;
					var pickupables = worldInventory.CreatePickupablesList(tag);
					if (pickupables != null) {
						foreach (var p in pickupables) {
							if (p.GetComponent<Assignable>().assignee == null)
								available++;
						}
					}

					string desc = "";
					var prefabTag = prefab.PrefabID();
					if (BionicUpgradeComponentConfig.UpgradesData.TryGetValue(
						prefabTag, out var upgradeData))
						desc = upgradeData.stateMachineDescription ?? "";

					entries.Add(new BoosterEntry(tag, name, assignedCount,
						available, desc));
				}
			} catch (Exception ex) {
				Util.Log.Warn($"SkillsHelper.GetBoosterEntries: {ex.Message}");
			}
			return entries;
		}

		internal static bool TryAssignBooster(
			MinionIdentity minionIdentity, Tag boosterTag) {
			try {
				var smi = minionIdentity.GetSMI<BionicUpgradesMonitor.Instance>();
				if (smi == null) return false;

				// Find a truly empty slot (not locked, not assigned, not mid-ejection)
				bool hasEmptySlot = false;
				foreach (var slot in smi.upgradeComponentSlots) {
					if (!slot.IsLocked && !slot.HasUpgradeComponentAssigned
						&& !slot.HasUpgradeInstalled) {
						hasEmptySlot = true;
						break;
					}
				}
				if (!hasEmptySlot) return false;

				// Find an available booster item (matches game's FindAvailableBoosterOfType)
				var worldId = minionIdentity.GetMyWorldId();
				var worldInventory = ClusterManager.Instance.GetWorld(worldId).worldInventory;
				var items = worldInventory.CreatePickupablesList(boosterTag);
				if (items == null) return false;
				foreach (var item in items) {
					var comp = item.GetComponent<BionicUpgradeComponent>();
					if (comp != null && comp.assignee == null) {
						comp.Assign(minionIdentity);
						return true;
					}
				}
				return false;
			} catch (Exception ex) {
				Util.Log.Warn($"SkillsHelper.TryAssignBooster: {ex.Message}");
				return false;
			}
		}

		internal static bool TryUnassignBooster(
			MinionIdentity minionIdentity, Tag boosterTag) {
			try {
				var smi = minionIdentity.GetSMI<BionicUpgradesMonitor.Instance>();
				if (smi == null) return false;
				var slots = smi.upgradeComponentSlots;
				// First pass: installed+matching (matches game's DecrementBoosterAssignment)
				for (int i = slots.Length - 1; i >= 0; i--) {
					var slot = slots[i];
					if (slot.assignedUpgradeComponent != null &&
						slot.assignedUpgradeComponent.PrefabID() == boosterTag &&
						slot.HasUpgradeInstalled &&
						slot.AssignedUpgradeMatchesInstalledUpgrade) {
						slot.GetAssignableSlotInstance().Unassign();
						return true;
					}
				}
				// Second pass: any assigned booster of this type
				for (int i = slots.Length - 1; i >= 0; i--) {
					var slot = slots[i];
					if (slot.assignedUpgradeComponent != null &&
						slot.assignedUpgradeComponent.PrefabID() == boosterTag) {
						slot.GetAssignableSlotInstance().Unassign();
						return true;
					}
				}
				return false;
			} catch (Exception ex) {
				Util.Log.Warn($"SkillsHelper.TryUnassignBooster: {ex.Message}");
				return false;
			}
		}

		internal struct BoosterEntry {
			internal readonly Tag Tag;
			internal readonly string Name;
			internal readonly int AssignedCount;
			internal readonly int AvailableCount;
			internal readonly string Description;
			internal BoosterEntry(Tag tag, string name, int assigned,
				int available, string desc) {
				Tag = tag;
				Name = name;
				AssignedCount = assigned;
				AvailableCount = available;
				Description = desc;
			}
		}

		internal static string BuildBoosterLabel(BoosterEntry entry) {
			var parts = new List<string>();
			parts.Add(entry.Name);
			parts.Add(string.Format(STRINGS.ONIACCESS.SKILLS.ASSIGNED,
				entry.AssignedCount));
			parts.Add(string.Format(STRINGS.ONIACCESS.SKILLS.BOOSTER_AVAILABLE,
				entry.AvailableCount));
			if (!string.IsNullOrEmpty(entry.Description))
				parts.Add(TextFilter.FilterForSpeech(entry.Description));
			return string.Join(". ", parts);
		}

		// ========================================
		// ACTIONS
		// ========================================

		internal static void TryLearnSkill(
			Skill skill, IAssignableIdentity identity, KScreen screen) {
			var resume = GetResume(identity);
			if (resume == null) {
				PlayRejectSound();
				SpeechPipeline.SpeakInterrupt(STRINGS.ONIACCESS.SKILLS.CANNOT_LEARN);
				return;
			}

			if (resume.HasMasteredSkill(skill.Id)) {
				PlayRejectSound();
				SpeechPipeline.SpeakInterrupt(string.Format(
					STRINGS.ONIACCESS.SKILLS.NAME_STATUS,
					skill.Name, STRINGS.ONIACCESS.SKILLS.MASTERED));
				return;
			}

			var conditions = resume.GetSkillMasteryConditions(skill.Id);
			if (!resume.CanMasterSkill(conditions)) {
				PlayRejectSound();
				string reason = GetLockReason(skill, resume);
				SpeechPipeline.SpeakInterrupt(string.Format(
					STRINGS.ONIACCESS.SKILLS.NAME_STATUS, skill.Name, reason));
				return;
			}

			resume.MasterSkill(skill.Id);
			var skillsScreen = screen as SkillsScreen;
			if (skillsScreen != null)
				skillsScreen.RefreshAll();
			PlayClickSound();
			SpeechPipeline.SpeakInterrupt(string.Format(
				STRINGS.ONIACCESS.SKILLS.LEARNED, skill.Name));
		}

		// ========================================
		// SOUNDS
		// ========================================

		internal static void PlayClickSound() {
			BaseScreenHandler.PlaySound("HUD_Click_Open");
		}

		internal static void PlayRejectSound() {
			BaseScreenHandler.PlaySound("Negative");
		}
	}
}
