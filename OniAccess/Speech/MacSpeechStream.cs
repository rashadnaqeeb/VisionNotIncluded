using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using OniAccess.Util;

namespace OniAccess.Speech {
	/// <summary>
	/// A speech queue with no gaps, for the system voice on macOS.
	///
	/// AVSpeechSynthesizer's own queue starts and stops the audio output around
	/// every utterance, which costs about 120 ms of silence per line for the
	/// compact voices and about 250 ms for Eloquence (measured August 2026: four
	/// short lines took 1612 ms to play for 602 ms of speech); a screen reader
	/// user hears speech stopping and restarting on every line, and nothing on
	/// the utterance API changes it. So each message is rendered instead, with
	/// writeUtterance:toBufferCallback:, which delivers the same audio the voice
	/// would play in a fraction of its playing time with no silence in front and
	/// a few tens of milliseconds behind. The samples are trimmed, brought to
	/// one loudness (the voices render far below the game's mix, and each at its
	/// own level), faded in and out over a few milliseconds so a line that ends
	/// mid-waveform does not click, and scheduled back-to-back on an AVAudioPlayerNode of our
	/// own, in the voice's native
	/// format (Eloquence renders at 16 kHz, the compact voices at 22.05 kHz; the
	/// engine's mixer resamples to the output device). Our own AVAudioEngine,
	/// rather than the game's FMOD output, keeps speech clear of the game's audio
	/// and gives it a volume of its own.
	///
	/// One render runs at a time. AVSpeech delivers the buffers through an
	/// Objective-C block built by hand here, on the main dispatch queue: the
	/// Unity player's frames are fired by an NSTimer on the main run loop, and
	/// that loop drains the main queue between frames (sampled August 2026),
	/// so the callbacks run on the main thread between frames. The block writes
	/// into the <see cref="Render"/> in flight and <see cref="Update"/> collects
	/// the result once per frame and starts the next. A render cannot be
	/// aborted (stopSpeakingAtBoundary: does not touch a write), so when one
	/// must be abandoned, on an interrupt or because it stalled, its synthesizer
	/// and block are retired with it and a fresh synthesizer takes the next
	/// message at once; a retired set is reaped, its block reused, only after
	/// its render has delivered AVSpeech's end marker, so nothing a late
	/// callback may still touch is freed under it. Stopping the player node
	/// discards its scheduled buffers, which is the interrupt. Everything
	/// happens on the main thread, inside an autorelease pool of its own; the
	/// callback keeps its locking so it stays correct should a macOS release
	/// ever deliver off the main queue.
	///
	/// The first render in the process is slow: the voice's synthesizer
	/// extension has to start, which took 8 s for a Kona voice (Reed) while
	/// the game was loading on every core under Rosetta. Later renders of a
	/// short line take well under a second. The stall timeout allows for a
	/// cold start on a slower machine.
	/// </summary>
	public sealed class MacSpeechStream {
		const double InitialRate = 22050;       // the player's format until the first render says otherwise
		const float GapSeconds = 0.05f;         // silence between utterances, as a screen reader would pause
		const double StallTimeoutSeconds = 30;  // a render that delivers nothing for this long is abandoned; see the cold start note above
		const double SlowRenderSeconds = 2;     // a render slower than this is noted in the log

		// Resolved when the type is first touched, which MacSystemVoiceBackend
		// guarantees happens after ObjC.LoadSpeechFrameworks.
		private static readonly IntPtr ClassPcmBuffer = ObjC.Class("AVAudioPCMBuffer");
		private static readonly IntPtr SelFloatChannelData = ObjC.Sel("floatChannelData");
		private static readonly IntPtr SelFrameLength = ObjC.Sel("frameLength");
		private static readonly IntPtr SelSetFrameLength = ObjC.Sel("setFrameLength:");
		private static readonly IntPtr SelScheduleBuffer = ObjC.Sel("scheduleBuffer:completionHandler:");
		private static readonly IntPtr SelFormat = ObjC.Sel("format");
		private static readonly IntPtr SelSampleRate = ObjC.Sel("sampleRate");
		private static readonly IntPtr SelWrite = ObjC.Sel("writeUtterance:toBufferCallback:");
		private static readonly IntPtr SelUtteranceWithString = ObjC.Sel("speechUtteranceWithString:");
		private static readonly IntPtr SelSetVoice = ObjC.Sel("setVoice:");
		private static readonly IntPtr SelSetRate = ObjC.Sel("setRate:");
		private static readonly IntPtr SelVoiceWithIdentifier = ObjC.Sel("voiceWithIdentifier:");
		private static readonly IntPtr SelAlloc = ObjC.Sel("alloc");
		private static readonly IntPtr SelInitWithFormat = ObjC.Sel("initWithPCMFormat:frameCapacity:");
		private static readonly IntPtr SelIsRunning = ObjC.Sel("isRunning");
		private static readonly IntPtr SelStartAndReturnError = ObjC.Sel("startAndReturnError:");
		private static readonly IntPtr SelStop = ObjC.Sel("stop");
		private static readonly IntPtr SelPlay = ObjC.Sel("play");
		private static readonly IntPtr ClassUtterance = ObjC.Class("AVSpeechUtterance");
		private static readonly IntPtr ClassVoice = ObjC.Class("AVSpeechSynthesisVoice");

