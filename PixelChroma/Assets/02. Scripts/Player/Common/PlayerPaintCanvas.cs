using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;

namespace PixelChroma
{
    // ── 셰이더 마스크 방식 페인트 캔버스 ─────────────────────────────────────────
    //   원본 스프라이트는 건드리지 않고, "어디에 무슨 색을 칠했나"를 페인트 마스크
    //   텍스처 1장에만 기록한다. 렌더링 시 PlayerPaintMask 셰이더가 현재 표시 중인
    //   스프라이트 프레임과 마스크를 실시간 합성한다.
    //   → 프레임(애니메이션) 수와 무관하게 페인트 상태는 포즈당 마스크 1장으로 관리된다.
    //
    //   초기화 전용 코드(InitializeForm, 엠보스 법선 계산 등)는
    //   PlayerPaintCanvas.Init.cs에 분리되어 있다.
    public partial class PlayerPaintCanvas : NetworkBehaviour
    {
        // ── 상수 / 내부 타입 ────────────────────────────────────────────────────

        public enum PaintFormType
        {
            Default,
            Test1,
        }

        private static readonly int PaintMaskID = Shader.PropertyToID("_PaintMask");
        private static readonly int EmbossTexID = Shader.PropertyToID("_EmbossTex");
        private static readonly int MaskRectID = Shader.PropertyToID("_MaskRect");
        private static readonly int ShadingStrengthID = Shader.PropertyToID("_ShadingStrength");
        private static readonly int EdgeHighlightID = Shader.PropertyToID("_EdgeHighlight");
        private static readonly int EdgeShadowID = Shader.PropertyToID("_EdgeShadow");
        private static readonly int LightAngleID = Shader.PropertyToID("_LightAngle");

        [System.Serializable]
        public class PaintForm
        {
            public PaintFormType formType;
            public Sprite[] sprites;

            [Tooltip("이 폼 전용 AnimatorOverrideController. 기본 폼(index 0)은 비워두면 원본 컨트롤러를 사용합니다.")]
            public AnimatorOverrideController overrideController;

            [System.NonSerialized] public int texWidth;
            [System.NonSerialized] public int texHeight;
            [System.NonSerialized] public Texture2D paintMask;
            [System.NonSerialized] public Color32[] maskPixels;
            [System.NonSerialized] public Texture2D[] embossTextures;
            [System.NonSerialized] public Vector4[] maskRects;
            [System.NonSerialized] public bool[] unionOpaque;
            [System.NonSerialized] public bool[] referenceOpaque;
            [System.NonSerialized] public Dictionary<Sprite, int> spriteIndex;
            [System.NonSerialized] public int paintedCells;
            [System.NonSerialized] public int totalOpaquePixels;
            [System.NonSerialized] public Dictionary<int, Fade> fading;
            [System.NonSerialized] public bool isDirty;
        }

        public struct Fade
        {
            public byte startAlpha;
            public float startTime;
        }

        // ── 직렬화 필드 ─────────────────────────────────────────────────────────

        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private PaintForm[] forms = new PaintForm[]
        {
            new PaintForm { formType = PaintFormType.Default }
        };
        [SerializeField] private int brushSize = 1;

        [Tooltip("PixelChroma/PlayerPaintMask 셰이더. 비워두면 Shader.Find로 찾습니다(빌드 포함을 위해 배정 권장).")]
        [SerializeField] private Shader paintShader;

        [Header("Erase Fade (물총 서서히 지우기)")]
        [Tooltip("물총에 맞은 물감이 균일하게 옅어지며 완전히 씻겨나가기까지 걸리는 시간(초)")]
        [SerializeField] private float eraseFadeDuration = 0.5f;

        [Header("입체감 (외곽선 엠보스)")]
        [Tooltip("엠보스 효과 전체 세기. 0 = 완전 단색. 몸통 내부는 항상 페인트색 그대로 유지됨")]
        [Range(0f, 1f)]
        [SerializeField] private float shadingStrength = 1f;
        [Tooltip("빛(위쪽)을 받는 외곽선이 페인트색에서 흰색 쪽으로 밝아지는 정도")]
        [Range(0f, 1f)]
        [SerializeField] private float edgeHighlight = 0.05f;
        [Tooltip("그림자 지는(아래쪽) 외곽선이 페인트색에서 검은색 쪽으로 어두워지는 정도")]
        [Range(0f, 1f)]
        [SerializeField] private float edgeShadow = 0.05f;
        [Tooltip("빛이 들어오는 각도(도). 90 = 바로 위, 135 = 좌상단, 45 = 우상단")]
        [Range(0f, 360f)]
        [SerializeField] private float lightAngle = 135f;

