using System.Collections.Generic;

using OniAccess.Handlers.Screens.Starmap;

namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Handler for non-DLC StarmapScreen. Three tabs:
	/// Rockets (list/detail), Destinations (nested by distance tier),
	/// Destination Details (flat detail list with analyze action).
	///
	/// Shared state: active rocket and selected destination persist
	/// across tab switches.
	///
	/// Lifecycle: Show-patch on StarmapScreen.OnShow(bool).
	/// </summary>
	public class StarmapScreenHandler: TabbedScreenHandler {
		private enum TabId { Rockets, Destinations, Details }

		private readonly RocketsTab _rocketsTab;
		private readonly DestinationsTab _destinationsTab;
		private readonly DestinationDetailsTab _detailsTab;

		private Spacecraft _activeRocket;
		private SpaceDestination _selectedDestination;

		public StarmapScreenHandler(KScreen screen) : base(screen) {
			_rocketsTab = new RocketsTab(this);
			_destinationsTab = new DestinationsTab(this);
			_detailsTab = new DestinationDetailsTab(this);
			SetTabs(_rocketsTab, _destinationsTab, _detailsTab);
		}

		public override string DisplayName => STRINGS.ONIACCESS.STARMAP.HANDLER_NAME;

		public override bool CapturesAllInput => true;

		internal Spacecraft ActiveRocket => _activeRocket;
		internal SpaceDestination SelectedDestination => _selectedDestination;

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			base.OnActivate();

			// Detect opening context
			DetectPreSelectedRocket();
			if (_selectedDestination == null)
				DetectTelescopeTarget();

			if (_selectedDestination != null && _activeRocket == null) {
				ActiveTabIndex = (int)TabId.Details;
				_detailsTab.OnDestinationChanged();
				_detailsTab.OnTabActivated(announce: false);
			} else {
				ActiveTabIndex = (int)TabId.Rockets;
				_rocketsTab.OnTabActivated(announce: false);
			}
		}

		// ========================================
		// TAB MANAGEMENT
		// ========================================

		internal void JumpToDetailsTab() {
			DeactivateCurrentTab();
			ActiveTabIndex = (int)TabId.Details;
			_detailsTab.OnDestinationChanged();
			PlaySound("HUD_Mouseover");
			ActivateCurrentTab(announce: true);
		}

		internal void SetActiveRocket(Spacecraft rocket) {
			_activeRocket = rocket;
		}

		internal void SelectDestination(SpaceDestination dest) {
			_selectedDestination = dest;
		}

		// ========================================
		// OPENING CONTEXT
		// ========================================

		private void DetectPreSelectedRocket() {
			try {
				var selected = SelectTool.Instance?.selected;
				if (selected == null) return;

				var cmd = selected.GetComponent<CommandModule>();
				var lcm = selected.GetComponent<LaunchConditionManager>();
				if (cmd == null || lcm == null) return;

				var spacecraft = SpacecraftManager.instance
					.GetSpacecraftFromLaunchConditionManager(lcm);
				if (spacecraft == null) return;

				_activeRocket = spacecraft;
				var dest = SpacecraftManager.instance
					.GetSpacecraftDestination(lcm);
				if (dest != null)
					_selectedDestination = dest;
			} catch (System.Exception ex) {
				Util.Log.Warn(
					$"StarmapScreenHandler.DetectPreSelectedRocket: {ex}");
			}
		}

		private void DetectTelescopeTarget() {
			try {
				if (!SpacecraftManager.instance.HasAnalysisTarget()) return;
				int targetId = SpacecraftManager.instance
					.GetStarmapAnalysisDestinationID();
				var dest = SpacecraftManager.instance.GetDestination(targetId);
				if (dest != null)
					_selectedDestination = dest;
			} catch (System.Exception ex) {
				Util.Log.Warn(
					$"StarmapScreenHandler.DetectTelescopeTarget: {ex}");
			}
		}
	}
}
