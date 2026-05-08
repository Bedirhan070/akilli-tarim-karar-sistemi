using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TarimSistemi.Configuration;
using TarimSistemi.Data;
using TarimSistemi.Services;

namespace TarimSistemi
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Entity Framework - SQL Server
            builder.Services.AddDbContext<TarimDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Bearer {token}"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id   = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            builder.Services.Configure<EmailAyarlari>(builder.Configuration.GetSection("Email"));

            // Servisler
            builder.Services.AddScoped<IEmailGonderici, SmtpEmailGonderici>();
            builder.Services.AddScoped<AuthService>();
            builder.Services.AddHttpClient<HavaService>();
            builder.Services.AddHttpClient<MlService>(client =>
            {
                var baseUrl = builder.Configuration["FastApi:BaseUrl"];
                if (!string.IsNullOrWhiteSpace(baseUrl))
                    client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            });
            builder.Services.AddHttpClient<TelegramService>();
            builder.Services.AddSingleton<GunlukBildirimServisi>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<GunlukBildirimServisi>());

            // JWT Authentication
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
                    };

                    // MVC view'ları için: token'ı cookie'den de oku
                    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                    {
                        OnMessageReceived = ctx =>
                        {
                            if (string.IsNullOrEmpty(ctx.Token))
                                ctx.Token = ctx.Request.Cookies["tarim_token"];
                            return Task.CompletedTask;
                        },
                        // API dışı route'larda 401 yerine login sayfasına yönlendir
                        OnChallenge = ctx =>
                        {
                            if (!ctx.Request.Path.StartsWithSegments("/api"))
                            {
                                ctx.HandleResponse();
                                ctx.Response.Redirect("/Home/Login");
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            builder.Services.AddMemoryCache();
            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TarimDbContext>();
                DbInitializer.SeedUrunler(db);
            }

            // Telegram webhook kaydı
            // Önce Telegram:WebhookBaseUrl'e bak, yoksa Email:PublicBaseUrl'i kullan
            var botToken = app.Configuration["Telegram:BotToken"];
            var webhookBase = (app.Configuration["Telegram:WebhookBaseUrl"]
                               ?? app.Configuration["Email:PublicBaseUrl"] ?? "").TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(botToken) && !string.IsNullOrWhiteSpace(webhookBase)
                && !webhookBase.Contains("localhost"))
            {
                using var scope = app.Services.CreateScope();
                var telegram = scope.ServiceProvider.GetRequiredService<TelegramService>();
                var webhookUrl = $"{webhookBase}/api/telegram/webhook";
                var secretToken = app.Configuration["Telegram:WebhookSecretToken"];
                await telegram.WebhookKaydetAsync(webhookUrl, secretToken);
            }

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

           // app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapGet("/", () => Results.Redirect("/Home/Index"));

            app.Run();
        }
    }
}