using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace UnityLearning.Labs
{
    public sealed class LabPooledUnit : MonoBehaviour
    {
        public int Id;
        public float Remaining;
        public Vector3 Velocity;
    }
    public sealed class ObjectPoolFlowLab : LearningLabBase
    {
        public override string LabId => "pool";
        protected override string LabTitle => "对象池流程实验室 · 创建 / 发射 / 回收 / 复用";
        protected override string Guide => "实际 ObjectPool<LabPooledUnit> 管理场景中的 GameObject，最多 4 个活跃对象，最多保留 2 个空闲对象。\n流程：创建池 → Get 发射 → 推进运动／寿命 → Release → 再次 Get 复用 → 容量溢出销毁 → 清理池。\n代码解析：CreatePool 定义四个回调；Rent 在激活后初始化全部运动状态；Return 防止重复回收；Simulate 统一推进活跃对象；DisposeLab 先回收活跃对象，再 Clear 空闲池。";
        protected override string[] Phases => new[] { "创建池", "Get 发射", "推进模拟", "Release", "复用", "容量限制", "清理池" };
        protected override string[] SourceFiles => new[] { "ObjectPoolFlowLab.cs" };
        ObjectPool<LabPooledUnit> pool;
        readonly List<LabPooledUnit> active = new List<LabPooledUnit>();
        int created, destroyed, reused;
        bool running;
        LabPooledUnit last;
        UnityEngine.UIElements.Label details;
        protected override void InitializeLab()
        {
            details = Label(Dashboard, "", "body");
            Button(Dashboard, "发射一个", () => { if (pool != null) Rent(); });
            Button(Dashboard, "回收最后一个", () => { if (active.Count > 0) Return(active[active.Count - 1]); });
            Button(Dashboard, "连续模拟 / 暂停", () => running = !running);
            ResetLab();
        }
        protected override void ResetLab()
        {
            DisposeLab(); ClearObjects(); created = destroyed = reused = 0; last = null;
            SetPhase(-1, "先创建池；发射后可连续模拟，2 秒寿命结束自动回收。"); Refresh();
        }
        void CreatePool()
        {
            pool = new ObjectPool<LabPooledUnit>(() =>
            {
                var go = Cube("Pooled Unit", Vector3.zero, new Color(.9f, .55f, .2f)); go.SetActive(false);
                var unit = go.AddComponent<LabPooledUnit>(); unit.Id = ++created; return unit;
            }, unit => { active.Add(unit); unit.gameObject.SetActive(true); },
            unit => { active.Remove(unit); unit.Velocity = Vector3.zero; unit.Remaining = 0; unit.gameObject.SetActive(false); },
            unit => { destroyed++; Destroy(unit.gameObject); }, true, 2, 2);
            Log("ObjectPool 构建：默认容器容量 2；最多空闲 2。这里没有预创建对象。");
        }
        LabPooledUnit Rent()
        {
            if (active.Count >= 4) { Log("活跃上限 4，拒绝本次发射。"); return null; }
            bool reuse = pool.CountInactive > 0;
            var unit = pool.Get(); if (reuse) reused++;
            unit.Remaining = 2; unit.Velocity = Vector3.right * 3;
            unit.transform.position = new Vector3(-3, 0, (unit.Id % 4 - 1.5f) * 1.5f);
            unit.transform.rotation = Quaternion.identity;
            last = unit; Log("Get 对象 #" + unit.Id + (reuse ? "（复用）" : "（新建）") + " → 重置位置、速度、寿命。"); Refresh(); return unit;
        }
        void Return(LabPooledUnit unit)
        {
            if (unit == null || !active.Contains(unit)) { Log("忽略重复回收。"); return; }
            int id = unit.Id; pool.Release(unit); Log("Release #" + id + " → 清空速度／计时 → 失活 → 空闲或销毁。"); Refresh();
        }
        void Simulate(float dt)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var unit = active[i]; unit.transform.position += unit.Velocity * dt; unit.Remaining -= dt;
                if (unit.Remaining <= 0) Return(unit);
            }
        }
        void Refresh() { details.text = "累计创建 " + created + " · 复用 " + reused + " · 销毁 " + destroyed + " · 活跃 " + active.Count + " · 空闲 " + (pool == null ? 0 : pool.CountInactive); }
        protected override void TickLab() { if (running && pool != null) Simulate(Time.deltaTime); }
        protected override void StepLab()
        {
            int next = Phase + 1;
            switch (next)
            {
                case 0: CreatePool(); break;
                case 1: Rent(); Rent(); break;
                case 2: Simulate(.5f); Log("每个对象 position += velocity * dt，remaining -= dt。"); break;
                case 3: if (last != null) Return(last); break;
                case 4: Rent(); break;
                case 5: for (int i = 0; i < 5; i++) Rent(); break;
                case 6: DisposeLab(); Refresh(); break;
                default: return;
            }
            SetPhase(next, Phases[next]);
        }
        protected override IEnumerator ExecuteTests()
        {
            ResetLab(); CreatePool(); Check(created == 0, "defaultCapacity 不等于预创建");
            var unit = Rent(); Check(active.Count == 1 && created == 1, "Get 创建并激活");
            Simulate(.25f); Check(Mathf.Abs(unit.transform.position.x + 2.25f) < .001f, "实际场景对象运动");
            Return(unit); Check(pool.CountInactive == 1 && !unit.gameObject.activeSelf, "Release 失活并入池");
            var reusedUnit = Rent(); Check(ReferenceEquals(unit, reusedUnit) && created == 1, "再次 Get 复用同一对象");
            Check(reusedUnit.Remaining == 2 && reusedUnit.Velocity == Vector3.right * 3, "复用重置速度和寿命");
            Return(unit); Return(unit); Check(pool.CountInactive == 1, "重复回收不会重复入池");
            for (int i = 0; i < 5; i++) Rent(); Check(active.Count == 4, "额外控制活跃数量上限");
            Simulate(3); Check(active.Count == 0 && pool.CountInactive == 2 && destroyed == 2, "超时回收与空闲容量溢出销毁");
            DisposeLab(); Check(pool == null && active.Count == 0, "先回收活跃对象，再清理池");
            CreatePool(); Rent(); Check(active.Count == 1, "清理后可重新开始"); SetPhase(1, "测试完成，可手动发射与回收。");
            yield return null;
        }
        protected override void DisposeLab()
        {
            running = false;
            if (pool == null) return;
            for (int i = active.Count - 1; i >= 0; i--) pool.Release(active[i]);
            pool.Clear(); pool = null;
        }
    }
}
