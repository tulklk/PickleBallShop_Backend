using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Interfaces;
using PickleBall.Application.Mapping;
using PickleBall.Application.Validators;
using PickleBall.Infrastructure.Data;
using PickleBall.Infrastructure.Services;
using ApplicationDbContext = PickleBall.Infrastructure.Data.ApplicationDbContext;

namespace PickleBall;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        builder.Services.AddControllers();

        // Configure Swagger
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "PickleBall Shop API",
                Version = "v1",
                Description = "E-commerce API for PickleBall products"
            });

            // JWT Authentication in Swagger
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Enter your token (without 'Bearer' prefix)",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Group by tags
            c.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] ?? "Default" });
        });

        // Configure Entity Framework Core with PostgreSQL
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        
        if (!string.IsNullOrEmpty(connectionString) && !connectionString.Contains("your-supabase-host"))
        {
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));
        }
        else
        {
            // Use in-memory database if connection string not configured (for development/testing)
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("PickleBallShop"));
        }

        // Configure JWT Authentication
        var jwtSettings = builder.Configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"] ?? "PickleBallShop",
                ValidAudience = jwtSettings["Audience"] ?? "PickleBallShop",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ClockSkew = TimeSpan.Zero // Remove clock skew tolerance
            };

            // Add event handlers for better error logging
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(context.Exception, "Authentication failed: {Error}", context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogWarning("Authentication challenge: {Error}, {ErrorDescription}", 
                        context.Error, context.ErrorDescription);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("Token validated successfully for user: {UserId}", 
                        context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
                    return Task.CompletedTask;
                }
            };
        });

        builder.Services.AddAuthorization();

        // Configure AutoMapper
        builder.Services.AddAutoMapper(typeof(MappingProfile));

        // Configure FluentValidation
        builder.Services.AddFluentValidationAutoValidation();
        builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

        // Configure CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // Configure payment settings
        builder.Services.Configure<MoMoPaymentSettings>(builder.Configuration.GetSection("Payment:MoMo"));
        builder.Services.Configure<VnPayPaymentSettings>(builder.Configuration.GetSection("Payment:VNPay"));

        // Register services
        builder.Services.AddScoped<ITokenService, TokenService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IProductService, ProductService>();
        builder.Services.AddScoped<ICategoryService, CategoryService>();
        builder.Services.AddScoped<ICartService, CartService>();
        builder.Services.AddScoped<IOrderService, OrderService>();
        builder.Services.AddScoped<IMoMoPaymentService, MoMoPaymentService>();
        builder.Services.AddScoped<IVnPayPaymentService, VnPayPaymentService>();
        builder.Services.AddScoped<IPaymentService, PaymentService>();

        var app = builder.Build();

        // Configure the HTTP request pipeline
        // Enable Swagger in all environments (you can restrict to Development if needed)
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "PickleBall Shop API V1");
            c.RoutePrefix = "swagger"; // Swagger UI at /swagger
        });

        // Global error handling middleware
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception ex)
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "Unhandled exception occurred");

                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "An error occurred while processing your request",
                    error = app.Environment.IsDevelopment() ? ex.Message : null
                });
            }
        });

        // Only redirect to HTTPS in development
        // In production (Render), HTTPS is handled by the reverse proxy
        if (app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseCors("AllowAll");

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        // Apply migrations on startup (optional - remove in production)
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();
                // Uncomment to apply migrations automatically
                context.Database.Migrate();
                SeedData.SeedAsync(context).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred while migrating the database");
            }
        }

        app.Run();
    }
}