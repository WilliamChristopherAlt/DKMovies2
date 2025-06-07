using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Net.Mail;
using System.Net;

using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;
using DKMovies.Models.ViewModels;

namespace DKMovies.Controllers
{
    public class AccountController : Controller
    {
        private void CreateUserNotification(int userId, string title, string message, int? ticketId = null, string type = "Order Status Update")
        {
            var notification = new Notification
            {
                UserID = userId,
                Title = title,
                Message = message,
                NotificationType = type,
                TicketID = ticketId, // ✅ Add the ticket ID
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.Notifications.Add(notification);
        }

        private void CreateAdminNotification(int adminId, string title, string message, int? ticketId = null, string type = "Security Alert")
        {
            var notification = new Notification
            {
                AdminID = adminId,
                Title = title,
                Message = message,
                NotificationType = type,
                TicketID = ticketId,
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.Notifications.Add(notification);
        }


        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }

        private async Task SendConfirmationEmail(string toEmail, string code)
        {
            var fromAddress = new MailAddress("ducn3683@gmail.com", "DKMovies");
            var toAddress = new MailAddress(toEmail);
            const string fromPassword = "ubuj nryh dbrf mrcd"; // App password (no spaces)
            string subject = "🎬 DKMovies - Account Confirmation";
            string body = $@"
        <html>
        <body style='font-family: Arial, sans-serif;'>
            <h2 style='color:#2c3e50;'>Confirm Your DKMovies Registration</h2>
            <p>Hello,</p>
            <p>Thank you for registering an account with <strong>DKMovies</strong>.</p>
            <p style='font-size:18px;'>
                🔐 Your confirmation code is:<br />
                <span style='font-size:24px; font-weight:bold; color:#2ecc71;'>{code}</span>
            </p>
            <p>This code is valid for a few minutes. Please do not share it with anyone.</p>
            <p>Best regards,<br />The DKMovies Team</p>
        </body>
        </html>";

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            };

            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            })
            {
                await smtp.SendMailAsync(message);
            }
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        //        [HttpPost]
        //        [ValidateAntiForgeryToken]
        //        public async Task<IActionResult> Login(string username, string password, bool rememberMe)
        //        {
        //            var hashedPassword = HashPassword(password);
        //            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username || u.Email == username);

        //            if (user != null && user.PasswordHash == hashedPassword)
        //            {
        //                if (!user.EmailConfirmed)
        //                {
        //                    ViewData["ToastError"] = "⚠️ Vui lòng xác minh email trước khi đăng nhập.";
        //                    ViewBag.ActiveTab = "login";
        //                    return View("Login");
        //                }

        //                if (user.TwoFactorEnabled)
        //                {
        //                    var code = new Random().Next(100000, 999999).ToString();
        //                    user.TwoFactorCode = code;
        //                    user.TwoFactorExpiry = DateTime.UtcNow.AddMinutes(5);
        //                    await _context.SaveChangesAsync();

        //                    await Send2FACodeEmail(user.Email, code);

        //                    TempData["Email2FA"] = user.Email;
        //                    TempData["RememberMe"] = rememberMe.ToString();
        //                    return RedirectToAction("Verify2FA");
        //                }

        //                await SignInUser(user, rememberMe);
        //                TempData["ToastSuccess"] = "🎉 Đăng nhập thành công!";
        //                return RedirectToAction("Index", "MoviesList");
        //            }

        //            ViewData["ToastError"] = "❌ Tên đăng nhập hoặc mật khẩu không đúng.";
        //            ViewBag.ActiveTab = "login";
        //            return View("Login");
        //        }

