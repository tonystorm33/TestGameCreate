using System;
using UnityEngine.UIElements;

namespace UnityLearning
{
    public interface IKnowledgePresentation
    {
        void Enter(VisualElement host, KnowledgeTopic topic);
        void Exit();
    }

    // 自定义动画、视频、3D 交互等展示均可继承该基类。
    // Exit 同时负责停止计时器、解绑事件和清理展示对象。
    public abstract class KnowledgePresentation : IKnowledgePresentation, IDisposable
    {
        protected VisualElement Host { get; private set; }
        protected KnowledgeTopic Topic { get; private set; }
        protected bool IsActive { get; private set; }

        public void Enter(VisualElement host, KnowledgeTopic topic)
        {
            Exit();
            Host = host;
            Topic = topic;
            IsActive = true;
            OnEnter();
        }

        public void Exit()
        {
            if (!IsActive) return;
            IsActive = false;
            OnExit();
            Host.Clear();
            Host = null;
            Topic = null;
        }

        protected abstract void OnEnter();
        protected virtual void OnExit() { }
        public void Dispose() { Exit(); }

        protected static Label Text(string value, string css)
        {
            var label = new Label(value);
            label.AddToClassList(css);
            return label;
        }

        protected static Button Action(string value, Action callback, string css = "action")
        {
            var button = new Button(callback) { text = value };
            button.AddToClassList(css);
            return button;
        }
    }
}
