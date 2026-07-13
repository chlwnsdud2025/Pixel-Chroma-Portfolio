using System.Collections.Generic;
using UnityEngine;

namespace PixelChroma
{
    // ── 초기화 전용 ─────────────────────────────────────────────────────────────
    //   Start에서 한 번 실행되는 초기화 로직을 모아둔 partial 파일.
    //   원본 픽셀 읽기, 엠보스 법선 계산, 실루엣 빌드, 마스크 생성 등
    //   런타임에는 다시 호출되지 않는 코드만 포함한다.
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

            // ── 1) 원본 픽셀 읽기 ──────────────────────────────────────────────
            // 스프라이트 시트의 Read/Write가 꺼져 있어도 읽을 수 있도록 GPU→CPU 복사본을
            // 만든 뒤, 프레임별 영역을 잘라 originalPixels[프레임][픽셀]에 저장한다.
            // 같은 시트를 공유하는 프레임이 연속되면 복사본을 재사용한다.
            // 이 데이터는 아래 2·3단계에서만 쓰이고, 이 메서드가 끝나면 GC 대상이 된다.
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

            // ── 2) 엠보스 법선 + 실루엣 빌드 ──────────────────────────────────
            // 프레임별 원본 픽셀의 알파를 보고 테두리 법선을 계산한다.
            // 동시에 프레임별 불투명 픽셀을 순회하며:
            //   - unionOpaque: 어느 프레임에서든 몸인 칸 (칠 허용 판정용)
            //   - referenceOpaque: 프레임 0의 실루엣 (칠한 비율 판정 기준)
            //   - embossTextures[i]: 법선을 텍스처로 구워 셰이더에 전달
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

            // ── 3) 기준 실루엣의 불투명 픽셀 수 카운트 ────────────────────────
            // GetPaintRatio()에서 paintedCells / totalOpaquePixels 로 O(1) 비율 판정에 사용
            form.totalOpaquePixels = 0;
            for (int i = 0; i < pixelCount; i++)
                if (form.referenceOpaque[i]) form.totalOpaquePixels++;

            // ── 4) 빈 페인트 마스크 생성 ───────────────────────────────────────
            // rgba 전부 0인 텍스처 1장 = "아직 아무것도 안 칠한 상태"
            // 이후 PaintLocal()이 여기에 색을 기록하고, 셰이더가 원본과 합성한다.
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

        // ── 외곽선 엠보스 법선 계산 ─────────────────────────────────────────────
        //   투명 픽셀과 맞닿은 실루엣 테두리 픽셀만 골라, 투명한 쪽을 향하는 방향(법선)을
        //   계산한다. 결과는 BuildNormalPixels()로 텍스처 픽셀(rg=법선, b=테두리 플래그)로
        //   인코딩되어 GPU 셰이더(PlayerPaintMask)에 전달된다.

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
