using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class LocalHttpServer : MonoBehaviour
{
    public int port = 8080;

    private TcpListener listener;
    private Thread serverThread;
    private volatile bool running;
    private string htmlContent;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);

        string path = Path.Combine(Application.streamingAssetsPath, "ControllerPage", "index.html");
        try
        {
            if (File.Exists(path))
                htmlContent = File.ReadAllText(path);
            else
                htmlContent = "<html><body><h1>Controller page not found</h1></body></html>";
        }
        catch
        {
            htmlContent = "<html><body><h1>Error loading page</h1></body></html>";
        }

        running = true;
        serverThread = new Thread(ServerLoop);
        serverThread.IsBackground = true;
        serverThread.Start();
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
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Debug.Log("[HTTP] Server on port " + port);

            while (running)
            {
                var c = listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(HandleClient, c);
            }
        }
        catch (Exception e)
        {
            if (running && !(e is ThreadAbortException))
                Debug.LogError("[HTTP] " + e.Message);
        }
    }

    void HandleClient(object obj)
    {
        try
        {
            using (var c = (TcpClient)obj)
            using (var s = c.GetStream())
            {
                byte[] buf = new byte[4096];
                int total = 0;
                while (total < buf.Length)
                {
                    int r = s.Read(buf, total, Math.Min(512, buf.Length - total));
                    if (r <= 0) break;
                    total += r;
                    if (Encoding.UTF8.GetString(buf, 0, total).Contains("\r\n\r\n")) break;
                }

                if (total == 0) return;

                byte[] body = Encoding.UTF8.GetBytes(htmlContent);
                string header = "HTTP/1.1 200 OK\r\n" +
                                "Content-Type: text/html; charset=utf-8\r\n" +
                                "Access-Control-Allow-Origin: *\r\n" +
                                "Content-Length: " + body.Length + "\r\n" +
                                "Connection: close\r\n\r\n";
                s.Write(Encoding.UTF8.GetBytes(header), 0, header.Length);
                s.Write(body, 0, body.Length);
            }
        }
        catch { }
    }
}
