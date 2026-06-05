using System.Collections.Generic;
using System.Reflection;

using OniAccess.Handlers.Screens;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens {
	public class OwnablesSecondHandler: BaseMenuHandler {
		private OwnablesSecondSideScreen OwnablesScreen =>
			(OwnablesSecondSideScreen)_screen;

		private bool _pendingActivation;

		private static readonly FieldInfo _itemRowsField = typeof(OwnablesSecondSideScreen)
			.GetField("itemRows", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo _sideScreensField = typeof(DetailsScreen)
			.GetField("sideScreens", BindingFlags.NonPublic | BindingFlags.Instance);

		static OwnablesSecondHandler() {
			if (_itemRowsField == null) Util.Log.Warn("OwnablesSecondHandler: itemRows field not found");
			if (_sideScreensField == null) Util.Log.Warn("OwnablesSecondHandler: sideScreens field not found");
		}

		public override string DisplayName => null;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		public OwnablesSecondHandler(OwnablesSecondSideScreen screen) : base(screen) {
			HelpEntries = BuildHelpEntries();
		}

		// ========================================
		// LIST DESCRIPTION
		// ========================================

		private List<OwnablesSecondSideScreenRow> GetActiveRows() {
			var allRows = _itemRowsField.GetValue(OwnablesScreen)
				as List<OwnablesSecondSideScreenRow>;
			if (allRows == null) {
				Util.Log.Warn("OwnablesSecondHandler: itemRows value is null or wrong type");
				return new List<OwnablesSecondSideScreenRow>();
			}
			var active = new List<OwnablesSecondSideScreenRow>();
			foreach (var row in allRows) {
				if (row.gameObject.activeSelf && row.item != null)
					active.Add(row);
			}
			return active;
		}

		public override int ItemCount => 1 + GetActiveRows().Count;

		public override string GetItemLabel(int index) {
			if (index == 0) {
				bool hasItem = OwnablesScreen.HasItem;
				string label = (string)STRINGS.UI.UISIDESCREENS.OWNABLESSECONDSIDESCREEN.NONE_ROW_LABEL;
				if (!hasItem)
					label = (string)STRINGS.ONIACCESS.STATES.SELECTED + ", " + label;
				return label;
			}

			var rows = GetActiveRows();
			int rowIdx = index - 1;
			if (rowIdx < 0 || rowIdx >= rows.Count) return null;
			var row = rows[rowIdx];

			var item = row.item;
			string itemLabel = TextFilter.FilterForSpeech(item.GetProperName());

			var info = item.gameObject.GetComponent<InfoDescription>();
			if (info != null && !string.IsNullOrEmpty(info.description))
				itemLabel += ", " + TextFilter.FilterForSpeech(info.description);

			bool isCurrentItem = OwnablesScreen.HasItem
				&& OwnablesScreen.CurrentSlotItem == item;

			if (item.IsAssigned()) {
				if (isCurrentItem)
					itemLabel += ", " + (string)STRINGS.UI.UISIDESCREENS.OWNABLESSECONDSIDESCREEN.ASSIGNED_TO_SELF_STATUS;
				else
					itemLabel += ", " + string.Format(
						(string)STRINGS.UI.UISIDESCREENS.OWNABLESSECONDSIDESCREEN.ASSIGNED_TO_OTHER_STATUS,
						item.assignee.GetProperName());
			}

			int cell = Grid.PosToCell(item.transform.position);
			itemLabel += ". " + Util.GridCoordinates.Format(cell);

			if (isCurrentItem)
				itemLabel = (string)STRINGS.ONIACCESS.STATES.SELECTED + ", " + itemLabel;

			return itemLabel;
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(GetItemLabel(CurrentIndex)));
		}

		// ========================================
		// ACTIVATION
		// ========================================

		protected override void ActivateCurrentItem() {
			if (CurrentIndex == 0) {
				OwnablesScreen.noneRow.onClick?.Invoke();
			} else {
				var rows = GetActiveRows();
				int rowIdx = CurrentIndex - 1;
				if (rowIdx >= 0 && rowIdx < rows.Count) {
					var row = rows[rowIdx];
					row.OnRowClicked?.Invoke(row);
				}
			}
			// Row list may change after click; clamp index
			int newCount = ItemCount;
			if (CurrentIndex >= newCount && newCount > 0)
				CurrentIndex = newCount - 1;
			SpeakCurrentItem();
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
			// SetSelectedSlot(null) clears lastSelectedSlot (deselects the
			// parent toggle) and internally calls ClearSecondarySideScreen.
			var parent = FindParentScreen();
			if (parent != null)
				parent.SetSelectedSlot(null);
			else
				DetailsScreen.Instance?.ClearSecondarySideScreen();
		}

		private OwnablesSidescreen FindParentScreen() {
			var ds = DetailsScreen.Instance;
			if (ds == null) return null;
			var refs = _sideScreensField.GetValue(ds)
				as List<DetailsScreen.SideScreenRef>;
			if (refs == null) return null;
			foreach (var r in refs) {
				if (r.screenInstance is OwnablesSidescreen ownables)
					return ownables;
			}
			return null;
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
				string slotName = OwnablesScreen.SlotType?.Name;
				string label = slotName ?? "";
				// Announce slot context + first item
				string firstItem = GetItemLabel(0);
				if (!string.IsNullOrEmpty(firstItem))
					label += ": " + firstItem;
				SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(label));
				return false;
			}
			return base.Tick();
		}
	}
}
