using HarmonyLib;
using OniAccess.ConduitTracking;
using UnityEngine;

namespace OniAccess.Patches {
	[HarmonyPatch(typeof(Game), "OnPrefabInit")]
	internal static class Game_OnPrefabInit_FlowTracker_Patch {
		private static void Postfix(Game __instance) {
			FlowTracker.Attach(__instance);
		}
	}

	[HarmonyPatch(typeof(ConduitFlow), nameof(ConduitFlow.Sim200ms))]
	internal static class ConduitFlow_Sim200ms_Patch {
		private static void Prefix(ConduitFlow __instance, float dt,
				ref bool __state) {
			float elapsed = Traverse.Create(__instance)
				.Field<float>("elapsedTime").Value;
			__state = dt > 0f && elapsed + dt >= 1f;
		}

		private static void Postfix(ConduitFlow __instance, bool __state) {
			if (!__state) return;
			var tracker = __instance.conduitType == ConduitType.Gas
				? FlowTracker.Gas : FlowTracker.Liquid;
			tracker.RecordFluid(__instance);
			BridgeFlowCapture.Clear();
		}
	}

	[HarmonyPatch(typeof(SolidConduitFlow),
		nameof(SolidConduitFlow.Sim200ms))]
	internal static class SolidConduitFlow_Sim200ms_Patch {
		private static void Prefix(SolidConduitFlow __instance, float dt,
				ref bool __state) {
			float elapsed = Traverse.Create(__instance)
				.Field<float>("elapsedTime").Value;
			__state = dt > 0f && elapsed + dt >= 1f;
		}

		private static void Postfix(SolidConduitFlow __instance,
				bool __state) {
			if (!__state) return;
			FlowTracker.Solid.RecordSolid(__instance);
		}
	}

	[HarmonyPatch(typeof(ConduitBridge), "ConduitUpdate")]
	internal static class ConduitBridge_ConduitUpdate_Patch {
		private static void Prefix(ConduitBridge __instance,
				ref ConduitFlow.ConduitContents __state) {
			var trav = Traverse.Create(__instance);
			int inputCell = trav.Field<int>("inputCell").Value;
			var flow = Conduit.GetFlowManager(__instance.type);
			__state = flow.GetContents(inputCell);
		}

		private static void Postfix(ConduitBridge __instance,
				ConduitFlow.ConduitContents __state) {
			if (__state.element == SimHashes.Vacuum) return;
			var trav = Traverse.Create(__instance);
			int inputCell = trav.Field<int>("inputCell").Value;
			int outputCell = trav.Field<int>("outputCell").Value;
			var flow = Conduit.GetFlowManager(__instance.type);
			var after = flow.GetContents(inputCell);
			if (after.mass >= __state.mass) return;

			var building = __instance.GetComponent<Building>();
			int origin = Grid.PosToCell(
				building.transform.GetPosition());
			int dx = Grid.CellColumn(origin) - Grid.CellColumn(inputCell);
			int dy = Grid.CellRow(origin) - Grid.CellRow(inputCell);
			int dir;
			if (dx > 0) dir = FlowTracker.DirRight;
			else if (dx < 0) dir = FlowTracker.DirLeft;
			else if (dy > 0) dir = FlowTracker.DirUp;
			else dir = FlowTracker.DirDown;

			BridgeFlowCapture.Record(inputCell, outputCell,
				__instance.type, __state.element, dir);
		}
	}
}
