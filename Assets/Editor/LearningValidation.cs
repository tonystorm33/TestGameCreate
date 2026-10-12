using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityLearning;

// 在菜单或 batchmode 中运行；只创建临时展示，不改动场景。
public static class LearningValidation
{
    [MenuItem("Tools/Unity Learning/Validate Knowledge Framework")]
    public static void Run()
    {
        try
        {
            var topics = Resources.LoadAll<KnowledgeTopic>("Learning/Topics");
            Require(topics.Length >= 5, "知识资源未完整导入");
            Require(topics.All(t => !string.IsNullOrEmpty(t.id)), "知识 id 不能为空");
            Require(topics.Select(t => t.id).Distinct().Count() == topics.Length, "知识 id 必须唯一");
            Require(Resources.Load<StyleSheet>("Learning/Learning") != null, "样式表未导入");
            Require(Resources.Load<ThemeStyleSheet>("Learning/LearningTheme") != null, "UI Toolkit 默认主题未导入");
            var host = new VisualElement();
            var lifecycle = topics.First(t => t.id == "lifecycle");
            var steps = new StepPresentation();
            steps.Enter(host, lifecycle);
            Click(host, "下一步");
            Require(host.Query<Label>(className: "section-title").First().text == lifecycle.steps[1].title,
                "下一步没有切换内容");
            Click(host, "播放演示");
            Require(host.Query<Button>().ToList().Any(b => b.text == "暂停演示"), "播放状态错误");
            steps.Exit();
            Require(host.childCount == 0, "退出时未清理展示");
            var playing = (bool)typeof(StepPresentation).GetField("playing", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(steps);
            Require(!playing, "退出时未停止播放");
            steps.Enter(host, lifecycle);
            Require(host.Query<Label>(className: "section-title").First().text == lifecycle.steps[0].title,
                "重新进入时没有重置");
            steps.Exit();

            foreach (var topic in topics)
            {
                Require(topic.steps != null && topic.steps.Length > 0, "图文内容为空：" + topic.title);
                Require(!string.IsNullOrEmpty(topic.learningGoals) && !string.IsNullOrEmpty(topic.prerequisites), "学习目标或预备知识为空：" + topic.title);
                Require(!string.IsNullOrEmpty(topic.references), "官方参考为空：" + topic.title);
                steps.Enter(host, topic);
                for (int i = 1; i < topic.steps.Length; i++) Click(host, "下一步");
                Require(host.Query<Label>(className: "section-title").First().text == topic.steps.Last().title,
                    "无法到达最后一步：" + topic.title);
                steps.Exit();
            }
            string[] examples = { "LifecycleProbe", "FollowCameraExample", "ProjectilePoolExample", "EcsMovementExample", "NetworkSwitchExample", "AssetBundleUpdateExample" };
            foreach (string example in examples)
            {
                var asset = Resources.Load<TextAsset>("Learning/Examples/" + example + ".cs");
                Require(asset != null && !string.IsNullOrEmpty(asset.text), "完整实例未导入：" + example);
                Require(topics.Any(t => t.steps.Any(s => s.exampleCode == asset)), "实例未绑定课程：" + example);
            }
            var demo = topics.SelectMany(t => t.steps).First(s => s.exampleCode != null);
            var demoTopic = topics.First(t => t.steps.Contains(demo));
            steps.Enter(host, demoTopic);
            var node = host.Query<Button>(className: "step-node").ToList().First(b => b.text.EndsWith(demo.title));
            Click(host, node.text);
            Require(host.Query<Label>(className: "code").First().text == demo.exampleCode.text, "完整实例显示错误");
            steps.Exit();

            var pool = new ObjectPoolPresentation();
            pool.Enter(host, topics.First(t => t.id == "pool"));
            Click(host, "取出 Get");
            var firstBall = host.Query<Label>(className: "ball").First();
            Click(host, "回收 Release");
            Click(host, "取出 Get");
            Require(ReferenceEquals(firstBall, host.Query<Label>(className: "ball").First()), "对象池没有复用同一对象");
            for (int i = 0; i < 10; i++) Click(host, "取出 Get");
            Require(host.Query<Label>(className: "working").ToList().Count == 8, "池容量限制无效");
            Click(host, "重置实验");
            Require(host.Query<Label>(className: "ball").ToList().Count == 0, "池重置未清理");
            pool.Exit();
            Require(host.childCount == 0, "池退出未清理");

            string report = "PASS: Unity " + Application.unityVersion + "\n" +
                topics.Length + " topic assets / stylesheet / theme imported\n" +
                "step navigation / play-exit cleanup / re-entry reset\n" +
                "all text presentations reach final step\n" +
                topics.Sum(t => t.steps.Length) + " curriculum chapters / 6 full examples / example binding\n" +
                "object identity reuse / capacity / reset / exit cleanup\n";
            string reportFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/Validation"));
            Directory.CreateDirectory(reportFolder);
            File.WriteAllText(Path.Combine(reportFolder, "FrameworkReport.txt"), report);
            Debug.Log(report);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    static void Click(VisualElement host, string text)
    {
        var button = host.Query<Button>().ToList().First(b => b.text == text);
        if (!button.enabledSelf) return;
        // 执行真实 Clickable 的回调，验证按钮绑定，而非直接调用展示内部方法。
        var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.NonPublic | BindingFlags.Instance);
        Require(invoke != null, "当前 Unity 版本没有 Clickable.Invoke");
        invoke.Invoke(button.clickable, new object[] { null });
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
