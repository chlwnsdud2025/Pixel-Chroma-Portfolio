using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace PixelChroma.UI
{
    public class UIManager : MonoSingleton<UIManager>
    {
        [SerializeField] private Transform sceneRoot;
        [SerializeField] private Transform popupRoot;
        [SerializeField] private UIRegistrySO registry;

        private readonly Dictionary<Type, UIPanel> _panels = new();
        private readonly Stack<UIPopup> _popupStack = new();

        private UIScene _currentScene;

        public UIScene CurrentScene => _currentScene;
        public UIPopup CurrentPopup => _popupStack.Count > 0 ? _popupStack.Peek() : null;

        public async UniTask InitializeAsync()
        {
            var tasks = new List<UniTask>();

            foreach (var reference in registry.sceneUIs)
                tasks.Add(LoadAndRegisterPanel(reference, sceneRoot));

            foreach (var reference in registry.popupUIs)
                tasks.Add(LoadAndRegisterPanel(reference, popupRoot));

            await UniTask.WhenAll(tasks);
        }

        private async UniTask LoadAndRegisterPanel(AssetReferenceGameObject reference, Transform root)
        {
            var go = await reference.InstantiateAsync(root).ToUniTask();
            var panel = go.GetComponent<UIPanel>();

            if (panel == null)
            {
                Debug.LogError($"[UIManager] {go.name} 에 UIPanel 컴포넌트가 없습니다.");
                Addressables.ReleaseInstance(go);
                return;
            }

            go.transform.SetParent(root, false);
            _panels[panel.GetType()] = panel;
        }

        // Scene 패널 열기 — UIScene만 허용
        public async UniTask ShowScene<T>() where T : UIScene
        {
            if (!TryGetPanel<T>(out var next)) return;

            if (_currentScene != null && _currentScene.gameObject.activeSelf)
            {
                _currentScene.OnAfterHide();
                await _currentScene.Hide();
            }

            _currentScene = next;
            _currentScene.OnBeforeShow();
            await _currentScene.Show();
        }

        // Popup 열기 — UIPopup만 허용
        public async UniTask ShowPopup<T>() where T : UIPopup
        {
            if (!TryGetPanel<T>(out var popup)) return;

            _popupStack.Push(popup);
            popup.OnBeforeShow();
            await popup.Show();
        }

        // 가장 위 Popup 닫기
        public async UniTask HideTopPopup()
        {
            if (_popupStack.Count == 0) return;

            var popup = _popupStack.Pop();
            await popup.Hide();
            popup.OnAfterHide();
        }

        // 특정 타입의 Popup 닫기
        public async UniTask HidePopup<T>() where T : UIPopup
        {
            if (!TryGetPanel<T>(out var target)) return;

            var temp = new Stack<UIPopup>();
            while (_popupStack.Count > 0)
            {
                var p = _popupStack.Pop();
                if (p.GetType() != typeof(T))
                    temp.Push(p);
            }
            while (temp.Count > 0)
                _popupStack.Push(temp.Pop());

            await target.Hide();
            target.OnAfterHide();
        }

        // 모든 Popup 닫기
        public async UniTask HideAllPopups()
        {
            var tasks = new List<UniTask>();
            while (_popupStack.Count > 0)
            {
                var popup = _popupStack.Pop();
                tasks.Add(popup.Hide());
            }
            await UniTask.WhenAll(tasks);
        }

        public T GetPanel<T>() where T : UIPanel
        {
            _panels.TryGetValue(typeof(T), out var panel);
            return panel as T;
        }

        private bool TryGetPanel<T>(out T panel) where T : UIPanel
        {
            if (_panels.TryGetValue(typeof(T), out var found))
            {
                panel = found as T;
                return true;
            }

            Debug.LogError($"[UIManager] {typeof(T).Name} 패널이 등록되지 않았습니다.");
            panel = null;
            return false;
        }

        public bool HasOpenPopup => _popupStack.Count > 0;
    }
}
