using System.Collections.Generic;
using OniAccess.Input;
using OniAccess.Widgets;

namespace OniAccess.Handlers {
	/// <summary>
	/// Handler for ? help mode. Extends BaseMenuHandler for 1D navigation
	/// with type-ahead search, Home/End, and wrap sounds. No KScreen.
	/// Speaks help entries one at a time with Up/Down arrow navigation.
	/// Escape or ? returns to the previous handler.
	///
	/// Per locked decision: ? opens a navigable list (arrow keys step through entries),
	/// not a speech dump. Show only the active handler's keys.
	/// </summary>
	public class HelpHandler: BaseMenuHandler {
		private readonly IReadOnlyList<HelpEntry> _entries;

		public override string DisplayName => STRINGS.ONIACCESS.HANDLERS.HELP;

		/// <summary>
		/// HelpHandler's own help entries describe how to navigate the help list itself.
		/// </summary>
		public override IReadOnlyList<HelpEntry> HelpEntries { get; }
			= new List<HelpEntry> {
				new HelpEntry("A-Z", STRINGS.ONIACCESS.HELP.TYPE_SEARCH),
				new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.NAVIGATE),
				new HelpEntry("Home/End", STRINGS.ONIACCESS.HELP.JUMP_FIRST_LAST),
				new HelpEntry("Escape", STRINGS.ONIACCESS.HELP.CLOSE),
				new HelpEntry("?", STRINGS.ONIACCESS.HELP.CLOSE),
			}.AsReadOnly();

		/// <summary>
		/// Entries appended to the bottom of every help list, regardless of handler.
		/// </summary>
		private static readonly List<HelpEntry> _commonEntries
			= new List<HelpEntry> {
			new HelpEntry("F12", STRINGS.ONIACCESS.HELP.OPEN_CONFIG),
			new HelpEntry("Ctrl+Shift+F12", STRINGS.ONIACCESS.HOTKEYS.TOGGLE_MOD),
		};

		public HelpHandler(IReadOnlyList<HelpEntry> entries) {
			var combined = new List<HelpEntry>();
			if (entries != null)
				combined.AddRange(entries);
			combined.AddRange(_commonEntries);
			_entries = combined.AsReadOnly();
		}

		public override int ItemCount => _entries.Count;

		public override string GetItemLabel(int index) {
			if (index < 0 || index >= _entries.Count) return null;
			return _entries[index].ToString();
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			if (CurrentIndex >= 0 && CurrentIndex < _entries.Count)
				Speech.SpeechPipeline.SpeakInterrupt(
					WidgetSpeech.ComposeLabel(_entries[CurrentIndex].ToString()));
		}

		public override void OnActivate() {
			PlaySound("HUD_Click_Open");
			base.OnActivate();
			if (_entries.Count > 0)
				Speech.SpeechPipeline.SpeakQueued(
					WidgetSpeech.ComposeLabel(_entries[CurrentIndex].ToString()));
			else
				Speech.SpeechPipeline.SpeakQueued(STRINGS.ONIACCESS.SPEECH.NO_COMMANDS);
		}

		public override bool Tick() {
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Slash)
			&& InputUtil.ShiftHeld() && !InputUtil.CtrlHeld()) {
				Close();
				return true;
			}
			return base.Tick();
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e))
				return true;
			if (e.TryConsume(Action.Escape)) {
				Close();
				return true;
			}
			return false;
		}

		private void Close() {
			Speech.SpeechPipeline.SpeakInterrupt(STRINGS.ONIACCESS.TOOLTIP.CLOSED);
			PlaySound("HUD_Click_Close");
			HandlerStack.Pop();
		}
	}
}
