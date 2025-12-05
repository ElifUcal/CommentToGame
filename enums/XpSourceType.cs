namespace CommentToGame.enums;
 
 public enum XpSourceType
    {
        // Profil
        ProfileAvatarUploaded = 1,
        ProfileBioCompleted = 2,
        ProfileLocationCompleted = 3,
        ProfileFavoriteGenresCompleted = 4,
        ProfilePlatformsCompleted = 5,

        // Oyun / Kütüphane
        GameAddedToLibrary = 100,
        GameRated = 101,
        GameProgressOver50 = 102,
        GameCompleted = 103,

        // Yorum
        CommentCreated = 200
    }