using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityLearning.Labs
{
    [Serializable]
    public sealed class SyncMessage
    {
        public string kind;
        public int sequence;
        public bool value;
    }

    public sealed class NetworkSyncFlowLab : LearningLabBase
    {
        public override string LabId => "network";
        protected override string LabTitle => "网络同步流程实验室 · TCP 请求与服务器权威";
        protected override string Guide => "同一进程中的两个真实 TCP 端点（127.0.0.1，随机端口），左侧为服务端权威状态，右侧为客户端显示。\n流程：建立连接 → 初始快照 → 客户端请求 → 服务端校验 → 回传版本化快照 → 拒绝非法操作 → 断开释放。\n代码解析：LoopbackTransport 只搬运字节；SendCommand 生成请求；Pump 验证并修改权威状态；ApplySnapshot 拒绝旧版本。教学延迟发生在应用层，TCP 本身是可靠有序传输。此实验不使用 NGO，也不模拟完整生产鉴权或预测。";
        protected override string[] Phases => new[] { "TCP 连接", "初始快照", "发送请求", "权威更新", "同步显示", "拒绝非法操作", "断开连接" };
        protected override string[] SourceFiles => new[] { "NetworkSyncFlowLab.cs", "LoopbackTransport.cs" };
        LoopbackTransport transport;
        GameObject serverView, clientView;
        UnityEngine.UIElements.Label details;
        bool serverValue, clientValue;
        int version, clientVersion = -1, requestSequence, lastRequest;
        int accepted, rejected, stale;
        readonly Queue<Tuple<float, string>> snapshots = new Queue<Tuple<float, string>>();
        public float DelaySeconds { get; set; } = .25f;

        protected override void InitializeLab()
        {
            details = Label(Dashboard, "", "body");
            Button(Dashboard, "客户端请求切换", () => SendCommand("toggle"));
            Button(Dashboard, "发送非法操作", () => SendCommand("grant-reward"));
            Button(Dashboard, "延迟 0 / 1 秒", () => { DelaySeconds = DelaySeconds == 0 ? 1 : 0; Refresh(); });
            ResetLab();
        }
        protected override void ResetLab()
        {
            DisposeLab(); ClearObjects();
            serverValue = clientValue = false; version = 0; clientVersion = -1;
            requestSequence = lastRequest = accepted = rejected = stale = 0;
            serverView = Cube("Server authoritative state", new Vector3(-2, 0, 0), Color.red);
            clientView = Cube("Client replicated state", new Vector3(2, 0, 0), Color.gray);
            SetPhase(-1, "先建立连接；随后可反复请求切换。"); Refresh();
        }
        void Connect()
        {
            clientVersion = -1; clientValue = false;
            if (clientView != null) clientView.GetComponent<Renderer>().sharedMaterial.color = Color.gray;
            transport = new LoopbackTransport();
            Log("TcpListener.Start → TcpClient.Connect(127.0.0.1:" + transport.Port + ") → AcceptTcpClient。");
        }
        void SendCommand(string kind)
        {
            if (transport == null || !transport.Connected) { Log("连接尚未就绪。"); return; }
            var command = new SyncMessage { kind = kind, sequence = ++requestSequence };
            transport.SendRequest(JsonUtility.ToJson(command));
            Log("Client Write：" + JsonUtility.ToJson(command));
        }
        void QueueSnapshot()
        {
            var snapshot = new SyncMessage { kind = "snapshot", sequence = version, value = serverValue };
            snapshots.Enqueue(Tuple.Create(Time.unscaledTime + DelaySeconds, JsonUtility.ToJson(snapshot)));
        }
        void ApplySnapshot(SyncMessage message)
        {
            if (message.sequence <= clientVersion) { stale++; Log("Client 忽略重复／旧快照 v" + message.sequence); return; }
            clientVersion = message.sequence; clientValue = message.value;
            Paint(clientView, clientValue); Log("Client ApplySnapshot：v" + clientVersion + " = " + clientValue);
        }
        void Pump()
        {
            if (transport == null) return;
            while (transport.Errors.TryDequeue(out string error)) Log("TCP：" + error);
            while (transport.ServerInbox.TryDequeue(out string json))
            {
                var request = JsonUtility.FromJson<SyncMessage>(json);
                if (request.kind != "toggle" || request.sequence <= lastRequest)
                { rejected++; Log("Server 拒绝非法或重放请求：" + json); continue; }
                lastRequest = request.sequence; accepted++; version++; serverValue = !serverValue;
                Paint(serverView, serverValue); QueueSnapshot();
                Log("Server 校验成功 → 修改权威状态 → 生成快照 v" + version);
            }
            while (snapshots.Count > 0 && snapshots.Peek().Item1 <= Time.unscaledTime)
                transport.SendSnapshot(snapshots.Dequeue().Item2);
            while (transport.ClientInbox.TryDequeue(out string json))
                ApplySnapshot(JsonUtility.FromJson<SyncMessage>(json));
            Refresh();
        }
        static void Paint(GameObject view, bool value) { if (view != null) view.GetComponent<Renderer>().sharedMaterial.color = value ? Color.green : Color.red; }
        void Refresh()
        {
            details.text = "左 / Server: " + serverValue + " v" + version + "  |  右 / Client: " + clientValue + " v" + clientVersion +
                "\n连接 " + (transport != null && transport.Connected) + " · 接受 " + accepted + " · 拒绝 " + rejected + " · 旧快照 " + stale + " · 教学延迟 " + DelaySeconds + "s";
        }
        protected override void TickLab() { Pump(); }
        protected override void StepLab()
        {
            int next = Phase + 1;
            switch (next)
            {
                case 0: Connect(); break;
                case 1: QueueSnapshot(); break;
                case 2: SendCommand("toggle"); break;
                case 3: Pump(); Log("服务端收包由主线程队列消费；可以观察 v0 → v1。"); break;
                case 4: Pump(); Log("客户端收到快照后刷新颜色。"); break;
                case 5: SendCommand("grant-reward"); break;
                case 6: DisposeLab(); Log("Dispose：关闭两个套接字、停止监听、结束后台读线程。"); break;
                default: Log("流程已完成，请重置后重连。"); return;
            }
            SetPhase(next, Phases[next]); Refresh();
        }
        IEnumerator WaitUntilState(Func<bool> condition, float timeout = 4)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
        }
        protected override IEnumerator ExecuteTests()
        {
            ResetLab(); DelaySeconds = .05f; Connect();
            yield return WaitUntilState(() => transport.Connected);
            Check(transport.Connected, "真实 TCP 连接建立");
            QueueSnapshot(); yield return WaitUntilState(() => clientVersion == 0);
            Check(clientVersion == 0 && !clientValue, "初始状态同步");
            SendCommand("toggle"); yield return WaitUntilState(() => clientVersion == 1);
            Check(version == 1 && clientValue && serverValue, "请求 → 校验 → 权威更新 → 客户端应用");
            SendCommand("grant-reward"); yield return WaitUntilState(() => rejected == 1);
            Check(rejected == 1 && version == 1, "非法操作不修改状态");
            transport.SendRequest(JsonUtility.ToJson(new SyncMessage { kind = "toggle", sequence = 1 }));
            yield return WaitUntilState(() => rejected == 2);
            Check(rejected == 2 && version == 1, "重复请求被拒绝");
            transport.SendSnapshot(JsonUtility.ToJson(new SyncMessage { kind = "snapshot", sequence = 0, value = false }));
            yield return WaitUntilState(() => stale > 0);
            Check(clientValue && clientVersion == 1 && stale > 0, "旧快照不能覆盖新状态");
            DelaySeconds = .3f; SendCommand("toggle"); yield return WaitUntilState(() => version == 2);
            Check(clientVersion == 1, "延迟窗口中客户端保留已确认状态");
            yield return WaitUntilState(() => clientVersion == 2);
            Check(!clientValue && !serverValue, "延迟后最终一致");
            DisposeLab(); Check(transport == null && snapshots.Count == 0, "断开释放连接和待发送队列");
            Connect(); yield return WaitUntilState(() => transport.Connected); QueueSnapshot(); yield return WaitUntilState(() => clientVersion == 2);
            Check(transport.Connected && clientVersion == version, "重连恢复当前权威状态");
            SetPhase(4, "测试完成，可手动切换两端状态。");
        }
        protected override void DisposeLab() { transport?.Dispose(); transport = null; snapshots.Clear(); }
    }
}
