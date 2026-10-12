using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityLearning
{
    // 示意对象池：复用同一个 UI 对象，便于直观看到 Get / Release。
    public class ObjectPoolPresentation : KnowledgePresentation
    {
        readonly Stack<Label> idle = new Stack<Label>();
        readonly List<Label> active = new List<Label>();
        VisualElement idleArea, activeArea;
        Label status;
        Button get, release;
        StepPresentation theory;
        int created, reused;
        const int Capacity = 8;

        protected override void OnEnter()
        {
            created = reused = 0;
            Host.Add(Text("动手实验 · 取出、回收，再次复用", "section-title"));
            Host.Add(Text("每颗小球都是同一个可复用的 UI 元素。先取出几颗，再回收，观察编号与创建次数。", "body"));
            var lanes = new VisualElement();
            lanes.AddToClassList("pool-lanes");
            idleArea = Lane(lanes, "休息区 · 可复用");
            activeArea = Lane(lanes, "工作区 · 使用中");
            Host.Add(lanes);
            status = Text("", "pool-status");
            Host.Add(status);
            var controls = new VisualElement();
            controls.AddToClassList("controls");
            get = Action("取出 Get", Take);
            release = Action("回收 Release", Return, "secondary");
            controls.Add(get);
            controls.Add(release);
            controls.Add(Action("重置实验", Reset, "secondary"));
            Host.Add(controls);
            Host.Add(Text("真实项目中，Get 时重置位置、生命值等状态；Release 时关闭对象、清理订阅与计时器。池容量和销毁策略需按项目设计。", "body"));
            Host.Add(Text("var item = pool.Get();\n// 使用 item …\npool.Release(item);", "code"));
            Refresh("等待操作");
            Host.Add(Text("对象池系统课程 · 原理、场景与完整实例", "section-title"));
            var lesson = new VisualElement();
            Host.Add(lesson);
            theory = new StepPresentation();
            theory.Enter(lesson, Topic);
        }

        VisualElement Lane(VisualElement parent, string title)
        {
            var lane = new VisualElement();
            lane.AddToClassList("pool-lane");
            lane.Add(Text(title, "eyebrow"));
            var balls = new VisualElement();
            balls.AddToClassList("balls");
            lane.Add(balls);
            parent.Add(lane);
            return balls;
        }

        void Take()
        {
            if (active.Count >= Capacity) return;
            Label ball;
            bool reuse = idle.Count > 0;
            if (reuse) { ball = idle.Pop(); reused++; }
            else { ball = Text((++created).ToString("00"), "ball"); }
            ball.RemoveFromHierarchy();
            ball.EnableInClassList("working", true);
            activeArea.Add(ball);
            active.Add(ball);
            Refresh(reuse ? "复用了小球 " + ball.text : "新建了小球 " + ball.text);
        }

        void Return()
        {
            if (active.Count == 0) return;
            var ball = active[active.Count - 1];
            active.RemoveAt(active.Count - 1);
            ball.RemoveFromHierarchy();
            ball.EnableInClassList("working", false);
            idleArea.Add(ball);
            idle.Push(ball);
            Refresh("小球 " + ball.text + " 已回收，等待再次使用");
        }

        void Refresh(string message)
        {
            status.text = message + "\n累计创建 " + created + "  ·  复用 " + reused + "  ·  使用中 " + active.Count + "  ·  空闲 " + idle.Count;
            get.SetEnabled(active.Count < Capacity);
            release.SetEnabled(active.Count > 0);
        }

        void Reset()
        {
            idleArea.Clear(); activeArea.Clear(); idle.Clear(); active.Clear();
            created = reused = 0;
            Refresh("实验已重置");
        }

        protected override void OnExit()
        {
            if (theory != null) theory.Exit();
            theory = null;
            idle.Clear(); active.Clear();
        }
    }
}
