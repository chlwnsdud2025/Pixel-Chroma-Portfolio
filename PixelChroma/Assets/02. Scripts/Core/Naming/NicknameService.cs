using UnityEngine;

namespace PixelChroma
{
    // 닉네임 저장/기본값을 한곳에 모은다. (UINickNameSetting, UIRoomSetting 공용)
    public static class NicknameService
    {
        public const string PrefsKey = "CustomPlayerNickname";
        public const int MaxLength = 10;

        // 접두사(6자) + 임의 번호(4자)가 MaxLength에 딱 맞는다. 셋 중 하나를 바꾸려면 함께 맞출 것.
        private const string DefaultNamePrefix = "Chroma";

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

        // 저장된 닉네임이 없으면 스팀 이름(스팀 미사용 시 임의 이름)으로 대체한다.
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
