#if DEBUG
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using OniAccess.Util;

namespace OniAccess.Dev {
	/// <summary>
	/// Dev-only in-process driver. Exposes a loopback HTTP server so an external
	/// driver (Claude, curl, python) can introspect and drive the live mod and game:
	///   POST /eval            body = C# source, run against the live game (REPL state
	///                         persists across calls); returns output, errors, result.
	///   GET  /speech?since=N  lines the mod has spoken since cursor N (the driver
	///                         cannot hear the screen reader; this is how it listens).
	///   GET  /screenshot      capture the game framebuffer to a PNG; returns its path.
	///   POST /reload          dispose the module generation and byte-load the rebuilt
	///                         Module/OniAccess.Module.dll; no game restart.
	///   GET  /module          which module generation and assembly is loaded.
	///   GET  /health          liveness.
	/// The module registers its own routes on Load (/gui, /input, /loadsave) and
	/// removes them on Dispose, so a reload swaps the handlers with the code they call.
	///
	/// The driver's actions stay silent for the player: any request other than
	/// /speech, /health and /module sets <see cref="DriverQuiet"/>, which mutes the
	/// mod's speech output (the /speech log still gets every line), and a physical
	/// key press or click clears it. It starts set, since the driver launched the game.
	///
	/// Eval and every module route run on the Unity main thread: HTTP requests
	/// enqueue a job and block until <see cref="Pump"/> (called once per frame from
	/// the host Ticker) executes it. /speech reads a thread-safe buffer directly.
	///
	/// Compiled only in DEBUG (#if DEBUG); a Release build has none of it. Even in
	/// Debug it stays inert unless ONIACCESS_DEV=1 or the marker file exists.
	/// </summary>
	public sealed class DevServer {
		public static readonly DevServer Instance = new DevServer();

		public const string EnableEnv = "ONIACCESS_DEV";
		public const string PortEnv = "ONIACCESS_DEV_PORT";
		public const string MarkerFile = "devserver.enable"; // under Mod.DataDir
		private const int DefaultPort = 8772; // Tangledeep 8770, WrathAccess 8771; keep distinct.

		// Routes the MODULE registers on Load and unregisters on Dispose.
		// handler(method, body, query) -> response body. Runs on the HTTP thread;
		// handlers hop to the main thread with OnMain when they touch game state.
		private readonly Dictionary<string, Func<string, string, string, string>> _moduleRoutes
			= new Dictionary<string, Func<string, string, string, string>>(StringComparer.OrdinalIgnoreCase);
		private readonly object _routeLock = new object();

		private sealed class Job {
			public Func<string> Work;
			public string Result = "";
			public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
		}

		private readonly SpeechLog _speech = new SpeechLog();
		private readonly CSharpEvaluator _evaluator = new CSharpEvaluator {
			CurrentModule = () => Modularity.ModuleLoader.CurrentAssembly,
		};
		private readonly ConcurrentQueue<Job> _jobs = new ConcurrentQueue<Job>();
		private DevHttpServer _http;

		/// <summary>True once the server is listening. Module code uses it to skip
		/// dev-only hooks (input injection patches) in a normal Debug run.</summary>
		public bool Enabled { get; private set; }

		private volatile bool _driverQuiet = true;

		/// <summary>True while the driver has the game; the module mutes speech output
		/// while it holds. Set by driving requests, cleared by physical input.</summary>
		public bool DriverQuiet => _driverQuiet;

		public void RegisterRoute(string route, Func<string, string, string, string> handler) {
			lock (_routeLock) _moduleRoutes[route] = handler;
		}

		public void UnregisterRoute(string route) {
			lock (_routeLock) _moduleRoutes.Remove(route);
		}

		/// <summary>Run work on the Unity main thread (next Pump) and block for its result.</summary>
		public string OnMain(Func<string> work, int timeoutSeconds = 30) {
			var job = new Job { Work = work };
			_jobs.Enqueue(job);
			if (!job.Done.Wait(TimeSpan.FromSeconds(timeoutSeconds)))
				return "[timeout] main thread did not run the job within " + timeoutSeconds + "s (frozen / not pumping?)\n";
			return job.Result;
		}

		/// <summary>Feed one spoken line into the /speech buffer. The module wires
		/// SpeechPipeline's observer here on Load.</summary>
		public void TapSpeech(string line) => _speech.Add(line);

		// Enabled by the env var OR a marker file the dev launcher drops. The marker
		// is immune to HOW the game is launched: a Steam relaunch spawns a fresh
		// process that does not inherit the env var, whereas the file is read from
		// the mod's data folder regardless.
		private static bool DevEnabled(out string how) {
			how = null;
			if (Environment.GetEnvironmentVariable(EnableEnv) == "1") {
				how = "env";
				return true;
			}
			if (File.Exists(Path.Combine(Mod.DataDir, MarkerFile))) {
				how = "marker";
				return true;
			}
			return false;
		}

