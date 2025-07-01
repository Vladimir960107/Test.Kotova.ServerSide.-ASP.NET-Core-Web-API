using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Extensions; // Added for ServiceExtensions
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Security.Cryptography.X509Certificates;
using Serilog;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);


builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5239); // Set HTTP port
    /*serverOptions.ListenAnyIP(7052, listenOptions => // Set HTTPS port
    {
        listenOptions.UseHttps("C:/Users/hifly/Desktop/OpenSSL FireDaemon/certificate.pfx", "Test321!", configureOptions =>
        {
            configureOptions.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
        });
    });*/
});

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Configure DbContext and DbService using extension methods
builder.Services.ConfigureLynksDbContext(builder.Configuration);
builder.Services.ConfigureTransElectroDbContext(builder.Configuration);
builder.Services.ConfigureLynksDbService();

builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            //options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
        });

builder.Services.AddSignalR();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "My API", Version = "v1" });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "¬ведите токен в формате 'Bearer {token}'",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

builder.Services.AddSingleton<ChiefsManager>();
builder.Services.AddSingleton<JWTTokenValidator>();

// Get JWT settings with validation
var jwtSecret = builder.Configuration["JwtConfig:Secret"];
if (string.IsNullOrEmpty(jwtSecret))
{
Log.Error("JWT Secret is missing in configuration");
throw new InvalidOperationException("JWT Secret is not configured. Please add JwtConfig:Secret to your appsettings.json");
}

var jwtIssuer = builder.Configuration["JwtConfig:Issuer"] ?? "DefaultIssuer";
var jwtAudience = builder.Configuration["JwtConfig:Audience"] ?? "DefaultAudience";

// Then use these validated settings in your JWT configuration
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };

        // Enable SignalR JWT Authentication
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];

                // If the request is for our SignalR hub
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/notificationHub"))
                {
                    // Read the token out of the query string
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Coordinator", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
        {
            return context.User.IsInRole("Coordinator");
        });
    });
    options.AddPolicy("ChiefOfDepartment", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
        {
            return context.User.IsInRole("ChiefOfDepartment");
        });
    });
    options.AddPolicy("User", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
        {
            return context.User.IsInRole("User");
        });
    });
    options.AddPolicy("Administrator", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
        {
            return context.User.IsInRole("Administrator");
        });
    });
    options.AddPolicy("Management", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
        {
            return context.User.IsInRole("Management");
        });
    });
    options.AddPolicy("DeputyChief", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => context.User.IsInRole("DeputyChief"));
    });
});

builder.Services.AddScoped<LegacyAuthenticationService>();
builder.Services.AddScoped<NotificationsService>();
builder.Services.AddScoped<MyDataService>();

var app = builder.Build();


try
{
    await app.Services.EnsureDatabasesAvailableAsync();
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to initialize databases");
    // Decide if you want to continue or stop the application
}



if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // ќграничьте доступ к Swagger UI в производственной среде 
    //!!!!!!!!!!your-secret-key - зашифруй его, добавь в отдельный файл и спр€чь!
    app.UseWhen(context => context.Request.Path.StartsWithSegments("/swagger"), appBuilder =>
    {
        appBuilder.Use(async (context, next) =>
        {
            if (!context.Request.Headers.ContainsKey("X-Swagger-Auth") || context.Request.Headers["X-Swagger-Auth"] != "your-secret-key")
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }

            await next();
        });
    });

    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API v1"));
}

//app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Map your SignalR hub here
app.MapHub<NotificationHub>("/notificationHub");

app.MapControllers();
app.Run();