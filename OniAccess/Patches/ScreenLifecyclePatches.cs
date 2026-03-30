using HarmonyLib;
using OniAccess.Handlers;
using OniAccess.Handlers.Screens;
using OniAccess.Handlers.Screens.Codex;
using OniAccess.Handlers.Screens.Inventory;
using OniAccess.Handlers.Screens.Outfits;
using OniAccess.Speech;
using OniAccess.Util;
using UnityEngine;

namespace OniAccess.Patches {
	/// <summary>
	/// Harmony patches for KScreen lifecycle events (activate, deactivate, show/hide).
	/// These feed screen transitions into ContextDetector for handler switching.
	///
	/// KScreen_Activate_Patch: Fires context detection when screens open.
	/// KScreen_Deactivate_Patch: Fires context detection when screens close (Prefix because
	/// Deactivate calls PopScreen then Destroy).
	///
	/// Show/OnShow patches: Some screens call Show(false) during prefab init, which means
	/// KScreen.Activate/Deactivate hooks don't fire for user-visible show/hide transitions.
	/// These patches dispatch to ContextDetector via DispatchShowEvent instead.
	/// Whether to patch Show or OnShow depends on the screen — KModalScreen subclasses
	/// that override Show use Show; screens that only override OnShow use OnShow.
	/// </summary>

	/// <summary>
	/// Shared dispatch for Show/OnShow postfixes. Pushes or pops the handler
	/// via ContextDetector based on the show flag.
	/// </summary>
	static class ShowDispatch {
		internal static void Handle(KScreen instance, bool show) {
			if (!ModToggle.IsEnabled) return;
			if (show)
				ContextDetector.OnScreenActivated(instance);
			else
				ContextDetector.OnScreenDeactivating(instance);
		}
	}

	/// <summary>
	/// Detect screen activations for context-aware handler switching.
	/// Postfix: fires after KScreen.Activate completes (screen is now on the stack).
	/// </summary>
	[HarmonyPatch(typeof(KScreen), nameof(KScreen.Activate))]
	internal static class KScreen_Activate_Patch {
		private static void Postfix(KScreen __instance) {
			if (!ModToggle.IsEnabled) return;
			// Skip screens managed via Show patches -- their OnActivate calls Show(false)
			// during prefab init, so this postfix would push a zombie handler.
			if (ContextDetector.IsShowPatched(__instance.GetType())) return;
			ContextDetector.OnScreenActivated(__instance);
		}
	}

	/// <summary>
	/// Detect screen deactivations for context-aware handler switching.
	/// Prefix: fires BEFORE KScreen.Deactivate because Deactivate calls PopScreen then Destroy.
	/// </summary>
	[HarmonyPatch(typeof(KScreen), nameof(KScreen.Deactivate))]
	internal static class KScreen_Deactivate_Patch {
		private static void Prefix(KScreen __instance) {
			if (!ModToggle.IsEnabled) return;
			if (ContextDetector.IsShowPatched(__instance.GetType())) return;
			ContextDetector.OnScreenDeactivating(__instance);
		}
	}

	/// <summary>
	/// Guard against NullReferenceException in KModalButtonMenu.Unhide.
	/// When a child screen's Close event fires after the parent is already destroyed,
	/// panelRoot is null and the original Unhide crashes. Skip the call if panelRoot is null.
	/// </summary>
	[HarmonyPatch(typeof(KModalButtonMenu), "Unhide")]
	internal static class KModalButtonMenu_Unhide_Patch {
		private static bool Prefix(KModalButtonMenu __instance) {
			try {
				var panelRoot = Traverse.Create(__instance).Field<GameObject>("panelRoot").Value;
				if (panelRoot == null) {
					Log.Debug("KModalButtonMenu.Unhide skipped: panelRoot is null (screen already destroyed)");
					return false;
				}
			} catch (System.Exception ex) {
				// Traverse failed (field renamed?). Fall through to let original Unhide run.
				Log.Warn($"KModalButtonMenu_Unhide_Patch: Traverse failed, skipping guard: {ex.Message}");
			}
			return true;
		}
	}

