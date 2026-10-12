using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TextCore.Text;
using UnityEngine.SceneManagement;

namespace UnityLearning
{
    [DisallowMultipleComponent]
    public class KnowledgeLearningApp : MonoBehaviour
    {
        [Tooltip("打包到其他平台时，可指定包含中文字形的项目字体。")]
        [SerializeField] Font chineseFont = null;
        readonly Dictionary<PresentationKind, Func<KnowledgePresentation>> factories =
            new Dictionary<PresentationKind, Func<KnowledgePresentation>>();
        readonly HashSet<string> visited = new HashSet<string>();
        KnowledgeTopic[] topics;
        KnowledgePresentation current;
        VisualElement root, home, lesson, cards, stage;
        Label title, subtitle, note, progress;
        TextField search;
        ScrollView page;
        Button launchLab;
        string labScene;
        UIDocument document;
        PanelSettings ownedPanel;
        FontAsset ownedFontAsset;
        string category = "全部";
        readonly List<Button> filters = new List<Button>();

        public bool IsInLesson { get { return current != null; } }

        // 新表现类型在枚举中添加标识，然后注册工厂；目录不依赖具体实现。
        public void RegisterPresentation(PresentationKind kind, Func<KnowledgePresentation> factory)
        {
            if (factory == null) throw new ArgumentNullException("factory");
            factories[kind] = factory;
        }

        void OnEnable()
        {
            if (!factories.ContainsKey(PresentationKind.Steps))
                RegisterPresentation(PresentationKind.Steps, () => new StepPresentation());
            if (!factories.ContainsKey(PresentationKind.ObjectPool))
                RegisterPresentation(PresentationKind.ObjectPool, () => new ObjectPoolPresentation());
            topics = Resources.LoadAll<KnowledgeTopic>("Learning/Topics").OrderBy(t => t.order).ToArray();
            document = GetComponent<UIDocument>();
            if (document == null) document = gameObject.AddComponent<UIDocument>();
            if (document.panelSettings == null)
            {
                ownedPanel = ScriptableObject.CreateInstance<PanelSettings>();
                ownedPanel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                ownedPanel.referenceResolution = new Vector2Int(1280, 800);
                ownedPanel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                ownedPanel.match = .5f;
                ownedPanel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("Learning/LearningTheme");
                document.panelSettings = ownedPanel;
            }
            document.sortingOrder = 10;
            root = document.rootVisualElement;
            root.focusable = true;
            root.Clear();
            root.AddToClassList("learning-root");
            var sheet = Resources.Load<StyleSheet>("Learning/Learning");
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            ApplyFont();
            root.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            root.RegisterCallback<KeyDownEvent>(OnKey);
            Build();
            ReturnHome();
        }

        void ApplyFont()
        {
            Font font = chineseFont;
            if (font == null)
            {
                var installed = Font.GetOSInstalledFontNames();
                string family = new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "PingFang SC", "Arial Unicode MS" }
                    .FirstOrDefault(candidate => installed.Contains(candidate));
                if (family != null)
                {
                    ownedFontAsset = FontAsset.CreateFontAsset(family, "Regular", 48);
                    if (ownedFontAsset != null)
                    {
                        ownedFontAsset.isMultiAtlasTexturesEnabled = true;
                        root.style.unityFontDefinition = FontDefinition.FromSDFFont(ownedFontAsset);
                    }
                }
            }
            if (font != null)
            {
                root.style.unityFontDefinition = FontDefinition.FromFont(font);
            }
        }

        void Build()
        {
            page = new ScrollView(ScrollViewMode.Vertical);
            page.AddToClassList("page-scroll");
            root.Add(page);
            var shell = Box(page, "shell");
            var brand = Box(shell, "brand-row");
            AddText(brand, "U", "brand-mark");
            AddText(brand, "UNITY 学习小岛", "brand-title");
            AddText(brand, "一点一点，搭建你的开发世界", "brand-caption");

            home = Box(shell, "home");
            var hero = Box(home, "hero");
            AddText(hero, "LEARN · PLAY · CREATE", "eyebrow");
            AddText(hero, "今天，探索一个新知识！", "hero-title");
            AddText(hero, "从一张知识卡片出发，把抽象概念变成看得见的小实验。", "body");
            progress = AddText(hero, "", "progress-caption");
            var tools = Box(home, "tools");
            var filterRow = Box(tools, "filters");
            foreach (var value in new[] { "全部" }.Concat(topics.Select(t => t.category).Distinct()))
            {
                string selected = value;
                var filter = MakeButton(filterRow, selected, () => { category = selected; RenderCards(); }, "filter");
                filters.Add(filter);
            }
            search = new TextField { label = "搜索知识" };
            search.AddToClassList("search");
            search.RegisterValueChangedCallback(_ => RenderCards());
            tools.Add(search);
            cards = Box(home, "cards");
            AddText(home, "学习提示：先看步骤，再试着用自己的话讲一遍。", "footer");

            lesson = Box(shell, "lesson");
            MakeButton(lesson, "← 返回知识目录", ReturnHome, "back");
            title = AddText(lesson, "", "hero-title");
            subtitle = AddText(lesson, "", "body");
            launchLab = MakeButton(lesson, "进入独立流程实验室 →", () => SceneManager.LoadScene(labScene), "action");
            stage = Box(lesson, "stage");
            note = AddText(lesson, "", "note");
            AddText(lesson, "可以按 Esc 返回目录，继续探索其他知识。", "footer");
        }

