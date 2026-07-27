using UnityEngine;
using TMPro;

namespace PixelChroma
{
    public enum NameValidation
    {
        Valid,
        TooLong,
        Banned,
        Unsupported,
    }

    // 닉네임/방 이름 저장·기본값·검증을 한곳에 모은다. (UINickNameSetting, UIRoomSetting 공용)
    public static class NicknameService
    {
        public const string PrefsKey = "CustomPlayerNickname";
        public const int MaxLength = 10;

        // 접두사(6자) + 임의 번호(4자)가 MaxLength에 딱 맞는다. 셋 중 하나를 바꾸려면 함께 맞출 것.
        private const string DefaultNamePrefix = "Chroma";

        // 유저가 입력한 이름의 공통 검증. 경고 문구는 로컬라이즈 대상이라 결과만 돌려주고 UI가 문구를 고른다.
        public static NameValidation Validate(string name, BannedWordsSO bannedWords, int maxLength, TMP_FontAsset font = null)
        {
            if (string.IsNullOrEmpty(name))
                return NameValidation.Valid;

            if (name.Length > maxLength)
                return NameValidation.TooLong;

            if (bannedWords != null && bannedWords.Contains(name))
                return NameValidation.Banned;

            if (font != null && HasUnsupportedCharacter(name, font))
                return NameValidation.Unsupported;

            return NameValidation.Valid;
        }

        // 폰트(폴백 포함)로 렌더 불가능한 문자가 하나라도 있으면 true.
        private static bool HasUnsupportedCharacter(string name, TMP_FontAsset font)
        {
            foreach (char c in name)
            {
                if (char.IsWhiteSpace(c))
                    continue;

                if (!font.HasCharacter(c, searchFallbacks: true, tryAddCharacter: true))
                    return true;
            }

            return false;
        }

        // 유저가 닉네임을 비워뒀을 때 쓸 기본 이름. 항상 MaxLength 이내로 돌려준다.
        public static string GetDefaultSteamName()
        {
            try
            {
                if (Steamworks.SteamAPI.IsSteamRunning())
                    return Truncate(Steamworks.SteamFriends.GetPersonaName(), MaxLength);
            }
            catch (System.Exception) { }

            return DefaultNamePrefix + UnityEngine.Random.Range(1000, 9999);
        }

        public static string LoadOrDefault()
        {
            string saved = PlayerPrefs.GetString(PrefsKey, "");
            return string.IsNullOrEmpty(saved) ? GetDefaultSteamName() : saved;
        }

        public static void Save(string nickname)
        {
            PlayerPrefs.SetString(PrefsKey, nickname);
            PlayerPrefs.Save();
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;

            // 스팀 이름에는 이모지가 흔하다. 서로게이트 쌍 가운데를 자르면 깨진 글자가 남으므로 한 칸 물러난다.
            if (maxLength > 0 && char.IsHighSurrogate(value[maxLength - 1]))
                maxLength--;

            return value.Substring(0, maxLength);
        }
    }
}