		private IntPtr _engine; // owned (+1)
		private IntPtr _player; // owned (+1)
		private IntPtr _format; // owned (+1): float32, mono, at _formatRate; the player's connection to the mixer
		private double _formatRate;
		private float[] _gap;   // GapSeconds of silence at _formatRate, appended to every line

		private IntPtr _synth;  // owned (+1): the AVSpeechSynthesizer rendering now, or Zero until the next render needs one; never speaks aloud
		private IntPtr _block;  // the callback block for the next render, or Zero; see MakeBlock
		private Render _inFlight;
		private readonly List<Retired> _retired = new List<Retired>();
		// Blocks free for reuse, oldest first. AVSpeech sends its end marker twice
		// (measured August 2026: two empty buffers, delivered together), so a block
		// is never handed to the render right after the one it served: the second
		// marker must find the old handle slot, not a new render's.
		private readonly Queue<IntPtr> _freeBlocks = new Queue<IntPtr>();
		const int MinFreeBlocksBeforeReuse = 2;
		private bool _broken;   // a renderer could not be created; see Enqueue

		private struct Retired {
			public IntPtr Synth;
			public IntPtr Block;
			public Render Render;
		}

		private struct Pending {
			public string Text;
			public string Voice;
			public float Rate;
		}

		private readonly Queue<Pending> _pending = new Queue<Pending>();
		private IntPtr _voice;          // owned (+1): the AVSpeechSynthesisVoice for _resolvedVoice, or Zero for AVSpeech's default
		private string _resolvedVoice;  // the identifier _voice was resolved from

		/// <summary>Throws when the audio engine or the first renderer cannot be set up; the caller logs and falls back.</summary>
		public MacSpeechStream() {
			IntPtr pool = ObjC.AutoreleasePoolPush();
			try {
				_engine = Alloc("AVAudioEngine", "init");
				_player = Alloc("AVAudioPlayerNode", "init");
				ObjC.Send(_engine, ObjC.Sel("attachNode:"), _player);
				Connect(InitialRate);
				if (!ObjC.SendBool(_engine, SelStartAndReturnError, IntPtr.Zero))
					throw new InvalidOperationException("AVAudioEngine failed to start");
				_synth = Alloc("AVSpeechSynthesizer", "init");
				_block = MakeBlock();
			} catch {
				Dispose();
				throw;
			} finally {
				ObjC.AutoreleasePoolPop(pool);
			}
		}

		private static IntPtr Alloc(string className, string initSelector) {
			IntPtr obj = ObjC.Send(ObjC.Send(ObjC.Class(className), SelAlloc), ObjC.Sel(initSelector));
			if (obj == IntPtr.Zero) throw new InvalidOperationException($"{className} {initSelector} returned nil");
			return obj;
		}

		/// <summary>Playback volume, 0 to 1.</summary>
		public void SetVolume(float volume) => ObjC.SendFloat(_player, SelSetVolume, volume);

		private static readonly IntPtr SelSetVolume = ObjC.Sel("setVolume:");

