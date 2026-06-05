using System.Collections.Generic;

using OniAccess.Input;
using OniAccess.Navigation;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Resources {
	/// <summary>
	/// Two-level resource browser backed by AllResourcesScreen.
	/// Level 0 = discovered resource categories, level 1 = resources within category.
	///
	/// If any resources are pinned, a synthetic "Pinned" category is prepended at
	/// index 0 containing all pinned resources. Space at level 1 toggles pin;
	/// Shift+C clears all pins; both rebuild the tree, so only the cursor needs
	/// fixing when the pinned category appears or disappears. Type-ahead searches
	/// the resources, excluding the pinned duplicates. Enter pushes
	/// ResourceInstanceHandler. Escape closes AllResourcesScreen.
	/// </summary>
	internal sealed class ResourceBrowserHandler: NavTreeHandler {
		internal ResourceBrowserHandler(KScreen screen) : base(screen) {
			// Search the real resources, not the synthetic pinned duplicates.
			Nav.SearchFilter = n => n.RoleKey != "pinned";
		}

		private static readonly ConsumedKey[] _consumedKeys = {
			new ConsumedKey(KKeyCode.Space),
			new ConsumedKey(KKeyCode.C, Modifier.Shift),
		};
		public override IReadOnlyList<ConsumedKey> ConsumedKeys => _consumedKeys;

		public override string DisplayName =>
			(string)STRINGS.ONIACCESS.RESOURCES.BROWSER_TITLE;

		public override void OnActivate() {
			PlaySound("HUD_Click_Open");
			base.OnActivate();

			try {
				var field = HarmonyLib.Traverse.Create(_screen).Field("searchInputField")
					.GetValue<KInputTextField>();
				if (field != null)
					field.DeactivateInputField();
			} catch (System.Exception ex) {
				Util.Log.Warn($"ResourceBrowserHandler: failed to deactivate search field: {ex.Message}");
			}

			AnnounceCurrent(interrupt: false);
		}

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }
			= new List<HelpEntry>(DrillNavHelpEntries) {
				new HelpEntry("Space", STRINGS.ONIACCESS.RESOURCES.HELP_PIN),
				new HelpEntry("Shift+C", STRINGS.ONIACCESS.RESOURCES.HELP_CLEAR_PINS),
			}.AsReadOnly();

		// ========================================
		// PINNED CATEGORY OFFSET
		// ========================================

		/// <summary>1 when pinned resources exist (synthetic category at index 0), 0 otherwise.</summary>
		private int PinnedOffset =>
			ClusterManager.Instance.activeWorld.worldInventory.pinnedResources.Count > 0 ? 1 : 0;

		private bool IsPinnedCategory(int catIndex) =>
			PinnedOffset == 1 && catIndex == 0;

		// ========================================
		// TREE CONSTRUCTION
		// ========================================

		protected override IReadOnlyList<NavItem> BuildRoots() {
			var roots = new List<NavItem>();
			if (PinnedOffset == 1) {
				roots.Add(new MenuNode(
					() => (string)STRINGS.ONIACCESS.RESOURCES.PINNED,
					children: BuildPinnedResources));
			}
			foreach (var cat in ResourceHelper.GetCategories()) {
				var c = cat;
				roots.Add(new MenuNode(
					() => ResourceHelper.BuildCategoryLabel(c.Tag),
					children: () => BuildCategoryResources(c.Tag),
					contextLabel: () => c.Tag.ProperNameStripLink()));
			}
			return roots;
		}

		private IReadOnlyList<NavItem> BuildPinnedResources() {
			var pinned = ResourceHelper.GetPinnedResources();
			var list = new List<NavItem>(pinned.Count);
			foreach (var res in pinned) {
				var r = res;
				var measure = ResourceHelper.GetMeasureForResource(r);
				list.Add(new MenuNode(
					() => ResourceHelper.BuildResourceLabel(r, measure),
					activate: () => { OpenInstance(r, measure); return true; },
					roleKey: "pinned",
					searchText: () => r.ProperNameStripLink()));
			}
			return list;
		}

		private IReadOnlyList<NavItem> BuildCategoryResources(Tag categoryTag) {
			var resources = ResourceHelper.GetResources(categoryTag);
			var measure = ResourceHelper.GetMeasure(categoryTag);
			var list = new List<NavItem>(resources.Count);
			foreach (var res in resources) {
				var r = res;
				list.Add(new MenuNode(
					() => ResourceHelper.BuildResourceLabel(r, measure),
					activate: () => { OpenInstance(r, measure); return true; },
					searchText: () => r.ProperNameStripLink()));
			}
			return list;
		}

		private void OpenInstance(Tag resourceTag, GameUtil.MeasureUnit measure) {
			if (ResourceHelper.GetInstances(resourceTag).Count == 0) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.RESOURCES.NO_INSTANCES);
				return;
			}
			PlaySound("HUD_Click_Open");
			HandlerStack.Push(new ResourceInstanceHandler(resourceTag, measure));
		}

		// ========================================
		// TICK: Space pin, Shift+C clear
		// ========================================

		public override bool Tick() {
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Space)
				&& !InputUtil.AnyModifierHeld()) {
				TogglePin();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.C)
				&& InputUtil.ShiftHeld()) {
				ClearAllPins();
				return true;
			}
			return base.Tick();
		}

		private void TogglePin() {
			if (Nav.Depth != 1) return;

			bool inPinned = IsPinnedCategory(Nav.Path[0]);
			Tag tag;
			if (inPinned) {
				var pinned = ResourceHelper.GetPinnedResources();
				int resIdx = Nav.Path[1];
				if (resIdx < 0 || resIdx >= pinned.Count) return;
				tag = pinned[resIdx];
			} else {
				var categories = ResourceHelper.GetCategories();
				int catIdx = Nav.Path[0] - PinnedOffset;
				if (catIdx < 0 || catIdx >= categories.Count) return;
				var resources = ResourceHelper.GetResources(categories[catIdx].Tag);
				int resIdx = Nav.Path[1];
				if (resIdx < 0 || resIdx >= resources.Count) return;
				tag = resources[resIdx];
			}

			var pinnedList = ClusterManager.Instance.activeWorld.worldInventory.pinnedResources;
			if (pinnedList.Contains(tag)) {
				pinnedList.Remove(tag);
				PlaySound("HUD_Click_Deselect");
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.RESOURCES.UNPINNED);

				if (inPinned) {
					var remaining = ResourceHelper.GetPinnedResources();
					if (remaining.Count == 0) {
						// Pinned category gone — drop to level 0
						Nav.SetPath(new[] { 0 });
					} else {
						int idx = Nav.Path[1];
						if (idx >= remaining.Count) idx = remaining.Count - 1;
						Nav.SetPath(new[] { 0, idx });
					}
					AnnounceCurrent(interrupt: false);
				}
			} else {
				int oldOffset = PinnedOffset;
				pinnedList.Add(tag);
				PlaySound("HUD_Click");
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.RESOURCES.PINNED);

				// PinnedOffset 0->1: pinned category inserted at index 0,
				// so the cursor's category index must shift up by 1 to stay put.
				if (oldOffset == 0 && PinnedOffset == 1)
					Nav.SetPath(new[] { Nav.Path[0] + 1, Nav.Path[1] });
			}
		}

		private void ClearAllPins() {
			var pinned = ClusterManager.Instance.activeWorld.worldInventory.pinnedResources;
			if (pinned.Count == 0) return;

			bool wasInPinned = IsPinnedCategory(Nav.Path[0]);
			pinned.Clear();
			PlaySound("HUD_Click_Deselect");
			SpeechPipeline.SpeakInterrupt(
				(string)STRINGS.ONIACCESS.RESOURCES.ALL_UNPINNED);

			if (wasInPinned) {
				Nav.SetPath(new[] { 0 });
				AnnounceCurrent(interrupt: false);
			} else if (Nav.Depth == 0) {
				// Category indices shifted down by 1, adjust
				int idx = Nav.Path[0] - 1;
				if (idx < 0) idx = 0;
				Nav.SetPath(new[] { idx });
			} else {
				// Level 1 in regular category: category index shifted down by 1
				Nav.SetPath(new[] { Nav.Path[0] - 1, Nav.Path[1] });
			}
		}

		// ========================================
		// ESCAPE: close AllResourcesScreen
		// ========================================

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e))
				return true;
			if (e.TryConsume(Action.Escape)) {
				CloseScreen();
				return true;
			}
			return false;
		}

		internal void CloseScreen() {
			PlaySound("HUD_Click_Close");
			if (AllResourcesScreen.Instance != null)
				AllResourcesScreen.Instance.Show(false);
		}
	}
}