	/// Patch Show — OnActivate calls Show(false) during prefab init.
	[HarmonyPatch(typeof(LockerMenuScreen), nameof(LockerMenuScreen.Show))]
	internal static class LockerMenuScreen_Show_Patch {
		private static void Postfix(KScreen __instance, bool show) =>
			ShowDispatch.Handle(__instance, show);
	}

	/// Same pattern as LockerMenuScreen.
	[HarmonyPatch(typeof(KleiItemDropScreen), nameof(KleiItemDropScreen.Show))]
	internal static class KleiItemDropScreen_Show_Patch {
		private static void Postfix(KScreen __instance, bool show) =>
			ShowDispatch.Handle(__instance, show);
	}

	/// KleiInventoryScreen lifecycle is managed by LockerNavigator via SetActive,
	/// not Show(). OnShow fires on first open (OnActivate → OnShow(true)), but
	/// re-opens only fire OnCmpEnable. We patch both: OnShow for first open,
	/// OnCmpEnable (guarded by IsActive) for re-opens, and OnCmpDisable for pop.
	[HarmonyPatch(typeof(KleiInventoryScreen), "OnShow")]
	internal static class KleiInventoryScreen_OnShow_Patch {
		private static void Postfix(KScreen __instance, bool show) =>
			ShowDispatch.Handle(__instance, show);
	}

	/// Re-open: LockerNavigator.PushScreen calls SetActive(true), which fires
	/// OnCmpEnable but not OnShow. Guard with IsActive() — false on first open
	/// (Activate hasn't run yet), true on re-opens.
	[HarmonyPatch(typeof(KleiInventoryScreen), "OnCmpEnable")]
	internal static class KleiInventoryScreen_OnCmpEnable_Patch {
		private static void Postfix(KScreen __instance) {
			if (!ModToggle.IsEnabled) return;
			if (!__instance.IsActive()) return;
			ContextDetector.OnScreenActivated(__instance);
		}
	}

	/// Dismiss: LockerNavigator.PopScreen calls SetActive(false), which fires
	/// OnCmpDisable but not OnShow. KleiInventoryScreen doesn't declare
	/// OnCmpDisable, so patch the declaring type (KModalScreen) and filter.
	[HarmonyPatch(typeof(KModalScreen), "OnCmpDisable")]
	internal static class KleiInventoryScreen_OnCmpDisable_Patch {
		private static void Postfix(KScreen __instance) {
			if (__instance is KleiInventoryScreen)
				ContextDetector.OnScreenDeactivating(__instance);
		}
	}

	/// BarterConfirmationScreen is dynamically instantiated, not pushed via
	/// the KScreen stack. Patch OnActivate to push our handler when it appears.
	[HarmonyPatch(typeof(BarterConfirmationScreen), "OnActivate")]
	internal static class BarterConfirmationScreen_OnActivate_Patch {
		private static void Postfix(KScreen __instance) {
			if (!ModToggle.IsEnabled) return;
			ContextDetector.OnScreenActivated(__instance);
		}
	}

	/// Patch ShowResultPanel to notify BarterConfirmationHandler of transaction result.
	[HarmonyPatch(typeof(BarterConfirmationScreen), "ShowResultPanel")]
	internal static class BarterConfirmationScreen_ShowResultPanel_Patch {
		private static void Postfix(BarterConfirmationScreen __instance, bool transationResult) {
			if (!ModToggle.IsEnabled) return;
			if (HandlerStack.ActiveHandler is BarterConfirmationHandler handler
				&& handler.Screen == __instance) {
				handler.OnTransactionResult(transationResult);
			}
		}
	}

