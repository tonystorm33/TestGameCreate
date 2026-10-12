using System;
using UnityEngine;

namespace UnityLearning
{
    public enum PresentationKind { Steps, ObjectPool }

    [Serializable]
    public class KnowledgeStep
    {
        public string title;
        [TextArea(3, 8)] public string explanation;
        [TextArea(2, 8)] public string code;
        [TextArea(3, 10)] public string usage;
        [TextArea(3, 10)] public string pitfalls;
        [TextArea(3, 10)] public string exercise;
        [Tooltip("完整代码示例（文本资源），优先于 Code 显示。")]
        public TextAsset exampleCode;
    }

    // 内容与 UI 分离：创建一个资源即可扩展知识目录。
    [CreateAssetMenu(menuName = "Unity Learning/Knowledge Topic", fileName = "NewTopic")]
    public class KnowledgeTopic : ScriptableObject
    {
        public string id;
        public int order;
        public string title;
        public string category;
        [TextArea] public string summary;
        public string badge;
        public Color accent = new Color(.45f, .4f, .8f);
        public PresentationKind presentation;
        [TextArea] public string note;
        [TextArea(3, 10)] public string learningGoals;
        [TextArea(2, 8)] public string prerequisites;
        [TextArea(2, 8)] public string references;
        [Tooltip("独立流程实验场景名（需加入 Build Settings）")]
        public string demonstrationScene;
        public KnowledgeStep[] steps = new KnowledgeStep[0];
    }
}
