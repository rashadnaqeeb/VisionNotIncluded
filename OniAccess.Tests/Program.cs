using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using OniAccess.ConduitTracking;
using OniAccess.Handlers;
using OniAccess.Handlers.Build;
using OniAccess.Handlers.Screens.Details;
using RectangleSelection = OniAccess.Handlers.RectangleSelection;
using OniAccess.Handlers.Notifications;
using OniAccess.Handlers.Screens.ClusterMap;
using OniAccess.Handlers.Tiles;
using OniAccess.Handlers.Tiles.Scanner;
using OniAccess.Handlers.Tiles.Sections;
using OniAccess.Speech;
using OniAccess.Util;
using OniAccess.Widgets;
using UnityEngine;

namespace OniAccess.Tests {
	class Program {
		static int Main(string[] args) {
			// Resolve game assemblies at runtime from ONI_MANAGED
			var managed = Environment.GetEnvironmentVariable("ONI_MANAGED")
				?? @"C:\Program Files (x86)\Steam\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed";

			AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
				var name = new AssemblyName(e.Name).Name;
				var path = Path.Combine(managed, name + ".dll");
				return File.Exists(path) ? Assembly.LoadFrom(path) : null;
			};

			return RunTests();
		}

		static int RunTests() {
			// Replace Unity time/frame sources to avoid native calls in tests
			SpeechPipeline.TimeSource = () => 0f;
			SpeechPipeline.SpeakAction = (text, intr) => { };
			HandlerStack.FrameSource = () => 0;

			var results = new List<(string name, bool passed, string detail)>();

			// --- HandlerStack ---
			results.Add(PopExposesLowerHandler());
			results.Add(PushCallsOnActivate());
			results.Add(PopCallsOnDeactivate());
			results.Add(PopReactivatesExposedHandler());
			results.Add(ReplaceSwapsHandlers());
			results.Add(DeactivateAllCallsOnDeactivate());
			results.Add(ReplaceOnEmptyStack());
			results.Add(PopOnEmptyStack());
			results.Add(RapidPushPopSequence());
			results.Add(ExceptionInOnActivateDoesNotCorruptStack());
			results.Add(ExceptionInOnDeactivateDoesNotCorruptStack());

			// --- CollectHelpEntries ---
			results.Add(CollectHelpEntriesEmptyStack());
			results.Add(CollectHelpEntriesSingleHandler());
			results.Add(CollectHelpEntriesTwoNonCapturing());
			results.Add(CollectHelpEntriesBarrierStopsWalk());
			results.Add(CollectHelpEntriesKeyDedup());
			results.Add(CollectHelpEntriesBarrierInclusive());

			HandlerStack.Clear();

			// --- TypeAheadSearch ---
			results.Add(SearchWordStartMatch());
			results.Add(SearchMultiWordMatch());
			results.Add(SearchCaseInsensitive());
			results.Add(SearchMultiCharNarrowing());
			results.Add(SearchRepeatLetterCycles());
			results.Add(SearchRepeatLetterSkipsSubstring());
			results.Add(SearchBackspace());
			results.Add(SearchNavigateWraps());
			results.Add(SearchJumpFirstLast());
			results.Add(SearchNoMatch());
			results.Add(MatchTierStartWholeWord());
			results.Add(MatchTierStartPrefix());
			results.Add(MatchTierMidWholeWord());
			results.Add(MatchTierMidPrefix());
			results.Add(MatchTierSubstring());
			results.Add(MatchTierNoMatch());
			results.Add(SearchTierOrdering());
			results.Add(SearchTierPositionSorting());
			results.Add(SearchSpaceMultiWord());
			results.Add(SearchTrailingSpaceIgnored());
			results.Add(SearchNameLengthTiebreaker());
			results.Add(SearchLengthBeatsPosition());
			results.Add(SearchMultiTokenAbbreviation());
			results.Add(MatchTierMultiTokenAbbreviation());
			results.Add(MatchTierMultiTokenOrderRequired());
			results.Add(MatchTierMultiTokenCrossSegment());
			results.Add(SearchNameMatchBeatsDescriptionMatch());
			results.Add(SearchMultiTokenNameBeatsDescription());
			results.Add(SearchSortsByNameLengthNotFullLabel());
			results.Add(MatchTierAccentInsensitive());
			results.Add(MatchTierAccentedQuery());
			results.Add(MatchTierLigatureOe());

			// --- ScannerSearch ---
			results.Add(ScannerSearchPrefixMatch());
			results.Add(ScannerSearchWholeWordMatch());
			results.Add(ScannerSearchWordStartMatch());
			results.Add(ScannerSearchNoMatch());
			results.Add(ScannerSearchCaseInsensitive());
			results.Add(ScannerSearchFilterRemapsCategory());
			results.Add(ScannerSearchBestMatchAcrossPositions());
			results.Add(ScannerSearchAccentInsensitive());
			results.Add(ScannerSearchKoreanQuery());
			results.Add(ScannerSearchMatcherStripsLinkFormatting());

			// --- TextFilter ---
			TextFilter.RegisterSprite("warning", "warning:");
			TextFilter.RegisterSprite("logic_signal_green", "green signal");
			TextFilter.RegisterSprite("logic_signal_red", "red signal");

			results.Add(TextFilterStripsBold());
			results.Add(TextFilterStripsColor());
			results.Add(TextFilterStripsNested());
			results.Add(TextFilterConvertsSpriteToText());
			results.Add(TextFilterStripsUnregisteredSprite());
			results.Add(TextFilterStripsTmpBracketSprites());
			results.Add(TextFilterHandlesLinkTags());
			results.Add(TextFilterNormalizesWhitespace());
			results.Add(TextFilterHandlesEmptyAndNull());
			results.Add(TextFilterPreservesPlainText());
			results.Add(TextFilterStripsHotkey());
			results.Add(TextFilterStripsSizeStyle());
			results.Add(TextFilterUnclosedTag());
			results.Add(TextFilterMismatchedTags());
			results.Add(TextFilterSpriteNameCaseInsensitive());
			results.Add(TextFilterReplacesMasculineOrdinalDegree());
			results.Add(TextFilterStripsTempUnitCelsius());
			results.Add(TextFilterStripsTempUnitFahrenheit());
			results.Add(TextFilterPreservesKelvin());
			results.Add(TextFilterStripsBullet());
			results.Add(TextFilterPreservesNumericBrackets());
			results.Add(TextFilterStripsControlChars());
			results.Add(TextFilterCombinedMarkup());
			TextFilter.SetOriginalMutationLabel("Original");
			results.Add(TextFilterStripsOriginalMutation());
			results.Add(TextFilterPreservesMutatedPlant());

			// --- Log class ---
			results.Add(LogWarnRoutesToWarnFn());
			results.Add(LogErrorRoutesToErrorFn());
			results.Add(LogBackendSwapWorks());

			// --- HandlerStack diagnostic quality ---
			results.Add(PushFailureLogsHandlerNameAndException());
			results.Add(PushNullLogsWarning());
			results.Add(PopOnEmptyLogsWarning());
			results.Add(ReplaceFailureLogsHandlerNameAndException());

			HandlerStack.Clear();

			// --- TooltipCapture ---
			TooltipCapture.Reset();
			results.Add(TooltipCaptureEmptyFrameReturnsNull());
			results.Add(TooltipCaptureTextOutsideBlockDiscarded());
			results.Add(TooltipCaptureSingleBlock());
			results.Add(TooltipCaptureTwoBlocks());
			results.Add(TooltipCaptureMultipleTokensInBlock());
			results.Add(TooltipCaptureNewLineSeparatesLines());
			results.Add(TooltipCaptureEmptyTextSkipped());
			results.Add(TooltipCaptureResetClearsState());
			results.Add(TooltipCaptureGetLinesGroupsByBlock());
			TooltipCapture.Reset();

			// --- UnionFind ---
			results.Add(UnionFindSameSetAfterUnion());
			results.Add(UnionFindDisjointSetsDistinct());
			results.Add(UnionFindTransitiveUnion());
			results.Add(UnionFindSelfUnionNoOp());
			results.Add(UnionFindDuplicateUnionNoOp());
			results.Add(UnionFindResetReinitializes());
			results.Add(UnionFindResetReallocatesOnSizeChange());
			results.Add(UnionFindLargeChainMerge());

			// --- SpeechPipeline ---
			results.Add(PipelineDisabledSkipsSpeech());
			results.Add(PipelineEnabledSpeaks());
			results.Add(PipelineFiltersBeforeSpeaking());
			results.Add(PipelineDeduplicatesSameText());
			results.Add(PipelineDuplicateSuppressedWithinWindow());
			results.Add(PipelineAllowsSameTextAfterWindow());
			results.Add(PipelineAllowsDifferentTextImmediately());
			results.Add(PipelineNullAndEmptySkipped());
			results.Add(PipelineInterruptFlagIsTrue());
			results.Add(PipelineQueuedSpeaksWithoutInterrupt());
			results.Add(PipelineQueuedNotDeduplicated());
			results.Add(PipelineInterruptDedupeDoesNotAffectQueued());

			// --- ColorNameUtil ---
			results.Add(ColorNameUtilAllPaletteColorsMapped());
			results.Add(ColorNameUtilNoDuplicateNames());
			results.Add(ColorNameUtilUnknownColorReturnsNull());

			// --- ScannerSnapshot ---
			results.Add(RemoveInstanceKeepsStructure());
			results.Add(RemoveLastInstancePrunesBothSubcategories());
			results.Add(PruneEmptySubcategory());
			results.Add(FullCascadePrunesCategory());

			// --- Custom scanner categories ---
			results.Add(CustomCategorySortsToFront());
			results.Add(CustomAllSelectorGathersAcrossSubs());
			results.Add(CustomNamedSelectorFiltersToSub());
			results.Add(CustomItemsDistinctFromReal());
			results.Add(CustomAllSharesRefWithinCustom());
			results.Add(CustomEmptyCategorySkipped());
			results.Add(CustomPruneDoesNotAffectReal());
			results.Add(CustomKeywordGathersMatches());
			results.Add(CustomKeywordSortsBeforeSelectors());
			results.Add(CustomKeywordRanksByMatchQuality());
			results.Add(CustomKeywordSelectorOverlapDedupsAll());
			results.Add(CustomKeywordPrunePropagates());
			results.Add(CustomSameNameDistinctSubcategoriesNotMerged());
			results.Add(CustomKeywordCrossCategoryNotMerged());
			results.Add(StoreSetAllSupersedesSubs());
			results.Add(StoreToggleSubWhileAllExpands());
			results.Add(StoreToggleSubIndividual());
			results.Add(StoreSetAllOffClears());

			// --- WrapSkipEmpty ---
			results.Add(WrapSkipEmptyForwardWrap());
			results.Add(WrapSkipEmptyBackwardWrap());
			results.Add(WrapSkipEmptyAllEmptyReturnsCurrent());
			results.Add(WrapSkipEmptySingleNonEmpty());

			// --- ScannerTaxonomy ---
			results.Add(TaxonomyAllCategoriesHaveSubcategories());
			results.Add(TaxonomySortIndicesRoundTrip());

			// --- GlanceComposer ---
			results.Add(ComposerThrowingSectionDoesNotAbortOthers());
			results.Add(ComposerAllEmptyReturnsNull());

			// --- AnnouncementFormatter ---
			results.Add(FormatDistanceSameCellReturnsEmpty());
			results.Add(FormatDistanceVerticalOnly());
			results.Add(FormatDistanceHorizontalOnly());
			results.Add(FormatDistanceBothAxes());
			results.Add(FormatClusterSingleDelegatesToEntity());
			results.Add(FormatClusterMultiIncludesCount());

			// --- BuildMenuData ---
			results.Add(OrientationNameCoversAllKnownValues());
			results.Add(OrientationNameDefaultReturnsUp());
			results.Add(OrientationNameHorizontalFlowShiftsCW());
			results.Add(AppendOrientationUsesFacingForDirectionalRotations());
			results.Add(AppendOrientationLeavesR90AsPlainAxis());

			// --- CleanTooltipEntry ---
			results.Add(CleanTooltipSingleNewline());
			results.Add(CleanTooltipTripleNewlineCollapses());
			results.Add(CleanTooltipBulletWithSpace());
			results.Add(CleanTooltipBulletWithSurroundingSpaces());
			results.Add(CleanTooltipLeadingWhitespaceTrimmed());
			results.Add(CleanTooltipDoubledPeriodCollapsed());
			results.Add(CleanTooltipIndentedBulletAfterNewline());
			results.Add(CleanTooltipColonNewlineDropsPeriod());
			results.Add(CleanTooltipNullAndEmptyPassthrough());
			results.Add(CleanTooltipStripsReplacementChar());

			// --- AppendTooltip ---
			results.Add(AppendTooltipNullReturnsSpeech());
			results.Add(AppendTooltipEmptySpeechReturnsTooltip());
			results.Add(AppendTooltipDuplicateSegmentSuppressed());
			results.Add(AppendTooltipNonMatchingAppends());
			results.Add(AppendTooltipSubstringNotSuppressed());
			results.Add(AppendTooltipSingleSegmentDuplicate());
			results.Add(AppendTooltipSentenceDedup());
			results.Add(AppendTooltipAllSentencesDuplicate());

			// --- NavigableGraph ---
			results.Add(GraphNavigateDownSetsSiblingContext());
			results.Add(GraphNavigateUpAtRootEstablishesRootContext());
			results.Add(GraphNavigateUpToRootUsesRoots());
			results.Add(GraphCycleSiblingWrapForward());
			results.Add(GraphCycleSiblingWrapBackward());
			results.Add(GraphCycleSiblingNoWrap());
			results.Add(GraphMoveToClearsSiblingContext());
			results.Add(GraphMoveToWithSiblingsPreservesContext());
			results.Add(GraphIndexOfFallbackReturnsZero());
			results.Add(GraphSingleSiblingReturnsNull());

			// --- NotificationAnnouncer ---
			results.Add(AnnouncerLoadPhaseHoldsUntilUnpause());
			results.Add(AnnouncerFirstFlushUsesLongWindow());
			results.Add(AnnouncerSubsequentFlushUsesShortWindow());
			results.Add(AnnouncerCountDeltaOnlyAnnouncesIncreases());
			results.Add(AnnouncerStaleKeyCleanupAllowsReannouncement());
			results.Add(AnnouncerFirstGroupInterruptsRestQueue());
			results.Add(AnnouncerBatchWindowResetsOnNewArrival());

			// --- LoadGate ---
			results.Add(LoadGateStartsNotReady());
			results.Add(LoadGateStaysGatedWhilePaused());
			results.Add(LoadGateNotReadyAtPointNineSeconds());
			results.Add(LoadGateReadyAt1Second());
			results.Add(LoadGateResetRequiresFullCycle());
			results.Add(LoadGateStartingUnpausedStillWaits());

			// --- GridUtil.ValidateCluster ---
			results.Add(ValidateClusterAllPrunedReturnsFalse());
			results.Add(ValidateClusterClosestCellSelected());
			results.Add(ValidateClusterStaleCellsRemoved());
			results.Add(ValidateClusterSingleSurvivor());

			// --- ClusterScanSnapshot ---
			results.Add(ClusterSnapshotCategoryOrder());
			results.Add(ClusterSnapshotAllSharesReferences());
			results.Add(ClusterSnapshotItemSortBySortKeyThenDistance());
			results.Add(ClusterSnapshotSkipAllCategory());
			results.Add(ClusterSnapshotRemovePrunesFromAllAndNamed());
			results.Add(ClusterSnapshotRemoveLastPrunesCategory());

			// --- ClusterMapTaxonomy ---
			results.Add(ClusterTaxonomySortIndicesRoundTrip());

			// --- HexCoordinates ---
			results.Add(HexCompassAllOctants());
			results.Add(HexFormatSameHexReturnsHere());

			// --- HexPathfinder.FormatResult ---
			results.Add(FormatResultNoPath());
			results.Add(FormatResultVisibleOnly());
			results.Add(FormatResultFogOnly());
			results.Add(FormatResultBothFogShorter());
			results.Add(FormatResultBothVisibleShorter());

			// --- StatusFilter.ShouldSpeak ---
			StatusFilterTests.Initialize();
			results.Add(StatusFilterTests.OverlayMatchingItem());
			results.Add(StatusFilterTests.OverlayNonMatchingItem());
			results.Add(StatusFilterTests.FarmingNeutralPlant());
			results.Add(StatusFilterTests.FarmingNeutralNonPlant());
			results.Add(StatusFilterTests.DefaultAlwaysNeutral());
			results.Add(StatusFilterTests.DefaultOtherNeutralSuppressed());
			results.Add(StatusFilterTests.DefaultBadNotOverlay());
			results.Add(StatusFilterTests.DefaultBadClaimedByOverlay());

			// --- RectangleSelection ---
			results.Add(RectSelectionFirstCornerSet());
			results.Add(RectSelectionSecondCornerCompletesRect());
			results.Add(RectSelectionClearRectRemovesContainingRect());
			results.Add(RectSelectionClearRectReturnsFalseWhenEmpty());
			results.Add(RectSelectionMultiRectAccumulation());
			results.Add(RectSelectionIsCellSelectedInRect());
			results.Add(RectSelectionIsCellSelectedOutsideRect());
			results.Add(RectSelectionClearAllResetsState());
			results.Add(RectSelectionAutoSelectSingle());
			results.Add(RectSelectionAddRectangleDirect());
			results.Add(RectSelectionComputeArea());
			results.Add(RectSelectionTileCountBetween());

			// --- CursorBookmarks.DigitKeyToIndex ---
			results.Add(DigitKeyAlpha1Through9());
			results.Add(DigitKeyAlpha0MapsToNine());
			results.Add(DigitKeyKeypad1Through9());
			results.Add(DigitKeyKeypad0MapsToNine());
			results.Add(DigitKeyNonDigitReturnsNegativeOne());

			// --- SectionMerger ---
			results.Add(MergerMatchedItemsPreserveOrder());
			results.Add(MergerItemRemoved());
			results.Add(MergerItemAdded());
			results.Add(MergerSectionRemoved());
			results.Add(MergerSectionAdded());
			results.Add(MergerJitterPreservesOrder());
			results.Add(MergerChildrenMerged());
			results.Add(MergerMissingKeyFallback());
			results.Add(MergerTypeMismatchReplaces());
			results.Add(MergerUpdateFromCopiesFields());

			// --- FlowTracker ---
			results.Add(FlowTrackerGetDirectionCountsUninitialized());
			results.Add(FlowTrackerGetDirectionCountsOutOfRange());
			results.Add(FlowTrackerGetDirectionCountsSingleSample());
			results.Add(FlowTrackerGetDirectionCountsPartialBuffer());
			results.Add(FlowTrackerGetDirectionCountsWrappedBuffer());
			results.Add(FlowTrackerGetDirectionCountsMultiConduit());
			results.Add(FlowTrackerGetElementCountsSkipsDirNone());
			results.Add(FlowTrackerGetElementCountsGroupsByElement());
			results.Add(FlowTrackerGetElementCountsWrapped());
			results.Add(FlowTrackerClearResetsState());

			// --- NavTree (lazy-children navigation engine) ---
			foreach (var r in NavTreeTests.All())
				results.Add(r);

			// --- Verbose-UI composer (WidgetSpeech + VerboseMeta) ---
			foreach (var r in VerboseSpeechTests.All())
				results.Add(r);

			// --- LineReview (Alt+Up/Down line stepping) ---
			foreach (var r in LineReviewTests.All())
				results.Add(r);

			int passed = 0, failed = 0;
			foreach (var (name, ok, detail) in results) {
				if (ok) {
					Console.WriteLine($"  PASS  {name}");
					passed++;
				} else {
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"  FAIL  {name}: {detail}");
					Console.ResetColor();
					failed++;
				}
			}

			Console.WriteLine();
			Console.WriteLine($"{passed} passed, {failed} failed, {results.Count} total");