		/// <summary>
		/// Queue a message in <paramref name="voiceIdentifier"/> (null for
		/// AVSpeech's default) at <paramref name="rate"/> on AVSpeech's [0, 1]
		/// scale. False if the stream can no longer render, in which case the
		/// caller should drop it and speak another way.
		/// </summary>
		public bool Enqueue(string text, string voiceIdentifier, float rate) {
			if (_broken) return false;
			if (string.IsNullOrWhiteSpace(text)) return true;
			_pending.Enqueue(new Pending { Text = text, Voice = voiceIdentifier, Rate = rate });
			IntPtr pool = ObjC.AutoreleasePoolPush();
			try {
				StartNext();
			} finally {
				ObjC.AutoreleasePoolPop(pool);
			}
			return !_broken;
		}

		/// <summary>Drop everything queued, abandon the render in flight, and stop what is playing.</summary>
		public void Clear() {
			_pending.Clear();
			if (_inFlight != null) Retire();
			ObjC.Send(_player, SelStop);
		}

		/// <summary>Once per frame: reap retired renders, collect a finished one, and start the next.</summary>
		public void Update() {
			IntPtr pool = ObjC.AutoreleasePoolPush();
			try {
				Reap();
				Render render = _inFlight;
				if (render != null) {
					if (render.Done)
						Finish(render);
					else if (render.SecondsSinceProgress > StallTimeoutSeconds) {
						Log.Error($"MacSpeechStream: a render stalled{(render.Problem == null ? "" : $" ({render.Problem})")}; abandoning it");
						Retire();
					}
				}
				StartNext();
			} finally {
				ObjC.AutoreleasePoolPop(pool);
			}
		}

		public void Dispose() {
			IntPtr pool = ObjC.AutoreleasePoolPush();
			try {
				if (_player != IntPtr.Zero) Clear();
				try {
					if (_engine != IntPtr.Zero) ObjC.Send(_engine, SelStop);
				} catch (Exception ex) {
					Log.Warn($"MacSpeechStream: engine stop failed: {ex.Message}");
				}
				ObjC.Release(_player);
				ObjC.Release(_engine);
				ObjC.Release(_format);
				ObjC.Release(_voice);
				ObjC.Release(_synth);
				_player = _engine = _format = _voice = _synth = IntPtr.Zero;
				_block = IntPtr.Zero;
				_resolvedVoice = null;
				// Renders still in flight keep their synthesizer, block and handle:
				// their callbacks are still queued and run on a later pump. A few
				// hundred bytes each, only on shutdown while something was being
				// rendered. Blocks are never freed at all; the runtime reads a
				// global block's flags on release, so a freed one could be read
				// after its owner is gone.
				Reap();
				if (_retired.Count > 0)
					Log.Info($"MacSpeechStream: {_retired.Count} render(s) still in flight at dispose; left to finish on their own");
				_retired.Clear();
			} finally {
				ObjC.AutoreleasePoolPop(pool);
			}
		}

		// ---- rendering ----

		/// <summary>A synthesizer and block for the next render, made on demand; false, and the stream broken, if they cannot be.</summary>
		private bool EnsureRenderer() {
			if (_synth != IntPtr.Zero && _block != IntPtr.Zero) return true;
			try {
				if (_synth == IntPtr.Zero) _synth = Alloc("AVSpeechSynthesizer", "init");
				if (_block == IntPtr.Zero) _block = _freeBlocks.Count >= MinFreeBlocksBeforeReuse ? _freeBlocks.Dequeue() : MakeBlock();
				return true;
			} catch (Exception ex) {
				Log.Error($"MacSpeechStream: no renderer; the stream is unusable from here on: {ex.Message}");
				ObjC.Release(_synth);
				_synth = IntPtr.Zero;
				_broken = true;
				return false;
			}
		}

		private void StartNext() {
			if (_inFlight != null || _pending.Count == 0 || !EnsureRenderer()) return;
			Pending next = _pending.Dequeue();
			IntPtr utterance = MakeUtterance(next.Text, ResolveVoice(next.Voice), next.Rate);
			if (utterance == IntPtr.Zero) {
				Log.Error("MacSpeechStream: AVSpeechUtterance returned nil; dropping the message");
				return;
			}
			var render = new Render();
			Marshal.WriteIntPtr(_block, BlockHandleOffset, GCHandle.ToIntPtr(render.Handle));
			_inFlight = render;
			ObjC.Send(_synth, SelWrite, utterance, _block);
		}

