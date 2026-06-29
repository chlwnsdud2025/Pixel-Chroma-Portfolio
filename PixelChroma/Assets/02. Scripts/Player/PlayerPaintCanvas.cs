using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace PixelChroma
{
    public class PlayerPaintCanvas : NetworkBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite[] originalSprites;
        [SerializeField] private int brushSize = 1;

        private Texture2D[] _runtimeTextures;
        private Color[][] _originalPixels;
        private Sprite[] _runtimeSprites;
        private Dictionary<Sprite, Sprite> _spriteMap;
        private int _texWidth;
        private int _texHeight;
        private bool _isDirty;

        public int BrushSize { get => brushSize; set => brushSize = value; }
        public int TextureWidth => _texWidth;
        public int TextureHeight => _texHeight;
        public float PixelsPerUnit => _runtimeSprites != null && _runtimeSprites.Length > 0
            ? _runtimeSprites[0].pixelsPerUnit : 16f;

        void Start()
        {
            InitializeRuntimeTextures();
        }

        void LateUpdate()
        {
            if (_spriteMap == null) return;

            var current = targetRenderer.sprite;
            if (current != null && _spriteMap.TryGetValue(current, out var runtimeSprite))
                targetRenderer.sprite = runtimeSprite;
        }

        private void InitializeRuntimeTextures()
        {
            if (originalSprites == null || originalSprites.Length == 0) return;

            int count = originalSprites.Length;
            var first = originalSprites[0];
            _texWidth = (int)first.rect.width;
            _texHeight = (int)first.rect.height;

            _runtimeTextures = new Texture2D[count];
            _originalPixels = new Color[count][];
            _runtimeSprites = new Sprite[count];
            _spriteMap = new Dictionary<Sprite, Sprite>(count);

            Texture2D lastSourceTex = null;
            Texture2D readableCopy = null;

            for (int i = 0; i < count; i++)
            {
                var srcSprite = originalSprites[i];
                var srcTex = srcSprite.texture;
                var rect = srcSprite.rect;

                if (srcTex != lastSourceTex)
                {
                    if (readableCopy != null) Destroy(readableCopy);
                    readableCopy = CreateReadableCopy(srcTex);
                    lastSourceTex = srcTex;
                }

                _originalPixels[i] = readableCopy.GetPixels(
                    (int)rect.x, (int)rect.y, _texWidth, _texHeight);

                _runtimeTextures[i] = new Texture2D(_texWidth, _texHeight, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                _runtimeTextures[i].SetPixels(_originalPixels[i]);
                _runtimeTextures[i].Apply();

                _runtimeSprites[i] = Sprite.Create(
                    _runtimeTextures[i],
                    new Rect(0, 0, _texWidth, _texHeight),
                    new Vector2(srcSprite.pivot.x / rect.width, srcSprite.pivot.y / rect.height),
                    srcSprite.pixelsPerUnit
                );

                _spriteMap[srcSprite] = _runtimeSprites[i];
            }

            if (readableCopy != null) Destroy(readableCopy);
        }

        private static Texture2D CreateReadableCopy(Texture2D source)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply(false, false);

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }

        public bool PaintAt(int px, int py, Color color)
        {
            bool painted = PaintLocal(px, py, color, brushSize);
            if (painted)
                ApplyChanges();
            return painted;
        }

        private bool PaintLocal(int px, int py, Color color, int size)
        {
            if (_runtimeTextures == null) return false;

            bool anyPainted = false;
            int half = size / 2;

            for (int bx = -half; bx < size - half; bx++)
            {
                for (int by = -half; by < size - half; by++)
                {
                    int x = px + bx;
                    int y = py + by;
                    if (x < 0 || x >= _texWidth || y < 0 || y >= _texHeight) continue;

                    int idx = y * _texWidth + x;

                    for (int i = 0; i < _runtimeTextures.Length; i++)
                    {
                        if (_originalPixels[i][idx].a < 0.01f) continue;
                        _runtimeTextures[i].SetPixel(x, y, color);
                    }

                    anyPainted = true;
                }
            }

            if (anyPainted)
                _isDirty = true;

            return anyPainted;
        }

        public void EraseAt(int px, int py)
        {
            if (_runtimeTextures == null) return;

            int half = brushSize / 2;

            for (int bx = -half; bx < brushSize - half; bx++)
            {
                for (int by = -half; by < brushSize - half; by++)
                {
                    int x = px + bx;
                    int y = py + by;
                    if (x < 0 || x >= _texWidth || y < 0 || y >= _texHeight) continue;

                    int idx = y * _texWidth + x;
                    for (int i = 0; i < _runtimeTextures.Length; i++)
                        _runtimeTextures[i].SetPixel(x, y, _originalPixels[i][idx]);
                }
            }

            _isDirty = true;
            ApplyChanges();
        }

        public void ResetTexture()
        {
            if (_runtimeTextures == null) return;

            for (int i = 0; i < _runtimeTextures.Length; i++)
            {
                _runtimeTextures[i].SetPixels(_originalPixels[i]);
                _runtimeTextures[i].Apply();
            }

            _isDirty = false;
        }

        public void ApplyChanges()
        {
            if (!_isDirty) return;

            for (int i = 0; i < _runtimeTextures.Length; i++)
                _runtimeTextures[i].Apply();

            _isDirty = false;
        }

        public Vector2Int WorldToPixel(Vector3 worldPos)
        {
            Vector3 localPos = targetRenderer.transform.InverseTransformPoint(worldPos);
            float ppu = PixelsPerUnit;

            int px = Mathf.FloorToInt((localPos.x + 0.5f * _texWidth / ppu) * ppu);
            int py = Mathf.FloorToInt((localPos.y + 0.5f * _texHeight / ppu) * ppu);

            return new Vector2Int(px, py);
        }

        public bool IsInBounds(int px, int py)
        {
            return px >= 0 && px < _texWidth && py >= 0 && py < _texHeight;
        }

        private void OnDestroy()
        {
            if (_runtimeTextures == null) return;

            for (int i = 0; i < _runtimeTextures.Length; i++)
            {
                if (_runtimeTextures[i] != null) Destroy(_runtimeTextures[i]);
                if (_runtimeSprites[i] != null) Destroy(_runtimeSprites[i]);
            }
        }
    }
}
