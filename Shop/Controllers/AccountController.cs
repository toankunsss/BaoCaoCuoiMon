using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Shop.Models;
using Shop.Areas.Administrator.Data.message;
using System.Runtime.Caching;
using Shop.Mail;

namespace Shop.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private static MemoryCache _cache = MemoryCache.Default;
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        MyDataDataContext context = new MyDataDataContext();

        public AccountController()
        {
        }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set
            {
                _signInManager = value;
            }
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        //----------------------------Đăng nhập Admin-----------------------------
        public bool AuthAdmin(AspNetUser checkUser)
        {
            if (checkUser == null || string.IsNullOrEmpty(checkUser.UserName))
                return false;

            var user = context.AspNetUsers.FirstOrDefault(u => u.UserName == checkUser.UserName);
            if (user == null)
                return false;

            var userExist = user.AspNetUserRoles?.FirstOrDefault(r => r.UserId == user.Id);
            if (userExist == null)
                return false;

            return userExist.RoleId == "1";
        }

        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await SignInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, shouldLockout: false);
            switch (result)
            {
                case SignInStatus.Success:
                    {
                        AspNetUser kh = context.AspNetUsers.Where(p => p.Email == model.Email).FirstOrDefault();
                        if (AuthAdmin(kh) == true)
                        {
                            Session["taikhoanadmin"] = kh;
                            Notification.set_flash("Đăng nhập Admin thành công!", "success");
                            return RedirectToAction("Index", "Administrator/MainPage");
                        }
                        else
                        {
                            if (kh.LockoutEnabled == false)
                            {
                                AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                                return View("Lockout");
                            }
                            Session["TaiKhoan"] = kh;
                            Notification.set_flash("Đăng nhập khách hàng thành công!", "success");
                            return RedirectToLocal(returnUrl);
                        }
                    }
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.RequiresVerification:
                    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });
                case SignInStatus.Failure:
                default:
                    ModelState.AddModelError("", "Thông tin đăng nhập không hợp lệ.");
                    return View(model);
            }
        }

        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email, hoten = model.hoten, diachi = model.diachi };

                // Tạo mã xác minh ngẫu nhiên
                var verificationCode = GenerateVerificationCode();
                var cacheKey = $"VerificationCode_{model.Email}";

                // Lưu thông tin người dùng và mã xác minh vào bộ nhớ cache (hết hạn sau 10 phút)
                var cacheItem = new
                {
                    User = user,
                    Password = model.Password,
                    VerificationCode = verificationCode
                };
                _cache.Set(cacheKey, cacheItem, new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10) });

                // Gửi mã xác minh đến người dùng (qua email)
                await SendVerificationCode(model.Email, verificationCode);

                Notification.set_flash("Vui lòng kiểm tra email để nhận mã xác thực!", "info");
                return RedirectToAction("VerifyCode", new { email = model.Email });
            }

            return View(model);
        }

        // GET: /Account/VerifyCode
        [AllowAnonymous]
        public ActionResult VerifyCode(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return View("Error");
            }
            return View(new VerifyCodeViewModel { Email = email });
        }

        // POST: /Account/VerifyCode
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> VerifyCode(VerifyCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var cacheKey = $"VerificationCode_{model.Email}";
            var cachedData = _cache.Get(cacheKey);

            if (cachedData == null)
            {
                ModelState.AddModelError("", "Mã xác thực đã hết hạn hoặc không tồn tại.");
                return View(model);
            }

            var cachedItem = (dynamic)cachedData;
            if (cachedItem.VerificationCode != model.Code)
            {
                ModelState.AddModelError("", "Mã xác thực không đúng.");
                return View(model);
            }

            // Mã hợp lệ, tiến hành tạo người dùng
            var user = cachedItem.User as ApplicationUser;
            var password = cachedItem.Password as string;

            var result = await UserManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);

                // Xóa mục bộ nhớ cache sau khi xác minh thành công
                _cache.Remove(cacheKey);

                Notification.set_flash("Đăng ký tài khoản khách thành công!", "success");
                return RedirectToAction("Index", "Home");
            }

            AddErrors(result);
            return View(model);
        }

        // Phương thức hỗ trợ để tạo mã xác minh ngẫu nhiên
        private string GenerateVerificationCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString(); // Mã 6 chữ số
        }

        // Phương thức hỗ trợ để gửi mã xác minh (triển khai logic gửi email tại đây)
        private async Task<bool> SendVerificationCode(string email, string code)
        {
            try
            {
                var mailHelper = new MailHelper();
                await Task.Run(() =>
                {
                    mailHelper.SendEmail(
                        email,
                        "Mã xác minh tài khoản",
                        $"Mã xác minh của bạn là: {code}. Vui lòng nhập mã này để hoàn tất đăng ký."
                    );
                });
                return true; // Gửi thành công
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi gửi email: {ex.Message}");
                return false; // Gửi thất bại
            }
        }
        // GET: /Account/ConfirmEmail (Tùy chọn, nếu bạn muốn giữ xác nhận email)
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmEmail(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return View("Error");
            }
            var result = await UserManager.ConfirmEmailAsync(userId, code);
            return View(result.Succeeded ? "ConfirmEmail" : "Error");
        }

        // GET: /Account/ForgotPassword
        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        // POST: /Account/ForgotPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await UserManager.FindByNameAsync(model.Email);
                if (user == null || !(await UserManager.IsEmailConfirmedAsync(user.Id)))
                {
                    return View("ForgotPasswordConfirmation");
                }
            }
            return View(model);
        }

        // GET: /Account/ForgotPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        // GET: /Account/ResetPassword
        [AllowAnonymous]
        public ActionResult Locking()
        {
            return View();
        }

        // GET: /Account/ResetPassword
        [AllowAnonymous]
        public ActionResult ResetPassword(string code)
        {
            return code == null ? View("Error") : View();
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var user = await UserManager.FindByNameAsync(model.Email);
            if (user == null)
            {
                return RedirectToAction("ResetPasswordConfirmation", "Account");
            }
            var result = await UserManager.ResetPasswordAsync(user.Id, model.Code, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction("ResetPasswordConfirmation", "Account");
            }
            AddErrors(result);
            return View();
        }

        // GET: /Account/ResetPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        // POST: /Account/ExternalLogin
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLogin(string provider, string returnUrl)
        {
            return new ChallengeResult(provider, Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl }));
        }

        // GET: /Account/SendCode
        [AllowAnonymous]
        public async Task<ActionResult> SendCode(string returnUrl, bool rememberMe)
        {
            var userId = await SignInManager.GetVerifiedUserIdAsync();
            if (userId == null)
            {
                return View("Error");
            }
            var userFactors = await UserManager.GetValidTwoFactorProvidersAsync(userId);
            var factorOptions = userFactors.Select(purpose => new SelectListItem { Text = purpose, Value = purpose }).ToList();
            return View(new SendCodeViewModel { Providers = factorOptions, ReturnUrl = returnUrl, RememberMe = rememberMe });
        }

        // POST: /Account/SendCode
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SendCode(SendCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View();
            }

            if (!await SignInManager.SendTwoFactorCodeAsync(model.SelectedProvider))
            {
                return View("Error");
            }
            return RedirectToAction("VerifyCode", new { Provider = model.SelectedProvider, ReturnUrl = model.ReturnUrl, RememberMe = model.RememberMe });
        }

        // GET: /Account/ExternalLoginCallback
        [AllowAnonymous]
        public async Task<ActionResult> ExternalLoginCallback(string returnUrl)
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync();
            if (loginInfo == null)
            {
                return RedirectToAction("Login");
            }

            var result = await SignInManager.ExternalSignInAsync(loginInfo, isPersistent: false);
            switch (result)
            {
                case SignInStatus.Success:
                    {
                        var kh = context.AspNetUsers.Where(p => p.Email == loginInfo.Email).FirstOrDefault();
                        if (AuthAdmin(kh) == true)
                        {
                            Session["taikhoanadmin"] = kh;
                            Notification.set_flash("Đăng nhập Admin thành công!", "success");
                            return RedirectToAction("Index", "Administrator/MainPage");
                        }
                        else
                        {
                            if (kh.LockoutEnabled == false)
                            {
                                AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                                return View("Lockout");
                            }
                            Session["TaiKhoan"] = kh;
                            Notification.set_flash("Đăng nhập khách hàng thành công!", "success");
                            return RedirectToLocal(returnUrl);
                        }
                    }
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.RequiresVerification:
                    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = false });
                case SignInStatus.Failure:
                default:
                    ViewBag.ReturnUrl = returnUrl;
                    ViewBag.LoginProvider = loginInfo.Login.LoginProvider;
                    return View("ExternalLoginConfirmation", new ExternalLoginConfirmationViewModel { Email = loginInfo.Email });
            }
        }

        // POST: /Account/ExternalLoginConfirmation
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model, string returnUrl)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Manage");
            }

            if (ModelState.IsValid)
            {
                var info = await AuthenticationManager.GetExternalLoginInfoAsync();
                if (info == null)
                {
                    return View("ExternalLoginFailure");
                }
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email, hoten = "", diachi = "" };
                var result = await UserManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    result = await UserManager.AddLoginAsync(user.Id, info.Login);
                    if (result.Succeeded)
                    {
                        await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                        return RedirectToLocal(returnUrl);
                    }
                }
                AddErrors(result);
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            Session["taikhoanadmin"] = null;
            Session["TaiKhoan"] = null;
            Notification.set_flash("Đăng xuất thành công!", "success");
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/ExternalLoginFailure
        [AllowAnonymous]
        public ActionResult ExternalLoginFailure()
        {
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_userManager != null)
                {
                    _userManager.Dispose();
                    _userManager = null;
                }

                if (_signInManager != null)
                {
                    _signInManager.Dispose();
                    _signInManager = null;
                }
            }

            base.Dispose(disposing);
        }

        #region Helpers
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        internal class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri)
                : this(provider, redirectUri, null)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties { RedirectUri = RedirectUri };
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }

        public async Task<ActionResult> UserProfile()
        {
            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
            var model = new IndexViewModel
            {
            PhoneNumber = user.PhoneNumber,
            diachi = user.diachi, // Thêm địa chỉ
            HasPassword = await UserManager.HasPasswordAsync(user.Id),
            TwoFactor = await UserManager.GetTwoFactorEnabledAsync(user.Id)
            };
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UpdateProfile(IndexViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return HttpNotFound();
            }

            // Cập nhật PhoneNumber và diachi
            user.PhoneNumber = model.PhoneNumber;
            user.diachi = model.diachi;

            // Lưu thay đổi vào cơ sở dữ liệu
            var result = await UserManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                // Hiển thị thông báo thành công
                TempData["StatusMessage"] = "Cập nhật thông tin thành công!";
                return RedirectToAction("Index");
            }

            // Nếu có lỗi, hiển thị lỗi
            AddErrors(result);
            return View("Index", model);
        }

 
        #endregion
    }

}