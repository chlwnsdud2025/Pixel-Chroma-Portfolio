#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OneText.UGUI;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.TextCore.LowLevel;

namespace PixelChroma.EditorTools
{
    /// <summary>
    /// String Table에서 실제 사용된 문자만 뽑아 TMP Font Asset Creator의
    /// "Characters from File"용 텍스트 파일을 만든다.
    ///
    /// 언어별로 파일을 쪼개던 <see cref="LocalizationCharSetExporter"/>와 달리
    /// 여러 로케일을 묶어 "폰트 하나에 들어갈 한 덩어리"로 뽑는다.
    /// (예: M PLUS Rounded 1c 하나로 라틴 8개 + 러시아어 + 일본어를 커버)
    ///
    /// 굽기 전에 원본 폰트 cmap을 읽어 없는 글리프를 미리 잡아준다.
    ///
    /// 메뉴: PixelChroma > Localization > Character Set Builder
    /// </summary>
    public class LocalizationCharSetWindow : EditorWindow
    {
        private const string OutputFolder = "Assets/Language/CharSets";
        private const int AsciiFirst = 0x20;
        private const int AsciiLast = 0x7E;
        private const int MaxRangeExpansion = 200000;

        // FontTable 구성과 맞춘 프리셋
        private static readonly (string Label, string[] Locales)[] Presets =
        {
            ("M PLUS 그룹", new[] { "en", "es", "pt-BR", "de", "fr", "it", "pl", "tr", "ru", "ja" }),
            ("한국어", new[] { "ko" }),
            ("중국어 간체", new[] { "zh-Hans" }),
            ("중국어 번체", new[] { "zh-Hant" }),
        };

        private readonly SortedDictionary<string, HashSet<int>> _byLocale = new();
        private readonly Dictionary<string, bool> _selected = new();

        // OnGUI는 초당 수십 번 돈다. 폰트 파일은 수십 MB라 경로별로 한 번만 읽는다.
        private readonly Dictionary<string, HashSet<int>> _cmapCache = new();

        private bool _includeAscii = true;
        private string _extraRanges = string.Empty;
        private Font _validationFont;
        private string _outputName = "MPLUSRounded1c";
        private Vector2 _scroll;

        // ── 굽기(Bake) 세팅 ─────────────────────────────────────────────────
        private static readonly int[] AtlasSizes = { 256, 512, 1024, 2048, 4096, 8192 };
        private const string BakePathPrefKeyPrefix = "PixelChroma.CharSetBaker.path.";

        private bool _showBakeSettings;
        private bool _autoSize = true;
        private int _pointSize = 90;
        private int _padding = 5;
        private int _atlasWidth = 1024;
        private int _atlasHeight = 1024;
        private GlyphRenderMode _renderMode = GlyphRenderMode.SDFAA;
        private AtlasPopulationMode _populationMode = AtlasPopulationMode.Static;
        private bool _multiAtlas = true;
        private string _bakePath = string.Empty;

        [MenuItem("PixelChroma/Localization/Character Set Builder")]
        private static void Open()
        {
            var window = GetWindow<LocalizationCharSetWindow>("Character Set Builder");
            window.minSize = new Vector2(460f, 520f);
            window.Scan();
        }

        private void OnEnable()
        {
            if (_byLocale.Count == 0) Scan();
        }

        // ── String Table 스캔 ───────────────────────────────────────────────

        private void Scan()
        {
            _byLocale.Clear();
            _cmapCache.Clear(); // 폰트 파일을 교체했을 수 있으므로 같이 비운다

            var collections = LocalizationEditorSettings.GetStringTableCollections();
            if (collections == null) return;

            foreach (var collection in collections)
            {
                foreach (var table in collection.StringTables)
                {
                    if (table == null) continue;

                    string code = table.LocaleIdentifier.Code;
                    if (string.IsNullOrEmpty(code)) continue;

                    if (!_byLocale.TryGetValue(code, out var set))
                    {
                        set = new HashSet<int>();
                        _byLocale[code] = set;
                    }

                    foreach (StringTableEntry entry in table.Values)
                    {
                        if (string.IsNullOrEmpty(entry?.Value)) continue;

                        foreach (int cp in CodePoints(entry.Value))
                        {
                            if (cp < AsciiFirst) continue; // 개행 등 제어문자 제외
                            set.Add(cp);
                        }
                    }
                }
            }

            foreach (string code in _byLocale.Keys)
                if (!_selected.ContainsKey(code)) _selected[code] = false;
        }