		/// <summary>An autoreleased AVSpeechUtterance for <paramref name="text"/> on <paramref name="voice"/> (Zero for AVSpeech's default) at <paramref name="rate"/>, or Zero.</summary>
		internal static IntPtr MakeUtterance(string text, IntPtr voice, float rate) {
			IntPtr utterance = ObjC.Send(ClassUtterance, SelUtteranceWithString, ObjC.NSString(text));
			if (utterance == IntPtr.Zero) return IntPtr.Zero;
			if (voice != IntPtr.Zero) ObjC.Send(utterance, SelSetVoice, voice);
			ObjC.SendFloat(utterance, SelSetRate, rate);
			return utterance;
		}

		/// <summary>The autoreleased AVSpeechSynthesisVoice for an identifier, or Zero if AVSpeech has no such voice.</summary>
		internal static IntPtr LookupVoice(string identifier) =>
			ObjC.Send(ClassVoice, SelVoiceWithIdentifier, ObjC.NSString(identifier));

		/// <summary>The voice object for an identifier, retained and kept for the identifier last used; Zero for null or for a voice AVSpeech does not have.</summary>
		private IntPtr ResolveVoice(string identifier) {
			if (identifier == _resolvedVoice) return _voice;
			ObjC.Release(_voice);
			_voice = IntPtr.Zero;
			_resolvedVoice = identifier;
			if (identifier == null) return IntPtr.Zero;
			_voice = ObjC.Retain(LookupVoice(identifier));
			if (_voice == IntPtr.Zero)
				Log.Error($"MacSpeechStream: AVSpeech has no voice {identifier}; its default stands in");
			return _voice;
		}

		/// <summary>Take the finished render off its block, free its handle, put the block up for later reuse, and schedule its speech.</summary>
		private void Finish(Render render) {
			_inFlight = null;
			Marshal.WriteIntPtr(_block, BlockHandleOffset, IntPtr.Zero);
			render.Handle.Free();
			_freeBlocks.Enqueue(_block);
			_block = IntPtr.Zero;
			if (render.SecondsSinceStart > SlowRenderSeconds)
				Log.Debug($"MacSpeechStream: a render took {render.SecondsSinceStart:F1} s");
			if (render.Problem != null) {
				Log.Error($"MacSpeechStream: rendered speech unusable: {render.Problem}");
				return;
			}
			try {
				float[] trimmed = SpeechSamples.Trim(render.Samples, render.Count, render.SampleRate);
				SpeechSamples.Normalize(trimmed, render.SampleRate);
				SpeechSamples.FadeEdges(trimmed, render.SampleRate);
				Schedule(trimmed, render.SampleRate);
			} catch (Exception ex) {
				Log.Error($"MacSpeechStream: could not play rendered speech: {ex.Message}");
			}
		}

		/// <summary>Abandon the render in flight together with its synthesizer and block; the next render gets new ones.</summary>
		private void Retire() {
			Render render = _inFlight;
			if (render == null) return;
			_retired.Add(new Retired { Synth = _synth, Block = _block, Render = render });
			_inFlight = null;
			_synth = IntPtr.Zero;
			_block = IntPtr.Zero;
		}

		/// <summary>Release retired synthesizers whose render has ended, and put their blocks up for reuse.</summary>
		private void Reap() {
			for (int i = _retired.Count - 1; i >= 0; i--) {
				Retired r = _retired[i];
				if (!r.Render.Done) continue;
				Marshal.WriteIntPtr(r.Block, BlockHandleOffset, IntPtr.Zero);
				r.Render.Handle.Free();
				ObjC.Release(r.Synth);
				_freeBlocks.Enqueue(r.Block);
				_retired.RemoveAt(i);
			}
		}

		// ---- playback ----

		/// <summary>Connect the player to the mixer in float32 mono at <paramref name="sampleRate"/>, replacing any earlier connection.</summary>
		private void Connect(double sampleRate) {
			IntPtr format = ObjC.SendDoubleUInt(ObjC.Send(ObjC.Class("AVAudioFormat"), SelAlloc),
				ObjC.Sel("initStandardFormatWithSampleRate:channels:"), sampleRate, 1);
			if (format == IntPtr.Zero) throw new InvalidOperationException($"AVAudioFormat init returned nil for {sampleRate} Hz");
			if (_format != IntPtr.Zero) ObjC.Send(_engine, ObjC.Sel("disconnectNodeOutput:"), _player);
			ObjC.Release(_format);
			_format = format;
			_formatRate = sampleRate;
			_gap = new float[(int)(GapSeconds * sampleRate)];
			ObjC.Send(_engine, ObjC.Sel("connect:to:format:"), _player, ObjC.Send(_engine, ObjC.Sel("mainMixerNode")), _format);
		}

