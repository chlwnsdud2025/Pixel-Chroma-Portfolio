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

        [Header("Erase Fade (물총 서서히 지우기)")]
        [Tooltip("물총에 맞은 물감이 균일하게 옅어지며 완전히 씻겨나가기까지 걸리는 시간(초)")]
        [SerializeField] private float eraseFadeDuration = 0.5f;

        [Header("입체감 (외곽선 엠보스)")]
        [Tooltip("엠보스 효과 전체 세기. 0 = 완전 단색(기존 방식). 몸통 내부는 항상 페인트색 그대로 유지됨")]
        [Range(0f, 1f)]
        [SerializeField] private float shadingStrength = 1f;
        [Tooltip("빛(위쪽)을 받는 외곽선이 페인트색에서 흰색 쪽으로 밝아지는 정도")]
        [Range(0f, 1f)]
        [SerializeField] private float edgeHighlight = 0.05f;
        [Tooltip("그림자 지는(아래쪽) 외곽선이 페인트색에서 검은색 쪽으로 어두워지는 정도")]
        [Range(0f, 1f)]
        [SerializeField] private float edgeShadow = 0.05f;
        [Tooltip("빛이 들어오는 각도(도). 90 = 바로 위에서 수직, 135 = 좌상단, 45 = 우상단, 270 = 아래에서 위로")]
        [Range(0f, 360f)]
        [SerializeField] private float lightAngle = 135f;

        private Texture2D[] _runtimeTextures;
        private Color[][] _originalPixels;
        private Sprite[] _runtimeSprites;
        private Dictionary<Sprite, Sprite> _spriteMap;
        private int _texWidth;
        private int _texHeight;
        private bool _isDirty;
        private Vector2[][] _edgeNormals; // [텍스처][픽셀] 실루엣 테두리의 바깥쪽 법선 (내부 픽셀은 zero)

        // ── 페이드(서서히 지우기) 상태 · 픽셀 인덱스(idx = y*width + x) 기준 ──
        private Color[][] _fadeStart;      // [텍스처][픽셀] 페이드 시작색
        private float[] _fadeStartTime;    // [픽셀] 페이드 시작 절대시간
        private readonly HashSet<int> _fadingPixels = new();
        private readonly List<int> _fadeRemoveBuffer = new();

        /// <summary>물총 페이드가 모두 끝나 더 이상 지워질 픽셀이 없을 때 호출됩니다. (아웃 판정용)</summary>
        public event System.Action OnEraseFadeCompleted;

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

            // 페이드 상태 배열 할당 (텍스처당 픽셀 수 만큼)
            int pixelCount = _texWidth * _texHeight;
            _fadeStart = new Color[count][];
            for (int i = 0; i < count; i++) _fadeStart[i] = new Color[pixelCount];
            _fadeStartTime = new float[pixelCount];
            _fadingPixels.Clear();

            BuildShadeMasks();

            if (readableCopy != null) Destroy(readableCopy);
        }

        // ── 외곽선 엠보스 법선 ───────────────────────────────────────────────────
        //   투명 픽셀과 맞닿은 실루엣 테두리 픽셀만 골라, 투명한 쪽을 향하는 방향(법선)을
        //   저장합니다. 칠하는 순간 lightAngle 방향과 내적해서 빛을 받는 테두리는 밝게,
        //   반대쪽 테두리는 어둡게 하여, 배경과 완전히 같은 색을 칠해도 물체가
        //   튀어나와 보이는 엠보스 효과를 만듭니다. 몸통 내부(눈 포함)는 법선이 zero라
        //   페인트색이 그대로 유지됩니다.
        //   모든 클라이언트가 같은 원본에서 같은 계산을 하므로 네트워크 동기화에 영향이 없습니다.

        private void BuildShadeMasks()
        {
            int count = _originalPixels.Length;
            int pixelCount = _texWidth * _texHeight;
            _edgeNormals = new Vector2[count][];

            for (int i = 0; i < count; i++)
            {
                var pixels = _originalPixels[i];
                var normals = new Vector2[pixelCount];
                _edgeNormals[i] = normals;

                for (int y = 0; y < _texHeight; y++)
                {
                    for (int x = 0; x < _texWidth; x++)
                    {
                        int idx = y * _texWidth + x;
                        if (pixels[idx].a < 0.01f) continue; // 투명 픽셀은 칠해지지 않음

                        // 8방향 이웃 중 투명한 쪽으로 향하는 벡터를 합산 → 바깥쪽 법선
                        float nx = 0f, ny = 0f;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                if (IsTransparentAt(pixels, x + dx, y + dy))
                                {
                                    nx += dx;
                                    ny += dy;
                                }
                            }
                        }

                        float len = Mathf.Sqrt(nx * nx + ny * ny);
                        if (len < 0.001f) continue; // 테두리가 아닌 내부 픽셀 → zero (페인트색 그대로)

                        normals[idx] = new Vector2(nx / len, ny / len);
                    }
                }
            }
        }

        /// <summary>인스펙터의 lightAngle(도)을 방향 벡터로 변환합니다. 90도 = 바로 위.</summary>
        private Vector2 LightDirection =>
            new Vector2(Mathf.Cos(lightAngle * Mathf.Deg2Rad), Mathf.Sin(lightAngle * Mathf.Deg2Rad));

        /// <summary>텍스처 경계 밖은 투명으로 취급합니다.</summary>
        private bool IsTransparentAt(Color[] pixels, int x, int y)
        {
            if (x < 0 || x >= _texWidth || y < 0 || y >= _texHeight) return true;
            return pixels[y * _texWidth + x].a < 0.01f;
        }

        /// <summary>
        /// 페인트색에 외곽선 엠보스를 적용합니다. edge = 법선·빛방향 내적.
        /// 빛을 받는 테두리(edge > 0)는 흰색 쪽으로, 반대쪽 테두리(edge < 0)는 검은색 쪽으로
        /// 보정하고, 내부(edge == 0)는 원색 그대로 둡니다.
        /// </summary>
        private Color ApplyEdgeEmboss(Color color, float edge)
        {
            if (edge == 0f || shadingStrength <= 0f) return color;

            Color result = edge > 0f
                ? Color.Lerp(color, Color.white, edge * edgeHighlight * shadingStrength)
                : Color.Lerp(color, Color.black, -edge * edgeShadow * shadingStrength);
            result.a = color.a;
            return result;
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
            Vector2 light = LightDirection;

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
                        Vector2 n = _edgeNormals[i][idx];
                        _runtimeTextures[i].SetPixel(x, y, ApplyEdgeEmboss(color, n.x * light.x + n.y * light.y));
                    }

                    // 페이드 중인 픽셀을 다시 칠하면 지워짐(페이드)을 취소해 러너가 버틸 수 있게 함
                    if (_fadingPixels.Count > 0) _fadingPixels.Remove(idx);

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

        // ── 물총 피격 생존 검사 ──────────────────────────────────────────────────

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

        // ── 물총 피격 "서서히 지우기(페이드)" 로직 ───────────────────────────────
        //   맞은 원형 영역의 물감이 eraseFadeDuration 동안 균일하게 옅어지며 원본색으로 돌아갑니다.

        void Update()
        {
            if (_fadingPixels.Count == 0) return;

            float now = Time.time;
            _fadeRemoveBuffer.Clear();

            foreach (int idx in _fadingPixels)
            {
                int x = idx % _texWidth;
                int y = idx / _texWidth;
                float t = eraseFadeDuration <= 0f ? 1f : (now - _fadeStartTime[idx]) / eraseFadeDuration;

                if (t >= 1f)
                {
                    // 마지막 프레임은 부동소수점 오차 없이 원본색으로 정확히 스냅
                    for (int i = 0; i < _runtimeTextures.Length; i++)
                        _runtimeTextures[i].SetPixel(x, y, _originalPixels[i][idx]);
                    _fadeRemoveBuffer.Add(idx);
                }
                else
                {
                    for (int i = 0; i < _runtimeTextures.Length; i++)
                        _runtimeTextures[i].SetPixel(x, y, Color.Lerp(_fadeStart[i][idx], _originalPixels[i][idx], t));
                }
            }

            for (int i = 0; i < _fadeRemoveBuffer.Count; i++)
                _fadingPixels.Remove(_fadeRemoveBuffer[i]);

            for (int i = 0; i < _runtimeTextures.Length; i++)
                _runtimeTextures[i].Apply();

            // 모든 페이드가 끝났으면 아웃 판정 콜백 호출
            if (_fadingPixels.Count == 0)
                OnEraseFadeCompleted?.Invoke();
        }

        /// <summary>물총에 맞은 원형 영역을 서서히 지웁니다. (RunnerPlayer가 호출)</summary>
        public void EraseArea(Vector2Int center, int pixelRadius)
        {
            if (_runtimeTextures == null) return;

            int r2 = pixelRadius * pixelRadius;
            for (int dx = -pixelRadius; dx <= pixelRadius; dx++)
                for (int dy = -pixelRadius; dy <= pixelRadius; dy++)
                {
                    if (dx * dx + dy * dy > r2) continue;
                    int px = center.x + dx, py = center.y + dy;
                    if (!IsInBounds(px, py)) continue;
                    EnqueueFade(px, py);
                }
        }

        /// <summary>
        /// 픽셀 하나를 페이드 대상으로 등록합니다. 실제로 덧칠된 픽셀만 등록되며,
        /// 현재 색을 시작색으로 캡처해 eraseFadeDuration 동안 원본색으로 되돌립니다.
        /// </summary>
        private void EnqueueFade(int px, int py)
        {
            int idx = py * _texWidth + px;

            bool painted = false;
            for (int i = 0; i < _runtimeTextures.Length; i++)
            {
                Color c = _runtimeTextures[i].GetPixel(px, py);
                _fadeStart[i][idx] = c;
                if (c != _originalPixels[i][idx]) painted = true;
            }
            if (!painted) return; // 원본 그대로면(안 칠해진 픽셀) 지울 것이 없음

            _fadeStartTime[idx] = Time.time;
            _fadingPixels.Add(idx);
        }

        // ────────────────────────────────────────────────────────────────────────

        public void ResetTexture()
        {
            if (_runtimeTextures == null) return;

            _fadingPixels.Clear(); // 진행 중이던 페이드도 초기화

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