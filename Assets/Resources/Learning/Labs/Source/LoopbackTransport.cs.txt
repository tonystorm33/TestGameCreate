using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace UnityLearning.Labs
{
    // 两个真实 TCP 端点，换行分帧。后台线程只处理字节，Unity 主线程消费队列。
    public sealed class LoopbackTransport : IDisposable
    {
        public readonly ConcurrentQueue<string> ServerInbox = new ConcurrentQueue<string>();
        public readonly ConcurrentQueue<string> ClientInbox = new ConcurrentQueue<string>();
        public readonly ConcurrentQueue<string> Errors = new ConcurrentQueue<string>();
        readonly object gate = new object();
        readonly TcpListener listener;
        readonly TcpClient client;
        TcpClient server;
        Thread serverThread, clientThread;
        volatile bool disposed;
        public bool Connected { get { lock (gate) return !disposed && server != null; } }
        public int Port { get; private set; }

        public LoopbackTransport()
        {
            listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            serverThread = new Thread(() =>
            {
                try
                {
                    var accepted = listener.AcceptTcpClient(); accepted.NoDelay = true;
                    lock (gate) { if (disposed) { accepted.Dispose(); return; } server = accepted; }
                    Read(accepted, ServerInbox);
                }
                catch (Exception e) { if (!disposed) Errors.Enqueue(e.Message); }
            }) { IsBackground = true, Name = "Learning TCP Server" };
            serverThread.Start();
            client = new TcpClient { NoDelay = true }; client.Connect(IPAddress.Loopback, Port);
            clientThread = new Thread(() => Read(client, ClientInbox)) { IsBackground = true, Name = "Learning TCP Client" };
            clientThread.Start();
        }
        void Read(TcpClient endpoint, ConcurrentQueue<string> inbox)
        {
            try
            {
                using (var reader = new StreamReader(endpoint.GetStream(), Encoding.UTF8))
                    while (!disposed)
                    {
                        string line = reader.ReadLine();
                        if (line == null) break;
                        if (line.Length > 4096) { Errors.Enqueue("消息超过教学协议限制"); break; }
                        inbox.Enqueue(line);
                    }
            }
            catch (Exception e) { if (!disposed) Errors.Enqueue(e.Message); }
        }
        static void Write(TcpClient endpoint, string message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message + "\n");
            endpoint.GetStream().Write(bytes, 0, bytes.Length);
        }
        public void SendRequest(string json)
        { lock (gate) { if (disposed) throw new ObjectDisposedException("LoopbackTransport"); Write(client, json); } }
        public void SendSnapshot(string json)
        { lock (gate) { if (disposed || server == null) return; Write(server, json); } }
        public void Dispose()
        {
            lock (gate) { if (disposed) return; disposed = true; client.Dispose(); server?.Dispose(); listener.Stop(); }
            serverThread?.Join(300); clientThread?.Join(300);
        }
    }
}
