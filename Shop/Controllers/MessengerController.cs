using System;
using System.Linq;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Shop.EF;
using Shop.Areas.Administrator.Data.message;

namespace Shop.Controllers
{
    public class MessengerController : Controller
    {

        private readonly ApplicationDbContext _context = new ApplicationDbContext();

        public MessengerController()
        {
            _context = new ApplicationDbContext();
            // Kích hoạt logging SQL
            _context.Database.Log = sql => System.Diagnostics.Debug.WriteLine($"SQL Query: {sql}");
            System.Diagnostics.Debug.WriteLine("MessengerController initialized");
        }
        // GET: /Messenger/Chat
        [Authorize]
        public ActionResult Chat()
        {
            var userId = User.Identity.GetUserId();
            var admins = _context.AspNetUserRoles
                .Where(r => r.RoleId == "1")
                .Select(r => r.UserId)
                .Distinct()
                .ToList();

            var adminUsers = _context.AspNetUsers
                .Where(u => admins.Contains(u.Id))
                .ToList();

            var messages = _context.Messages
                .Where(m => m.SenderId == userId || m.ReceiverId == userId)
                .OrderBy(m => m.Timestamp)
                .ToList();

            ViewBag.Admins = adminUsers;
            ViewBag.Messages = messages;
            return View();
        }

        // GET: /Messenger/AdminChat
        [Authorize(Roles = "Administrator")]
        public ActionResult AdminChat()
        {
            var messages = _context.Messages
                .GroupBy(m => m.SenderId)
                .Select(g => g.OrderByDescending(m => m.Timestamp).First())
                .OrderBy(m => m.Timestamp)
                .ToList();

            ViewBag.Messages = messages;
            ViewBag.Users = _context.AspNetUsers.ToList();
            return View();
        }

        // GET: /Messenger/GetMessages
        [Authorize(Roles = "Administrator")]
        public JsonResult GetMessages(string userId)
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();

                // Bước 1: Query từ DB trước (chưa dùng ToString)
                var messagesFromDb = _context.Messages
                    .Where(m => (m.SenderId == userId && m.ReceiverId == currentUserId) ||
                                (m.SenderId == currentUserId && m.ReceiverId == userId))
                    .OrderBy(m => m.Timestamp)
                    .ToList();  // Chuyển dữ liệu về bộ nhớ (LINQ to Objects)

                // Bước 2: Dùng ToString() trên bộ nhớ (thoải mái dùng hàm .NET)
                var messages = messagesFromDb.Select(m => new
                {
                    senderId = m.SenderId,
                    senderName = m.Sender.UserName,
                    content = m.Content,
                    timestamp = m.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                }).ToList();

                return Json(messages, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi trong GetMessages: {ex.Message} - StackTrace: {ex.StackTrace}");
                return Json(new { error = "Không thể truy xuất tin nhắn." }, JsonRequestBehavior.AllowGet);
            }
        }


        // GET: /Messenger/GetMessagesForUser
        [Authorize]
        public JsonResult GetMessagesForUser(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    System.Diagnostics.Debug.WriteLine("userId is null or empty in GetMessagesForUser.");
                    return Json(new { error = "ID người dùng không hợp lệ." }, JsonRequestBehavior.AllowGet);
                }

                System.Diagnostics.Debug.WriteLine("the ajax is running.");

                // Lấy dữ liệu từ database trước (không được Select ở đây)
                var messagesFromDb = _context.Messages
                    .Where(m => m.SenderId == userId || m.ReceiverId == userId)
                    .OrderBy(m => m.Timestamp)
                    .ToList();  // Bây giờ dữ liệu đã nằm trong bộ nhớ RAM

                // Select và format timestamp sau
                var messages = messagesFromDb.Select(m => new
                {
                    senderId = m.SenderId,
                    content = m.Content,
                    timestamp = m.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") // Bây giờ ToString() hợp lệ
                }).ToList();

                System.Diagnostics.Debug.WriteLine($"GetMessagesForUser trả về {messages.Count} tin nhắn cho userId: {userId}");
                return Json(messages, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi trong GetMessagesForUser: {ex.Message} - StackTrace: {ex.StackTrace}");
                return Json(new { error = "Không thể truy xuất tin nhắn." }, JsonRequestBehavior.AllowGet);
            }
        }


        // GET: /Messenger/GetAdminIds
        [Authorize]
        public JsonResult GetAdminIds()
        {
            try
            {
                var adminIds = _context.AspNetUserRoles
                    .Where(r => r.RoleId == "1")
                    .Select(r => new { id = r.UserId })
                    .Distinct()
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"GetAdminIds trả về {adminIds.Count} admin: {string.Join(", ", adminIds.Select(a => a.id))}");
                return Json(adminIds, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi trong GetAdminIds: {ex.Message} - StackTrace: {ex.StackTrace}");
                return Json(new { error = "Không thể truy xuất ID admin." }, JsonRequestBehavior.AllowGet);
            }
        }

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

