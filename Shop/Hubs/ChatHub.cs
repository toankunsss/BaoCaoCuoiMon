using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.Identity;
using System.Threading.Tasks;
using Shop.EF;
using System.Linq;
using System;

namespace Shop.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context = new ApplicationDbContext();

        /// <summary>
        /// Gửi tin nhắn từ người dùng đến người nhận và tất cả admin.
        /// </summary>
        public async Task SendMessage(string receiverId, string senderId, string message, string senderName)
        {
            // Kiểm tra xem người gửi có đăng nhập không
            if (string.IsNullOrEmpty(senderId))
            {
                await Clients.Caller.showError("Bạn phải đăng nhập để gửi tin nhắn.");
                return;
            }

            // Kiểm tra xem người nhận có tồn tại không
            var receiver = _context.AspNetUsers.FirstOrDefault(u => u.Id == receiverId);
            if (receiver == null)
            {
                await Clients.Caller.showError("Không tìm thấy người nhận.");
                return;
            }

            try
            {
                // Tạo tin nhắn mới
                var chatMessage = new Message
                {
                    SenderId = senderId,
                    ReceiverId = receiverId,
                    Content = message,
                    Timestamp = DateTime.UtcNow,
                    IsRead = false
                };

                // Lưu tin nhắn vào cơ sở dữ liệu
                _context.Messages.Add(chatMessage);
                await _context.SaveChangesAsync();

                // Gửi tin nhắn đến người gửi (khách hàng)
                await Clients.User(senderId).receiveMessage(senderName, message, chatMessage.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), false);

                // Lấy danh sách ID của tất cả admin
                var adminIds = _context.AspNetUserRoles
                    .Where(r => r.RoleId == "1")
                    .Select(r => r.UserId)
                    .Distinct()
                    .ToList();

                // Gửi tin nhắn đến tất cả admin
                foreach (var adminId in adminIds)
                {
                    await Clients.User(adminId).receiveMessage(senderName, message, chatMessage.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), true);
                }
            }
            catch (Exception ex)
            {
                // Thông báo lỗi nếu lưu tin nhắn thất bại
                await Clients.Caller.showError($"Lỗi khi gửi tin nhắn: {ex.Message}");
            }
        }

        /// <summary>
        /// Xử lý khi người dùng kết nối đến hub.
        /// </summary>
        public override Task OnConnected()
        {
            var userId = Context.User.Identity.GetUserId();
            if (!string.IsNullOrEmpty(userId))
            {
                Clients.User(userId).notifyConnected("Bạn đã kết nối với chat.");
            }
            return base.OnConnected();
        }

        /// <summary>
        /// Giải phóng tài nguyên khi hub bị hủy.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context.Dispose();
            }
            base.Dispose(disposing);
        }
    }

}