	/// Notify KleiItemDropHandler when an item is presented for reveal.
	/// On first open, this fires inside Show() before the handler is pushed,
	/// so we store the item in static pending fields for OnActivate to consume.
	[HarmonyPatch(typeof(KleiItemDropScreen), nameof(KleiItemDropScreen.PresentItem))]
	internal static class KleiItemDropScreen_PresentItem_Patch {
		private static void Postfix(KleiItemDropScreen __instance, KleiItems.ItemData item, bool firstItemPresentation) {
			if (!ModToggle.IsEnabled) return;
			if (HandlerStack.ActiveHandler is KleiItemDropHandler handler && handler.Screen == __instance) {
				handler.OnItemPresented(item, firstItemPresentation);
			} else {
				KleiItemDropHandler.PendingItem = item;
				KleiItemDropHandler.HasPendingItem = true;
			}
		}
	}

	/// Notify KleiItemDropHandler when the server responds to a reveal request.
	[HarmonyPatch(typeof(KleiItemDropScreen), nameof(KleiItemDropScreen.OnOpenItemRequestResponse))]
	internal static class KleiItemDropScreen_OnOpenItemRequestResponse_Patch {
		private static void Postfix(KleiItemDropScreen __instance, KleiItems.Result result) {
			if (!ModToggle.IsEnabled) return;
			if (HandlerStack.ActiveHandler is KleiItemDropHandler handler && handler.Screen == __instance) {
				handler.OnRevealResponse(result.Success);
			}
		}
	}

	/// Notify KleiItemDropHandler when no items are available.
	/// On first open with no items, this fires inside Show() before the handler
	/// is pushed, so we set a pending flag for OnActivate to consume.
	[HarmonyPatch(typeof(KleiItemDropScreen), nameof(KleiItemDropScreen.PresentNoItemAvailablePrompt))]
	internal static class KleiItemDropScreen_PresentNoItemAvailablePrompt_Patch {
		private static void Postfix(KleiItemDropScreen __instance) {
			if (!ModToggle.IsEnabled) return;
			if (HandlerStack.ActiveHandler is KleiItemDropHandler handler && handler.Screen == __instance) {
				handler.OnNoItemAvailable();
			} else {
				KleiItemDropHandler.HasPendingNoItem = true;
			}
		}
	}

	/// Patch OnShow — PauseScreen overrides OnShow, not Show.
	[HarmonyPatch(typeof(PauseScreen), "OnShow")]
	internal static class PauseScreen_OnShow_Patch {
		private static void Postfix(KScreen __instance, bool show) =>
			ShowDispatch.Handle(__instance, show);
	}

	/// Same pattern as PauseScreen.
	[HarmonyPatch(typeof(VideoScreen), "OnShow")]
	internal static class VideoScreen_OnShow_Patch {
		private static void Postfix(KScreen __instance, bool show) =>
			ShowDispatch.Handle(__instance, show);
	}

	/// <summary>
	/// TableScreen subclasses (JobsTableScreen, ConsumablesTableScreen) extend
	/// ShowOptimizedKScreen, which hides via canvas alpha in Show(false) without
	/// calling Deactivate. ManagementMenu toggles them via Show(). Patch
	/// TableScreen.OnShow and let ContextDetector filter by registration.
	/// </summary>
	[HarmonyPatch(typeof(TableScreen), "OnShow")]
	internal static class TableScreen_OnShow_Patch {
		private static void Postfix(KScreen __instance, bool show) =>
			ShowDispatch.Handle(__instance, show);
	}

	/// <summary>
	/// DetailsScreen.OnPrefabInit() calls Show(false) during init, so
	/// KScreen.Activate/Deactivate patches do not fire for user-visible show/hide.
	/// Push-only: handler removal is handled by OnCmpDisable below.
	/// </summary>
	[HarmonyPatch(typeof(DetailsScreen), "OnShow")]
	internal static class DetailsScreen_OnShow_Patch {
		private static void Postfix(DetailsScreen __instance, bool show) {
			if (!ModToggle.IsEnabled) return;
			if (show) {
				ContextDetector.OnScreenActivated(__instance);
			}
		}
	}

