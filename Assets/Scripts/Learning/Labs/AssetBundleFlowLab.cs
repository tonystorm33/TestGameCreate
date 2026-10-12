using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityLearning.Labs
{
    [Serializable] public sealed class LabPackageVersion
    {
        public int version;
        public string file;
        public string hash;
        public uint crc;
        public string asset;
    }
    [Serializable] public sealed class LabPackageManifest
    {
        public string platform;
        public LabPackageVersion[] versions;
    }
    public sealed class AssetBundleFlowLab : LearningLabBase
    {
        public override string LabId => "hotupdate";
        protected override string LabTitle => "资源热更新流程实验室 · HTTP / AssetBundle / 失败回退";
        protected override string Guide => "本机随机端口 HTTP 服务提供实际构建的 V1 / V2 AssetBundle，无需外部服务器。\n流程：读取构建清单 → 下载到新事务 → CRC 校验 → LoadAssetAsync → 创建新实例 → 替换旧实例 → 释放旧包。失败时保留旧实例和版本。\n代码解析：PrepareServer 管理包服务；ApplyVersion 负责下载、校验和提交；CancelTransaction 清理未提交资源；DisposeLab 负责退出。这里演示的是资源更新，不是 C# 代码热更新。目标为 Windows Editor / Standalone，本机 HTTP 仅用于可复现教学。";
        protected override string[] Phases => new[] { "读取清单", "启动 HTTP", "下载 V1", "提交 V2", "404 回退", "CRC 回退", "退出释放" };
        protected override string[] SourceFiles => new[] { "AssetBundleFlowLab.cs", "LocalPackageServer.cs", "LabContentBuilder.cs" };
        LocalPackageServer server;
        LabPackageManifest manifest;
        AssetBundle currentBundle, staging;
        GameObject currentView;
        UnityWebRequest pending;
        Coroutine update;
        UnityEngine.UIElements.Label details;
        public bool Busy { get; private set; }
        public int AppliedVersion { get; private set; }
        public int Failures { get; private set; }
        public int Downloads { get; private set; }
        string PackageRoot => Path.Combine(Application.streamingAssetsPath, "LearningLabPackages");
        bool ready;

        protected override void InitializeLab()
        {
            details = Label(Dashboard, "", "body");
            Button(Dashboard, "更新到 V1", () => BeginUpdate(1));
            Button(Dashboard, "更新到 V2", () => BeginUpdate(2));
            Button(Dashboard, "404 失败", () => BeginUpdate(2, "missing"));
            Button(Dashboard, "CRC 失败", () => BeginUpdate(2, "crc"));
            Button(Dashboard, "取消下载", CancelTransaction);
            ResetLab();
        }
        protected override void ResetLab()
        {
            DisposeLab(); ClearObjects(); AppliedVersion = Failures = Downloads = 0;
            ready = false; manifest = null;
            SetPhase(-1, "先执行读取清单，或运行全部测试。首次准备资源请用 Tools / Unity Learning / Build Lab Content。");
            Refresh("未读取资源清单");
        }
        bool ReadManifest()
        {
            string path = Path.Combine(PackageRoot, "manifest.json");
            if (!File.Exists(path)) { Log("缺少构建包。请退出 Play 后执行 Build Lab Content。"); return false; }
            manifest = JsonUtility.FromJson<LabPackageManifest>(File.ReadAllText(path));
            if (manifest == null || manifest.platform != "StandaloneWindows64" || manifest.versions == null || manifest.versions.Length != 2)
            { Log("清单平台或版本不符合实验要求。"); return false; }
            ready = true; Log("读取版本清单：V1 / V2、hash、CRC、资源名。平台 " + manifest.platform); return true;
        }
        void PrepareServer()
        {
            if (!ready && !ReadManifest()) return;
            if (server != null) return;
            var files = new Dictionary<string, byte[]>();
            foreach (var entry in manifest.versions) files["/v" + entry.version] = File.ReadAllBytes(Path.Combine(PackageRoot, entry.file));
            server = new LocalPackageServer(files);
            Log("HTTP 服务启动：" + server.BaseUrl + "；只提供预定义包，外部路径不可访问。");
        }
        void BeginUpdate(int version, string fault = "")
        {
            if (Busy) { Log("已有事务进行中，拒绝并发更新。"); return; }
            if (!ready && !ReadManifest()) return;
            PrepareServer();
            update = StartCoroutine(ApplyVersion(version, fault));
        }
        IEnumerator ApplyVersion(int version, string fault = "")
        {
            if (AppliedVersion == version && string.IsNullOrEmpty(fault)) { Log("相同版本已提交，无需再次下载。"); yield break; }
            var entry = Array.Find(manifest.versions, v => v.version == version);
            if (entry == null) { Failures++; Log("未知版本"); yield break; }
            Busy = true;
            while (!Caching.ready) yield return null;
            // 故障用独立缓存键，保证 CRC 测试不会误用以前的有效缓存。
            Hash128 hash = fault == "crc" ? Hash128.Compute(entry.hash + "invalid-crc") : Hash128.Parse(entry.hash);
            uint crc = fault == "crc" ? entry.crc ^ 0xffffffffu : entry.crc;
            string url = server.BaseUrl + (fault == "missing" ? "/missing" : "/v" + version);
            pending = UnityWebRequestAssetBundle.GetAssetBundle(url, hash, crc); pending.timeout = 8; Downloads++;
            Log("GetAssetBundle(" + url + ", hash, crc) → SendWebRequest");
            yield return pending.SendWebRequest();
            if (pending.result != UnityWebRequest.Result.Success)
            {
                Failures++; Log("下载／校验失败：" + pending.error + "；保留 V" + AppliedVersion);
                pending.Dispose(); pending = null; Busy = false; Refresh("更新失败，旧版本保留"); yield break;
            }
            staging = DownloadHandlerAssetBundle.GetContent(pending); pending.Dispose(); pending = null;
            if (staging == null) { Failures++; Busy = false; Refresh("包读取失败"); yield break; }
            var load = staging.LoadAssetAsync<GameObject>(entry.asset);
            yield return load;
            var prefab = load.asset as GameObject;
            if (prefab == null)
            {
                staging.Unload(true); staging = null; Failures++; Busy = false; Log("资源名错误，未提交。"); yield break;
            }
            var replacement = Instantiate(prefab, Objects);
            replacement.transform.position = Vector3.zero;
            if (currentView != null) { currentView.SetActive(false); Destroy(currentView); }
            if (currentBundle != null) currentBundle.Unload(true);
            currentView = replacement; currentBundle = staging; staging = null;
            AppliedVersion = version; Busy = false;
            Log("LoadAssetAsync → Instantiate → 提交 V" + version + " → 释放旧包（示例包无共享依赖）。");
            Refresh("V" + version + " 已生效");
        }
        void Refresh(string message)
        {
            details.text = message + "\n当前版本 V" + AppliedVersion + " · 事务 " + Busy + " · 下载调用 " + Downloads + " · 失败 " + Failures +
                "\nHTTP 服务 " + (server == null ? "未启动" : server.BaseUrl);
        }
        protected override void StepLab()
        {
            if (Busy) { Log("等待当前下载／加载完成后再执行下一步。"); return; }
            int next = Phase + 1;
            switch (next)
            {
                case 0: if (!ReadManifest()) return; break;
                case 1: PrepareServer(); break;
                case 2: BeginUpdate(1); break;
                case 3: BeginUpdate(2); break;
                case 4: BeginUpdate(1, "missing"); break;
                case 5: BeginUpdate(1, "crc"); break;
                case 6: DisposeLab(); Refresh("资源和服务已释放"); break;
                default: Log("流程结束；可重置后再次执行。"); return;
            }
            SetPhase(next, Phases[next]);
        }
        protected override void TickLab() { if (pending != null) Refresh("下载进度 " + Mathf.RoundToInt(pending.downloadProgress * 100) + "%"); }
        protected override IEnumerator ExecuteTests()
        {
            ResetLab(); bool loaded = ReadManifest(); Check(loaded, "读取真实构建清单"); if (!loaded) yield break;
            PrepareServer(); Check(server != null, "本机 HTTP 服务启动");
            yield return ApplyVersion(1);
            Check(AppliedVersion == 1 && currentView != null, "HTTP 下载 V1 → CRC 校验 → 加载 → 提交");
            int downloads = Downloads; yield return ApplyVersion(1);
            Check(Downloads == downloads, "重复版本不重复下载");
            yield return ApplyVersion(2);
            Check(AppliedVersion == 2 && currentView != null && currentView.name.Contains("V2"), "V2 实际 Prefab 替换 V1");
            var old = currentView; int failures = Failures;
            yield return ApplyVersion(1, "missing");
            Check(AppliedVersion == 2 && currentView == old && Failures == failures + 1, "HTTP 404 保留当前版本与实例");
            failures = Failures; yield return ApplyVersion(1, "crc");
            Check(AppliedVersion == 2 && currentView == old && Failures == failures + 1, "错误 CRC 不提交新版本");
            BeginUpdate(1); CancelTransaction();
            Check(!Busy && staging == null && pending == null && AppliedVersion == 2, "取消事务清理临时资源，保留已提交版本");
            DisposeLab(); Check(server == null && currentBundle == null && currentView == null, "退出释放 HTTP 与资源包");
            PrepareServer(); yield return ApplyVersion(1); Check(AppliedVersion == 1, "释放后可重新运行完整流程");
            SetPhase(2, "测试结束，可手动切换资源版本。");
        }
        void CancelTransaction()
        {
            if (pending != null) { pending.Abort(); pending.Dispose(); pending = null; }
            if (update != null) StopCoroutine(update); update = null;
            if (staging != null) staging.Unload(true); staging = null; Busy = false;
            Log("CancelTransaction：停止请求／协程，释放未提交包。");
        }
        protected override void DisposeLab()
        {
            CancelTransaction(); server?.Dispose(); server = null;
            if (currentView != null) { currentView.SetActive(false); Destroy(currentView); currentView = null; }
            if (currentBundle != null) { currentBundle.Unload(true); currentBundle = null; }
            AppliedVersion = 0;
        }
    }
}
