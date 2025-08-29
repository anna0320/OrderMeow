using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OrderMeow.Core.Entities;
using OrderMeow.Core.Enums;
using OrderMeow.Core.Interfaces;
using OrderMeow.Infrastructure.Config;
using OrderMeow.Infrastructure.Persistence;
using OrderMeow.Infrastructure.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
if (jwtSettings == null || string.IsNullOrEmpty(jwtSettings.SecretKey))
{
    throw new InvalidOperationException("JWT settings are not configured properly.");
}

builder.Services
    .Configure<IISServerOptions>(options =>
    {
        options.AutomaticAuthentication = false;
    })
    .Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMq"))
    .Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"))
    .AddMemoryCache(options =>
    {
        options.SizeLimit = 512;
        options.CompactionPercentage = 0.3;
    })
    .AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("Redis");
        options.InstanceName = "OrderMeow_";
    });
    
builder.Services
    .AddSingleton(jwtSettings)
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.Zero,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer
        };
        
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception}");
                return Task.CompletedTask;
            },
            OnTokenValidated = _ =>
            {
                Console.WriteLine("Token successfully validated");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                var result = System.Text.Json.JsonSerializer.Serialize(context.Error);
                return context.Response.WriteAsync(result);
            }
        };
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("UserOrAdmin", policy => 
        policy.RequireRole(nameof(RoleType.User), nameof(RoleType.Admin)));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddControllers();


builder.Services.AddScoped<IUserService, UserService>()
    .AddScoped<IOrderService, OrderService>()
    .AddScoped<IJwtService, JwtService>()
    .AddScoped<IMessageQueueService, RabbitMqService>()
    .AddScoped<ICacheService, CacheService>();


builder.Services.AddEndpointsApiExplorer()
    .AddSwaggerGen(c =>
    {
        var jwtSecurityScheme = new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Description = "Please insert JWT with Bearer into field",
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = JwtBearerDefaults.AuthenticationScheme
            }
        };
        c.AddSecurityDefinition(jwtSecurityScheme.Reference.Id, jwtSecurityScheme);
        c.AddSecurityRequirement(new OpenApiSecurityRequirement()
        {
            {
                jwtSecurityScheme, Array.Empty<string>()
            }
        });
    });


var app = builder.Build();

app.UseAuthentication()
    .UseAuthorization();

app.UseHttpMetrics();
app.MapMetrics();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!dbContext.Users.Any(u => u.Role == RoleType.Admin))
    {
        dbContext.Users.Add(new User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("secureAdminPassword"),
            Role = RoleType.Admin
        });
        await dbContext.SaveChangesAsync();
    }
}

app.Run();