        // ── 런타임 필드 / 이벤트 ────────────────────────────────────────────────

        private PaintForm _activeForm;
        private Dictionary<PaintFormType, PaintForm> _formLookup;
        private int _currentIndex = -1;
        private Material _material;
        private RuntimeAnimatorController _baseAnimatorController;
        private readonly List<int> _fadeRemoveBuffer = new();

        public event System.Action OnEraseFadeCompleted;

        // ── Public 프로퍼티 ─────────────────────────────────────────────────────

        public int BrushSize { get => brushSize; set => brushSize = value; }
        public PaintFormType _activeFormType => _activeForm.formType;
        public int TextureWidth => _activeForm.texWidth;
        public int TextureHeight => _activeForm.texHeight;
        public float PixelsPerUnit
        {
            get
            {
                var form = _activeForm;
                return form.sprites != null && form.sprites.Length > 0
                    ? form.sprites[0].pixelsPerUnit : 16f;
            }
        }
        public int FormCount => forms != null ? forms.Length : 0;
        public int SortingLayerID => targetRenderer != null ? targetRenderer.sortingLayerID : 0;
        public int SortingOrder => targetRenderer != null ? targetRenderer.sortingOrder : 0;

        // ── Unity 라이프사이클 ──────────────────────────────────────────────────

        void Start()
        {
            InitializeAllForms();
        }

        void Update()
        {
            ProcessFading();
        }