        // ── GUI ────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (_byLocale.Count == 0)
            {
                EditorGUILayout.HelpBox("String Table Collection을 찾지 못했습니다.", MessageType.Warning);
                if (GUILayout.Button("다시 스캔")) Scan();
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("프리셋", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var (label, locales) in Presets)
                {
                    if (!GUILayout.Button(label, EditorStyles.miniButton)) continue;
                    foreach (string code in _byLocale.Keys.ToArray())
                        _selected[code] = locales.Contains(code);
                    _outputName = label switch
                    {
                        "M PLUS 그룹" => "MPLUSRounded1c",
                        "한국어" => "Korean",
                        "중국어 간체" => "ChineseSC",
                        _ => "ChineseTC",
                    };
                    _bakePath = EditorPrefs.GetString(BakePathPrefKeyPrefix + _outputName, string.Empty);
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("포함할 언어", EditorStyles.boldLabel);
            foreach (string code in _byLocale.Keys.ToArray())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _selected[code] = EditorGUILayout.ToggleLeft(code, _selected[code], GUILayout.Width(140f));
                    EditorGUILayout.LabelField($"{_byLocale[code].Count}자", EditorStyles.miniLabel);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("전체 선택", EditorStyles.miniButton))
                    foreach (string code in _byLocale.Keys.ToArray()) _selected[code] = true;
                if (GUILayout.Button("전체 해제", EditorStyles.miniButton))
                    foreach (string code in _byLocale.Keys.ToArray()) _selected[code] = false;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("추가 문자", EditorStyles.boldLabel);
            _includeAscii = EditorGUILayout.ToggleLeft(
                $"기본 ASCII 포함 (U+{AsciiFirst:X4}~U+{AsciiLast:X4}, {AsciiLast - AsciiFirst + 1}자)", _includeAscii);
            EditorGUILayout.LabelField(
                "런타임 보간값({0} 숫자·구분자)이 번역문에 안 잡히므로 켜두는 것을 권장합니다.",
                EditorStyles.miniLabel);

            _extraRanges = EditorGUILayout.TextField(
                new GUIContent("추가 범위", "쉼표 구분. 예: 0400-04FF, 2010-2027, 20AC"), _extraRanges);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("검증 · 굽기 대상 폰트", EditorStyles.boldLabel);
            _validationFont = (Font)EditorGUILayout.ObjectField(
                new GUIContent("원본 폰트", "구울 대상 .ttf/.otf. cmap을 읽어 없는 글리프를 미리 잡아내고, 아틀라스도 이 폰트로 굽습니다."),
                _validationFont, typeof(Font), false);

            EditorGUILayout.Space();
            var codePoints = BuildSet();
            EditorGUILayout.LabelField("결과", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"총 {codePoints.Count}자");

            if (_validationFont != null && codePoints.Count > 0)
                DrawValidation(codePoints);

            EditorGUILayout.Space();
            _outputName = EditorGUILayout.TextField("파일 이름", _outputName);
            EditorGUILayout.LabelField($"{OutputFolder}/{_outputName}.txt", EditorStyles.miniLabel);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(codePoints.Count == 0 || string.IsNullOrWhiteSpace(_outputName)))
            {
                if (GUILayout.Button("Export (txt만)", GUILayout.Height(26f)))
                    Export(codePoints);
            }

            DrawBakeSection(codePoints);

            EditorGUILayout.Space();
            if (GUILayout.Button("String Table 다시 스캔", EditorStyles.miniButton)) Scan();

            EditorGUILayout.EndScrollView();
        }

        // ── 굽기(Bake) UI ──────────────────────────────────────────────────

