using System;
using System.Collections.Generic;

using Database;

using OniAccess.Navigation;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Skills {
	/// <summary>
	/// Tab 2: tree with categories (Dupe Info, Available, Locked, Mastered, Boosters).
	/// Level 0 = categories, level 1 = items within category, level 2 = hat list
	/// (only under the Dupe Info hat entry).
	///
	/// Type-ahead searches the learnable skills (tagged "skill"); the dupe-info,
	/// booster, and hat rows are excluded. Space jumps to the tree tab for the
	/// current skill; +/- assign and unassign boosters.
	/// </summary>
	internal class SkillsTab: NavTreeHandler, IScreenTab {
		private readonly SkillsScreenHandler _parent;

		// Category indices
		private const int CAT_DUPE_INFO = 0;
		private const int CAT_AVAILABLE = 1;
		private const int CAT_LOCKED = 2;
		private const int CAT_MASTERED = 3;
		private const int CAT_BOOSTERS = 4;

		// Dupe Info item indices
		private const int INFO_HAT = 5;

		internal SkillsTab(SkillsScreenHandler parent) : base(screen: null) {
			_parent = parent;
			// Search the learnable skills only.
			Nav.SearchFilter = n => n.RoleKey == "skill";
		}

		public string TabName => (string)STRINGS.ONIACCESS.SKILLS.SKILLS_TAB;

		public override string DisplayName => TabName;

		protected override int StartDepth => 1;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }
			= new List<HelpEntry>(DrillNavHelpEntries) {
				new HelpEntry("Tab/Shift+Tab", STRINGS.ONIACCESS.HELP.SWITCH_PANEL),
				new HelpEntry("Space", STRINGS.ONIACCESS.SKILLS.JUMP_TO_TREE_HELP),
				new HelpEntry("Enter", STRINGS.ONIACCESS.SKILLS.LEARN_HELP),
				new HelpEntry("+/-", STRINGS.ONIACCESS.SKILLS.BOOSTER_HELP),
			}.AsReadOnly();

		// ========================================
		// IScreenTab
		// ========================================

		public void OnTabActivated(bool announce) {
			ResetState();
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			if (ItemCount > 0)
				AnnounceCurrent(interrupt: false);
		}

		public void OnTabDeactivated() {
			_search.Clear();
		}

		public bool HandleInput() {
			// Space: jump to tree tab for current skill
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Space) && Nav.Depth == 1) {
				var skill = GetCurrentSkill();
				if (skill != null) {
					_parent.JumpToTreeTab(skill);
					return true;
				}
			}

			// Plus/Minus for booster assign/unassign
			if (Nav.Depth == 1 && Nav.Path[0] == GetBoosterCategoryIndex()) {
				if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Equals) ||
					UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadPlus)) {
					HandleBoosterAssign();
					return true;
				}
				if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Minus) ||
					UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadMinus)) {
					HandleBoosterUnassign();
					return true;
				}
			}

			return base.Tick();
		}

		public new bool HandleKeyDown(KButtonEvent e) {
			return base.HandleKeyDown(e);
		}

		// ========================================
		// TREE CONSTRUCTION
		// ========================================

		protected override IReadOnlyList<NavItem> BuildRoots() {
			int count = GetCategoryCount();
			var roots = new List<NavItem>(count);
			for (int c = 0; c < count; c++) {
				int cat = c;
				roots.Add(new MenuNode(
					() => GetCategoryName(cat),
					children: () => BuildLevel1(cat)));
			}
			return roots;
		}

		private IReadOnlyList<NavItem> BuildLevel1(int cat) {
			var identity = _parent.SelectedDupe;
			if (identity == null) return System.Array.Empty<NavItem>();
			var model = SkillsHelper.GetDupeModel(identity);

			if (cat == CAT_DUPE_INFO) return BuildDupeInfoNodes(identity);
			if (cat == GetBoosterCategoryIndex()) return BuildBoosterNodes();
			return BuildSkillNodes(cat, identity, model);
		}

		private IReadOnlyList<NavItem> BuildSkillNodes(int cat, IAssignableIdentity identity, Tag model) {
			var skills = SkillsHelper.GetSkillsInBucket(CategoryToBucket(cat), identity, model);
			var list = new List<NavItem>(skills.Count);
			foreach (var s in skills) {
				var skill = s;
				list.Add(new MenuNode(
					() => SkillsHelper.BuildSkillLabel(skill, _parent.SelectedDupe),
					activate: () => { TryLearnSkill(skill); return true; },
					roleKey: "skill",
					searchText: () => skill.Name));
			}
			return list;
		}

		private IReadOnlyList<NavItem> BuildDupeInfoNodes(IAssignableIdentity identity) {
			var labels = SkillsHelper.BuildDupeInfoLabels(identity);
			var list = new List<NavItem>(labels.Count);
			for (int i = 0; i < labels.Count; i++) {
				int idx = i;
				if (idx == INFO_HAT && labels.Count > INFO_HAT) {
					// Hat row drills into the hat list.
					list.Add(new MenuNode(
						() => RowLabel(idx),
						children: () => BuildHatNodes()));
				} else {
					// Info-only row.
					list.Add(new MenuNode(() => RowLabel(idx)));
				}
			}
			return list;
		}

		private string RowLabel(int idx) {
			var labels = SkillsHelper.BuildDupeInfoLabels(_parent.SelectedDupe);
			return idx >= 0 && idx < labels.Count ? labels[idx] : null;
		}

		private IReadOnlyList<NavItem> BuildHatNodes() {
			var resume = SkillsHelper.GetResume(_parent.SelectedDupe);
			if (resume == null) return System.Array.Empty<NavItem>();
			var hats = SkillsHelper.GetAvailableHats(resume);
			var list = new List<NavItem>(hats.Count);
			for (int i = 0; i < hats.Count; i++) {
				int hatIdx = i;
				list.Add(new MenuNode(
					() => HatName(hatIdx),
					activate: () => { SelectHat(hatIdx); return true; }));
			}
			return list;
		}

		private string HatName(int hatIdx) {
			var resume = SkillsHelper.GetResume(_parent.SelectedDupe);
			if (resume == null) return null;
			var hats = SkillsHelper.GetAvailableHats(resume);
			return hatIdx >= 0 && hatIdx < hats.Count ? hats[hatIdx].Name : null;
		}

		private IReadOnlyList<NavItem> BuildBoosterNodes() {
			SkillsHelper.ResolveDupe(_parent.SelectedDupe, out var minionIdentity, out _);
			if (minionIdentity == null) return System.Array.Empty<NavItem>();
			var list = new List<NavItem> {
				// Slot summary; Enter on any booster row speaks the hint.
				new MenuNode(
					() => SkillsHelper.BuildSlotSummary(minionIdentity),
					activate: () => { SpeechPipeline.SpeakInterrupt(STRINGS.ONIACCESS.SKILLS.BOOSTER_HINT); return true; }),
			};
			var entries = SkillsHelper.GetBoosterEntries(minionIdentity);
			foreach (var e in entries) {
				var entry = e;
				list.Add(new MenuNode(
					() => SkillsHelper.BuildBoosterLabel(entry),
					activate: () => { SpeechPipeline.SpeakInterrupt(STRINGS.ONIACCESS.SKILLS.BOOSTER_HINT); return true; }));
			}
			return list;
		}

		// ========================================
		// Categories
		// ========================================

		private int GetCategoryCount() {
			return ShowBoosters() ? 5 : 4;
		}

		private string GetCategoryName(int cat) {
			switch (cat) {
				case CAT_DUPE_INFO: return STRINGS.ONIACCESS.SKILLS.BUCKET_DUPE_INFO;
				case CAT_AVAILABLE: return STRINGS.ONIACCESS.RESEARCH.BUCKET_AVAILABLE;
				case CAT_LOCKED: return STRINGS.ONIACCESS.RESEARCH.BUCKET_LOCKED;
				case CAT_MASTERED: return STRINGS.ONIACCESS.SKILLS.BUCKET_MASTERED;
				case CAT_BOOSTERS: return STRINGS.ONIACCESS.SKILLS.BUCKET_BOOSTERS;
				default: return "";
			}
		}

		private int GetBoosterCategoryIndex() {
			return ShowBoosters() ? CAT_BOOSTERS : -1;
		}

		private bool ShowBoosters() {
			var identity = _parent.SelectedDupe;
			if (identity == null) return false;
			if (SkillsHelper.IsStored(identity)) return false;
			return SkillsHelper.IsBionic(identity);
		}

		// ========================================
		// Hat selection
		// ========================================

		private void SelectHat(int hatIdx) {
			var resume = SkillsHelper.GetResume(_parent.SelectedDupe);
			if (resume == null) {
				SkillsHelper.PlayRejectSound();
				return;
			}
			var hats = SkillsHelper.GetAvailableHats(resume);
			if (hatIdx < 0 || hatIdx >= hats.Count) return;
			var hat = hats[hatIdx];
			if (string.IsNullOrEmpty(hat.HatId)) {
				// "None" — remove hat immediately
				resume.SetHats(resume.CurrentHat, null);
				resume.ApplyTargetHat();
			} else {
				// Actual hat — set target and queue the chore
				resume.SetHats(resume.CurrentHat, hat.HatId);
				if (resume.OwnsHat(hat.HatId))
					new PutOnHatChore(resume, Db.Get().ChoreTypes.SwitchHat);
			}
			var skillsScreen = _parent.Screen as SkillsScreen;
			if (skillsScreen != null)
				skillsScreen.RefreshAll();
			SkillsHelper.PlayClickSound();
			string msg = string.IsNullOrEmpty(hat.HatId)
				? string.Format(STRINGS.ONIACCESS.SKILLS.HAT_SELECTED, hat.Name)
				: string.Format(STRINGS.ONIACCESS.SKILLS.HAT_QUEUED, hat.Name);
			SpeechPipeline.SpeakInterrupt(msg);
		}

		// ========================================
		// Skill actions
		// ========================================

		private Skill GetCurrentSkill() {
			if (Nav.Depth < 1) return null;
			int cat = Nav.Path[0];
			if (cat == CAT_DUPE_INFO || cat == GetBoosterCategoryIndex()) return null;
			return GetSkillAtLevel1(cat, Nav.Path[1]);
		}

		private Skill GetSkillAtLevel1(int cat, int idx) {
			var identity = _parent.SelectedDupe;
			if (identity == null) return null;
			var model = SkillsHelper.GetDupeModel(identity);
			var skills = SkillsHelper.GetSkillsInBucket(
				CategoryToBucket(cat), identity, model);
			if (idx < 0 || idx >= skills.Count) return null;
			return skills[idx];
		}

		private void TryLearnSkill(Skill skill) {
			SkillsHelper.TryLearnSkill(skill, _parent.SelectedDupe, _parent.Screen);
		}

		// ========================================
		// Boosters
		// ========================================

		private void HandleBoosterAssign() {
			SkillsHelper.ResolveDupe(
				_parent.SelectedDupe, out var minionIdentity, out _);
			if (minionIdentity == null) { SkillsHelper.PlayRejectSound(); return; }
			int idx = Nav.Path[1] - 1; // Subtract 1 for slot summary
			if (idx < 0) { SkillsHelper.PlayRejectSound(); return; }
			try {
				var entries = SkillsHelper.GetBoosterEntries(minionIdentity);
				if (idx >= entries.Count) { SkillsHelper.PlayRejectSound(); return; }
				var entry = entries[idx];
				if (entry.AvailableCount <= 0) {
					SkillsHelper.PlayRejectSound();
					SpeechPipeline.SpeakInterrupt(
						STRINGS.ONIACCESS.SKILLS.NO_BOOSTERS_AVAILABLE);
					return;
				}
				if (SkillsHelper.TryAssignBooster(minionIdentity, entry.Tag)) {
					RefreshGameScreen();
					SkillsHelper.PlayClickSound();
					SpeechPipeline.SpeakInterrupt(
						STRINGS.ONIACCESS.SKILLS.BOOSTER_ASSIGNED);
				} else {
					SkillsHelper.PlayRejectSound();
					SpeechPipeline.SpeakInterrupt(
						STRINGS.ONIACCESS.SKILLS.NO_EMPTY_SLOTS);
				}
			} catch (Exception ex) {
				Util.Log.Warn($"SkillsTab.HandleBoosterAssign: {ex.Message}");
				SkillsHelper.PlayRejectSound();
			}
		}

		private void HandleBoosterUnassign() {
			SkillsHelper.ResolveDupe(
				_parent.SelectedDupe, out var minionIdentity, out _);
			if (minionIdentity == null) { SkillsHelper.PlayRejectSound(); return; }
			int idx = Nav.Path[1] - 1;
			if (idx < 0) { SkillsHelper.PlayRejectSound(); return; }
			try {
				var entries = SkillsHelper.GetBoosterEntries(minionIdentity);
				if (idx >= entries.Count) { SkillsHelper.PlayRejectSound(); return; }
				var entry = entries[idx];
				if (entry.AssignedCount <= 0) {
					SkillsHelper.PlayRejectSound();
					SpeechPipeline.SpeakInterrupt(
						STRINGS.ONIACCESS.SKILLS.NONE_ASSIGNED);
					return;
				}
				if (SkillsHelper.TryUnassignBooster(minionIdentity, entry.Tag)) {
					RefreshGameScreen();
					SkillsHelper.PlayClickSound();
					SpeechPipeline.SpeakInterrupt(
						STRINGS.ONIACCESS.SKILLS.BOOSTER_UNASSIGNED);
				} else {
					SkillsHelper.PlayRejectSound();
				}
			} catch (Exception ex) {
				Util.Log.Warn($"SkillsTab.HandleBoosterUnassign: {ex.Message}");
				SkillsHelper.PlayRejectSound();
			}
		}

		private void RefreshGameScreen() {
			var skillsScreen = _parent.Screen as SkillsScreen;
			if (skillsScreen != null)
				skillsScreen.RefreshAll();
		}

		// ========================================
		// Helpers
		// ========================================

		private static SkillsHelper.Bucket CategoryToBucket(int cat) {
			switch (cat) {
				case CAT_AVAILABLE: return SkillsHelper.Bucket.Available;
				case CAT_LOCKED: return SkillsHelper.Bucket.Locked;
				case CAT_MASTERED: return SkillsHelper.Bucket.Mastered;
				default: return SkillsHelper.Bucket.DupeInfo;
			}
		}
	}
}
