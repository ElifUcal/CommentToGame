using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CommentToGame.Hubs
{
    [Authorize] // Bildirim almak için logine zorla
    public class NotificationHub : Hub
    {
        // İstersen connection açıldığında bir şey yapabilirsin
        public override Task OnConnectedAsync()
        {
            // Kullanıcıya özel grup atama vs. burada da yapabilirsin
            return base.OnConnectedAsync();
        }
    }
}
