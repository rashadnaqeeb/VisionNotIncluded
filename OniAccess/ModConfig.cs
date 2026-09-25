using System.Collections.Generic;
using OniAccess.Handlers.Tiles;
using OniAccess.Handlers.Tiles.Scanner;
using OniAccess.Speech;

namespace OniAccess {
	public class ModConfig {
		public SpeechOutputMode SpeechOutput { get; set; } = SpeechOutputMode.ScreenReader;
		// Voice name and language, not index: the OS voice list reorders as voices
		// are installed, and macOS ships one name (Eddy, Flo, Grandma, ...) in a
		// dozen languages. An empty name means the operating system's own voice.
		public string SystemVoice { get; set; } = "";
		public string SystemVoiceLanguage { get; set; } = "";
		// The backend's own stable id for the voice where it has one (AVSpeech on
		// Mac), preferred over the name, which is not unique.
		public string SystemVoiceIdentifier { get; set; } = "";
		// 0-100, or Unset to keep the operating system's rate.
		public int SystemVoiceRate { get; set; } = SpeechOutputSelector.Unset;
		public int SystemVoiceVolume { get; set; } = SpeechOutputSelector.VolumeDefault;

		public bool VerboseUi { get; set; } = false;
		public CoordinateMode CoordinateMode { get; set; } = CoordinateMode.Off;
		public RectSizeMode RectSizeMode { get; set; } = RectSizeMode.Off;
		public bool AutoMoveCursor { get; set; } = false;
		public bool ScannerMassReadout { get; set; } = true;
		public bool LockZoom { get; set; } = true;
		public bool UtilityPresenceEarcons { get; set; } = false;
		public bool PipeShapeEarcons { get; set; } = false;
		public bool PassabilityEarcons { get; set; } = false;
		public bool AnnounceBiomeChanges { get; set; } = true;
		public bool FlowSonification { get; set; } = false;
		public bool FlowDirectionReadout { get; set; } = true;
		public bool SkipStopsAtBridgeMiddles { get; set; } = false;
		public bool SkipStopsAtPorts { get; set; } = false;
		public bool TemperatureBandEarcons { get; set; } = false;
		public bool FollowMovementEarcons { get; set; } = false;
		public bool FootstepEarcons { get; set; } = true;
		public bool ScannerDirectionEarcons { get; set; } = false;
		public bool SweeperActivityReadout { get; set; } = true;
		public bool SweeperPickupEarcons { get; set; } = true;

		public float UtilityPresenceVolume { get; set; } = 1.0f;
		public float PipeShapeVolume { get; set; } = 0.15f;
		public float PassabilityVolume { get; set; } = 0.25f;
		public float TemperatureBandVolume { get; set; } = 0.25f;
		public float FlowSonificationVolume { get; set; } = 0.05f;
		public float FollowMovementVolume { get; set; } = 0.11f;
		public float FootstepVolume { get; set; } = 1.5f;
		public float ScannerDirectionVolume { get; set; } = 0.15f;
		public float SweeperPickupVolume { get; set; } = 1.0f;

		public List<CustomScannerCategory> CustomScannerCategories { get; set; }
			= new List<CustomScannerCategory>();
	}
}
