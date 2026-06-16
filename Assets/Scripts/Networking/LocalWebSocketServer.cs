using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

public class LocalWebSocketServer : MonoBehaviour
{
    public int port = 8081;
    public System.Action<int> OnPlayerConnected;
    public System.Action<int> OnPlayerDisconnected;

    private TcpListener listener;
    private Thread serverThread;
    private volatile bool running;

    private bool[] connected = new bool[3];
    private string[] playerNames = new string[3];
    private bool[] left = new bool[3];
    private bool[] right = new bool[3];
    private bool[] jump = new bool[3];
    private bool[] boost = new bool[3];
    private object inputLock = new object();

    private Dictionary<NetworkStream, int> streamToPlayer = new Dictionary<NetworkStream, int>();

    private Queue<Action> mainQueue = new Queue<Action>();
    private object qLock = new object();

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        running = true;
        serverThread = new Thread(ServerLoop);
        serverThread.IsBackground = true;
        serverThread.Start();
    }

    void Update()
    {
        lock (qLock) { while (mainQueue.Count > 0) mainQueue.Dequeue()(); }
    }

    void OnDestroy() { Stop(); }
    void OnApplicationQuit() { Stop(); }

    void Stop()
    {
        running = false;
        if (listener != null) listener.Stop();
    }

    void ServerLoop()
    {
        try
        {
            listener = new TcpListener(IPAddress.Parse("0.0.0.0"), port);
            listener.Start();
            Debug.Log("[WS] Server on port " + port);

            while (running)
            {
                var c = listener.AcceptTcpClient();
                var t = new Thread(() => HandleConnection(c));
                t.IsBackground = true;
                t.Start();
            }
        }
        catch (Exception e)
        {
            if (running && !(e is ThreadAbortException))
                Debug.LogError("[WS] " + e.Message);
        }
    }

    void HandleConnection(object obj)
    {
        var client = (TcpClient)obj;
        Debug.Log("[WS] Accepted connection from " + client.Client.RemoteEndPoint);
        NetworkStream stream = null;
        int assignedPlayer = 0;
        try
        {
            stream = client.GetStream();
            stream.ReadTimeout = 5000;

            byte[] buf = new byte[4096];
            int total = 0;
            while (total < buf.Length)
            {
                int read = stream.Read(buf, total, Math.Min(512, buf.Length - total));
                if (read <= 0) break;
                total += read;
                string data = Encoding.UTF8.GetString(buf, 0, total);
                if (data.Contains("\r\n\r\n")) break;
            }

            if (total == 0) { client.Close(); return; }

            string request = Encoding.UTF8.GetString(buf, 0, total);
            var match = Regex.Match(request, "Sec-WebSocket-Key: ([^\r\n]+)", RegexOptions.IgnoreCase);
            if (!match.Success) { client.Close(); return; }

            string key = match.Groups[1].Value.Trim();

            string accept;
            using (var sha = new SHA1Managed())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));
                accept = Convert.ToBase64String(hash);
            }

            string response = "HTTP/1.1 101 Switching Protocols\r\n" +
                              "Upgrade: websocket\r\n" +
                              "Connection: Upgrade\r\n" +
                              "Sec-WebSocket-Accept: " + accept + "\r\n\r\n";

            byte[] resp = Encoding.UTF8.GetBytes(response);
            stream.Write(resp, 0, resp.Length);

            try { client.Client.Blocking = true; } catch { }
            assignedPlayer = ReadLoop(stream, client);
        }
        catch (Exception e)
        {
            Debug.Log("[WS] Error: " + e.GetType().Name + ": " + e.Message);
        }
        finally
        {
            if (assignedPlayer > 0)
            {
                lock (inputLock)
                {
                    connected[assignedPlayer] = false;
                    playerNames[assignedPlayer] = null;
                }
                lock (qLock) { mainQueue.Enqueue(() => OnPlayerDisconnected?.Invoke(assignedPlayer)); }
            }
            lock (streamToPlayer) { streamToPlayer.Remove(stream); }
            try { client.Close(); } catch { }
        }
    }

    int ReadLoop(NetworkStream stream, TcpClient client)
    {
        byte[] buf = new byte[4096];
        int offset = 0;
        while (running)
        {
            if (!stream.DataAvailable)
            {
                Thread.Sleep(5);
                continue;
            }
            int read = stream.Read(buf, offset, buf.Length - offset);
            if (read <= 0) break;
            offset += read;
            int consumed = 0;
            while (true)
            {
                if (offset - consumed < 2) break;
                int b1 = buf[consumed];
                int opcode = b1 & 0x0F;
                if (opcode == 0x8) { int pid; lock (streamToPlayer) { streamToPlayer.TryGetValue(stream, out pid); } return pid; }
                if (opcode == 0x9)
                {
                    stream.Write(new byte[] { 0x8A, 0x00 }, 0, 2);
                    consumed += 2;
                    continue;
                }
                if (opcode != 0x1) { consumed++; continue; }

                int b2 = buf[consumed + 1];
                bool mask = (b2 & 0x80) != 0;
                long len = b2 & 0x7F;
                int header = 2;

                if (len == 126) { if (offset - consumed < 4) break; len = (buf[consumed + 2] << 8) | buf[consumed + 3]; header = 4; }
                else if (len == 127) { if (offset - consumed < 10) break; len = 0; for (int i = 0; i < 8; i++) len = (len << 8) | buf[consumed + 2 + i]; header = 10; }

                if (mask) header += 4;
                int frameEnd = consumed + header + (int)len;
                if (offset - consumed < header + (int)len) break;

                byte[] payload = new byte[len];
                int maskStart = consumed + (len == 126 ? 4 : len == 127 ? 10 : 2);
                int maskKeyIdx = mask ? maskStart : -1;
                int payloadStart = mask ? maskStart + 4 : consumed + (len == 126 ? 4 : len == 127 ? 10 : 2);
                Array.Copy(buf, payloadStart, payload, 0, (int)len);

                if (mask)
                    for (int i = 0; i < payload.Length; i++)
                        payload[i] ^= buf[maskKeyIdx + (i % 4)];

                string msg = Encoding.UTF8.GetString(payload);
                ProcessMessage(msg, stream);
                consumed = frameEnd;
            }
            if (consumed > 0 && offset > consumed)
            {
                Array.Copy(buf, consumed, buf, 0, offset - consumed);
                offset -= consumed;
            }
            else if (consumed > 0)
                offset = 0;
        }
        int p; lock (streamToPlayer) { streamToPlayer.TryGetValue(stream, out p); }
        return p;
    }

    int ReadExact(NetworkStream stream, byte[] buf, int count)
    {
        int total = 0;
        while (total < count)
        {
            int r = stream.Read(buf, total, count - total);
            if (r <= 0) break;
            total += r;
        }
        return total;
    }

    void ProcessMessage(string json, NetworkStream stream)
    {
        try
        {
            string type = Extract(json, "type");
            if (type == "join")
            {
                string name = Extract(json, "name");
                if (string.IsNullOrEmpty(name)) return;

                int pid = 0;
                bool full = false;
                lock (inputLock)
                {
                    full = connected[1] && connected[2];
                    if (!full)
                    {
                        pid = connected[1] ? 2 : 1;
                        connected[pid] = true;
                        playerNames[pid] = name;
                    }
                }

                if (full)
                {
                    byte[] err = Encoding.UTF8.GetBytes("{\"type\":\"error\",\"message\":\"Game is full\"}");
                    byte[] errHdr = err.Length < 126 ? new byte[] { 0x81, (byte)err.Length } : new byte[] { 0x81, 126, (byte)(err.Length >> 8), (byte)(err.Length & 0xFF) };
                    try { stream.Write(errHdr, 0, errHdr.Length); stream.Write(err, 0, err.Length); } catch { }
                    return;
                }

                byte[] resp = Encoding.UTF8.GetBytes("{\"type\":\"state\",\"player\":" + pid + ",\"name\":\"" + JsonEscape(name) + "\",\"connected\":true}");
                byte[] respHdr = resp.Length < 126 ? new byte[] { 0x81, (byte)resp.Length } : new byte[] { 0x81, 126, (byte)(resp.Length >> 8), (byte)(resp.Length & 0xFF) };
                try { stream.Write(respHdr, 0, respHdr.Length); stream.Write(resp, 0, resp.Length); } catch { }

                Debug.Log("[WS] Player " + pid + " (" + name + ") joined");
                lock (streamToPlayer) { streamToPlayer[stream] = pid; }
                lock (qLock) { mainQueue.Enqueue(() => OnPlayerConnected?.Invoke(pid)); }
            }
            else if (type == "input")
            {
                int pid = ExtractInt(json, "player");
                bool l = ExtractBool(json, "left");
                bool r = ExtractBool(json, "right");
                bool j = ExtractBool(json, "jump");
                bool b = ExtractBool(json, "boost");
                lock (inputLock)
                {
                    left[pid] = l; right[pid] = r; jump[pid] = j; boost[pid] = b;
                }
            }
        }
        catch { }
    }

    string JsonEscape(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    public bool IsPlayerConnected(int pid)
    {
        lock (inputLock) { return pid >= 1 && pid <= 2 && connected[pid]; }
    }

    public string GetPlayerName(int pid)
    {
        lock (inputLock) { return pid >= 1 && pid <= 2 ? playerNames[pid] : null; }
    }

    public bool IsFull
    {
        get { lock (inputLock) { return connected[1] && connected[2]; } }
    }

    public void GetPlayerInput(int pid, out bool l, out bool r, out bool j, out bool b)
    {
        lock (inputLock) { l = left[pid]; r = right[pid]; j = jump[pid]; b = boost[pid]; }
    }

    string Extract(string j, string k)
    {
        string s = "\"" + k + "\":\""; int i = j.IndexOf(s); if (i < 0) return ""; i += s.Length; int e = j.IndexOf("\"", i); return e > i ? j.Substring(i, e - i) : "";
    }
    int ExtractInt(string j, string k)
    {
        string s = "\"" + k + "\":"; int i = j.IndexOf(s); if (i < 0) return 0; i += s.Length; int e = i; while (e < j.Length && (char.IsDigit(j[e]) || j[e] == '-')) e++; int.TryParse(j.Substring(i, e - i), out int v); return v;
    }
    bool ExtractBool(string j, string k)
    {
        string s = "\"" + k + "\":"; int i = j.IndexOf(s); if (i < 0) return false; return j.Substring(i + s.Length, 4) == "true";
    }
}
