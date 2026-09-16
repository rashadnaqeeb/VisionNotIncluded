#if DEBUG
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using OniAccess.Util;

namespace OniAccess.Dev {
	/// <summary>
	/// Minimal loopback HTTP/1.1 server on a raw TcpListener. Speaks just enough
	/// of the protocol for curl and Python's urllib: request line, Content-Length
	/// body, one plain-text response, connection close. Avoids
	/// System.Net.HttpListener so there is no http.sys URL reservation on
	/// Windows. Loopback-only; a dev tool, not hardened. DEBUG-only.
	/// </summary>
	internal sealed class DevHttpServer {
		// method, route+query, body -> response body
		public delegate string RequestHandler(string method, string path, string body);

		private readonly int _port;
		private readonly RequestHandler _handler;
		private TcpListener _listener;
		private Thread _thread;
		private volatile bool _running;

		/// <summary>The bound port (differs from the requested one when 0 was asked for).</summary>
		public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

		public DevHttpServer(int port, RequestHandler handler) {
			_port = port;
			_handler = handler;
		}

		public void Start() {
			_listener = new TcpListener(IPAddress.Loopback, _port);
			_listener.Start();
			_running = true;
			_thread = new Thread(Loop) { IsBackground = true, Name = "OniAccessDevHttp" };
			_thread.Start();
		}

		public void Stop() {
			_running = false;
			try {
				_listener.Stop();
			} catch (Exception e) {
				Log.Warn($"dev http: stop failed: {e.Message}");
			}
		}

		private void Loop() {
			while (_running) {
				TcpClient client = null;
				try {
					client = _listener.AcceptTcpClient();
					Handle(client);
				} catch (Exception e) {
					if (_running) Log.Warn($"dev http: {e.Message}");
				} finally {
					if (client != null) {
						try {
							client.Close();
						} catch (Exception e) {
							Log.Warn($"dev http: close failed: {e.Message}");
						}
					}
				}
			}
		}

		private void Handle(TcpClient client) {
			client.ReceiveTimeout = 15000;
			NetworkStream stream = client.GetStream();
			var data = new List<byte>();
			var buf = new byte[8192];

			int headerEnd = -1;
			while (headerEnd < 0) {
				int n = stream.Read(buf, 0, buf.Length);
				if (n <= 0) return;
				for (int i = 0; i < n; i++) data.Add(buf[i]);
				headerEnd = IndexOfHeaderEnd(data);
				if (data.Count > 1 << 20) {
					Write(stream, 400, "header too large\n");
					return;
				}
			}

			string header = Encoding.ASCII.GetString(data.ToArray(), 0, headerEnd);
			string[] lines = header.Split(new[] { "\r\n" }, StringSplitOptions.None);
			string[] requestLine = lines[0].Split(' ');
			string method = requestLine.Length > 0 ? requestLine[0] : "";
			string path = requestLine.Length > 1 ? requestLine[1] : "/";

			int contentLength = 0;
			foreach (string line in lines) {
				if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
					int.TryParse(line.Substring("Content-Length:".Length).Trim(), out contentLength);
			}

			int bodyStart = headerEnd + 4;
			while (data.Count - bodyStart < contentLength) {
				int n = stream.Read(buf, 0, buf.Length);
				if (n <= 0) break;
				for (int i = 0; i < n; i++) data.Add(buf[i]);
			}
			int have = Math.Min(contentLength, data.Count - bodyStart);
			string body = have > 0 ? Encoding.UTF8.GetString(data.ToArray(), bodyStart, have) : "";

			string response;
			try {
				response = _handler(method, path, body) ?? "";
			} catch (Exception e) {
				Write(stream, 500, "handler error: " + e + "\n");
				return;
			}
			Write(stream, 200, response);
		}

		private static int IndexOfHeaderEnd(List<byte> d) {
			for (int i = 0; i + 3 < d.Count; i++) {
				if (d[i] == 13 && d[i + 1] == 10 && d[i + 2] == 13 && d[i + 3] == 10) return i;
			}
			return -1;
		}

		private static void Write(NetworkStream stream, int status, string body) {
			byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
			string reason = status == 200 ? "OK" : "ERROR";
			string head = "HTTP/1.1 " + status + " " + reason + "\r\n"
				+ "Content-Type: text/plain; charset=utf-8\r\n"
				+ "Content-Length: " + bodyBytes.Length + "\r\n"
				+ "Connection: close\r\n\r\n";
			byte[] headBytes = Encoding.ASCII.GetBytes(head);
			stream.Write(headBytes, 0, headBytes.Length);
			stream.Write(bodyBytes, 0, bodyBytes.Length);
			stream.Flush();
		}
	}
}
#endif
