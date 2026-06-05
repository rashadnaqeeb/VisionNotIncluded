using System.Collections.Generic;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.ClusterMap {
	/// <summary>
	/// Modal picker for selecting one of multiple cluster map entities at a hex.
	/// Selecting calls ClusterMapSelectTool.Instance.Select() instead of
	/// SelectTool.Instance.Select().
	/// </summary>
	public class ClusterEntityPickerHandler: BaseMenuHandler {
		private readonly IReadOnlyList<ClusterGridEntity> _entities;

		public override string DisplayName =>
			(string)STRINGS.ONIACCESS.CLUSTER_MAP.SELECT_OBJECT;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }
			= Tools.ToolPickerHandler.ModalMenuHelp;

		public ClusterEntityPickerHandler(IReadOnlyList<ClusterGridEntity> entities) {
			_entities = entities;
		}

		public override int ItemCount => _entities.Count;

		public override string GetItemLabel(int index) {
			if (index < 0 || index >= _entities.Count) return null;
			return _entities[index].Name;
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			if (CurrentIndex >= 0 && CurrentIndex < _entities.Count)
				SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(_entities[CurrentIndex].Name));
		}

		public override void OnActivate() {
			PlaySound("HUD_Click_Open");
			CurrentIndex = 0;
			_search.Clear();
			SpeechPipeline.SpeakQueued(
				(string)STRINGS.ONIACCESS.CLUSTER_MAP.SELECT_OBJECT);
			if (_entities.Count > 0)
				SpeechPipeline.SpeakQueued(WidgetSpeech.ComposeLabel(_entities[0].Name));
		}

		public override void OnDeactivate() {
			PlaySound("HUD_Click_Close");
			base.OnDeactivate();
		}

		protected override void ActivateCurrentItem() {
			if (CurrentIndex < 0 || CurrentIndex >= _entities.Count) return;
			var entity = _entities[CurrentIndex];
			var selectable = entity.GetComponent<KSelectable>();
			// Pop before Select: Select() synchronously triggers DetailsScreen.OnShow
			// which pushes DetailsScreenHandler. If we pop after, we'd pop that instead.
			HandlerStack.Pop();
			ClusterMapSelectTool.Instance.Select(selectable);
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e)) return true;
			if (e.TryConsume(Action.Escape)) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.TOOLTIP.CLOSED);
				HandlerStack.Pop();
				return true;
			}
			return false;
		}
	}
}