	/// <summary>
	/// RootMenu.CloseSubMenus() hides DetailsScreen via gameObject.SetActive(false),
	/// bypassing Show(false) entirely. OnCmpDisable fires for both SetActive(false)
	/// and Show(false), so it catches all hiding paths.
	/// </summary>
	[HarmonyPatch(typeof(DetailsScreen), "OnCmpDisable")]
	internal static class DetailsScreen_OnCmpDisable_Patch {
		private static void Postfix(DetailsScreen __instance) {
			if (!ModToggle.IsEnabled) return;
			ContextDetector.OnScreenDeactivating(__instance);
		}
	}

	/// ImmigrantScreen declares OnShow directly. Patch it for show/hide lifecycle.
	[HarmonyPatch(typeof(ImmigrantScreen), "OnShow")]
	internal static class ImmigrantScreen_OnShow_Patch {
		private static void Postfix(KScreen __instance, bool show) =>
			ShowDispatch.Handle(__instance, show);
	}

	/// <summary>
	/// MinionSelectScreen.OnSpawn() does not call base.OnSpawn(), so
	/// KScreen.Activate() is never invoked. Patch OnSpawn directly.
	/// </summary>
	[HarmonyPatch(typeof(MinionSelectScreen), "OnSpawn")]
	internal static class MinionSelectScreen_OnSpawn_Patch {
		private static void Postfix(MinionSelectScreen __instance) {
			if (!ModToggle.IsEnabled) return;
			ContextDetector.OnScreenActivated(__instance);
		}
	}

	/// <summary>
	/// ResearchScreen extends KModalScreen whose OnActivate calls OnShow(true)
	/// during prefab init. Patch Show (not OnShow) to avoid that init path.
	/// </summary>
	[HarmonyPatch(typeof(ResearchScreen), nameof(ResearchScreen.Show))]
	internal static class ResearchScreen_Show_Patch {
		private static void Postfix(KScreen __instance, bool show) =>
			ShowDispatch.Handle(__instance, show);
	}

	/// Patch OnShow — SkillsScreen overrides OnShow, not Show.
	[HarmonyPatch(typeof(SkillsScreen), "OnShow")]
	internal static class SkillsScreen_OnShow_Patch {
		private static void Postfix(KScreen __instance, bool show) =>
			ShowDispatch.Handle(__instance, show);
	}

	/// ScheduleScreen extends KScreen (not KModalScreen). ManagementMenu toggles
	/// it via Show(), which calls OnShow(), without going through Activate/Deactivate.
	[HarmonyPatch(typeof(ScheduleScreen), "OnShow")]
	internal static class ScheduleScreen_OnShow_Patch {
		private static void Postfix(KScreen __instance, bool show) =>
			ShowDispatch.Handle(__instance, show);
	}

	/// ReportScreen extends KScreen. ManagementMenu toggles it via Show(),
	/// which calls OnShow(). ReportScreen declares OnShow, so patch it directly.
	[HarmonyPatch(typeof(ReportScreen), "OnShow")]
	internal static class ReportScreen_OnShow_Patch {
		private static void Postfix(KScreen __instance, bool bShow) =>
			ShowDispatch.Handle(__instance, bShow);
	}

	/// <summary>
	/// RetiredColonyInfoScreen reuses its instance via Show(true) on subsequent opens,
	/// so KScreen.Activate never fires again. The duplicate guard in OnScreenActivated
	/// prevents double-pushing when both Activate and Show(true) fire on first open.
	/// </summary>
	[HarmonyPatch(typeof(RetiredColonyInfoScreen), nameof(RetiredColonyInfoScreen.Show))]
	internal static class RetiredColonyInfoScreen_Show_Patch {
		private static void Postfix(KScreen __instance, bool show) =>
			ShowDispatch.Handle(__instance, show);
	}

