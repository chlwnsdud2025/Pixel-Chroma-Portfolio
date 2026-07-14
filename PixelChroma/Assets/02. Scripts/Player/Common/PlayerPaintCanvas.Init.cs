using System.Collections.Generic;
using UnityEngine;

namespace PixelChroma
{
    // Start에서 한 번만 실행되는 초기화(원본 픽셀 읽기, 엠보스 법선, 실루엣, 마스크 생성).
    public partial class PlayerPaintCanvas
    {
        private void InitializeAllForms()
        {
            if (forms == null || forms.Length == 0) return;

            var shader = paintShader != null ? paintShader : Shader.Find("PixelChroma/PlayerPaintMask");
            if (shader == null)
            {
                Debug.LogError("PlayerPaintMask 셰이더를 찾을 수 없습니다. paintShader를 인스펙터에 배정하세요.", this);
                return;
            }

            _material = new Material(shader) { name = "PlayerPaintMask (Instance)" };
            targetRenderer.material = _material;
            PushShadingParams();

            if (targetAnimator != null)
                _baseAnimatorController = targetAnimator.runtimeAnimatorController;

            _formLookup = new Dictionary<PaintFormType, PaintForm>(forms.Length);
            for (int i = 0; i < forms.Length; i++)
            {
                InitializeForm(forms[i]);
                _formLookup[forms[i].formType] = forms[i];
            }

            ActivateForm(PaintFormType.Default);
        }

