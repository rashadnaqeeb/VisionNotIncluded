#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Mono.CSharp;

namespace OniAccess.Dev {
	/// <summary>
	/// Wraps Mono.CSharp's REPL so the dev driver can POST arbitrary C# and run
	/// it against the live game. State persists across calls (usings and
	/// variables defined in one eval are visible to the next), so the session
	/// behaves like a REPL. DEBUG-only.
	///
	/// MUST be used from the Unity main thread: evaluated code routinely touches
	/// Unity and game objects. The dev server enqueues code and pumps it from
	/// the per-frame tick.
	/// </summary>
	public sealed class CSharpEvaluator {
		private Evaluator _evaluator;
		private StringWriter _report; // compiler diagnostics land here

		/// <summary>The current module assembly; the evaluator references only
		/// this generation, so stale generations never make types ambiguous.
		/// Null means no module filter (tests).</summary>
		public Func<Assembly> CurrentModule = () => null;

		/// <summary>Drop the REPL session so the next Eval builds a fresh one.
		/// Called after a module reload, so evaluated code binds against the NEW
		/// generation (and only it).</summary>
		public void Reset() => _evaluator = null;

		private void Initialize() {
			_report = new StringWriter();
			var ctx = new CompilerContext(new CompilerSettings(), new StreamReportPrinter(_report));
			_evaluator = new Evaluator(ctx);

			// Make the game and mod assemblies visible to evaluated code. The core
			// BCL assemblies the evaluator already imports must NOT be re-referenced:
			// adding them again imports every type twice and makes even int
			// ambiguous (CS0433/CS0121). The seed set doubles as the dedupe set.
			var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
				"mscorlib", "System", "System.Core", "System.Xml", "System.Xml.Linq",
				"System.Configuration", "System.Data", "Mono.CSharp", "Mono.Security",
				"netstandard",
			};
			Assembly current = CurrentModule();
			foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies()) {
				string name = asm.GetName().Name;
				if (string.IsNullOrEmpty(name) || asm.IsDynamic || !referenced.Add(name)) continue;
				// Old module GENERATIONS leak in the AppDomain (byte loads are
				// uncollectible) and carry the same types under different assembly
				// names; referencing more than the current one would make every
				// module type ambiguous.
				if (name.StartsWith("OniAccess.Module", StringComparison.OrdinalIgnoreCase)
					&& current != null && !ReferenceEquals(asm, current)) continue;
				try {
					_evaluator.ReferenceAssembly(asm);
				} catch (Exception e) {
					Util.Log.Warn($"[eval] cannot reference {name}: {e.Message}");
				}
			}

			_evaluator.Run(
				"using System; using System.Linq; using System.Reflection; "
				+ "using System.Collections.Generic; using UnityEngine;");
		}

		/// <summary>Compile and run <paramref name="code"/>; return captured
		/// console output, compile diagnostics, the exception, and the value of
		/// the last expression on a line starting with "=> ".</summary>
		public string Eval(string code) {
			if (_evaluator == null) Initialize();

			var output = new StringWriter();
			TextWriter origOut = Console.Out;
			TextWriter origErr = Console.Error;
			int reportStart = _report.GetStringBuilder().Length;

			object value = null;
			bool hasValue = false;
			Exception thrown = null;

			Console.SetOut(output);
			Console.SetError(output);
			try {
				// Evaluate consumes one statement or expression at a time and returns
				// the unconsumed remainder; loop until it is all gone, or it stalls
				// or errors.
				string input = code;
				int guard = 0;
				while (!string.IsNullOrWhiteSpace(input) && guard++ < 10000) {
					string remainder;
					object res;
					bool resSet;
					try {
						remainder = _evaluator.Evaluate(input, out res, out resSet);
					} catch (Exception e) {
						thrown = e;
						break;
					}
					if (resSet) {
						value = res;
						hasValue = true;
					}
					if (_report.GetStringBuilder().Length > reportStart) break; // a compile diagnostic was emitted
					if (remainder == input) break; // no progress (incomplete input)
					input = remainder;
				}
			} finally {
				Console.SetOut(origOut);
				Console.SetError(origErr);
			}

			var sb = new StringBuilder();
			string captured = output.ToString();
			if (captured.Length > 0) {
				sb.Append(captured);
				if (!captured.EndsWith("\n")) sb.Append('\n');
			}
			string diagnostics = _report.ToString().Substring(reportStart).Trim();
			if (diagnostics.Length > 0) sb.Append("[compile] ").Append(diagnostics).Append('\n');
			if (thrown != null) sb.Append("[exception] ").Append(thrown).Append('\n');
			if (hasValue) sb.Append("=> ").Append(value == null ? "null" : value.ToString()).Append('\n');
			if (sb.Length == 0) sb.Append("(ok)\n");
			return sb.ToString();
		}
	}
}
#endif