        void RenderCards()
        {
            cards.Clear();
            foreach (var filter in filters) filter.EnableInClassList("selected", filter.text == category);
            string keyword = search.value == null ? "" : search.value.Trim();
            var visible = topics.Where(t => (category == "全部" || t.category == category) &&
                MatchesKeyword(t, keyword));
            int count = 0;
            foreach (var topic in visible)
            {
                count++;
                var card = MakeButton(cards, "", () => OpenTopic(topic), "knowledge-card");
                var ribbon = Box(card, "card-ribbon");
                var badge = AddText(ribbon, topic.badge, "badge");
                badge.style.backgroundColor = topic.accent;
                AddText(ribbon, topic.category, "category");
                AddText(card, topic.title, "card-title");
                AddText(card, topic.summary, "card-summary");
                AddText(card, (topic.steps == null ? 0 : topic.steps.Length) + " 章节 · 原理 / 场景 / 实例", "chapter-count");
                AddText(card, visited.Contains(topic.id) ? "已探索 · 再看一次 →" : "开始探索 →", "card-link");
            }
            if (count == 0) AddText(cards, topics.Length == 0 ? "还没有知识资源，请在 Resources/Learning/Topics 中添加 Knowledge Topic。" : "没有找到相关知识，试试其他关键词。", "empty");
            progress.text = "探索记录  " + visited.Count + " / " + topics.Length + "   ·   本次运行有效";
        }

        static bool MatchesKeyword(KnowledgeTopic topic, string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return true;
            string overview = topic.title + " " + topic.summary + " " + topic.category + " " + topic.learningGoals;
            if (overview.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return topic.steps != null && topic.steps.Any(step =>
                (step.title + " " + step.explanation + " " + step.usage)
                .IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public void OpenTopic(KnowledgeTopic topic)
        {
            if (topic == null || stage == null) return;
            Func<KnowledgePresentation> factory;
            if (!factories.TryGetValue(topic.presentation, out factory))
            {
                Debug.LogError("没有为知识类型注册展示工厂：" + topic.presentation, this);
                return;
            }
            ClosePresentation();
            title.text = topic.title;
            subtitle.text = topic.summary;
            note.text = topic.note;
            labScene = topic.demonstrationScene;
            launchLab.style.display = string.IsNullOrEmpty(labScene) ? DisplayStyle.None : DisplayStyle.Flex;
            home.style.display = DisplayStyle.None;
            lesson.style.display = DisplayStyle.Flex;
            page.scrollOffset = Vector2.zero;
            current = factory();
            try { current.Enter(stage, topic); }
            catch { ReturnHome(); throw; }
            visited.Add(topic.id);
            root.Focus();
        }

        public void ReturnHome()
        {
            ClosePresentation();
            if (home == null) return;
            lesson.style.display = DisplayStyle.None;
            home.style.display = DisplayStyle.Flex;
            page.scrollOffset = Vector2.zero;
            RenderCards();
        }

        void ClosePresentation()
        {
            if (current == null) return;
            current.Exit();
            current = null;
        }

        void OnKey(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape || current == null) return;
            ReturnHome();
            evt.StopPropagation();
        }

        void OnGeometry(GeometryChangedEvent evt)
        {
            root.EnableInClassList("compact", evt.newRect.width < 850);
        }

        void OnDisable()
        {
            ClosePresentation();
            filters.Clear();
            if (root != null)
            {
                root.UnregisterCallback<GeometryChangedEvent>(OnGeometry);
                root.UnregisterCallback<KeyDownEvent>(OnKey);
                root.Clear();
            }
            if (ownedFontAsset != null)
            {
                foreach (var atlas in ownedFontAsset.atlasTextures)
                    if (atlas != null) Destroy(atlas);
                if (ownedFontAsset.material != null) Destroy(ownedFontAsset.material);
                Destroy(ownedFontAsset);
            }
            ownedFontAsset = null;
        }

        void OnDestroy()
        {
            if (ownedPanel != null) Destroy(ownedPanel);
        }

        static VisualElement Box(VisualElement parent, string css)
        {
            var box = new VisualElement();
            box.AddToClassList(css);
            parent.Add(box);
            return box;
        }

        static Label AddText(VisualElement parent, string value, string css)
        {
            var label = new Label(value);
            label.AddToClassList(css);
            parent.Add(label);
            return label;
        }

        static Button MakeButton(VisualElement parent, string value, Action action, string css)
        {
            var button = new Button(action) { text = value };
            button.AddToClassList(css);
            parent.Add(button);
            return button;
        }
    }
}
