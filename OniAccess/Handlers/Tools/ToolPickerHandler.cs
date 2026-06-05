using System.Collections.Generic;
using HarmonyLib;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Tools {
	/// <summary>
	/// Modal menu for selecting a standard toolbar tool via type-ahead search.
	/// Lists all 12 tools from ToolHandler.AllTools. Selecting a tool either
	/// activates it directly or opens ToolFilterHandler for mode-pick first
	/// (e.g., Harvest).
	/// </summary>
	public class ToolPickerHandler: BaseMenuHandler {
		public override string DisplayName => (string)STRINGS.ONIACCESS.TOOLS.PICKER_NAME;

		internal static readonly IReadOnlyList<HelpEntry> ModalMenuHelp
			= new List<HelpEntry> {
				new HelpEntry("A-Z", STRINGS.ONIACCESS.HELP.TYPE_SEARCH),
				new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.NAVIGATE_ITEMS),
				new HelpEntry("Home/End", STRINGS.ONIACCESS.HELP.JUMP_FIRST_LAST),
				new HelpEntry("Enter", STRINGS.ONIACCESS.HELP.SELECT_ITEM),
				new HelpEntry("Escape", STRINGS.ONIACCESS.HELP.CLOSE),
			}.AsReadOnly();

		public override IReadOnlyList<HelpEntry> HelpEntries => ModalMenuHelp;

		public override int ItemCount => ToolHandler.AllTools.Count;

		public override string GetItemLabel(int index) {
			if (index < 0 || index >= ToolHandler.AllTools.Count) return null;
			return ToolHandler.AllTools[index].Label;
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			if (CurrentIndex >= 0 && CurrentIndex < ToolHandler.AllTools.Count)
				SpeechPipeline.SpeakInterrupt(
					WidgetSpeech.ComposeLabel(ToolHandler.AllTools[CurrentIndex].Label));
		}

		public override void OnActivate() {
			PlaySound("HUD_Click_Open");
			CurrentIndex = 0;
			_search.Clear();
			if (ToolHandler.AllTools.Count > 0)
				SpeechPipeline.SpeakInterrupt(
					WidgetSpeech.ComposeLabel(ToolHandler.AllTools[0].Label));
		}

		public override void OnDeactivate() {
			PlaySound("HUD_Click_Close");
			base.OnDeactivate();
		}

		protected override void ActivateCurrentItem() {
			if (CurrentIndex < 0 || CurrentIndex >= ToolHandler.AllTools.Count)
				return;

			var tool = ToolHandler.AllTools[CurrentIndex];
			if (tool.RequiresModeFirst) {
				HandlerStack.Replace(new ToolFilterHandler(tool));
			} else {
				ActivateTool(tool);
				HandlerStack.Replace(new ToolHandler());
			}
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e))
				return true;
			if (e.TryConsume(Action.Escape)) {
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TOOLTIP.CLOSED);
				HandlerStack.Pop();
				return true;
			}
			return false;
		}

		private static System.Reflection.MethodInfo _chooseToolMethod;
		private static System.Reflection.MethodInfo _chooseCollectionMethod;

		internal static void ActivateTool(ModToolInfo tool) {
			try {
				ToolMenu.ToolInfo found = null;
				foreach (var collection in ToolMenu.Instance.basicTools)
					foreach (var ti in collection.tools) {
						if (ti.toolName == tool.ToolName) {
							found = ti;
							break;
						}
					}

				if (found != null) {
					ChooseToolViaReflection(found);
				} else {
					foreach (var interfaceTool in PlayerController.Instance.tools) {
						if (interfaceTool.GetType().Name == tool.ToolName) {
							PlayerController.Instance.ActivateTool(interfaceTool);
							UISounds.PlaySound(UISounds.Sound.ClickObject);
							break;
						}
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ToolPickerHandler.ActivateTool: {ex}");
			}
		}

		internal static void ActivateSandboxTool(ToolMenu.ToolInfo ti) {
			try {
				ChooseToolViaReflection(ti);
			} catch (System.Exception ex) {
				Util.Log.Error($"ToolPickerHandler.ActivateSandboxTool: {ex}");
			}
		}

		private static void ChooseToolViaReflection(ToolMenu.ToolInfo ti) {
			if (_chooseCollectionMethod == null)
				_chooseCollectionMethod = AccessTools.Method(typeof(ToolMenu), "ChooseCollection");
			if (_chooseToolMethod == null)
				_chooseToolMethod = AccessTools.Method(typeof(ToolMenu), "ChooseTool");
			_chooseCollectionMethod.Invoke(ToolMenu.Instance, new object[] { ti.collection, false });
			_chooseToolMethod.Invoke(ToolMenu.Instance, new object[] { ti });
		}
	}
}