        private void InitializeForm(PaintForm form)
        {
            if (form.sprites == null || form.sprites.Length == 0) return;

            int count = form.sprites.Length;
            var first = form.sprites[0];
            form.texWidth = (int)first.rect.width;
            form.texHeight = (int)first.rect.height;
            int pixelCount = form.texWidth * form.texHeight;

            form.embossTextures = new Texture2D[count];
            form.maskRects = new Vector4[count];
            form.spriteIndex = new Dictionary<Sprite, int>(count);
            form.fading = new Dictionary<int, Fade>();
            form.undo = new List<PaintCommand>();
            form.redo = new List<PaintCommand>();

            // 1) 원본 픽셀 읽기. Read/Write 꺼진 시트도 읽도록 GPU→CPU 복사본을 만들어
            //    프레임별로 잘라 originalPixels에 저장(연속된 같은 시트는 복사본 재사용).
            var originalPixels = new Color[count][];
            Texture2D lastSourceTex = null;
            Texture2D readableCopy = null;

            for (int i = 0; i < count; i++)
            {
                var srcSprite = form.sprites[i];
                var srcTex = srcSprite.texture;
                var rect = srcSprite.rect;

                if (srcTex != lastSourceTex)
                {
                    if (readableCopy != null) Destroy(readableCopy);
                    readableCopy = CreateReadableCopy(srcTex);
                    lastSourceTex = srcTex;
                }

                originalPixels[i] = readableCopy.GetPixels(
                    (int)rect.x, (int)rect.y, form.texWidth, form.texHeight);
                form.spriteIndex[srcSprite] = i;
                form.maskRects[i] = ComputeMaskRect(srcSprite);
            }

            if (readableCopy != null) Destroy(readableCopy);

            // 2) 엠보스 법선 + 실루엣 빌드. unionOpaque=어느 프레임에서든 몸인 칸(칠 허용),
            //    referenceOpaque=프레임0 실루엣(칠 비율 기준), embossTextures=법선을 구운 텍스처.
            var edgeNormals = BuildEdgeNormals(originalPixels, form.texWidth, form.texHeight);

            form.unionOpaque = new bool[pixelCount];
            form.referenceOpaque = new bool[pixelCount];
            for (int i = 0; i < count; i++)
            {
                for (int k = 0; k < pixelCount; k++)
                {
                    bool opaque = originalPixels[i][k].a >= 0.01f;
                    if (opaque) form.unionOpaque[k] = true;
                    if (i == 0) form.referenceOpaque[k] = opaque;
                }

                var tex = new Texture2D(form.texWidth, form.texHeight, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                tex.SetPixels32(BuildNormalPixels(edgeNormals[i]));
                tex.Apply();
                form.embossTextures[i] = tex;
            }

            // 3) 기준 실루엣 불투명 픽셀 수(GetPaintRatio의 분모).
            form.totalOpaquePixels = 0;
            for (int i = 0; i < pixelCount; i++)
                if (form.referenceOpaque[i]) form.totalOpaquePixels++;

            // 4) 빈 페인트 마스크(rgba=0) 생성.
            form.paintMask = new Texture2D(form.texWidth, form.texHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            form.maskPixels = new Color32[pixelCount];
            form.paintMask.SetPixels32(form.maskPixels);
            form.paintMask.Apply();
            form.paintedCells = 0;
            form.isDirty = false;
        }

        // ── 텍스처 읽기 유틸리티 ────────────────────────────────────────────────

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

        private static Vector4 ComputeMaskRect(Sprite sprite)
        {
            var uv = sprite.uv;
            if (uv == null || uv.Length == 0)
            {
                var tex = sprite.texture;
                var r = sprite.rect;

                return new Vector4(r.x / tex.width, r.y / tex.height,
                    r.width / tex.width, r.height / tex.height);
            }

            Vector2 min = uv[0], max = uv[0];
            for (int i = 1; i < uv.Length; i++)
            {
                min = Vector2.Min(min, uv[i]);
                max = Vector2.Max(max, uv[i]);
            }

            return new Vector4(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        // 실루엣 테두리 픽셀에서 투명한 쪽을 향하는 법선을 계산(BuildNormalPixels가 셰이더용
        // 텍스처로 인코딩).
        private static Vector2[][] BuildEdgeNormals(Color[][] originalPixels, int texWidth, int texHeight)
        {
            int count = originalPixels.Length;
            int pixelCount = texWidth * texHeight;
            var edgeNormals = new Vector2[count][];

            for (int i = 0; i < count; i++)
            {
                var pixels = originalPixels[i];
                var normals = new Vector2[pixelCount];
                edgeNormals[i] = normals;

                for (int y = 0; y < texHeight; y++)
                {
                    for (int x = 0; x < texWidth; x++)
                    {
                        int idx = y * texWidth + x;
                        if (pixels[idx].a < 0.01f) continue;

                        float nx = 0f, ny = 0f;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                if (IsTransparentAt(pixels, x + dx, y + dy, texWidth, texHeight))
                                {
                                    nx += dx;
                                    ny += dy;
                                }
                            }
                        }

                        float len = Mathf.Sqrt(nx * nx + ny * ny);
                        if (len < 0.001f) continue;

                        normals[idx] = new Vector2(nx / len, ny / len);
                    }
                }
            }

            return edgeNormals;
        }

        /// <summary>
        /// 외곽선 법선을 셰이더용 텍스처 픽셀로 인코딩한다.
        /// rg = normal.xy(0~1로 인코딩), b = 테두리 플래그(테두리 255, 내부/투명 0).
        /// 셰이더에서 edge = (rg*2-1)·빛방향 * b 로 복원한다.
        /// </summary>
        private static Color32[] BuildNormalPixels(Vector2[] normals)
        {
            var pixels = new Color32[normals.Length];
            for (int i = 0; i < normals.Length; i++)
            {
                Vector2 n = normals[i];
                bool isEdge = n.x != 0f || n.y != 0f;
                byte r = (byte)Mathf.Clamp(Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), 0, 255);
                byte g = (byte)Mathf.Clamp(Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), 0, 255);
                pixels[i] = new Color32(r, g, (byte)(isEdge ? 255 : 0), 255);
            }
            return pixels;
        }

        private static bool IsTransparentAt(Color[] pixels, int x, int y, int texWidth, int texHeight)
        {
            if (x < 0 || x >= texWidth || y < 0 || y >= texHeight) return true;
            return pixels[y * texWidth + x].a < 0.01f;
        }
    }
}
