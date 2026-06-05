using System.Collections.Generic;

using OniAccess.Input;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Screen-bound widget handler extending BaseMenuHandler with widget discovery,
	/// interaction, speech, and lifecycle management.
	///
	/// Provides the bridge between BaseMenuHandler's abstract list navigation and
	/// concrete Widget-based screen handlers. Implements ItemCount, GetItemLabel,
	/// SpeakCurrentItem, IsItemValid by delegating to the _widgets list.
	///
	/// Concrete screen handlers extend this and implement only:
	/// - DiscoverWidgets (populate _widgets)
	/// - DisplayName (screen title for speech)
	/// - HelpEntries (composing from MenuHelpEntries + ListNavHelpEntries + screen-specific)
	///
	/// Per locked decisions:
	/// - Enter activates (ClickButton for KButton, KToggle.Click)
	/// - Left/Right adjust sliders and cycle dropdowns
	/// - Shift/Ctrl/Ctrl+Shift+Left/Right for progressively larger step adjustment
	/// - Tab/Shift+Tab for tabbed screens (virtual stubs)
	/// - Widget readout: label and value only, no type announcement
	/// - TextInput: Enter to begin editing, Enter to confirm, Escape to cancel
	///   (via TextEdit helper; subclasses using accessor-based Begin() override
	///   ActivateCurrentItem for that widget)
	///
	/// Discovery timing:
	/// - MaxDiscoveryRetries: how many frames to retry when DiscoverWidgets returns false
	/// - DeferFirstDiscovery: skip the initial DiscoverWidgets call in OnActivate entirely,
	///   letting the retry infrastructure pick it up next frame. Use this for screens that
	///   populate content after StartScreen returns (e.g., via property setters or SetEventData).
	/// </summary>
	public abstract class BaseWidgetHandler: BaseMenuHandler {
		protected readonly List<Widget> _widgets = new List<Widget>();
		private TextEditHelper _textEdit;
		protected TextEditHelper TextEdit => _textEdit ??= new TextEditHelper();
		protected bool IsTextEditing => _textEdit != null && _textEdit.IsEditing;

		/// <summary>
		/// When true, Tick() will retry DiscoverWidgets.
		/// Set when OnActivate finds zero widgets — this happens when our Harmony
		/// postfix fires inside base.OnSpawn() before the screen subclass finishes
		/// setting up its UI in its own OnSpawn override.
		/// Retries up to MaxDiscoveryRetries times (default 1).
		/// </summary>
		protected bool _pendingRediscovery;
		private bool _pendingSilentRefresh;
		private int _retryCount;

		/// <summary>
		/// Maximum number of frames to retry DiscoverWidgets when it returns false.
		/// Override in subclasses that need more time (e.g., coroutine-driven screens).
		/// </summary>
		protected virtual int MaxDiscoveryRetries => 1;

		/// <summary>
		/// When true, OnActivate skips the initial DiscoverWidgets call and sets
		/// _pendingRediscovery so Tick() picks it up next frame. Use for screens
		/// that populate content after StartScreen returns.
		/// </summary>
		protected virtual bool DeferFirstDiscovery => false;

		protected BaseWidgetHandler(KScreen screen) : base(screen) { }

		// ========================================
		// BaseMenuHandler ABSTRACT IMPLEMENTATIONS
		// ========================================

		public override int ItemCount => _widgets.Count;

		public override string GetItemLabel(int index) {
			if (index < 0 || index >= _widgets.Count) return null;
			return _widgets[index].Label;
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			SpeakCurrentWidget();
		}

		protected override bool IsItemValid(int index) {
			if (index < 0 || index >= _widgets.Count) return false;
			return IsWidgetValid(_widgets[index]);
		}

		// ========================================
		// ABSTRACT: WIDGET DISCOVERY
		// ========================================

		/// <summary>
		/// Populate _widgets from the screen's UI hierarchy.
		/// Each subclass implements to enumerate that screen's interactive elements.
		/// </summary>
		/// <returns>
		/// true if discovery is complete and widgets are ready to speak;
		/// false if the screen isn't ready yet, BaseWidgetHandler will retry next frame.
		/// </returns>
		public abstract bool DiscoverWidgets(KScreen screen);

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			base.OnActivate();
			_retryCount = 0;
			MousePointerSync.Push();

			if (DeferFirstDiscovery) {
				_pendingRediscovery = true;
				return;
			}

			bool ready = DiscoverWidgets(_screen);

			if (ready && _widgets.Count > 0) {
				_pendingRediscovery = false;
				_pendingSilentRefresh = true;
			} else {
				_pendingRediscovery = true;
			}
		}

		public override void OnDeactivate() {
			base.OnDeactivate();
			_widgets.Clear();
			MousePointerSync.Pop();
		}

		// ========================================
		// TICK
		// ========================================

		public override bool Tick() {
			if (_textEdit != null && _textEdit.HandleTick())
				return false;

			// Deferred first-widget announcement: rediscover to pick up widgets
			// that weren't activeInHierarchy on frame 0, then queue the first widget.
			if (_pendingSilentRefresh) {
				_pendingSilentRefresh = false;
				int oldCount = _widgets.Count;
				DiscoverWidgets(_screen);
				CurrentIndex = System.Math.Min(CurrentIndex, System.Math.Max(0, _widgets.Count - 1));
				if (_widgets.Count != oldCount)
					Util.Log.Debug($"{GetType().Name}: deferred refresh changed widget count {oldCount} → {_widgets.Count}");
				if (_widgets.Count > 0) {
					var w = _widgets[CurrentIndex];
					Speech.SpeechPipeline.SpeakQueued(BuildWidgetText(w));
					MousePointerSync.SyncToWidget(w);
				}
			}

			// Deferred rediscovery: screen UI wasn't ready during OnActivate
			if (_pendingRediscovery) {
				_pendingRediscovery = false;
				bool ready = DiscoverWidgets(_screen);
				CurrentIndex = 0;
				if (ready && _widgets.Count > 0) {
					Speech.SpeechPipeline.SpeakQueued(BuildWidgetText(_widgets[0]));
					MousePointerSync.SyncToWidget(_widgets[0]);
				} else if (_retryCount < MaxDiscoveryRetries) {
					_retryCount++;
					_pendingRediscovery = true;
				} else {
					_retryCount = 0;
					Util.Log.Warn($"{GetType().Name}: gave up retrying DiscoverWidgets after {MaxDiscoveryRetries} attempts");
				}
			}

			// Detect invalidated widgets — do not reset _retryCount here so the
			// retry limit still applies if rediscovery keeps finding the same invalid widget.
			if (!_pendingRediscovery && _widgets.Count > 0) {
				int idx = System.Math.Min(CurrentIndex, _widgets.Count - 1);
				if (!IsWidgetValid(_widgets[idx])) {
					_pendingRediscovery = true;
					return false;
				}
			}

			return base.Tick();
		}

		// ========================================
		// HANDLE KEY DOWN
		// ========================================

		public override bool HandleKeyDown(KButtonEvent e) {
			if (_textEdit != null && _textEdit.HandleKeyDown(e))
				return true;

			return base.HandleKeyDown(e);
		}

		// ========================================
		// WIDGET INTERACTION (ActivateCurrentItem / AdjustCurrentItem overrides)
		// ========================================

		/// <summary>
		/// Activate the currently focused widget. Dispatches by Widget subclass:
		/// - Button: ClickButton (triggers onClick + plays button sound)
		/// - Toggle: Click() then speak new state
		/// - TextInput: Begin/Confirm via TextEdit (Enter toggles editing)
		/// </summary>
		protected override void ActivateCurrentItem() {
			if (CurrentIndex < 0 || CurrentIndex >= _widgets.Count) return;
			var widget = _widgets[CurrentIndex];
			if (!IsWidgetValid(widget)) return;

			if (!widget.IsInteractable) {
				PlaySound("Negative");
				Speech.SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.FABRICATOR.UNAVAILABLE);
				return;
			}

			if (widget is ButtonWidget bw) {
				bw.Activate();
				return;
			}

			if (widget is ToggleWidget tw) {
				tw.Activate();
				Speech.SpeechPipeline.SpeakInterrupt(WidgetOps.GetSpeechText(tw));
				return;
			}

			if (widget is TextInputWidget tiw) {
				var textField = tiw.GetTextField();
				if (textField != null) {
					if (!TextEdit.IsEditing)
						TextEdit.Begin(textField, onEnd: QueueCurrentWidget);
					else
						TextEdit.Confirm();
				}
				return;
			}
		}

		/// <summary>
		/// Adjust the currently focused widget's value. Dispatches by Widget subclass:
		/// - Slider: step by wholeNumbers-aware increment, speak new value
		/// - Dropdown: delegate to CycleDropdown virtual method
		/// </summary>
		protected override void AdjustCurrentItem(int direction, int stepLevel) {
			if (CurrentIndex < 0 || CurrentIndex >= _widgets.Count) return;
			var widget = _widgets[CurrentIndex];
			if (!IsWidgetValid(widget)) return;

			if (widget is SliderWidget sw) {
				bool changed = sw.Adjust(direction, stepLevel);
				PlaySliderSound(sw.GetBoundarySound(direction));
				if (changed) {
					var slider = widget.Component as KSlider;
					if (slider != null)
						Speech.SpeechPipeline.SpeakInterrupt($"{widget.Label}, {FormatSliderValue(slider)}");
				}
				return;
			}

			if (widget is DropdownWidget) {
				CycleDropdown(widget, direction);
			}
		}

		/// <summary>
		/// Cycle a dropdown widget's value. No-op default.
		/// </summary>
		protected virtual void CycleDropdown(Widget widget, int direction) { }

		// ========================================
		// WIDGET VALIDITY
		// ========================================

		protected virtual bool IsWidgetValid(Widget widget) => WidgetOps.IsValid(widget);

		protected static string GetButtonLabel(KButton button, string fallback = null)
			=> WidgetOps.GetButtonLabel(button, fallback);

		// ========================================
		// WIDGET SPEECH
		// ========================================

		protected virtual string GetWidgetSpeechText(Widget widget) => WidgetOps.GetSpeechText(widget);

		protected string BuildWidgetText(Widget widget) {
			return WidgetSpeech.Compose(widget, NavContext.None, GetTooltipText(widget));
		}

		/// <summary>
		/// Speak the currently focused widget via SpeakInterrupt.
		/// Appends tooltip text if available.
		/// </summary>
		protected void SpeakCurrentWidget() {
			if (CurrentIndex >= 0 && CurrentIndex < _widgets.Count) {
				var w = _widgets[CurrentIndex];
				if (!IsWidgetValid(w)) return;
				Speech.SpeechPipeline.SpeakInterrupt(BuildWidgetText(w));
				MousePointerSync.SyncToWidget(w);
			}
		}

		/// <summary>
		/// Queue the currently focused widget via SpeakQueued so it follows
		/// a preceding SpeakInterrupt (e.g., after text-edit confirm/cancel).
		/// </summary>
		protected void QueueCurrentWidget() {
			if (CurrentIndex >= 0 && CurrentIndex < _widgets.Count) {
				var w = _widgets[CurrentIndex];
				if (!IsWidgetValid(w)) return;
				Speech.SpeechPipeline.SpeakQueued(BuildWidgetText(w));
				MousePointerSync.SyncToWidget(w);
			}
		}

		// ========================================
		// TOOLTIP TEXT
		// ========================================

		protected virtual string GetTooltipText(Widget widget) => WidgetOps.GetTooltipText(widget);

		protected static string ReadAllTooltipText(ToolTip tooltip) => WidgetOps.ReadAllTooltipText(tooltip);

		// ========================================
		// UTILITY METHODS
		// ========================================

		protected static void ClickButton(KButton button) => WidgetOps.ClickButton(button);
		protected static void ClickMultiToggle(MultiToggle toggle) => WidgetOps.ClickMultiToggle(toggle);

		private void PlaySliderSound(string soundName) {
			BaseScreenHandler.PlaySound(soundName);
		}

		protected virtual string FormatSliderValue(KSlider slider) => WidgetOps.FormatSliderValue(slider);
	}
}
