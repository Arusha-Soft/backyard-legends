namespace BackyardLegends.Runtime.Firebase
{
    public sealed class AuthUserSnapshot
    {
        public AuthUserSnapshot(
            string uid,
            string displayName,
            bool isAnonymous,
            string platform,
            int rating)
        {
            Uid = uid ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            IsAnonymous = isAnonymous;
            Platform = platform ?? string.Empty;
            Rating = rating;
        }

        public string Uid { get; }
        public string DisplayName { get; }
        public bool IsAnonymous { get; }
        public string Platform { get; }
        public int Rating { get; }

        public bool IsSignedIn => !string.IsNullOrEmpty(Uid);

        public string ShortUid
        {
            get
            {
                if (string.IsNullOrEmpty(Uid))
                {
                    return string.Empty;
                }

                return Uid.Length <= 8 ? Uid : Uid.Substring(0, 8);
            }
        }

        public string StatusLabel
        {
            get
            {
                if (!IsSignedIn)
                {
                    return "Not signed in";
                }

                if (IsAnonymous)
                {
                    return $"Guest · {ShortUid}";
                }

                var name = string.IsNullOrWhiteSpace(DisplayName) ? "Player" : DisplayName;
                return $"{name} · {ShortUid}";
            }
        }

        public static AuthUserSnapshot None { get; } = new AuthUserSnapshot(string.Empty, string.Empty, true, string.Empty, 0);
    }
}
