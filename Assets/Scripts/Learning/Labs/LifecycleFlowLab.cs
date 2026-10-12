using System;
using System.Collections;
using UnityEngine;

namespace UnityLearning.Labs
{
    public sealed class LabLifeActor : MonoBehaviour
    {
        public Action<string> Report;
        public int Awakes, Enables, Starts, Disables, Destroys, Pulses, Updates, Heartbeats;
        public Action Pulse;
        Coroutine heartbeat;
        void Awake() { Awakes++; Report?.Invoke("Awake：自身初始化一次"); }
        void OnEnable()
        {
            Enables++; Pulse += Receive; heartbeat = StartCoroutine(Heartbeat());
            Report?.Invoke("OnEnable：订阅事件、启动心跳");
        }
        void Start() { Starts++; Report?.Invoke("Start：首次更新前一次"); }
        void Update() { Updates++; transform.Rotate(Vector3.up, 60 * Time.deltaTime); }
        IEnumerator Heartbeat() { while (true) { yield return new WaitForSecondsRealtime(.2f); Heartbeats++; } }
        void Receive() { Pulses++; Report?.Invoke("事件响应：第 " + Pulses + " 次"); }
        void OnDisable()
        {
            Disables++; Pulse -= Receive; if (heartbeat != null) StopCoroutine(heartbeat); heartbeat = null;
            Report?.Invoke("OnDisable：解除订阅、停止心跳");
        }
        void OnDestroy() { Destroys++; Report?.Invoke("OnDestroy：最终清理"); }
    }
    public sealed class LifecycleFlowLab : LearningLabBase
    {
        public override string LabId => "lifecycle";
        protected override string LabTitle => "生命周期流程实验室 · 真正启用、禁用与销毁";
        protected override string Guide => "创建不活跃对象后激活，实际观察 Awake / OnEnable / Start；方块旋转来自 Update。\n每 0.2 秒心跳来自协程。禁用组件后心跳停止、事件解绑；重新启用后不会重复 Awake / Start。\n代码解析：LabLifeActor 是被观察组件，LifecycleFlowLab 负责发起操作和验证。Report 将真实回调写入面板，不是手工生成的生命周期顺序。";
        protected override string[] Phases => new[] { "创建不活跃对象", "激活", "首次更新", "发出事件", "禁用组件", "重新启用", "销毁" };
        protected override string[] SourceFiles => new[] { "LifecycleFlowLab.cs" };
        LabLifeActor actor;
        UnityEngine.UIElements.Label details;
        protected override void InitializeLab() { details = Label(Dashboard, "", "body"); ResetLab(); }
        protected override void ResetLab() { DisposeLab(); ClearObjects(); SetPhase(-1, "单步触发真实引擎回调。"); details.text = "尚未创建观察对象"; }
        void CreateActor()
        {
            var go = Cube("Lifecycle Actor", Vector3.zero, new Color(.6f, .45f, .9f)); go.SetActive(false);
            actor = go.AddComponent<LabLifeActor>(); actor.Report = Log;
            Log("CreatePrimitive → SetActive(false) → AddComponent；组件延迟至激活初始化。");
        }
        protected override void StepLab()
        {
            int next = Phase + 1;
            switch (next)
            {
                case 0: CreateActor(); break;
                case 1: actor.gameObject.SetActive(true); break;
                case 2: Log("Start 和 Update 由下一引擎帧触发；观察计数与旋转。"); break;
                case 3: actor.Pulse?.Invoke(); break;
                case 4: actor.enabled = false; break;
                case 5: actor.enabled = true; break;
                case 6: Destroy(actor.gameObject); break;
                default: return;
            }
            SetPhase(next, Phases[next]);
        }
        protected override void TickLab()
        {
            if (actor == null) return;
            details.text = "Awake " + actor.Awakes + " / OnEnable " + actor.Enables + " / Start " + actor.Starts + " / OnDisable " + actor.Disables +
                "\nUpdate " + actor.Updates + " / 心跳 " + actor.Heartbeats + " / 事件响应 " + actor.Pulses;
        }
        protected override IEnumerator ExecuteTests()
        {
            ResetLab(); CreateActor(); Check(actor.Awakes == 0 && actor.Enables == 0, "不活跃对象延迟初始化");
            actor.gameObject.SetActive(true); yield return null; yield return null;
            Check(actor.Awakes == 1 && actor.Enables == 1 && actor.Starts == 1, "引擎触发首次生命周期");
            Check(actor.Updates > 0, "Update 真正执行");
            actor.Pulse?.Invoke(); Check(actor.Pulses == 1, "启用时事件有一个监听");
            actor.enabled = false; int updates = actor.Updates, heartbeats = actor.Heartbeats;
            actor.Pulse?.Invoke(); yield return new WaitForSecondsRealtime(.3f);
            Check(actor.Disables == 1 && actor.Updates == updates && actor.Heartbeats == heartbeats, "禁用停止 Update 与显式停止协程");
            Check(actor.Pulses == 1, "禁用时事件已解绑");
            actor.enabled = true; yield return null; actor.Pulse?.Invoke();
            Check(actor.Awakes == 1 && actor.Starts == 1 && actor.Enables == 2, "重新启用不重复 Awake / Start");
            Check(actor.Pulses == 2, "重新启用无重复订阅");
            var old = actor; Destroy(actor.gameObject); yield return null;
            Check(old.Destroys == 1 && old.Disables == 2, "销毁触发禁用和最终清理");
            CreateActor(); actor.gameObject.SetActive(true); yield return null; Check(actor.Awakes == 1, "重新创建实例计数独立");
            SetPhase(2, "测试完成，新观察对象正在运行。");
        }
        protected override void DisposeLab() { if (actor != null) { actor.Report = null; actor.gameObject.SetActive(false); Destroy(actor.gameObject); } actor = null; }
    }
}
