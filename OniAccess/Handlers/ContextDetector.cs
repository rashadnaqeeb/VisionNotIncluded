using OniAccess.Handlers.Resources;
using OniAccess.Handlers.Screens;
using OniAccess.Handlers.Screens.Codex;

namespace OniAccess.Handlers {
	/// <summary>
	/// Static class that receives screen lifecycle events from Harmony patches and
	/// determines which handler to activate on the HandlerStack.
	///
	/// Uses a type-safe registry mapping KScreen types to handler factories.
	/// When a registered screen activates, creates and pushes a handler.
	/// When a screen deactivates, pops the handler if it matches the top of stack.
	/// Unregistered screens are silently ignored (structural UI, not interactive menus).
	///
	/// Called from ScreenLifecyclePatches (KScreen.Activate postfix, KScreen.Deactivate prefix).
	/// </summary>
	public static class ContextDetector {
		/// <summary>
		/// Registry mapping KScreen types to handler factory functions.
		/// Populated during mod initialization by concrete handler registration.
		/// </summary>
		private static readonly System.Collections.Generic.Dictionary<System.Type, System.Func<KScreen, IAccessHandler>> _registry
			= new System.Collections.Generic.Dictionary<System.Type, System.Func<KScreen, IAccessHandler>>();

		/// <summary>
		/// Screen types whose lifecycle is managed via Show patches instead of
		/// KScreen.Activate/Deactivate. These screens call Show(false) inside OnActivate
		/// during prefab init, so the generic Activate/Deactivate patches must skip them
		/// to avoid pushing zombie handlers on startup.
		/// </summary>
		private static readonly System.Collections.Generic.HashSet<System.Type> _showPatchedTypes
			= new System.Collections.Generic.HashSet<System.Type>();

		/// <summary>
		/// Returns true if the given type uses Show-patch lifecycle and should be
		/// skipped by generic KScreen.Activate/Deactivate patches.
		/// </summary>
		public static bool IsShowPatched(System.Type screenType) {
			return _showPatchedTypes.Contains(screenType);
		}

		/// <summary>
		/// Register a screen type to handler factory mapping.
		/// Generic overload for compile-time type safety.
		/// </summary>
		/// <typeparam name="TScreen">The KScreen subclass to register.</typeparam>
		/// <param name="factory">Factory function that creates a handler for the screen.</param>
		public static void Register<TScreen>(System.Func<KScreen, IAccessHandler> factory) where TScreen : KScreen {
			_registry[typeof(TScreen)] = factory;
			Util.Log.Debug($"ContextDetector.Register: {typeof(TScreen).Name}");
		}

		/// <summary>
		/// Register a screen type to handler factory mapping.
		/// Non-generic overload for runtime-resolved types (e.g., AccessTools.TypeByName).
		/// </summary>
		/// <param name="screenType">The screen type to register.</param>
		/// <param name="factory">Factory function that creates a handler for the screen.</param>
		public static void Register(System.Type screenType, System.Func<KScreen, IAccessHandler> factory) {
			if (screenType == null) {
				Util.Log.Warn("ContextDetector.Register called with null screenType");
				return;
			}
			_registry[screenType] = factory;
			Util.Log.Debug($"ContextDetector.Register: {screenType.Name}");
		}

		/// <summary>
		/// Called from Harmony postfix on KScreen.Activate.
		/// Looks up the screen type in the registry. If found, creates and pushes a handler.
		/// Unregistered screens are silently ignored (structural UI like FrontEndBackground).
		/// </summary>
		public static void OnScreenActivated(KScreen screen) {
			if (screen == null) return;

			var screenType = screen.GetType();
			if (!_registry.TryGetValue(screenType, out var factory)) {
				Util.Log.Debug($"Screen activated (no handler): {screenType.Name}");
				return;
			}

			// Clean up stale handlers before pushing. This runs in a Harmony
			// postfix, so stale handlers from the previous game may still be
			// on the stack (KeyPoller.Update hasn't run yet this frame).
			// Without this, the old handler's OnDeactivate can destroy
			// singletons (e.g. TileCursor) that the new handler just created.
			HandlerStack.RemoveStaleHandlers();

			// Guard: don't push a duplicate handler for the same screen instance
			var active = HandlerStack.ActiveHandler;
			if (active is BaseScreenHandler sh && sh.Screen == screen) {
				Util.Log.Debug($"Screen activated (already handled): {screenType.Name}");
				return;
			}

			var handler = factory(screen);
			HandlerStack.Push(handler);
			Util.Log.Debug($"Screen activated: {screenType.Name} -> pushed handler");
		}

