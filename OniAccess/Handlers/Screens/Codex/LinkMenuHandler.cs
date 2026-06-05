using System.Collections.Generic;

using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Codex {
	/// <summary>
	/// Temporary popup menu for choosing between multiple links on a widget.
	/// Pushed onto HandlerStack; pops itself on Enter (follow link) or Escape.
	/// </summary>
	internal class LinkMenuHandler: BaseMenuHandler {
		private readonly CodexScreenHandler _parent;
		private readonly List<(string id, string text)> _links;

		internal LinkMenuHandler(CodexScreenHandler parent, List<(string id, string text)> links) : base(screen: null) {
			_parent = parent;
			_links = links;
		}

		public override string DisplayName => string.Format(
			STRINGS.ONIACCESS.CODEX.LINK_MENU, _links.Count);

		public override void OnActivate() {
			CurrentIndex = 0;
			_search.Clear();
			SuppressSearchThisFrame();
			SpeakCurrentItem();
		}

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }
			= new List<HelpEntry> {
				new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.NAVIGATE_ITEMS),
				new HelpEntry("Enter", STRINGS.ONIACCESS.CODEX.FOLLOW_LINK_HELP),
			}.AsReadOnly();

		public override int ItemCount => _links.Count;

		public override string GetItemLabel(int index) {
			if (index < 0 || index >= _links.Count) return null;
			return _links[index].text;
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			if (CurrentIndex < 0 || CurrentIndex >= _links.Count) return;
			string text = _links[CurrentIndex].text;
			if (!string.IsNullOrEmpty(parentContext))
				text = parentContext + ", " + text;
			SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(text));
		}

		protected override void ActivateCurrentItem() {
			if (CurrentIndex < 0 || CurrentIndex >= _links.Count) return;
			string linkId = _links[CurrentIndex].id;
			HandlerStack.Pop();
			_parent.ContentTabRef.FollowLink(linkId);
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e))
				return true;
			if (e.TryConsume(Action.Escape)) {
				HandlerStack.Pop();
				SpeechPipeline.SpeakInterrupt(STRINGS.ONIACCESS.TOOLTIP.CLOSED);
				return true;
			}
			return false;
		}
	}
}
