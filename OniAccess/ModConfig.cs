using System.Collections.Generic;
using OniAccess.Handlers.Tiles;
using OniAccess.Handlers.Tiles.Scanner;

namespace OniAccess {
	public class ModConfig {
		public CoordinateMode CoordinateMode { get; set; } = CoordinateMode.Off;
		public bool AutoMoveCursor { get; set; } = false;
		public bool ScannerMassReadout { get; set; } = true;
		public bool LockZoom { get; set; } = true;
		public bool UtilityPresenceEarcons { get; set; } = false;
		public bool PipeShapeEarcons { get; set; } = false;
		public bool PassabilityEarcons { get; set; } = false;
		public bool AnnounceBiomeChanges { get; set; } = true;
		public bool FlowSonification { get; set; } = false;
		public bool FlowDirectionReadout { get; set; } = true;
		public bool TemperatureBandEarcons { get; set; } = false;
		public bool FollowMovementEarcons { get; set; } = false;
		public bool FootstepEarcons { get; set; } = true;
		public bool ScannerDirectionEarcons { get; set; } = false;

		public float UtilityPresenceVolume { get; set; } = 1.0f;
		public float PipeShapeVolume { get; set; } = 0.15f;
		public float PassabilityVolume { get; set; } = 0.25f;
		public float TemperatureBandVolume { get; set; } = 0.25f;
		public float FlowSonificationVolume { get; set; } = 0.05f;
		public float FollowMovementVolume { get; set; } = 0.11f;
		public float FootstepVolume { get; set; } = 1.5f;
		public float ScannerDirectionVolume { get; set; } = 0.15f;

		public List<CustomScannerCategory> CustomScannerCategories { get; set; }
			= new List<CustomScannerCategory>();
	}
}