	/// AllResourcesScreen declares OnShow directly (ShowOptimizedKScreen subclass).
	/// ManagementMenu toggles it via Show(), which calls OnShow().
	[HarmonyPatch(typeof(AllResourcesScreen), "OnShow")]
	internal static class AllResourcesScreen_OnShow_Patch {
		private static void Postfix(KScreen __instance, bool show) =>
			ShowDispatch.Handle(__instance, show);
	}

	/// AllDiagnosticsScreen declares OnShow directly (ShowOptimizedKScreen subclass).
	/// Opened via sidebar "See All" button or mod's Shift+D hotkey.
	[HarmonyPatch(typeof(AllDiagnosticsScreen), "OnShow")]
	internal static class AllDiagnosticsScreen_OnShow_Patch {
		private static void Postfix(KScreen __instance, bool show) =>
			ShowDispatch.Handle(__instance, show);
	}

	/// CodexScreen extends KScreen directly. ManagementMenu toggles it via Show().
	/// CodexScreen does not override Show or OnShow, so we must target the base
	/// KScreen.OnShow where the method actually lives and filter by instance type.
	[HarmonyPatch(typeof(KScreen), "OnShow")]
	internal static class CodexScreen_OnShow_Patch {
		private static void Postfix(KScreen __instance, bool show) {
			if (__instance is CodexScreen)
				ShowDispatch.Handle(__instance, show);
		}
	}

	/// Postfix on ChangeArticle to notify the handler when the article changes.
	/// Fires for user navigation, deep links, and unlock-triggered refreshes.
	[HarmonyPatch(typeof(CodexScreen), nameof(CodexScreen.ChangeArticle))]
	internal static class CodexScreen_ChangeArticle_Patch {
		private static void Postfix(CodexScreen __instance) {
			if (!ModToggle.IsEnabled) return;
			var active = HandlerStack.ActiveHandler;
			if (active is CodexScreenHandler handler && handler.Screen == __instance) {
				handler.OnArticleChanged();
			}
		}
	}

	/// <summary>
	/// ReportErrorDialog is a plain MonoBehaviour (not a KScreen), so normal
	/// lifecycle patches don't detect it. Push the error handler when it starts.
	/// </summary>
	[HarmonyPatch(typeof(ReportErrorDialog), "Start")]
	internal static class ReportErrorDialog_Start_Patch {
		private static void Postfix(ReportErrorDialog __instance) {
			if (!ModToggle.IsEnabled) return;
			HandlerStack.Push(new ErrorScreenHandler(__instance));
		}
	}

	/// <summary>
	/// Pop the error handler when the dialog is destroyed.
	/// Prefix so it fires before Unity cleanup.
	/// </summary>
	[HarmonyPatch(typeof(ReportErrorDialog), "OnDestroy")]
	internal static class ReportErrorDialog_OnDestroy_Patch {
		private static void Prefix(ReportErrorDialog __instance) {
			if (!ModToggle.IsEnabled) return;
			if (HandlerStack.ActiveHandler is ErrorScreenHandler h && h.Dialog == __instance)
				HandlerStack.Pop();
		}
	}

	/// StarmapScreen declares OnShow directly. Patch it for show/hide lifecycle.
	[HarmonyPatch(typeof(StarmapScreen), "OnShow")]
	internal static class StarmapScreen_OnShow_Patch {
		private static void Postfix(KScreen __instance, bool show) =>
			ShowDispatch.Handle(__instance, show);
	}

	/// ClusterMapScreen declares OnShow directly. Patch it for show/hide lifecycle.
	[HarmonyPatch(typeof(ClusterMapScreen), "OnShow")]
	internal static class ClusterMapScreen_OnShow_Patch {
		private static void Postfix(KScreen __instance, bool show) =>
			ShowDispatch.Handle(__instance, show);
	}

