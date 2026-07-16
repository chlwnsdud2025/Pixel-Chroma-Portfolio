namespace PixelChroma
{
    public enum NameValidation
    {
        Valid,
        TooLong,
        Banned,
    }

    // 닉네임 / 방 이름처럼 유저가 입력하는 이름의 공통 검증 규칙.
    // 경고 문구는 로컬라이즈 대상이라 여기서 만들지 않고, 결과만 돌려주고 UI가 문구를 고른다.
    //
    // 빈 이름은 유효로 본다. 비워두는 건 실수가 아니라 "기본값을 쓰겠다"는 선택이고,
    // 무엇으로 대체할지는 쓰는 쪽마다 달라서 여기서 정할 수 없다.
    // (닉네임 → NicknameService.GetDefaultSteamName(), 방 이름 → "{hostName}의 방")
    public static class NameValidator
    {
        public static NameValidation Validate(string name, BannedWordsSO bannedWords, int maxLength)
        {
            if (string.IsNullOrEmpty(name))
                return NameValidation.Valid;

            if (name.Length > maxLength)
                return NameValidation.TooLong;

            if (bannedWords != null && bannedWords.Contains(name))
                return NameValidation.Banned;

            return NameValidation.Valid;
        }
    }
}
