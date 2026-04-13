using System.Collections.Generic;

using HarmonyLib;
using UnityEngine;

using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Details tab for PrinterceptorScreenHandler. Flat widget list for the
	/// currently selected printable: name, description, data-bank cost, wallet
	/// amount, Print button. Cost and wallet are re-read from the live screen
	/// every time the tab is activated so print-count scaling and databank
	/// spending stay current.
	/// </summary>
	internal class PrinterceptorDetailsTab: BaseMenuHandler, IScreenTab {
		private readonly PrinterceptorScreenHandler _parent;
		private readonly List<Widget> _widgets = new List<Widget>();

		private static readonly List<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.NAVIGATE_ITEMS),
			new HelpEntry("Home/End", STRINGS.ONIACCESS.HELP.JUMP_FIRST_LAST),
			new HelpEntry("Enter", STRINGS.ONIACCESS.HELP.SELECT_ITEM),
			new HelpEntry("Escape", STRINGS.ONIACCESS.HELP.GO_BACK),
		};

		internal PrinterceptorDetailsTab(PrinterceptorScreenHandler parent) : base(screen: null) {
			_parent = parent;
		}

		public string TabName => (string)STRINGS.ONIACCESS.PRINTERCEPTOR.DETAILS_TAB;

		public override string DisplayName => TabName;

		public override IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;

		// ========================================
		// IScreenTab
		// ========================================

		public void OnTabActivated(bool announce) {
			RebuildWidgets();
			CurrentIndex = 0;
			_search.Clear();
			SuppressSearchThisFrame();
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			SpeakCurrentItemQueued();
		}

		public void OnTabDeactivated() {
			_search.Clear();
		}

		public bool HandleInput() {
			return base.Tick();
		}

		public new bool HandleKeyDown(KButtonEvent e) {
			return base.HandleKeyDown(e);
		}

		// ========================================
		// BaseMenuHandler abstracts
		// ========================================

		public override int ItemCount => _widgets.Count;

		public override string GetItemLabel(int index) {
			if (index < 0 || index >= _widgets.Count) return null;
			return _widgets[index].Label;
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			if (CurrentIndex < 0 || CurrentIndex >= _widgets.Count) return;
			var text = _widgets[CurrentIndex].GetSpeechText();
			if (!string.IsNullOrEmpty(text))
				SpeechPipeline.SpeakInterrupt(text);
		}

		private void SpeakCurrentItemQueued() {
			if (CurrentIndex < 0 || CurrentIndex >= _widgets.Count) return;
			var text = _widgets[CurrentIndex].GetSpeechText();
			if (!string.IsNullOrEmpty(text))
				SpeechPipeline.SpeakQueued(text);
		}

		protected override void ActivateCurrentItem() {
			if (CurrentIndex < 0 || CurrentIndex >= _widgets.Count) return;
			var widget = _widgets[CurrentIndex];
			if (widget is ButtonWidget bw && bw.Tag is string tag && tag == "print") {
				ActivatePrint();
				return;
			}
		}

		// ========================================
		// PRINT ACTION
		// ========================================

		private void ActivatePrint() {
			var screen = _parent.PrinterceptorScreen;
			if (screen == null) return;

			Tag selected = _parent.SelectedTag;
			if (!selected.IsValid) {
				Util.Log.Warn("PrinterceptorDetailsTab.ActivatePrint: no selection");
				return;
			}

			int cost = GetCurrentCost(screen, selected);
			float wallet = GetCurrentWallet(screen);
			if (wallet < cost) {
				PlaySound("Negative");
				string tooltip = (string)STRINGS.UI.PRINTERCEPTORSCREEN.PRINT_TOOLTIP_DISABLED;
				SpeechPipeline.SpeakInterrupt(tooltip);
				return;
			}

			try {
				AccessTools.Method(typeof(PrinterceptorScreen), "SelectEntity",
					new System.Type[] { typeof(Tag) })
					.Invoke(screen, new object[] { selected });
			} catch (System.Exception ex) {
				Util.Log.Error($"PrinterceptorDetailsTab.ActivatePrint(SelectEntity): {ex.Message}");
				return;
			}

			try {
				var printButton = Traverse.Create(screen).Field<KButton>("printButton").Value;
				if (printButton == null) {
					Util.Log.Warn("PrinterceptorDetailsTab.ActivatePrint: printButton null");
					return;
				}
				WidgetOps.ClickButton(printButton);
			} catch (System.Exception ex) {
				Util.Log.Error($"PrinterceptorDetailsTab.ActivatePrint(click): {ex.Message}");
			}
		}

		// ========================================
		// WIDGET LIST BUILDING
		// ========================================

		private void RebuildWidgets() {
			_widgets.Clear();

			var screen = _parent.PrinterceptorScreen;
			if (screen == null) return;

			Tag selected = _parent.SelectedTag;
			if (!selected.IsValid) return;

			GameObject prefab;
			try {
				prefab = Assets.GetPrefab(selected);
			} catch (System.Exception ex) {
				Util.Log.Error($"PrinterceptorDetailsTab.RebuildWidgets(prefab): {ex.Message}");
				return;
			}
			if (prefab == null) return;

			string name = prefab.GetProperName();
			if (!string.IsNullOrEmpty(name))
				_widgets.Add(new LabelWidget { Label = name, GameObject = screen.gameObject });

			try {
				var info = prefab.GetComponent<InfoDescription>();
				if (info != null && !string.IsNullOrEmpty(info.description))
					_widgets.Add(new LabelWidget {
						Label = info.description,
						GameObject = screen.gameObject
					});
			} catch (System.Exception ex) {
				Util.Log.Warn($"PrinterceptorDetailsTab.RebuildWidgets(desc): {ex.Message}");
			}

			int cost = GetCurrentCost(screen, selected);
			float wallet = GetCurrentWallet(screen);

			_widgets.Add(new LabelWidget {
				Label = string.Format(
					(string)STRINGS.UI.PRINTERCEPTORSCREEN.DATABANKS_COST,
					cost.ToString()),
				GameObject = screen.gameObject
			});

			_widgets.Add(new LabelWidget {
				Label = string.Format(
					(string)STRINGS.UI.PRINTERCEPTORSCREEN.DATABANKS_AVAILABLE,
					wallet.ToString()),
				GameObject = screen.gameObject
			});

			bool canPrint = wallet >= cost;
			KButton printButton = null;
			try {
				printButton = Traverse.Create(screen).Field<KButton>("printButton").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"PrinterceptorDetailsTab.RebuildWidgets(printButton): {ex.Message}");
			}

			string printLabel = (string)STRINGS.UI.PRINTERCEPTORSCREEN.PRINT;
			string printTooltip = canPrint
				? string.Format((string)STRINGS.UI.PRINTERCEPTORSCREEN.PRINT_TOOLTIP,
					HijackedHeadquartersConfig.COST_INCREASE_PER_PRINT)
				: (string)STRINGS.UI.PRINTERCEPTORSCREEN.PRINT_TOOLTIP_DISABLED;

			var bw = new ButtonWidget {
				Label = printLabel + ". " + printTooltip,
				Component = printButton,
				GameObject = printButton != null ? printButton.gameObject : screen.gameObject,
				Tag = "print",
			};
			bw.IsInteractable = canPrint;
			_widgets.Add(bw);
		}

		private static int GetCurrentCost(PrinterceptorScreen screen, Tag selected) {
			try {
				int printCount = 0;
				var target = Traverse.Create(screen).Field("target").GetValue<HijackedHeadquarters.Instance>();
				if (target != null && target.printCounts != null
					&& target.printCounts.TryGetValue(selected, out int pc))
					printCount = pc;
				return HijackedHeadquartersConfig.GetDataBankCost(selected, printCount);
			} catch (System.Exception ex) {
				Util.Log.Error($"PrinterceptorDetailsTab.GetCurrentCost: {ex.Message}");
				return HijackedHeadquartersConfig.DEFAULT_DATABANK_PRINT_COST;
			}
		}

		private static float GetCurrentWallet(PrinterceptorScreen screen) {
			try {
				var target = Traverse.Create(screen).Field("target").GetValue<HijackedHeadquarters.Instance>();
				if (target == null) return 0f;
				var storage = target.GetComponent<Storage>();
				if (storage == null) return 0f;
				return storage.GetAmountAvailable(DatabankHelper.ID);
			} catch (System.Exception ex) {
				Util.Log.Error($"PrinterceptorDetailsTab.GetCurrentWallet: {ex.Message}");
				return 0f;
			}
		}
	}
}
