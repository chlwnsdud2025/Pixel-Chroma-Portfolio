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

        [Header("Initial Loading (추가됨)")]
        [SerializeField] private UIInitialLoading initialLoading; // GameManager에서 이사 옴

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

        // ── [신규 추가] 초기 로딩 제어 함수 ──
        public void ShowInitialLoading()
        {
            if (initialLoading != null) initialLoading.Show();
        }

        public void HideInitialLoading()
        {
            if (initialLoading != null) initialLoading.Hide();
        }

        private async UniTask LoadAndRegisterPanel(AssetReferenceGameObject reference, Transform root)
        {
            var go = await reference.InstantiateAsync(root).ToUniTask();
            var panel = go.GetComponent<UIPanel>();

            if (panel == null)
            {
                Debug.LogError($"[UIManager] 프리팹에 UIPanel 컴포넌트가 없습니다: {go.name}");
                return;
            }

            _panels.Add(panel.GetType(), panel);
        }

        public async UniTask ShowScene<T>() where T : UIScene
        {
            if (!TryGetPanel<T>(out var target)) return;

            if (_currentScene != null)
            {
                await _currentScene.Hide();
                _currentScene.OnAfterHide();
            }

            _currentScene = target;
            _currentScene.OnBeforeShow();
            await _currentScene.Show();
        }

        public async UniTask<T> ShowPopup<T>() where T : UIPopup
        {
            if (!TryGetPanel<T>(out var target)) return null;

            target.OnBeforeShow();
            await target.Show();
            _popupStack.Push(target);

            return target;
        }

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

            Debug.LogError($"[UIManager] 패널을 찾을 수 없습니다: {typeof(T)}");
            panel = null;
            return false;
        }
    }
}