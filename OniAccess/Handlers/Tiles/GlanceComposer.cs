using System.Collections.Generic;
using OniAccess.ConduitTracking;

namespace OniAccess.Handlers.Tiles {
	/// <summary>
	/// Runs an ordered list of ICellSection instances for a cell,
	/// concatenates their output tokens with ", ", and returns
	/// the final speech string.
	///
	/// Constructed with an immutable section list.
	/// OverlayProfileRegistry constructs different instances per overlay profile.
	/// </summary>
	public class GlanceComposer {
		private readonly IReadOnlyList<ICellSection> _sections;

		/// <summary>
		/// Shared stateless section instances, reused across profiles.
		/// </summary>
		internal static readonly ICellSection Building = new Sections.BuildingSection();
		internal static readonly ICellSection Element = new Sections.ElementSection();
		internal static readonly ICellSection Entity = new Sections.EntitySection();
		internal static readonly ICellSection Order = new Sections.OrderSection();
		internal static readonly ICellSection Debris = new Sections.DebrisSection();
		internal static readonly ICellSection Light = new Sections.LightSection();
		internal static readonly ICellSection Radiation = new Sections.RadiationSection();
		internal static readonly ICellSection Decor = new Sections.DecorSection();
		internal static readonly ICellSection Disease = new Sections.DiseaseSection();
		internal static readonly ICellSection Power = new Sections.ConduitSection(
			() => Game.Instance.electricalConduitSystem,
			ObjectLayer.ReplacementWire,
			(int)ObjectLayer.Wire, (int)ObjectLayer.WireConnectors);
		internal static readonly ICellSection Plumbing = new Sections.ConduitSection(
			() => Game.Instance.liquidConduitSystem,
			() => FlowTracker.Liquid,
			cell => Game.Instance.liquidConduitFlow.GetConduit(cell).idx,
			cell => Game.Instance.liquidConduitFlow.IsConduitEmpty(cell),
			ObjectLayer.ReplacementLiquidConduit,
			(int)ObjectLayer.LiquidConduit, (int)ObjectLayer.LiquidConduitConnection);
		internal static readonly ICellSection Ventilation = new Sections.ConduitSection(
			() => Game.Instance.gasConduitSystem,
			() => FlowTracker.Gas,
			cell => Game.Instance.gasConduitFlow.GetConduit(cell).idx,
			cell => Game.Instance.gasConduitFlow.IsConduitEmpty(cell),
			ObjectLayer.ReplacementGasConduit,
			(int)ObjectLayer.GasConduit, (int)ObjectLayer.GasConduitConnection);
		internal static readonly ICellSection Conveyor = new Sections.ConduitSection(
			() => Game.Instance.solidConduitSystem,
			() => FlowTracker.Solid,
			cell => Game.Instance.solidConduitFlow.GetConduit(cell).idx,
			cell => Game.Instance.solidConduitFlow.IsConduitEmpty(cell),
			ObjectLayer.ReplacementSolidConduit,
			(int)ObjectLayer.SolidConduit, (int)ObjectLayer.SolidConduitConnection);
		internal static readonly ICellSection Automation = new Sections.AutomationSection();
		internal static readonly ICellSection Temperature = new Sections.TemperatureSection();

		public GlanceComposer(IReadOnlyList<ICellSection> sections) {
			_sections = sections;
		}

		/// <summary>
		/// Build the speech string for a visible cell. Returns null
		/// if all sections produce empty output.
		/// Fog-of-war gating is the caller's responsibility.
		/// </summary>
		public string Compose(int cell) {
			var ctx = new CellContext();
			var tokens = new List<string>();
			foreach (var section in _sections) {
				try {
					foreach (var token in section.Read(cell, ctx)) {
						if (!string.IsNullOrEmpty(token))
							tokens.Add(token);
					}
				} catch (System.Exception ex) {
					Util.Log.Error(
						$"GlanceComposer: {section.GetType().Name} threw: {ex}");
				}
			}
			if (tokens.Count == 0) return null;
			return string.Join(", ", tokens);
		}

		/// <summary>
		/// Create the default (no-overlay) glance composer with all
		/// five standard sections in speech order.
		/// </summary>
		public static GlanceComposer CreateDefault() {
			return new GlanceComposer(new List<ICellSection> {
				Building, Element, Entity, Order, Debris
			}.AsReadOnly());
		}
	}
}
