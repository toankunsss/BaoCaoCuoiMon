using System;
using System.Linq;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Shop.EF;
using Shop.Areas.Administrator.Data.message;
using System.Web;
using System.Collections.Generic;
using Shop.Hubs;

namespace Shop.Areas.Administrator.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class AdminMessengerController : Controller
    {
        private readonly ApplicationDbContext _context;
        public AdminMessengerController()
        {
            _context = new ApplicationDbContext();
            // Kích hoạt logging SQL
            _context.Database.Log = sql => System.Diagnostics.Debug.WriteLine($"SQL Query: {sql}");
            System.Diagnostics.Debug.WriteLine("AdminMessengerController initialized");
        }

        // GET: /Administrator/AdminMessenger/GetOnlineUsers
        [OutputCache(NoStore = true, Duration = 0)]
        public JsonResult GetOnlineUsers()
        {
            try
            {
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
                Response.Cache.SetNoStore();

                // Truy cập danh sách người dùng trực tuyến từ ChatHub
                var onlineUsers = ChatHub.GetOnlineUsers();
                System.Diagnostics.Debug.WriteLine($"GetOnlineUsers trả về {onlineUsers.Count} người dùng trực tuyến: {string.Join(", ", onlineUsers)}");

                return Json(onlineUsers, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi trong GetOnlineUsers: {ex.Message} - StackTrace: {ex.StackTrace}");
                return Json(new { error = "Không thể truy xuất danh sách người dùng trực tuyến." }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: /Administrator/AdminMessenger/AdminChat
        [OutputCache(NoStore = true, Duration = 0)]
        public ActionResult AdminChat()
        {
            try
            {
                // Ngăn cache
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
                Response.Cache.SetNoStore();

                var currentUserId = User.Identity.GetUserId();

                // 1. Tìm người dùng (khách hàng) nhắn tin gần nhất với admin
                var lastUser = _context.Messages
                    .Where(m =>
                        ((m.SenderId == currentUserId && !_context.AspNetUserRoles.Any(ur => ur.UserId == m.ReceiverId)) ||
                         (m.ReceiverId == currentUserId && !_context.AspNetUserRoles.Any(ur => ur.UserId == m.SenderId))))
                    .OrderByDescending(m => m.Timestamp)
                    .Select(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                    .FirstOrDefault();

                if (lastUser == null)
                {
                    ViewBag.Messages = new List<Message>();
                    ViewBag.Users = new List<AspNetUser>();
                    ViewBag.SelectedUserId = null;
                    return View();
                }

                // 2. Lấy danh sách tin nhắn giữa admin và người dùng đó
                var messagesFromDb = _context.Messages
                    .Where(m =>
                        (m.SenderId == lastUser && m.ReceiverId == currentUserId) ||
                        (m.SenderId == currentUserId && m.ReceiverId == lastUser))
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                // 3. Lấy danh sách tất cả khách hàng (dùng để load sidebar người dùng)
                var users = _context.AspNetUsers
                    .Where(u => !_context.AspNetUserRoles.Any(ur => ur.UserId == u.Id))
                    .ToList();

                // Log
                System.Diagnostics.Debug.WriteLine($"AdminChat loaded: {messagesFromDb.Count} messages with user {lastUser}");

                ViewBag.Messages = messagesFromDb;
                ViewBag.Users = users;
                ViewBag.SelectedUserId = lastUser;

                return View();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi trong AdminChat: {ex.Message} - StackTrace: {ex.StackTrace}");
                return View("Error", new HandleErrorInfo(ex, "AdminMessenger", "AdminChat"));
            }
        }



        // GET: /Administrator/AdminMessenger/GetMessages
        [OutputCache(NoStore = true, Duration = 0)]
        public JsonResult GetMessages(string userId)
        {
            try
            {
                // Ngăn cache
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
                Response.Cache.SetNoStore();

                if (string.IsNullOrEmpty(userId))
                {
                    System.Diagnostics.Debug.WriteLine("userId is null or empty in GetMessages.");
                    return Json(new { error = "ID người dùng không hợp lệ." }, JsonRequestBehavior.AllowGet);
                }

                var currentUserId = User.Identity.GetUserId();
                var messagesFromDb = _context.Messages
                    .Where(m => (m.SenderId == userId && m.ReceiverId == currentUserId) ||
                                (m.SenderId == currentUserId && m.ReceiverId == userId))
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                var messages = messagesFromDb.Select(m => new
                {
                    senderId = m.SenderId,
                    senderName = m.Sender?.UserName ?? "Unknown",
                    content = m.Content,
                    timestamp = m.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                }).ToList();

                System.Diagnostics.Debug.WriteLine($"GetMessages trả về {messages.Count} tin nhắn cho userId: {userId}");
                return Json(messages, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi trong GetMessages: {ex.Message} - StackTrace: {ex.StackTrace}");
                return Json(new { error = "Không thể truy xuất tin nhắn." }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: /Administrator/AdminMessenger/GetAllUsersWithMessages
        [OutputCache(NoStore = true, Duration = 0)]
        public JsonResult GetAllUsersWithMessages()
        {
            try
            {
                // Ngăn cache
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
                Response.Cache.SetNoStore();

                // Lấy danh sách người dùng có tin nhắn với admin
                var currentUserId = User.Identity.GetUserId();
                var usersWithMessages = _context.Messages
                    .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
                    .Select(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                    .Distinct()
                    .ToList();

                // Chỉ lấy khách hàng (không có bản ghi trong AspNetUserRoles)
                var users = _context.AspNetUsers
                    .Where(u => usersWithMessages.Contains(u.Id) &&
                                !_context.AspNetUserRoles.Any(ur => ur.UserId == u.Id))
                    .Select(u => new
                    {
                        id = u.Id,
                        userName = u.UserName ?? "Unknown"
                    })
                    .ToList();

                // Log chi tiết danh sách người dùng
                var userLog = string.Join(", ", users.Select(u => $"ID: {u.id}, Username: {u.userName}"));
                System.Diagnostics.Debug.WriteLine($"GetAllUsersWithMessages trả về {users.Count} khách hàng (no records in AspNetUserRoles). Users: [{userLog}]");

                return Json(users, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi trong GetAllUsersWithMessages: {ex.Message} - StackTrace: {ex.StackTrace}");
                return Json(new { error = "Không thể truy xuất danh sách người dùng." }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: /Administrator/AdminMessenger/GetUserList
        [OutputCache(NoStore = true, Duration = 0)]
        public JsonResult GetUserList()
        {
            try
            {
                // Ngăn cache
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
                Response.Cache.SetNoStore();

                // Lấy danh sách người dùng có tin nhắn với admin, sắp xếp theo LastActivityTime giảm dần
                var currentUserId = User.Identity.GetUserId();
                var usersWithMessages = _context.Messages
                    .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
                    .GroupBy(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        LastActivityTime = g.Max(m => m.Timestamp)
                    })
                    .OrderByDescending(g => g.LastActivityTime)
                    .ToList();

                // Lọc người dùng không có bản ghi trong AspNetUserRoles (khách hàng)
                var users = usersWithMessages
                    .Where(u => !_context.AspNetUserRoles.Any(ur => ur.UserId == u.UserId))
                    .Select(u => new
                    {
                        id = u.UserId,
                        userName = _context.AspNetUsers
                            .FirstOrDefault(user => user.Id == u.UserId)?.UserName ?? "Unknown"
                    })
                    .ToList();

                // Log chi tiết danh sách người dùng
                var userLog = string.Join(", ", users.Select(u => $"ID: {u.id}, Username: {u.userName}"));
                System.Diagnostics.Debug.WriteLine($"GetUserList trả về {users.Count} khách hàng (no records in AspNetUserRoles). Users: [{userLog}]");

                return Json(users, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi trong GetUserList: {ex.Message} - StackTrace: {ex.StackTrace}");
                return Json(new { error = "Không thể truy xuất danh sách người dùng." }, JsonRequestBehavior.AllowGet);
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