using OniAccess.ConduitTracking;

namespace OniAccess.Audio {
	public class SonifierController {
		public static SonifierController Instance { get; private set; }

		private enum SonifierMode { None, Conduit, Power }

		private int _activeCell = Grid.InvalidCell;
		private SonifierMode _activeMode = SonifierMode.None;
		private ConduitType _activeConduitType = ConduitType.None;

		public SonifierController() {
			Instance = this;
		}

		public void OnCursorMoved(int cell, HashedString overlayMode) {
			if (!ConfigManager.Config.FlowSonification) {
				Deactivate();
				return;
			}

			if (!Grid.IsValidCell(cell)) {
				Deactivate();
				return;
			}

			if (overlayMode == OverlayModes.Power.ID) {
				if (Game.Instance.electricalConduitSystem.GetNetworkForCell(cell) == null) {
					Deactivate();
					return;
				}
				_activeCell = cell;
				_activeMode = SonifierMode.Power;
				_activeConduitType = ConduitType.None;
				SampleAndUpdate();
				return;
			}

			var conduitType = GetConduitType(overlayMode);
			if (conduitType == ConduitType.None || !HasConduit(conduitType, cell)) {
				Deactivate();
				return;
			}

			_activeCell = cell;
			_activeMode = SonifierMode.Conduit;
			_activeConduitType = conduitType;
			SampleAndUpdate();
		}

		public void OnOverlayChanged(HashedString newMode) {
			if (newMode != OverlayModes.Power.ID && GetConduitType(newMode) == ConduitType.None)
				Deactivate();
		}

		public void Tick() {
			if (_activeMode == SonifierMode.None) return;
			SampleAndUpdate();
		}

		public void Stop() {
			Deactivate();
		}

		private void Deactivate() {
			_activeCell = Grid.InvalidCell;
			_activeMode = SonifierMode.None;
			_activeConduitType = ConduitType.None;
			Sonifier.Instance.Stop();
		}

		private void SampleAndUpdate() {
			if (_activeMode == SonifierMode.Power) {
				SamplePower();
				return;
			}
			if (_activeConduitType == ConduitType.Solid) {
				SampleRail();
				return;
			}
			var flow = GetConduitFlow(_activeConduitType);
			var contents = flow.GetContents(_activeCell);
			if (contents.mass <= 0f
					&& BridgeFlowCapture.TryGetOutputCell(
						_activeCell, out int outputCell,
						out ConduitType bridgeType)
					&& bridgeType == _activeConduitType) {
				contents = flow.GetContents(outputCell);
			}
			float maxMass = GetMaxMass(_activeConduitType);
			float fillRatio = UnityEngine.Mathf.Clamp01(contents.mass / maxMass);
			bool hasContents = contents.mass > 0f;
			Sonifier.Instance.UpdateTone(fillRatio, hasContents);
		}

		private void SamplePower() {
			ushort circuitID = Game.Instance.circuitManager.GetCircuitID(_activeCell);
			if (circuitID == ushort.MaxValue) {
				Sonifier.Instance.UpdateTone(0f, false);
				return;
			}
			float wattsUsed = Game.Instance.circuitManager.GetWattsUsedByCircuit(circuitID);
			float maxWatts = Game.Instance.circuitManager.GetMaxSafeWattageForCircuit(circuitID);
			float fillRatio = maxWatts > 0f ? wattsUsed / maxWatts : 0f;
			Sonifier.Instance.UpdateTone(fillRatio, wattsUsed > 0f);
		}

		// A rail cell holds at most one item; pitch follows its mass against the
		// largest chunk a Conveyor Loader puts down.
		private void SampleRail() {
			var flow = Game.Instance.solidConduitFlow;
			// Null when the cell is empty or its item was destroyed in transit.
			var pickupable = flow.GetPickupable(flow.GetContents(_activeCell).pickupableHandle);
			if (pickupable == null) {
				Sonifier.Instance.UpdateTone(0f, false);
				return;
			}
			float mass = pickupable.PrimaryElement.Mass;
			float fillRatio = UnityEngine.Mathf.Clamp01(mass / SolidConduitFlow.MAX_SOLID_MASS);
			Sonifier.Instance.UpdateTone(fillRatio, true);
		}

		private static ConduitType GetConduitType(HashedString overlayMode) {
			if (overlayMode == OverlayModes.LiquidConduits.ID)
				return ConduitType.Liquid;
			if (overlayMode == OverlayModes.GasConduits.ID)
				return ConduitType.Gas;
			if (overlayMode == OverlayModes.SolidConveyor.ID)
				return ConduitType.Solid;
			return ConduitType.None;
		}

		private static bool HasConduit(ConduitType type, int cell) {
			if (type == ConduitType.Solid)
				return Game.Instance.solidConduitFlow.HasConduit(cell);
			return GetConduitFlow(type).HasConduit(cell);
		}

		private static ConduitFlow GetConduitFlow(ConduitType type) {
			switch (type) {
				case ConduitType.Liquid: return Game.Instance.liquidConduitFlow;
				case ConduitType.Gas: return Game.Instance.gasConduitFlow;
				default: return null;
			}
		}

		private static float GetMaxMass(ConduitType type) {
			switch (type) {
				case ConduitType.Liquid: return ConduitFlow.MAX_LIQUID_MASS;
				case ConduitType.Gas: return ConduitFlow.MAX_GAS_MASS;
				default: return 1f;
			}
		}
	}
}
