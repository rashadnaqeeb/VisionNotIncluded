using System.Collections.Generic;

namespace OniAccess.Handlers.Tiles.Skip {
	/// <summary>
	/// Maps overlay mode IDs to skip strategies.
	/// Falls back to DefaultSkipStrategy for unmapped overlays.
	/// </summary>
	public sealed class SkipStrategyRegistry {
		private readonly Dictionary<HashedString, ISkipStrategy> _strategies
			= new Dictionary<HashedString, ISkipStrategy>();
		private readonly ISkipStrategy _default = new DefaultSkipStrategy();

		public void Register(HashedString modeId, ISkipStrategy strategy) {
			_strategies[modeId] = strategy;
		}

		public ISkipStrategy GetStrategy(HashedString modeId) {
			if (_strategies.TryGetValue(modeId, out var strategy))
				return strategy;
			return _default;
		}

		public static SkipStrategyRegistry Build() {
			var registry = new SkipStrategyRegistry();

			registry.Register(OverlayModes.Oxygen.ID,
				new GasSkipStrategy());

			registry.Register(OverlayModes.Power.ID,
				new UtilitySkipStrategy(
					() => Game.Instance.electricalConduitSystem,
					ObjectLayer.ReplacementWire,
					new[] { (int)ObjectLayer.Wire, (int)ObjectLayer.WireConnectors }));

			registry.Register(OverlayModes.LiquidConduits.ID,
				new UtilitySkipStrategy(
					() => Game.Instance.liquidConduitSystem,
					ObjectLayer.ReplacementLiquidConduit,
					new[] { (int)ObjectLayer.LiquidConduit, (int)ObjectLayer.LiquidConduitConnection }));

			registry.Register(OverlayModes.GasConduits.ID,
				new UtilitySkipStrategy(
					() => Game.Instance.gasConduitSystem,
					ObjectLayer.ReplacementGasConduit,
					new[] { (int)ObjectLayer.GasConduit, (int)ObjectLayer.GasConduitConnection }));

			registry.Register(OverlayModes.SolidConveyor.ID,
				new UtilitySkipStrategy(
					() => Game.Instance.solidConduitSystem,
					ObjectLayer.ReplacementSolidConduit,
					new[] { (int)ObjectLayer.SolidConduit, (int)ObjectLayer.SolidConduitConnection }));

			registry.Register(OverlayModes.Logic.ID,
				new UtilitySkipStrategy(
					() => Game.Instance.logicCircuitSystem,
					ObjectLayer.ReplacementLogicWire,
					new[] { (int)ObjectLayer.LogicWire, (int)ObjectLayer.LogicGate }));

			registry.Register(OverlayModes.Rooms.ID,
				new RoomSkipStrategy());

			registry.Register(OverlayModes.Disease.ID,
				new DiseaseSkipStrategy());

			registry.Register(OverlayModes.Light.ID,
				new LightSkipStrategy());

			registry.Register(OverlayModes.Temperature.ID,
				new TemperatureSkipStrategy());

			registry.Register(OverlayModes.Radiation.ID,
				new RadiationSkipStrategy());

			registry.Register(OverlayModes.Decor.ID,
				new DecorSkipStrategy());

			return registry;
		}
	}
}
