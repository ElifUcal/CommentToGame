namespace CommentToGame.Dtos
{
    public class NotificationDto
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }

    public class CreateNotificationDto
    {
        public string UserId { get; set; }   // Admin panelden manuel gönderirken lazım olabilir
        public string Type { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }
}