		/// <summary>Copy the samples plus the gap into an AVAudioPCMBuffer and queue it on the player node, which plays it after whatever it already holds.</summary>
		private void Schedule(float[] trimmed, double sampleRate) {
			if (trimmed.Length == 0) return; // nothing to say

			// The engine stops on its own after an output device change; bring it
			// back first. Scheduling on a node whose engine is not running raises
			// an Objective-C exception, which .NET cannot catch.
			if (!ObjC.SendBool(_engine, SelIsRunning) && !ObjC.SendBool(_engine, SelStartAndReturnError, IntPtr.Zero)) {
				Log.Error("MacSpeechStream: AVAudioEngine failed to restart; dropping the message");
				return;
			}

			// A voice with another native rate: the player's buffers must match
			// its connection, so reconnect. Whatever the old voice still had
			// queued is dropped; a voice change comes from the settings screen,
			// where every key press silences speech anyway.
			if (sampleRate != _formatRate) {
				ObjC.Send(_player, SelStop);
				Connect(sampleRate);
			}

			int frames = trimmed.Length + _gap.Length;
			IntPtr buffer = ObjC.Send(ObjC.Send(ClassPcmBuffer, SelAlloc), SelInitWithFormat, _format, (IntPtr)frames);
			if (buffer == IntPtr.Zero) {
				Log.Error("MacSpeechStream: AVAudioPCMBuffer init returned nil");
				return;
			}
			IntPtr channels = ObjC.Send(buffer, SelFloatChannelData);
			if (channels == IntPtr.Zero) {
				Log.Error("MacSpeechStream: AVAudioPCMBuffer has no float channel data");
				ObjC.Release(buffer);
				return;
			}
			IntPtr channel = Marshal.ReadIntPtr(channels);
			Marshal.Copy(trimmed, 0, channel, trimmed.Length);
			Marshal.Copy(_gap, 0, channel + trimmed.Length * sizeof(float), _gap.Length);
			ObjC.Send(buffer, SelSetFrameLength, (IntPtr)frames);

			ObjC.Send(_player, SelScheduleBuffer, buffer, IntPtr.Zero);
			ObjC.Release(buffer); // the node holds its own reference until played
			ObjC.Send(_player, SelPlay); // idempotent while playing; needed after a stop or an engine restart
		}

		// ---- the callback block ----

		// An Objective-C block is a struct the runtime and the callee both read:
		// isa, flags, reserved, the invoke function, a descriptor, then any
		// captured variables. This one is a global block (never copied, never
		// freed by the runtime) whose single captured variable is a GCHandle to
		// the Render it is serving, rewritten between renders. Layout per the
		// Clang Block ABI; the signature says "void, taking the block and an
		// object".
		const int BlockSize = 40;
		const int BlockHandleOffset = 32;
		const int BlockIsGlobal = 1 << 28;
		const int BlockHasSignature = 1 << 30;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void BufferCallback(IntPtr block, IntPtr buffer);

		// Kept alive for the process: the function pointer below must stay valid.
		private static readonly BufferCallback OnBufferDelegate = OnBuffer;
		private static readonly IntPtr OnBufferPointer = Marshal.GetFunctionPointerForDelegate(OnBufferDelegate);
		private static readonly IntPtr Descriptor = MakeDescriptor();
		private static readonly IntPtr GlobalBlockClass = ObjC.Symbol("_NSConcreteGlobalBlock");

		private static IntPtr MakeDescriptor() {
			IntPtr descriptor = Marshal.AllocHGlobal(24);
			Marshal.WriteIntPtr(descriptor, 0, IntPtr.Zero);               // reserved
			Marshal.WriteIntPtr(descriptor, 8, (IntPtr)BlockSize);         // size of the block literal
			Marshal.WriteIntPtr(descriptor, 16, Marshal.StringToHGlobalAnsi("v16@?0@8")); // signature
			return descriptor;
		}

