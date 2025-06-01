using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;
using DKMovies.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Net;

namespace DKMovies.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PaymentController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult CreateCheckoutSession(int ticketId)
        {
            Ticket? ticket = null;

            try
            {
                // Include all necessary related data
                ticket = _context.Tickets
                    .Include(t => t.ShowTime)
                        .ThenInclude(st => st.Movie)
                    .Include(t => t.TicketSeats)
                        .ThenInclude(ts => ts.Seat)
                    .Include(t => t.OrderItems)
                        .ThenInclude(oi => oi.TheaterConcession)
                            .ThenInclude(tc => tc.Concession)
                    .FirstOrDefault(t => t.ID == ticketId);

                if (ticket == null)
                {
                    TempData["Error"] = "Ticket not found.";
                    return RedirectToAction("Index", "MoviesList");
                }

                // Calculate total price
                decimal totalPrice = ticket.TotalPrice;

                if (totalPrice <= 0)
                {
                    TempData["Error"] = "Invalid ticket price.";
                    return RedirectToAction("Index", "MoviesList");
                }

                var domain = $"{Request.Scheme}://{Request.Host}";

                var options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card" },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                Currency = "usd",
                                UnitAmount = (long)(totalPrice * 100), // Convert to cents
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = $"Movie Ticket for {ticket.ShowTime?.Movie?.Title ?? "Selected Movie"}",
                                    Description = $"Seats: {string.Join(", ", ticket.TicketSeats?.Select(ts => ts.Seat?.SeatNumber.ToString() ?? "N/A") ?? new[] { "No seats" })}"
                                }
                            },
                            Quantity = 1
                        }
                    },
                    Mode = "payment",
                    SuccessUrl = $"{domain}/Payment/Success?ticketId={ticketId}",
                    CancelUrl = $"{domain}/Payment/Cancel?ticketId={ticketId}"
                };

                var service = new SessionService();
                var session = service.Create(options);

                return Redirect(session.Url);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Payment processing error: {ex.Message}";
                return RedirectToAction("OrderTicketDetails", "Tickets", new { id = ticket?.ShowTimeID });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Success(int ticketId)
        {
            try
            {
                var ticket = _context.Tickets
                    .Include(t => t.ShowTime)
                        .ThenInclude(st => st.Movie)
                    .Include(t => t.ShowTime)
                        .ThenInclude(st => st.Auditorium)
                            .ThenInclude(a => a.Theater)
                    .Include(t => t.TicketSeats)
                        .ThenInclude(ts => ts.Seat)
                    .Include(t => t.OrderItems)
                        .ThenInclude(oi => oi.TheaterConcession)
                            .ThenInclude(tc => tc.Concession)
                    .Include(t => t.User)
                    .FirstOrDefault(t => t.ID == ticketId);

                if (ticket != null && ticket.Status == TicketStatus.PENDING)
                {
                    // Update ticket status
                    ticket.Status = TicketStatus.PAID;
                    _context.SaveChanges();

                    // Send confirmation email
                    if (ticket.User != null && !string.IsNullOrEmpty(ticket.User.Email))
                    {
                        try
                        {
                            await SendTicketConfirmationEmail(ticket.User.Email, ticket.User.Username, ticket);
                            TempData["Success"] = "Payment successful! Confirmation email sent.";
                        }
                        catch (Exception emailEx)
                        {
                            // Log email error but don't fail the payment process
                            TempData["Warning"] = "Payment successful, but we couldn't send your confirmation email.";
                        }
                    }
                }

                return RedirectToAction("OrderConfirmation", "Tickets", new { ticketId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Payment was processed but there was an error. Please contact support.";
                return RedirectToAction("OrderConfirmation", "Tickets", new { ticketId });
            }
        }

        public IActionResult Cancel(int? ticketId)
        {
            if (ticketId.HasValue)
            {
                TempData["Info"] = "Payment was cancelled. You can try again or modify your booking.";
                return RedirectToAction("OrderTicketDetails", "Tickets", new { id = ticketId });
            }

            return View();
        }

        private async Task SendTicketConfirmationEmail(string toEmail, string userName, Ticket ticket)
        {
            var fromAddress = new MailAddress("ducn3683@gmail.com", "DKMovies");
            var toAddress = new MailAddress(toEmail);
            const string fromPassword = "ubuj nryh dbrf mrcd";

            string subject = "🎬 DKMovies - Xác nhận vé xem phim";

            var movieTitle = ticket.ShowTime?.Movie?.Title ?? "Phim";
            var showTime = ticket.ShowTime?.StartTime.ToString("dddd, dd/MM/yyyy 'lúc' HH:mm") ?? "Chưa xác định";
            var theaterName = ticket.ShowTime?.Auditorium?.Theater?.Name ?? "Rạp";
            var auditoriumName = ticket.ShowTime?.Auditorium?.Name ?? "Phòng";

            var seatNumbers = string.Join(", ",
                ticket.TicketSeats?.Select(ts => ts.Seat != null ? ts.Seat.SeatNumber.ToString() : "N/A") ??
                new[] { "Chưa có ghế" });

            decimal ticketPrice = (ticket.ShowTime?.Price ?? 0) * (ticket.TicketSeats?.Count ?? 0);
            decimal concessionTotal = ticket.OrderItems?.Sum(oi => oi.Quantity * oi.PriceAtPurchase) ?? 0;
            decimal totalAmount = ticketPrice + concessionTotal;

            string body = $@"
        <html>
        <body style='font-family: Arial, sans-serif;'>
            <h2 style='color:#2c3e50;'>🎫 Xác nhận vé DKMovies</h2>
            <p>Xin chào <strong>{userName}</strong>,</p>
            <p>Bạn đã đặt vé xem phim <strong>{movieTitle}</strong> thành công!</p>
            <p><strong>Thời gian:</strong> {showTime}</p>
            <p><strong>Rạp:</strong> {theaterName} - {auditoriumName}</p>
            <p><strong>Ghế:</strong> {seatNumbers}</p>
            <p><strong>Tổng thanh toán:</strong> ${totalAmount:F2}</p>
            <p>Mã vé: <strong>#{ticket.ID}</strong></p>
            <hr />
            <p>Trình email này tại quầy vé để nhận vé vào rạp. Chúc bạn xem phim vui vẻ!</p>
        </body>
        </html>";

            using var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            };

            using var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            try
            {
                await smtp.SendMailAsync(message);
                Console.WriteLine("Email sent to " + toEmail);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Email failed: " + ex.Message);
                throw; // Let the caller decide how to handle it
            }
        }

    }
}