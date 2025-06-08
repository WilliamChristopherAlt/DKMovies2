using Microsoft.EntityFrameworkCore;
using DKMovies.Services; // Add this using statement
using Stripe;
using DKMovies.ViewModels;
using Controllers.UserController;

using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<LayoutDataFilter>();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<LayoutDataFilter>();
});

// Add session service to handle login/logout state
builder.Services.AddDistributedMemoryCache(); // Enable memory cache for session storage

// Add DbContext for SQL Server connection
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "MyCookieAuth";
})
.AddCookie("MyCookieAuth", options =>
{
    options.LoginPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization();

// Configure Stripe settings
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));

// Configure Email settings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

// Register Email Service
builder.Services.AddScoped<IEmailService, EmailService>();

// Add logging
builder.Services.AddLogging();

var app = builder.Build();

// Configure Stripe API Key
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Admin}/{action=Index}/{id?}");

// Run the application
app.Run();

// Stripe configuration class
public class StripeSettings
{
    public string PublishableKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}