	/// <summary>
	/// When a side screen triggers destination selection, ensure ClusterMapHandler
	/// is on the stack and announce the mode. Handles two scenarios:
	/// - Map not open: OnShow patch already pushed the handler, just announce.
	/// - Map already open: no OnShow fires, so push the handler manually.
	/// </summary>
	[HarmonyPatch(typeof(ClusterMapScreen), nameof(ClusterMapScreen.ShowInSelectDestinationMode))]
	internal static class ClusterMapScreen_ShowInSelectDestinationMode_Patch {
		private static void Postfix(ClusterMapScreen __instance) {
			if (!ModToggle.IsEnabled) return;

			DetailsScreenHandler.PreserveNavigationOnReactivate = true;

			var active = HandlerStack.ActiveHandler;
			if (active is OniAccess.Handlers.Screens.ClusterMap.ClusterMapHandler) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.CLUSTER_MAP.SELECT_DESTINATION);
				return;
			}

			// Map was already open — force close-on-select so the map closes
			// after selection, returning the player to the side screen.
			try {
				AccessTools.Field(typeof(ClusterMapScreen), "m_closeOnSelect")
					.SetValue(__instance, true);
			} catch (System.Exception ex) {
				Util.Log.Error($"ClusterMapScreen_ShowInSelectDestinationMode_Patch: {ex}");
			}

			HandlerStack.RemoveByScreen(__instance);
			ContextDetector.OnScreenActivated(__instance);
		}
	}

	/// OutfitBrowserScreen extends KMonoBehaviour (not KScreen), so it
	/// cannot go through ContextDetector. LockerNavigator manages it via
	/// SetActive, which fires OnCmpEnable/OnCmpDisable.
	[HarmonyPatch(typeof(OutfitBrowserScreen), "OnCmpEnable")]
	internal static class OutfitBrowserScreen_OnCmpEnable_Patch {
		private static void Postfix(OutfitBrowserScreen __instance) {
			if (!ModToggle.IsEnabled) return;
			if (!__instance.Config.isValid) return; // not yet configured
			// Configure() calls SetActive(false)/SetActive(true) on re-open,
			// which fires OnCmpDisable then OnCmpEnable — skip if already active
			if (HandlerStack.ActiveHandler is OutfitBrowserHandler h
				&& h.BrowserScreen == __instance) return;
			HandlerStack.Push(new OutfitBrowserHandler(__instance));
		}
	}

	/// OutfitBrowserScreen does not declare OnCmpDisable, so patch the
	/// declaring type (KMonoBehaviour) and filter by instance type.
	[HarmonyPatch(typeof(KMonoBehaviour), "OnCmpDisable")]
	internal static class OutfitBrowserScreen_OnCmpDisable_Patch {
		private static void Postfix(KMonoBehaviour __instance) {
			if (!(__instance is OutfitBrowserScreen screen)) return;
			if (!ModToggle.IsEnabled) return;
			if (HandlerStack.ActiveHandler is OutfitBrowserHandler handler
				&& handler.BrowserScreen == screen)
				HandlerStack.Pop();
		}
	}

	/// OutfitDesignerScreen extends KMonoBehaviour (not KScreen), same
	/// lifecycle pattern as OutfitBrowserScreen.
	[HarmonyPatch(typeof(OutfitDesignerScreen), "OnCmpEnable")]
	internal static class OutfitDesignerScreen_OnCmpEnable_Patch {
		private static void Postfix(OutfitDesignerScreen __instance) {
			if (!ModToggle.IsEnabled) return;
			if (!__instance.Config.isValid) return; // not yet configured
			HandlerStack.Push(new OutfitDesignerHandler(__instance));
		}
	}

	[HarmonyPatch(typeof(OutfitDesignerScreen), "OnCmpDisable")]
	internal static class OutfitDesignerScreen_OnCmpDisable_Patch {
		private static void Postfix(OutfitDesignerScreen __instance) {
			if (!ModToggle.IsEnabled) return;
			if (HandlerStack.ActiveHandler is OutfitDesignerHandler handler
				&& handler.DesignerScreen == __instance)
				HandlerStack.Pop();
		}
	}
}
