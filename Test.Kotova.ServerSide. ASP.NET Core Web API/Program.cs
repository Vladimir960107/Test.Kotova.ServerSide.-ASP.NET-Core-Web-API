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
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Extensions; // For ServiceExtensions
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Security.Cryptography.X509Certificates;
using Serilog;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel server
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

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ================================
// SERVICE REGISTRATION USING EXTENSIONS
// ================================

// Option 1: Use individual extension methods (current approach)
builder.Services.ConfigureLynksDbContext(builder.Configuration);
builder.Services.ConfigureTransElectroDbContext(builder.Configuration);
builder.Services.ConfigureLynksDbService();

// Option 2: Use combined extension method (alternative - choose one)
// builder.Services.AddLynksServices(builder.Configuration);

// Add Controllers with JSON options
builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            //options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
        });

// Add SignalR
builder.Services.AddSignalR();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "LYNKS API", Version = "v1" });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Введите токен в формате 'Bearer {token}'",
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

    // Include XML comments for API documentation
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

// Register singleton services
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

// Configure JWT Authentication
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

// Configure Authorization policies
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

// Register scoped services
builder.Services.AddScoped<LegacyAuthenticationService>();
builder.Services.AddScoped<NotificationsService>();
builder.Services.AddScoped<MyDataService>();

// Build the application
var app = builder.Build();

// ================================
// DATABASE INITIALIZATION
// ================================

try
{
    await app.Services.EnsureDatabasesAvailableAsync();
    Log.Information("Database initialization completed successfully");
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to initialize databases");
    // Decide if you want to continue or stop the application
    // throw; // Uncomment to stop application on database failure
}

// ================================
// MIDDLEWARE CONFIGURATION
// ================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Ограничьте доступ к Swagger UI в производственной среде 
    //!!!!!!!!!!your-secret-key - зашифруй его, добавь в отдельный файл и спрячь!
    app.UseWhen(context => context.Request.Path.StartsWithSegments("/swagger"), appBuilder =>
    {
        appBuilder.Use(async (context, next) =>
        {
            if (!context.Request.Headers.ContainsKey("X-Swagger-Auth") ||
                context.Request.Headers["X-Swagger-Auth"] != "your-secret-key")
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }

            await next();
        });
    });

    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "LYNKS API v1"));
}

//app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Map SignalR hub
app.MapHub<NotificationHub>("/notificationHub");

// Map controllers
app.MapControllers();

Log.Information("LYNKS Application started successfully");
app.Run();