        private async Task SignInUser(User user, bool rememberMe)
        {
            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.ID.ToString()),
            new Claim(ClaimTypes.Role, "User")
        };

            var identity = new ClaimsIdentity(
                claims,
                "MyCookieAuth", // 👈 Must exactly match what you used in SignInAsync
                ClaimTypes.Name,
                ClaimTypes.Role
            );

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("MyCookieAuth", principal, new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddHours(1)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ActiveTab = "login";
                return View(model);
            }

            var hashedPassword = HashPassword(model.Password);
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            DateTime attemptTime = DateTime.UtcNow;

            // Try Users first
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == model.Username || u.Email == model.Username);
            if (user != null && user.PasswordHash == hashedPassword)
            {
                // Check if email is confirmed
                if (!user.EmailConfirmed)
                {
                    ModelState.AddModelError(string.Empty, "⚠️ Please verify your email before logging in.");
                    ViewBag.ActiveTab = "login";
                    return View(model);
                }

                // Handle Remember Me - Store credentials in cookies if checked
                if (model.RememberMe)
                {
                    var cookieOptions = new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddDays(30),
                        HttpOnly = false, // Allow JavaScript access for form population
                        Secure = Request.IsHttps,
                        SameSite = SameSiteMode.Strict
                    };

                    Response.Cookies.Append("RememberedUsername", model.Username, cookieOptions);
                    Response.Cookies.Append("RememberedPassword", model.Password, cookieOptions); // Note: Consider security implications
                    Response.Cookies.Append("RememberMe", "true", cookieOptions);
                }
                else
                {
                    // Clear remember me cookies if not checked
                    Response.Cookies.Delete("RememberedUsername");
                    Response.Cookies.Delete("RememberedPassword");
                    Response.Cookies.Delete("RememberMe");
                }

                // Check if login from new IP
                bool isNewDevice = !await _context.LoginAttempts.AnyAsync(a => a.UserID == user.ID && a.IPAddress == ipAddress);

                // Log attempt
                _context.LoginAttempts.Add(new LoginAttempt
                {
                    AttemptTime = attemptTime,
                    IsSuccessful = true,
                    IPAddress = ipAddress,
                    UserID = user.ID,
                    AdminID = null
                });

                if (isNewDevice)
                {
                    CreateUserNotification(
                        user.ID,
                        "🔐 New Login Detected",
                        $"A login was detected from a new device at {attemptTime:G} (IP: {ipAddress}). If this wasn't you, please change your password.",
                        null,
                        NotificationType.SecurityAlert.GetDisplayName()
                    );
                }

                await _context.SaveChangesAsync();

                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.ID.ToString()),
            new Claim(ClaimTypes.Role, "User")
        };

                var claimsIdentity = new ClaimsIdentity(claims, "MyCookieAuth");
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(1)
                };

                await HttpContext.SignInAsync("MyCookieAuth", new ClaimsPrincipal(claimsIdentity), authProperties);
                TempData["ToastSuccess"] = "🎉 Login successful!";
                return RedirectToAction("Index", "UserMovies");
            }

            // Try Admins
            var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Username == model.Username);
            if (admin != null && admin.PasswordHash == hashedPassword)
            {
                // Handle Remember Me for Admin
                if (model.RememberMe)
                {
                    var cookieOptions = new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddDays(30),
                        HttpOnly = false,
                        Secure = Request.IsHttps,
                        SameSite = SameSiteMode.Strict
                    };

                    Response.Cookies.Append("RememberedUsername", model.Username, cookieOptions);
                    Response.Cookies.Append("RememberedPassword", model.Password, cookieOptions);
                    Response.Cookies.Append("RememberMe", "true", cookieOptions);
                }
                else
                {
                    Response.Cookies.Delete("RememberedUsername");
                    Response.Cookies.Delete("RememberedPassword");
                    Response.Cookies.Delete("RememberMe");
                }

                bool isNewDevice = !await _context.LoginAttempts.AnyAsync(a => a.AdminID == admin.ID && a.IPAddress == ipAddress);

                // Log attempt
                _context.LoginAttempts.Add(new LoginAttempt
                {
                    AttemptTime = attemptTime,
                    IsSuccessful = true,
                    IPAddress = ipAddress,
                    AdminID = admin.ID,
                    UserID = null
                });

                if (isNewDevice)
                {
                    CreateAdminNotification(
                        admin.ID,
                        "🔐 New Login Detected",
                        $"A login was detected from a new device at {attemptTime:G} (IP: {ipAddress}).",
                        null,
                        NotificationType.SecurityAlert.GetDisplayName()
                    );
                }

                await _context.SaveChangesAsync();

                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, admin.Username),
            new Claim(ClaimTypes.NameIdentifier, admin.ID.ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        };

                var claimsIdentity = new ClaimsIdentity(claims, "MyCookieAuth");
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
                };

                await HttpContext.SignInAsync("MyCookieAuth", new ClaimsPrincipal(claimsIdentity), authProperties);
                TempData["ToastSuccess"] = "🎉 Login successful!";
                return RedirectToAction("Index", "Admin");
            }

            // Clear remember me cookies on failed login
            Response.Cookies.Delete("RememberedUsername");
            Response.Cookies.Delete("RememberedPassword");
            Response.Cookies.Delete("RememberMe");

            // Add error message (no longer logging failed attempts)
            ModelState.AddModelError(string.Empty, "❌ Invalid username or password.");
            ViewBag.ActiveTab = "login";
            return View(model);
        }

        [HttpGet]
        public IActionResult Verify2FA()
        {
            ViewBag.Email = TempData["Email2FA"] ?? TempData.Peek("Email2FA");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify2FA(string email, string code)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || !user.TwoFactorEnabled)
            {
                ModelState.AddModelError("", "Invalid account or 2FA not enabled.");
                return View();
            }

            if (user.TwoFactorCode == code && user.TwoFactorExpiry > DateTime.UtcNow)
            {
                user.TwoFactorCode = null;
                user.TwoFactorExpiry = null;
                await _context.SaveChangesAsync();

                var rememberMe = TempData["RememberMe"] != null && bool.TryParse(TempData["RememberMe"].ToString(), out var r) && r;
                await SignInUser(user, rememberMe);

                TempData["ToastSuccess"] = "🎉 Login successful!";
                return RedirectToAction("Index", "UserMovies");
            }

            ModelState.AddModelError("", "Invalid or expired verification code.");
            ViewBag.Email = TempData["Email2FA"] ?? TempData.Peek("Email2FA");
            return View();
        }

        private async Task Send2FACodeEmail(string toEmail, string code)
        {
            var fromAddress = new MailAddress("ducn3683@gmail.com", "DKMovies");
            var toAddress = new MailAddress(toEmail);
            const string fromPassword = "ubuj nryh dbrf mrcd"; // No spaces if you 
            string subject = "🎬 DKMovies - Account Verification";
            string body = $@"
        <html>
        <body style='font-family: Arial, sans-serif;'>
            <h2 style='color:#2c3e50;'>DKMovies Registration Confirmation</h2>
            <p>Hello,</p>
            <p>Thank you for registering an account with <strong>DKMovies</strong>.</p>
            <p style='font-size:18px;'>
                🔐 Your verification code is:<br />
                <span style='font-size:24px; font-weight:bold; color:#2ecc71;'>{code}</span>
            </p>
            <p>This code is valid for a few minutes. Please do not share this code with anyone.</p>
            <p>Best regards,<br />The DKMovies Team</p>
        </body>
        </html>";

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            };

            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            })
            {
                await smtp.SendMailAsync(message);
            }
        }

        [HttpGet("Account/Signup")]
        public IActionResult Signup()
        {
            ViewBag.ActiveTab = "register";
            return View("Login");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignUp(string username, string email, string password,
                           string fullName, string phone,
                           DateTime? birthDate, string gender)
        {
            ViewBag.ActiveTab = "register";

            // Store form data in ViewBag to repopulate fields
            ViewBag.RegUsername = username;
            ViewBag.RegEmail = email;
            ViewBag.RegFullName = fullName;
            ViewBag.RegPhone = phone;
            ViewBag.RegBirthDate = birthDate?.ToString("yyyy-MM-dd");
            ViewBag.RegGender = gender;

            // Basic validation
            if (string.IsNullOrEmpty(username))
            {
                ViewData["ToastError"] = "Username is required.";
                return View("Login");
            }
            if (string.IsNullOrEmpty(email))
            {
                ViewData["ToastError"] = "Email is required.";
                return View("Login");
            }
            if (string.IsNullOrEmpty(password))
            {
                ViewData["ToastError"] = "Password is required.";
                return View("Login");
            }
            if (password.Length < 6)
            {
                ViewData["ToastError"] = "Password must be at least 6 characters.";
                return View("Login");
            }
            if (string.IsNullOrEmpty(fullName))
            {
                ViewData["ToastError"] = "Full name is required.";
                return View("Login");
            }
            if (string.IsNullOrEmpty(gender))
            {
                ViewData["ToastError"] = "Please select a gender.";
                return View("Login");
            }
            // Check for existing username
            if (await _context.Users.AnyAsync(u => u.Username == username))
            {
                ViewData["ToastError"] = "Username already exists.";
                return View("Login");
            }
            // Check for existing email
            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                ViewData["ToastError"] = "Email is already in use.";
                return View("Login");
            }
            // Generate confirmation code
            string confirmationCode = new Random().Next(100000, 999999).ToString();
            // Send confirmation email
            try
            {
                await SendConfirmationEmail(email, confirmationCode);
            }
            catch (Exception)
            {
                ViewData["ToastError"] = "Cannot send confirmation email. Please try again later.";
                return View("Login");
            }
            // Create user if email sent successfully
            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = HashPassword(password),
                FullName = fullName,
                Phone = phone,
                BirthDate = birthDate,
                Gender = gender,
                CreatedAt = DateTime.Now,
                EmailConfirmed = false,
                ConfirmationCode = confirmationCode
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            TempData["EmailToVerify"] = email;
            return RedirectToAction("VerifyEmail");
        }

        [HttpGet]
        public IActionResult VerifyEmail()
        {
            ViewBag.Email = TempData["EmailToVerify"] ?? TempData.Peek("EmailToVerify");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(string email, string code)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            {
                ViewData["ToastError"] = "Please fill in all required information.";
                ViewBag.Email = email;
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                ViewData["ToastError"] = "Account does not exist.";
                ViewBag.Email = email;
                return View();
            }

            if (user.EmailConfirmed)
            {
                TempData["ToastSuccess"] = "Email has already been verified previously.";
                return RedirectToAction("Login");
            }

            if (user.ConfirmationCode == code)
            {
                try
                {
                    // Update email confirmation status
                    user.EmailConfirmed = true;
                    user.ConfirmationCode = null;

                    // Ensure Entity Framework tracks changes
                    _context.Entry(user).State = EntityState.Modified;

                    // Save changes and check result
                    var result = await _context.SaveChangesAsync();

                    if (result > 0)
                    {
                        CreateUserNotification(
                            user.ID,
                            "🔐 Email Verified",
                            "Your email has been successfully verified.",
                            null,
                            NotificationType.SecurityAlert.GetDisplayName()
                        );

                        await _context.SaveChangesAsync(); // Save the notification
                        TempData["ToastSuccess"] = "✅ Verification successful! You can now log in.";
                        return RedirectToAction("Login");
                    }
                    else
                    {
                        ViewData["ToastError"] = "❌ An error occurred while saving information. Please try again.";
                        ViewBag.Email = email;
                        return View();
                    }
                }
                catch (Exception ex)
                {
                    // Log error for debugging
                    // You can log ex.Message to see specific error
                    ViewData["ToastError"] = "❌ An error occurred while updating information. Please try again.";
                    ViewBag.Email = email;
                    return View();
                }
            }

            ViewData["ToastError"] = "❌ Verification code is incorrect or has expired.";
            ViewBag.Email = email;
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("MyCookieAuth");
            TempData["ToastSuccess"] = "🎉 Logged out successfully!";
            return RedirectToAction("Login", "Account");
        }
    }


}
