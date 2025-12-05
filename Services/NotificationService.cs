using CommentToGame.Data; // Senin MongoDbService’inin namespace'ini buraya göre düzelt
using CommentToGame.Models; // gerekirse değiştir
using CommentToGame.Dtos;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using CommentToGame.Hubs;
using MongoDB.Bson;

namespace CommentToGame.Services
{
    public interface INotificationService
    {
        Task<NotificationDto> CreateAsync(
            string userId,
            string type,
            string title,
            string message,
            object data = null,
            CancellationToken ct = default);

        Task<(IReadOnlyList<NotificationDto> items, long totalCount)> GetForUserAsync(
            string userId,
            bool onlyUnread,
            int page,
            int pageSize,
            CancellationToken ct = default);

        Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default);

        Task MarkAsReadAsync(string userId, string notificationId, CancellationToken ct = default);

        Task MarkAllAsReadAsync(string userId, CancellationToken ct = default);

        // İleride: SendToAdminsAsync, SendToAllUsersAsync vs. ekleyebilirsin
    }

    public class NotificationService : INotificationService
    {
        private readonly IMongoCollection<Notification> _notifications;
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationService(MongoDbService db, IHubContext<NotificationHub> hubContext)
        {
            _notifications = db.GetCollection<Notification>("Notifications");
            _hubContext = hubContext;
        }

        public async Task<NotificationDto> CreateAsync(
            string userId,
            string type,
            string title,
            string message,
            object data = null,
            CancellationToken ct = default)
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                Data = data != null ? BsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(data)) : null,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await _notifications.InsertOneAsync(notification, cancellationToken: ct);

            var dto = MapToDto(notification);

            // SignalR ile kullanıcıya anlık gönder
            await _hubContext.Clients.User(userId).SendAsync("NewNotification", dto, ct);

            return dto;
        }

        public async Task<(IReadOnlyList<NotificationDto> items, long totalCount)> GetForUserAsync(
            string userId,
            bool onlyUnread,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var filterBuilder = Builders<Notification>.Filter;
            var filter = filterBuilder.Eq(n => n.UserId, userId);

            if (onlyUnread)
            {
                filter &= filterBuilder.Eq(n => n.IsRead, false);
            }

            var totalCount = await _notifications.CountDocumentsAsync(filter, cancellationToken: ct);

            var notifications = await _notifications
                .Find(filter)
                .SortByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(ct);

            var dtos = notifications.Select(MapToDto).ToList();

            return (dtos, totalCount);
        }

        public async Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default)
        {
            var filter = Builders<Notification>.Filter.And(
                Builders<Notification>.Filter.Eq(n => n.UserId, userId),
                Builders<Notification>.Filter.Eq(n => n.IsRead, false)
            );

            var count = await _notifications.CountDocumentsAsync(filter, cancellationToken: ct);
            return (int)count;
        }

        public async Task MarkAsReadAsync(string userId, string notificationId, CancellationToken ct = default)
        {
            var filter = Builders<Notification>.Filter.And(
                Builders<Notification>.Filter.Eq(n => n.Id, notificationId),
                Builders<Notification>.Filter.Eq(n => n.UserId, userId)
            );

            var update = Builders<Notification>.Update
                .Set(n => n.IsRead, true)
                .Set(n => n.ReadAt, DateTime.UtcNow);

            await _notifications.UpdateOneAsync(filter, update, cancellationToken: ct);
        }

        public async Task MarkAllAsReadAsync(string userId, CancellationToken ct = default)
        {
            var filter = Builders<Notification>.Filter.And(
                Builders<Notification>.Filter.Eq(n => n.UserId, userId),
                Builders<Notification>.Filter.Eq(n => n.IsRead, false)
            );

            var update = Builders<Notification>.Update
                .Set(n => n.IsRead, true)
                .Set(n => n.ReadAt, DateTime.UtcNow);

            await _notifications.UpdateManyAsync(filter, update, cancellationToken: ct);
        }

        private NotificationDto MapToDto(Notification n)
        {
            object dataObj = null;
            if (n.Data != null && n.Data.ElementCount > 0)
            {
                // BsonDocument -> dynamic object
                dataObj = System.Text.Json.JsonSerializer.Deserialize<object>(n.Data.ToJson());
            }

            return new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                Data = dataObj,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt
            };
        }
    }
}
