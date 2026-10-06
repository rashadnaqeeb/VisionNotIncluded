using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.Video;

using OniAccess.Util;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Handler for VideoScreen (KModalScreen): victory cinematics and intro videos.
	///
	/// Two phases during victory sequences:
	/// Phase 1: Unskippable video plays. No interactive elements. Announces "Video playing".
	/// Phase 2: Victory loop. closeButton + proceedButton activate, overlay text appears.
	///          Re-discovers widgets and speaks the first one.
	///
	/// For skippable intro videos, closeButton starts active so Phase 2 triggers
	/// immediately after the Phase 1 announcement — DiscoverWidgets finds just the
	/// close button, which is the correct behavior.
	///
	/// Lifecycle: OnActivate calls Show(false) during prefab init, so a Harmony patch
	/// on VideoScreen.OnShow pushes/pops this handler.
	///
	/// Widget discovery is gated on _inVictoryLoop. During Phase 1 there are no
	/// interactive elements, and the handler is pushed mid-PlayVideo before button
	/// states are configured (Show() fires at line 151, SetActive at line 172),
	/// so discovering during Phase 1 would pick up stale prefab state. Phase 2
	/// transition is detected in Tick() by polling closeButton.activeSelf.
	/// </summary>
	public class VideoScreenHandler: BaseWidgetHandler {
		private bool _announcedPlaying;
		private bool _inVictoryLoop;
		private int _descCursor;
		private string _currentClipName;
		private bool _volumeLowered;

		public override string DisplayName => (string)STRINGS.ONIACCESS.HANDLERS.VIDEO;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		public VideoScreenHandler(KScreen screen) : base(screen) {
			HelpEntries = BuildHelpEntries();
		}

		public override void OnActivate() {
			_announcedPlaying = false;
			_inVictoryLoop = false;
			_descCursor = 0;
			_currentClipName = null;
			_volumeLowered = false;
			base.OnActivate();
		}

		public override bool DiscoverWidgets(KScreen screen) {
			_widgets.Clear();

			if (!_inVictoryLoop) return true;

			var t = Traverse.Create(screen);

			// Overlay text from victory loop (victoryLoopMessage rendered via VideoOverlay prefab)
			try {
				var overlayContainer = t.Field<UnityEngine.RectTransform>("overlayContainer").Value;
				if (overlayContainer != null && overlayContainer.gameObject.activeInHierarchy) {
					var locTexts = overlayContainer.GetComponentsInChildren<LocText>();
					var parts = new List<string>();
					foreach (var lt in locTexts) {
						if (lt != null && !string.IsNullOrEmpty(lt.text))
							parts.Add(lt.text);
					}
					if (parts.Count > 0) {
						_widgets.Add(new LabelWidget {
							Label = string.Join(". ", parts.ToArray()),
							GameObject = overlayContainer.gameObject
						});
					}
				}
			} catch (System.Exception ex) {
				Log.Error($"VideoScreenHandler: overlay text discovery failed: {ex.Message}");
			}

			// closeButton — only when active
			WidgetDiscoveryUtil.TryAddButtonField(screen, "closeButton", null, _widgets);

			// proceedButton — only when active
			WidgetDiscoveryUtil.TryAddButtonField(screen, "proceedButton", null, _widgets);

			return true;
		}

		public override bool Tick() {
			if (!_announcedPlaying) {
				_announcedPlaying = true;
				Speech.SpeechPipeline.SpeakQueued((string)STRINGS.ONIACCESS.VIDEO.PLAYING);
			}

			try {
				var t = Traverse.Create(_screen);
				var videoPlayer = t.Field<VideoPlayer>("videoPlayer").Value;
				if (videoPlayer != null && videoPlayer.clip != null) {
					if (!_volumeLowered) {
						_volumeLowered = true;
						var audioHandle = t.Field<FMOD.Studio.EventInstance>("audioHandle").Value;
						if (audioHandle.isValid())
							audioHandle.setVolume(0.3f);
					}
					var clipName = videoPlayer.clip.name;
					if (clipName != _currentClipName) {
						_currentClipName = clipName;
						_descCursor = 0;
					}
					var descs = VideoDescriptions.GetDescriptions(clipName);
					if (descs != null) {
						var time = videoPlayer.time;
						while (_descCursor < descs.Count && descs[_descCursor].time <= time) {
							Speech.SpeechPipeline.SpeakQueued(descs[_descCursor].text);
							_descCursor++;
						}
					}
				}
			} catch (System.Exception ex) {
				Log.Error($"VideoScreenHandler: description polling failed: {ex.Message}");
			}

			if (!_inVictoryLoop) {
				try {
					var closeButton = Traverse.Create(_screen).Field<KButton>("closeButton").Value;
					if (closeButton != null && closeButton.gameObject.activeSelf) {
						_inVictoryLoop = true;
						_pendingRediscovery = false;
						DiscoverWidgets(_screen);
						CurrentIndex = 0;
						if (_widgets.Count > 0) {
							Speech.SpeechPipeline.SpeakQueued(ComposeWidgetText(_widgets[0]));
						}
					}
				} catch (System.Exception ex) {
					Log.Error($"VideoScreenHandler: victory loop detection failed: {ex.Message}");
				}
			} else {
				DiscoverWidgets(_screen);
				if (_widgets.Count > 0 && CurrentIndex >= _widgets.Count) {
					CurrentIndex = _widgets.Count - 1;
				}
			}

			return base.Tick();
		}
	}
}
