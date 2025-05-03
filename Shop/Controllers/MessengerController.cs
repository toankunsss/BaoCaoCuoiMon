using System;
using System.Linq;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Shop.EF;
using Shop.Areas.Administrator.Data.message;
using System.Web;

namespace Shop.Controllers
{
    public class MessengerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MessengerController()
        {
            _context = new ApplicationDbContext();
            _context.Database.Log = sql => System.Diagnostics.Debug.WriteLine($"SQL Query: {sql}");
            System.Diagnostics.Debug.WriteLine("MessengerController initialized");
        }

        // GET: /Messenger/Chat
        [Authorize]
        [OutputCache(NoStore = true, Duration = 0)]
        public ActionResult Chat()
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

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

        // GET: /Messenger/GetMessagesForUser
        [Authorize]
        [OutputCache(NoStore = true, Duration = 0)]
        public JsonResult GetMessagesForUser(string userId)
        {
            try
            {
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
                Response.Cache.SetNoStore();

                if (string.IsNullOrEmpty(userId) || userId != User.Identity.GetUserId())
                {
                    System.Diagnostics.Debug.WriteLine("userId is null or does not match authenticated user in GetMessagesForUser.");
                    return Json(new { error = "ID người dùng không hợp lệ." }, JsonRequestBehavior.AllowGet);
                }

                System.Diagnostics.Debug.WriteLine("the ajax is running.");

                var messagesFromDb = _context.Messages
                    .Where(m => m.SenderId == userId || m.ReceiverId == userId)
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                var messages = messagesFromDb.Select(m => new
                {
                    senderId = m.SenderId,
                    content = m.Content,
                    timestamp = m.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
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
        [OutputCache(NoStore = true, Duration = 0)]
        public JsonResult GetAdminIds()
        {
            try
            {
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
                Response.Cache.SetNoStore();

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