		/// <summary>Stand up the server if enabled; otherwise stay inert.</summary>
		public void Start() {
			string how;
			if (!DevEnabled(out how)) return;

			// Keep the Unity player loop (and thus Pump, and thus every route)
			// running while the game is unfocused; otherwise the loop freezes the
			// moment the terminal takes focus and jobs never execute. Re-asserted
			// each Pump in case the game resets it.
			UnityEngine.Application.runInBackground = true;

			int port = DefaultPort;
			string p = Environment.GetEnvironmentVariable(PortEnv);
			if (!string.IsNullOrEmpty(p)) int.TryParse(p, out port);

			try {
				_http = new DevHttpServer(port, HandleRequest);
				_http.Start();
				Enabled = true;
				Log.Info($"Dev server on http://127.0.0.1:{port} (gate: {how})");
			} catch (Exception e) {
				// Dev-only infrastructure: the player's session is unaffected, the
				// launcher's health poll reports the outage.
				Log.Warn($"Dev server failed to start: {e}");
			}
		}

		/// <summary>Run queued main-thread jobs. Called once per frame by the host Ticker.</summary>
		public void Pump() {
			if (!Enabled) return;
			UnityEngine.Application.runInBackground = true;
			// Injection patches GetKeyDown/GetKey only, so anyKeyDown is the hardware.
			if (_driverQuiet && UnityEngine.Input.anyKeyDown) _driverQuiet = false;
			// Only the jobs queued before this frame run now; one enqueued during
			// the pump waits for the next frame, which lets a route let one frame
			// pass between two steps (key injection relies on it).
			int pending = _jobs.Count;
			Job job;
			while (pending-- > 0 && _jobs.TryDequeue(out job)) {
				try {
					job.Result = job.Work() ?? "";
				} catch (Exception e) {
					job.Result = "[host error] " + e + "\n";
				}
				job.Done.Set();
			}
		}

		// Runs on the HTTP thread.
		private string HandleRequest(string method, string path, string body) {
			string route = path;
			string query = "";
			int q = path.IndexOf('?');
			if (q >= 0) {
				route = path.Substring(0, q);
				query = path.Substring(q + 1);
			}

			if (route != "/speech" && route != "/health" && route != "/" && route != "/module")
				_driverQuiet = true;

			if (route == "/eval" && method == "POST") {
				if (string.IsNullOrWhiteSpace(body)) return "[empty] POST C# source as the request body\n";
				return OnMain(() => _evaluator.Eval(body));
			}

			if (route == "/screenshot" && method == "GET")
				return Screenshot();

			if (route == "/reload" && method == "POST") {
				return OnMain(() => {
					string result = Modularity.ModuleLoader.Reload();
					_evaluator.Reset(); // the next eval binds against the fresh generation
					return result;
				}, 60);
			}

			if (route == "/module" && method == "GET")
				return Modularity.ModuleLoader.Describe();

			Func<string, string, string, string> moduleHandler;
			lock (_routeLock) _moduleRoutes.TryGetValue(route, out moduleHandler);
			if (moduleHandler != null)
				return moduleHandler(method, body, query);

			if (route == "/speech" && method == "GET") {
				long since = 0;
				foreach (string kv in query.Split('&')) {
					if (kv.StartsWith("since=", StringComparison.Ordinal))
						long.TryParse(kv.Substring("since=".Length), out since);
				}
				long next;
				string lines = _speech.Render(since, out next);
				return "cursor: " + next + "\n" + lines;
			}

			if (route == "/health" || route == "/") return "ok\n";

			return "[404] " + method + " " + route + "\n";
		}

		// Capture the framebuffer to a PNG (works unfocused) and return its path.
		// ScreenCapture writes asynchronously over the next frame(s): trigger on the
		// main thread, then wait here on the HTTP thread for the file to appear and
		// its size to settle.
		private string Screenshot() {
			string path = Path.Combine(Path.GetTempPath(), "oniaccess_shot.png");
			OnMain(() => {
				if (File.Exists(path)) File.Delete(path);
				UnityEngine.ScreenCapture.CaptureScreenshot(path);
				return "requested";
			});

			var timer = System.Diagnostics.Stopwatch.StartNew();
			while (timer.Elapsed.TotalSeconds < 8) {
				if (File.Exists(path)) {
					long size = new FileInfo(path).Length;
					if (size > 0) {
						Thread.Sleep(60);
						if (new FileInfo(path).Length == size) return path + "\n";
					}
				}
				Thread.Sleep(50);
			}
			return "[timeout] screenshot not written within 8s\n";
		}
	}
}
#endif
