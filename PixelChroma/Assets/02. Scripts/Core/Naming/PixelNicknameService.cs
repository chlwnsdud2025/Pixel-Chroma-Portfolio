using System;
using UnityEngine;

namespace PixelChroma
{
    public static class PixelNicknameService
    {
        private const string PrefsKey = "PixelNickname";

        public static void Save(byte[] pngData)
        {
            if (pngData == null || pngData.Length == 0)
            {
                PlayerPrefs.DeleteKey(PrefsKey);
                PlayerPrefs.Save();
                return;
            }

            PlayerPrefs.SetString(PrefsKey, Convert.ToBase64String(pngData));
            PlayerPrefs.Save();
        }

        public static byte[] Load()
        {
            string base64 = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(base64)) return null;

            try { return Convert.FromBase64String(base64); }
            catch { return null; }
        }
    }
}
