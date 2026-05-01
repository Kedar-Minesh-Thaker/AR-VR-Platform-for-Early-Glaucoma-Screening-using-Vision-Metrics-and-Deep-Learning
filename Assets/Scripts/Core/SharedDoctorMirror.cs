// SharedDoctorMirror.cs
// Singleton MonoBehaviour that replaces the per-test DoctorMirror.
// Provides a single WebSocket server for the entire app, JPEG frame streaming,
// and a Broadcast() method for any serialisable payload.
//
// Uses the same raw TCP / RFC 6455 approach as the original DoctorMirror.cs.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace OphthalSuite.Core
{
    public class SharedDoctorMirror : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────────
        public static SharedDoctorMirror Instance { get; private set; }

        // ── Inspector ────────────────────────────────────────────────────────────
        [Header("Doctor Camera")]
        [Tooltip("RenderTexture that DoctorCam renders into (640×360, RGBA32).")]
        public RenderTexture MonitorFeed;

        [SerializeField] private Vector2Int feedResolution = new Vector2Int(640, 360);

        [Header("WebSocket Server")]
        [SerializeField] private int  wsPort         = 8765;
        [SerializeField] private bool enableWebSocket = true;

        [Header("JPEG Frame Streaming")]
        [Tooltip("Seconds between JPEG frames sent to clients (0 = disable).")]
        [SerializeField] private float jpegIntervalSec = 2f;
        [SerializeField] [Range(10, 100)] private int jpegQuality = 60;

        // ── Runtime state ────────────────────────────────────────────────────────
        private Camera       _doctorCam;
        private TcpListener  _tcpListener;
        private Thread       _listenerThread;
        private readonly List<TcpClient>  _clients   = new List<TcpClient>();
        private readonly Queue<byte[]>    _sendQueue  = new Queue<byte[]>();
        private readonly Queue<string>    _inboundJson = new Queue<string>();
        private readonly object           _inboundLock = new object();
        private bool _running;

        // Last session state for new clients
        private string _lastSessionStateJson = "";

        // ── Unity lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            // Singleton enforcement
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("SharedDoctorMirror: duplicate instance destroyed.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            SetupDoctorCam();
            if (enableWebSocket) StartServer();
            if (jpegIntervalSec > 0f) StartCoroutine(StreamJpegFrames());
        }

        private void Update()
        {
            // Drain send queue on main thread
            lock (_sendQueue)
            {
                while (_sendQueue.Count > 0)
                {
                    byte[] frame = _sendQueue.Dequeue();
                    SendToAllClients(frame);
                }
            }

            // Prune dead clients
            lock (_clients)
            {
                _clients.RemoveAll(c => !c.Connected);
            }
        }

        /// <summary>Thread-safe: background WS reader enqueues JSON text frames here.</summary>
        public bool TryDequeueInboundJson(out string json)
        {
            lock (_inboundLock)
            {
                if (_inboundJson.Count == 0)
                {
                    json = null;
                    return false;
                }

                json = _inboundJson.Dequeue();
                return true;
            }
        }

        private void EnqueueInboundJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            lock (_inboundLock)
            {
                if (_inboundJson.Count > 200) return;
                _inboundJson.Enqueue(json);
            }
        }

        private void OnDestroy()
        {
            _running = false;
            _tcpListener?.Stop();
            _listenerThread?.Join(500);
            if (Instance == this) Instance = null;
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Serialise any object to JSON and broadcast to all connected WebSocket clients.
        /// Thread-safe — can be called from any thread (queued to main thread).
        /// </summary>
        public void Broadcast(object payload)
        {
            string json = JsonUtility.ToJson(payload);

            // Cache SESSION_STATE for new clients
            if (json.Contains("\"messageType\":\"SESSION_STATE\""))
                _lastSessionStateJson = json;

            byte[] frame = EncodeWsTextFrame(Encoding.UTF8.GetBytes(json));
            lock (_sendQueue)
            {
                _sendQueue.Enqueue(frame);
            }
        }

        // ── Doctor Camera setup ──────────────────────────────────────────────────

        private void SetupDoctorCam()
        {
            if (MonitorFeed == null)
            {
                MonitorFeed = new RenderTexture(
                    feedResolution.x, feedResolution.y, 24, RenderTextureFormat.ARGB32);
                MonitorFeed.name = "MonitorFeed_Shared";
                MonitorFeed.Create();
            }

            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogError("SharedDoctorMirror: no Main Camera found.");
                return;
            }

            var go = new GameObject("DoctorCam_Shared");
            go.transform.SetParent(mainCam.transform, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            _doctorCam = go.AddComponent<Camera>();
            _doctorCam.CopyFrom(mainCam);
            _doctorCam.targetTexture = MonitorFeed;
            _doctorCam.depth         = mainCam.depth - 1;

            Debug.Log("SharedDoctorMirror: DoctorCam created.");
        }

        // ── WebSocket server ─────────────────────────────────────────────────────

        private void StartServer()
        {
            _running = true;
            _tcpListener = new TcpListener(IPAddress.Any, wsPort);
            _tcpListener.Start();
            _listenerThread = new Thread(AcceptLoop) { IsBackground = true };
            _listenerThread.Start();
            Debug.Log($"SharedDoctorMirror: WebSocket server on port {wsPort}.");
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _tcpListener.AcceptTcpClient();
                    var t = new Thread(() => HandshakeAndKeepAlive(client)) { IsBackground = true };
                    t.Start();
                }
                catch { break; }
            }
        }

        private void HandshakeAndKeepAlive(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();

                // Read HTTP upgrade request
                byte[] buf = new byte[4096];
                int len = stream.Read(buf, 0, buf.Length);
                string req = Encoding.UTF8.GetString(buf, 0, len);

                // Extract Sec-WebSocket-Key
                const string keyHeader = "Sec-WebSocket-Key: ";
                int ki = req.IndexOf(keyHeader, StringComparison.Ordinal);
                if (ki < 0) { client.Close(); return; }
                int ks = ki + keyHeader.Length;
                string wsKey = req.Substring(ks,
                    req.IndexOf("\r\n", ks, StringComparison.Ordinal) - ks).Trim();

                // Compute accept hash
                string acceptKey = Convert.ToBase64String(
                    System.Security.Cryptography.SHA1.Create().ComputeHash(
                        Encoding.UTF8.GetBytes(wsKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

                string response =
                    "HTTP/1.1 101 Switching Protocols\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Connection: Upgrade\r\n" +
                    $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";
                byte[] respBytes = Encoding.UTF8.GetBytes(response);
                stream.Write(respBytes, 0, respBytes.Length);

                lock (_clients) _clients.Add(client);
                Debug.Log("SharedDoctorMirror: client connected.");

                // Send cached session state to new client
                if (!string.IsNullOrEmpty(_lastSessionStateJson))
                {
                    byte[] stateFrame = EncodeWsTextFrame(
                        Encoding.UTF8.GetBytes(_lastSessionStateJson));
                    try { stream.Write(stateFrame, 0, stateFrame.Length); }
                    catch { /* ignore */ }
                }

                var rx = new List<byte>();
                var tmp = new byte[8192];
                bool closeRequested = false;
                while (_running && client.Connected && !closeRequested)
                {
                    try
                    {
                        if (!stream.DataAvailable)
                        {
                            Thread.Sleep(15);
                            continue;
                        }

                        int n = stream.Read(tmp, 0, tmp.Length);
                        if (n <= 0) break;
                        for (int i = 0; i < n; i++) rx.Add(tmp[i]);

                        while (WsCodec.TryConsumeFrame(rx, out int op, out byte[] pl))
                        {
                            if (op == 1 && pl != null)
                            {
                                string txt = Encoding.UTF8.GetString(pl);
                                EnqueueInboundJson(txt);
                            }
                            else if (op == 8)
                            {
                                closeRequested = true;
                                break;
                            }
                            else if (op == 9 && pl != null)
                            {
                                byte[] pong = WsCodec.EncodeControlFrame(0x0A, pl);
                                stream.Write(pong, 0, pong.Length);
                            }
                        }
                    }
                    catch
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"SharedDoctorMirror handshake error: {ex.Message}");
            }
            finally
            {
                lock (_clients) _clients.Remove(client);
                client.Close();
            }
        }

        // ── Send helpers ─────────────────────────────────────────────────────────

        private void SendToAllClients(byte[] frame)
        {
            lock (_clients)
            {
                foreach (var client in _clients)
                {
                    try
                    {
                        if (client.Connected)
                            client.GetStream().Write(frame, 0, frame.Length);
                    }
                    catch { /* client disconnected */ }
                }
            }
        }

        // ── JPEG frame streaming ─────────────────────────────────────────────────

        private IEnumerator StreamJpegFrames()
        {
            var wait = new WaitForSeconds(jpegIntervalSec);
            while (true)
            {
                yield return wait;
                if (_clients.Count == 0 || MonitorFeed == null) continue;

                // Readback from RenderTexture (main thread)
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = MonitorFeed;
                var tex = new Texture2D(MonitorFeed.width, MonitorFeed.height,
                                        TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, MonitorFeed.width, MonitorFeed.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                byte[] jpeg = tex.EncodeToJPG(jpegQuality);
                Destroy(tex);

                // Send as binary frame with "FRAME:" prefix
                byte[] prefix  = Encoding.UTF8.GetBytes("FRAME:");
                byte[] payload = new byte[prefix.Length + jpeg.Length];
                Buffer.BlockCopy(prefix, 0, payload, 0,             prefix.Length);
                Buffer.BlockCopy(jpeg,   0, payload, prefix.Length, jpeg.Length);

                byte[] frame = EncodeBinaryWsFrame(payload);
                lock (_sendQueue) _sendQueue.Enqueue(frame);
            }
        }

        // ── RFC 6455 frame encoders ──────────────────────────────────────────────

        private static byte[] EncodeWsTextFrame(byte[] payload)
        {
            return EncodeWsFrame(0x81, payload);  // 0x81 = FIN + text opcode
        }

        private static byte[] EncodeBinaryWsFrame(byte[] payload)
        {
            return EncodeWsFrame(0x82, payload);  // 0x82 = FIN + binary opcode
        }

        private static byte[] EncodeWsFrame(byte opcode, byte[] payload)
        {
            int len = payload.Length;
            byte[] header;

            if (len < 126)
            {
                header = new byte[] { opcode, (byte)len };
            }
            else if (len < 65536)
            {
                header = new byte[]
                {
                    opcode, 126,
                    (byte)(len >> 8), (byte)(len & 0xFF)
                };
            }
            else
            {
                header = new byte[]
                {
                    opcode, 127,
                    0, 0, 0, 0,
                    (byte)(len >> 24), (byte)(len >> 16),
                    (byte)(len >> 8), (byte)(len & 0xFF)
                };
            }

            var frame = new byte[header.Length + payload.Length];
            Buffer.BlockCopy(header,  0, frame, 0,             header.Length);
            Buffer.BlockCopy(payload, 0, frame, header.Length, payload.Length);
            return frame;
        }
    }
}
