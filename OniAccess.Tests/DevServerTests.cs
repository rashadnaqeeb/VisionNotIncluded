using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using OniAccess.Dev;

namespace OniAccess.Tests {
	/// <summary>
	/// Offline tests for the dev server's host-side pieces: the speech log the
	/// driver reads speech back from, the raw loopback HTTP server, and the C#
	/// evaluator behind /eval. Real code paths only; the HTTP test opens a real
	/// loopback socket on an ephemeral port.
	/// </summary>
	static class DevServerTests {
		private static (string, bool, string) Check(string name, bool ok, string detail)
			=> (name, ok, ok ? "OK" : detail);

		public static IEnumerable<(string, bool, string)> All() {
			yield return SpeechLogCursorReturnsOnlyNewLines();
			yield return SpeechLogEvictionKeepsIndicesStable();
			yield return HttpServerParsesPostBodyAndAnswers();
			yield return EvaluatorKeepsStateAcrossCalls();
			yield return EvaluatorCapturesConsoleOutput();
			yield return EvaluatorReportsCompileErrors();
		}

		// The driver polls with the cursor from the previous answer; a cursor
		// that repeats or skips a line means speech is read twice or missed.
		private static (string, bool, string) SpeechLogCursorReturnsOnlyNewLines() {
			var log = new SpeechLog();
			log.Add("a");
			log.Add("b");
			string first = log.Render(0, out long next);
			log.Add("c");
			string second = log.Render(next, out long next2);
			string third = log.Render(next2, out long next3);
			bool ok = first == "0: a\n1: b\n" && next == 2
				&& second == "2: c\n" && next2 == 3
				&& third == "" && next3 == 3;
			return Check("SpeechLogCursorReturnsOnlyNewLines", ok,
				$"first={first.Replace("\n", "|")} next={next} second={second.Replace("\n", "|")} next2={next2} third={third.Replace("\n", "|")} next3={next3}");
		}

		// Once the ring evicts old lines, global indices must not shift: a stale
		// cursor clamps to the oldest kept line, and a cursor past the end is
		// pulled back to the end rather than pointing into the future.
		private static (string, bool, string) SpeechLogEvictionKeepsIndicesStable() {
			var log = new SpeechLog();
			for (int i = 0; i < SpeechLog.Capacity + 2; i++) log.Add("line" + i);
			string fromStart = log.Render(0, out long next);
			string firstLine = fromStart.Substring(0, fromStart.IndexOf('\n'));
			string beyond = log.Render(next + 100, out long nextBeyond);
			bool ok = firstLine == "2: line2" && next == SpeechLog.Capacity + 2
				&& beyond == "" && nextBeyond == SpeechLog.Capacity + 2;
			return Check("SpeechLogEvictionKeepsIndicesStable", ok,
				$"firstLine={firstLine} next={next} beyond={beyond.Length} nextBeyond={nextBeyond}");
		}

		// curl sends the header and body in separate packets; the server must
		// keep reading until Content-Length bytes have arrived, count them as
		// bytes (not chars), and answer with a matching Content-Length.
		private static (string, bool, string) HttpServerParsesPostBodyAndAnswers() {
			string seenMethod = null, seenPath = null, seenBody = null;
			var server = new DevHttpServer(0, (method, path, body) => {
				seenMethod = method;
				seenPath = path;
				seenBody = body;
				return "réponse\n";
			});
			server.Start();
			try {
				string body = "int x = 1; // héllo";
				byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
				string head = "POST /eval?since=3 HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: " + bodyBytes.Length + "\r\n\r\n";
				using (var client = new TcpClient("127.0.0.1", server.Port)) {
					var stream = client.GetStream();
					byte[] headBytes = Encoding.ASCII.GetBytes(head);
					stream.Write(headBytes, 0, headBytes.Length);
					stream.Flush();
					Thread.Sleep(50);
					stream.Write(bodyBytes, 0, bodyBytes.Length);
					stream.Flush();
					var received = new List<byte>();
					var buf = new byte[4096];
					int n;
					while ((n = stream.Read(buf, 0, buf.Length)) > 0)
						for (int i = 0; i < n; i++) received.Add(buf[i]);
					string response = Encoding.UTF8.GetString(received.ToArray());
					int split = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);
					string responseHead = split < 0 ? response : response.Substring(0, split);
					string responseBody = split < 0 ? "" : response.Substring(split + 4);
					bool ok = seenMethod == "POST" && seenPath == "/eval?since=3" && seenBody == body
						&& responseHead.StartsWith("HTTP/1.1 200 OK")
						&& responseHead.Contains("Content-Length: " + Encoding.UTF8.GetByteCount("réponse\n"))
						&& responseBody == "réponse\n";
					return Check("HttpServerParsesPostBodyAndAnswers", ok,
						$"method={seenMethod} path={seenPath} body={seenBody} head={responseHead.Replace("\r\n", "|")} body={responseBody}");
				}
			} finally {
				server.Stop();
			}
		}

		// The session is a REPL: a variable defined by one request is visible to
		// the next, and the last expression's value comes back on the "=> " line.
		private static (string, bool, string) EvaluatorKeepsStateAcrossCalls() {
			var ev = new CSharpEvaluator();
			string first = ev.Eval("int devServerTestValue = 2;");
			// Addition, not multiplication: the REPL reads "a * b" as a pointer
			// declaration (the C ambiguity), which is a quirk, not the invariant here.
			string second = ev.Eval("devServerTestValue + 40");
			bool ok = first == "(ok)\n" && second == "=> 42\n";
			return Check("EvaluatorKeepsStateAcrossCalls", ok, $"first={first.Trim()} second={second.Trim()}");
		}

		// Console output is what eval'd probe code reports with; it must land in
		// the response, not in the game's stdout.
		private static (string, bool, string) EvaluatorCapturesConsoleOutput() {
			var ev = new CSharpEvaluator();
			string result = ev.Eval("Console.WriteLine(\"probe\"); 7");
			bool ok = result == "probe\n=> 7\n";
			return Check("EvaluatorCapturesConsoleOutput", ok, $"result={result.Replace("\n", "|")}");
		}

		// A compile error must come back as text instead of a silent "(ok)".
		private static (string, bool, string) EvaluatorReportsCompileErrors() {
			var ev = new CSharpEvaluator();
			string result = ev.Eval("int broken = \"text\";");
			bool ok = result.StartsWith("[compile] ") && result.Contains("CS0029");
			return Check("EvaluatorReportsCompileErrors", ok, $"result={result.Trim()}");
		}
	}
}
