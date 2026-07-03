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

        public event Action<MapDataSO> OnMapSelected;

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

            if (_registry == null || _registry.Maps.Count == 0) return;

            int idx = 0;
            if (!string.IsNullOrEmpty(currentMapName))
            {
                for (int i = 0; i < _registry.Maps.Count; i++)
                {
                    if (_registry.Maps[i] != null && _registry.Maps[i].name == currentMapName)
                    {
                        idx = i;
                        break;
                    }
                }
            }

            Select(idx);
        }

        private void Select(int index)
        {
            if (_registry == null || index < 0 || index >= _registry.Maps.Count) return;

            var map = _registry.Maps[index];
            if (map == null) return;

            _selectedIndex = index;

            if (previewImage != null && map.previewSprite != null)
                previewImage.sprite = map.previewSprite;

            RefreshHighlight();
            OnMapSelected?.Invoke(map);
        }

        private void RebuildList()
        {
            for (int i = 0; i < _pool.Count; i++)
                _pool[i].gameObject.SetActive(false);

            if (_registry == null) return;

            for (int i = 0; i < _registry.Maps.Count; i++)
            {
                var map = _registry.Maps[i];
                if (map == null) continue;

                UIMapItem item = GetOrCreateItem(i);
                item.Setup(i, map.displayName);
                item.gameObject.SetActive(true);
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
