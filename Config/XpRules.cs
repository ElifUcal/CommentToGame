using CommentToGame.enums;

namespace CommentToGame.Config
{
    public static class XpRules
    {
        public const int LevelXpStep = 100; // 100 XP = 1 level

        private static readonly Dictionary<XpSourceType, int> _xpMap = new()
        {
            // Profil
            { XpSourceType.ProfileAvatarUploaded, 25 },
            { XpSourceType.ProfileBioCompleted, 20 },
            { XpSourceType.ProfileLocationCompleted, 10 },
            { XpSourceType.ProfileFavoriteGenresCompleted, 15 },
            { XpSourceType.ProfilePlatformsCompleted, 10 },

            // Oyun / Kütüphane
            { XpSourceType.GameAddedToLibrary, 10 },
            { XpSourceType.GameRated, 15 },
            { XpSourceType.GameProgressOver50, 15 },
            { XpSourceType.GameCompleted, 20 },

            // Yorum
            { XpSourceType.CommentCreated, 25 }, // min karakter kontrolü vs. service içinde olacak
        };

        public static int GetXpForSource(XpSourceType sourceType)
        {
            return _xpMap.TryGetValue(sourceType, out var xp) ? xp : 0;
        }
    }
}
