using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityLearning.Labs;

// 菜单运行真实 PlayMode 场景测试，复用场景中可见的完整用例。
[InitializeOnLoad]
public static class LabSceneTests
{
    static readonly string[] scenes = { "EcsFlowDemo", "NetworkSyncDemo", "HotUpdateDemo", "ObjectPoolDemo", "LifecycleDemo" };
    static int phase;
    static double deadline;
    static LearningLabBase lab;
    static RenderTexture captureTarget;
    static double captureAt;
    static LabSceneTests()
    {
        if (SessionState.GetBool("LearningLabTests", false))
        { EditorApplication.update += Tick; deadline = EditorApplication.timeSinceStartup + 90; }
    }
    [MenuItem("Tools/Unity Learning/Run All Lab Scene Tests")]
    public static void RunAll()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止当前 Play 模式。");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        LabSourceSync.Sync();
        LabContentBuilder.Build();
        EditorSceneManager.OpenScene("Assets/Scenes/Labs/EcsFlowDemo.unity");
        SessionState.SetInt("LearningLabTestIndex", 0);
        SessionState.SetBool("LearningLabTests", true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("流程测试超时，场景 " + SceneManager.GetActiveScene().name);
            int index = SessionState.GetInt("LearningLabTestIndex", 0);
            if (phase == 0)
            {
                lab = UnityEngine.Object.FindObjectOfType<LearningLabBase>();
                if (lab == null || SceneManager.GetActiveScene().name != scenes[index]) return;
                lab.BeginTests(); phase = 1;
            }
            else if (phase == 1 && lab.TestsFinished)
            {
                if (lab.TestFailed > 0 || lab.TestPassed == 0) throw new Exception(scenes[index] + " 测试失败，请查看 LabReports。");
                var settings = lab.GetComponent<UIDocument>().panelSettings;
                captureTarget = new RenderTexture(1280, 1800, 24); captureTarget.Create();
                settings.scaleMode = PanelScaleMode.ConstantPixelSize; settings.scale = 1; settings.targetTexture = captureTarget;
                captureAt = EditorApplication.timeSinceStartup + 1;
                phase = 3;
            }
            else if (phase == 3 && EditorApplication.timeSinceStartup >= captureAt)
            {
                Capture(scenes[index], lab);
                Debug.Log("LAB PASS " + scenes[index] + " " + lab.TestPassed);
                index++; SessionState.SetInt("LearningLabTestIndex", index);
                if (index == scenes.Length)
                {
                    SceneManager.LoadScene("PlayScene"); phase = 2;
                }
                else { SceneManager.LoadScene(scenes[index]); phase = 0; }
                deadline = EditorApplication.timeSinceStartup + 90;
            }
            else if (phase == 2 && SceneManager.GetActiveScene().name == "PlayScene")
            {
                if (UnityEngine.Object.FindObjectOfType<UnityLearning.KnowledgeLearningApp>() == null) throw new Exception("返回目录失败");
                Debug.Log("LAB PASS navigation back to PlayScene"); Finish(0);
            }
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }
    static void Capture(string name, LearningLabBase app)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>()) if (camera.targetTexture != null) camera.Render();
        var document = app.GetComponent<UIDocument>(); var target = captureTarget;
        var panel = document.rootVisualElement.panel;
        panel.GetType().GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Invoke(panel, new object[] { new Event { type = EventType.Repaint } });
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/Previews/Labs")); Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
        RenderTexture.active = null; document.panelSettings.targetTexture = null; target.Release(); captureTarget = null;
        UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image);
    }
    static void Finish(int code)
    {
        SessionState.SetBool("LearningLabTests", false); EditorApplication.update -= Tick;
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else EditorApplication.ExitPlaymode();
    }
}