        void LateUpdate()
        {
            var form = _activeForm;
            if (form == null || form.spriteIndex == null || _material == null) return;

            var current = targetRenderer.sprite;
            if (current == null) return;

            if (form.spriteIndex.TryGetValue(current, out int index) && index != _currentIndex)
            {
                _currentIndex = index;
                ApplyFrameToMaterial(form, index);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            PushShadingParams();
        }
#endif

        private void OnDestroy()
        {
            if (forms != null)
            {
                foreach (var form in forms)
                {
                    if (form.embossTextures != null)
                    {
                        for (int i = 0; i < form.embossTextures.Length; i++)
                            if (form.embossTextures[i] != null) Destroy(form.embossTextures[i]);
                    }
                    if (form.paintMask != null) Destroy(form.paintMask);
                }
            }

            if (_material != null) Destroy(_material);
        }

        // ── 포즈 전환 ───────────────────────────────────────────────────────────

        public void SwitchForm(PaintFormType formType)
        {
            if (_formLookup == null || !_formLookup.TryGetValue(formType, out var form)) return;
            if (form == _activeForm) return;

            ActivateForm(formType);
            CmdSwitchForm(formType);
        }

        private void ActivateForm(PaintFormType formType)
        {
            if (_formLookup == null || !_formLookup.TryGetValue(formType, out var form)) return;

            _activeForm = form;

            if (_material != null && form.paintMask != null)
                _material.SetTexture(PaintMaskID, form.paintMask);

            if (targetAnimator != null && _baseAnimatorController != null)
            {
                targetAnimator.runtimeAnimatorController =
                    form.overrideController != null
                        ? form.overrideController
                        : _baseAnimatorController;
            }
        }

        [Command]
        private void CmdSwitchForm(PaintFormType formType)
        {
            RpcSwitchForm(formType);
        }

        [ClientRpc]
        private void RpcSwitchForm(PaintFormType formType)
        {
            if (isOwned) return;
            ActivateForm(formType);
        }

        // ── 색칠 ────────────────────────────────────────────────────────────────

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

        private bool PaintLocal(int px, int py, Color color, int size)
        {
            var form = _activeForm;
            if (form.maskPixels == null) return false;

            bool anyPainted = false;
            GetBrushBounds(form, px, py, size, out int x0, out int y0, out int x1, out int y1);

            for (int x = x0; x < x1; x++)
            {
                for (int y = y0; y < y1; y++)
                {
                    int idx = y * form.texWidth + x;
                    if (!form.unionOpaque[idx]) continue;

                    SetMaskCell(form, idx, color);

                    if (form.fading.Count > 0) form.fading.Remove(idx);

                    anyPainted = true;
                }
            }

            if (anyPainted) form.isDirty = true;
            return anyPainted;
        }

        private static void SetMaskCell(PaintForm form, int idx, Color color)
        {
            if (form.maskPixels[idx].a == 0 && form.referenceOpaque[idx]) form.paintedCells++;
            form.maskPixels[idx] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
                255);
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

        // ── 지우기 (즉시) ───────────────────────────────────────────────────────

        public void EraseAt(int px, int py)
        {
            if (!EraseLocal(px, py, brushSize)) return;

            ApplyChanges();
            CmdErase(px, py, brushSize);
        }

        private bool EraseLocal(int px, int py, int size)
        {
            var form = _activeForm;
            if (form.maskPixels == null) return false;

            GetBrushBounds(form, px, py, size, out int x0, out int y0, out int x1, out int y1);
            if (x0 >= x1 || y0 >= y1) return false;

            for (int x = x0; x < x1; x++)
            {
                for (int y = y0; y < y1; y++)
                {
                    int idx = y * form.texWidth + x;
                    ClearMaskCell(form, idx);
                    if (form.fading.Count > 0) form.fading.Remove(idx);
                }
            }

            form.isDirty = true;
            return true;
        }

        private static void ClearMaskCell(PaintForm form, int idx)
        {
            if (form.maskPixels[idx].a != 0 && form.referenceOpaque[idx]) form.paintedCells--;
            form.maskPixels[idx] = new Color32(0, 0, 0, 0);
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

            if (EraseLocal(px, py, size))
                ApplyChanges();
        }

        // ── 페이드 (물총 피격 → 서서히 지우기) ─────────────────────────────────

        private void ProcessFading()
        {
            var form = _activeForm;
            if (form.fading == null || form.fading.Count == 0) return;

            float now = Time.time;
            _fadeRemoveBuffer.Clear();

            foreach (var kv in form.fading)
            {
                int idx = kv.Key;
                Fade f = kv.Value;
                float t = eraseFadeDuration <= 0f ? 1f : (now - f.startTime) / eraseFadeDuration;

                if (t >= 1f)
                {
                    ClearMaskCell(form, idx);
                    _fadeRemoveBuffer.Add(idx);
                }
                else
                {
                    var c = form.maskPixels[idx];
                    c.a = (byte)(f.startAlpha * (1f - t));
                    form.maskPixels[idx] = c;
                }
            }

            for (int i = 0; i < _fadeRemoveBuffer.Count; i++)
                form.fading.Remove(_fadeRemoveBuffer[i]);

            form.isDirty = true;
            ApplyChanges();

            if (form.fading.Count == 0)
                OnEraseFadeCompleted?.Invoke();
        }

        public void EraseArea(Vector2Int center, int pixelRadius)
        {
            var form = _activeForm;
            if (form.maskPixels == null) return;

            int r2 = pixelRadius * pixelRadius;
            for (int dx = -pixelRadius; dx <= pixelRadius; dx++)
                for (int dy = -pixelRadius; dy <= pixelRadius; dy++)
                {
                    if (dx * dx + dy * dy > r2) continue;
                    int px = center.x + dx, py = center.y + dy;
                    if (!IsInBounds(px, py)) continue;
                    EnqueueFade(form, px, py);
                }
        }

        private void EnqueueFade(PaintForm form, int px, int py)
        {
            int idx = py * form.texWidth + px;
            byte a = form.maskPixels[idx].a;
            if (a == 0) return;

            form.fading[idx] = new Fade { startAlpha = a, startTime = Time.time };
        }

        // ── 판정 ────────────────────────────────────────────────────────────────

        public float GetPaintRatio()
        {
            var form = _activeForm;
            if (form.maskPixels == null || form.totalOpaquePixels == 0) return 0f;
            return (float)form.paintedCells / form.totalOpaquePixels;
        }

        public bool HasAnyPaintedPixels()
        {
            var form = _activeForm;
            if (form.maskPixels == null) return true;
            return form.paintedCells > 0;
        }

        // ── 좌표 변환 / 브러시 유틸리티 ─────────────────────────────────────────

        public Vector2Int WorldToPixel(Vector3 worldPos)
        {
            if (targetRenderer == null) return Vector2Int.zero;

            var form = _activeForm;
            Vector3 localPos = targetRenderer.transform.InverseTransformPoint(worldPos);
            float ppu = PixelsPerUnit;

            int px = Mathf.FloorToInt((localPos.x + 0.5f * form.texWidth / ppu) * ppu);
            int py = Mathf.FloorToInt((localPos.y + 0.5f * form.texHeight / ppu) * ppu);

            return new Vector2Int(px, py);
        }

        public bool IsInBounds(int px, int py)
        {
            var form = _activeForm;
            return px >= 0 && px < form.texWidth && py >= 0 && py < form.texHeight;
        }

        public Vector3 GetBrushWorldCenter(int px, int py, int size)
        {
            if (targetRenderer == null) return Vector3.zero;

            var form = _activeForm;
            float ppu = PixelsPerUnit;
            int half = size / 2;
            float localX = (px - half + 0.5f * size) / ppu - 0.5f * form.texWidth / ppu;
            float localY = (py - half + 0.5f * size) / ppu - 0.5f * form.texHeight / ppu;
            return targetRenderer.transform.TransformPoint(new Vector3(localX, localY, 0f));
        }

        public float GetBrushWorldSize(int size)
        {
            float scale = targetRenderer != null ? Mathf.Abs(targetRenderer.transform.lossyScale.x) : 1f;
            return size / PixelsPerUnit * scale;
        }

        // ── 텍스처 · 머티리얼 유틸리티 ──────────────────────────────────────────

        public void ResetTexture()
        {
            var form = _activeForm;
            if (form.maskPixels == null) return;

            form.fading.Clear();
            System.Array.Clear(form.maskPixels, 0, form.maskPixels.Length);
            form.paintedCells = 0;
            form.paintMask.SetPixels32(form.maskPixels);
            form.paintMask.Apply();
            form.isDirty = false;
        }

        public void ApplyChanges()
        {
            var form = _activeForm;
            if (!form.isDirty) return;

            form.paintMask.SetPixels32(form.maskPixels);
            form.paintMask.Apply();
            form.isDirty = false;
        }

        private void ApplyFrameToMaterial(PaintForm form, int index)
        {
            _material.SetVector(MaskRectID, form.maskRects[index]);
            _material.SetTexture(EmbossTexID, form.embossTextures[index]);
        }

        private void PushShadingParams()
        {
            if (_material == null) return;
            _material.SetFloat(ShadingStrengthID, shadingStrength);
            _material.SetFloat(EdgeHighlightID, edgeHighlight);
            _material.SetFloat(EdgeShadowID, edgeShadow);
            _material.SetFloat(LightAngleID, lightAngle);
        }

        private void GetBrushBounds(PaintForm form, int px, int py, int size,
            out int x0, out int y0, out int x1, out int y1)
        {
            int half = size / 2;
            x0 = Mathf.Max(px - half, 0);
            y0 = Mathf.Max(py - half, 0);
            x1 = Mathf.Min(px + size - half, form.texWidth);
            y1 = Mathf.Min(py + size - half, form.texHeight);
        }

        // ── 에디터 전용 ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
        [ContextMenu("Animator에서 스프라이트 수집 (다음 폼에 추가)")]
        private void CollectSpritesFromAnimator()
        {
            var anim = GetComponent<Animator>();
            if (anim == null || anim.runtimeAnimatorController == null)
            {
                Debug.LogWarning("Animator 또는 AnimatorController가 없습니다.", this);
                return;
            }

            var clips = anim.runtimeAnimatorController.animationClips;
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
                Debug.LogWarning("AnimationClip에서 스프라이트를 찾을 수 없습니다.", this);
                return;
            }

            if (forms == null) forms = System.Array.Empty<PaintForm>();

            var usedTypes = new HashSet<PaintFormType>();
            foreach (var f in forms) usedTypes.Add(f.formType);

            PaintFormType newType = default;
            foreach (PaintFormType t in System.Enum.GetValues(typeof(PaintFormType)))
            {
                if (!usedTypes.Contains(t)) { newType = t; break; }
            }

            int newIndex = forms.Length;
            System.Array.Resize(ref forms, newIndex + 1);
            forms[newIndex] = new PaintForm
            {
                formType = newType,
                sprites = sprites.OrderBy(s => s.name).ToArray()
            };

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[{newType}] 스프라이트 {sprites.Count}개를 수집하여 인덱스 {newIndex}에 추가했습니다.", this);
        }
#endif
    }
}
