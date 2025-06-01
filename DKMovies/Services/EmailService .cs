// ============================================
// 1. CREATE: Services/IEmailService.cs
// ============================================
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using DKMovies.Models;
using DKMovies.ViewModels;

namespace DKMovies.Services
{
    public interface IEmailService
    {
        Task SendTicketConfirmationEmailAsync(string userEmail, string userName, Ticket ticket);
        Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);
    }
}

namespace DKMovies.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task SendTicketConfirmationEmailAsync(string userEmail, string userName, Ticket ticket)
        {
            try
            {
                var subject = $"Ticket Confirmation - {ticket.ShowTime?.Movie?.Title}";
                var body = GenerateTicketConfirmationHtml(userName, ticket);

                await SendEmailAsync(userEmail, subject, body, true);
                _logger.LogInformation($"Ticket confirmation email sent to {userEmail} for ticket ID {ticket.ID}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send ticket confirmation email to {userEmail}");
                throw;
            }
        }

        public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
        {
            try
            {
                using var client = new SmtpClient(_emailSettings.SmtpHost, _emailSettings.SmtpPort);
                client.EnableSsl = _emailSettings.EnableSsl;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password);

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml
                };

                mailMessage.To.Add(to);

                await client.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {to}");
                throw;
            }
        }

        private string GenerateTicketConfirmationHtml(string userName, Ticket ticket)
        {
            var seatNumbers = string.Join(", ",
                ticket.TicketSeats?.Select(ts => ts.Seat != null ? ts.Seat.SeatNumber.ToString() : "N/A")
                ?? Enumerable.Empty<string>());

            var showTime = ticket.ShowTime?.StartTime.ToString("dddd, MMMM dd, yyyy 'at' hh:mm tt");
            var theaterName = ticket.ShowTime?.Auditorium?.Theater?.Name ?? "Theater";
            var auditoriumName = ticket.ShowTime?.Auditorium?.Name ?? "Auditorium";

            // Calculate total with concessions
            var ticketPrice = (ticket.ShowTime?.Price ?? 0) * (ticket.TicketSeats?.Count ?? 0);
            var concessionTotal = ticket.OrderItems?.Sum(oi => oi.Quantity * oi.PriceAtPurchase) ?? 0;
            var totalAmount = ticketPrice + concessionTotal;

            var concessionsList = "";
            if (ticket.OrderItems?.Any() == true)
            {
                concessionsList = "<h3>Concessions:</h3><ul>";
                foreach (var item in ticket.OrderItems)
                {
                    var concessionName = item.TheaterConcession?.Concession?.Name ?? "Item";
                    concessionsList += $"<li>{item.Quantity}x {concessionName} - ${item.PriceAtPurchase * item.Quantity:F2}</li>";
                }
                concessionsList += "</ul>";
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .header {{ text-align: center; color: #333; border-bottom: 2px solid #e74c3c; padding-bottom: 20px; margin-bottom: 30px; }}
        .ticket-info {{ background-color: #f8f9fa; padding: 20px; border-radius: 8px; margin: 20px 0; }}
        .info-row {{ display: flex; justify-content: space-between; margin: 10px 0; padding: 5px 0; border-bottom: 1px solid #eee; }}
        .label {{ font-weight: bold; color: #555; }}
        .value {{ color: #333; }}
        .total {{ font-size: 18px; font-weight: bold; color: #e74c3c; text-align: center; margin: 20px 0; padding: 15px; background-color: #fff3f3; border-radius: 8px; }}
        .footer {{ text-align: center; margin-top: 30px; color: #666; font-size: 14px; }}
        .qr-placeholder {{ text-align: center; margin: 20px 0; padding: 20px; background-color: #f8f9fa; border: 2px dashed #ddd; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎬 Ticket Confirmation</h1>
            <p>Thank you for your purchase!</p>
        </div>
        
        <p>Dear {userName},</p>
        <p>Your movie ticket has been successfully confirmed! Here are your booking details:</p>
        
        <div class='ticket-info'>
            <div class='info-row'>
                <span class='label'>Movie:</span>
                <span class='value'>{ticket.ShowTime?.Movie?.Title ?? "Movie Title"}</span>
            </div>
            <div class='info-row'>
                <span class='label'>Date & Time:</span>
                <span class='value'>{showTime}</span>
            </div>
            <div class='info-row'>
                <span class='label'>Theater:</span>
                <span class='value'>{theaterName}</span>
            </div>
            <div class='info-row'>
                <span class='label'>Auditorium:</span>
                <span class='value'>{auditoriumName}</span>
            </div>
            <div class='info-row'>
                <span class='label'>Seats:</span>
                <span class='value'>{seatNumbers}</span>
            </div>
            <div class='info-row'>
                <span class='label'>Ticket ID:</span>
                <span class='value'>#{ticket.ID}</span>
            </div>
        </div>

        {concessionsList}

        <div class='total'>
            Total Amount: ${totalAmount:F2}
        </div>

        <div class='qr-placeholder'>
            <p>📱 Show this email at the theater</p>
            <p>Ticket ID: #{ticket.ID}</p>
        </div>

        <div class='footer'>
            <p><strong>Important:</strong> Please arrive at least 15 minutes before showtime.</p>
            <p>Present this email confirmation at the theater entrance.</p>
            <p><em>Enjoy your movie!</em></p>
            <hr style='margin: 20px 0;'>
            <p style='font-size: 12px; color: #999;'>
                This is an automated message. Please do not reply to this email.<br>
                For support, contact us at support@dkmovies.com
            </p>
        </div>
    </div>
</body>
</html>";
        }
    }
}