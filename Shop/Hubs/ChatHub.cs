using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.Identity;
using System.Threading.Tasks;
using Shop.EF;
using System.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic; // Thêm namespace này

namespace Shop.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context = new ApplicationDbContext();
        private static readonly ConcurrentDictionary<string, string> OnlineUsers = new ConcurrentDictionary<string, string>();

        // Phương thức tĩnh để lấy danh sách người dùng trực tuyến
        public static List<string> GetOnlineUsers()
        {
            return OnlineUsers.Keys.ToList();
        }

        public async Task SendMessage(string receiverId, string senderId, string message, string senderName)
        {
            System.Diagnostics.Debug.WriteLine($"SendMessage: Sender={senderId}, Receiver={receiverId}, Message={message}");
            System.Diagnostics.Debug.WriteLine($"OnlineUsers: {string.Join(", ", OnlineUsers.Keys)}");

            if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(receiverId))
            {
                System.Diagnostics.Debug.WriteLine("Lỗi: SenderId hoặc ReceiverId trống.");
                await Clients.Caller.showError("Thông tin người dùng không hợp lệ.");
                return;
            }

            var receiver = _context.AspNetUsers.FirstOrDefault(u => u.Id == receiverId);
            if (receiver == null)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi: Không tìm thấy người nhận với ID={receiverId}.");
                await Clients.Caller.showError("Không tìm thấy người nhận.");
                return;
            }

            try
            {
                var chatMessage = new Message
                {
                    SenderId = senderId,
                    ReceiverId = receiverId,
                    Content = message,
                    Timestamp = DateTime.UtcNow,
                    IsRead = false
                };

                _context.Messages.Add(chatMessage);
                await _context.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine("Lưu tin nhắn thành công.");

                if (OnlineUsers.TryGetValue(senderId, out var senderConnectionId))
                {
                    await Clients.Client(senderConnectionId).receiveMessage(senderName, message, chatMessage.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), false, senderId);
                    System.Diagnostics.Debug.WriteLine($"Gửi tin nhắn đến sender: {senderId}, ConnectionId: {senderConnectionId}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Sender offline: {senderId}");
                }

                if (OnlineUsers.TryGetValue(receiverId, out var receiverConnectionId))
                {
                    await Clients.Client(receiverConnectionId).receiveMessage(senderName, message, chatMessage.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), true, senderId);
                    System.Diagnostics.Debug.WriteLine($"Gửi tin nhắn đến receiver: {receiverId}, ConnectionId: {receiverConnectionId}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Receiver offline: {receiverId}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi trong SendMessage: {ex.Message} - StackTrace: {ex.StackTrace}");
                await Clients.Caller.showError($"Lỗi khi gửi tin nhắn: {ex.Message}");
            }
        }

        public override Task OnConnected()
        {
            var userId = Context.User?.Identity?.GetUserId();
            if (!string.IsNullOrEmpty(userId))
            {
                OnlineUsers.AddOrUpdate(userId, Context.ConnectionId, (key, oldValue) => Context.ConnectionId);
                System.Diagnostics.Debug.WriteLine($"Người dùng kết nối: {userId}, ConnectionId: {Context.ConnectionId}, OnlineUsers Count: {OnlineUsers.Count}");
                Clients.Client(Context.ConnectionId).notifyConnected("Bạn đã kết nối với chat.");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Người dùng kết nối nhưng không có UserId.");
            }
            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            var userId = Context.User?.Identity?.GetUserId();
            if (!string.IsNullOrEmpty(userId))
            {
                OnlineUsers.TryRemove(userId, out _);
                System.Diagnostics.Debug.WriteLine($"Người dùng ngắt kết nối: {userId}, OnlineUsers Count: {OnlineUsers.Count}");
            }
            return base.OnDisconnected(stopCalled);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                System.Diagnostics.Debug.WriteLine("Giải phóng ApplicationDbContext.");
                _context.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}