        private void DrawBakeSection(List<int> codePoints)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("폰트 에셋 굽기 (Font Asset)", EditorStyles.boldLabel);

            _showBakeSettings = EditorGUILayout.Foldout(_showBakeSettings, "굽기 세팅", true);
            if (_showBakeSettings)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _autoSize = EditorGUILayout.ToggleLeft(
                        new GUIContent("Auto Sizing", "아틀라스 한 장에 최대 크기로 들어가는 point size를 자동으로 찾습니다."),
                        _autoSize);

                    using (new EditorGUI.DisabledScope(_autoSize))
                        _pointSize = Mathf.Max(8, EditorGUILayout.IntField(
                            new GUIContent("Sampling Point Size", "Auto Sizing이 꺼졌을 때 사용할 고정 크기."), _pointSize));

                    _padding = Mathf.Max(0, EditorGUILayout.IntField("Padding", _padding));
                    _atlasWidth = SizePopup("Atlas Width", _atlasWidth);
                    _atlasHeight = SizePopup("Atlas Height", _atlasHeight);
                    _renderMode = (GlyphRenderMode)EditorGUILayout.EnumPopup(
                        new GUIContent("Character Render Mode", "기본 SDFAA. SDF 계열은 Distance Field 셰이더로 저장됩니다."), _renderMode);
                    _populationMode = (AtlasPopulationMode)EditorGUILayout.EnumPopup(
                        new GUIContent("Atlas Population Mode", "Static: 지금 구운 글자로 고정. Dynamic: 런타임에 추가 가능."), _populationMode);
                    _multiAtlas = EditorGUILayout.ToggleLeft(
                        new GUIContent("Multi Atlas", "한 장에 다 못 들어가면 아틀라스를 여러 장으로 나눕니다."), _multiAtlas);
                }
            }

            if (!string.IsNullOrEmpty(_bakePath))
                EditorGUILayout.LabelField("최근 저장 위치", _bakePath, EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(_validationFont == null || codePoints.Count == 0))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("아틀라스 굽기…", GUILayout.Height(30f)))
                        PromptAndBake(codePoints, alsoExport: false);
                    if (GUILayout.Button("Export + 굽기…", GUILayout.Height(30f)))
                        PromptAndBake(codePoints, alsoExport: true);
                }
            }

            if (_validationFont == null)
                EditorGUILayout.HelpBox("굽기를 하려면 위에서 '원본 폰트'를 지정하세요.", MessageType.None);
            else
                EditorGUILayout.LabelField(
                    "버튼을 누르면 저장 위치를 고릅니다. 기존 파일을 고르면 참조를 유지한 채 덮어씁니다.",
                    EditorStyles.miniLabel);
        }

        private static int SizePopup(string label, int current)
        {
            int index = System.Array.IndexOf(AtlasSizes, current);
            if (index < 0) index = System.Array.IndexOf(AtlasSizes, 1024);
            var labels = AtlasSizes.Select(s => new GUIContent(s.ToString())).ToArray();
            index = EditorGUILayout.Popup(new GUIContent(label), index, labels);
            return AtlasSizes[index];
        }

        /// <summary>저장 위치를 대화상자로 고른 뒤 굽는다. 취소하면 아무 것도 하지 않는다.</summary>
        private void PromptAndBake(List<int> codePoints, bool alsoExport)
        {
            if (_validationFont == null || codePoints.Count == 0) return;

            string startDir = "Assets";
            string defName = _outputName + "_Static";
            if (!string.IsNullOrEmpty(_bakePath))
            {
                startDir = Path.GetDirectoryName(_bakePath)?.Replace('\\', '/') ?? "Assets";
                defName = Path.GetFileNameWithoutExtension(_bakePath);
            }

            string chosen = EditorUtility.SaveFilePanelInProject(
                "폰트 에셋 저장 위치", defName, "asset",
                "구울 TMP 폰트 에셋 경로를 지정하세요. 기존 파일을 고르면 참조를 유지한 채 덮어씁니다.", startDir);
            if (string.IsNullOrEmpty(chosen)) return; // 취소

            _bakePath = chosen;
            EditorPrefs.SetString(BakePathPrefKeyPrefix + _outputName, _bakePath);

            if (alsoExport) Export(codePoints);
            BakeAtlas(codePoints);
        }

        private void BakeAtlas(List<int> codePoints)
        {
            if (_validationFont == null || string.IsNullOrWhiteSpace(_bakePath) || codePoints.Count == 0)
                return;

            var settings = new FontAtlasBaker.Settings
            {
                AutoSize = _autoSize,
                PointSize = _pointSize,
                Padding = _padding,
                AtlasWidth = _atlasWidth,
                AtlasHeight = _atlasHeight,
                RenderMode = _renderMode,
                PopulationMode = _populationMode,
                MultiAtlas = _multiAtlas,
            };

            FontAtlasBaker.Result r = FontAtlasBaker.Bake(_validationFont, codePoints, settings, _bakePath);
            if (r.Ok)
            {
                EditorPrefs.SetString(BakePathPrefKeyPrefix + _outputName, _bakePath);
                Debug.Log($"[CharSet] 아틀라스 {r.Message}\n  → {_bakePath}", r.Asset);
                EditorGUIUtility.PingObject(r.Asset);
                Selection.activeObject = r.Asset;
            }
            else
            {
                Debug.LogError($"[CharSet] 아틀라스 굽기 실패: {r.Message}");
                EditorUtility.DisplayDialog("굽기 실패", r.Message, "확인");
            }
        }

        private void DrawValidation(List<int> codePoints)
        {
            string path = AssetDatabase.GetAssetPath(_validationFont);
            if (!_cmapCache.TryGetValue(path, out var available))
            {
                available = FontCmapReader.Read(path);
                _cmapCache[path] = available;
            }

            if (available.Count == 0)
            {
                EditorGUILayout.HelpBox($"cmap을 읽지 못했습니다: {path}", MessageType.Warning);
                return;
            }

            var missing = codePoints.Where(cp => !available.Contains(cp)).ToList();
            if (missing.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"{_validationFont.name}이(가) {codePoints.Count}자를 모두 가지고 있습니다.", MessageType.Info);
                return;
            }

            var preview = new StringBuilder();
            foreach (int cp in missing.Take(40))
                preview.Append($"U+{cp:X4} {char.ConvertFromUtf32(cp)}  ");
            if (missing.Count > 40) preview.Append($"... 외 {missing.Count - 40}자");

            EditorGUILayout.HelpBox(
                $"{_validationFont.name}에 없는 글자 {missing.Count}자 — 구워도 아틀라스에 안 들어갑니다.\n{preview}",
                MessageType.Error);
        }

        // ── 집합 구성 ──────────────────────────────────────────────────────

        private List<int> BuildSet()
        {
            var set = new HashSet<int>();

            if (_includeAscii)
                for (int cp = AsciiFirst; cp <= AsciiLast; cp++) set.Add(cp);

            foreach (var kv in _byLocale)
                if (_selected.TryGetValue(kv.Key, out bool on) && on)
                    set.UnionWith(kv.Value);

            foreach (int cp in ParseRanges(_extraRanges)) set.Add(cp);

            var list = set.ToList();
            list.Sort();
            return list;
        }

        private static IEnumerable<int> ParseRanges(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;

            int budget = MaxRangeExpansion;

            foreach (string token in text.Split(','))
            {
                string trimmed = token.Trim();
                if (trimmed.Length == 0) continue;

                string[] parts = trimmed.Split('-');
                if (!TryParseHex(parts[0], out int from)) continue;

                int to = from;
                if (parts.Length > 1 && !TryParseHex(parts[1], out to)) continue;
                if (to < from) continue;

                for (int cp = from; cp <= to && budget > 0; cp++, budget--)
                    yield return cp;
            }
        }

        private static bool TryParseHex(string text, out int value) =>
            int.TryParse(text.Trim().Replace("U+", string.Empty).Replace("u+", string.Empty),
                NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

        private static IEnumerable<int> CodePoints(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    yield return char.ConvertToUtf32(c, text[i + 1]);
                    i++;
                }
                else if (!char.IsSurrogate(c))
                {
                    yield return c;
                }
            }
        }

        // ── 출력 ───────────────────────────────────────────────────────────

        private void Export(List<int> codePoints)
        {
            if (!Directory.Exists(OutputFolder)) Directory.CreateDirectory(OutputFolder);

            var builder = new StringBuilder(codePoints.Count);
            foreach (int cp in codePoints) builder.Append(char.ConvertFromUtf32(cp));

            string path = $"{OutputFolder}/{_outputName}.txt";
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();

            string locales = string.Join(", ", _selected.Where(kv => kv.Value).Select(kv => kv.Key));
            Debug.Log($"[CharSet] {codePoints.Count}자 → {path}\n  포함 로케일: {locales}");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
        }
    }

    /// <summary>
    /// .ttf/.otf/.ttc의 cmap 테이블을 직접 읽어 지원 코드포인트를 구한다.
    /// TMP_FontAsset.HasCharacter는 이미 구워진 아틀라스만 보므로 굽기 전 검증에는 못 쓴다.
    /// </summary>
    internal static class FontCmapReader
    {
        public static HashSet<int> Read(string assetPath)
        {
            var result = new HashSet<int>();
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) return result;

            byte[] data;
            try { data = File.ReadAllBytes(assetPath); }
            catch (IOException) { return result; }

            if (data.Length < 12) return result;

            // TrueType Collection이면 첫 번째 폰트만 본다.
            int origin = Tag(data, 0) == "ttcf" ? (int)U32(data, 12) : 0;
            if (origin + 12 > data.Length) return result;

            int tableCount = U16(data, origin + 4);
            int cmap = 0;
            for (int i = 0; i < tableCount; i++)
            {
                int record = origin + 12 + i * 16;
                if (record + 16 > data.Length) return result;
                if (Tag(data, record) != "cmap") continue;

                cmap = (int)U32(data, record + 8);
                break;
            }

            if (cmap == 0 || cmap + 4 > data.Length) return result;

            int subtableCount = U16(data, cmap + 2);
            for (int i = 0; i < subtableCount; i++)
            {
                int record = cmap + 4 + i * 8;
                if (record + 8 > data.Length) break;

                int subtable = cmap + (int)U32(data, record + 4);
                if (subtable + 4 > data.Length) continue;

                switch (U16(data, subtable))
                {
                    case 4: ReadFormat4(data, subtable, result); break;
                    case 12: ReadFormat12(data, subtable, result); break;
                }
            }

            return result;
        }

        private static void ReadFormat4(byte[] data, int subtable, HashSet<int> result)
        {
            int segCountX2 = U16(data, subtable + 6);
            int endOffset = subtable + 14;
            int startOffset = endOffset + segCountX2 + 2;
            if (startOffset + segCountX2 > data.Length) return;

            for (int s = 0; s < segCountX2 / 2; s++)
            {
                int end = U16(data, endOffset + s * 2);
                int start = U16(data, startOffset + s * 2);
                if (start == 0xFFFF) continue;

                for (int cp = start; cp <= end; cp++) result.Add(cp);
            }
        }

        private static void ReadFormat12(byte[] data, int subtable, HashSet<int> result)
        {
            if (subtable + 16 > data.Length) return;

            uint groupCount = U32(data, subtable + 12);
            for (uint g = 0; g < groupCount; g++)
            {
                int offset = subtable + 16 + (int)g * 12;
                if (offset + 12 > data.Length) return;

                uint start = U32(data, offset);
                uint end = U32(data, offset + 4);
                if (end < start || end - start > 100000) continue;

                for (uint cp = start; cp <= end; cp++) result.Add((int)cp);
            }
        }

        private static string Tag(byte[] data, int offset) =>
            Encoding.ASCII.GetString(data, offset, 4);

        private static int U16(byte[] data, int offset) =>
            (data[offset] << 8) | data[offset + 1];

        private static uint U32(byte[] data, int offset) =>
            ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) |
            ((uint)data[offset + 2] << 8) | data[offset + 3];
    }
}
#endif
