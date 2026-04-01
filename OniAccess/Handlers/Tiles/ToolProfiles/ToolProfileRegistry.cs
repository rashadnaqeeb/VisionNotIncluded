using System;
using System.Collections.Generic;

namespace OniAccess.Handlers.Tiles.ToolProfiles {
	public sealed class ToolProfileRegistry {
		public static ToolProfileRegistry Instance { get; private set; }

		internal static readonly ICellSection Selection = new Sections.SelectionSection();

		private readonly Dictionary<Type, ToolProfile> _profiles
			= new Dictionary<Type, ToolProfile>();

		public void Register(Type toolType, ToolProfile profile) {
			_profiles[toolType] = profile;
		}

		public ToolProfile GetProfile(Type toolType) {
			if (_profiles.TryGetValue(toolType, out var profile))
				return profile;
			return null;
		}

		public static ToolProfileRegistry Build() {
			var registry = new ToolProfileRegistry();

			var dig = new Sections.DigToolSection();
			var mop = new Sections.MopToolSection();
			var disinfect = new Sections.DisinfectToolSection();
			var sweep = new Sections.SweepToolSection();
			var attack = new Sections.AttackToolSection();
			var capture = new Sections.CaptureToolSection();
			var harvest = new Sections.HarvestToolSection();
			var deconstruct = new Sections.DeconstructToolSection();
			var cancel = new Sections.CancelToolSection();
			var prioritize = new Sections.PrioritizeToolSection();
			var emptyPipe = new Sections.EmptyPipeToolSection();
			var disconnect = new Sections.DisconnectToolSection();

			registry.Register(typeof(DigTool), MakeProfile("DigTool", dig));
			registry.Register(typeof(MopTool), MakeProfile("MopTool", mop));
			registry.Register(typeof(DisinfectTool), MakeProfile("DisinfectTool", disinfect));
			registry.Register(typeof(ClearTool), MakeProfile("ClearTool", sweep));
			registry.Register(typeof(AttackTool), MakeProfile("AttackTool", attack));
			registry.Register(typeof(CaptureTool), MakeProfile("CaptureTool", capture));
			registry.Register(typeof(HarvestTool), MakeProfile("HarvestTool", harvest));
			registry.Register(typeof(DeconstructTool), MakeProfile("DeconstructTool", deconstruct));
			registry.Register(typeof(CancelTool), MakeProfile("CancelTool", cancel));
			registry.Register(typeof(PrioritizeTool), MakeProfile("PrioritizeTool", prioritize));
			registry.Register(typeof(EmptyPipeTool), MakeProfile("EmptyPipeTool", emptyPipe));
			registry.Register(typeof(DisconnectTool), MakeProfile("DisconnectTool", disconnect));

			var utilityLayer = new Sections.UtilityLayerSection();
			var buildLine = new Sections.BuildToolSection();
			var buildPriority = new Sections.BuildPrioritySection();
			var extentSection = new Sections.BuildExtentSection();
			registry.Register(typeof(BuildTool), new ToolProfile("BuildTool",
				new GlanceComposer(new List<ICellSection> {
					utilityLayer, GlanceComposer.Building, buildPriority, GlanceComposer.Element, GlanceComposer.Order, extentSection
				}.AsReadOnly())));
			registry.Register(typeof(UtilityBuildTool), new ToolProfile("UtilityBuildTool",
				new GlanceComposer(new List<ICellSection> {
					utilityLayer, GlanceComposer.Building, buildPriority, GlanceComposer.Element, buildLine
				}.AsReadOnly())));
			registry.Register(typeof(WireBuildTool), new ToolProfile("WireBuildTool",
				new GlanceComposer(new List<ICellSection> {
					utilityLayer, GlanceComposer.Building, buildPriority, GlanceComposer.Element, buildLine
				}.AsReadOnly())));

			Instance = registry;
			return registry;
		}

		private static ToolProfile MakeProfile(string name, ICellSection toolSection) {
			var sections = new List<ICellSection> {
				Selection, toolSection
			}.AsReadOnly();
			return new ToolProfile(name, sections);
		}
	}
}
