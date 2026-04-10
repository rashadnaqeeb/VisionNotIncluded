using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace OniAccess.Widgets {
	/// <summary>
	/// Recursively walks a SideScreenContent's widget hierarchy and emits
	/// Widget items. Priority order per node: KSlider, KToggle,
	/// MultiToggle, KNumberInputField, KInputField, KButton, LocText.
	/// When an interactive component is found on a node, that node is
	/// consumed and its children are not walked further.
	/// Inactive GameObjects and mouse-only controls are skipped.
	/// All SpeechFuncs read live component state via GetParsedText().
	/// </summary>
	public static class SideScreenWalker {
		public class RadioMember {
			public string Label;
			public KToggle Toggle;
			public MultiToggle MultiToggleRef;
			public object Tag;
			public System.Action OnSelect;
			public System.Func<bool> IsActive;
		}

		static readonly Dictionary<System.Type, System.Action<SideScreenContent, List<Widget>>> _overrides
			= new Dictionary<System.Type, System.Action<SideScreenContent, List<Widget>>>();

		public static void RegisterOverride<T>(System.Action<T, List<Widget>> handler)
				where T : SideScreenContent {
			_overrides[typeof(T)] = (screen, items) => handler((T)screen, items);
		}

		/// <summary>
		/// Walk the ContentContainer of a SideScreenContent (or its
		/// root transform if ContentContainer is null/inactive).
		/// Appends discovered widgets to <paramref name="items"/>.
		/// </summary>
		public static void Walk(SideScreenContent screen, List<Widget> items) {
			if (_overrides.TryGetValue(screen.GetType(), out var handler)) {
				handler(screen, items);
				return;
			}
			WalkDefault(screen, items);
		}

		/// <summary>
		/// Generic walk path: walks ContentContainer, picks up
		/// outside-container widgets, removes claimed labels, and
		/// collapses radio toggle groups. Overrides that need the
		/// default walk followed by a fixup call this directly.
		/// </summary>
		internal static void WalkDefault(SideScreenContent screen, List<Widget> items) {
			var claimedLabels = new HashSet<LocText>();

			var root = screen.ContentContainer != null
				&& screen.ContentContainer.activeInHierarchy
				? screen.ContentContainer.transform
				: screen.transform;
			WalkTransform(root, items, claimedLabels);

			// Pick up widgets outside ContentContainer (e.g., AutomatableSideScreen)
			if (root != screen.transform) {
				var screenT = screen.transform;
				for (int i = 0; i < screenT.childCount; i++) {
					var child = screenT.GetChild(i);
					if (child == root.transform) continue;
					if (!child.gameObject.activeSelf) continue;
					if (IsSkipped(child.gameObject.name)) continue;
					if (IsChrome(child)) continue;
					if (TryAddWidget(child, items, claimedLabels))
						continue;
					WalkTransform(child, items, claimedLabels);
				}
			}

			// Remove LocTexts that were claimed as labels by interactive widgets
			items.RemoveAll(item => {
				if (!(item is LabelWidget)) return false;
				var lt = item.GameObject?.GetComponent<LocText>();
				return lt != null && claimedLabels.Contains(lt);
			});

			CollapseRadioToggles(items, screen.GetTitle(), screen.transform, claimedLabels);
		}

		internal static void WalkConditionContainer(
				GameObject container, List<Widget> items) {
			WalkConditionContainer(container, items, new HashSet<LocText>());
		}

		internal static void WalkConditionContainer(
				GameObject container, List<Widget> items,
				HashSet<LocText> claimedLabels) {
			if (container == null) return;
			var containerT = container.transform;
			for (int i = 0; i < containerT.childCount; i++) {
				var child = containerT.GetChild(i);
				if (!child.gameObject.activeSelf) continue;
				TryAddConditionRow(child, items, claimedLabels);
			}
		}

		private static void WalkTransform(Transform parent, List<Widget> items, HashSet<LocText> claimedLabels) {
			for (int i = 0; i < parent.childCount; i++) {
				var child = parent.GetChild(i);
				if (!child.gameObject.activeSelf) continue;
				if (IsSkipped(child.gameObject.name)) continue;
				if (TryAddConditionRow(child, items, claimedLabels)) continue;
				if (TryAddCategoryContainer(child, items, claimedLabels)) continue;
				if (TryAddSelectionCategoryContainer(child, items, claimedLabels)) continue;
				if (TryAddWidget(child, items, claimedLabels)) continue;
				WalkTransform(child, items, claimedLabels);
			}
		}

		/// <summary>
		/// Try to emit a widget for the given transform. Returns true if a
		/// component was found (caller should not recurse into children).
		/// </summary>
		private static bool TryAddWidget(Transform t, List<Widget> items, HashSet<LocText> claimedLabels) {
			var go = t.gameObject;

			// ReceptacleToggle: compound widget (title + amount + selection toggle).
			// Must be checked first — contains child MultiToggle/LocText that would
			// otherwise be matched individually.
			var receptacleToggle = go.GetComponent<ReceptacleToggle>();
			if (receptacleToggle != null && receptacleToggle.toggle != null) {
				var captured = receptacleToggle;
				if (captured.title != null) claimedLabels.Add(captured.title);
				if (captured.amount != null) claimedLabels.Add(captured.amount);

				string label = captured.title != null
					? captured.title.GetParsedText() : t.name;
				if (!HasVisibleContent(label)) {
					Util.Log.Warn($"Walker: blank label for {t.name} (ReceptacleToggle)");
					return true;
				}

				items.Add(new ToggleWidget {
					Label = label,
					Component = captured.toggle,
					GameObject = go,
					SuppressTooltip = true,
					SpeechFunc = () => {
						string name = captured.title != null
							? captured.title.text : captured.transform.name;
						var parts = new List<string> { name };
						string count = captured.amount != null
							? captured.amount.text : null;
						if (!string.IsNullOrEmpty(count))
							parts.Add($"{count} {(string)STRINGS.ONIACCESS.STATES.AVAILABLE}");
						int state = captured.toggle.CurrentState;
						if (state == 1 || state == 3)
							parts.Add((string)STRINGS.ONIACCESS.STATES.SELECTED);
						string desc = GetReceptacleDescription(captured);
						if (desc != null)
							parts.Add(desc);
						return string.Join(", ", parts);
					}
				});
				return true;
			}

			// SingleItemSelectionRow: compound widget (labelText + button + selected state).
			// Must be checked before KButton — the row contains a child KButton that
			// would otherwise be matched individually.
			var selectionRow = go.GetComponent<SingleItemSelectionRow>();
			if (selectionRow != null) {
				var captured = selectionRow;
				LocText labelLt;
				try {
					labelLt = Traverse.Create(captured).Field<LocText>("labelText").Value;
				} catch (System.Exception ex) {
					Util.Log.Warn($"Walker: SingleItemSelectionRow labelText read failed: {ex.Message}");
					return true;
				}
				if (labelLt != null) claimedLabels.Add(labelLt);
				string label = labelLt != null ? labelLt.GetParsedText() : t.name;
				if (!HasVisibleContent(label)) {
					Util.Log.Warn($"Walker: blank label for {t.name} (SingleItemSelectionRow)");
					return true;
				}
				items.Add(new ButtonWidget {
					Label = label,
					Component = captured.button,
					GameObject = go,
					SuppressTooltip = true,
					SpeechFunc = () => {
						string name = labelLt != null
							? labelLt.GetParsedText() : captured.transform.name;
						string speech = name;
						if (captured.IsSelected)
							speech += $", {(string)STRINGS.ONIACCESS.STATES.SELECTED}";
						return speech;
					}
				});
				return true;
			}

			// KSlider (catches NonLinearSlider which extends KSlider)
			var slider = go.GetComponent<KSlider>();
			if (slider != null) {
				var captured = slider;
				var labelLt = FindChildLocText(t, null)
					?? FindSiblingLocText(t) ?? FindSiblingLocText(t.parent);
				if (labelLt != null) claimedLabels.Add(labelLt);
				string label = ReadLocText(labelLt, t.name);
				if (!HasVisibleContent(label)) {
					Util.Log.Warn($"Walker: blank label for {t.name} (KSlider) parent={t.parent?.name}");
					return true;
				}
				items.Add(new SliderWidget {
					Label = label,
					Component = captured,
					GameObject = go,
					SpeechFunc = () => {
						if (labelLt != null) labelLt.ForceMeshUpdate();
						string lbl = ReadLocText(labelLt, captured.transform.name);
						return $"{lbl}, {WidgetOps.FormatSliderValue(captured)}, {(string)STRINGS.ONIACCESS.STATES.SLIDER}";
					}
				});
				return true;
			}

			// KToggle
			var ktoggle = go.GetComponent<KToggle>();
			if (ktoggle != null) {
				var captured = ktoggle;
				var labelLt = FindChildLocText(t, null)
					?? FindSiblingLocText(t) ?? FindSiblingLocText(t.parent);
				if (labelLt != null) claimedLabels.Add(labelLt);
				string label = ReadLocText(labelLt, t.name);
				if (!HasVisibleContent(label)) {
					Util.Log.Warn($"Walker: blank label for {t.name} (KToggle) parent={t.parent?.name}");
					return true;
				}
				items.Add(new ToggleWidget {
					Label = label,
					Component = captured,
					GameObject = go,
					SpeechFunc = () => {
						string lbl = ReadLocText(labelLt, captured.transform.name);
						string state = IsToggleActive(captured)
							? (string)STRINGS.ONIACCESS.STATES.ON
							: (string)STRINGS.ONIACCESS.STATES.OFF;
						return $"{lbl}, {state}";
					}
				});
				return true;
			}

			// MultiToggle — skip if a sibling KToggle or preceding MultiToggle
			// already represents this row's toggle (suppresses expand arrows).
			var multiToggle = go.GetComponent<MultiToggle>();
			if (multiToggle != null) {
				bool redundant = IsRedundantMultiToggle(t);
				if (redundant)
					return true;
				var captured = multiToggle;
				var childLt = FindChildLocText(t, null);
				var sibLt = FindSiblingLocText(t);
				var parentSibLt = FindSiblingLocText(t.parent);
				var labelLt = childLt ?? sibLt ?? parentSibLt;
				if (labelLt != null) claimedLabels.Add(labelLt);
				string label = ReadLocText(labelLt, t.name);
				if (!HasVisibleContent(label)) {
					Util.Log.Warn($"Walker: blank label for {t.name} (MultiToggle) parent={t.parent?.name}");
					return true;
				}
				items.Add(new ToggleWidget {
					Label = label,
					Component = captured,
					GameObject = go,
					SpeechFunc = () => {
						string lbl = ReadLocText(labelLt, captured.transform.name);
						return $"{lbl}, {WidgetOps.GetMultiToggleState(captured)}";
					}
				});
				return true;
			}

			// KNumberInputField (extends KInputField — check first)
			var knum = go.GetComponent<KNumberInputField>();
			if (knum != null) {
				var captured = knum;
				var labelLt = FindSiblingLocText(t) ?? FindSiblingLocText(t.parent);
				if (labelLt != null) claimedLabels.Add(labelLt);
				string label = ReadLocText(labelLt, t.name);
				if (!HasVisibleContent(label)) {
					Util.Log.Warn($"Walker: blank label for {t.name} (KNumberInputField) parent={t.parent?.name}");
					return true;
				}
				var unitsLt = FindFollowingSiblingLocText(t)
					?? FindFollowingSiblingLocText(t.parent);
				if (unitsLt != null) claimedLabels.Add(unitsLt);
				items.Add(new TextInputWidget {
					Label = label,
					Component = captured,
					GameObject = go,
					SpeechFunc = () => {
						string lbl = ReadLocText(labelLt, captured.transform.name);
						string val = captured.field != null
							? captured.field.text : "";
						string units = unitsLt != null
							? unitsLt.GetParsedText() : null;
						string ifl = (string)STRINGS.ONIACCESS.STATES.INPUT_FIELD;
						if (!string.IsNullOrEmpty(units))
							return $"{lbl}, {val} {units}, {ifl}";
						return $"{lbl}, {val}, {ifl}";
					}
				});
				return true;
			}

			// KInputField (AlarmSideScreen text input — Category B)
			var kinput = go.GetComponent<KInputField>();
			if (kinput != null) {
				var captured = kinput;
				var labelLt = FindSiblingLocText(t) ?? FindSiblingLocText(t.parent);
				if (labelLt != null) claimedLabels.Add(labelLt);
				string label = ReadLocText(labelLt, t.name);
				if (!HasVisibleContent(label)) {
					Util.Log.Warn($"Walker: blank label for {t.name} (KInputField) parent={t.parent?.name}");
					return true;
				}
				items.Add(new TextInputWidget {
					Label = label,
					Component = captured,
					GameObject = go,
					SpeechFunc = () => {
						string lbl = ReadLocText(labelLt, captured.transform.name);
						string val = captured.field != null
							? captured.field.text : "";
						return $"{lbl}, {val}, {(string)STRINGS.ONIACCESS.STATES.INPUT_FIELD}";
					}
				});
				return true;
			}

			// KButton — check for PlayerControlledToggleSideScreen first,
			// where a KButton acts as an on/off toggle with animation only.
			var kbutton = go.GetComponent<KButton>();
			if (kbutton != null) {
				var captured = kbutton;
				var toggleScreen = go.GetComponentInParent<PlayerControlledToggleSideScreen>();
				if (toggleScreen != null && toggleScreen.target != null) {
					var capturedTarget = toggleScreen.target;
					string label = GetButtonLabel(captured, t.name);
					items.Add(new ToggleWidget {
						Label = label,
						Component = captured,
						GameObject = go,
						SpeechFunc = () => {
							string l = GetButtonLabel(captured, captured.transform.name);
							bool on = capturedTarget.ToggleRequested
								? !capturedTarget.ToggledOn()
								: capturedTarget.ToggledOn();
							string state = on
								? (string)STRINGS.ONIACCESS.STATES.ON
								: (string)STRINGS.ONIACCESS.STATES.OFF;
							return $"{l}, {state}";
						}
					});
					return true;
				}
				string label2 = GetButtonLabel(captured, t.name);
				if (!HasVisibleContent(label2)) {
					Util.Log.Warn($"Walker: blank label for {t.name} (KButton) parent={t.parent?.name}");
					return true;
				}
				items.Add(new ButtonWidget {
					Label = label2,
					Component = captured,
					GameObject = go,
					SpeechFunc = () => GetButtonLabel(captured, captured.transform.name)
				});
				return true;
			}

			// LocText (standalone label — emit all non-empty; claimed labels
			// are removed post-walk via claimedLabels set).
			var locText = go.GetComponent<LocText>();
			if (locText != null) {
				var captured = locText;
				string text = captured.GetParsedText();
				if (HasVisibleContent(text)) {
					items.Add(new LabelWidget {
						Label = text,
						GameObject = go,
						SpeechFunc = () => captured.GetParsedText()
					});
					return true;
				}
			}

			return false;
		}

		// ========================================
		// CONDITION ROW HELPERS
		// ========================================

		/// <summary>
		/// Detect a launch condition row (CommandModuleSideScreen prefabCondition):
		/// HierarchyReferences with "Label" (LocText) and "Check" (Image, active = ready).
		/// Emits a single Label widget with check/uncheck status prepended.
		/// </summary>
		internal static bool TryAddConditionRow(Transform t, List<Widget> items, HashSet<LocText> claimedLabels) {
			var href = t.GetComponent<HierarchyReferences>();
			if (href == null) return false;
			if (!href.HasReference("Label") || !href.HasReference("Check"))
				return false;

			var labelLt = href.GetReference<LocText>("Label");
			if (labelLt == null) return false;

			var checkImage = href.GetReference<Image>("Check");
			if (checkImage == null) return false;

			claimedLabels.Add(labelLt);

			var capturedLabel = labelLt;
			var capturedCheck = checkImage;
			string label = capturedLabel.GetParsedText();
			if (!HasVisibleContent(label)) return true;

			items.Add(new LabelWidget {
				Label = label,
				GameObject = t.gameObject,
				SpeechFunc = () => {
					string text = capturedLabel.GetParsedText();
					bool ready = capturedCheck.gameObject.activeSelf;
					string status = ready
						? (string)STRINGS.ONIACCESS.STATES.CONDITION_MET
						: (string)STRINGS.ONIACCESS.STATES.CONDITION_NOT_MET;
					return $"{status}, {text}";
				}
			});
			return true;
		}

		// ========================================
		// RECEPTACLE HELPERS
		// ========================================

		/// <summary>
		/// Detect a ReceptacleSideScreen category container: HierarchyReferences
		/// with "HeaderLabel" and "GridLayout" whose grid children have
		/// ReceptacleToggle. Emits a single drillable parent Widget with
		/// Children for the seed rows inside.
		/// </summary>
		private static bool TryAddCategoryContainer(Transform t, List<Widget> items, HashSet<LocText> claimedLabels) {
			return TryAddCategoryCore(t, items, claimedLabels,
				"HeaderLabel", "GridLayout",
				child => child.GetComponent<ReceptacleToggle>() != null);
		}

		private static bool TryAddSelectionCategoryContainer(Transform t, List<Widget> items, HashSet<LocText> claimedLabels) {
			return TryAddCategoryCore(t, items, claimedLabels,
				"Label", "Entries",
				child => child.GetComponent<SingleItemSelectionRow>() != null);
		}

		/// <summary>
		/// Shared logic for TryAddCategoryContainer and
		/// TryAddSelectionCategoryContainer. Detects a HierarchyReferences
		/// node with a header LocText and a container whose children pass
		/// the given component check, then emits a drillable LabelWidget.
		/// </summary>
		private static bool TryAddCategoryCore(
				Transform t, List<Widget> items, HashSet<LocText> claimedLabels,
				string headerRef, string containerRef,
				System.Func<Transform, bool> hasTargetComponent) {
			var href = t.GetComponent<HierarchyReferences>();
			if (href == null) return false;
			if (!href.HasReference(headerRef) || !href.HasReference(containerRef))
				return false;

			var containerObj = href.GetReference(containerRef);
			if (containerObj == null) return false;
			var containerT = containerObj.transform;

			bool found = false;
			for (int i = 0; i < containerT.childCount; i++) {
				var child = containerT.GetChild(i);
				if (!child.gameObject.activeSelf) continue;
				if (hasTargetComponent(child)) { found = true; break; }
			}
			if (!found) return false;

			// Force the container active so children are activeInHierarchy
			// and their toggles respond to clicks. The visual state is
			// irrelevant for blind users.
			containerObj.gameObject.SetActive(true);

			var children = new List<Widget>();
			for (int i = 0; i < containerT.childCount; i++) {
				var child = containerT.GetChild(i);
				if (!child.gameObject.activeSelf) continue;
				TryAddWidget(child, children, claimedLabels);
			}

			var headerLt = href.GetReference<LocText>(headerRef);
			if (headerLt != null) claimedLabels.Add(headerLt);

			var capturedHeader = headerLt;
			var capturedContainer = containerT;
			items.Add(new LabelWidget {
				Label = headerLt != null ? headerLt.GetParsedText() : t.name,
				GameObject = t.gameObject,
				SuppressTooltip = true,
				Children = children,
				SpeechFunc = () => {
					string header = capturedHeader != null
						? capturedHeader.GetParsedText() : t.name;
					int activeCount = 0;
					for (int i = 0; i < capturedContainer.childCount; i++) {
						if (capturedContainer.GetChild(i).gameObject.activeSelf)
							activeCount++;
					}
					string countText = string.Format(
						(string)STRINGS.ONIACCESS.RECEPTACLE.ITEM_COUNT, activeCount);
					return $"{header}, {countText}";
				}
			});
			return true;
		}

		/// <summary>
		/// Extract the description from a ReceptacleToggle's tooltip.
		/// The tooltip format is "{name}\n\n{description}". Returns just
		/// the description with rich text tags stripped, or null.
		/// </summary>
		private static string GetReceptacleDescription(ReceptacleToggle rt) {
			var tooltip = rt.GetComponent<ToolTip>();
			if (tooltip == null) return null;

			string text = WidgetOps.ReadAllTooltipText(tooltip);
			if (string.IsNullOrEmpty(text)) return null;

			string[] parts = text.Split(new[] { "\n\n" }, System.StringSplitOptions.None);
			if (parts.Length < 2) return null;

			// Skip the first segment (seed name), take the description
			string desc = parts[1];
			// Strip Unity rich text tags
			desc = Regex.Replace(desc, "<[^>]+>", "");
			desc = desc.Trim();
			return string.IsNullOrEmpty(desc) ? null : desc;
		}

		// ========================================
		// SIBLING CHECKS
		// ========================================

		/// <summary>
		/// Returns true if this MultiToggle is redundant: either a sibling
		/// KToggle already owns the row's checkbox, or a preceding sibling
		/// MultiToggle already represents the row's toggle (the second
		/// MultiToggle in a row is typically an expand/collapse arrow).
		/// </summary>
		private static bool IsRedundantMultiToggle(Transform t) {
			if (t.parent == null) return false;
			var parent = t.parent;
			int myIndex = t.GetSiblingIndex();

			for (int i = 0; i < parent.childCount; i++) {
				var sibling = parent.GetChild(i);
				if (sibling == t) continue;
				if (!sibling.gameObject.activeSelf) continue;
				if (sibling.GetComponentInChildren<KToggle>() != null)
					return true;
			}

			for (int i = 0; i < myIndex; i++) {
				var sibling = parent.GetChild(i);
				if (!sibling.gameObject.activeSelf) continue;
				if (sibling.GetComponent<MultiToggle>() != null)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Returns true if the string contains at least one character that
		/// would produce visible output in speech or on screen. Rejects
		/// null, empty, whitespace-only, and strings made entirely of
		/// Unicode format/zero-width characters (U+200B, U+FEFF, etc.)
		/// that TextMeshPro inserts.
		/// </summary>
		internal static bool HasVisibleContent(string text) {
			if (string.IsNullOrEmpty(text)) return false;
			for (int i = 0; i < text.Length; i++) {
				char c = text[i];
				if (char.IsWhiteSpace(c)) continue;
				var cat = char.GetUnicodeCategory(c);
				if (cat == System.Globalization.UnicodeCategory.Control) continue;
				if (cat == System.Globalization.UnicodeCategory.Format) continue;
				if (cat == System.Globalization.UnicodeCategory.OtherNotAssigned) continue;
				return true;
			}
			return false;
		}

		// ========================================
		// LABEL RESOLUTION
		// ========================================

		/// <summary>
		/// Find the first active child LocText on the given transform.
		/// Skips the excluded component's GameObject if provided.
		/// Returns the LocText reference (for live reading), or null.
		/// </summary>
		internal static LocText FindChildLocText(Transform t, Component exclude) {
			for (int i = 0; i < t.childCount; i++) {
				var child = t.GetChild(i);
				if (!child.gameObject.activeSelf) continue;
				if (exclude != null && child.gameObject == exclude.gameObject) continue;
				var lt = child.GetComponent<LocText>();
				if (lt != null && HasVisibleContent(lt.GetParsedText()))
					return lt;
			}
			return null;
		}

		/// <summary>
		/// Find a sibling LocText for text input fields. Searches preceding
		/// siblings first (closest label), then following siblings.
		/// </summary>
		private static LocText FindSiblingLocText(Transform t) {
			if (t.parent == null) return null;
			var parent = t.parent;
			int myIndex = t.GetSiblingIndex();

			for (int i = myIndex - 1; i >= 0; i--) {
				var sibling = parent.GetChild(i);
				if (!sibling.gameObject.activeSelf) continue;
				var lt = FindDirectOrSafeChildLocText(sibling);
				if (lt != null && HasVisibleContent(lt.GetParsedText()))
					return lt;
			}

			for (int i = myIndex + 1; i < parent.childCount; i++) {
				var sibling = parent.GetChild(i);
				if (!sibling.gameObject.activeSelf) continue;
				var lt = FindDirectOrSafeChildLocText(sibling);
				if (lt != null && HasVisibleContent(lt.GetParsedText()))
					return lt;
			}

			return null;
		}

		/// <summary>
		/// Get the LocText directly on a sibling transform. Does not search
		/// children — a LocText nested inside a container (StateIndicator,
		/// input field internals, etc.) belongs to that container, not to
		/// the widget searching for a label.
		/// </summary>
		private static LocText FindDirectOrSafeChildLocText(Transform sibling) {
			return sibling.GetComponent<LocText>();
		}

		/// <summary>
		/// Find the first LocText among following siblings only. Used to
		/// capture a units suffix (e.g., "kg") for number input fields.
		/// </summary>
		private static LocText FindFollowingSiblingLocText(Transform t) {
			if (t.parent == null) return null;
			var parent = t.parent;
			int myIndex = t.GetSiblingIndex();

			for (int i = myIndex + 1; i < parent.childCount; i++) {
				var sibling = parent.GetChild(i);
				if (!sibling.gameObject.activeSelf) continue;
				if (HasInteractiveDescendant(sibling)) break;
				var lt = FindDirectOrSafeChildLocText(sibling);
				if (lt != null && HasVisibleContent(lt.GetParsedText())) {
					// If the next active sibling contains a widget, this
					// LocText is a label for that widget, not a units suffix
					if (NextActiveSiblingHasWidget(parent, i))
						return null;
					return lt;
				}
			}

			return null;
		}

		/// <summary>
		/// Returns true if the next active sibling after <paramref name="afterIndex"/>
		/// contains an interactive widget. Used to distinguish units suffixes
		/// from widget labels in FindFollowingSiblingLocText.
		/// </summary>
		private static bool NextActiveSiblingHasWidget(Transform parent, int afterIndex) {
			for (int i = afterIndex + 1; i < parent.childCount; i++) {
				var sibling = parent.GetChild(i);
				if (!sibling.gameObject.activeSelf) continue;
				return HasInteractiveDescendant(sibling);
			}
			return false;
		}

		/// <summary>
		/// Returns true if the KToggle is in the "active/selected" state.
		/// Prefers ImageToggleState.GetIsActive() which reflects the true
		/// visual state. Some screens (ThresholdSwitchSideScreen) use isOn
		/// inversely — the visually active toggle has isOn=false.
		/// Falls back to KToggle.isOn when no ImageToggleState is present.
		/// </summary>
		internal static bool IsToggleActive(KToggle toggle) {
			var its = toggle.GetComponent<ImageToggleState>();
			if (its != null) return its.GetIsActive();
			return toggle.isOn;
		}

		private static bool HasInteractiveDescendant(Transform t) {
			if (t.GetComponentInChildren<KSlider>() != null) return true;
			if (t.GetComponentInChildren<KToggle>() != null) return true;
			if (t.GetComponentInChildren<MultiToggle>() != null) return true;
			if (t.GetComponentInChildren<KNumberInputField>() != null) return true;
			if (t.GetComponentInChildren<KInputField>() != null) return true;
			return false;
		}

		/// <summary>
		/// Read a LocText reference, falling back to a name-based label.
		/// </summary>
		internal static string ReadLocText(LocText lt, string fallback) {
			if (lt != null) {
				string parsed = lt.GetParsedText();
				if (HasVisibleContent(parsed))
					return parsed;
				string raw = lt.text;
				if (HasVisibleContent(raw))
					return raw;
			}
			return fallback;
		}

		/// <summary>
		/// Read a KButton's label from its child LocText using GetParsedText().
		/// Falls back to the enclosing SideScreenContent title when the
		/// button has no text (e.g., animated icon-only buttons).
		/// </summary>
		internal static string GetButtonLabel(KButton button, string fallback) {
			var lt = button.GetComponentInChildren<LocText>();
			if (lt != null) {
				string text = lt.GetParsedText();
				if (HasVisibleContent(text))
					return text;
			}
			var screen = button.GetComponentInParent<SideScreenContent>();
			if (screen != null) {
				string title = screen.GetTitle();
				if (HasVisibleContent(title))
					return title;
			}
			return fallback;
		}

		/// <summary>
		/// Filter out mouse-only UI elements that are irrelevant for
		/// keyboard navigation.
		/// </summary>
		private static bool IsSkipped(string name) {
			if (name.IndexOf("Scrollbar", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
			if (name.IndexOf("Drag", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
			if (name.IndexOf("Resize", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
			if (name.StartsWith("increment", System.StringComparison.OrdinalIgnoreCase)) return true;
			if (name.StartsWith("decrement", System.StringComparison.OrdinalIgnoreCase)) return true;
			// ThresholdSwitchSideScreen uses abbreviated names for its
			// mouse-only +/- step buttons: "Inc Major", "Inc Minor", etc.
			if (name.StartsWith("Inc ", System.StringComparison.OrdinalIgnoreCase)) return true;
			if (name.StartsWith("Dec ", System.StringComparison.OrdinalIgnoreCase)) return true;
			return false;
		}

		/// <summary>
		/// Filter out screen-level chrome when walking outside ContentContainer.
		/// Skips title bars and close buttons that shouldn't be navigable widgets.
		/// </summary>
		private static bool IsChrome(Transform t) {
			string name = t.gameObject.name;
			if (name.IndexOf("Title", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
			if (name.IndexOf("Header", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
			if (name.IndexOf("CloseButton", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
			return false;
		}

		// ========================================
		// RADIO GROUP COLLAPSE
		// ========================================

		/// <summary>
		/// Detect consecutive KToggle widgets sharing the same parent where
		/// exactly one is isOn (radio-style mutual exclusion). Replace with a
		/// single Dropdown widget that cycles between members.
		/// </summary>
		private static void CollapseRadioToggles(
				List<Widget> items, string screenTitle, Transform screenRoot,
				HashSet<LocText> claimedLabels) {
			// Group consecutive KToggle items by parent transform
			var groups = new List<(Transform parent, int start, int count)>();
			int i = 0;
			while (i < items.Count) {
				var w = items[i];
				if (!(w is ToggleWidget) || !(w.Component is KToggle)) { i++; continue; }

				var parent = w.GameObject.transform.parent;
				int start = i;
				int count = 1;
				while (i + count < items.Count
					&& items[i + count] is ToggleWidget
					&& items[i + count].Component is KToggle
					&& items[i + count].GameObject.transform.parent == parent)
					count++;

				if (count >= 2)
					groups.Add((parent, start, count));
				i += count;
			}

			// Collect GameObjects already represented in items (for orphan detection)
			var emittedObjects = new HashSet<GameObject>();
			foreach (var item in items) {
				if (item.GameObject != null)
					emittedObjects.Add(item.GameObject);
			}

			// Process groups in reverse to preserve indices
			for (int g = groups.Count - 1; g >= 0; g--) {
				var (parent, start, count) = groups[g];

				// Verify exactly one active (confirms mutual exclusivity)
				int onCount = 0;
				for (int j = start; j < start + count; j++) {
					if (IsToggleActive((KToggle)items[j].Component))
						onCount++;
				}
				if (onCount != 1) continue;

				// Build member list
				var members = new List<RadioMember>();
				for (int j = start; j < start + count; j++) {
					members.Add(new RadioMember {
						Label = items[j].Label,
						Toggle = (KToggle)items[j].Component
					});
				}

				// Search the full screen tree for an orphan description LocText
				var descriptionLt = FindOrphanDescription(screenRoot, emittedObjects, claimedLabels);

				string groupLabel = screenTitle ?? items[start].Label;
				var radioMembers = members;
				items[start] = new DropdownWidget {
					Label = groupLabel,
					Component = members[0].Toggle,
					GameObject = parent.gameObject,
					Tag = radioMembers,
					SpeechFunc = () => {
						string selected = null;
						for (int k = 0; k < radioMembers.Count; k++) {
							if (radioMembers[k].Toggle != null && IsToggleActive(radioMembers[k].Toggle)) {
								selected = radioMembers[k].Label;
								break;
							}
						}
						string speech = selected != null
							? $"{groupLabel}, {selected}" : groupLabel;
						if (descriptionLt != null) {
							string desc = descriptionLt.GetParsedText();
							if (!string.IsNullOrEmpty(desc))
								speech += ", " + desc;
						}
						return speech;
					}
				};

				// Remove the collapsed items
				items.RemoveRange(start + 1, count - 1);
			}
		}

		/// <summary>
		/// Search the screen's full transform tree for a LocText that wasn't
		/// emitted as a widget item — a candidate description label. Skips
		/// LocTexts that are children of interactive widgets (those are labels,
		/// not descriptions).
		/// </summary>
		private static LocText FindOrphanDescription(
				Transform root, HashSet<GameObject> emittedObjects,
				HashSet<LocText> claimedLabels) {
			var allLocTexts = root.GetComponentsInChildren<LocText>(false);
			foreach (var lt in allLocTexts) {
				if (emittedObjects.Contains(lt.gameObject)) continue;
				if (claimedLabels.Contains(lt)) continue;

				string text = lt.GetParsedText();
				if (!HasVisibleContent(text)) continue;

				// Skip LocTexts inside interactive widgets (they're labels)
				if (lt.GetComponentInParent<KToggle>() != null) continue;
				if (lt.GetComponentInParent<KButton>() != null) continue;
				if (lt.GetComponentInParent<KSlider>() != null) continue;
				if (lt.GetComponentInParent<MultiToggle>() != null) continue;

				// Skip the screen title (GetTitle() reads from a LocText too)
				var ssc = root.GetComponent<SideScreenContent>();
				if (ssc != null) {
					string title = ssc.GetTitle();
					if (text == title) continue;
				}

				return lt;
			}
			return null;
		}

	}
}
