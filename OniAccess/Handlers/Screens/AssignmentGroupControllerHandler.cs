using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using OniAccess.Navigation;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens {
	public class AssignmentGroupControllerHandler: NavTreeHandler {
		private AssignmentGroupControllerSideScreen CrewScreen =>
			(AssignmentGroupControllerSideScreen)_screen;

		private bool _pendingActivation;

		private static readonly FieldInfo _targetField = typeof(AssignmentGroupControllerSideScreen)
			.GetField("target", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo _identityRowMapField = typeof(AssignmentGroupControllerSideScreen)
			.GetField("identityRowMap", BindingFlags.NonPublic | BindingFlags.Instance);

		static AssignmentGroupControllerHandler() {
			if (_targetField == null) Util.Log.Warn("AssignmentGroupControllerHandler: target field not found");
			if (_identityRowMapField == null) Util.Log.Warn("AssignmentGroupControllerHandler: identityRowMap field not found");
		}

		public override string DisplayName => null;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		public AssignmentGroupControllerHandler(AssignmentGroupControllerSideScreen screen) : base(screen) {
			HelpEntries = new List<HelpEntry>(DrillNavHelpEntries).AsReadOnly();
		}

		// ========================================
		// DATA ACCESS
		// ========================================

		private AssignmentGroupController GetTarget() {
			return _targetField.GetValue(CrewScreen) as AssignmentGroupController;
		}

		private List<GameObject> GetIdentityRowMap() {
			return _identityRowMapField.GetValue(CrewScreen) as List<GameObject>;
		}

		private struct RowData {
			public MultiToggle Toggle;
			public string Name;
			public string Designation;
			public bool IsOffworld;
		}

		private void GetAllRows(out List<RowData> sameWorld, out List<RowData> offWorld) {
			sameWorld = new List<RowData>();
			offWorld = new List<RowData>();
			var rows = GetIdentityRowMap();
			if (rows == null) return;
			string offworldText = (string)STRINGS.UI.UISIDESCREENS.ASSIGNMENTGROUPCONTROLLER.OFFWORLD;
			foreach (var row in rows) {
				if (!row.activeSelf) continue;
				var refs = row.GetComponent<HierarchyReferences>();
				if (refs == null) continue;
				var toggle = refs.GetReference<MultiToggle>("Toggle");
				var label = refs.GetReference<LocText>("Label");
				var designation = refs.GetReference<LocText>("Designation");
				string desigText = designation != null ? designation.GetParsedText() : "";
				var data = new RowData {
					Toggle = toggle,
					Name = label != null ? label.GetParsedText() : "",
					Designation = desigText,
					IsOffworld = desigText == offworldText
				};
				if (data.IsOffworld)
					offWorld.Add(data);
				else
					sameWorld.Add(data);
			}
		}

		// ========================================
		// TREE CONSTRUCTION
		// ========================================

		protected override IReadOnlyList<NavItem> BuildRoots() {
			GetAllRows(out var same, out var off);
			var roots = new List<NavItem> {
				SectionNode(GetSectionHeader(0), same),
			};
			if (off.Count > 0)
				roots.Add(SectionNode(GetSectionHeader(1), off));
			return roots;
		}

		private NavItem SectionNode(string header, List<RowData> rows) {
			return new MenuNode(
				() => header,
				children: () => BuildRowNodes(rows));
		}

		private IReadOnlyList<NavItem> BuildRowNodes(List<RowData> rows) {
			var list = new List<NavItem>(rows.Count);
			foreach (var row in rows) {
				var r = row;
				list.Add(new MenuNode(
					() => BuildRowLabel(r),
					activate: () => { ToggleRow(r); return true; }));
			}
			return list;
		}

		private void ToggleRow(RowData row) {
			row.Toggle.onClick?.Invoke();
			// Read state from the captured toggle reference — RefreshRows
			// re-sorts the list, so re-querying by index could hit the wrong row.
			int memberCount = GetTarget().GetMembers().Count;
			string state = row.Toggle.CurrentState == 1
				? (string)STRINGS.ONIACCESS.STATES.ASSIGNED
				: (string)STRINGS.ONIACCESS.STATES.UNASSIGNED;
			string feedback = state + ", " + string.Format(STRINGS.ONIACCESS.CREW_SCREEN.TOTAL_FORMAT, memberCount);
			SpeechPipeline.SpeakInterrupt(feedback);
		}

		// ========================================
		// LABELS
		// ========================================

		private static string GetSectionHeader(int section) {
			if (section == 0)
				return (string)STRINGS.ONIACCESS.CREW_SCREEN.AVAILABLE;
			return (string)STRINGS.UI.UISIDESCREENS.ASSIGNMENTGROUPCONTROLLER.OFFWORLD;
		}

		private static string BuildRowLabel(RowData row) {
			string label = row.Name;
			// Skip designation for offworld rows — section header already provides context
			if (!row.IsOffworld && !string.IsNullOrEmpty(row.Designation))
				label += ", " + row.Designation;
			string state = row.Toggle.CurrentState == 1
				? (string)STRINGS.ONIACCESS.STATES.ASSIGNED
				: (string)STRINGS.ONIACCESS.STATES.UNASSIGNED;
			label += ", " + state;
			return label;
		}

		// ========================================
		// ESCAPE
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

		private void CloseScreen() {
			DetailsScreenHandler.PreserveNavigationOnReactivate = true;
			HandlerStack.Pop();
			DetailsScreen.Instance?.ClearSecondarySideScreen();
		}

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			base.OnActivate();
			_pendingActivation = true;
		}

		public override bool Tick() {
			if (_pendingActivation) {
				_pendingActivation = false;
				string title = (string)STRINGS.UI.UISIDESCREENS.ASSIGNMENTGROUPCONTROLLER.TITLE;
				int memberCount = GetTarget().GetMembers().Count;
				string countLabel = string.Format(STRINGS.ONIACCESS.SKILLS.ASSIGNED, memberCount);
				string firstItem = Nav.Current()?.Announce();
				string announcement = title + ", " + countLabel + ", " + firstItem;
				SpeechPipeline.SpeakInterrupt(announcement);
				return false;
			}
			return base.Tick();
		}
	}
}