		/// <summary>
		/// Called from Harmony prefix/postfix on screen deactivation.
		/// Pops the handler if it's on top, otherwise searches the stack and removes
		/// it from wherever it is. This handles both normal dismissal (handler on top)
		/// and buried handlers (e.g., a dialog was pushed on top before the screen closed).
		/// </summary>
		public static void OnScreenDeactivating(KScreen screen) {
			if (screen == null) return;

			var active = HandlerStack.ActiveHandler;

			// Fast path: handler is on top — use Pop for proper reactivation
			if (active is BaseScreenHandler screenHandler && screenHandler.Screen == screen) {
				HandlerStack.Pop();
				Util.Log.Debug($"Screen deactivating: {screen.GetType().Name} -> popped handler");
				return;
			}

			// Handler may be buried under other handlers — remove by screen reference
			if (HandlerStack.RemoveByScreen(screen)) {
				Util.Log.Debug($"Screen deactivating: {screen.GetType().Name} -> removed buried handler");
				return;
			}

			Util.Log.Debug($"Screen deactivating (no matching handler): {screen.GetType().Name}");
		}

		/// <summary>
		/// Register all menu screen handlers.
		/// Called during mod initialization (Mod.OnLoad).
		/// </summary>
		public static void RegisterMenuHandlers() {
			// MainMenu (direct KScreen subclass, NOT KButtonMenu)
			Register<MainMenu>(screen => new MainMenuHandler(screen));

			// PauseScreen (KModalButtonMenu)
			// Show patch pushes/pops via ContextDetector since OnActivate calls Show(false)
			Register<PauseScreen>(screen => new PauseMenuHandler(screen));
			_showPatchedTypes.Add(typeof(PauseScreen));

			// ConfirmDialogScreen (KModalScreen)
			Register<ConfirmDialogScreen>(screen => new ConfirmDialogHandler(screen));

			// OptionsMenuScreen (KModalButtonMenu -- top-level options menu)
			Register<OptionsMenuScreen>(screen => new OptionsMenuHandler(screen));

			// Options sub-screens may not have compile-time types available.
			// Use AccessTools.TypeByName for runtime resolution and the non-generic Register overload.
			foreach (var name in new[] {
				"AudioOptionsScreen", "GraphicsOptionsScreen", "GameOptionsScreen",
				"MetricsOptionsScreen", "FeedbackScreen", "CreditsScreen" }) {
				Register(HarmonyLib.AccessTools.TypeByName(name),
					screen => new OptionsMenuHandler(screen));
			}

			var inputBindingsType = HarmonyLib.AccessTools.TypeByName("InputBindingsScreen");
			Register(inputBindingsType, screen => new KeyBindingsHandler(screen));

			// RetiredColonyInfoScreen (KModalScreen -- colony summary, MENU-09)
			var retiredColonyType = HarmonyLib.AccessTools.TypeByName("RetiredColonyInfoScreen");
			Register(retiredColonyType, screen => new ColonySummaryHandler(screen));

			// ModeSelectScreen (Survival vs No Sweat -- first screen after New Game)
			var modeSelectType = HarmonyLib.AccessTools.TypeByName("ModeSelectScreen");
			Register(modeSelectType, screen => new ColonySetupHandler(screen));

			// ClusterCategorySelectionScreen (game mode select -- Survival/No Sweat/Custom)
			var clusterCategoryType = HarmonyLib.AccessTools.TypeByName("ClusterCategorySelectionScreen");
			Register(clusterCategoryType, screen => new ColonySetupHandler(screen));

			// ColonyDestinationSelectScreen (asteroid selection, settings, seed)
			Register<ColonyDestinationSelectScreen>(screen => new ColonySetupHandler(screen));

			// WorldGenScreen (world generation progress -- no widgets, just progress polling)
			var worldGenType = HarmonyLib.AccessTools.TypeByName("WorldGenScreen");
			Register(worldGenType, screen => new WorldGenHandler(screen));

			// MinionSelectScreen (CharacterSelectionController -> NewGameFlowScreen)
			Register<MinionSelectScreen>(screen => new MinionSelectHandler(screen));

			// ImmigrantScreen (Printing Pod selection, every 3 cycles)
			Register<ImmigrantScreen>(screen => new ImmigrantScreenHandler(screen));
			_showPatchedTypes.Add(typeof(ImmigrantScreen));

			// LoadScreen (KModalScreen -- save/load with two-level colony/save navigation)
			Register<LoadScreen>(screen => new SaveLoadHandler(screen));

			// SaveScreen (KModalScreen -- save game dialog from pause menu)
			Register<SaveScreen>(screen => new SaveScreenHandler(screen));

			// FileNameDialog (KModalScreen -- filename entry for new saves)
			var fileNameDialogType = HarmonyLib.AccessTools.TypeByName("FileNameDialog");
			Register(fileNameDialogType, screen => new FileNameDialogHandler(screen));

			// ModsScreen (KModalScreen -- mod management from main menu)
			Register<ModsScreen>(screen => new ModsHandler(screen));

			// LanguageOptionsScreen (KModalScreen -- language/translation selection from options)
			var langOptionsType = HarmonyLib.AccessTools.TypeByName("LanguageOptionsScreen");
			Register(langOptionsType, screen => new TranslationHandler(screen));

			// InfoDialogScreen (KModalScreen -- used by DLC toggle, mod warnings, etc.)
			var infoDialogType = HarmonyLib.AccessTools.TypeByName("InfoDialogScreen");
			Register(infoDialogType, screen => new ConfirmDialogHandler(screen));

			// CustomizableDialogScreen (KModalScreen -- multi-button dialogs, DLC toggles, mod warnings)
			var customDialogType = HarmonyLib.AccessTools.TypeByName("CustomizableDialogScreen");
			Register(customDialogType, screen => new ConfirmDialogHandler(screen));

			// GameOverScreen (KModalScreen -- colony death)
			var gameOverType = HarmonyLib.AccessTools.TypeByName("GameOverScreen");
			Register(gameOverType, screen => new ConfirmDialogHandler(screen,
				(string)STRINGS.UI.COLONYLOSTSCREEN.COLONYLOST));

			// VictoryScreen (KModalScreen -- achievement completion)
			var victoryType = HarmonyLib.AccessTools.TypeByName("VictoryScreen");
			Register(victoryType, screen => new ConfirmDialogHandler(screen,
				(string)STRINGS.UI.VICTORYSCREEN.HEADER));

			// PatchNotesScreen (KModalScreen -- post-update notes)
			var patchNotesType = HarmonyLib.AccessTools.TypeByName("PatchNotesScreen");
			Register(patchNotesType, screen => new ConfirmDialogHandler(screen,
				(string)STRINGS.UI.FRONTEND.PATCHNOTESSCREEN.HEADER));

			// LockerMenuScreen (KModalScreen -- Supply Closet hub from main menu)
			// Show patch pushes/pops via ContextDetector since OnActivate calls Show(false)
			Register<LockerMenuScreen>(screen => new LockerMenuHandler(screen));
			_showPatchedTypes.Add(typeof(LockerMenuScreen));

			// KleiItemDropScreen (KModalScreen -- cosmetic item claim/reveal)
			// Show patch pushes/pops via ContextDetector since OnActivate calls Show(false)
			Register<KleiItemDropScreen>(screen => new KleiItemDropHandler(screen));
			_showPatchedTypes.Add(typeof(KleiItemDropScreen));

			// KleiInventoryScreen (KModalScreen -- blueprint gallery in Supply Closet)
			// Show patch pushes/pops via ContextDetector since OnActivate calls OnShow(true)
			Register<KleiInventoryScreen>(screen =>
				new Screens.Inventory.InventoryScreenHandler(screen));
			_showPatchedTypes.Add(typeof(KleiInventoryScreen));

			// BarterConfirmationScreen (KModalScreen -- buy/sell confirmation dialog)
			// Dynamically instantiated; OnActivate patch pushes via ContextDetector
			Register<BarterConfirmationScreen>(screen =>
				new Screens.Inventory.BarterConfirmationHandler(screen));

			// WattsonMessage (KScreen -- welcome narrative at colony start)
			Register<WattsonMessage>(screen => new WattsonMessageHandler(screen));

			// EventInfoScreen (KModalScreen -- story trait popups and gameplay events)
			Register<EventInfoScreen>(screen => new EventInfoHandler(screen));

			// StoryMessageScreen (KScreen -- victory sequence story popup)
			Register<StoryMessageScreen>(screen => new StoryMessageHandler(screen));

			// VideoScreen (KModalScreen -- victory cinematics and intro videos)
			// Show patch pushes/pops via ContextDetector since OnActivate calls Show(false)
			Register<VideoScreen>(screen => new VideoScreenHandler(screen));
			_showPatchedTypes.Add(typeof(VideoScreen));

			// Hud (KScreen -- game world HUD, tile cursor navigation)
			Register<Hud>(screen => new Tiles.TileCursorHandler(screen));

			// DetailsScreen (entity inspection panel -- show/hide lifecycle)
			// Show patch pushes/pops via ContextDetector since OnPrefabInit calls Show(false)
			Register<DetailsScreen>(screen => new DetailsScreenHandler(screen));
			_showPatchedTypes.Add(typeof(DetailsScreen));

			// JobsTableScreen (duplicant priority management -- 2D grid)
			// Show patch pushes/pops via ContextDetector since ManagementMenu uses Show()
			Register<JobsTableScreen>(screen => new PriorityScreenHandler(screen));
			_showPatchedTypes.Add(typeof(JobsTableScreen));

			// VitalsTableScreen (duplicant health stats -- 2D grid)
			// Show patch pushes/pops via ContextDetector since ManagementMenu uses Show()
			Register<VitalsTableScreen>(screen => new VitalsScreenHandler(screen));
			_showPatchedTypes.Add(typeof(VitalsTableScreen));

			// ConsumablesTableScreen (duplicant food/medicine permissions -- 2D grid)
			// Show patch pushes/pops via ContextDetector since ManagementMenu uses Show()
			Register<ConsumablesTableScreen>(screen => new ConsumablesScreenHandler(screen));
			_showPatchedTypes.Add(typeof(ConsumablesTableScreen));

			// ResearchScreen (KModalScreen -- research management)
			// Show patch pushes/pops via ContextDetector since ManagementMenu uses Show()
			Register<ResearchScreen>(screen => new ResearchScreenHandler(screen));
			_showPatchedTypes.Add(typeof(ResearchScreen));

			// SkillsScreen (KModalScreen -- duplicant skill management)
			// Show patch pushes/pops via ContextDetector since ManagementMenu uses Show()
			Register<SkillsScreen>(screen => new SkillsScreenHandler(screen));
			_showPatchedTypes.Add(typeof(SkillsScreen));

			// ScheduleScreen (KScreen -- duplicant schedule management)
			// Show patch pushes/pops via ContextDetector since ManagementMenu uses Show()
			Register<ScheduleScreen>(screen => new ScheduleScreenHandler(screen));
			_showPatchedTypes.Add(typeof(ScheduleScreen));

			// ReportScreen (KScreen -- daily reports)
			// Show patch pushes/pops via ContextDetector since ManagementMenu uses Show()
			Register<ReportScreen>(screen => new ReportScreenHandler(screen));
			_showPatchedTypes.Add(typeof(ReportScreen));

			// CodexScreen (KScreen -- in-game Database/Incyclopedia)
			// Show patch pushes/pops via ContextDetector since ManagementMenu uses Show()
			Register<CodexScreen>(screen => new CodexScreenHandler(screen));
			_showPatchedTypes.Add(typeof(CodexScreen));

			// AllResourcesScreen (ShowOptimizedKScreen -- full resource list)
			// Show patch pushes/pops via ContextDetector since it uses Show() toggle
			Register<AllResourcesScreen>(screen => new ResourceBrowserHandler(screen));
			_showPatchedTypes.Add(typeof(AllResourcesScreen));

			// AllDiagnosticsScreen (ShowOptimizedKScreen -- full diagnostics panel)
			// Show patch pushes/pops via ContextDetector since it uses Show() toggle
			Register<AllDiagnosticsScreen>(screen =>
				new Screens.DiagnosticBrowserHandler(screen));
			_showPatchedTypes.Add(typeof(AllDiagnosticsScreen));

			// MessageDialogFrame (KScreen -- message popup from notification click)
			// OnActivate is declared on MessageDialogFrame, so generic KScreen_Activate_Patch fires.
			Register<MessageDialogFrame>(screen =>
				new Notifications.MessageDialogFrameHandler(screen));

			// StarmapScreen (KModalScreen -- non-DLC starmap/rocket management)
			// Show patch pushes/pops via ContextDetector since ManagementMenu uses Show()
			Register<StarmapScreen>(screen => new StarmapScreenHandler(screen));
			_showPatchedTypes.Add(typeof(StarmapScreen));

			// ClusterMapScreen (KScreen -- DLC starmap/hex grid)
			// Show patch pushes/pops via ContextDetector since ManagementMenu uses Show()
			Register<ClusterMapScreen>(screen =>
				new Screens.ClusterMap.ClusterMapHandler(screen));
			_showPatchedTypes.Add(typeof(ClusterMapScreen));

			Util.Log.Debug("ContextDetector.RegisterMenuHandlers: Phase 3 handlers registered");
		}

