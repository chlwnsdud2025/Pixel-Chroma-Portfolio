#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace PixelChroma.EditorTools
{
    /// <summary>
    /// 모든 String Table을 순회해 언어별로 실제 사용된 고유 문자를 추출하고,
    /// TMP Font Asset Creator의 "Characters from File"에 넣을 텍스트 파일로 저장한다.
    ///
    /// 사용 흐름:
    ///   1) 번역 수정
    ///   2) 메뉴 실행 → CharSets/{code}.txt 갱신 (git diff로 변경 확인)
    ///   3) 바뀐 언어만 Font Asset Creator에서 "Characters from File"로 재굽기
    ///
    /// 메뉴: PixelChroma > Localization > Export Character Sets
    /// </summary>
    public static class LocalizationCharSetExporter
    {
        private const string OutputFolder = "Assets/Language/CharSets";

        // 런타임 보간값({0} 숫자 등)·혼용 라틴 대응용 베이스라인 (ASCII 0x20~0x7E)
        private static readonly char[] Baseline =
            Enumerable.Range(0x20, 0x7F - 0x20).Select(c => (char)c).ToArray();

        [MenuItem("PixelChroma/Localization/Export Character Sets")]
        public static void Export()
        {
            var collections = LocalizationEditorSettings.GetStringTableCollections();
            if (collections == null || collections.Count == 0)
            {
                Debug.LogWarning("[CharSet] String Table Collection이 없습니다.");
                return;
            }

            // code -> 고유 문자 집합
            var byLocale = new Dictionary<string, HashSet<char>>();

            foreach (var collection in collections)
            {
                foreach (var table in collection.StringTables)
                {
                    if (table == null) continue;

                    string code = table.LocaleIdentifier.Code;
                    if (string.IsNullOrEmpty(code)) continue;

                    if (!byLocale.TryGetValue(code, out var set))
                    {
                        set = new HashSet<char>(Baseline);
                        byLocale[code] = set;
                    }

                    foreach (StringTableEntry entry in table.Values)
                    {
                        if (entry == null) continue;
                        string value = entry.Value;
                        if (string.IsNullOrEmpty(value)) continue;

                        foreach (char ch in value)
                        {
                            if (ch < 0x20) continue; // 개행 등 제어문자 제외
                            set.Add(ch);
                        }
                    }
                }
            }

            if (byLocale.Count == 0)
            {
                Debug.LogWarning("[CharSet] 추출된 문자가 없습니다. 번역이 비어있는지 확인하세요.");
                return;
            }

            if (!Directory.Exists(OutputFolder))
                Directory.CreateDirectory(OutputFolder);

            var utf8NoBom = new UTF8Encoding(false);
            var report = new StringBuilder("[CharSet] 추출 완료\n");

            foreach (var kv in byLocale.OrderBy(k => k.Key))
            {
                var sorted = kv.Value.OrderBy(c => c).ToArray();
                string content = new string(sorted);

                string path = $"{OutputFolder}/{kv.Key}.txt";
                File.WriteAllText(path, content, utf8NoBom);

                report.AppendLine($"  {kv.Key,-8} : {sorted.Length}자  →  {path}");
            }

            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
        }
    }
}
#endif