			return failed > 0 ? 1 : 0;
		}

		// --- Test handler ---

		private class TestHandler: IAccessHandler {
			public string DisplayName { get; }
			public bool CapturesAllInput { get; }
			public IReadOnlyList<HelpEntry> HelpEntries { get; }
			public IReadOnlyList<ConsumedKey> ConsumedKeys { get; }
				= Array.Empty<ConsumedKey>();

			public int ActivateCount { get; private set; }
			public int DeactivateCount { get; private set; }
			public int TickCount { get; private set; }
			public bool ConsumeKeyDown { get; set; }
			public int HandleKeyDownCount { get; private set; }

			public TestHandler(string name, bool capturesAll = false,
				IReadOnlyList<HelpEntry> helpEntries = null) {
				DisplayName = name;
				CapturesAllInput = capturesAll;
				HelpEntries = helpEntries ?? new List<HelpEntry>().AsReadOnly();
			}

			public bool Tick() { TickCount++; return false; }
			public bool HandleKeyDown(KButtonEvent e) { HandleKeyDownCount++; return ConsumeKeyDown; }
			public void OnActivate() => ActivateCount++;
			public void OnDeactivate() => DeactivateCount++;
		}

		private class ThrowingHandler: IAccessHandler {
			public string DisplayName => "Thrower";
			public bool CapturesAllInput => false;
			public IReadOnlyList<HelpEntry> HelpEntries { get; }
				= new List<HelpEntry>().AsReadOnly();
			public IReadOnlyList<ConsumedKey> ConsumedKeys { get; }
				= Array.Empty<ConsumedKey>();

			public bool ThrowOnActivate { get; set; }
			public bool ThrowOnDeactivate { get; set; }

			public bool Tick() => false;
			public bool HandleKeyDown(KButtonEvent e) => false;

			public void OnActivate() {
				if (ThrowOnActivate)
					throw new InvalidOperationException("OnActivate exploded");
			}

			public void OnDeactivate() {
				if (ThrowOnDeactivate)
					throw new InvalidOperationException("OnDeactivate exploded");
			}
		}

		// --- Helpers ---

		private static void Reset() => HandlerStack.Clear();

		private static (string, bool, string) Assert(string name, bool ok, string detail)
			=> (name, ok, ok ? "OK" : detail);

		// --- Mock data for TypeAheadSearch ---

		private static readonly string[] SearchItems =
			{ "Apple", "Apricot", "Banana", "Blue Cheese", "Cherry", null, "" };

		private static string NameByIndex(int i) =>
			i >= 0 && i < SearchItems.Length ? SearchItems[i] : null;

		// ========================================
		// HandlerStack tests
		// ========================================

		private static (string, bool, string) PopExposesLowerHandler() {
			Reset();
			var bottom = new TestHandler("Bottom");
			var top = new TestHandler("Top");
			HandlerStack.Push(bottom);
			HandlerStack.Push(top);
			HandlerStack.Pop();

			bool ok = HandlerStack.ActiveHandler == bottom && HandlerStack.Count == 1;
			return Assert("PopExposesLowerHandler", ok,
				$"ActiveHandler={HandlerStack.ActiveHandler?.DisplayName ?? "null"}, count={HandlerStack.Count}");
		}

		private static (string, bool, string) PushCallsOnActivate() {
			Reset();
			var handler = new TestHandler("Test");
			HandlerStack.Push(handler);

			bool ok = handler.ActivateCount == 1;
			return Assert("PushCallsOnActivate", ok,
				$"ActivateCount={handler.ActivateCount}");
		}

		private static (string, bool, string) PopCallsOnDeactivate() {
			Reset();
			var handler = new TestHandler("Test");
			HandlerStack.Push(handler);
			HandlerStack.Pop();

			bool ok = handler.DeactivateCount == 1;
			return Assert("PopCallsOnDeactivate", ok,
				$"DeactivateCount={handler.DeactivateCount}");
		}

		private static (string, bool, string) PopReactivatesExposedHandler() {
			Reset();
			var bottom = new TestHandler("Bottom");
			var top = new TestHandler("Top");
			HandlerStack.Push(bottom); // ActivateCount = 1
			HandlerStack.Push(top);
			HandlerStack.Pop(); // should reactivate bottom

			bool ok = bottom.ActivateCount == 2;
			return Assert("PopReactivatesExposedHandler", ok,
				$"ActivateCount={bottom.ActivateCount}, expected 2");
		}

		private static (string, bool, string) ReplaceSwapsHandlers() {
			Reset();
			var first = new TestHandler("First");
			var second = new TestHandler("Second");
			HandlerStack.Push(first);
			HandlerStack.Replace(second);

			bool ok = HandlerStack.ActiveHandler == second
				   && HandlerStack.Count == 1
				   && first.DeactivateCount == 1
				   && second.ActivateCount == 1;
			return Assert("ReplaceSwapsHandlers", ok,
				$"Active={HandlerStack.ActiveHandler?.DisplayName ?? "null"}, " +
				$"count={HandlerStack.Count}, " +
				$"first.Deactivate={first.DeactivateCount}, " +
				$"second.Activate={second.ActivateCount}");
		}

		private static (string, bool, string) DeactivateAllCallsOnDeactivate() {
			Reset();
			var bottom = new TestHandler("Bottom");
			var top = new TestHandler("Top");
			HandlerStack.Push(bottom);
			HandlerStack.Push(top);
			HandlerStack.DeactivateAll();

			bool ok = top.DeactivateCount == 1
				   && bottom.DeactivateCount == 1
				   && HandlerStack.Count == 0;
			return Assert("DeactivateAllCallsOnDeactivate", ok,
				$"top.Deactivate={top.DeactivateCount}, " +
				$"bottom.Deactivate={bottom.DeactivateCount}, " +
				$"count={HandlerStack.Count}");
		}

		// ========================================
		// HandlerStack edge cases
		// ========================================

		private static (string, bool, string) ReplaceOnEmptyStack() {
			Reset();
			var handler = new TestHandler("Replacement");
			HandlerStack.Replace(handler);

			bool ok = HandlerStack.Count == 1 && handler.ActivateCount == 1;
			return Assert("ReplaceOnEmptyStack", ok,
				$"count={HandlerStack.Count}, ActivateCount={handler.ActivateCount}");
		}

		private static (string, bool, string) PopOnEmptyStack() {
			Reset();
			try {
				HandlerStack.Pop();
			} catch (Exception ex) {
				return Assert("PopOnEmptyStack", false, $"threw {ex.GetType().Name}");
			}

			bool ok = HandlerStack.Count == 0;
			return Assert("PopOnEmptyStack", ok, $"count={HandlerStack.Count}");
		}

		private static (string, bool, string) RapidPushPopSequence() {
			Reset();
			var a = new TestHandler("A");
			var b = new TestHandler("B");
			var c = new TestHandler("C");

			HandlerStack.Push(a);
			HandlerStack.Push(b);
			HandlerStack.Pop();
			HandlerStack.Push(c);
			HandlerStack.Pop();
			HandlerStack.Pop();

			// a: activated on push(1), reactivated after pop(b)(2), reactivated after pop(c)(3)
			// a: deactivated on final pop(1)
			bool ok = HandlerStack.Count == 0
				   && a.ActivateCount == 3
				   && a.DeactivateCount == 1
				   && b.DeactivateCount == 1
				   && c.DeactivateCount == 1;
			return Assert("RapidPushPopSequence", ok,
				$"count={HandlerStack.Count}, a.Act={a.ActivateCount}, a.Deact={a.DeactivateCount}, " +
				$"b.Deact={b.DeactivateCount}, c.Deact={c.DeactivateCount}");
		}

		private static (string, bool, string) ExceptionInOnActivateDoesNotCorruptStack() {
			Reset();
			var good = new TestHandler("Good");
			HandlerStack.Push(good);

			var thrower = new ThrowingHandler { ThrowOnActivate = true };
			bool threw = false;
			try {
				HandlerStack.Push(thrower);
			} catch (InvalidOperationException) {
				threw = true;
			}

			// Push calls OnActivate before adding to stack. If OnActivate throws,
			// Push catches the exception, logs it, and does NOT add the handler.
			// The exception does not propagate. Stack stays clean with only "Good".
			bool ok = !threw && HandlerStack.Count == 1 && HandlerStack.ActiveHandler == good;
			return Assert("ExceptionInOnActivateDoesNotCorruptStack", ok,
				$"threw={threw}, count={HandlerStack.Count}");
		}

		private static (string, bool, string) ExceptionInOnDeactivateDoesNotCorruptStack() {
			Reset();
			var bottom = new TestHandler("Bottom");
			var thrower = new ThrowingHandler { ThrowOnDeactivate = true };
			HandlerStack.Push(bottom);
			HandlerStack.Push(thrower);

			// Pop wraps OnDeactivate in try/catch, so the exception should not propagate.
			HandlerStack.Pop();

			// Thrower is removed, bottom is reactivated despite the throw.
			bool ok = HandlerStack.Count == 1
				   && HandlerStack.ActiveHandler == bottom
				   && bottom.ActivateCount == 2;
			return Assert("ExceptionInOnDeactivateDoesNotCorruptStack", ok,
				$"count={HandlerStack.Count}, " +
				$"active={HandlerStack.ActiveHandler?.DisplayName ?? "null"}, " +
				$"activateCount={bottom.ActivateCount}");
		}

		// ========================================
		// CollectHelpEntries tests
		// ========================================

		private static IReadOnlyList<HelpEntry> MakeEntries(params string[] keys) {
			var list = new List<HelpEntry>();
			foreach (var k in keys)
				list.Add(new HelpEntry(k, $"desc-{k}"));
			return list.AsReadOnly();
		}

		private static (string, bool, string) CollectHelpEntriesEmptyStack() {
			Reset();
			var entries = HandlerStack.CollectHelpEntries();
			bool ok = entries.Count == 0;
			return Assert("CollectHelpEntriesEmptyStack", ok,
				$"count={entries.Count}");
		}

		private static (string, bool, string) CollectHelpEntriesSingleHandler() {
			Reset();
			HandlerStack.Push(new TestHandler("A", helpEntries: MakeEntries("F1", "F2")));
			var entries = HandlerStack.CollectHelpEntries();
			bool ok = entries.Count == 2
				   && entries[0].KeyName == "F1"
				   && entries[1].KeyName == "F2";
			return Assert("CollectHelpEntriesSingleHandler", ok,
				$"count={entries.Count}");
		}

		private static (string, bool, string) CollectHelpEntriesTwoNonCapturing() {
			Reset();
			HandlerStack.Push(new TestHandler("Bottom", capturesAll: false,
				helpEntries: MakeEntries("F1")));
			HandlerStack.Push(new TestHandler("Top", capturesAll: false,
				helpEntries: MakeEntries("F2")));
			var entries = HandlerStack.CollectHelpEntries();
			bool ok = entries.Count == 2
				   && entries[0].KeyName == "F2"
				   && entries[1].KeyName == "F1";
			return Assert("CollectHelpEntriesTwoNonCapturing", ok,
				$"count={entries.Count}, [0]={entries[0]?.KeyName}, [1]={entries[1]?.KeyName}");
		}

		private static (string, bool, string) CollectHelpEntriesBarrierStopsWalk() {
			Reset();
			HandlerStack.Push(new TestHandler("Bottom", capturesAll: false,
				helpEntries: MakeEntries("F1")));
			HandlerStack.Push(new TestHandler("Barrier", capturesAll: true,
				helpEntries: MakeEntries("F2")));
			var entries = HandlerStack.CollectHelpEntries();
			// Barrier is inclusive — its entries included, but bottom excluded
			bool ok = entries.Count == 1 && entries[0].KeyName == "F2";
			return Assert("CollectHelpEntriesBarrierStopsWalk", ok,
				$"count={entries.Count}");
		}

		private static (string, bool, string) CollectHelpEntriesKeyDedup() {
			Reset();
			HandlerStack.Push(new TestHandler("Bottom", capturesAll: false,
				helpEntries: MakeEntries("F1", "F2")));
			HandlerStack.Push(new TestHandler("Top", capturesAll: false,
				helpEntries: MakeEntries("F1", "F3")));
			var entries = HandlerStack.CollectHelpEntries();
			// F1 from Top wins, Bottom's F1 suppressed. F3 from Top + F2 from Bottom.
			bool ok = entries.Count == 3
				   && entries[0].KeyName == "F1"
				   && entries[0].Description == "desc-F1"
				   && entries[1].KeyName == "F3"
				   && entries[2].KeyName == "F2";
			return Assert("CollectHelpEntriesKeyDedup", ok,
				$"count={entries.Count}");
		}

		private static (string, bool, string) CollectHelpEntriesBarrierInclusive() {
			Reset();
			HandlerStack.Push(new TestHandler("Bottom", capturesAll: false,
				helpEntries: MakeEntries("F1")));
			HandlerStack.Push(new TestHandler("Barrier", capturesAll: true,
				helpEntries: MakeEntries("F2", "F3")));
			HandlerStack.Push(new TestHandler("Top", capturesAll: false,
				helpEntries: MakeEntries("F4")));
			var entries = HandlerStack.CollectHelpEntries();
			// Top(F4) + Barrier(F2,F3) — barrier is inclusive, Bottom excluded
			bool ok = entries.Count == 3
				   && entries[0].KeyName == "F4"
				   && entries[1].KeyName == "F2"
				   && entries[2].KeyName == "F3";
			return Assert("CollectHelpEntriesBarrierInclusive", ok,
				$"count={entries.Count}");
		}

		// ========================================
		// TypeAheadSearch tests
		// ========================================

		private static (string, bool, string) SearchWordStartMatch() {
			var search = new TypeAheadSearch();
			search.AddChar('a');
			search.Search(SearchItems.Length, NameByIndex);

			// 'a' matches Apple(0,tier1), Apricot(1,tier1), Banana(2,tier4 substring)
			bool ok = search.ResultCount == 3 && search.SelectedOriginalIndex == 0;
			return Assert("SearchWordStartMatch", ok,
				$"ResultCount={search.ResultCount}, SelectedOriginalIndex={search.SelectedOriginalIndex}");
		}

		private static (string, bool, string) SearchMultiWordMatch() {
			var search = new TypeAheadSearch();
			search.AddChar('c');
			search.Search(SearchItems.Length, NameByIndex);

			// 'c' matches Cherry(4,tier1), Blue Cheese(3,tier3 mid-word),
			// Apricot(1,tier4 substring). Verify Blue Cheese is in the results
			// to prove mid-word matching works.
			bool foundBlueCheese = false;
			for (int i = 0; i < search.ResultCount; i++) {
				if (search.SelectedOriginalIndex == 3) foundBlueCheese = true;
				if (i < search.ResultCount - 1) search.NavigateResults(1);
			}
			bool ok = search.ResultCount == 3 && foundBlueCheese;
			return Assert("SearchMultiWordMatch", ok,
				$"ResultCount={search.ResultCount}, foundBlueCheese={foundBlueCheese}");
		}

		private static (string, bool, string) SearchCaseInsensitive() {
			var search = new TypeAheadSearch();
			search.AddChar('B'); // uppercase input
			search.Search(SearchItems.Length, NameByIndex);

			// Uppercase 'B' must still match Banana(2), Blue Cheese(3)
			// because Search lowercases the buffer before matching.
			bool ok = search.ResultCount == 2;
			return Assert("SearchCaseInsensitive", ok,
				$"ResultCount={search.ResultCount}");
		}

		private static (string, bool, string) SearchMultiCharNarrowing() {
			var search = new TypeAheadSearch();

			search.AddChar('a');
			search.Search(SearchItems.Length, NameByIndex);
			int afterA = search.ResultCount;

			search.AddChar('p');
			search.Search(SearchItems.Length, NameByIndex);
			int afterAp = search.ResultCount;

			search.AddChar('r');
			search.Search(SearchItems.Length, NameByIndex);
			int afterApr = search.ResultCount;

			// 'a' also matches Banana(substring), 'ap' narrows to Apple+Apricot, 'apr' to Apricot
			bool ok = afterA == 3 && afterAp == 2 && afterApr == 1
				   && search.SelectedOriginalIndex == 1; // Apricot
			return Assert("SearchMultiCharNarrowing", ok,
				$"a={afterA}, ap={afterAp}, apr={afterApr}, idx={search.SelectedOriginalIndex}");
		}

		private static (string, bool, string) SearchRepeatLetterCycles() {
			var search = new TypeAheadSearch();
			search.AddChar('b');
			search.Search(SearchItems.Length, NameByIndex);
			int firstIdx = search.SelectedOriginalIndex; // Banana(2)

			search.AddChar('b'); // buffer="bb", triggers cycle
			search.Search(SearchItems.Length, NameByIndex);
			int secondIdx = search.SelectedOriginalIndex; // Blue Cheese(3)

			bool ok = firstIdx == 2 && secondIdx == 3
				   && search.Buffer == "b";
			return Assert("SearchRepeatLetterCycles", ok,
				$"first={firstIdx}, second={secondIdx}, buffer=\"{search.Buffer}\"");
		}

		private static (string, bool, string) SearchRepeatLetterSkipsSubstring() {
			// 'a' matches Apple(0,tier1), Apricot(1,tier1), Banana(2,tier4 substring).
			// Cycling should stay within Apple/Apricot, never reach Banana.
			var search = new TypeAheadSearch();
			search.AddChar('a');
			search.Search(SearchItems.Length, NameByIndex);
			int first = search.SelectedOriginalIndex; // Apple(0)

			search.AddChar('a'); // cycle -> Apricot
			search.Search(SearchItems.Length, NameByIndex);
			int second = search.SelectedOriginalIndex; // Apricot(1)

			search.AddChar('a'); // cycle wraps -> Apple
			search.Search(SearchItems.Length, NameByIndex);
			int third = search.SelectedOriginalIndex; // Apple(0), not Banana

			bool ok = first == 0 && second == 1 && third == 0;
			return Assert("SearchRepeatLetterSkipsSubstring", ok,
				$"first={first}, second={second}, third={third}");
		}

		private static (string, bool, string) SearchBackspace() {
			var search = new TypeAheadSearch();
			search.AddChar('a');
			search.AddChar('p');
			search.Search(SearchItems.Length, NameByIndex);
			int afterAp = search.ResultCount;

			search.RemoveChar(); // buffer="a"
			search.Search(SearchItems.Length, NameByIndex);
			int afterBackspace = search.ResultCount;

			// backspace from 'ap' to 'a' restores Banana as substring match
			bool ok = afterAp == 2 && afterBackspace == 3
				   && search.Buffer == "a";
			return Assert("SearchBackspace", ok,
				$"ap={afterAp}, after backspace={afterBackspace}, buffer=\"{search.Buffer}\"");
		}

		private static (string, bool, string) SearchNavigateWraps() {
			var search = new TypeAheadSearch();
			search.AddChar('a');
			search.Search(SearchItems.Length, NameByIndex);
			// 3 results: Apple(0), Apricot(1), Banana(2). Cursor at 0.

			search.NavigateResults(1); // cursor -> 1
			search.NavigateResults(1); // cursor -> 2
			search.NavigateResults(1); // cursor -> 0 (wrap)

			bool ok = search.SelectedOriginalIndex == 0; // back to Apple
			return Assert("SearchNavigateWraps", ok,
				$"SelectedOriginalIndex={search.SelectedOriginalIndex}");
		}

		private static (string, bool, string) SearchJumpFirstLast() {
			var search = new TypeAheadSearch();
			search.AddChar('a');
			search.Search(SearchItems.Length, NameByIndex);
			// 3 results: Apple(0), Apricot(1), Banana(2)

			search.JumpToLastResult();
			int lastIdx = search.SelectedOriginalIndex; // Banana(2)

			search.JumpToFirstResult();
			int firstIdx = search.SelectedOriginalIndex; // Apple(0)

			bool ok = lastIdx == 2 && firstIdx == 0;
			return Assert("SearchJumpFirstLast", ok,
				$"last={lastIdx}, first={firstIdx}");
		}

		private static (string, bool, string) SearchNoMatch() {
			var search = new TypeAheadSearch();
			search.AddChar('z');
			search.Search(SearchItems.Length, NameByIndex);

			bool ok = search.ResultCount == 0 && search.IsSearchActive;
			return Assert("SearchNoMatch", ok,
				$"ResultCount={search.ResultCount}, IsSearchActive={search.IsSearchActive}");
		}

		private static (string, bool, string) MatchTierStartWholeWord() {
			// "wood" at start of "wood club" = tier 0, position 0
			int tier = TypeAheadSearch.MatchTier("wood club", "wood", out int pos);
			bool ok = tier == 0 && pos == 0;
			return Assert("MatchTierStartWholeWord", ok, $"tier={tier} pos={pos}");
		}

		private static (string, bool, string) MatchTierStartPrefix() {
			// "wood" at start of "wooden club" = tier 1, position 0
			int tier = TypeAheadSearch.MatchTier("wooden club", "wood", out int pos);
			bool ok = tier == 1 && pos == 0;
			return Assert("MatchTierStartPrefix", ok, $"tier={tier} pos={pos}");
		}

		private static (string, bool, string) MatchTierMidWholeWord() {
			// "wood" as whole word mid-string in "pine wood" = tier 2, position 5
			int tier = TypeAheadSearch.MatchTier("pine wood", "wood", out int pos);
			bool ok = tier == 2 && pos == 5;
			return Assert("MatchTierMidWholeWord", ok, $"tier={tier} pos={pos}");
		}

		private static (string, bool, string) MatchTierMidPrefix() {
			// "wood" as prefix of mid-string word in "a wooden thing" = tier 3, position 2
			int tier = TypeAheadSearch.MatchTier("a wooden thing", "wood", out int pos);
			bool ok = tier == 3 && pos == 2;
			return Assert("MatchTierMidPrefix", ok, $"tier={tier} pos={pos}");
		}

		private static (string, bool, string) MatchTierSubstring() {
			// "wood" inside "plywood" = tier 4, position 3
			int tier = TypeAheadSearch.MatchTier("plywood", "wood", out int pos);
			bool ok = tier == 4 && pos == 3;
			return Assert("MatchTierSubstring", ok, $"tier={tier} pos={pos}");
		}

		private static (string, bool, string) MatchTierNoMatch() {
			int tier = TypeAheadSearch.MatchTier("banana", "wood", out int pos);
			bool ok = tier == -1 && pos == -1;
			return Assert("MatchTierNoMatch", ok, $"tier={tier} pos={pos}");
		}

		private static (string, bool, string) MatchTierAccentInsensitive() {
			// ASCII "e" matches accented "é" in "défaut"
			int tier = TypeAheadSearch.MatchTier("défaut", "def");
			bool ok = tier == 1; // start-of-string prefix
			return Assert("MatchTierAccentInsensitive", ok, $"tier={tier}");
		}

		private static (string, bool, string) MatchTierAccentedQuery() {
			// Accented query also matches: "é" matches "é"
			int tier = TypeAheadSearch.MatchTier("défaut", "déf");
			bool ok = tier == 1;
			return Assert("MatchTierAccentedQuery", ok, $"tier={tier}");
		}

		private static (string, bool, string) MatchTierLigatureOe() {
			// "oeuf" matches "œuf" — ligature expanded to "oe"
			int tier = TypeAheadSearch.MatchTier("œuf", "oeuf");
			bool ok = tier == 0; // start-of-string whole word
			return Assert("MatchTierLigatureOe", ok, $"tier={tier}");
		}

		private static (string, bool, string) SearchTierOrdering() {
			// Items designed so each tier is represented:
			// "Wood Club" (tier 0), "Wooden Axe" (tier 1), "Pine Wood" (tier 2),
			// "A Wooden Thing" (tier 3), "Plywood" (tier 4)
			var items = new[] { "Plywood", "A Wooden Thing", "Pine Wood", "Wooden Axe", "Wood Club" };
			string nameByIndex(int i) => i >= 0 && i < items.Length ? items[i] : null;

			var search = new TypeAheadSearch();
			search.AddChar('w');
			search.AddChar('o');
			search.AddChar('o');
			search.AddChar('d');
			search.Search(items.Length, nameByIndex);

			// Expected order: Wood Club(4,t0), Wooden Axe(3,t1), Pine Wood(2,t2),
			// A Wooden Thing(1,t3), Plywood(0,t4)
			bool ok = search.ResultCount == 5;
			if (ok) {
				int[] expected = { 4, 3, 2, 1, 0 };
				for (int i = 0; i < 5; i++) {
					if (search.SelectedOriginalIndex != expected[i]) {
						ok = false;
						break;
					}
					if (i < 4) search.NavigateResults(1);
				}
			}
			return Assert("SearchTierOrdering", ok,
				$"ResultCount={search.ResultCount}");
		}

		private static (string, bool, string) SearchTierPositionSorting() {
			// "room" matches "washroom" at position 4 and "fried mushroom" at position 10
			// Both are tier 4 (substring). Washroom should rank first (earlier match position).
			var items = new[] { "Fried Mushroom", "Washroom" };
			string nameByIndex(int i) => i >= 0 && i < items.Length ? items[i] : null;

			var search = new TypeAheadSearch();
			search.AddChar('r');
			search.AddChar('o');
			search.AddChar('o');
			search.AddChar('m');
			search.Search(items.Length, nameByIndex);

			bool ok = search.ResultCount == 2 && search.SelectedOriginalIndex == 1;
			if (ok) {
				search.NavigateResults(1);
				ok = search.SelectedOriginalIndex == 0;
			}
			return Assert("SearchTierPositionSorting", ok,
				$"ResultCount={search.ResultCount}, First={search.SelectedOriginalIndex}");
		}

		private static (string, bool, string) SearchSpaceMultiWord() {
			// "blue c" should match "Blue Cheese" but not "Banana"
			var search = new TypeAheadSearch();
			search.AddChar('b');
			search.AddChar('l');
			search.AddChar('u');
			search.AddChar('e');
			search.AddChar(' ');
			search.AddChar('c');
			search.Search(SearchItems.Length, NameByIndex);

			bool ok = search.ResultCount == 1 && search.SelectedOriginalIndex == 3;
			return Assert("SearchSpaceMultiWord", ok,
				$"ResultCount={search.ResultCount}, SelectedOriginalIndex={search.SelectedOriginalIndex}");
		}

		private static (string, bool, string) SearchTrailingSpaceIgnored() {
			// "blue " (trailing space) should still match "Blue Cheese"
			var search = new TypeAheadSearch();
			search.AddChar('b');
			search.AddChar('l');
			search.AddChar('u');
			search.AddChar('e');
			search.AddChar(' ');
			search.Search(SearchItems.Length, NameByIndex);

			bool ok = search.ResultCount == 1 && search.SelectedOriginalIndex == 3;
			return Assert("SearchTrailingSpaceIgnored", ok,
				$"ResultCount={search.ResultCount}, SelectedOriginalIndex={search.SelectedOriginalIndex}");
		}

		private static (string, bool, string) SearchNameLengthTiebreaker() {
			// In tiers 0-1 (start-of-string), shorter names should rank first
			// when match positions are equal — "wood" is closer to an exact match than "wooden"
			var items = new[] { "Wood Club", "Wooden", "Wood" };
			string nameByIndex(int i) => i >= 0 && i < items.Length ? items[i] : null;

			var search = new TypeAheadSearch();
			search.AddChar('w');
			search.Search(items.Length, nameByIndex);

			// Expected order: Wood(2,t1,len=4), Wooden(1,t1,len=6), Wood Club(0,t1,len=9)
			bool ok = search.ResultCount == 3 && search.SelectedOriginalIndex == 2;
			if (ok) {
				search.NavigateResults(1);
				ok = search.SelectedOriginalIndex == 1;
			}
			if (ok) {
				search.NavigateResults(1);
				ok = search.SelectedOriginalIndex == 0;
			}
			return Assert("SearchNameLengthTiebreaker", ok,
				$"ResultCount={search.ResultCount}, First={search.SelectedOriginalIndex}");
		}

		private static (string, bool, string) SearchMultiTokenAbbreviation() {
			// "ga pi" should match "Gas Pipe" as a space-delimited word-prefix
			// abbreviation. "Liquid Pipe" and "Gas Reservoir" each satisfy only one
			// token and should be excluded.
			var items = new[] { "Gas Pipe", "Liquid Pipe", "Gas Reservoir" };
			string nameByIndex(int i) => i >= 0 && i < items.Length ? items[i] : null;

			var search = new TypeAheadSearch();
			search.AddChar('g');
			search.AddChar('a');
			search.AddChar(' ');
			search.AddChar('p');
			search.AddChar('i');
			search.Search(items.Length, nameByIndex);

			bool ok = search.ResultCount == 1 && search.SelectedOriginalIndex == 0;
			return Assert("SearchMultiTokenAbbreviation", ok,
				$"ResultCount={search.ResultCount}, SelectedOriginalIndex={search.SelectedOriginalIndex}");
		}

		private static (string, bool, string) MatchTierMultiTokenAbbreviation() {
			int tier = TypeAheadSearch.MatchTier("gas pipe", "ga pi", out int pos);
			bool ok = tier == 5 && pos == 0;
			return Assert("MatchTierMultiTokenAbbreviation", ok, $"tier={tier} pos={pos}");
		}

		private static (string, bool, string) MatchTierMultiTokenOrderRequired() {
			// Tokens must be consumed in order — "pi ga" should NOT match "gas pipe"
			int tier = TypeAheadSearch.MatchTier("gas pipe", "pi ga", out int pos);
			bool ok = tier == -1;
			return Assert("MatchTierMultiTokenOrderRequired", ok, $"tier={tier} pos={pos}");
		}

		private static (string, bool, string) SearchSortsByNameLengthNotFullLabel() {
			// Build menu labels concatenate "Name, size, cost, description..." — sorting by the
			// full label length would put an item with a long name but short description before
			// one with a short name and long description. Sort must key on the name (up to the
			// first comma) so "Gas Pipe" ranks above "Gas Pipe Element Sensor" for "ga pi".
			var items = new[] {
				"Gas Pipe Element Sensor, 1x1, short desc",                     // long name,  short label
				"Gas Pipe, 1x1, lots of extra padding description text here",   // short name, long label
			};
			string nameByIndex(int i) => i >= 0 && i < items.Length ? items[i] : null;

			var search = new TypeAheadSearch();
			search.AddChar('g');
			search.AddChar('a');
			search.AddChar(' ');
			search.AddChar('p');
			search.AddChar('i');
			search.Search(items.Length, nameByIndex);

			bool ok = search.ResultCount == 2 && search.SelectedOriginalIndex == 1;
			return Assert("SearchSortsByNameLengthNotFullLabel", ok,
				$"ResultCount={search.ResultCount}, SelectedOriginalIndex={search.SelectedOriginalIndex}");
		}

		private static (string, bool, string) MatchTierMultiTokenCrossSegment() {
			// Tier 5 must match within a single comma-delimited segment so the returned
			// position reliably locates the match. Here "ga pi" matches only in the effect
			// description segment, so tier is 5 and pos is past the first comma.
			string label = "gas vent, 1x1, vents gas from pipes into a room";
			int firstComma = label.IndexOf(',');
			int tier = TypeAheadSearch.MatchTier(label, "ga pi", out int pos);
			bool ok = tier == 5 && pos > firstComma;
			return Assert("MatchTierMultiTokenCrossSegment", ok, $"tier={tier} pos={pos} firstComma={firstComma}");
		}

		private static (string, bool, string) SearchNameMatchBeatsDescriptionMatch() {
			// "pipe" matches "Liquid Pipe" as a whole word in the name segment, and matches
			// "Gas Bridge" as a whole word in the description segment. The name-segment match
			// must rank first even though "gas bridge" is a shorter name than "liquid pipe".
			var items = new[] {
				"Gas Bridge, 1x1, runs one gas pipe section over another",
				"Liquid Pipe, 1x1, transports liquid",
			};
			string nameByIndex(int i) => i >= 0 && i < items.Length ? items[i] : null;

			var search = new TypeAheadSearch();
			search.AddChar('p');
			search.AddChar('i');
			search.AddChar('p');
			search.AddChar('e');
			search.Search(items.Length, nameByIndex);

			bool ok = search.ResultCount == 2 && search.SelectedOriginalIndex == 1;
			return Assert("SearchNameMatchBeatsDescriptionMatch", ok,
				$"ResultCount={search.ResultCount}, SelectedOriginalIndex={search.SelectedOriginalIndex}");
		}

		private static (string, bool, string) SearchMultiTokenNameBeatsDescription() {
			// "ga pi" abbreviation-matches "Gas Pipe" in the name and also matches "Gas Vent"
			// inside its effect description ("vents gas from pipes"). Name match must rank first.
			var items = new[] {
				"Gas Vent, 1x1, vents gas from pipes into a room",
				"Gas Pipe, 1x1, transports gas",
			};
			string nameByIndex(int i) => i >= 0 && i < items.Length ? items[i] : null;

			var search = new TypeAheadSearch();
			search.AddChar('g');
			search.AddChar('a');
			search.AddChar(' ');
			search.AddChar('p');
			search.AddChar('i');
			search.Search(items.Length, nameByIndex);

			bool ok = search.ResultCount == 2 && search.SelectedOriginalIndex == 1;
			return Assert("SearchMultiTokenNameBeatsDescription", ok,
				$"ResultCount={search.ResultCount}, SelectedOriginalIndex={search.SelectedOriginalIndex}");
		}

		private static (string, bool, string) SearchLengthBeatsPosition() {
			// Within a tier, shorter names rank first even when a longer name has an
			// earlier match position. "wood" hits Oakwood Shelf at pos 3 and Pinewood
			// at pos 4 — both tier 4 — but Pinewood (len 8) outranks Oakwood Shelf (len 13).
			var items = new[] { "Oakwood Shelf", "Pinewood" };
			string nameByIndex(int i) => i >= 0 && i < items.Length ? items[i] : null;

			var search = new TypeAheadSearch();
			search.AddChar('w');
			search.AddChar('o');
			search.AddChar('o');
			search.AddChar('d');
			search.Search(items.Length, nameByIndex);

			bool ok = search.ResultCount == 2 && search.SelectedOriginalIndex == 1;
			if (ok) {
				search.NavigateResults(1);
				ok = search.SelectedOriginalIndex == 0;
			}
			return Assert("SearchLengthBeatsPosition", ok,
				$"ResultCount={search.ResultCount}, First={search.SelectedOriginalIndex}");
		}

		// ========================================
		// ScannerSearch tests
		// ========================================

		private static (string, bool, string) ScannerSearchPrefixMatch() {
			int key = ScannerSearch.MatchSortKey("Sandstone", "sand");
			bool ok = key == 0;
			return Assert("ScannerSearchPrefixMatch", ok, $"key={key}");
		}

		private static (string, bool, string) ScannerSearchWholeWordMatch() {
			int key = ScannerSearch.MatchSortKey("Polluted Water", "water");
			bool ok = key == 1;
			return Assert("ScannerSearchWholeWordMatch", ok, $"key={key}");
		}

		private static (string, bool, string) ScannerSearchWordStartMatch() {
			int key = ScannerSearch.MatchSortKey("Coal Generator", "gen");
			bool ok = key == 2;
			return Assert("ScannerSearchWordStartMatch", ok, $"key={key}");
		}

		private static (string, bool, string) ScannerSearchNoMatch() {
			int key = ScannerSearch.MatchSortKey("Sandstone", "iron");
			bool ok = key == -1;
			return Assert("ScannerSearchNoMatch", ok, $"key={key}");
		}

		private static (string, bool, string) ScannerSearchCaseInsensitive() {
			int key = ScannerSearch.MatchSortKey("Sandstone", "SAND");
			bool ok = key == 0;
			return Assert("ScannerSearchCaseInsensitive", ok, $"key={key}");
		}

		private static (string, bool, string) ScannerSearchFilterRemapsCategory() {
			var entries = new List<ScanEntry> {
				new ScanEntry { Cell = 0, ItemName = "Sandstone", Category = "Solids", Subcategory = "Stone" },
				new ScanEntry { Cell = 1, ItemName = "Iron Ore", Category = "Solids", Subcategory = "Ores" },
			};
			var results = ScannerSearch.Filter(entries, "sand");
			bool ok = results.Count == 1
				&& results[0].Category == (string)STRINGS.ONIACCESS.SCANNER.CATEGORIES.SEARCH
				&& results[0].Subcategory == "Solids"
				&& results[0].ItemName == "Sandstone"
				&& results[0].SortKey == 0;
			return Assert("ScannerSearchFilterRemapsCategory", ok,
				$"count={results.Count}");
		}

		private static (string, bool, string) ScannerSearchBestMatchAcrossPositions() {
			// "gen" in "X Generators Gen" should find whole-word "Gen" (key=1),
			// not stop at word-start "Generators" (key=2)
			int key = ScannerSearch.MatchSortKey("X Generators Gen", "gen");
			bool ok = key == 1;
			return Assert("ScannerSearchBestMatchAcrossPositions", ok, $"key={key}");
		}

		private static (string, bool, string) ScannerSearchAccentInsensitive() {
			// ASCII "categ" matches "Catégorie" with accented é
			int key = ScannerSearch.MatchSortKey("Catégorie", "categ");
			bool ok = key == 0; // prefix match
			return Assert("ScannerSearchAccentInsensitive", ok, $"key={key}");
		}

		private static (string, bool, string) ScannerSearchKoreanQuery() {
			// Covers Hangul normalization in the matcher (RemoveDiacritics must not
			// mangle decomposed jamo), not the IME input path, which needs the game.
			int key = ScannerSearch.MatchSortKey("오염된 물", "물");
			bool ok = key == 1;
			return Assert("ScannerSearchKoreanQuery", ok, $"key={key}");
		}

		private static (string, bool, string) ScannerSearchMatcherStripsLinkFormatting() {
			// NameMatcher.Match must strip link markup before matching; the link
			// target ("ELEMENT") must not leak into the matched text.
			var matcher = new ScannerSearch.NameMatcher("coal");
			int key = matcher.Match("<link=\"ELEMENT\">Coal</link>");
			bool ok = key == 0; // prefix match against the stripped "Coal"
			return Assert("ScannerSearchMatcherStripsLinkFormatting", ok, $"key={key}");
		}

		// ========================================
		// TextFilter tests (ported + new edge cases)
		// ========================================

		private static (string, bool, string) TextFilterStripsBold() {
			string result = TextFilter.FilterForSpeech("<b>Warning</b>");
			bool ok = result == "Warning";
			return Assert("TextFilterStripsBold", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterStripsColor() {
			string result = TextFilter.FilterForSpeech("<color=#FF0000>Hot</color>");
			bool ok = result == "Hot";
			return Assert("TextFilterStripsColor", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterStripsNested() {
			string result = TextFilter.FilterForSpeech("<b><color=red>Alert</color></b>");
			bool ok = result == "Alert";
			return Assert("TextFilterStripsNested", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterConvertsSpriteToText() {
			string result = TextFilter.FilterForSpeech("<sprite name=warning>Pipe broken");
			bool ok = result == "warning: Pipe broken";
			return Assert("TextFilterConvertsSpriteToText", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterStripsUnregisteredSprite() {
			string result = TextFilter.FilterForSpeech("<sprite name=decorative_icon>text");
			bool ok = result == "text";
			return Assert("TextFilterStripsUnregisteredSprite", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterStripsTmpBracketSprites() {
			string result = TextFilter.FilterForSpeech("[icon_name] some text");
			bool ok = result == "some text";
			return Assert("TextFilterStripsTmpBracketSprites", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterHandlesLinkTags() {
			string result = TextFilter.FilterForSpeech("<link=\"LINK_ID\">Click here</link>");
			bool ok = result == "Click here";
			return Assert("TextFilterHandlesLinkTags", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterNormalizesWhitespace() {
			string result = TextFilter.FilterForSpeech("word1   word2\n\nword3");
			bool ok = result == "word1 word2 word3";
			return Assert("TextFilterNormalizesWhitespace", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterHandlesEmptyAndNull() {
			string nullResult = TextFilter.FilterForSpeech(null);
			string emptyResult = TextFilter.FilterForSpeech("");

			bool ok = nullResult == "" && emptyResult == "";
			return Assert("TextFilterHandlesEmptyAndNull", ok,
				$"null->\"{nullResult}\", empty->\"{emptyResult}\"");
		}

		private static (string, bool, string) TextFilterPreservesPlainText() {
			string input = "Copper Ore, 200 kg, 25\u00B0C";
			string result = TextFilter.FilterForSpeech(input);
			bool ok = result == "Copper Ore, 200 kg, 25\u00B0";
			return Assert("TextFilterPreservesPlainText", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterStripsHotkey() {
			string result = TextFilter.FilterForSpeech("Build a Ladder {Hotkey}");
			bool ok = result == "Build a Ladder";
			return Assert("TextFilterStripsHotkey", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterStripsSizeStyle() {
			string result = TextFilter.FilterForSpeech("<size=10><style=\"KKeyword\">text</style></size>");
			bool ok = result == "text";
			return Assert("TextFilterStripsSizeStyle", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterUnclosedTag() {
			string result = TextFilter.FilterForSpeech("<b>Bold text");
			bool ok = result == "Bold text";
			return Assert("TextFilterUnclosedTag", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterMismatchedTags() {
			string result = TextFilter.FilterForSpeech("<b>text</color>");
			bool ok = result == "text";
			return Assert("TextFilterMismatchedTags", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterSpriteNameCaseInsensitive() {
			string result = TextFilter.FilterForSpeech("<sprite name=WARNING>text");
			bool ok = result == "warning: text";
			return Assert("TextFilterSpriteNameCaseInsensitive", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterReplacesMasculineOrdinalDegree() {
			string result = TextFilter.FilterForSpeech("21.9 \u00BAC");
			bool ok = result == "21.9 \u00B0";
			return Assert("TextFilterReplacesMasculineOrdinalDegree", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterStripsTempUnitCelsius() {
			string result = TextFilter.FilterForSpeech("21.9 \u00B0C");
			bool ok = result == "21.9 \u00B0";
			return Assert("TextFilterStripsTempUnitCelsius", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterStripsTempUnitFahrenheit() {
			string result = TextFilter.FilterForSpeech("71.4 \u00B0F");
			bool ok = result == "71.4 \u00B0";
			return Assert("TextFilterStripsTempUnitFahrenheit", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterPreservesKelvin() {
			string result = TextFilter.FilterForSpeech("294.3 K");
			bool ok = result == "294.3 K";
			return Assert("TextFilterPreservesKelvin", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterStripsBullet() {
			string result = TextFilter.FilterForSpeech("\u2022 One or more Duplicants are missing a bed");
			bool ok = result == "One or more Duplicants are missing a bed";
			return Assert("TextFilterStripsBullet", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterPreservesNumericBrackets() {
			string result = TextFilter.FilterForSpeech("Growing [45%]");
			bool ok = result == "Growing 45%";
			return Assert("TextFilterPreservesNumericBrackets", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterStripsControlChars() {
			string result = TextFilter.FilterForSpeech("Hello\x00World");
			bool ok = result == "HelloWorld";
			return Assert("TextFilterStripsControlChars", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterCombinedMarkup() {
			string result = TextFilter.FilterForSpeech(
				"<sprite name=warning><b><color=red>Overheat [45%]</color></b> {Hotkey}Ctrl+X");
			bool ok = result == "warning: Overheat 45%";
			return Assert("TextFilterCombinedMarkup", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterStripsOriginalMutation() {
			string result = TextFilter.FilterForSpeech("Mealwood (Original)");
			bool ok = result == "Mealwood";
			return Assert("TextFilterStripsOriginalMutation", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) TextFilterPreservesMutatedPlant() {
			string result = TextFilter.FilterForSpeech("Mealwood (Squashed, Exuberant)");
			bool ok = result == "Mealwood (Squashed, Exuberant)";
			return Assert("TextFilterPreservesMutatedPlant", ok, $"got \"{result}\"");
		}

		// ========================================
		// LogCapture helper
		// ========================================

		private class LogCapture: IDisposable {
			public List<string> LogMessages = new List<string>();
			public List<string> WarnMessages = new List<string>();
			public List<string> ErrorMessages = new List<string>();

			private readonly Action<string> _origLog;
			private readonly Action<string> _origWarn;
			private readonly Action<string> _origError;

			public LogCapture() {
				_origLog = Log.LogFn;
				_origWarn = Log.WarnFn;
				_origError = Log.ErrorFn;
				Log.LogFn = msg => LogMessages.Add(msg);
				Log.WarnFn = msg => WarnMessages.Add(msg);
				Log.ErrorFn = msg => ErrorMessages.Add(msg);
			}

			public void Dispose() {
				Log.LogFn = _origLog;
				Log.WarnFn = _origWarn;
				Log.ErrorFn = _origError;
			}
		}

		// ========================================
		// Log class tests
		// ========================================

		private static (string, bool, string) LogWarnRoutesToWarnFn() {
			using (var capture = new LogCapture()) {
				Log.Warn("oops");
				bool ok = capture.WarnMessages.Count == 1
					   && capture.LogMessages.Count == 0;
				return Assert("LogWarnRoutesToWarnFn", ok,
					$"warn={capture.WarnMessages.Count}, log={capture.LogMessages.Count}");
			}
		}

		private static (string, bool, string) LogErrorRoutesToErrorFn() {
			using (var capture = new LogCapture()) {
				Log.Error("bad");
				bool ok = capture.ErrorMessages.Count == 1
					   && capture.LogMessages.Count == 0;
				return Assert("LogErrorRoutesToErrorFn", ok,
					$"error={capture.ErrorMessages.Count}, log={capture.LogMessages.Count}");
			}
		}

		private static (string, bool, string) LogBackendSwapWorks() {
			var capture1 = new LogCapture();
			Log.Info("first");
			bool gotFirst = capture1.LogMessages.Count == 1;
			capture1.Dispose();

			var capture2 = new LogCapture();
			Log.Info("second");
			bool gotSecond = capture2.LogMessages.Count == 1
						  && capture1.LogMessages.Count == 1; // original didn't get more
			capture2.Dispose();

			// Verify restore works: log after dispose should go to original backend
			bool ok = gotFirst && gotSecond;
			return Assert("LogBackendSwapWorks", ok,
				$"gotFirst={gotFirst}, gotSecond={gotSecond}");
		}

		// ========================================
		// HandlerStack diagnostic quality tests
		// ========================================

		private static (string, bool, string) PushFailureLogsHandlerNameAndException() {
			Reset();
			using (var capture = new LogCapture()) {
				var thrower = new ThrowingHandler { ThrowOnActivate = true };
				HandlerStack.Push(thrower);
				bool ok = capture.ErrorMessages.Count > 0
					   && capture.ErrorMessages[0].Contains("Thrower")
					   && capture.ErrorMessages[0].Contains("OnActivate exploded");
				return Assert("PushFailureLogsHandlerNameAndException", ok,
					$"errors={capture.ErrorMessages.Count}" +
					(capture.ErrorMessages.Count > 0 ? $", msg=\"{capture.ErrorMessages[0]}\"" : ""));
			}
		}

		private static (string, bool, string) PushNullLogsWarning() {
			Reset();
			using (var capture = new LogCapture()) {
				HandlerStack.Push(null);
				bool ok = capture.WarnMessages.Count > 0
					   && capture.WarnMessages[0].Contains("null");
				return Assert("PushNullLogsWarning", ok,
					$"warns={capture.WarnMessages.Count}" +
					(capture.WarnMessages.Count > 0 ? $", msg=\"{capture.WarnMessages[0]}\"" : ""));
			}
		}

		private static (string, bool, string) PopOnEmptyLogsWarning() {
			Reset();
			using (var capture = new LogCapture()) {
				HandlerStack.Pop();
				bool ok = capture.WarnMessages.Count > 0;
				return Assert("PopOnEmptyLogsWarning", ok,
					$"warns={capture.WarnMessages.Count}");
			}
		}

		private static (string, bool, string) ReplaceFailureLogsHandlerNameAndException() {
			Reset();
			using (var capture = new LogCapture()) {
				var thrower = new ThrowingHandler { ThrowOnActivate = true };
				HandlerStack.Replace(thrower);
				bool ok = capture.ErrorMessages.Count > 0
					   && capture.ErrorMessages[0].Contains("Thrower")
					   && capture.ErrorMessages[0].Contains("OnActivate exploded");
				return Assert("ReplaceFailureLogsHandlerNameAndException", ok,
					$"errors={capture.ErrorMessages.Count}" +
					(capture.ErrorMessages.Count > 0 ? $", msg=\"{capture.ErrorMessages[0]}\"" : ""));
			}
		}

		// ========================================
		// TooltipCapture tests
		// ========================================

		private static (string, bool, string) TooltipCaptureEmptyFrameReturnsNull() {
			TooltipCapture.Reset();
			TooltipCapture.BeginFrame();
			TooltipCapture.EndFrame();
			string result = TooltipCapture.GetTooltipText();
			return Assert("TooltipCaptureEmptyFrameReturnsNull", result == null,
				$"got \"{result}\"");
		}

		private static (string, bool, string) TooltipCaptureTextOutsideBlockDiscarded() {
			TooltipCapture.Reset();
			TooltipCapture.BeginFrame();
			TooltipCapture.AppendText("orphan");
			TooltipCapture.EndFrame();
			string result = TooltipCapture.GetTooltipText();
			return Assert("TooltipCaptureTextOutsideBlockDiscarded", result == null,
				$"got \"{result}\"");
		}

		private static (string, bool, string) TooltipCaptureSingleBlock() {
			TooltipCapture.Reset();
			TooltipCapture.BeginFrame();
			TooltipCapture.BeginBlock();
			TooltipCapture.AppendText("Copper Ore");
			TooltipCapture.EndFrame();
			string result = TooltipCapture.GetTooltipText();
			return Assert("TooltipCaptureSingleBlock", result == "Copper Ore",
				$"got \"{result}\"");
		}

		private static (string, bool, string) TooltipCaptureTwoBlocks() {
			TooltipCapture.Reset();
			TooltipCapture.BeginFrame();
			TooltipCapture.BeginBlock();
			TooltipCapture.AppendText("Copper Ore");
			TooltipCapture.BeginBlock();
			TooltipCapture.AppendText("Hot");
			TooltipCapture.EndFrame();
			string result = TooltipCapture.GetTooltipText();
			return Assert("TooltipCaptureTwoBlocks", result == "Copper Ore, Hot",
				$"got \"{result}\"");
		}

		private static (string, bool, string) TooltipCaptureMultipleTokensInBlock() {
			TooltipCapture.Reset();
			TooltipCapture.BeginFrame();
			TooltipCapture.BeginBlock();
			TooltipCapture.AppendText("1657");
			TooltipCapture.AppendText(".3");
			TooltipCapture.AppendText(" g");
			TooltipCapture.EndFrame();
			string result = TooltipCapture.GetTooltipText();
			return Assert("TooltipCaptureMultipleTokensInBlock", result == "1657.3 g",
				$"got \"{result}\"");
		}

		private static (string, bool, string) TooltipCaptureNewLineSeparatesLines() {
			TooltipCapture.Reset();
			TooltipCapture.BeginFrame();
			TooltipCapture.BeginBlock();
			TooltipCapture.AppendText("OXYGEN");
			TooltipCapture.AppendNewLine();
			TooltipCapture.AppendText("Breathable Gas");
			TooltipCapture.AppendNewLine();
			TooltipCapture.AppendText("1657");
			TooltipCapture.AppendText(".3 g");
			TooltipCapture.EndFrame();
			string result = TooltipCapture.GetTooltipText();
			return Assert("TooltipCaptureNewLineSeparatesLines",
				result == "OXYGEN, Breathable Gas, 1657.3 g",
				$"got \"{result}\"");
		}

		private static (string, bool, string) TooltipCaptureEmptyTextSkipped() {
			TooltipCapture.Reset();
			TooltipCapture.BeginFrame();
			TooltipCapture.BeginBlock();
			TooltipCapture.AppendText(null);
			TooltipCapture.AppendText("");
			TooltipCapture.AppendText("   ");
			TooltipCapture.AppendText("real");
			TooltipCapture.EndFrame();
			string result = TooltipCapture.GetTooltipText();
			return Assert("TooltipCaptureEmptyTextSkipped", result == "real",
				$"got \"{result}\"");
		}

		private static (string, bool, string) TooltipCaptureResetClearsState() {
			TooltipCapture.BeginFrame();
			TooltipCapture.BeginBlock();
			TooltipCapture.AppendText("should vanish");
			TooltipCapture.Reset();
			string result = TooltipCapture.GetTooltipText();
			return Assert("TooltipCaptureResetClearsState", result == null,
				$"got \"{result}\"");
		}

		private static (string, bool, string) TooltipCaptureGetLinesGroupsByBlock() {
			TooltipCapture.Reset();
			TooltipCapture.BeginFrame();
			TooltipCapture.BeginBlock();
			TooltipCapture.AppendText("OXYGEN");
			TooltipCapture.AppendNewLine();
			TooltipCapture.AppendText("Breathable Gas");
			TooltipCapture.BeginBlock();
			TooltipCapture.AppendText("21.8 C");
			TooltipCapture.EndFrame();
			var lines = TooltipCapture.GetTooltipLines();
			bool ok = lines != null && lines.Count == 2
				&& lines[0] == "OXYGEN, Breathable Gas"
				&& lines[1] == "21.8 C";
			return Assert("TooltipCaptureGetLinesGroupsByBlock", ok,
				lines == null ? "null" : $"count={lines.Count}: [{string.Join("|", lines)}]");
		}

		// ========================================
		// UnionFind tests
		// ========================================

		private static (string, bool, string) UnionFindSameSetAfterUnion() {
			var uf = new UnionFind(5);
			uf.Union(1, 3);
			bool ok = uf.Find(1) == uf.Find(3);
			return Assert("UnionFindSameSetAfterUnion", ok,
				$"Find(1)={uf.Find(1)}, Find(3)={uf.Find(3)}");
		}

		private static (string, bool, string) UnionFindDisjointSetsDistinct() {
			var uf = new UnionFind(5);
			uf.Union(0, 1);
			uf.Union(2, 3);
			bool ok = uf.Find(0) != uf.Find(2);
			return Assert("UnionFindDisjointSetsDistinct", ok,
				$"Find(0)={uf.Find(0)}, Find(2)={uf.Find(2)}");
		}

		private static (string, bool, string) UnionFindTransitiveUnion() {
			var uf = new UnionFind(5);
			uf.Union(0, 1);
			uf.Union(1, 2);
			bool ok = uf.Find(0) == uf.Find(2);
			return Assert("UnionFindTransitiveUnion", ok,
				$"Find(0)={uf.Find(0)}, Find(2)={uf.Find(2)}");
		}

		private static (string, bool, string) UnionFindSelfUnionNoOp() {
			var uf = new UnionFind(3);
			int rootBefore = uf.Find(1);
			uf.Union(1, 1);
			int rootAfter = uf.Find(1);
			bool ok = rootBefore == rootAfter && rootAfter == 1;
			return Assert("UnionFindSelfUnionNoOp", ok,
				$"before={rootBefore}, after={rootAfter}");
		}

		private static (string, bool, string) UnionFindDuplicateUnionNoOp() {
			var uf = new UnionFind(4);
			uf.Union(0, 1);
			int rootAfterFirst = uf.Find(0);
			uf.Union(0, 1);
			int rootAfterSecond = uf.Find(0);
			bool ok = rootAfterFirst == rootAfterSecond
				&& uf.Find(0) == uf.Find(1);
			return Assert("UnionFindDuplicateUnionNoOp", ok,
				$"first={rootAfterFirst}, second={rootAfterSecond}");
		}

		private static (string, bool, string) UnionFindResetReinitializes() {
			var uf = new UnionFind(4);
			uf.Union(0, 1);
			uf.Union(2, 3);
			uf.Reset(4);
			// After reset, every element should be its own root
			bool ok = uf.Find(0) == 0 && uf.Find(1) == 1
				&& uf.Find(2) == 2 && uf.Find(3) == 3;
			return Assert("UnionFindResetReinitializes", ok,
				$"roots: 0={uf.Find(0)}, 1={uf.Find(1)}, 2={uf.Find(2)}, 3={uf.Find(3)}");
		}

		private static (string, bool, string) UnionFindResetReallocatesOnSizeChange() {
			var uf = new UnionFind(3);
			uf.Union(0, 1);
			uf.Reset(5);
			// Should work with the larger size
			uf.Union(3, 4);
			bool ok = uf.Find(3) == uf.Find(4)
				&& uf.Find(0) == 0 && uf.Find(1) == 1;
			return Assert("UnionFindResetReallocatesOnSizeChange", ok,
				$"Find(3)={uf.Find(3)}, Find(4)={uf.Find(4)}, Find(0)={uf.Find(0)}");
		}

		private static (string, bool, string) UnionFindLargeChainMerge() {
			int size = 1000;
			var uf = new UnionFind(size);
			// Union all elements into one set via chain
			for (int i = 0; i < size - 1; i++)
				uf.Union(i, i + 1);
			// All should share the same root
			int root = uf.Find(0);
			bool ok = true;
			for (int i = 1; i < size; i++) {
				if (uf.Find(i) != root) {
					ok = false;
					return Assert("UnionFindLargeChainMerge", false,
						$"element {i} has root {uf.Find(i)}, expected {root}");
				}
			}
			return Assert("UnionFindLargeChainMerge", ok, "OK");
		}

		// ========================================
		// SpeechPipeline tests
		// ========================================

		private static void ResetPipeline(ref float fakeTime, List<(string text, bool interrupt)> spoken) {
			fakeTime = 0f;
			spoken.Clear();
			SpeechPipeline.Reset();
		}

		private static (string, bool, string) PipelineDisabledSkipsSpeech() {
			float fakeTime = 0f;
			var spoken = new List<(string text, bool interrupt)>();
			SpeechPipeline.TimeSource = () => fakeTime;
			SpeechPipeline.SpeakAction = (text, intr) => spoken.Add((text, intr));
			SpeechPipeline.Reset();

			SpeechPipeline.SetEnabled(false);
			SpeechPipeline.SpeakInterrupt("hello");
			bool ok = spoken.Count == 0;

			SpeechPipeline.SetEnabled(true);
			return Assert("PipelineDisabledSkipsSpeech", ok, $"spoken={spoken.Count}");
		}

		private static (string, bool, string) PipelineEnabledSpeaks() {
			float fakeTime = 0f;
			var spoken = new List<(string text, bool interrupt)>();
			SpeechPipeline.TimeSource = () => fakeTime;
			SpeechPipeline.SpeakAction = (text, intr) => spoken.Add((text, intr));
			SpeechPipeline.Reset();

			SpeechPipeline.SpeakInterrupt("hello");
			bool ok = spoken.Count == 1 && spoken[0].text == "hello";
			return Assert("PipelineEnabledSpeaks", ok,
				$"spoken={spoken.Count}" + (spoken.Count > 0 ? $", text=\"{spoken[0].text}\"" : ""));
		}

		private static (string, bool, string) PipelineFiltersBeforeSpeaking() {
			float fakeTime = 0f;
			var spoken = new List<(string text, bool interrupt)>();
			SpeechPipeline.TimeSource = () => fakeTime;
			SpeechPipeline.SpeakAction = (text, intr) => spoken.Add((text, intr));
			SpeechPipeline.Reset();

			SpeechPipeline.SpeakInterrupt("<b>bold</b>");
			bool ok = spoken.Count == 1 && spoken[0].text == "bold";
			return Assert("PipelineFiltersBeforeSpeaking", ok,
				$"spoken={spoken.Count}" + (spoken.Count > 0 ? $", text=\"{spoken[0].text}\"" : ""));
		}

		private static (string, bool, string) PipelineDeduplicatesSameText() {
			float fakeTime = 0f;
			var spoken = new List<(string text, bool interrupt)>();
			SpeechPipeline.TimeSource = () => fakeTime;
			SpeechPipeline.SpeakAction = (text, intr) => spoken.Add((text, intr));
			SpeechPipeline.Reset();

			SpeechPipeline.SpeakInterrupt("hello");
			SpeechPipeline.SpeakInterrupt("hello");
			bool ok = spoken.Count == 1;
			return Assert("PipelineDeduplicatesSameText", ok, $"spoken={spoken.Count}");
		}

		private static (string, bool, string) PipelineDuplicateSuppressedWithinWindow() {
			float fakeTime = 0f;
			var spoken = new List<(string text, bool interrupt)>();
			SpeechPipeline.TimeSource = () => fakeTime;
			SpeechPipeline.SpeakAction = (text, intr) => spoken.Add((text, intr));
			SpeechPipeline.Reset();

			SpeechPipeline.SpeakInterrupt("hello");
			fakeTime = 0.04f; // inside the 0.05s deduplication window
			SpeechPipeline.SpeakInterrupt("hello");
			bool ok = spoken.Count == 1;
			return Assert("PipelineDuplicateSuppressedWithinWindow", ok, $"spoken={spoken.Count}");
		}

		private static (string, bool, string) PipelineAllowsSameTextAfterWindow() {
			float fakeTime = 0f;
			var spoken = new List<(string text, bool interrupt)>();
			SpeechPipeline.TimeSource = () => fakeTime;
			SpeechPipeline.SpeakAction = (text, intr) => spoken.Add((text, intr));
			SpeechPipeline.Reset();

			SpeechPipeline.SpeakInterrupt("hello");
			fakeTime = 0.3f;
			SpeechPipeline.SpeakInterrupt("hello");
			bool ok = spoken.Count == 2;
			return Assert("PipelineAllowsSameTextAfterWindow", ok, $"spoken={spoken.Count}");
		}

		private static (string, bool, string) PipelineAllowsDifferentTextImmediately() {
			float fakeTime = 0f;
			var spoken = new List<(string text, bool interrupt)>();
			SpeechPipeline.TimeSource = () => fakeTime;
			SpeechPipeline.SpeakAction = (text, intr) => spoken.Add((text, intr));
			SpeechPipeline.Reset();

			SpeechPipeline.SpeakInterrupt("hello");
			SpeechPipeline.SpeakInterrupt("world");
			bool ok = spoken.Count == 2;
			return Assert("PipelineAllowsDifferentTextImmediately", ok, $"spoken={spoken.Count}");
		}

		private static Color[] _paletteColors;
		private static Color[] PaletteColors => _paletteColors ?? (_paletteColors = new Color[] {
			new Color(0.4862745f, 0.4862745f, 0.4862745f),
			new Color(0f, 0f, 84f / 85f),
			new Color(0f, 0f, 0.7372549f),
			new Color(4f / 15f, 8f / 51f, 0.7372549f),
			new Color(0.5803922f, 0f, 44f / 85f),
			new Color(56f / 85f, 0f, 0.1254902f),
			new Color(56f / 85f, 0.0627451f, 0f),
			new Color(8f / 15f, 4f / 51f, 0f),
			new Color(16f / 51f, 16f / 85f, 0f),
			new Color(0f, 0.47058824f, 0f),
			new Color(0f, 0.40784314f, 0f),
			new Color(0f, 0.34509805f, 0f),
			new Color(0f, 0.2509804f, 0.34509805f),
			new Color(0f, 0f, 0f),
			new Color(0.7372549f, 0.7372549f, 0.7372549f),
			new Color(0f, 0.47058824f, 0.972549f),
			new Color(0f, 0.34509805f, 0.972549f),
			new Color(0.40784314f, 4f / 15f, 84f / 85f),
			new Color(72f / 85f, 0f, 0.8f),
			new Color(76f / 85f, 0f, 0.34509805f),
			new Color(0.972549f, 0.21960784f, 0f),
			new Color(76f / 85f, 0.36078432f, 0.0627451f),
			new Color(0.6745098f, 0.4862745f, 0f),
			new Color(0f, 0.72156864f, 0f),
			new Color(0f, 56f / 85f, 0f),
			new Color(0f, 56f / 85f, 4f / 15f),
			new Color(0f, 8f / 15f, 8f / 15f),
			new Color(0f, 0f, 0f),
			new Color(0.972549f, 0.972549f, 0.972549f),
			new Color(0.23529412f, 0.7372549f, 84f / 85f),
			new Color(0.40784314f, 8f / 15f, 84f / 85f),
			new Color(0.59607846f, 0.47058824f, 0.972549f),
			new Color(0.972549f, 0.47058824f, 0.972549f),
			new Color(0.972549f, 0.34509805f, 0.59607846f),
			new Color(0.972549f, 0.47058824f, 0.34509805f),
			new Color(84f / 85f, 32f / 51f, 4f / 15f),
			new Color(0.972549f, 0.72156864f, 0f),
			new Color(0.72156864f, 0.972549f, 8f / 85f),
			new Color(0.34509805f, 72f / 85f, 28f / 85f),
			new Color(0.34509805f, 0.972549f, 0.59607846f),
			new Color(0f, 0.9098039f, 72f / 85f),
			new Color(0.47058824f, 0.47058824f, 0.47058824f),
			new Color(84f / 85f, 84f / 85f, 84f / 85f),
			new Color(0.6431373f, 76f / 85f, 84f / 85f),
			new Color(0.72156864f, 0.72156864f, 0.972549f),
			new Color(72f / 85f, 0.72156864f, 0.972549f),
			new Color(0.972549f, 0.72156864f, 0.972549f),
			new Color(0.972549f, 0.72156864f, 64f / 85f),
			new Color(0.9411765f, 0.8156863f, 0.6901961f),
			new Color(84f / 85f, 0.8784314f, 56f / 85f),
			new Color(0.972549f, 72f / 85f, 0.47058824f),
			new Color(72f / 85f, 0.972549f, 0.47058824f),
			new Color(0.72156864f, 0.972549f, 0.72156864f),
		});

		private static (string, bool, string) ColorNameUtilAllPaletteColorsMapped() {
			for (int i = 0; i < PaletteColors.Length; i++) {
				string name = ColorNameUtil.GetColorName(PaletteColors[i]);
				if (string.IsNullOrEmpty(name))
					return Assert("ColorNameUtilAllPaletteColorsMapped", false,
						$"index {i} returned null/empty");
			}
			return Assert("ColorNameUtilAllPaletteColorsMapped", true, null);
		}

		private static (string, bool, string) ColorNameUtilNoDuplicateNames() {
			var seen = new Dictionary<string, int>();
			for (int i = 0; i < PaletteColors.Length; i++) {
				string name = ColorNameUtil.GetColorName(PaletteColors[i]);
				if (name == null) continue;
				if (seen.ContainsKey(name)) {
					// Allow duplicate for the two black entries (indices 13 and 27)
					if (PaletteColors[i] == PaletteColors[seen[name]]) continue;
					return Assert("ColorNameUtilNoDuplicateNames", false,
						$"'{name}' at indices {seen[name]} and {i}");
				}
				seen[name] = i;
			}
			return Assert("ColorNameUtilNoDuplicateNames", true, null);
		}

		private static (string, bool, string) ColorNameUtilUnknownColorReturnsNull() {
			string result = ColorNameUtil.GetColorName(new Color(0.123f, 0.456f, 0.789f));
			return Assert("ColorNameUtilUnknownColorReturnsNull", result == null,
				$"expected null, got '{result}'");
		}

		// ========================================
		// ScannerSnapshot tests
		// ========================================

		private static (ScannerSnapshot snapshot, ScannerItem item, ScanEntry e1, ScanEntry e2)
				BuildTwoInstanceSnapshot() {
			var snapshot = new ScannerSnapshot(new List<ScanEntry>(), 0);
			var e1 = new ScanEntry { Cell = 1, Category = "Cat", Subcategory = "Sub", ItemName = "Item" };
			var e2 = new ScanEntry { Cell = 2, Category = "Cat", Subcategory = "Sub", ItemName = "Item" };
			var item = new ScannerItem { ItemName = "Item", Instances = new List<ScanEntry> { e1, e2 } };
			var allSub = new ScannerSubcategory { Name = "All", Items = new List<ScannerItem> { item } };
			var namedSub = new ScannerSubcategory { Name = "Sub", Items = new List<ScannerItem> { item } };
			var cat = new ScannerCategory {
				Name = "Cat",
				Subcategories = new List<ScannerSubcategory> { allSub, namedSub }
			};
			snapshot.Categories.Add(cat);
			return (snapshot, item, e1, e2);
		}

		private static (string, bool, string) RemoveInstanceKeepsStructure() {
			var (snapshot, item, e1, _) = BuildTwoInstanceSnapshot();
			snapshot.RemoveInstance(item, e1);
			bool ok = item.Instances.Count == 1
				   && snapshot.CategoryCount == 1
				   && snapshot.GetCategory(0).Subcategories.Count == 2;
			return Assert("RemoveInstanceKeepsStructure", ok,
				$"instances={item.Instances.Count}, cats={snapshot.CategoryCount}, " +
				$"subs={snapshot.GetCategory(0).Subcategories.Count}");
		}

		private static (string, bool, string) RemoveLastInstancePrunesBothSubcategories() {
			var (snapshot, item, e1, e2) = BuildTwoInstanceSnapshot();
			snapshot.RemoveInstance(item, e1);
			snapshot.RemoveInstance(item, e2);
			bool ok = snapshot.CategoryCount == 0;
			return Assert("RemoveLastInstancePrunesBothSubcategories", ok,
				$"cats={snapshot.CategoryCount}");
		}

		private static (string, bool, string) PruneEmptySubcategory() {
			var snapshot = new ScannerSnapshot(new List<ScanEntry>(), 0);
			var e1 = new ScanEntry { Cell = 1, Category = "Cat", Subcategory = "SubA", ItemName = "ItemA" };
			var itemA = new ScannerItem { ItemName = "ItemA", Instances = new List<ScanEntry> { e1 } };
			var e2 = new ScanEntry { Cell = 2, Category = "Cat", Subcategory = "SubB", ItemName = "ItemB" };
			var itemB = new ScannerItem { ItemName = "ItemB", Instances = new List<ScanEntry> { e2 } };
			var allSub = new ScannerSubcategory { Name = "All", Items = new List<ScannerItem> { itemA, itemB } };
			var subA = new ScannerSubcategory { Name = "SubA", Items = new List<ScannerItem> { itemA } };
			var subB = new ScannerSubcategory { Name = "SubB", Items = new List<ScannerItem> { itemB } };
			var cat = new ScannerCategory {
				Name = "Cat",
				Subcategories = new List<ScannerSubcategory> { allSub, subA, subB }
			};
			snapshot.Categories.Add(cat);

			snapshot.RemoveInstance(itemA, e1);
			bool ok = snapshot.CategoryCount == 1
				   && cat.Subcategories.Count == 2
				   && cat.Subcategories[0].Name == "All"
				   && cat.Subcategories[0].Items.Count == 1
				   && cat.Subcategories[1].Name == "SubB";
			return Assert("PruneEmptySubcategory", ok,
				$"cats={snapshot.CategoryCount}, subs={cat.Subcategories.Count}, " +
				$"allItems={cat.Subcategories[0].Items.Count}");
		}

		private static (string, bool, string) FullCascadePrunesCategory() {
			var snapshot = new ScannerSnapshot(new List<ScanEntry>(), 0);
			var e1 = new ScanEntry { Cell = 1, Category = "Cat", Subcategory = "Sub", ItemName = "Item" };
			var item = new ScannerItem { ItemName = "Item", Instances = new List<ScanEntry> { e1 } };
			var allSub = new ScannerSubcategory { Name = "All", Items = new List<ScannerItem> { item } };
			var namedSub = new ScannerSubcategory { Name = "Sub", Items = new List<ScannerItem> { item } };
			var cat = new ScannerCategory {
				Name = "Cat",
				Subcategories = new List<ScannerSubcategory> { allSub, namedSub }
			};
			snapshot.Categories.Add(cat);

			snapshot.RemoveInstance(item, e1);
			bool ok = snapshot.CategoryCount == 0;
			return Assert("FullCascadePrunesCategory", ok,
				$"cats={snapshot.CategoryCount}");
		}

		// ========================================
		// Custom scanner category synthesis tests
		// ========================================

		private static ScanEntry CustomEntry(string cat, string sub, string name, int cell) {
			return new ScanEntry { Cell = cell, Category = cat, Subcategory = sub, ItemName = name };
		}

		private static ScannerCategory FindCat(ScannerSnapshot s, string name) {
			foreach (var c in s.Categories)
				if (c.Name == name) return c;
			return null;
		}

		private static ScannerSubcategory FindSub(ScannerCategory c, string name) {
			if (c == null) return null;
			foreach (var sub in c.Subcategories)
				if (sub.Name == name) return sub;
			return null;
		}

		private static List<CustomScannerCategory> OneDef(string id, string name,
				params CustomSelector[] selectors) {
			return new List<CustomScannerCategory> {
				new CustomScannerCategory {
					Id = id, Name = name, Selectors = new List<CustomSelector>(selectors),
				}
			};
		}

		private static CustomSelector Sel(string cat, string sub) =>
			new CustomSelector { Category = cat, Subcategory = sub };

		private static (string, bool, string) CustomCategorySortsToFront() {
			SetupGrid(100);
			var entries = new List<ScanEntry> {
				CustomEntry("Solids", "Ores", "Iron", 0),
			};
			var defs = OneDef("c1", "MyOres", Sel("Solids", "all"));
			var snap = new ScannerSnapshot(entries, 0, defs);
			bool ok = snap.CategoryCount >= 1
				&& snap.GetCategory(0).Name == "c1"
				&& snap.GetCategory(0).DisplayName == "MyOres";
			return Assert("CustomCategorySortsToFront", ok,
				$"first={snap.GetCategory(0).Name}/{snap.GetCategory(0).DisplayName}");
		}

		private static (string, bool, string) CustomAllSelectorGathersAcrossSubs() {
			SetupGrid(100);
			var entries = new List<ScanEntry> {
				CustomEntry("Solids", "Ores", "Iron", 0),
				CustomEntry("Solids", "Stone", "Granite", 1),
			};
			var snap = new ScannerSnapshot(entries, 0, OneDef("c1", "All Solids", Sel("Solids", "all")));
			var sub = FindSub(FindCat(snap, "c1"), "Solids");
			bool ok = sub != null && sub.Items.Count == 2;
			return Assert("CustomAllSelectorGathersAcrossSubs", ok,
				$"items={(sub == null ? -1 : sub.Items.Count)}");
		}

		private static (string, bool, string) CustomNamedSelectorFiltersToSub() {
			SetupGrid(100);
			var entries = new List<ScanEntry> {
				CustomEntry("Solids", "Ores", "Iron", 0),
				CustomEntry("Solids", "Stone", "Granite", 1),
			};
			var snap = new ScannerSnapshot(entries, 0, OneDef("c1", "Just Ores", Sel("Solids", "Ores")));
			var custom = FindCat(snap, "c1");
			var sub = FindSub(custom, "Ores");
			var allSub = FindSub(custom, "all");
			bool ok = sub != null && sub.Items.Count == 1 && sub.Items[0].ItemName == "Iron"
				&& allSub != null && allSub.Items.Count == 1;
			return Assert("CustomNamedSelectorFiltersToSub", ok,
				$"sub={(sub == null ? -1 : sub.Items.Count)}, all={(allSub == null ? -1 : allSub.Items.Count)}");
		}

		private static (string, bool, string) CustomItemsDistinctFromReal() {
			SetupGrid(100);
			var entries = new List<ScanEntry> {
				CustomEntry("Solids", "Ores", "Iron", 0),
			};
			var snap = new ScannerSnapshot(entries, 0, OneDef("c1", "Mine", Sel("Solids", "all")));
			var realItem = FindSub(FindCat(snap, "Solids"), "Ores")?.Items[0];
			var customItem = FindSub(FindCat(snap, "c1"), "Solids")?.Items[0];
			bool ok = realItem != null && customItem != null
				&& !ReferenceEquals(realItem, customItem);
			return Assert("CustomItemsDistinctFromReal", ok,
				$"real={(realItem != null)}, custom={(customItem != null)}");
		}

		private static (string, bool, string) CustomAllSharesRefWithinCustom() {
			SetupGrid(100);
			var entries = new List<ScanEntry> {
				CustomEntry("Solids", "Ores", "Iron", 0),
			};
			var snap = new ScannerSnapshot(entries, 0, OneDef("c1", "Mine", Sel("Solids", "all")));
			var custom = FindCat(snap, "c1");
			var selItem = FindSub(custom, "Solids")?.Items[0];
			var allItem = FindSub(custom, "all")?.Items[0];
			bool ok = selItem != null && ReferenceEquals(selItem, allItem);
			return Assert("CustomAllSharesRefWithinCustom", ok,
				$"sel={(selItem != null)}, same={ReferenceEquals(selItem, allItem)}");
		}

		private static (string, bool, string) CustomEmptyCategorySkipped() {
			SetupGrid(100);
			var entries = new List<ScanEntry> {
				CustomEntry("Solids", "Ores", "Iron", 0),
			};
			var defs = new List<CustomScannerCategory> {
				new CustomScannerCategory { Id = "c1", Name = "NoMatch",
					Selectors = new List<CustomSelector> { Sel("Liquids", "all") } },
				new CustomScannerCategory { Id = "c2", Name = "NoSelectors",
					Selectors = new List<CustomSelector>() },
			};
			var snap = new ScannerSnapshot(entries, 0, defs);
			bool c1Present = FindCat(snap, "c1") != null;
			bool c2Present = FindCat(snap, "c2") != null;
			bool ok = !c1Present && !c2Present && FindCat(snap, "Solids") != null;
			return Assert("CustomEmptyCategorySkipped", ok,
				$"c1={c1Present}, c2={c2Present}");
		}

		private static (string, bool, string) CustomPruneDoesNotAffectReal() {
			SetupGrid(100);
			var entries = new List<ScanEntry> {
				CustomEntry("Solids", "Ores", "Iron", 0),
			};
			var snap = new ScannerSnapshot(entries, 0, OneDef("c1", "Mine", Sel("Solids", "all")));
			var customItem = FindSub(FindCat(snap, "c1"), "Solids").Items[0];
			snap.RemoveInstance(customItem, customItem.Instances[0]);
			// Real category's Ores item must survive the custom prune.
			var realSub = FindSub(FindCat(snap, "Solids"), "Ores");
			bool ok = realSub != null && realSub.Items.Count == 1
				&& FindSub(FindCat(snap, "c1"), "Solids") == null;
			return Assert("CustomPruneDoesNotAffectReal", ok,
				$"realOres={(realSub == null ? -1 : realSub.Items.Count)}");
		}

		private static List<CustomScannerCategory> DefKw(string id, string name,
				string[] keywords, params CustomSelector[] selectors) {
			return new List<CustomScannerCategory> {
				new CustomScannerCategory {
					Id = id, Name = name,
					Selectors = new List<CustomSelector>(selectors),
					Keywords = new List<string>(keywords ?? new string[0]),
				}
			};
		}

		private static (string, bool, string) CustomKeywordGathersMatches() {
			SetupGrid(100);
			var entries = new List<ScanEntry> {
				CustomEntry("Solids", "Ores", "Iron Ore", 0),
				CustomEntry("Buildings", "Production", "Iron Volcano", 1),
				CustomEntry("Solids", "Stone", "Granite", 2),
			};
			var snap = new ScannerSnapshot(entries, 0, DefKw("c1", "Iron", new[] { "iron" }));
			var sub = FindSub(FindCat(snap, "c1"), "iron");
			bool ok = sub != null && sub.Items.Count == 2;
			return Assert("CustomKeywordGathersMatches", ok,
				$"items={(sub == null ? -1 : sub.Items.Count)}");
		}

		private static (string, bool, string) CustomKeywordSortsBeforeSelectors() {
			SetupGrid(100);
			var entries = new List<ScanEntry> {
				CustomEntry("Solids", "Ores", "Iron Ore", 0),
			};
			var snap = new ScannerSnapshot(entries, 0,
				DefKw("c1", "Mix", new[] { "iron" }, Sel("Solids", "all")));
			var cat = FindCat(snap, "c1");
			// subs[0] is the synthetic "all"; keyword precedes the selector sub.
			bool ok = cat != null && cat.Subcategories.Count == 3
				&& cat.Subcategories[1].Name == "iron"
				&& cat.Subcategories[2].Name == "Solids";
			return Assert("CustomKeywordSortsBeforeSelectors", ok,
				cat == null ? "no cat" : string.Join("/", cat.Subcategories.ConvertAll(s => s.Name).ToArray()));
		}

		private static (string, bool, string) CustomKeywordRanksByMatchQuality() {
			SetupGrid(100);
			var entries = new List<ScanEntry> {
				CustomEntry("Debris", "Materials", "Scrap Iron", 0),
				CustomEntry("Solids", "Ores", "Iron Ore", 1),
			};
			var snap = new ScannerSnapshot(entries, 0, DefKw("c1", "Iron", new[] { "iron" }));
			var sub = FindSub(FindCat(snap, "c1"), "iron");
			// Prefix match ("Iron Ore") outranks word-boundary match ("Scrap Iron").
			bool ok = sub != null && sub.Items.Count == 2
				&& sub.Items[0].ItemName == "Iron Ore"
				&& sub.Items[1].ItemName == "Scrap Iron";
			return Assert("CustomKeywordRanksByMatchQuality", ok,
				sub == null ? "no sub" : $"{sub.Items[0].ItemName},{sub.Items[1].ItemName}");
		}

		private static (string, bool, string) CustomKeywordSelectorOverlapDedupsAll() {
			SetupGrid(100);
			var entries = new List<ScanEntry> {
				CustomEntry("Solids", "Ores", "Iron Ore", 0),
			};
			var snap = new ScannerSnapshot(entries, 0,
				DefKw("c1", "Mix", new[] { "iron" }, Sel("Solids", "all")));
			var cat = FindCat(snap, "c1");
			var allSub = FindSub(cat, "all");
			var kwItem = FindSub(cat, "iron")?.Items[0];
			var selItem = FindSub(cat, "Solids")?.Items[0];
			// "all" lists the overlapping item once, and the keyword and selector
			// subs share the one pooled ScannerItem so prune-by-identity reaches both.
			bool ok = allSub != null && allSub.Items.Count == 1
				&& kwItem != null && ReferenceEquals(kwItem, selItem);
			return Assert("CustomKeywordSelectorOverlapDedupsAll", ok,
				$"all={(allSub == null ? -1 : allSub.Items.Count)}, shared={ReferenceEquals(kwItem, selItem)}");
		}

		private static (string, bool, string) CustomKeywordPrunePropagates() {
			SetupGrid(100);
			var entries = new List<ScanEntry> {
				CustomEntry("Solids", "Ores", "Iron Ore", 0),
			};
			var snap = new ScannerSnapshot(entries, 0,
				DefKw("c1", "Mix", new[] { "iron" }, Sel("Solids", "all")));
			var kwItem = FindSub(FindCat(snap, "c1"), "iron").Items[0];
			snap.RemoveInstance(kwItem, kwItem.Instances[0]);
			// Removing the shared item empties every sub, so the custom category
			// disappears, while the real Solids category keeps its own item.
			bool ok = FindCat(snap, "c1") == null
				&& FindSub(FindCat(snap, "Solids"), "Ores")?.Items.Count == 1;
			return Assert("CustomKeywordPrunePropagates", ok,
				$"customGone={(FindCat(snap, "c1") == null)}");
		}

		private static (string, bool, string) CustomSameNameDistinctSubcategoriesNotMerged() {
			SetupGrid(100);
			var entries = new List<ScanEntry> {
				CustomEntry("Life", "Wild Critters", "Hatch", 0),
				CustomEntry("Life", "Tame Critters", "Hatch", 1),
			};
			var defs = new List<CustomScannerCategory> {
				new CustomScannerCategory { Id = "c1", Name = "Critters",
					Selectors = new List<CustomSelector> {
						Sel("Life", "Wild Critters"), Sel("Life", "Tame Critters") } }
			};
			var snap = new ScannerSnapshot(entries, 0, defs);
			var cat = FindCat(snap, "c1");
			var wild = FindSub(cat, "Wild Critters");
			var tame = FindSub(cat, "Tame Critters");
			var all = FindSub(cat, "all");
			// Same species name under two subcategories must stay two distinct
			// items, each with its own instance, not collapse via the pool.
			bool ok = wild != null && tame != null && all != null
				&& wild.Items.Count == 1 && wild.Items[0].Instances[0].Cell == 0
				&& tame.Items.Count == 1 && tame.Items[0].Instances[0].Cell == 1
				&& !ReferenceEquals(wild.Items[0], tame.Items[0])
				&& all.Items.Count == 2;
			return Assert("CustomSameNameDistinctSubcategoriesNotMerged", ok,
				$"wild={(wild == null ? -1 : wild.Items.Count)}, tame={(tame == null ? -1 : tame.Items.Count)}, all={(all == null ? -1 : all.Items.Count)}");
		}

		private static (string, bool, string) CustomKeywordCrossCategoryNotMerged() {
			SetupGrid(100);
			var entries = new List<ScanEntry> {
				CustomEntry("Solids", "Ores", "Coal", 0),
				CustomEntry("Debris", "Materials", "Coal", 1),
			};
			var snap = new ScannerSnapshot(entries, 0,
				DefKw("c1", "Coal", new[] { "coal" }, Sel("Solids", "all")));
			var cat = FindCat(snap, "c1");
			var kwSub = FindSub(cat, "coal");
			var selSub = FindSub(cat, "Solids");
			// The keyword matches both Coals (two distinct items), but the Solids
			// selector must keep only the solid one, not inherit the debris
			// instance through the shared pool.
			bool ok = kwSub != null && kwSub.Items.Count == 2
				&& selSub != null && selSub.Items.Count == 1
				&& selSub.Items[0].Instances.Count == 1
				&& selSub.Items[0].Instances[0].Cell == 0;
			return Assert("CustomKeywordCrossCategoryNotMerged", ok,
				$"kw={(kwSub == null ? -1 : kwSub.Items.Count)}, selInst={(selSub == null || selSub.Items.Count == 0 ? -1 : selSub.Items[0].Instances.Count)}");
		}

		// ========================================
		// Custom scanner category selector-logic tests
		// ========================================

		private static (string, bool, string) StoreSetAllSupersedesSubs() {
			var c = new CustomScannerCategory { Id = "x", Name = "n" };
			CustomCategoryStore.ApplyAll(c, "Solids", true);
			bool ok = CustomCategoryStore.IsAllSelected(c, "Solids")
				&& CustomCategoryStore.IsSubSelected(c, "Solids", "Ores")
				&& CustomCategoryStore.IsSubSelected(c, "Solids", "Stone")
				&& c.Selectors.Count == 1;
			return Assert("StoreSetAllSupersedesSubs", ok, $"selectors={c.Selectors.Count}");
		}

		private static (string, bool, string) StoreToggleSubWhileAllExpands() {
			var c = new CustomScannerCategory { Id = "x", Name = "n" };
			CustomCategoryStore.ApplyAll(c, "Solids", true);
			CustomCategoryStore.ApplySub(c, "Solids", "Ores", false);
			bool ok = !CustomCategoryStore.IsAllSelected(c, "Solids")
				&& !CustomCategoryStore.IsSubSelected(c, "Solids", "Ores")
				&& CustomCategoryStore.IsSubSelected(c, "Solids", "Stone");
			return Assert("StoreToggleSubWhileAllExpands", ok, $"selectors={c.Selectors.Count}");
		}

		private static (string, bool, string) StoreToggleSubIndividual() {
			var c = new CustomScannerCategory { Id = "x", Name = "n" };
			CustomCategoryStore.ApplySub(c, "Solids", "Ores", true);
			bool on = !CustomCategoryStore.IsAllSelected(c, "Solids")
				&& CustomCategoryStore.IsSubSelected(c, "Solids", "Ores")
				&& !CustomCategoryStore.IsSubSelected(c, "Solids", "Stone")
				&& c.Selectors.Count == 1;
			CustomCategoryStore.ApplySub(c, "Solids", "Ores", false);
			bool off = !CustomCategoryStore.IsSubSelected(c, "Solids", "Ores")
				&& c.Selectors.Count == 0;
			return Assert("StoreToggleSubIndividual", on && off, $"on={on}, off={off}");
		}

		private static (string, bool, string) StoreSetAllOffClears() {
			var c = new CustomScannerCategory { Id = "x", Name = "n" };
			CustomCategoryStore.ApplyAll(c, "Solids", true);
			CustomCategoryStore.ApplyAll(c, "Solids", false);
			bool ok = !CustomCategoryStore.IsAllSelected(c, "Solids") && c.Selectors.Count == 0;
			return Assert("StoreSetAllOffClears", ok, $"selectors={c.Selectors.Count}");
		}

		// ========================================
		// WrapSkipEmpty tests
		// ========================================

		private static int InvokeWrapSkipEmpty(int current, int direction,
				List<string> list, Func<string, bool> isNonEmpty) {
			var method = typeof(ScannerNavigator).GetMethod("WrapSkipEmpty",
				BindingFlags.NonPublic | BindingFlags.Static);
			if (method == null)
				throw new MissingMethodException(
					"ScannerNavigator.WrapSkipEmpty not found — was it renamed?");
			var generic = method.MakeGenericMethod(typeof(string));
			return (int)generic.Invoke(null, new object[] { current, direction, list, isNonEmpty });
		}

		private static (string, bool, string) WrapSkipEmptyForwardWrap() {
			var list = new List<string> { "a", "", "", "b", "" };
			int result = InvokeWrapSkipEmpty(3, 1, list, s => s.Length > 0);
			bool ok = result == 0;
			return Assert("WrapSkipEmptyForwardWrap", ok, $"result={result}");
		}

		private static (string, bool, string) WrapSkipEmptyBackwardWrap() {
			var list = new List<string> { "a", "", "", "b", "" };
			int result = InvokeWrapSkipEmpty(0, -1, list, s => s.Length > 0);
			bool ok = result == 3;
			return Assert("WrapSkipEmptyBackwardWrap", ok, $"result={result}");
		}

		private static (string, bool, string) WrapSkipEmptyAllEmptyReturnsCurrent() {
			var list = new List<string> { "", "", "" };
			int result = InvokeWrapSkipEmpty(1, 1, list, s => s.Length > 0);
			bool ok = result == 1;
			return Assert("WrapSkipEmptyAllEmptyReturnsCurrent", ok, $"result={result}");
		}

		private static (string, bool, string) WrapSkipEmptySingleNonEmpty() {
			var list = new List<string> { "", "x", "", "" };
			int fwd = InvokeWrapSkipEmpty(0, 1, list, s => s.Length > 0);
			int bwd = InvokeWrapSkipEmpty(2, -1, list, s => s.Length > 0);
			int self = InvokeWrapSkipEmpty(1, 1, list, s => s.Length > 0);
			bool ok = fwd == 1 && bwd == 1 && self == 1;
			return Assert("WrapSkipEmptySingleNonEmpty", ok,
				$"fwd={fwd}, bwd={bwd}, self={self}");
		}

		private static (string, bool, string) PipelineNullAndEmptySkipped() {
			float fakeTime = 0f;
			var spoken = new List<(string text, bool interrupt)>();
			SpeechPipeline.TimeSource = () => fakeTime;
			SpeechPipeline.SpeakAction = (text, intr) => spoken.Add((text, intr));
			SpeechPipeline.Reset();

			SpeechPipeline.SpeakInterrupt(null);
			SpeechPipeline.SpeakInterrupt("");
			bool ok = spoken.Count == 0;
			return Assert("PipelineNullAndEmptySkipped", ok, $"spoken={spoken.Count}");
		}

		private static (string, bool, string) PipelineInterruptFlagIsTrue() {
			float fakeTime = 0f;
			var spoken = new List<(string text, bool interrupt)>();
			SpeechPipeline.TimeSource = () => fakeTime;
			SpeechPipeline.SpeakAction = (text, intr) => spoken.Add((text, intr));
			SpeechPipeline.Reset();

			SpeechPipeline.SpeakInterrupt("hello");
			bool ok = spoken.Count == 1 && spoken[0].interrupt == true;
			return Assert("PipelineInterruptFlagIsTrue", ok,
				$"spoken={spoken.Count}" + (spoken.Count > 0 ? $", interrupt={spoken[0].interrupt}" : ""));
		}

		private static (string, bool, string) PipelineQueuedSpeaksWithoutInterrupt() {
			float fakeTime = 0f;
			var spoken = new List<(string text, bool interrupt)>();
			SpeechPipeline.TimeSource = () => fakeTime;
			SpeechPipeline.SpeakAction = (text, intr) => spoken.Add((text, intr));
			SpeechPipeline.Reset();

			SpeechPipeline.SpeakQueued("hello");
			bool ok = spoken.Count == 1 && spoken[0].text == "hello" && spoken[0].interrupt == false;
			return Assert("PipelineQueuedSpeaksWithoutInterrupt", ok,
				$"spoken={spoken.Count}" + (spoken.Count > 0 ? $", text=\"{spoken[0].text}\", interrupt={spoken[0].interrupt}" : ""));
		}

		private static (string, bool, string) PipelineQueuedNotDeduplicated() {
			float fakeTime = 0f;
			var spoken = new List<(string text, bool interrupt)>();
			SpeechPipeline.TimeSource = () => fakeTime;
			SpeechPipeline.SpeakAction = (text, intr) => spoken.Add((text, intr));
			SpeechPipeline.Reset();

			SpeechPipeline.SpeakQueued("hello");
			SpeechPipeline.SpeakQueued("hello");
			bool ok = spoken.Count == 2;
			return Assert("PipelineQueuedNotDeduplicated", ok, $"spoken={spoken.Count}");
		}

		private static (string, bool, string) PipelineInterruptDedupeDoesNotAffectQueued() {
			float fakeTime = 0f;
			var spoken = new List<(string text, bool interrupt)>();
			SpeechPipeline.TimeSource = () => fakeTime;
			SpeechPipeline.SpeakAction = (text, intr) => spoken.Add((text, intr));
			SpeechPipeline.Reset();

			SpeechPipeline.SpeakInterrupt("hello");
			SpeechPipeline.SpeakQueued("hello");
			bool ok = spoken.Count == 2
				&& spoken[0].interrupt == true
				&& spoken[1].interrupt == false;
			return Assert("PipelineInterruptDedupeDoesNotAffectQueued", ok,
				$"spoken={spoken.Count}" + (spoken.Count >= 2 ? $", [0].interrupt={spoken[0].interrupt}, [1].interrupt={spoken[1].interrupt}" : ""));
		}

		// ========================================
		// ScannerTaxonomy tests
		// ========================================

		private static (string, bool, string) TaxonomyAllCategoriesHaveSubcategories() {
			var missing = new List<string>();
			foreach (string cat in ScannerTaxonomy.CategoryOrder) {
				if (!ScannerTaxonomy.SubcategoryOrder.ContainsKey(cat))
					missing.Add(cat);
			}
			// Also check reverse: every SubcategoryOrder key is in CategoryOrder
			var catSet = new HashSet<string>(ScannerTaxonomy.CategoryOrder);
			foreach (string key in ScannerTaxonomy.SubcategoryOrder.Keys) {
				if (!catSet.Contains(key))
					missing.Add($"SubcategoryOrder has orphan: {key}");
			}
			bool ok = missing.Count == 0;
			return Assert("TaxonomyAllCategoriesHaveSubcategories", ok,
				ok ? "all synced" : $"missing: {string.Join(", ", missing)}");
		}

		private static (string, bool, string) TaxonomySortIndicesRoundTrip() {
			var failures = new List<string>();
			for (int i = 0; i < ScannerTaxonomy.CategoryOrder.Length; i++) {
				string cat = ScannerTaxonomy.CategoryOrder[i];
				int idx = ScannerTaxonomy.CategorySortIndex(cat);
				if (idx != i)
					failures.Add($"CategorySortIndex({cat})={idx}, expected {i}");
				if (!ScannerTaxonomy.SubcategoryOrder.TryGetValue(cat, out string[] subs))
					continue;
				for (int j = 0; j < subs.Length; j++) {
					int subIdx = ScannerTaxonomy.SubcategorySortIndex(cat, subs[j]);
					if (subIdx != j)
						failures.Add($"SubcategorySortIndex({cat},{subs[j]})={subIdx}, expected {j}");
				}
			}
			// Unknown category returns int.MaxValue
			int unknown = ScannerTaxonomy.CategorySortIndex("Nonexistent");
			if (unknown != int.MaxValue)
				failures.Add($"Unknown category returned {unknown}, expected int.MaxValue");
			bool ok = failures.Count == 0;
			return Assert("TaxonomySortIndicesRoundTrip", ok,
				ok ? "all correct" : string.Join("; ", failures));
		}

		// ========================================
		// GlanceComposer tests
		// ========================================

		private class StubSection: ICellSection {
			private readonly string[] _tokens;
			public StubSection(params string[] tokens) { _tokens = tokens; }
			public IEnumerable<string> Read(int cell, CellContext ctx) => _tokens;
		}

		private class ThrowingSection: ICellSection {
			public IEnumerable<string> Read(int cell, CellContext ctx) {
				throw new InvalidOperationException("section exploded");
			}
		}

		private static (string, bool, string) ComposerThrowingSectionDoesNotAbortOthers() {
			var sections = new List<ICellSection> {
				new StubSection("alpha"),
				new ThrowingSection(),
				new StubSection("beta"),
			};
			var composer = new GlanceComposer(sections.AsReadOnly());
			string result = composer.Compose(0);
			bool ok = result == "alpha, beta";
			return Assert("ComposerThrowingSectionDoesNotAbortOthers", ok,
				$"got \"{result}\"");
		}

		private static (string, bool, string) ComposerAllEmptyReturnsNull() {
			var sections = new List<ICellSection> {
				new StubSection("", null),
				new StubSection(),
			};
			var composer = new GlanceComposer(sections.AsReadOnly());
			string result = composer.Compose(0);
			bool ok = result == null;
			return Assert("ComposerAllEmptyReturnsNull", ok,
				$"got {(result == null ? "null" : $"\"{result}\"")}");
		}

		// ========================================
		// AnnouncementFormatter tests
		// ========================================

		private static void SetupGrid(int width) {
			Grid.WidthInCells = width;
		}

		private static (string, bool, string) FormatDistanceSameCellReturnsEmpty() {
			SetupGrid(100);
			string result = AnnouncementFormatter.FormatDistance(505, 505);
			bool ok = result == "";
			return Assert("FormatDistanceSameCellReturnsEmpty", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) FormatDistanceVerticalOnly() {
			SetupGrid(100);
			// cell 505 = row 5, col 5; cell 805 = row 8, col 5 -> 3 up
			string result = AnnouncementFormatter.FormatDistance(505, 805);
			bool ok = result.Contains("3") && result.Contains("up")
				&& !result.Contains("left") && !result.Contains("right");
			return Assert("FormatDistanceVerticalOnly", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) FormatDistanceHorizontalOnly() {
			SetupGrid(100);
			// cell 505 = row 5, col 5; cell 502 = row 5, col 2 -> 3 left
			string result = AnnouncementFormatter.FormatDistance(505, 502);
			bool ok = result.Contains("3") && result.Contains("left")
				&& !result.Contains("up") && !result.Contains("down");
			return Assert("FormatDistanceHorizontalOnly", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) FormatDistanceBothAxes() {
			SetupGrid(100);
			// cell 505 = row 5, col 5; cell 208 = row 2, col 8 -> 3 down, 3 right
			string result = AnnouncementFormatter.FormatDistance(505, 208);
			bool ok = result.Contains("3") && result.Contains("down")
				&& result.Contains("right");
			return Assert("FormatDistanceBothAxes", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) FormatClusterSingleDelegatesToEntity() {
			SetupGrid(100);
			string cluster = AnnouncementFormatter.FormatClusterInstance(
				1, "Iron", 505, 505, 1, 5);
			string entity = AnnouncementFormatter.FormatEntityInstance(
				"Iron", 505, 505, 1, 5);
			bool ok = cluster == entity;
			return Assert("FormatClusterSingleDelegatesToEntity", ok,
				$"cluster=\"{cluster}\", entity=\"{entity}\"");
		}

		private static (string, bool, string) FormatClusterMultiIncludesCount() {
			SetupGrid(100);
			string single = AnnouncementFormatter.FormatClusterInstance(
				1, "Iron", 505, 505, 1, 5);
			string multi = AnnouncementFormatter.FormatClusterInstance(
				7, "Iron", 505, 505, 1, 5);
			bool ok = multi.Contains("7") && !single.Contains("7");
			return Assert("FormatClusterMultiIncludesCount", ok,
				$"single=\"{single}\", multi=\"{multi}\"");
		}

		// ========================================
		// BuildMenuData.GetOrientationName tests
		// ========================================

		private static (string, bool, string) OrientationNameCoversAllKnownValues() {
			var failures = new List<string>();

			// R360: full directional rotation
			Check(Orientation.Neutral, PermittedRotations.R360,
				(string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_UP, "R360+Neutral");
			Check(Orientation.R90, PermittedRotations.R360,
				(string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_RIGHT, "R360+R90");
			Check(Orientation.R180, PermittedRotations.R360,
				(string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_DOWN, "R360+R180");
			Check(Orientation.R270, PermittedRotations.R360,
				(string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_LEFT, "R360+R270");

			// R90: vertical/horizontal toggle
			Check(Orientation.Neutral, PermittedRotations.R90,
				(string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_VERTICAL, "R90+Neutral");
			Check(Orientation.R90, PermittedRotations.R90,
				(string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_HORIZONTAL, "R90+R90");

			// FlipH: right/left toggle
			Check(Orientation.Neutral, PermittedRotations.FlipH,
				(string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_RIGHT, "FlipH+Neutral");
			Check(Orientation.FlipH, PermittedRotations.FlipH,
				(string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_LEFT, "FlipH+FlipH");

			// FlipV: up/down toggle
			Check(Orientation.Neutral, PermittedRotations.FlipV,
				(string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_UP, "FlipV+Neutral");
			Check(Orientation.FlipV, PermittedRotations.FlipV,
				(string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_DOWN, "FlipV+FlipV");

			bool ok = failures.Count == 0;
			return Assert("OrientationNameCoversAllKnownValues", ok,
				ok ? "all correct" : string.Join("; ", failures));

			void Check(Orientation o, PermittedRotations p,
					string expected, string label) {
				string actual = BuildMenuData.GetOrientationName(o, p);
				if (actual != expected)
					failures.Add($"{label}=\"{actual}\" expected \"{expected}\"");
			}
		}

		private static (string, bool, string) OrientationNameDefaultReturnsUp() {
			string result = BuildMenuData.GetOrientationName(
				(Orientation)99, PermittedRotations.R360);
			string expected = (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_UP;
			bool ok = result == expected;
			return Assert("OrientationNameDefaultReturnsUp", ok,
				$"got \"{result}\", expected \"{expected}\"");
		}

		private static (string, bool, string) OrientationNameHorizontalFlowShiftsCW() {
			var failures = new List<string>();
			Check(Orientation.Neutral, (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_RIGHT, "Neutral");
			Check(Orientation.R90, (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_DOWN, "R90");
			Check(Orientation.R180, (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_LEFT, "R180");
			Check(Orientation.R270, (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_UP, "R270");
			bool ok = failures.Count == 0;
			return Assert("OrientationNameHorizontalFlowShiftsCW", ok,
				ok ? "all correct" : string.Join("; ", failures));

			void Check(Orientation o, string expected, string label) {
				string actual = BuildMenuData.GetOrientationName(
					o, PermittedRotations.R360, horizontalFlow: true);
				if (actual != expected)
					failures.Add($"{label}=\"{actual}\" expected \"{expected}\"");
			}
		}

		private static (string, bool, string) AppendOrientationUsesFacingForDirectionalRotations() {
			string dir = (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_RIGHT;
			string result = BuildMenuData.AppendOrientation(
				"Gas Bridge", dir, PermittedRotations.R360);
			string expected = "Gas Bridge, " + string.Format(
				(string)STRINGS.ONIACCESS.BUILD_MENU.FACING, dir);
			bool ok = result == expected;
			return Assert("AppendOrientationUsesFacingForDirectionalRotations", ok,
				$"got \"{result}\", expected \"{expected}\"");
		}

		private static (string, bool, string) AppendOrientationLeavesR90AsPlainAxis() {
			string axis = (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_HORIZONTAL;
			string result = BuildMenuData.AppendOrientation(
				"Transit Tube Crossing", axis, PermittedRotations.R90);
			string expected = "Transit Tube Crossing, " + axis;
			bool ok = result == expected;
			return Assert("AppendOrientationLeavesR90AsPlainAxis", ok,
				$"got \"{result}\", expected \"{expected}\"");
		}

		// ========================================
		// CleanTooltipEntry
		// ========================================

		private static (string, bool, string) CleanTooltipSingleNewline() {
			string result = WidgetOps.CleanTooltipEntry("a\nb");
			bool ok = result == "a. b";
			return Assert("CleanTooltipSingleNewline", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) CleanTooltipTripleNewlineCollapses() {
			string result = WidgetOps.CleanTooltipEntry("a\n\n\nb");
			bool ok = result == "a. b";
			return Assert("CleanTooltipTripleNewlineCollapses", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) CleanTooltipBulletWithSpace() {
			string result = WidgetOps.CleanTooltipEntry("\u2022 text");
			bool ok = result == "text";
			return Assert("CleanTooltipBulletWithSpace", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) CleanTooltipBulletWithSurroundingSpaces() {
			string result = WidgetOps.CleanTooltipEntry("x \u2022 y");
			bool ok = result == "x. y";
			return Assert("CleanTooltipBulletWithSurroundingSpaces", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) CleanTooltipLeadingWhitespaceTrimmed() {
			string result = WidgetOps.CleanTooltipEntry("  text");
			bool ok = result == "text";
			return Assert("CleanTooltipLeadingWhitespaceTrimmed", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) CleanTooltipDoubledPeriodCollapsed() {
			// Period before newline produces ".." after newline→". " replacement:
			// "a.\nb" → "a.. b" → "a. b" after doubled-period collapse.
			string result = WidgetOps.CleanTooltipEntry("a.\nb");
			bool ok = result == "a. b";
			return Assert("CleanTooltipDoubledPeriodCollapsed", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) CleanTooltipIndentedBulletAfterNewline() {
			// Game text like "\n<b>Category</b>:\n    • Toilet" — the indent between
			// the colon's newline and the bullet must not prevent period deduplication.
			string result = WidgetOps.CleanTooltipEntry("Label:\n    \u2022 Value");
			bool ok = result == "Label:. Value";
			return Assert("CleanTooltipIndentedBulletAfterNewline", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) CleanTooltipColonNewlineDropsPeriod() {
			// Room entry: "<b>Effects:</b>. Morale: +2" — after CleanTooltipEntry converts
			// newlines and FilterForSpeech strips bold tags, ":." must collapse to ":".
			string cleaned = WidgetOps.CleanTooltipEntry("<b>Effects:</b>\nMorale: +2");
			string result = TextFilter.FilterForSpeech(cleaned);
			bool ok = result == "Effects: Morale: +2";
			return Assert("CleanTooltipColonNewlineDropsPeriod", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) CleanTooltipNullAndEmptyPassthrough() {
			string rNull = WidgetOps.CleanTooltipEntry(null);
			string rEmpty = WidgetOps.CleanTooltipEntry("");
			bool ok = rNull == null && rEmpty == "";
			return Assert("CleanTooltipNullAndEmptyPassthrough", ok,
				$"null→\"{rNull}\", empty→\"{rEmpty}\"");
		}

		private static (string, bool, string) CleanTooltipStripsReplacementChar() {
			string r1 = WidgetOps.CleanTooltipEntry("Nutrient Bar \uFFFC");
			string r2 = WidgetOps.CleanTooltipEntry("Nutrient Bar \uE00F");
			string r3 = WidgetOps.CleanTooltipEntry("Nutrient Bar \uE00F, 21.9 °C");
			bool ok = r1 == "Nutrient Bar" && r2 == "Nutrient Bar"
				&& r3 == "Nutrient Bar, 21.9 °C";
			return Assert("CleanTooltipStripsReplacementChar", ok,
				$"fffc=\"{r1}\", pua=\"{r2}\", comma=\"{r3}\"");
		}

		// ========================================
		// AppendTooltip
		// ========================================

		private static (string, bool, string) AppendTooltipNullReturnsSpeech() {
			string result = WidgetOps.AppendTooltip("x", null);
			bool ok = result == "x";
			return Assert("AppendTooltipNullReturnsSpeech", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) AppendTooltipEmptySpeechReturnsTooltip() {
			string r1 = WidgetOps.AppendTooltip("", "tip");
			string r2 = WidgetOps.AppendTooltip(null, "tip");
			bool ok = r1 == "tip" && r2 == "tip";
			return Assert("AppendTooltipEmptySpeechReturnsTooltip", ok,
				$"empty→\"{r1}\", null→\"{r2}\"");
		}

		private static (string, bool, string) AppendTooltipDuplicateSegmentSuppressed() {
			string result = WidgetOps.AppendTooltip("a, b", "a");
			bool ok = result == "a, b";
			return Assert("AppendTooltipDuplicateSegmentSuppressed", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) AppendTooltipNonMatchingAppends() {
			string result = WidgetOps.AppendTooltip("a, b", "c");
			bool ok = result == "a, b, c";
			return Assert("AppendTooltipNonMatchingAppends", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) AppendTooltipSubstringNotSuppressed() {
			string result = WidgetOps.AppendTooltip("ab, c", "a");
			bool ok = result == "ab, c, a";
			return Assert("AppendTooltipSubstringNotSuppressed", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) AppendTooltipSingleSegmentDuplicate() {
			string result = WidgetOps.AppendTooltip("hello", "hello");
			bool ok = result == "hello";
			return Assert("AppendTooltipSingleSegmentDuplicate", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) AppendTooltipSentenceDedup() {
			// Storage item: tooltip has raw markup that gets filtered, then
			// first sentence (item name) is deduped against speech segments
			string result = WidgetOps.AppendTooltip(
				"Nutrient Bar, 21.9 °C, 12.6 kg",
				"Nutrient Bar. <sprite=\"oni_sprite_assets\" name=\"germs\"> 2,125 germs [<link=\"FP\">Food Poisoning</link>].");
			bool ok = result == "Nutrient Bar, 21.9 °C, 12.6 kg, 2,125 germs";
			return Assert("AppendTooltipSentenceDedup", ok, $"got \"{result}\"");
		}

		private static (string, bool, string) AppendTooltipAllSentencesDuplicate() {
			string result = WidgetOps.AppendTooltip("a, b", "a. b");
			bool ok = result == "a, b";
			return Assert("AppendTooltipAllSentencesDuplicate", ok, $"got \"{result}\"");
		}

		// ========================================
		// NavigableGraph
		// ========================================

		private static NavigableGraph<string> MakeTestGraph(
				out Dictionary<string, List<string>> children,
				out Dictionary<string, List<string>> parents,
				List<string> roots = null) {
			// Default graph:  A -> [B, C],  B -> [D, E]
			var c = new Dictionary<string, List<string>> {
				["A"] = new List<string> { "B", "C" },
				["B"] = new List<string> { "D", "E" },
			};
			var p = new Dictionary<string, List<string>> {
				["B"] = new List<string> { "A" },
				["C"] = new List<string> { "A" },
				["D"] = new List<string> { "B" },
				["E"] = new List<string> { "B" },
			};
			children = c;
			parents = p;
			return new NavigableGraph<string>(
				node => p.TryGetValue(node, out var list) ? list : (IReadOnlyList<string>)Array.Empty<string>(),
				node => c.TryGetValue(node, out var list) ? list : (IReadOnlyList<string>)Array.Empty<string>(),
				roots != null ? (Func<IReadOnlyList<string>>)(() => roots) : null);
		}

		private static (string, bool, string) GraphNavigateDownSetsSiblingContext() {
			var graph = MakeTestGraph(out _, out _);
			graph.MoveTo("A");
			string down = graph.NavigateDown(); // → B
			string sibling = graph.CycleSibling(1, out _); // → C
			bool ok = down == "B" && sibling == "C";
			return Assert("GraphNavigateDownSetsSiblingContext", ok,
				$"down=\"{down}\", sibling=\"{sibling}\"");
		}

		private static (string, bool, string) GraphNavigateUpAtRootEstablishesRootContext() {
			var roots = new List<string> { "A", "X" };
			var graph = MakeTestGraph(out _, out _, roots);
			graph.MoveTo("A");
			string up = graph.NavigateUp(); // null (already root)
			string sibling = graph.CycleSibling(1, out _); // → X
			bool ok = up == null && sibling == "X";
			return Assert("GraphNavigateUpAtRootEstablishesRootContext", ok,
				$"up=\"{up}\", sibling=\"{sibling}\"");
		}

		private static (string, bool, string) GraphNavigateUpToRootUsesRoots() {
			var roots = new List<string> { "A", "X" };
			var graph = MakeTestGraph(out _, out _, roots);
			graph.MoveTo("B");
			string up = graph.NavigateUp(); // → A (a root node)
											// A is a root, so siblings should be the roots list
			string sibling = graph.CycleSibling(1, out _); // → X
			bool ok = up == "A" && sibling == "X";
			return Assert("GraphNavigateUpToRootUsesRoots", ok,
				$"up=\"{up}\", sibling=\"{sibling}\"");
		}

		private static (string, bool, string) GraphCycleSiblingWrapForward() {
			var graph = MakeTestGraph(out _, out _);
			graph.MoveTo("A");
			graph.NavigateDown(); // → B, siblings = [B, C]
			graph.CycleSibling(1, out _); // → C
			string wrapped = graph.CycleSibling(1, out bool didWrap); // → B (wrap)
			bool ok = wrapped == "B" && didWrap;
			return Assert("GraphCycleSiblingWrapForward", ok,
				$"node=\"{wrapped}\", wrapped={didWrap}");
		}

		private static (string, bool, string) GraphCycleSiblingWrapBackward() {
			var graph = MakeTestGraph(out _, out _);
			graph.MoveTo("A");
			graph.NavigateDown(); // → B, siblings = [B, C]
			string wrapped = graph.CycleSibling(-1, out bool didWrap); // → C (wrap)
			bool ok = wrapped == "C" && didWrap;
			return Assert("GraphCycleSiblingWrapBackward", ok,
				$"node=\"{wrapped}\", wrapped={didWrap}");
		}

		private static (string, bool, string) GraphCycleSiblingNoWrap() {
			var graph = MakeTestGraph(out _, out _);
			graph.MoveTo("A");
			graph.NavigateDown(); // → B, siblings = [B, C]
			string next = graph.CycleSibling(1, out bool didWrap); // → C (no wrap)
			bool ok = next == "C" && !didWrap;
			return Assert("GraphCycleSiblingNoWrap", ok,
				$"node=\"{next}\", wrapped={didWrap}");
		}

		private static (string, bool, string) GraphMoveToClearsSiblingContext() {
			var graph = MakeTestGraph(out _, out _);
			graph.MoveTo("A");
			graph.NavigateDown(); // → B, establishes siblings
			graph.MoveTo("D"); // clears siblings
			string result = graph.CycleSibling(1, out _);
			bool ok = result == null;
			return Assert("GraphMoveToClearsSiblingContext", ok,
				$"CycleSibling returned \"{result}\"");
		}

		private static (string, bool, string) GraphMoveToWithSiblingsPreservesContext() {
			var siblings = new List<string> { "B", "C" };
			var graph = MakeTestGraph(out _, out _);
			graph.MoveToWithSiblings("C", siblings); // index 1
			string prev = graph.CycleSibling(-1, out bool didWrap); // → B (natural backward, no wrap)
			bool ok = prev == "B" && !didWrap;
			return Assert("GraphMoveToWithSiblingsPreservesContext", ok,
				$"node=\"{prev}\", wrapped={didWrap}");
		}

		private static (string, bool, string) GraphIndexOfFallbackReturnsZero() {
			var siblings = new List<string> { "X", "Y" };
			var graph = MakeTestGraph(out _, out _);
			graph.MoveToWithSiblings("Z", siblings); // Z not in list → index 0
			string next = graph.CycleSibling(1, out _); // from index 0 → Y
			bool ok = next == "Y";
			return Assert("GraphIndexOfFallbackReturnsZero", ok,
				$"CycleSibling returned \"{next}\"");
		}

		private static (string, bool, string) GraphSingleSiblingReturnsNull() {
			var c = new Dictionary<string, List<string>> {
				["A"] = new List<string> { "B" },
			};
			var p = new Dictionary<string, List<string>> {
				["B"] = new List<string> { "A" },
			};
			var graph = new NavigableGraph<string>(
				node => p.TryGetValue(node, out var list) ? list : (IReadOnlyList<string>)Array.Empty<string>(),
				node => c.TryGetValue(node, out var list) ? list : (IReadOnlyList<string>)Array.Empty<string>());
			graph.MoveTo("A");
			graph.NavigateDown(); // → B, siblings = [B] (only child)
			string result = graph.CycleSibling(1, out _);
			bool ok = result == null;
			return Assert("GraphSingleSiblingReturnsNull", ok,
				$"CycleSibling returned \"{result}\"");
		}

		// ========================================
		// NotificationAnnouncer
		// ========================================

		private static readonly MethodInfo _trackerAdd = typeof(NotificationTracker)
			.GetMethod("OnNotificationAdded", BindingFlags.Instance | BindingFlags.NonPublic);
		private static readonly MethodInfo _trackerRemove = typeof(NotificationTracker)
			.GetMethod("OnNotificationRemoved", BindingFlags.Instance | BindingFlags.NonPublic);

		private static void AddNotification(NotificationTracker tracker, string title) {
			_trackerAdd.Invoke(tracker, new object[] {
				new Notification(title, NotificationType.Bad)
			});
		}

		private static void RemoveFirstNotification(NotificationTracker tracker, string title) {
			for (int i = 0; i < tracker.Notifications.Count; i++) {
				if (tracker.Notifications[i].titleText == title) {
					_trackerRemove.Invoke(tracker, new object[] { tracker.Notifications[i] });
					return;
				}
			}
		}

		private class AnnouncerHarness {
			public float FakeTime;
			public bool Paused;
			public List<(string text, bool interrupt)> Spoken;
			public NotificationTracker Tracker;
			public NotificationAnnouncer Announcer;
		}

		private static AnnouncerHarness SetupAnnouncer(bool startPaused = true) {
			var h = new AnnouncerHarness {
				FakeTime = 0f,
				Paused = startPaused,
				Spoken = new List<(string, bool)>(),
				Tracker = new NotificationTracker(),
			};
			NotificationAnnouncer.TimeSource = () => h.FakeTime;
			NotificationAnnouncer.IsPaused = () => h.Paused;
			SpeechPipeline.TimeSource = () => h.FakeTime;
			SpeechPipeline.SpeakAction = (text, intr) => h.Spoken.Add((text, intr));
			SpeechPipeline.Reset();
			h.Announcer = new NotificationAnnouncer(h.Tracker);
			return h;
		}

		private static void CleanupAnnouncer(AnnouncerHarness h) {
			h.Announcer.Detach();
			NotificationAnnouncer.TimeSource = () => 0f;
			NotificationAnnouncer.IsPaused = () => false;
		}

		private static (string, bool, string) AnnouncerLoadPhaseHoldsUntilUnpause() {
			var h = SetupAnnouncer(startPaused: true);
			AddNotification(h.Tracker, "Stress");
			h.FakeTime = 5f;
			h.Announcer.Tick();
			h.Announcer.Tick();
			bool held = h.Spoken.Count == 0;

			// Unpause → load phase ends, batch starts
			h.Paused = false;
			h.Announcer.Tick(); // exits load phase, sets batch pending
			bool stillHeld = h.Spoken.Count == 0; // hasn't flushed yet (window not elapsed)

			h.FakeTime = 6.1f;
			h.Announcer.Tick(); // first-flush window (1.0s) elapsed
			bool spoke = h.Spoken.Count == 1 && h.Spoken[0].text == "Stress";

			CleanupAnnouncer(h);
			bool ok = held && stillHeld && spoke;
			return Assert("AnnouncerLoadPhaseHoldsUntilUnpause", ok,
				$"held={held}, stillHeld={stillHeld}, spoke={spoke}, count={h.Spoken.Count}");
		}

		private static (string, bool, string) AnnouncerFirstFlushUsesLongWindow() {
			var h = SetupAnnouncer(startPaused: false);
			AddNotification(h.Tracker, "Stress");
			h.Announcer.Tick(); // exits load phase, resets batch start to current time (0)
			bool noSpeechYet = h.Spoken.Count == 0;

			// At 0.5s — within 1.0s first-flush window
			h.FakeTime = 0.5f;
			h.Announcer.Tick();
			bool stillWaiting = h.Spoken.Count == 0;

			// At 1.1s — past 1.0s first-flush window
			h.FakeTime = 1.1f;
			h.Announcer.Tick();
			bool spoke = h.Spoken.Count == 1;

			CleanupAnnouncer(h);
			bool ok = noSpeechYet && stillWaiting && spoke;
			return Assert("AnnouncerFirstFlushUsesLongWindow", ok,
				$"noSpeech={noSpeechYet}, stillWaiting={stillWaiting}, spoke={spoke}");
		}

		private static (string, bool, string) AnnouncerSubsequentFlushUsesShortWindow() {
			var h = SetupAnnouncer(startPaused: false);
			AddNotification(h.Tracker, "Stress");
			h.Announcer.Tick(); // exit load phase
			h.FakeTime = 1.1f;
			h.Announcer.Tick(); // first flush (1.0s window)
			h.Spoken.Clear();
			SpeechPipeline.Reset();

			// Add new notification — subsequent flush should use 0.2s window
			h.FakeTime = 2.0f;
			AddNotification(h.Tracker, "Hunger");
			h.FakeTime = 2.1f;
			h.Announcer.Tick();
			bool tooEarly = h.Spoken.Count == 0; // 0.1s < 0.2s window

			h.FakeTime = 2.3f;
			h.Announcer.Tick();
			bool spoke = h.Spoken.Count > 0;

			CleanupAnnouncer(h);
			bool ok = tooEarly && spoke;
			return Assert("AnnouncerSubsequentFlushUsesShortWindow", ok,
				$"tooEarly={tooEarly}, spoke={spoke}, count={h.Spoken.Count}");
		}

		private static (string, bool, string) AnnouncerCountDeltaOnlyAnnouncesIncreases() {
			var h = SetupAnnouncer(startPaused: false);
			AddNotification(h.Tracker, "Stress");
			AddNotification(h.Tracker, "Stress");
			h.Announcer.Tick(); // exit load phase
			h.FakeTime = 1.1f;
			h.Announcer.Tick(); // first flush → speaks "Stress x2", _knownCounts["Stress"]=2

			string format = (string)STRINGS.ONIACCESS.NOTIFICATIONS.GROUP_COUNT;
			string expectedX2 = string.Format(format, "Stress", 2);
			bool spokeIncrease = h.Spoken.Count == 1 && h.Spoken[0].text == expectedX2;

			// Remove one "Stress" (count 2→1) and add "Hunger" to trigger a flush.
			// The flush must skip "Stress" (count 1 <= knownCount 2) and only
			// announce "Hunger". This exercises the count-delta skip on line 87.
			h.Spoken.Clear();
			SpeechPipeline.Reset();
			h.FakeTime = 2.0f;
			RemoveFirstNotification(h.Tracker, "Stress");
			AddNotification(h.Tracker, "Hunger");
			h.FakeTime = 2.3f;
			h.Announcer.Tick();

			bool hungerOnly = h.Spoken.Count == 1 && h.Spoken[0].text == "Hunger";

			CleanupAnnouncer(h);
			bool ok = spokeIncrease && hungerOnly;
			return Assert("AnnouncerCountDeltaOnlyAnnouncesIncreases", ok,
				$"spokeIncrease={spokeIncrease}, hungerOnly={hungerOnly}" +
				$", spoken=[{string.Join(", ", h.Spoken.ConvertAll(s => s.text))}]");
		}

		private static (string, bool, string) AnnouncerStaleKeyCleanupAllowsReannouncement() {
			var h = SetupAnnouncer(startPaused: false);
			AddNotification(h.Tracker, "Stress");
			h.Announcer.Tick(); // exit load phase
			h.FakeTime = 1.1f;
			h.Announcer.Tick(); // first flush → speaks "Stress", _knownCounts["Stress"]=1

			// Remove "Stress" and add a different notification to trigger a flush
			// while "Stress" is absent — this prunes the stale key.
			h.FakeTime = 2.0f;
			RemoveFirstNotification(h.Tracker, "Stress");
			AddNotification(h.Tracker, "Hunger"); // triggers OnChanged with HasNew
			h.FakeTime = 2.3f;
			h.Announcer.Tick(); // flushes: announces Hunger, prunes "Stress" from _knownCounts

			// Re-add "Stress" — should announce again since key was pruned
			h.Spoken.Clear();
			SpeechPipeline.Reset();
			h.FakeTime = 3.0f;
			AddNotification(h.Tracker, "Stress");
			h.FakeTime = 3.3f;
			h.Announcer.Tick();

			bool reannounced = false;
			for (int i = 0; i < h.Spoken.Count; i++) {
				if (h.Spoken[i].text == "Stress") { reannounced = true; break; }
			}

			CleanupAnnouncer(h);
			return Assert("AnnouncerStaleKeyCleanupAllowsReannouncement", reannounced,
				$"count={h.Spoken.Count}" +
				(h.Spoken.Count > 0 ? $", texts=[{string.Join(", ", h.Spoken.ConvertAll(s => s.text))}]" : ""));
		}

		private static (string, bool, string) AnnouncerFirstGroupInterruptsRestQueue() {
			var h = SetupAnnouncer(startPaused: false);
			AddNotification(h.Tracker, "Stress");
			AddNotification(h.Tracker, "Hunger");
			h.Announcer.Tick(); // exit load phase
			h.FakeTime = 1.1f;
			h.Announcer.Tick(); // first flush

			bool firstInterrupt = h.Spoken.Count >= 1 && h.Spoken[0].interrupt;
			bool secondQueued = h.Spoken.Count >= 2 && !h.Spoken[1].interrupt;
			bool twoTotal = h.Spoken.Count == 2;

			CleanupAnnouncer(h);
			bool ok = firstInterrupt && secondQueued && twoTotal;
			return Assert("AnnouncerFirstGroupInterruptsRestQueue", ok,
				$"count={h.Spoken.Count}, firstInterrupt={firstInterrupt}, secondQueued={secondQueued}");
		}

		private static (string, bool, string) AnnouncerBatchWindowResetsOnNewArrival() {
			var h = SetupAnnouncer(startPaused: false);
			h.Announcer.Tick(); // exit load phase
			h.FakeTime = 1.1f;
			h.Announcer.Tick(); // flush empty first batch
			h.Spoken.Clear();
			SpeechPipeline.Reset();

			// Add notification at t=2.0
			h.FakeTime = 2.0f;
			AddNotification(h.Tracker, "Stress");

			// Add another at t=2.15 — resets the batch timer
			h.FakeTime = 2.15f;
			AddNotification(h.Tracker, "Hunger");

			// At t=2.25: only 0.1s since last arrival (2.15), within 0.2s window
			h.FakeTime = 2.25f;
			h.Announcer.Tick();
			bool tooEarly = h.Spoken.Count == 0;

			// At t=2.4: 0.25s since last arrival (2.15), past 0.2s window
			h.FakeTime = 2.4f;
			h.Announcer.Tick();
			bool spoke = h.Spoken.Count == 2;

			CleanupAnnouncer(h);
			bool ok = tooEarly && spoke;
			return Assert("AnnouncerBatchWindowResetsOnNewArrival", ok,
				$"tooEarly={tooEarly}, spoke={spoke}, count={h.Spoken.Count}");
		}

		// ========================================
		// GridUtil.ValidateCluster
		// ========================================

		private static (string, bool, string) ValidateClusterAllPrunedReturnsFalse() {
			SetupGrid(100);
			var entry = new ScanEntry { Cell = 505 };
			var cells = new List<int> { 10, 20, 30 };
			bool result = GridUtil.ValidateCluster(cells, 505, entry, cell => false);
			bool ok = !result && cells.Count == 0 && entry.Cell == 505;
			return Assert("ValidateClusterAllPrunedReturnsFalse", ok,
				$"result={result}, cells={cells.Count}, entry.Cell={entry.Cell}");
		}

		private static (string, bool, string) ValidateClusterClosestCellSelected() {
			SetupGrid(100);
			// cursor at row 5 col 5 (cell 505)
			// cells at: row 5 col 15 (dist 10), row 5 col 10 (dist 5), row 6 col 20 (dist 16)
			int cursor = 505;
			var cells = new List<int> { 515, 510, 620 };
			var entry = new ScanEntry { Cell = 0 };
			bool result = GridUtil.ValidateCluster(cells, cursor, entry, cell => true);
			bool ok = result && entry.Cell == 510;
			return Assert("ValidateClusterClosestCellSelected", ok,
				$"result={result}, entry.Cell={entry.Cell}");
		}

		private static (string, bool, string) ValidateClusterStaleCellsRemoved() {
			SetupGrid(100);
			int cursor = 505;
			// Keep 510 (dist 5) and 520 (dist 15), prune 515
			var cells = new List<int> { 510, 515, 520 };
			var entry = new ScanEntry { Cell = 0 };
			bool result = GridUtil.ValidateCluster(cells, cursor, entry,
				cell => cell != 515);
			bool ok = result && entry.Cell == 510 && cells.Count == 2
				&& !cells.Contains(515);
			return Assert("ValidateClusterStaleCellsRemoved", ok,
				$"result={result}, entry.Cell={entry.Cell}, cells={cells.Count}");
		}

		private static (string, bool, string) ValidateClusterSingleSurvivor() {
			SetupGrid(100);
			int cursor = 505;
			// Only cell 900 survives (far away but only option)
			var cells = new List<int> { 510, 520, 900 };
			var entry = new ScanEntry { Cell = 0 };
			bool result = GridUtil.ValidateCluster(cells, cursor, entry,
				cell => cell == 900);
			bool ok = result && entry.Cell == 900 && cells.Count == 1;
			return Assert("ValidateClusterSingleSurvivor", ok,
				$"result={result}, entry.Cell={entry.Cell}, cells={cells.Count}");
		}

		// ========================================
		// CursorBookmarks.DigitKeyToIndex
		// ========================================

		private static (string, bool, string) DigitKeyAlpha1Through9() {
			var failures = new List<string>();
			for (int i = 0; i < 9; i++) {
				KeyCode key = KeyCode.Alpha1 + i;
				int result = CursorBookmarks.DigitKeyToIndex(key);
				if (result != i)
					failures.Add($"Alpha{i + 1}→{result} (expected {i})");
			}
			bool ok = failures.Count == 0;
			return Assert("DigitKeyAlpha1Through9", ok,
				ok ? "all correct" : string.Join("; ", failures));
		}

		private static (string, bool, string) DigitKeyAlpha0MapsToNine() {
			int result = CursorBookmarks.DigitKeyToIndex(KeyCode.Alpha0);
			bool ok = result == 9;
			return Assert("DigitKeyAlpha0MapsToNine", ok, $"got {result}");
		}

		private static (string, bool, string) DigitKeyKeypad1Through9() {
			var failures = new List<string>();
			for (int i = 0; i < 9; i++) {
				KeyCode key = KeyCode.Keypad1 + i;
				int result = CursorBookmarks.DigitKeyToIndex(key);
				if (result != i)
					failures.Add($"Keypad{i + 1}→{result} (expected {i})");
			}
			bool ok = failures.Count == 0;
			return Assert("DigitKeyKeypad1Through9", ok,
				ok ? "all correct" : string.Join("; ", failures));
		}

		private static (string, bool, string) DigitKeyKeypad0MapsToNine() {
			int result = CursorBookmarks.DigitKeyToIndex(KeyCode.Keypad0);
			bool ok = result == 9;
			return Assert("DigitKeyKeypad0MapsToNine", ok, $"got {result}");
		}

		private static (string, bool, string) DigitKeyNonDigitReturnsNegativeOne() {
			int result = CursorBookmarks.DigitKeyToIndex(KeyCode.A);
			bool ok = result == -1;
			return Assert("DigitKeyNonDigitReturnsNegativeOne", ok, $"got {result}");
		}

		// ========================================
		// LoadGate
		// ========================================

		private static void SetupLoadGate(float time, bool paused) {
			LoadGate.Reset();
			LoadGate.TimeSource = () => time;
			LoadGate.IsPaused = () => paused;
		}

		private static void CleanupLoadGate() {
			LoadGate.Reset();
			LoadGate.TimeSource = () => 0f;
			LoadGate.IsPaused = () => false;
		}

		private static (string, bool, string) LoadGateStartsNotReady() {
			SetupLoadGate(0f, true);
			bool ok = !LoadGate.IsReady;
			CleanupLoadGate();
			return Assert("LoadGateStartsNotReady", ok, $"IsReady={LoadGate.IsReady}");
		}

		private static (string, bool, string) LoadGateStaysGatedWhilePaused() {
			float time = 0f;
			LoadGate.Reset();
			LoadGate.TimeSource = () => time;
			LoadGate.IsPaused = () => true;
			time = 100f;
			LoadGate.Tick();
			LoadGate.Tick();
			bool ok = !LoadGate.IsReady;
			CleanupLoadGate();
			return Assert("LoadGateStaysGatedWhilePaused", ok, $"IsReady after paused ticks");
		}

		private static (string, bool, string) LoadGateNotReadyAtPointNineSeconds() {
			float time = 0f;
			bool paused = true;
			LoadGate.Reset();
			LoadGate.TimeSource = () => time;
			LoadGate.IsPaused = () => paused;
			// Unpause at time 10
			paused = false;
			time = 10f;
			LoadGate.Tick();
			// Advance to 10.9s (0.9s after unpause)
			time = 10.9f;
			LoadGate.Tick();
			bool ok = !LoadGate.IsReady;
			CleanupLoadGate();
			return Assert("LoadGateNotReadyAtPointNineSeconds", ok, $"IsReady={LoadGate.IsReady}");
		}

		private static (string, bool, string) LoadGateReadyAt1Second() {
			float time = 0f;
			bool paused = true;
			LoadGate.Reset();
			LoadGate.TimeSource = () => time;
			LoadGate.IsPaused = () => paused;
			// Unpause at time 10
			paused = false;
			time = 10f;
			LoadGate.Tick();
			// Advance to 11s (1s after unpause)
			time = 11f;
			LoadGate.Tick();
			bool ok = LoadGate.IsReady;
			CleanupLoadGate();
			return Assert("LoadGateReadyAt1Second", ok, $"IsReady={LoadGate.IsReady}");
		}

		private static (string, bool, string) LoadGateResetRequiresFullCycle() {
			float time = 0f;
			bool paused = false;
			LoadGate.Reset();
			LoadGate.TimeSource = () => time;
			LoadGate.IsPaused = () => paused;
			// Open the gate: first tick records unpause, second tick after 1s opens
			LoadGate.Tick();
			time = 1f;
			LoadGate.Tick();
			bool openedFirst = LoadGate.IsReady;
			// Reset and verify gated again
			LoadGate.Reset();
			bool gatedAfterReset = !LoadGate.IsReady;
			// Tick while paused -- should stay gated
			paused = true;
			LoadGate.Tick();
			bool stillGated = !LoadGate.IsReady;
			// Unpause and wait full 1s
			paused = false;
			time = 10f;
			LoadGate.Tick();
			time = 11f;
			LoadGate.Tick();
			bool reopened = LoadGate.IsReady;
			bool ok = openedFirst && gatedAfterReset && stillGated && reopened;
			CleanupLoadGate();
			return Assert("LoadGateResetRequiresFullCycle", ok,
				$"opened={openedFirst}, gated={gatedAfterReset}, still={stillGated}, reopened={reopened}");
		}

		private static (string, bool, string) LoadGateStartingUnpausedStillWaits() {
			float time = 0f;
			LoadGate.Reset();
			LoadGate.TimeSource = () => time;
			LoadGate.IsPaused = () => false;
			// First tick at time 0 records unpause
			LoadGate.Tick();
			bool notYet = !LoadGate.IsReady;
			// 0.9s later -- not ready
			time = 0.9f;
			LoadGate.Tick();
			bool still = !LoadGate.IsReady;
			// 1s -- ready
			time = 1f;
			LoadGate.Tick();
			bool ready = LoadGate.IsReady;
			bool ok = notYet && still && ready;
			CleanupLoadGate();
			return Assert("LoadGateStartingUnpausedStillWaits", ok,
				$"notYet={notYet}, still={still}, ready={ready}");
		}
		// ========================================
		// ClusterScanSnapshot tests
		// ========================================

		private static ClusterScanEntry MakeClusterEntry(
				int r, int q, string category, string itemName, int sortKey = 0) {
			return new ClusterScanEntry {
				Location = new AxialI(r, q),
				Category = category,
				ItemName = itemName,
				SortKey = sortKey,
			};
		}

		private static (string, bool, string) ClusterSnapshotCategoryOrder() {
			var entries = new List<ClusterScanEntry> {
				MakeClusterEntry(1, 0, ClusterMapTaxonomy.Categories.Meteors, "Comet"),
				MakeClusterEntry(2, 0, ClusterMapTaxonomy.Categories.Asteroids, "Swamp"),
				MakeClusterEntry(3, 0, ClusterMapTaxonomy.Categories.POIs, "Beacon"),
				MakeClusterEntry(4, 0, ClusterMapTaxonomy.Categories.Rockets, "Ship"),
			};
			var origin = new AxialI(0, 0);
			var snap = new ClusterScanSnapshot(entries, origin);

			// Index 0 = All, then Asteroids, Rockets, POIs, Meteors
			var names = new List<string>();
			for (int i = 0; i < snap.CategoryCount; i++)
				names.Add(snap.GetCategory(i).Name);

			bool ok = snap.CategoryCount == 5
				&& names[0] == ClusterMapTaxonomy.Categories.All
				&& names[1] == ClusterMapTaxonomy.Categories.Asteroids
				&& names[2] == ClusterMapTaxonomy.Categories.Rockets
				&& names[3] == ClusterMapTaxonomy.Categories.POIs
				&& names[4] == ClusterMapTaxonomy.Categories.Meteors;
			return Assert("ClusterSnapshotCategoryOrder", ok,
				$"got [{string.Join(", ", names)}]");
		}

		private static (string, bool, string) ClusterSnapshotAllSharesReferences() {
			var entries = new List<ClusterScanEntry> {
				MakeClusterEntry(1, 0, ClusterMapTaxonomy.Categories.Asteroids, "Swamp"),
				MakeClusterEntry(2, 0, ClusterMapTaxonomy.Categories.Asteroids, "Swamp"),
			};
			var origin = new AxialI(0, 0);
			var snap = new ClusterScanSnapshot(entries, origin);

			// "All" category is at index 0, "Asteroids" at index 1
			var allCat = snap.GetCategory(0);
			var namedCat = snap.GetCategory(1);
			bool sameRef = allCat.Items[0] == namedCat.Items[0];
			bool ok = sameRef && allCat.Items[0].Instances.Count == 2;
			return Assert("ClusterSnapshotAllSharesReferences", ok,
				$"sameRef={sameRef}, instances={allCat.Items[0].Instances.Count}");
		}

		private static (string, bool, string) ClusterSnapshotItemSortBySortKeyThenDistance() {
			var origin = new AxialI(0, 0);
			// "Far" has lower sort key (0) -> should come first
			// "Near" has higher sort key (5) -> should come second despite being closer
			var entries = new List<ClusterScanEntry> {
				MakeClusterEntry(5, 0, ClusterMapTaxonomy.Categories.Asteroids, "Near", sortKey: 5),
				MakeClusterEntry(10, 0, ClusterMapTaxonomy.Categories.Asteroids, "Far", sortKey: 0),
			};
			var snap = new ClusterScanSnapshot(entries, origin);
			var asteroids = snap.GetCategory(1); // index 0 is All
			bool sortKeyWins = asteroids.Items[0].ItemName == "Far"
				&& asteroids.Items[1].ItemName == "Near";

			// Same sort key, different distances: closer first
			var entries2 = new List<ClusterScanEntry> {
				MakeClusterEntry(10, 0, ClusterMapTaxonomy.Categories.Asteroids, "FarAst", sortKey: 1),
				MakeClusterEntry(1, 0, ClusterMapTaxonomy.Categories.Asteroids, "CloseAst", sortKey: 1),
			};
			var snap2 = new ClusterScanSnapshot(entries2, origin);
			var ast2 = snap2.GetCategory(1);
			bool distanceBreaksTie = ast2.Items[0].ItemName == "CloseAst"
				&& ast2.Items[1].ItemName == "FarAst";

			bool ok = sortKeyWins && distanceBreaksTie;
			return Assert("ClusterSnapshotItemSortBySortKeyThenDistance", ok,
				$"sortKeyWins={sortKeyWins}, distanceBreaksTie={distanceBreaksTie}");
		}

		private static (string, bool, string) ClusterSnapshotSkipAllCategory() {
			var entries = new List<ClusterScanEntry> {
				MakeClusterEntry(1, 0, ClusterMapTaxonomy.Categories.Asteroids, "Swamp"),
				MakeClusterEntry(2, 0, ClusterMapTaxonomy.Categories.Rockets, "Ship"),
			};
			var origin = new AxialI(0, 0);
			var snap = new ClusterScanSnapshot(entries, origin, skipAllCategory: true);

			bool noAll = true;
			for (int i = 0; i < snap.CategoryCount; i++) {
				if (snap.GetCategory(i).Name == ClusterMapTaxonomy.Categories.All)
					noAll = false;
			}
			bool ok = noAll && snap.CategoryCount == 2;
			return Assert("ClusterSnapshotSkipAllCategory", ok,
				$"noAll={noAll}, count={snap.CategoryCount}");
		}

		private static (string, bool, string) ClusterSnapshotRemovePrunesFromAllAndNamed() {
			var entries = new List<ClusterScanEntry> {
				MakeClusterEntry(1, 0, ClusterMapTaxonomy.Categories.Asteroids, "Swamp"),
				MakeClusterEntry(2, 0, ClusterMapTaxonomy.Categories.Asteroids, "Swamp"),
				MakeClusterEntry(3, 0, ClusterMapTaxonomy.Categories.Rockets, "Ship"),
			};
			var origin = new AxialI(0, 0);
			var snap = new ClusterScanSnapshot(entries, origin);

			// Remove one Swamp instance — item should survive with 1 instance
			var allCat = snap.GetCategory(0);
			var swampItem = allCat.Items[0].ItemName == "Swamp"
				? allCat.Items[0] : allCat.Items[1];
			var entryToRemove = swampItem.Instances[0];
			snap.RemoveInstance(swampItem, entryToRemove);

			bool itemSurvived = swampItem.Instances.Count == 1;
			// Both All and Asteroids should still have the Swamp item
			bool allHasSwamp = false;
			for (int i = 0; i < allCat.Items.Count; i++) {
				if (allCat.Items[i].ItemName == "Swamp") allHasSwamp = true;
			}
			bool ok = itemSurvived && allHasSwamp && snap.CategoryCount == 3;
			return Assert("ClusterSnapshotRemovePrunesFromAllAndNamed", ok,
				$"survived={itemSurvived}, allHasSwamp={allHasSwamp}, cats={snap.CategoryCount}");
		}

		private static (string, bool, string) ClusterSnapshotRemoveLastPrunesCategory() {
			var entries = new List<ClusterScanEntry> {
				MakeClusterEntry(1, 0, ClusterMapTaxonomy.Categories.Rockets, "Ship"),
			};
			var origin = new AxialI(0, 0);
			var snap = new ClusterScanSnapshot(entries, origin);

			// All(1 item) + Rockets(1 item) = 2 categories
			bool startedWith2 = snap.CategoryCount == 2;
			var allCat = snap.GetCategory(0);
			var shipItem = allCat.Items[0];
			snap.RemoveInstance(shipItem, shipItem.Instances[0]);

			// Both categories should be pruned
			bool ok = startedWith2 && snap.CategoryCount == 0;
			return Assert("ClusterSnapshotRemoveLastPrunesCategory", ok,
				$"startedWith2={startedWith2}, remaining={snap.CategoryCount}");
		}

		// ========================================
		// ClusterMapTaxonomy tests
		// ========================================

		private static (string, bool, string) ClusterTaxonomySortIndicesRoundTrip() {
			string[] expected = {
				ClusterMapTaxonomy.Categories.All,
				ClusterMapTaxonomy.Categories.Asteroids,
				ClusterMapTaxonomy.Categories.Rockets,
				ClusterMapTaxonomy.Categories.POIs,
				ClusterMapTaxonomy.Categories.Meteors,
				ClusterMapTaxonomy.Categories.Unknown,
			};
			var failures = new List<string>();
			for (int i = 0; i < expected.Length; i++) {
				int idx = ClusterMapTaxonomy.CategorySortIndex(expected[i]);
				if (idx != i)
					failures.Add($"{expected[i]}→{idx}, expected {i}");
			}
			// Unknown category name should return length (sort to end)
			int unknownIdx = ClusterMapTaxonomy.CategorySortIndex("BogusCategory");
			if (unknownIdx != expected.Length)
				failures.Add($"BogusCategory→{unknownIdx}, expected {expected.Length}");
			bool ok = failures.Count == 0;
			return Assert("ClusterTaxonomySortIndicesRoundTrip", ok,
				ok ? "all correct" : string.Join("; ", failures));
		}

		// ========================================
		// HexCoordinates tests
		// ========================================

		private static (string, bool, string) HexCompassAllOctants() {
			var origin = new AxialI(0, 0);
			// AxialToWorld: x = sqrt(3)*r + sqrt(3)/2*q, y = -1.5*q
			// Pure cardinal axes need r = -q/2 for north/south (x cancels to 0)
			var cases = new (AxialI target, string expected)[] {
				(new AxialI(2, -4), "north"),       // x=0, y=+6
				(new AxialI(4, -2), "northeast"),   // x=+5.2, y=+3
				(new AxialI(4, 0), "east"),          // x=+6.9, y=0
				(new AxialI(2, 4), "southeast"),     // x=+6.9, y=-6
				(new AxialI(-2, 4), "south"),        // x=0, y=-6
				(new AxialI(-4, 2), "southwest"),    // x=-5.2, y=-3
				(new AxialI(-4, 0), "west"),         // x=-6.9, y=0
				(new AxialI(-2, -4), "northwest"),   // x=-6.9, y=+6
			};
			var failures = new List<string>();
			foreach (var (target, expected) in cases) {
				string result = HexCoordinates.GetCompassDirection(origin, target);
				if (result != expected)
					failures.Add($"({target.r},{target.q})→\"{result}\", expected \"{expected}\"");
			}
			bool ok = failures.Count == 0;
			return Assert("HexCompassAllOctants", ok,
				ok ? "all correct" : string.Join("; ", failures));
		}

		private static (string, bool, string) HexFormatSameHexReturnsHere() {
			var loc = new AxialI(3, 5);
			string result = HexCoordinates.Format(loc, loc);
			string expected = (string)STRINGS.ONIACCESS.SCANNER.HERE;
			bool ok = result == expected;
			return Assert("HexFormatSameHexReturnsHere", ok,
				$"got \"{result}\", expected \"{expected}\"");
		}

		// ========================================
		// RectangleSelection
		// ========================================

		private static (string, bool, string) RectSelectionFirstCornerSet() {
			SetupGrid(100);
			var sel = new RectangleSelection();
			int cell = Grid.XYToCell(5, 5);
			var result = sel.SetCorner(cell, out _);
			bool ok = result == RectangleSelection.SetCornerResult.FirstCornerSet
				&& sel.PendingFirstCorner == cell
				&& sel.RectangleCount == 0;
			return Assert("RectSelectionFirstCornerSet", ok,
				$"result={result}, pending={sel.PendingFirstCorner}, rects={sel.RectangleCount}");
		}

		private static (string, bool, string) RectSelectionSecondCornerCompletesRect() {
			SetupGrid(100);
			var sel = new RectangleSelection();
			int c1 = Grid.XYToCell(2, 3);
			int c2 = Grid.XYToCell(5, 7);
			sel.SetCorner(c1, out _);
			var result = sel.SetCorner(c2, out var rect);
			bool ok = result == RectangleSelection.SetCornerResult.RectangleComplete
				&& sel.PendingFirstCorner == Grid.InvalidCell
				&& sel.RectangleCount == 1
				&& rect.Cell1 == c1 && rect.Cell2 == c2;
			return Assert("RectSelectionSecondCornerCompletesRect", ok,
				$"result={result}, pending={sel.PendingFirstCorner}, rects={sel.RectangleCount}");
		}

		private static (string, bool, string) RectSelectionClearRectRemovesContainingRect() {
			SetupGrid(100);
			var sel = new RectangleSelection();
			sel.AddRectangle(Grid.XYToCell(0, 0), Grid.XYToCell(4, 4));
			int inside = Grid.XYToCell(2, 2);
			bool removed = sel.ClearRectAtCursor(inside);
			bool ok = removed && sel.RectangleCount == 0;
			return Assert("RectSelectionClearRectRemovesContainingRect", ok,
				$"removed={removed}, rects={sel.RectangleCount}");
		}

		private static (string, bool, string) RectSelectionClearRectReturnsFalseWhenEmpty() {
			SetupGrid(100);
			var sel = new RectangleSelection();
			bool removed = sel.ClearRectAtCursor(Grid.XYToCell(5, 5));
			bool ok = !removed;
			return Assert("RectSelectionClearRectReturnsFalseWhenEmpty", ok,
				$"removed={removed}");
		}

		private static (string, bool, string) RectSelectionMultiRectAccumulation() {
			SetupGrid(100);
			var sel = new RectangleSelection();
			sel.AddRectangle(Grid.XYToCell(0, 0), Grid.XYToCell(2, 2));
			sel.AddRectangle(Grid.XYToCell(5, 5), Grid.XYToCell(7, 7));
			bool ok = sel.RectangleCount == 2;
			return Assert("RectSelectionMultiRectAccumulation", ok,
				$"rects={sel.RectangleCount}");
		}

		private static (string, bool, string) RectSelectionIsCellSelectedInRect() {
			SetupGrid(100);
			var sel = new RectangleSelection();
			sel.AddRectangle(Grid.XYToCell(1, 1), Grid.XYToCell(3, 3));
			bool ok = sel.IsCellSelected(Grid.XYToCell(2, 2))
				&& sel.IsCellSelected(Grid.XYToCell(1, 1))
				&& sel.IsCellSelected(Grid.XYToCell(3, 3));
			return Assert("RectSelectionIsCellSelectedInRect", ok, "cell not selected");
		}

		private static (string, bool, string) RectSelectionIsCellSelectedOutsideRect() {
			SetupGrid(100);
			var sel = new RectangleSelection();
			sel.AddRectangle(Grid.XYToCell(1, 1), Grid.XYToCell(3, 3));
			bool ok = !sel.IsCellSelected(Grid.XYToCell(0, 0))
				&& !sel.IsCellSelected(Grid.XYToCell(4, 4));
			return Assert("RectSelectionIsCellSelectedOutsideRect", ok, "cell was selected");
		}

		private static (string, bool, string) RectSelectionClearAllResetsState() {
			SetupGrid(100);
			var sel = new RectangleSelection();
			sel.SetCorner(Grid.XYToCell(1, 1), out _);
			sel.AddRectangle(Grid.XYToCell(0, 0), Grid.XYToCell(2, 2));
			sel.ClearAll();
			bool ok = sel.RectangleCount == 0
				&& sel.PendingFirstCorner == Grid.InvalidCell
				&& !sel.HasSelection;
			return Assert("RectSelectionClearAllResetsState", ok,
				$"rects={sel.RectangleCount}, pending={sel.PendingFirstCorner}");
		}

		private static (string, bool, string) RectSelectionAutoSelectSingle() {
			SetupGrid(100);
			var sel = new RectangleSelection();
			int cell = Grid.XYToCell(5, 5);
			sel.AutoSelectSingle(cell);
			bool ok = sel.RectangleCount == 1 && sel.IsCellSelected(cell);
			return Assert("RectSelectionAutoSelectSingle", ok,
				$"rects={sel.RectangleCount}");
		}

		private static (string, bool, string) RectSelectionAddRectangleDirect() {
			SetupGrid(100);
			var sel = new RectangleSelection();
			// Set a pending corner first, AddRectangle should clear it
			sel.SetCorner(Grid.XYToCell(9, 9), out _);
			sel.AddRectangle(Grid.XYToCell(0, 0), Grid.XYToCell(1, 1));
			bool ok = sel.PendingFirstCorner == Grid.InvalidCell
				&& sel.RectangleCount == 1;
			return Assert("RectSelectionAddRectangleDirect", ok,
				$"pending={sel.PendingFirstCorner}, rects={sel.RectangleCount}");
		}

		private static (string, bool, string) RectSelectionComputeArea() {
			SetupGrid(100);
			int c1 = Grid.XYToCell(2, 3);
			int c2 = Grid.XYToCell(5, 6);
			int area = RectangleSelection.ComputeArea(c1, c2);
			// 4 wide x 4 tall = 16
			bool ok = area == 16;
			return Assert("RectSelectionComputeArea", ok, $"area={area}");
		}

		private static (string, bool, string) RectSelectionTileCountBetween() {
			SetupGrid(100);
			int c1 = Grid.XYToCell(2, 3);
			int c2 = Grid.XYToCell(5, 6);
			int count = RectangleSelection.TileCountBetween(c1, c2);
			// 4 wide + 4 tall - 1 = 7
			bool ok = count == 7;
			return Assert("RectSelectionTileCountBetween", ok, $"count={count}");
		}

		// ========================================
		// HexPathfinder.FormatResult tests
		// ========================================

		private static (string, bool, string) FormatResultNoPath() {
			var r = new HexPathfinder.PathResult();
			string result = HexPathfinder.FormatResult(r);
			string expected = (string)STRINGS.ONIACCESS.CLUSTER_MAP.NO_PATH;
			bool ok = result == expected;
			return Assert("FormatResultNoPath", ok,
				$"got \"{result}\", expected \"{expected}\"");
		}

		private static (string, bool, string) FormatResultVisibleOnly() {
			var r = new HexPathfinder.PathResult {
				HasVisiblePath = true,
				VisiblePathLength = 5
			};
			string result = HexPathfinder.FormatResult(r);
			string expected = string.Format(
				(string)STRINGS.ONIACCESS.CLUSTER_MAP.PATH_RESULT, 5);
			bool ok = result == expected;
			return Assert("FormatResultVisibleOnly", ok,
				$"got \"{result}\", expected \"{expected}\"");
		}

		private static (string, bool, string) FormatResultFogOnly() {
			var r = new HexPathfinder.PathResult {
				HasFogPath = true,
				FogPathLength = 7,
				FogCellCount = 3
			};
			string result = HexPathfinder.FormatResult(r);
			string expected = string.Format(
				(string)STRINGS.ONIACCESS.CLUSTER_MAP.PATH_THROUGH_FOG, 7, 3);
			bool ok = result == expected;
			return Assert("FormatResultFogOnly", ok,
				$"got \"{result}\", expected \"{expected}\"");
		}

		private static (string, bool, string) FormatResultBothFogShorter() {
			var r = new HexPathfinder.PathResult {
				HasVisiblePath = true,
				VisiblePathLength = 10,
				HasFogPath = true,
				FogPathLength = 6,
				FogCellCount = 2
			};
			string result = HexPathfinder.FormatResult(r);
			string expected = string.Format(
				(string)STRINGS.ONIACCESS.CLUSTER_MAP.PATH_FOG_WITH_ALT,
				6, 2, 10);
			bool ok = result == expected;
			return Assert("FormatResultBothFogShorter", ok,
				$"got \"{result}\", expected \"{expected}\"");
		}

		private static (string, bool, string) FormatResultBothVisibleShorter() {
			var r = new HexPathfinder.PathResult {
				HasVisiblePath = true,
				VisiblePathLength = 4,
				HasFogPath = true,
				FogPathLength = 8,
				FogCellCount = 3
			};
			string result = HexPathfinder.FormatResult(r);
			string expected = string.Format(
				(string)STRINGS.ONIACCESS.CLUSTER_MAP.PATH_RESULT, 4);
			bool ok = result == expected;
			return Assert("FormatResultBothVisibleShorter", ok,
				$"got \"{result}\", expected \"{expected}\"");
		}

		// ========================================
		// StatusFilter.ShouldSpeak tests
		// ========================================
		// Isolated in a nested class so HashedString/StatusItem types are
		// only loaded after Main() sets up the assembly resolver.

		private static class StatusFilterTests {
			private static readonly HashedString PowerOverlay =
				new HashedString("Power");
			private static readonly HashedString GasOverlay =
				new HashedString("GasConduit");
			private static readonly HashedString CropOverlay =
				new HashedString("Crop");
			private static readonly HashedString DefaultOverlay =
				HashedString.Invalid;

			public static void Initialize() {
				var flags = BindingFlags.NonPublic | BindingFlags.Static;
				var type = typeof(StatusFilter);

				var overlayItems =
						new Dictionary<HashedString, HashSet<string>> {
					{ PowerOverlay, new HashSet<string> {
						"NeedPower", "NotEnoughPower" } },
					{ GasOverlay, new HashSet<string> {
						"NeedGasIn", "NeedGasOut" } },
					{ CropOverlay, new HashSet<string> {
						"NeedPlant", "NeedSeed" } },
				};
				type.GetField("overlayItems", flags)
					.SetValue(null, overlayItems);

				var allOverlay = new HashSet<string>();
				foreach (var set in overlayItems.Values)
					foreach (var id in set)
						allOverlay.Add(id);
				type.GetField("allOverlayItems", flags)
					.SetValue(null, allOverlay);

				type.GetField("alwaysNeutrals", flags).SetValue(null,
					new HashSet<string> {
						"UnderConstruction", "BuildingDisabled" });

				type.GetField("cropOverlay", flags)
					.SetValue(null, CropOverlay);
			}

			private static StatusItem MakeStatusItem(string id,
					NotificationType severity) {
				var item = (StatusItem)FormatterServices
					.GetUninitializedObject(typeof(StatusItem));
				item.Id = id;
				item.notificationType = severity;
				return item;
			}

			public static (string, bool, string) OverlayMatchingItem() {
				var item = MakeStatusItem("NeedPower",
					NotificationType.Bad);
				bool result = StatusFilter.ShouldSpeak(item,
					PowerOverlay, isPlant: false);
				return Assert("ShouldSpeakOverlayMatchingItem", result,
					"expected true for power item in power overlay");
			}

			public static (string, bool, string) OverlayNonMatchingItem() {
				var item = MakeStatusItem("NeedPower",
					NotificationType.Bad);
				bool result = StatusFilter.ShouldSpeak(item,
					GasOverlay, isPlant: false);
				return Assert("ShouldSpeakOverlayNonMatchingItem",
					!result,
					"expected false for power item in gas overlay");
			}

			public static (string, bool, string) FarmingNeutralPlant() {
				var item = MakeStatusItem("SomePlantStatus",
					NotificationType.Neutral);
				bool result = StatusFilter.ShouldSpeak(item,
					CropOverlay, isPlant: true);
				return Assert("ShouldSpeakFarmingNeutralPlant", result,
					"expected true for neutral plant in farming overlay");
			}

			public static (string, bool, string) FarmingNeutralNonPlant() {
				var item = MakeStatusItem("SomeBuildingStatus",
					NotificationType.Neutral);
				bool result = StatusFilter.ShouldSpeak(item,
					CropOverlay, isPlant: false);
				return Assert("ShouldSpeakFarmingNeutralNonPlant",
					!result,
					"expected false for neutral non-plant in farming overlay");
			}

			public static (string, bool, string) DefaultAlwaysNeutral() {
				var item = MakeStatusItem("UnderConstruction",
					NotificationType.Neutral);
				bool result = StatusFilter.ShouldSpeak(item,
					DefaultOverlay, isPlant: false);
				return Assert("ShouldSpeakDefaultAlwaysNeutral", result,
					"expected true for always-neutral in default view");
			}

			public static (string, bool, string) DefaultOtherNeutralSuppressed() {
				var item = MakeStatusItem("SomeRandomNeutral",
					NotificationType.Neutral);
				bool result = StatusFilter.ShouldSpeak(item,
					DefaultOverlay, isPlant: false);
				return Assert(
					"ShouldSpeakDefaultOtherNeutralSuppressed",
					!result,
					"expected false for unlisted neutral in default view");
			}

			public static (string, bool, string) DefaultBadNotOverlay() {
				var item = MakeStatusItem("EntombedItem",
					NotificationType.Bad);
				bool result = StatusFilter.ShouldSpeak(item,
					DefaultOverlay, isPlant: false);
				return Assert("ShouldSpeakDefaultBadNotOverlay", result,
					"expected true for bad item not claimed by any overlay");
			}

			public static (string, bool, string) DefaultBadClaimedByOverlay() {
				var item = MakeStatusItem("NeedPower",
					NotificationType.Bad);
				bool result = StatusFilter.ShouldSpeak(item,
					DefaultOverlay, isPlant: false);
				return Assert(
					"ShouldSpeakDefaultBadClaimedByOverlay", !result,
					"expected false for bad item claimed by power overlay");
			}
		}

		// ========================================
		// SectionMerger tests
		// ========================================

		static DetailSection MakeSection(string key, params (string key, string label)[] items) {
			var s = new DetailSection { Key = key, Header = key };
			foreach (var (k, l) in items)
				s.Items.Add(new LabelWidget { Key = k, Label = l });
			return s;
		}

		static string ItemKeys(List<DetailSection> sections) {
			var sb = new System.Text.StringBuilder();
			foreach (var s in sections) {
				if (sb.Length > 0) sb.Append("|");
				sb.Append(s.Key ?? s.Header).Append(":");
				for (int i = 0; i < s.Items.Count; i++) {
					if (i > 0) sb.Append(",");
					sb.Append(s.Items[i].Key ?? s.Items[i].Label);
				}
			}
			return sb.ToString();
		}

		static (string, bool, string) MergerMatchedItemsPreserveOrder() {
			var existing = new List<DetailSection> {
				MakeSection("s1", ("a", "A"), ("b", "B"), ("c", "C"))
			};
			var fresh = new List<DetailSection> {
				MakeSection("s1", ("a", "A2"), ("b", "B2"), ("c", "C2"))
			};
			SectionMerger.Merge(existing, fresh);
			bool labelsUpdated = existing[0].Items[0].Label == "A2"
				&& existing[0].Items[1].Label == "B2"
				&& existing[0].Items[2].Label == "C2";
			return Assert("MergerMatchedItemsPreserveOrder", labelsUpdated,
				$"labels not updated: {ItemKeys(existing)}");
		}

		static (string, bool, string) MergerItemRemoved() {
			var existing = new List<DetailSection> {
				MakeSection("s1", ("a", "A"), ("b", "B"), ("c", "C"))
			};
			var fresh = new List<DetailSection> {
				MakeSection("s1", ("a", "A"), ("c", "C"))
			};
			SectionMerger.Merge(existing, fresh);
			bool ok = existing[0].Items.Count == 2
				&& existing[0].Items[0].Key == "a"
				&& existing[0].Items[1].Key == "c";
			return Assert("MergerItemRemoved", ok,
				$"expected a,c got {ItemKeys(existing)}");
		}

		static (string, bool, string) MergerItemAdded() {
			var existing = new List<DetailSection> {
				MakeSection("s1", ("a", "A"), ("c", "C"))
			};
			var fresh = new List<DetailSection> {
				MakeSection("s1", ("a", "A"), ("b", "B"), ("c", "C"))
			};
			SectionMerger.Merge(existing, fresh);
			bool ok = existing[0].Items.Count == 3
				&& existing[0].Items[0].Key == "a"
				&& existing[0].Items[1].Key == "b"
				&& existing[0].Items[2].Key == "c";
			return Assert("MergerItemAdded", ok,
				$"expected a,b,c got {ItemKeys(existing)}");
		}

		static (string, bool, string) MergerSectionRemoved() {
			var existing = new List<DetailSection> {
				MakeSection("s1", ("a", "A")),
				MakeSection("s2", ("b", "B")),
				MakeSection("s3", ("c", "C"))
			};
			var fresh = new List<DetailSection> {
				MakeSection("s1", ("a", "A")),
				MakeSection("s3", ("c", "C"))
			};
			SectionMerger.Merge(existing, fresh);
			bool ok = existing.Count == 2
				&& existing[0].Key == "s1"
				&& existing[1].Key == "s3";
			return Assert("MergerSectionRemoved", ok,
				$"expected s1,s3 got {ItemKeys(existing)}");
		}

		static (string, bool, string) MergerSectionAdded() {
			var existing = new List<DetailSection> {
				MakeSection("s1", ("a", "A")),
				MakeSection("s3", ("c", "C"))
			};
			var fresh = new List<DetailSection> {
				MakeSection("s1", ("a", "A")),
				MakeSection("s2", ("b", "B")),
				MakeSection("s3", ("c", "C"))
			};
			SectionMerger.Merge(existing, fresh);
			bool ok = existing.Count == 3
				&& existing[0].Key == "s1"
				&& existing[1].Key == "s2"
				&& existing[2].Key == "s3";
			return Assert("MergerSectionAdded", ok,
				$"expected s1,s2,s3 got {ItemKeys(existing)}");
		}

		static (string, bool, string) MergerJitterPreservesOrder() {
			var existing = new List<DetailSection> {
				MakeSection("s1", ("a", "A"), ("b", "B"), ("c", "C"))
			};
			// Fresh has same items but reordered (jitter).
			var fresh = new List<DetailSection> {
				MakeSection("s1", ("c", "C2"), ("a", "A2"), ("b", "B2"))
			};
			SectionMerger.Merge(existing, fresh);
			bool orderPreserved = existing[0].Items[0].Key == "a"
				&& existing[0].Items[1].Key == "b"
				&& existing[0].Items[2].Key == "c";
			bool labelsUpdated = existing[0].Items[0].Label == "A2"
				&& existing[0].Items[1].Label == "B2"
				&& existing[0].Items[2].Label == "C2";
			return Assert("MergerJitterPreservesOrder",
				orderPreserved && labelsUpdated,
				$"order or labels wrong: {ItemKeys(existing)}");
		}

		static (string, bool, string) MergerChildrenMerged() {
			var parent = new LabelWidget {
				Key = "p",
				Label = "Parent",
				Children = new List<Widget> {
					new LabelWidget { Key = "c1", Label = "Child1" },
					new LabelWidget { Key = "c2", Label = "Child2" }
				}
			};
			var existing = new List<DetailSection> {
				new DetailSection { Key = "s1", Header = "S1",
					Items = { parent } }
			};

			var freshParent = new LabelWidget {
				Key = "p",
				Label = "ParentNew",
				Children = new List<Widget> {
					new LabelWidget { Key = "c2", Label = "Child2New" },
					new LabelWidget { Key = "c1", Label = "Child1New" }
				}
			};
			var fresh = new List<DetailSection> {
				new DetailSection { Key = "s1", Header = "S1",
					Items = { freshParent } }
			};

			SectionMerger.Merge(existing, fresh);

			var children = existing[0].Items[0].Children;
			bool ok = children != null && children.Count == 2
				&& children[0].Key == "c1" && children[0].Label == "Child1New"
				&& children[1].Key == "c2" && children[1].Label == "Child2New";
			return Assert("MergerChildrenMerged", ok,
				$"children wrong: c1={children?[0]?.Label}, c2={children?[1]?.Label}");
		}

		static (string, bool, string) MergerMissingKeyFallback() {
			var existing = new List<DetailSection> {
				new DetailSection { Key = "s1", Header = "S1",
					Items = { new LabelWidget { Label = "NoKey" } } }
			};
			var fresh = new List<DetailSection> {
				new DetailSection { Key = "s1", Header = "S1",
					Items = { new LabelWidget { Label = "NoKey" } } }
			};
			SectionMerger.Merge(existing, fresh);
			bool ok = existing.Count == 1 && existing[0].Items.Count == 1
				&& existing[0].Items[0].Label == "NoKey";
			return Assert("MergerMissingKeyFallback", ok,
				$"expected 1 item, got {existing[0].Items.Count}");
		}

		static (string, bool, string) MergerTypeMismatchReplaces() {
			var existing = new List<DetailSection> {
				new DetailSection { Key = "s1", Header = "S1",
					Items = { new LabelWidget { Key = "x", Label = "Old" } } }
			};
			var fresh = new List<DetailSection> {
				new DetailSection { Key = "s1", Header = "S1",
					Items = { new ButtonWidget { Key = "x", Label = "New" } } }
			};
			SectionMerger.Merge(existing, fresh);
			bool ok = existing[0].Items.Count == 1
				&& existing[0].Items[0] is ButtonWidget
				&& existing[0].Items[0].Label == "New";
			return Assert("MergerTypeMismatchReplaces", ok,
				$"expected ButtonWidget, got {existing[0].Items[0].GetType().Name}");
		}

		static (string, bool, string) MergerUpdateFromCopiesFields() {
			var oldW = new LabelWidget {
				Key = "k",
				Label = "Old",
				SuppressTooltip = false
			};
			var newW = new LabelWidget {
				Key = "k",
				Label = "New",
				SuppressTooltip = true
			};
			oldW.UpdateFrom(newW);
			bool ok = oldW.Label == "New" && oldW.SuppressTooltip;
			return Assert("MergerUpdateFromCopiesFields", ok,
				$"Label={oldW.Label}, SuppressTooltip={oldW.SuppressTooltip}");
		}

		// ========================================
		// FlowTracker tests
		// ========================================

		private static readonly BindingFlags FTFlags =
			BindingFlags.NonPublic | BindingFlags.Instance;

		/// <summary>
		/// Injects internal state into a FlowTracker for testing the
		/// read methods without needing ConduitFlow game objects.
		/// </summary>
		static FlowTracker MakeFlowTracker(int conduitCount, int samplesRecorded,
				int writePos, int[] buffer, SimHashes[] elementBuffer) {
			var tracker = new FlowTracker();
			var t = typeof(FlowTracker);
			t.GetField("_conduitCount", FTFlags).SetValue(tracker, conduitCount);
			t.GetField("_samplesRecorded", FTFlags).SetValue(tracker, samplesRecorded);
			t.GetField("_writePos", FTFlags).SetValue(tracker, writePos);
			t.GetField("_buffer", FTFlags).SetValue(tracker, buffer);
			t.GetField("_elementBuffer", FTFlags).SetValue(tracker, elementBuffer);
			return tracker;
		}

		static (string, bool, string) FlowTrackerGetDirectionCountsUninitialized() {
			var tracker = new FlowTracker();
			var counts = new int[5];
			int samples = tracker.GetDirectionCounts(0, counts);
			bool ok = samples == 0
				&& counts[0] == 0 && counts[1] == 0 && counts[2] == 0
				&& counts[3] == 0 && counts[4] == 0;
			return Assert("FlowTrackerGetDirectionCountsUninitialized", ok,
				$"samples={samples}");
		}

		static (string, bool, string) FlowTrackerGetDirectionCountsOutOfRange() {
			// 2 conduits, 1 sample — query index 5 (out of range)
			var buffer = new int[2 * FlowTracker.BufferSize];
			var elements = new SimHashes[2 * FlowTracker.BufferSize];
			var tracker = MakeFlowTracker(2, 1, 1, buffer, elements);
			var counts = new int[5];
			int samples = tracker.GetDirectionCounts(5, counts);
			bool ok = samples == 0;
			return Assert("FlowTrackerGetDirectionCountsOutOfRange", ok,
				$"samples={samples}, expected 0 for out-of-range index");
		}

		static (string, bool, string) FlowTrackerGetDirectionCountsSingleSample() {
			// 1 conduit, 1 sample at slot 0 with DirUp
			int conduits = 1;
			var buffer = new int[conduits * FlowTracker.BufferSize];
			var elements = new SimHashes[conduits * FlowTracker.BufferSize];
			buffer[0] = FlowTracker.DirUp;
			var tracker = MakeFlowTracker(conduits, 1, 1, buffer, elements);
			var counts = new int[5];
			int samples = tracker.GetDirectionCounts(0, counts);
			bool ok = samples == 1 && counts[FlowTracker.DirUp] == 1
				&& counts[FlowTracker.DirNone] == 0;
			return Assert("FlowTrackerGetDirectionCountsSingleSample", ok,
				$"samples={samples}, up={counts[FlowTracker.DirUp]}");
		}

		static (string, bool, string) FlowTrackerGetDirectionCountsPartialBuffer() {
			// 1 conduit, 5 samples (not yet wrapped). Slots 0-4 filled.
			// Pattern: Up, Down, Left, Right, Up
			int conduits = 1;
			var buffer = new int[conduits * FlowTracker.BufferSize];
			var elements = new SimHashes[conduits * FlowTracker.BufferSize];
			buffer[0] = FlowTracker.DirUp;
			buffer[1] = FlowTracker.DirDown;
			buffer[2] = FlowTracker.DirLeft;
			buffer[3] = FlowTracker.DirRight;
			buffer[4] = FlowTracker.DirUp;
			var tracker = MakeFlowTracker(conduits, 5, 5, buffer, elements);
			var counts = new int[5];
			int samples = tracker.GetDirectionCounts(0, counts);
			bool ok = samples == 5
				&& counts[FlowTracker.DirUp] == 2
				&& counts[FlowTracker.DirDown] == 1
				&& counts[FlowTracker.DirLeft] == 1
				&& counts[FlowTracker.DirRight] == 1;
			return Assert("FlowTrackerGetDirectionCountsPartialBuffer", ok,
				$"samples={samples}, up={counts[FlowTracker.DirUp]}, " +
				$"down={counts[FlowTracker.DirDown]}, " +
				$"left={counts[FlowTracker.DirLeft]}, " +
				$"right={counts[FlowTracker.DirRight]}");
		}

		static (string, bool, string) FlowTrackerGetDirectionCountsWrappedBuffer() {
			// 1 conduit, buffer fully wrapped (samplesRecorded > BufferSize).
			// writePos=3 means oldest slot is 3, newest is 2.
			// Fill all 20 slots: slots 0-2 = DirLeft, slots 3-19 = DirRight.
			// Reading order starts at slot 3, so we expect 17 Right + 3 Left.
			int conduits = 1;
			var buffer = new int[conduits * FlowTracker.BufferSize];
			var elements = new SimHashes[conduits * FlowTracker.BufferSize];
			for (int i = 0; i < FlowTracker.BufferSize; i++)
				buffer[i] = i < 3 ? FlowTracker.DirLeft : FlowTracker.DirRight;
			// samplesRecorded=25 (> BufferSize), writePos=3
			var tracker = MakeFlowTracker(conduits, 25, 3, buffer, elements);
			var counts = new int[5];
			int samples = tracker.GetDirectionCounts(0, counts);
			bool ok = samples == FlowTracker.BufferSize
				&& counts[FlowTracker.DirRight] == 17
				&& counts[FlowTracker.DirLeft] == 3;
			return Assert("FlowTrackerGetDirectionCountsWrappedBuffer", ok,
				$"samples={samples}, right={counts[FlowTracker.DirRight]}, " +
				$"left={counts[FlowTracker.DirLeft]}");
		}

		static (string, bool, string) FlowTrackerGetDirectionCountsMultiConduit() {
			// 3 conduits, 2 samples. Verify correct per-conduit indexing.
			// Slot layout: [slot * conduitCount + conduitIdx]
			// Slot 0: conduit0=Up, conduit1=Down, conduit2=Left
			// Slot 1: conduit0=Right, conduit1=Up, conduit2=Down
			int conduits = 3;
			var buffer = new int[conduits * FlowTracker.BufferSize];
			var elements = new SimHashes[conduits * FlowTracker.BufferSize];
			// Slot 0
			buffer[0 * conduits + 0] = FlowTracker.DirUp;
			buffer[0 * conduits + 1] = FlowTracker.DirDown;
			buffer[0 * conduits + 2] = FlowTracker.DirLeft;
			// Slot 1
			buffer[1 * conduits + 0] = FlowTracker.DirRight;
			buffer[1 * conduits + 1] = FlowTracker.DirUp;
			buffer[1 * conduits + 2] = FlowTracker.DirDown;
			var tracker = MakeFlowTracker(conduits, 2, 2, buffer, elements);

			var c0 = new int[5];
			tracker.GetDirectionCounts(0, c0);
			var c1 = new int[5];
			tracker.GetDirectionCounts(1, c1);
			var c2 = new int[5];
			tracker.GetDirectionCounts(2, c2);

			bool ok = c0[FlowTracker.DirUp] == 1 && c0[FlowTracker.DirRight] == 1
				&& c1[FlowTracker.DirDown] == 1 && c1[FlowTracker.DirUp] == 1
				&& c2[FlowTracker.DirLeft] == 1 && c2[FlowTracker.DirDown] == 1;
			return Assert("FlowTrackerGetDirectionCountsMultiConduit", ok,
				$"c0: up={c0[FlowTracker.DirUp]} right={c0[FlowTracker.DirRight]}, " +
				$"c1: down={c1[FlowTracker.DirDown]} up={c1[FlowTracker.DirUp]}, " +
				$"c2: left={c2[FlowTracker.DirLeft]} down={c2[FlowTracker.DirDown]}");
		}

		static (string, bool, string) FlowTrackerGetElementCountsSkipsDirNone() {
			// 1 conduit, 3 samples: DirNone, DirUp, DirNone.
			// GetElementDirectionCounts should only report the DirUp sample.
			int conduits = 1;
			var buffer = new int[conduits * FlowTracker.BufferSize];
			var elements = new SimHashes[conduits * FlowTracker.BufferSize];
			buffer[0] = FlowTracker.DirNone;
			elements[0] = SimHashes.Water;
			buffer[1] = FlowTracker.DirUp;
			elements[1] = SimHashes.Water;
			buffer[2] = FlowTracker.DirNone;
			elements[2] = SimHashes.Water;
			var tracker = MakeFlowTracker(conduits, 3, 3, buffer, elements);
			var counts = new Dictionary<SimHashes, int[]>();
			int samples = tracker.GetElementDirectionCounts(0, counts);
			bool hasWater = counts.TryGetValue(SimHashes.Water, out int[] dirs);
			bool ok = samples == 3 && hasWater
				&& dirs[FlowTracker.DirUp] == 1
				&& dirs[FlowTracker.DirNone] == 0;
			return Assert("FlowTrackerGetElementCountsSkipsDirNone", ok,
				$"samples={samples}, hasWater={hasWater}, " +
				$"up={dirs?[FlowTracker.DirUp]}, none={dirs?[FlowTracker.DirNone]}");
		}

		static (string, bool, string) FlowTrackerGetElementCountsGroupsByElement() {
			// 1 conduit, 4 samples with two different elements.
			int conduits = 1;
			var buffer = new int[conduits * FlowTracker.BufferSize];
			var elements = new SimHashes[conduits * FlowTracker.BufferSize];
			buffer[0] = FlowTracker.DirUp; elements[0] = SimHashes.Water;
			buffer[1] = FlowTracker.DirDown; elements[1] = SimHashes.Oxygen;
			buffer[2] = FlowTracker.DirUp; elements[2] = SimHashes.Water;
			buffer[3] = FlowTracker.DirLeft; elements[3] = SimHashes.Oxygen;
			var tracker = MakeFlowTracker(conduits, 4, 4, buffer, elements);
			var counts = new Dictionary<SimHashes, int[]>();
			int samples = tracker.GetElementDirectionCounts(0, counts);
			bool hasWater = counts.TryGetValue(SimHashes.Water, out int[] waterDirs);
			bool hasOxygen = counts.TryGetValue(SimHashes.Oxygen, out int[] oxygenDirs);
			bool ok = samples == 4 && counts.Count == 2
				&& hasWater && waterDirs[FlowTracker.DirUp] == 2
				&& hasOxygen && oxygenDirs[FlowTracker.DirDown] == 1
				&& oxygenDirs[FlowTracker.DirLeft] == 1;
			return Assert("FlowTrackerGetElementCountsGroupsByElement", ok,
				$"samples={samples}, elements={counts.Count}, " +
				$"water.up={waterDirs?[FlowTracker.DirUp]}, " +
				$"oxygen.down={oxygenDirs?[FlowTracker.DirDown]}, " +
				$"oxygen.left={oxygenDirs?[FlowTracker.DirLeft]}");
		}

		static (string, bool, string) FlowTrackerGetElementCountsWrapped() {
			// 1 conduit, buffer wrapped. Verify element counts use correct
			// start slot and read the full ring.
			int conduits = 1;
			var buffer = new int[conduits * FlowTracker.BufferSize];
			var elements = new SimHashes[conduits * FlowTracker.BufferSize];
			// Fill all slots with DirUp/Water, except slot 0 = DirDown/Oxygen
			for (int i = 0; i < FlowTracker.BufferSize; i++) {
				buffer[i] = FlowTracker.DirUp;
				elements[i] = SimHashes.Water;
			}
			buffer[0] = FlowTracker.DirDown;
			elements[0] = SimHashes.Oxygen;
			// writePos=1 means slot 1 is oldest, slot 0 is newest
			var tracker = MakeFlowTracker(conduits, 30, 1, buffer, elements);
			var counts = new Dictionary<SimHashes, int[]>();
			int samples = tracker.GetElementDirectionCounts(0, counts);
			bool hasWater = counts.TryGetValue(SimHashes.Water, out int[] waterDirs);
			bool hasOxygen = counts.TryGetValue(SimHashes.Oxygen, out int[] oxygenDirs);
			bool ok = samples == FlowTracker.BufferSize
				&& hasWater && waterDirs[FlowTracker.DirUp] == 19
				&& hasOxygen && oxygenDirs[FlowTracker.DirDown] == 1;
			return Assert("FlowTrackerGetElementCountsWrapped", ok,
				$"samples={samples}, water.up={waterDirs?[FlowTracker.DirUp]}, " +
				$"oxygen.down={oxygenDirs?[FlowTracker.DirDown]}");
		}

		static (string, bool, string) FlowTrackerClearResetsState() {
			int conduits = 1;
			var buffer = new int[conduits * FlowTracker.BufferSize];
			var elements = new SimHashes[conduits * FlowTracker.BufferSize];
			buffer[0] = FlowTracker.DirUp;
			var tracker = MakeFlowTracker(conduits, 1, 1, buffer, elements);
			tracker.Clear();
			var counts = new int[5];
			int samples = tracker.GetDirectionCounts(0, counts);
			bool ok = samples == 0;
			return Assert("FlowTrackerClearResetsState", ok,
				$"samples={samples} after Clear, expected 0");
		}

	}
}