		/// <summary>
		/// Detect current game state and activate the appropriate handler.
		/// Called when mod is toggled ON to determine what handler should be active.
		///
		/// Uses FindObjectsOfType to discover all live KScreens in the scene,
		/// then sorts by KScreenManager.screenStack position for layering order.
		/// </summary>
		public static void DetectAndActivate() {
			if (HandlerStack.Count > 0) return;
			Util.Log.Debug("ContextDetector.DetectAndActivate called");

			// Find all live KScreens in the scene (ground truth, not a private stack)
			var allScreens = UnityEngine.Object.FindObjectsOfType<KScreen>();

			// Filter to registered, visible screens
			var matches = new System.Collections.Generic.List<KScreen>();
			foreach (var screen in allScreens) {
				if (!screen.IsScreenActive()) continue;
				if (!_registry.ContainsKey(screen.GetType())) continue;
				matches.Add(screen);
			}

			if (matches.Count > 0) {
				System.Collections.Generic.List<KScreen> screenStack = null;
				if (KScreenManager.Instance != null) {
					try {
						screenStack = HarmonyLib.Traverse.Create(KScreenManager.Instance)
							.Field<System.Collections.Generic.List<KScreen>>("screenStack").Value;
					} catch (System.Exception ex) {
						Util.Log.Warn($"DetectAndActivate: screenStack read failed: {ex.Message}");
					}
					if (screenStack == null)
						Util.Log.Warn("DetectAndActivate: screenStack field not found; handler order may be wrong");
				}

				// Build handlers, then sort: non-capturing handlers (e.g.
				// TileCursorHandler) push first (bottom of stack), capturing
				// handlers (menus) push last (top). During normal play Harmony
				// patches push one at a time so menus naturally land on top;
				// when reconstructing we must enforce that order explicitly.
				// Within each group, screenStack position breaks ties.
				var handlers = new System.Collections.Generic.List<(KScreen screen, IAccessHandler handler)>();
				foreach (var screen in matches)
					handlers.Add((screen, _registry[screen.GetType()](screen)));

				if (handlers.Count > 1) {
					handlers.Sort((a, b) => {
						int capA = a.handler.CapturesAllInput ? 1 : 0;
						int capB = b.handler.CapturesAllInput ? 1 : 0;
						if (capA != capB) return capA.CompareTo(capB);
						if (screenStack != null) {
							int idxA = screenStack.IndexOf(a.screen);
							int idxB = screenStack.IndexOf(b.screen);
							if (idxA < 0) idxA = -1;
							if (idxB < 0) idxB = -1;
							return idxA.CompareTo(idxB);
						}
						return 0;
					});
				}

				HandlerStack.Push(new BaselineHandler());
				foreach (var entry in handlers) {
					HandlerStack.Push(entry.handler);
					Util.Log.Debug($"DetectAndActivate: pushed handler for {entry.screen.GetType().Name}");
				}
				return;
			}

			// Nothing matched — push BaselineHandler as fallback
			HandlerStack.Push(new BaselineHandler());
		}
	}
}
