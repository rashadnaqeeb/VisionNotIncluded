using System.Collections.Generic;
using Database;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Build {
	/// <summary>
	/// Modal facade picker for a building with cosmetic skins.
	/// Lists unlocked facades plus the default appearance.
	/// Enter selects the facade and pops back to BuildInfoHandler.
	/// </summary>
	public class FacadePickerHandler: BaseMenuHandler {
		private readonly BuildingDef _def;
		private List<FacadeEntry> _facades;

		private static readonly IReadOnlyList<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("A-Z", STRINGS.ONIACCESS.HELP.TYPE_SEARCH),
			new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.NAVIGATE_ITEMS),
			new HelpEntry("Home/End", STRINGS.ONIACCESS.HELP.JUMP_FIRST_LAST),
			new HelpEntry("Enter", STRINGS.ONIACCESS.HELP.SELECT_ITEM),
			new HelpEntry("Escape", STRINGS.ONIACCESS.HELP.CLOSE),
		}.AsReadOnly();

		public override IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;
		public override string DisplayName => "";

		public FacadePickerHandler(BuildingDef def) {
			_def = def;
		}

		public override int ItemCount => _facades != null ? _facades.Count : 0;

		public override string GetItemLabel(int index) {
			if (_facades == null || index < 0 || index >= _facades.Count) return null;
			return _facades[index].Label;
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			if (_facades != null && CurrentIndex >= 0 && CurrentIndex < _facades.Count)
				SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(_facades[CurrentIndex].Label));
		}

		public override void OnActivate() {
			PlaySound("HUD_Click_Open");
			RebuildList();
			CurrentIndex = 0;
			_search.Clear();
			UnityEngine.Input.imeCompositionMode = UnityEngine.IMECompositionMode.On;

			PositionOnSelected();

			if (_facades.Count > 0)
				SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(_facades[CurrentIndex].Label));
		}

		public override void OnDeactivate() {
			PlaySound("HUD_Click_Close");
			base.OnDeactivate();
		}

		protected override void ActivateCurrentItem() {
			if (_facades == null || CurrentIndex < 0 || CurrentIndex >= _facades.Count)
				return;

			var entry = _facades[CurrentIndex];
			var facadePanel = PlanScreen.Instance.ProductInfoScreen.FacadeSelectionPanel;
			facadePanel.SelectedFacade = entry.Id;
			HandlerStack.Pop();
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e))
				return true;
			if (e.TryConsume(Action.Escape)) {
				HandlerStack.Pop();
				return true;
			}
			return false;
		}

		private void RebuildList() {
			_facades = new List<FacadeEntry>();

			_facades.Add(new FacadeEntry {
				Id = BuildMenuData.DefaultFacadeId,
				Label = (string)STRINGS.ONIACCESS.BUILD_MENU.FACADE_DEFAULT,
			});

			foreach (var id in _def.AvailableFacades) {
				var permit = Db.Get().Permits.TryGet(id);
				if (permit == null || !permit.IsUnlocked())
					continue;

				var resource = Db.GetBuildingFacades().TryGet(id);
				if (resource == null)
					continue;

				string label = resource.Name;
				if (!string.IsNullOrEmpty(resource.Description))
					label += ", " + resource.Description;

				_facades.Add(new FacadeEntry {
					Id = id,
					Label = label,
				});
			}
		}

		private void PositionOnSelected() {
			try {
				var facadePanel = PlanScreen.Instance.ProductInfoScreen.FacadeSelectionPanel;
				string selected = facadePanel.SelectedFacade;
				if (selected == null) return;

				for (int i = 0; i < _facades.Count; i++) {
					if (_facades[i].Id == selected) {
						CurrentIndex = i;
						break;
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"FacadePickerHandler.PositionOnSelected: {ex.Message}");
			}
		}


		private struct FacadeEntry {
			public string Id;
			public string Label;
		}
	}
}
