using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityLearning
{
    // 所有步骤来自配置；定时高亮和进度条构成轻量的知识动画。
    public class StepPresentation : KnowledgePresentation
    {
        readonly List<Button> nodes = new List<Button>();
        IVisualElementScheduledItem timer;
        Label heading, body, code, counter;
        VisualElement detail;
        ScrollView codeScroll;
        Button copy;
        Label usage, pitfalls, exercise;
        int interval = 15000;
        VisualElement fill;
        Button play, previous, next;
        int index;
        bool playing;

        protected override void OnEnter()
        {
            index = 0;
            interval = 15000;
            var steps = Topic.steps;
            if (steps == null || steps.Length == 0)
            {
                Host.Add(Text("内容正在准备中，请先为该知识配置 Steps。", "body"));
                return;
            }
            Host.Add(Text("学习目标\n" + Topic.learningGoals + "\n\n预备知识\n" + Topic.prerequisites, "learning-overview"));
            Host.Add(Text("章节目录 · 点击选择；长内容建议手动阅读", "eyebrow"));
            var flow = new VisualElement();
            flow.AddToClassList("flow");
            for (int i = 0; i < steps.Length; i++)
            {
                int selected = i;
                var node = Action((i + 1).ToString("00") + "  " + steps[i].title,
                    () => { Pause(); Show(selected); }, "step-node");
                nodes.Add(node);
                flow.Add(node);
            }
            Host.Add(flow);
            detail = new VisualElement();
            detail.AddToClassList("explanation");
            counter = Text("", "eyebrow");
            heading = Text("", "section-title");
            body = Text("", "body");
            code = Text("", "code");
            detail.Add(counter);
            detail.Add(heading);
            detail.Add(body);
            usage = Text("", "lesson-section");
            pitfalls = Text("", "lesson-warning");
            exercise = Text("", "lesson-exercise");
            detail.Add(usage);
            copy = Action("复制本节代码", () => UnityEngine.GUIUtility.systemCopyBuffer = code.text, "secondary");
            detail.Add(copy);
            codeScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            codeScroll.AddToClassList("code-scroll");
            codeScroll.Add(code);
            detail.Add(codeScroll);
            detail.Add(pitfalls);
            detail.Add(exercise);
            var track = new VisualElement();
            track.AddToClassList("progress-track");
            fill = new VisualElement();
            fill.AddToClassList("progress-fill");
            fill.style.backgroundColor = Topic.accent;
            track.Add(fill);
            Host.Add(track);
            var controls = new VisualElement();
            controls.AddToClassList("controls");
            previous = Action("上一步", () => { Pause(); Show(index - 1); }, "secondary");
            play = Action("播放演示", Toggle);
            next = Action("下一步", () => { Pause(); Show(index + 1); }, "secondary");
            controls.Add(previous);
            controls.Add(play);
            controls.Add(next);
            controls.Add(Action("重新开始", () => { Pause(); Show(0); }, "secondary"));
            Host.Add(controls);
            var speed = new SliderInt("自动播放每节停留（秒）", 3, 60) { value = 15, showInputField = true };
            speed.AddToClassList("reading-speed");
            speed.RegisterValueChangedCallback(evt =>
            {
                interval = evt.newValue * 1000;
                timer.Every(interval);
                if (playing) timer.ExecuteLater(interval);
            });
            Host.Add(speed);
            Host.Add(detail);
            Host.Add(Text("官方参考\n" + Topic.references, "references"));
            timer = Host.schedule.Execute(Tick).Every(interval);
            timer.Pause();
            Show(0);
        }

        void Show(int selected)
        {
            index = UnityEngine.Mathf.Clamp(selected, 0, Topic.steps.Length - 1);
            var step = Topic.steps[index];
            counter.text = "STEP " + (index + 1).ToString("00") + " / " + Topic.steps.Length.ToString("00");
            heading.text = step.title;
            body.text = step.explanation;
            code.text = step.exampleCode != null ? step.exampleCode.text : step.code;
            bool hasCode = !string.IsNullOrEmpty(code.text);
            codeScroll.style.display = hasCode ? DisplayStyle.Flex : DisplayStyle.None;
            copy.style.display = hasCode ? DisplayStyle.Flex : DisplayStyle.None;
            codeScroll.scrollOffset = UnityEngine.Vector2.zero;
            Section(usage, "使用场景与操作", step.usage);
            Section(pitfalls, "常见误区与边界", step.pitfalls);
            Section(exercise, "动手练习与验收", step.exercise);
            for (int i = 0; i < nodes.Count; i++) nodes[i].EnableInClassList("selected", i == index);
            fill.style.width = Length.Percent(100f * (index + 1) / Topic.steps.Length);
            previous.SetEnabled(index > 0);
            next.SetEnabled(index < Topic.steps.Length - 1);
        }

        void Toggle()
        {
            if (playing) { Pause(); return; }
            if (index == Topic.steps.Length - 1) Show(0);
            playing = true;
            play.text = "暂停演示";
            timer.ExecuteLater(interval);
        }

        void Tick()
        {
            if (!IsActive || !playing) return;
            Show(index + 1);
            if (index == Topic.steps.Length - 1) Pause();
        }

        void Pause()
        {
            playing = false;
            if (timer != null) timer.Pause();
            if (play != null) play.text = "播放演示";
        }

        static void Section(Label label, string title, string value)
        {
            label.text = title + "\n\n" + value;
            label.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        protected override void OnExit()
        {
            Pause();
            timer = null;
            nodes.Clear();
        }
    }
}
