using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;

var builder = WebApplication.CreateBuilder(args);



// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnectionForUsers")));
builder.Services.AddDbContext<ApplicationDBNotificationContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnectionForNotifications")));

builder.Services.AddAuthentication("CookieAuth")
    .AddCookie("CookieAuth", options =>
    {
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.LoginPath = "/login"; // Custom login path if needed
        options.LogoutPath = "/logout"; // Custom logout path if needed
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanAccessNotifications", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
        {
            // Allow access for all authenticated users except admins
            return !context.User.IsInRole("Administrator");
        });
    });
});


builder.Services.AddScoped<LegacyAuthenticationService>();//Try to understand what you have done here :)
builder.Services.AddScoped<NotificationsService>();

//builder.Services.AddTransient<IEmailService, EmailService>(); //for 2-factor authentication


var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.Run();

