using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIMapPanel : MonoBehaviour
    {
        [SerializeField] private Image previewImage;
        [SerializeField] private Transform listContent;
        [SerializeField] private UIMapItem itemPrefab;

        private readonly List<UIMapItem> _pool = new();
        private MapRegistrySO _registry;
        private int _selectedIndex = -1;

        private int _builtInCount;
        private readonly List<WorkshopMapEntry> _workshopEntries = new();

        public event Action<string> OnMapSelected;

        public void Initialize(MapRegistrySO registry)
        {
            if (listContent == null || registry == null) return;

            _registry = registry;

            foreach (Transform child in listContent)
            {
                var item = child.GetComponent<UIMapItem>();
                if (item != null)
                {
                    item.OnClicked += Select;
                    item.gameObject.SetActive(false);
                    _pool.Add(item);
                }
            }

            int needed = _registry.Maps.Count - _pool.Count;
            for (int i = 0; i < needed; i++)
                _pool.Add(CreateItem());
        }

        public void Refresh(MapRegistrySO registry, string currentMapName)
        {
            _registry = registry;
            RebuildList();

            if (_pool.Count == 0) return;

            int idx = 0;
            if (!string.IsNullOrEmpty(currentMapName))
            {
                for (int i = 0; i < _pool.Count; i++)
                {
                    if (!_pool[i].gameObject.activeSelf) continue;

                    if (i < _builtInCount)
                    {
                        if (i < _registry.Maps.Count && _registry.Maps[i] != null
                            && _registry.Maps[i].name == currentMapName)
                        {
                            idx = i;
                            break;
                        }
                    }
                    else
                    {
                        int wsIdx = i - _builtInCount;
                        if (wsIdx < _workshopEntries.Count
                            && _workshopEntries[wsIdx].NetworkId == currentMapName)
                        {
                            idx = i;
                            break;
                        }
                    }
                }
            }

            Select(idx);
        }

        private void Select(int index)
        {
            if (index < 0 || index >= _pool.Count || !_pool[index].gameObject.activeSelf)
                return;

            _selectedIndex = index;

            if (index < _builtInCount)
            {
                if (_registry == null || index >= _registry.Maps.Count) return;
                var map = _registry.Maps[index];
                if (map == null) return;

                if (previewImage != null && map.previewSprite != null)
                    previewImage.sprite = map.previewSprite;

                OnMapSelected?.Invoke(map.name);
            }
            else
            {
                int wsIdx = index - _builtInCount;
                if (wsIdx >= _workshopEntries.Count) return;

                var entry = _workshopEntries[wsIdx];

                if (previewImage != null && entry.Preview != null)
                    previewImage.sprite = entry.Preview;

                OnMapSelected?.Invoke(entry.NetworkId);
            }

            RefreshHighlight();
        }

        private void RebuildList()
        {
            for (int i = 0; i < _pool.Count; i++)
                _pool[i].gameObject.SetActive(false);

            _builtInCount = 0;
            _workshopEntries.Clear();

            // ── 내장맵 ──
            if (_registry != null)
            {
                for (int i = 0; i < _registry.Maps.Count; i++)
                {
                    var map = _registry.Maps[i];
                    if (map == null) continue;

                    UIMapItem item = GetOrCreateItem(_builtInCount);
                    item.Setup(_builtInCount, map.GetDisplayName());
                    item.SetWorkshopBadge(false);
                    item.gameObject.SetActive(true);
                    _builtInCount++;
                }
            }

            // ── 창작마당 맵 ──
            Debug.Log($"[UIMapPanel] SteamActive={GameManager.SteamActive}, SteamInitialized={GameManager.SteamInitialized}");

            if (GameManager.SteamActive)
            {
                var subscribedMaps = WorkshopSubscriptionService.GetSubscribedMaps();
                Debug.Log($"[UIMapPanel] 구독된 맵 수: {subscribedMaps.Count}");

                foreach (var entry in subscribedMaps)
                {
                    Debug.Log($"[UIMapPanel] 맵 fileId={entry.publishedFileId}, installed={entry.isInstalled}, path={entry.installPath}");

                    if (!entry.isInstalled)
                    {
                        // 구독됐지만 미설치 → 다운로드 요청
                        WorkshopSubscriptionService.RequestDownload(entry.publishedFileId);
                        continue;
                    }


                    entry.LoadMetadata();
                    _workshopEntries.Add(entry);

                    int globalIdx = _builtInCount + _workshopEntries.Count - 1;
                    UIMapItem item = GetOrCreateItem(globalIdx);

                    string displayName = string.IsNullOrEmpty(entry.Title) ? "???" : entry.Title;
                    item.Setup(globalIdx, displayName);
                    item.SetWorkshopBadge(true);
                    item.gameObject.SetActive(true);
                }
            }
        }

        private UIMapItem GetOrCreateItem(int index)
        {
            if (index < _pool.Count)
                return _pool[index];

            var item = CreateItem();
            _pool.Add(item);
            return item;
        }

        private UIMapItem CreateItem()
        {
            var item = Instantiate(itemPrefab, listContent);
            item.OnClicked += Select;
            item.gameObject.SetActive(false);
            return item;
        }

        private void RefreshHighlight()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].gameObject.activeSelf) continue;
                _pool[i].SetSelected(i == _selectedIndex);
            }
        }
    }
}