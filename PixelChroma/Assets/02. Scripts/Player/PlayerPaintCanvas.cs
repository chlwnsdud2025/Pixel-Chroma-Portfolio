using System.Collections.Generic;
using System.Linq;
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
            {
                ApplyChanges();
                CmdPaint(px, py, color, brushSize);
            }
            return painted;
        }

        [Command]
        private void CmdPaint(int px, int py, Color color, int size)
        {
            RpcPaint(px, py, color, size);
        }

        [ClientRpc]
        private void RpcPaint(int px, int py, Color color, int size)
        {
            if (isOwned) return;

            if (PaintLocal(px, py, color, size))
                ApplyChanges();
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

            EraseLocal(px, py, brushSize);
            ApplyChanges();
            CmdErase(px, py, brushSize);
        }

        private void EraseLocal(int px, int py, int size)
        {
            if (_runtimeTextures == null) return;

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
                        _runtimeTextures[i].SetPixel(x, y, _originalPixels[i][idx]);
                }
            }

            _isDirty = true;
        }

        [Command]
        private void CmdErase(int px, int py, int size)
        {
            RpcErase(px, py, size);
        }

        [ClientRpc]
        private void RpcErase(int px, int py, int size)
        {
            if (isOwned) return;

            EraseLocal(px, py, size);
            ApplyChanges();
        }

        // ── 💦 물총 피격 및 생존 검사용 신규 로직 ──────────────────────────────────

        /// <summary>
        /// 네트워크 통신 없이 픽셀 1개만 원본 색상으로 조용히 복구합니다.
        /// RunnerPlayer에서 물총 범위를 대량으로 씻어낼 때 RPC 폭주를 막기 위해 사용됩니다.
        /// </summary>
        public void RestorePixelLocal(int px, int py)
        {
            if (_runtimeTextures == null) return;
            if (!IsInBounds(px, py)) return;

            int idx = py * _texWidth + px;
            for (int i = 0; i < _runtimeTextures.Length; i++)
            {
                _runtimeTextures[i].SetPixel(px, py, _originalPixels[i][idx]);
            }
            _isDirty = true;
        }

        /// <summary>
        /// 캔버스 전체를 검사하여 덧칠된 페인트가 하나라도 남아있는지 확인합니다.
        /// (true = 살아있음 / false = 다 씻겨서 죽음)
        /// </summary>
        public bool HasAnyPaintedPixels()
        {
            if (_runtimeTextures == null || _runtimeTextures.Length == 0) return true;

            // 메모리 할당(GC)을 막기 위해 GetPixels() 배열 생성 대신 GetPixel() 루프를 돌립니다.
            for (int y = 0; y < _texHeight; y++)
            {
                for (int x = 0; x < _texWidth; x++)
                {
                    int idx = y * _texWidth + x;
                    Color original = _originalPixels[0][idx];

                    // 애초에 칠할 수 없는 투명한 배경 부분은 검사에서 제외합니다.
                    if (original.a < 0.01f) continue;

                    Color current = _runtimeTextures[0].GetPixel(x, y);

                    // 픽셀 색상이 원본과 다르면 (덧칠해져 있으면) 아직 살아있는 것입니다!
                    if (current != original)
                    {
                        return true;
                    }
                }
            }

            // 모든 픽셀이 원본 색상과 동일하다면 페인트가 완전히 씻겨 내려간 것입니다.
            return false;
        }

        // ────────────────────────────────────────────────────────────────────────

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

        public Color GetPixelColor(int px, int py)
        {
            if (_runtimeTextures == null || _runtimeTextures.Length == 0) return Color.white;
            if (!IsInBounds(px, py)) return Color.white;

            var current = targetRenderer.sprite;
            if (current != null && _spriteMap != null)
            {
                for (int i = 0; i < _runtimeSprites.Length; i++)
                {
                    if (_runtimeSprites[i] == current || _spriteMap.ContainsKey(current) && _spriteMap[current] == _runtimeSprites[i])
                        return _runtimeTextures[i].GetPixel(px, py);
                }
            }

            return _runtimeTextures[0].GetPixel(px, py);
        }

#if UNITY_EDITOR
        [ContextMenu("Animator에서 스프라이트 자동 수집")]
        private void CollectSpritesFromAnimator()
        {
            var animator = GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("Animator 또는 AnimatorController가 없습니다.", this);
                return;
            }

            var clips = animator.runtimeAnimatorController.animationClips;
            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning("AnimationClip이 없습니다.", this);
                return;
            }

            var sprites = new HashSet<Sprite>();

            foreach (var clip in clips)
            {
                var bindings = UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip);
                foreach (var binding in bindings)
                {
                    if (binding.type != typeof(SpriteRenderer) ||
                        binding.propertyName != "m_Sprite")
                        continue;

                    var keyframes = UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    foreach (var kf in keyframes)
                    {
                        if (kf.value is Sprite sprite)
                            sprites.Add(sprite);
                    }
                }
            }

            if (sprites.Count == 0)
            {
                Debug.LogWarning("Animator의 AnimationClip에서 스프라이트를 찾을 수 없습니다.", this);
                return;
            }

            originalSprites = sprites.OrderBy(s => s.name).ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"스프라이트 {originalSprites.Length}개를 수집했습니다.", this);
        }
#endif

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