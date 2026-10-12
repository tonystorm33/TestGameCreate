using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace UnityLearning.Labs
{
    // 各实验共用：流程操作、可视化、代码、测试报告与生命周期。
    public abstract class LearningLabBase : MonoBehaviour
    {
        public abstract string LabId { get; }
        protected abstract string LabTitle { get; }
        protected abstract string Guide { get; }
        protected abstract string[] Phases { get; }
        protected abstract string[] SourceFiles { get; }
        protected abstract void InitializeLab();
        protected abstract void StepLab();
        protected abstract void ResetLab();
        protected abstract void DisposeLab();
        protected abstract IEnumerator ExecuteTests();
        protected virtual void TickLab() { }
        protected Transform Objects { get; private set; }
        protected VisualElement Dashboard { get; private set; }
        protected VisualElement Root { get; private set; }
        public bool TestRunning { get; private set; }
        public bool TestsFinished { get; private set; }
        public int TestPassed { get; private set; }
        public int TestFailed { get; private set; }
        public int Phase { get; private set; }
        readonly List<string> messages = new List<string>();
        readonly List<string> results = new List<string>();
        readonly List<Material> materials = new List<Material>();
        readonly List<Label> phaseButtons = new List<Label>();
        readonly List<Button> actions = new List<Button>();
        Label log, tests, state;
        UIDocument document;
        PanelSettings panel;
        FontAsset font;
        Camera labCamera;
        Camera displayCamera;
        GameObject labLight;
        RenderTexture texture;
        Coroutine playback;

        void OnEnable()
        {
            document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1280, 900);
                panel.match = .5f;
                panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("Learning/LearningTheme");
            }
            document.panelSettings = panel;
            Root = document.rootVisualElement;
            Root.Clear();
            Root.AddToClassList("learning-root");
            Root.styleSheets.Add(Resources.Load<StyleSheet>("Learning/Learning"));
            Root.styleSheets.Add(Resources.Load<StyleSheet>("Learning/Labs/Labs"));
            string family = new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "PingFang SC" }
                .FirstOrDefault(f => Font.GetOSInstalledFontNames().Contains(f));
            if (family != null)
            {
                font = FontAsset.CreateFontAsset(family, "Regular", 48);
                if (font != null) { font.isMultiAtlasTexturesEnabled = true; Root.style.unityFontDefinition = FontDefinition.FromSDFFont(font); }
            }
            Root.focusable = true;
            Root.RegisterCallback<KeyDownEvent>(OnKey);
            var scroll = new ScrollView(); Root.Add(scroll); scroll.AddToClassList("page-scroll");
            var shell = new VisualElement(); shell.AddToClassList("lab-shell"); scroll.Add(shell);
            Button(shell, "← 返回知识目录", () => SceneManager.LoadScene("PlayScene"), "back");
            Label(shell, LabTitle, "hero-title");
            Label(shell, Guide, "learning-overview");
            var flow = new VisualElement(); flow.AddToClassList("flow"); shell.Add(flow);
            foreach (var phase in Phases) phaseButtons.Add(Label(flow, phase, "step-node"));
            var controls = new VisualElement(); controls.AddToClassList("controls"); shell.Add(controls);
            actions.Add(Button(controls, "执行下一步", StepLab));
            actions.Add(Button(controls, "运行完整流程", () => { if (playback == null) playback = StartCoroutine(Play()); }));
            actions.Add(Button(controls, "重置实验", () => { StopPlayback(); ResetLab(); }));
            actions.Add(Button(controls, "运行全部测试", BeginTests));
            state = Label(shell, "", "pool-status");
            Dashboard = new VisualElement(); shell.Add(Dashboard); Dashboard.AddToClassList("lab-dashboard");
            SetupCamera();
            var image = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit }; image.AddToClassList("lab-view"); shell.Add(image);
            tests = Label(shell, "测试未开始", "lesson-exercise");
            log = Label(shell, "", "lab-log");
            var analysis = Resources.Load<UnityEngine.TextAsset>("Learning/Labs/Analysis/" + LabId);
            if (analysis != null)
            {
                var explanation = new Foldout { text = "调用链与逐方法解析", value = false };
                shell.Add(explanation); Label(explanation, analysis.text, "learning-overview");
            }
            foreach (string file in SourceFiles)
            {
                var foldout = new Foldout { text = "代码解析 / 完整源文件：" + file, value = false }; shell.Add(foldout);
                var source = Resources.Load<UnityEngine.TextAsset>("Learning/Labs/Source/" + file);
                var codeView = new ScrollView(ScrollViewMode.VerticalAndHorizontal); codeView.AddToClassList("code-scroll"); foldout.Add(codeView);
                Label(codeView, source == null ? "源文件尚未生成" : source.text, "code");
                Button(foldout, "复制源文件", () => { if (source != null) GUIUtility.systemCopyBuffer = source.text; });
            }
            InitializeLab();
            Root.Focus();
        }

        void SetupCamera()
        {
            // The preview camera renders to a texture; Game view also needs a display camera.
            var displayObject = new GameObject("Lab Display Camera");
            displayObject.transform.SetParent(transform, false);
            displayCamera = displayObject.AddComponent<Camera>();
            displayCamera.targetDisplay = 0;
            displayCamera.clearFlags = CameraClearFlags.SolidColor;
            displayCamera.backgroundColor = new Color32(246, 244, 236, 255);
            displayCamera.cullingMask = 0;
            displayCamera.allowHDR = false;
            displayCamera.allowMSAA = false;
            Objects = new GameObject("Lab Objects").transform;
            Objects.SetParent(transform);
            var cameraObject = new GameObject("Lab Camera"); cameraObject.transform.SetParent(transform);
            labCamera = cameraObject.AddComponent<Camera>();
            labCamera.transform.position = new Vector3(0, 7, -14);
            labCamera.transform.LookAt(Vector3.zero);
            labCamera.clearFlags = CameraClearFlags.SolidColor;
            labCamera.backgroundColor = new Color(.88f, .9f, .97f);
            labCamera.orthographic = true; labCamera.orthographicSize = 5.5f;
            texture = new RenderTexture(1000, 400, 24); texture.Create(); labCamera.targetTexture = texture;
            labLight = new GameObject("Lab Light"); labLight.transform.SetParent(transform);
            labLight.AddComponent<Light>().type = LightType.Directional; labLight.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        protected GameObject Cube(string name, Vector3 position, Color color)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name; cube.transform.SetParent(Objects); cube.transform.position = position;
            var material = new Material(Shader.Find("Standard")); material.color = color; materials.Add(material);
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }
        protected void ClearObjects()
        {
            if (Objects != null) foreach (Transform child in Objects) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            foreach (var material in materials) if (material != null) Destroy(material);
            materials.Clear();
        }
        protected void SetPhase(int phase, string message)
        {
            Phase = phase;
            for (int i = 0; i < phaseButtons.Count; i++) phaseButtons[i].EnableInClassList("selected", i == phase);
            state.text = message;
        }
        protected void Log(string message)
        {
            messages.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + message);
            if (messages.Count > 35) messages.RemoveAt(0);
            log.text = string.Join("\n", messages);
        }
        protected void Check(bool condition, string name, string actual = "")
        {
            if (condition) TestPassed++; else TestFailed++;
            results.Add((condition ? "PASS  " : "FAIL  ") + name + "  " + actual);
            tests.text = "通过 " + TestPassed + " / 失败 " + TestFailed + "\n" + string.Join("\n", results);
        }
        public void BeginTests()
        {
            if (TestRunning) return;
            StopPlayback(); results.Clear(); TestPassed = TestFailed = 0; TestsFinished = false; TestRunning = true;
            foreach (var action in actions) action.SetEnabled(false);
            Dashboard.SetEnabled(false);
            StartCoroutine(TestRunner());
        }
        IEnumerator TestRunner()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(ExecuteTests());
            while (stack.Count > 0)
            {
                object current = null; bool moved = false;
                try { moved = stack.Peek().MoveNext(); if (moved) current = stack.Peek().Current; }
                catch (Exception e) { Check(false, "测试抛出异常", e.ToString()); break; }
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (current is IEnumerator nested && !(current is CustomYieldInstruction)) stack.Push(nested);
                else yield return current;
            }
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            TestRunning = false; TestsFinished = true;
            foreach (var action in actions) action.SetEnabled(true);
            Dashboard.SetEnabled(true);
            string folder = Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/Validation/LabReports")) : Path.Combine(Application.persistentDataPath, "LabReports");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, LabId + ".txt"), "Unity " + Application.unityVersion + "\n" + string.Join("\n", results));
            Log("测试完成，报告：" + folder);
        }
        IEnumerator Play()
        {
            ResetLab();
            for (int i = 0; i < Phases.Length; i++) { StepLab(); yield return new WaitForSecondsRealtime(1.5f); }
            playback = null;
        }
        void StopPlayback() { if (playback != null) StopCoroutine(playback); playback = null; }
        void Update() { TickLab(); }
        void OnKey(KeyDownEvent evt) { if (evt.keyCode == KeyCode.Escape) SceneManager.LoadScene("PlayScene"); }
        void OnDisable()
        {
            StopAllCoroutines(); playback = null; TestRunning = false;
            DisposeLab(); ClearObjects();
            if (Root != null) { Root.UnregisterCallback<KeyDownEvent>(OnKey); Root.Clear(); }
            if (displayCamera != null)
            {
                displayCamera.enabled = false;
                Destroy(displayCamera.gameObject);
            }
            if (labCamera != null) Destroy(labCamera.gameObject);
            if (labLight != null) Destroy(labLight);
            if (Objects != null) Destroy(Objects.gameObject);
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (font != null)
            {
                foreach (var atlas in font.atlasTextures) if (atlas != null) Destroy(atlas);
                Destroy(font.material); Destroy(font);
            }
            phaseButtons.Clear(); actions.Clear();
        }
        void OnDestroy() { if (panel != null) Destroy(panel); }
        protected static Label Label(VisualElement parent, string text, string css)
        { var label = new Label(text); label.AddToClassList(css); parent.Add(label); return label; }
        protected static Button Button(VisualElement parent, string text, Action action, string css = "secondary")
        { var button = new Button(action) { text = text }; button.AddToClassList(css); parent.Add(button); return button; }
    }
}
