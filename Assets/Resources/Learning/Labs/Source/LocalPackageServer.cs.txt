using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace UnityLearning.Labs
{
    // 无 URL ACL 的本机 HTTP 教学服务。仅暴露预先列出的文件，不读取任意路径。
    public sealed class LocalPackageServer : IDisposable
    {
        readonly TcpListener listener;
        readonly IDictionary<string, byte[]> files;
        readonly ConcurrentDictionary<TcpClient, byte> clients = new ConcurrentDictionary<TcpClient, byte>();
        readonly Thread thread;
        volatile bool disposed;
        public string BaseUrl { get; private set; }
        public LocalPackageServer(IDictionary<string, byte[]> files)
        {
            this.files = files;
            listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            BaseUrl = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
            thread = new Thread(() =>
            {
                while (!disposed)
                {
                    try
                    {
                        var client = listener.AcceptTcpClient();
                        if (disposed) { client.Dispose(); break; }
                        clients.TryAdd(client, 0);
                        ThreadPool.QueueUserWorkItem(_ => Serve(client));
                    }
                    catch (SocketException) { if (disposed) break; }
                    catch (ObjectDisposedException) { break; }
                }
            }) { IsBackground = true, Name = "Learning AssetBundle HTTP" };
            thread.Start();
        }
        void Serve(TcpClient client)
        {
            try
            {
                client.ReceiveTimeout = 3000; client.SendTimeout = 3000;
                var stream = client.GetStream(); var header = new StringBuilder(); int previous = 0;
                while (header.Length < 8192)
                {
                    int next = stream.ReadByte(); if (next < 0) return;
                    header.Append((char)next);
                    if (next == '\n' && previous == '\n') break;
                    if (header.ToString().EndsWith("\r\n\r\n")) break;
                    previous = next;
                }
                string[] request = header.ToString().Split('\n')[0].Trim().Split(' ');
                byte[] body = null;
                bool found = request.Length >= 2 && request[0] == "GET" && files.TryGetValue(request[1].Split('?')[0], out body);
                if (!found) body = Encoding.UTF8.GetBytes("Not found");
                string response = "HTTP/1.1 " + (found ? "200 OK" : "404 Not Found") + "\r\nContent-Type: application/octet-stream\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n";
                byte[] prefix = Encoding.ASCII.GetBytes(response); stream.Write(prefix, 0, prefix.Length);
                stream.Write(body, 0, body.Length); stream.Flush();
            }
            catch (Exception e) when (e is System.IO.IOException || e is SocketException || e is ObjectDisposedException) { }
            finally { clients.TryRemove(client, out _); client.Dispose(); }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; listener.Stop();
            foreach (var client in clients.Keys) client.Dispose();
            thread.Join(300);
        }
    }
}