		private static IntPtr MakeBlock() {
			if (GlobalBlockClass == IntPtr.Zero) throw new InvalidOperationException("_NSConcreteGlobalBlock not found");
			IntPtr block = Marshal.AllocHGlobal(BlockSize);
			Marshal.WriteIntPtr(block, 0, GlobalBlockClass);
			Marshal.WriteInt32(block, 8, BlockIsGlobal | BlockHasSignature);
			Marshal.WriteInt32(block, 12, 0);
			Marshal.WriteIntPtr(block, 16, OnBufferPointer);
			Marshal.WriteIntPtr(block, 24, Descriptor);
			Marshal.WriteIntPtr(block, BlockHandleOffset, IntPtr.Zero);
			return block;
		}

		/// <summary>AVSpeech hands over one AVAudioBuffer per call, then an empty one at the end. Nothing may throw out of here.</summary>
		private static void OnBuffer(IntPtr block, IntPtr buffer) {
			IntPtr pool = ObjC.AutoreleasePoolPush();
			Render render = null;
			try {
				IntPtr handle = Marshal.ReadIntPtr(block, BlockHandleOffset);
				if (handle == IntPtr.Zero) return; // a buffer for a render already collected
				render = (Render)GCHandle.FromIntPtr(handle).Target;
				render.Append(buffer);
			} catch (Exception ex) {
				if (render != null) render.Fail(ex.Message);
				else Log.Error($"MacSpeechStream: the render callback failed: {ex.Message}"); // Debug.LogError is safe off the main thread
			} finally {
				ObjC.AutoreleasePoolPop(pool);
			}
		}

		/// <summary>One utterance being rendered: the samples the callback appends, read once <see cref="Done"/>.</summary>
		private sealed class Render {
			private readonly object _lock = new object();
			private float[] _samples = new float[32768]; // 1.5 s at 22.05 kHz, longer than most lines
			private int _count;
			private volatile bool _done;
			private string _problem;
			private readonly long _started = Stopwatch.GetTimestamp();
			private long _lastProgress = Stopwatch.GetTimestamp();

			public GCHandle Handle { get; }
			public double SampleRate { get; private set; }
			public bool Done => _done;
			public string Problem { get { lock (_lock) return _problem; } }
			public double SecondsSinceStart => (Stopwatch.GetTimestamp() - _started) / (double)Stopwatch.Frequency;
			public double SecondsSinceProgress => (Stopwatch.GetTimestamp() - Volatile.Read(ref _lastProgress)) / (double)Stopwatch.Frequency;

			/// <summary>The samples so far, and how many; only read once <see cref="Done"/>, after which the callback thread writes no more.</summary>
			public float[] Samples => _samples;
			public int Count => _count;

			public Render() {
				Handle = GCHandle.Alloc(this);
			}

			public void Append(IntPtr buffer) {
				Volatile.Write(ref _lastProgress, Stopwatch.GetTimestamp());
				if (buffer == IntPtr.Zero) {
					_done = true; // not documented, but the only sensible meaning of a nil buffer
					return;
				}
				if (!ObjC.IsKindOfClass(buffer, ClassPcmBuffer)) {
					Fail("AVSpeech delivered a buffer that is not PCM");
					return;
				}
				int frames = (int)((long)ObjC.Send(buffer, SelFrameLength) & 0xFFFFFFFF); // AVAudioFrameCount, 32-bit
				if (frames == 0) {
					_done = true;
					return;
				}
				double rate = ObjC.SendReturningDouble(ObjC.Send(buffer, SelFormat), SelSampleRate);
				IntPtr channels = ObjC.Send(buffer, SelFloatChannelData);
				if (channels == IntPtr.Zero) {
					Fail("AVSpeech delivered a buffer without float channel data");
					return;
				}
				lock (_lock) {
					if (SampleRate <= 0) SampleRate = rate;
					else if (rate != SampleRate) {
						_problem = $"sample rate changed from {SampleRate} to {rate} within one utterance";
						return;
					}
					if (_count + frames > _samples.Length)
						Array.Resize(ref _samples, Math.Max(_samples.Length * 2, _count + frames));
					Marshal.Copy(Marshal.ReadIntPtr(channels), _samples, _count, frames); // channel 0; speech voices are mono
					_count += frames;
				}
			}

			/// <summary>Record what went wrong; the render still waits for AVSpeech's end marker so its block is not reused early.</summary>
			public void Fail(string problem) {
				lock (_lock) _problem = _problem ?? problem;
			}
		}
	}
}
