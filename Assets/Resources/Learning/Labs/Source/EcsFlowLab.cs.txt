using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace UnityLearning.Labs
{
    // 数据：与 GameObject 表现分离；实体拥有数据，System 统一处理。
    public struct LabPosition : IComponentData { public Vector3 Value; }
    public struct LabSpeed : IComponentData { public float Value; }
    public struct LabFrozen : IComponentData { }

    [DisableAutoCreation]
    public partial class LabMoveSystem : SystemBase
    {
        EntityQuery moving;
        protected override void OnCreate()
        {
            moving = GetEntityQuery(ComponentType.ReadWrite<LabPosition>(),
                ComponentType.ReadOnly<LabSpeed>(), ComponentType.Exclude<LabFrozen>());
        }
        protected override void OnUpdate()
        {
            // 教学版本使用临时实体数组便于逐行观察，并非性能基准。
            using (var entities = moving.ToEntityArray(Allocator.Temp))
                foreach (var entity in entities)
                {
                    var position = EntityManager.GetComponentData<LabPosition>(entity);
                    position.Value.x += EntityManager.GetComponentData<LabSpeed>(entity).Value * World.Time.DeltaTime;
                    if (position.Value.x > 4) position.Value.x = -4;
                    EntityManager.SetComponentData(entity, position);
                }
        }
    }

    public sealed class EcsFlowLab : LearningLabBase
    {
        public override string LabId => "ecs";
        protected override string LabTitle => "ECS 流程实验室 · 真实 Entity / Component / System";
        protected override string Guide => "单步顺序：创建 World → 创建实体与数据 → 查询 → 更新系统 → 添加冻结标签 → 销毁实体 → 释放 World。\n蓝色方块是实体数据的 GameObject 可视化桥接，不依赖 Entities Graphics。核心计算实际由 Entities 1.0.16 执行。\n代码解析：CreateWorld 管理所有权；Spawn 创建组件组合；Simulate 推进时间与 System；SyncViews 只读取结果。";
        protected override string[] Phases => new[] { "World", "实体 / 组件", "Query", "System.Update", "冻结标签", "DestroyEntity", "World.Dispose" };
        protected override string[] SourceFiles => new[] { "EcsFlowLab.cs" };
        World world;
        LabMoveSystem movement;
        readonly List<Entity> entities = new List<Entity>();
        readonly Dictionary<Entity, GameObject> views = new Dictionary<Entity, GameObject>();
        UnityEngine.UIElements.Label details;
        bool running;
        double elapsed;

        protected override void InitializeLab()
        {
            details = Label(Dashboard, "", "body");
            Button(Dashboard, "连续模拟 / 暂停", () => { if (world != null && entities.Count > 0) running = !running; });
            ResetLab();
        }
        protected override void ResetLab()
        {
            DisposeWorld(); ClearObjects(); entities.Clear(); views.Clear(); elapsed = 0;
            SetPhase(-1, "准备好后执行下一步；也可直接运行全部测试。");
            details.text = "World 未创建 · 实体数 0";
        }
        void CreateWorld()
        {
            world = new World("Learning Lab World");
            movement = world.CreateSystemManaged<LabMoveSystem>();
            Log("new World → CreateSystemManaged<LabMoveSystem>；不加入默认 PlayerLoop，由实验显式推进。");
        }
        void Spawn(int count)
        {
            var manager = world.EntityManager;
            var archetype = manager.CreateArchetype(typeof(LabPosition), typeof(LabSpeed));
            for (int i = 0; i < count; i++)
            {
                Entity entity = manager.CreateEntity(archetype);
                var position = new Vector3(-3 + i % 4 * 2, 0, (i / 4 - .5f) * 2);
                manager.SetComponentData(entity, new LabPosition { Value = position });
                manager.SetComponentData(entity, new LabSpeed { Value = 2 + .25f * i });
                entities.Add(entity); views[entity] = Cube("Entity " + entity.Index, position, new Color(.35f, .65f, .9f));
            }
            Log("CreateArchetype → CreateEntity → SetComponentData；创建 " + count + " 个实体。");
            SyncViews();
        }
        void Simulate(float delta)
        {
            if (world == null || !world.IsCreated) return;
            elapsed += delta;
            world.SetTime(new Unity.Core.TimeData(elapsed, delta));
            movement.Update();
            SyncViews();
        }
        void SyncViews()
        {
            if (world == null) return;
            foreach (var pair in views)
                if (world.EntityManager.Exists(pair.Key)) pair.Value.transform.position = world.EntityManager.GetComponentData<LabPosition>(pair.Key).Value;
            details.text = "World: " + world.Name + " · 实体 " + entities.Count + " · 模拟时间 " + elapsed.ToString("F2") + "\n查询条件：LabPosition + LabSpeed，排除 LabFrozen。冻结会改变组件组合。";
        }
        protected override void StepLab()
        {
            int next = Phase + 1;
            switch (next)
            {
                case 0: CreateWorld(); break;
                case 1: Spawn(8); break;
                case 2:
                    using (var query = world.EntityManager.CreateEntityQuery(typeof(LabPosition), typeof(LabSpeed)))
                        Log("Query 匹配数量 = " + query.CalculateEntityCount());
                    break;
                case 3: Simulate(.5f); Log("SetTime(dt=0.5) → System.Update → SetComponentData → SyncViews"); break;
                case 4: world.EntityManager.AddComponent<LabFrozen>(entities[0]); Simulate(.5f); Log("实体 0 添加 LabFrozen，不再匹配移动查询。"); break;
                case 5:
                    var last = entities[entities.Count - 1]; world.EntityManager.DestroyEntity(last);
                    views[last].SetActive(false); Destroy(views[last]); views.Remove(last); entities.Remove(last); SyncViews();
                    Log("DestroyEntity；旧句柄 Exists=false；对应表现对象清理。"); break;
                case 6: DisposeWorld(); ClearObjects(); entities.Clear(); views.Clear(); details.text = "World 已释放，实体和系统全部结束。"; break;
                default: Log("流程完成；重置后可以再执行。"); return;
            }
            SetPhase(next, Phases[next]);
        }
        protected override void TickLab() { if (running) Simulate(Time.deltaTime); }
        protected override IEnumerator ExecuteTests()
        {
            ResetLab(); CreateWorld(); Spawn(8);
            Check(world.IsCreated && entities.Count == 8, "创建 World、实体与组件");
            var manager = world.EntityManager; Entity first = entities[0];
            Vector3 start = manager.GetComponentData<LabPosition>(first).Value;
            Simulate(.5f);
            Check(Mathf.Abs(manager.GetComponentData<LabPosition>(first).Value.x - start.x - 1) < .001f, "System 正确积分 speed × dt");
            manager.AddComponent<LabFrozen>(first); start = manager.GetComponentData<LabPosition>(first).Value; Simulate(.5f);
            Check(manager.GetComponentData<LabPosition>(first).Value == start, "冻结标签排除查询");
            manager.RemoveComponent<LabFrozen>(first); Simulate(.5f);
            Check(manager.GetComponentData<LabPosition>(first).Value != start, "移除冻结标签后继续移动");
            Check(views[first].transform.position == manager.GetComponentData<LabPosition>(first).Value, "表现桥接与组件数据一致");
            Entity old = entities[7]; manager.DestroyEntity(old);
            Check(!manager.Exists(old), "销毁实体后句柄失效");
            var oldWorld = world; DisposeWorld(); Check(!oldWorld.IsCreated, "退出释放自有 World");
            ResetLab(); CreateWorld(); Spawn(8); Simulate(.1f); Check(entities.Count == 8, "重复进入无残留实体");
            SetPhase(3, "测试完成，可连续模拟观察实体运动。");
            yield return null;
        }
        void DisposeWorld() { running = false; if (world != null && world.IsCreated) world.Dispose(); world = null; movement = null; }
        protected override void DisposeLab() { DisposeWorld(); }
    }
}
