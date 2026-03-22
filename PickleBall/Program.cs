using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PickleBall.Application.DTOs.Email;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Interfaces;
using PickleBall.Application.Mapping;
using PickleBall.Application.Validators;
using PickleBall.Hubs;
using PickleBall.Infrastructure.Data;
using PickleBall.Infrastructure.Services;

namespace PickleBall;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Only override ASPNETCORE_URLS if PORT is set (for Fly.io deployment)
        // In local development, launchSettings.json will handle the URLs
        var port = Environment.GetEnvironmentVariable("PORT");
        if (!string.IsNullOrEmpty(port))
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"http://+:{port}");
        }

        // Add services to the container
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                // Support both string and number for enum values (e.g., "COD" or 0)
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                // Case insensitive property matching
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                // Allow reading numbers from strings
                options.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
                // Reference handling for recursive types
                options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
            });

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
            
            // Ignore obsolete actions
            c.IgnoreObsoleteActions();
            
            // Custom schema ID generation to avoid conflicts
            c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
            
            // Use fully qualified names to avoid ambiguity
            c.UseAllOfToExtendReferenceSchemas();
            c.UseOneOfForPolymorphism();
            c.SupportNonNullableReferenceTypes();
            
            // Include XML comments for better documentation
            try
            {
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }
            }
            catch
            {
                // Ignore if XML file doesn't exist
            }
            
            // Filter out problematic controllers/actions
            c.DocInclusionPredicate((docName, apiDesc) => true);
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
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/support-chat"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                },
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
        builder.Services.AddSignalR();

        // Configure AutoMapper
        builder.Services.AddAutoMapper(typeof(MappingProfile));

        // Configure FluentValidation
        builder.Services.AddFluentValidationAutoValidation();
        builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

        // Configure CORS
        // SignalR negotiate + fetch from browser may use credentials; browsers forbid
        // Access-Control-Allow-Origin: * with credentials. Must use explicit origins + AllowCredentials.
        var corsOrigins = BuildCorsAllowedOrigins(builder.Configuration, builder.Environment);
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowConfiguredOrigins", policy =>
            {
                policy.WithOrigins(corsOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()
                      .WithExposedHeaders("*");
            });

            options.DefaultPolicyName = "AllowConfiguredOrigins";
        });

        // Configure payment settings
        builder.Services.Configure<MoMoPaymentSettings>(builder.Configuration.GetSection("Payment:MoMo"));
        builder.Services.Configure<VnPayPaymentSettings>(builder.Configuration.GetSection("Payment:VNPay"));
        builder.Services.Configure<PayOSSettings>(builder.Configuration.GetSection("Payment:PayOS"));

        // Register PayOS service with HttpClient
        builder.Services.AddHttpClient<IPayOSService, PayOSService>();

        // Configure email settings
        builder.Services.Configure<EmailSettings>(
            builder.Configuration.GetSection("Email"));

        // Register services
        builder.Services.AddScoped<ITokenService, TokenService>();
        builder.Services.AddScoped<IEmailService, EmailService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<IProductService, ProductService>();
        builder.Services.AddScoped<ICategoryService, CategoryService>();
        builder.Services.AddScoped<ICartService, CartService>();
        builder.Services.AddScoped<IWishlistService, WishlistService>();
        builder.Services.AddScoped<IProductCommentService, ProductCommentService>();
        builder.Services.AddScoped<IProductReviewService, ProductReviewService>();
        builder.Services.AddScoped<INewsService, NewsService>();
        builder.Services.AddScoped<IOrderService, OrderService>();
        builder.Services.AddScoped<IVoucherService, VoucherService>();
        builder.Services.AddScoped<IMoMoPaymentService, MoMoPaymentService>();
        builder.Services.AddScoped<IVnPayPaymentService, VnPayPaymentService>();
        builder.Services.AddScoped<IPaymentService, PaymentService>();
        builder.Services.AddScoped<ISupportChatService, SupportChatService>();
        
        // Register HttpClient Factory and Gemini Chat Service
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<IGeminiChatService, GeminiChatService>();

        var app = builder.Build();

        // Configure the HTTP request pipeline
        
        // CORS must be very early in the pipeline, before authentication and error handling
        // This ensures preflight OPTIONS requests are handled correctly
        app.UseCors("AllowConfiguredOrigins");

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

                // Ensure CORS headers are still set even on error
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "application/json";

                    // In Development, include inner exception (e.g. PostgreSQL constraint message)
                    var errorDetail = (string?)null;
                    if (app.Environment.IsDevelopment())
                    {
                        var inner = ex;
                        var parts = new List<string> { inner.Message };
                        while (inner.InnerException != null)
                        {
                            inner = inner.InnerException;
                            parts.Add(inner.Message);
                        }
                        errorDetail = string.Join(" | ", parts);
                    }

                    await context.Response.WriteAsJsonAsync(new
                    {
                        message = "An error occurred while processing your request",
                        error = errorDetail
                    });
                }
            }
        });

        // Enable Swagger in all environments
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "PickleBall Shop API V1");
            c.RoutePrefix = "swagger"; // Swagger UI at /swagger
            c.DisplayRequestDuration();
        });

        // Only redirect to HTTPS in development
        // In production (Fly.io), HTTPS is handled by the reverse proxy
        if (app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<SupportChatHub>("/hubs/support-chat");

        // Apply migrations on startup (non-blocking)
        // If migrations fail, app will still start but may have database issues
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000); // Wait 2 seconds for app to fully start
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var context = services.GetRequiredService<ApplicationDbContext>();
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    
                    // Test database connection first
                    if (await context.Database.CanConnectAsync())
                    {
                        logger.LogInformation("Database connection successful, applying migrations...");
                        await context.Database.MigrateAsync();
                        await SeedData.SeedAsync(context);
                        logger.LogInformation("Migrations and seeding completed successfully");
                    }
                    else
                    {
                        logger.LogWarning("Cannot connect to database. Migrations skipped.");
                    }
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred while migrating the database. App will continue running.");
                }
            }
        });

        app.Run();
    }

    /// <summary>
    /// Collects allowed CORS origins from Cors:AllowedOrigins, Email:FrontendUrl, and dev defaults.
    /// </summary>
    private static string[] BuildCorsAllowedOrigins(IConfiguration configuration, IHostEnvironment environment)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddOrigin(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            var trimmed = url.Trim().TrimEnd('/');
            if (trimmed.Contains("your-frontend", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("localhost-placeholder", StringComparison.OrdinalIgnoreCase))
                return;
            set.Add(trimmed);
        }

        var fromSection = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (fromSection != null)
        {
            foreach (var o in fromSection)
                AddOrigin(o);
        }

        AddOrigin(configuration["Email:FrontendUrl"]);

        // Local dev (Next/Vite/React) — needed when FE hits Fly.io API from localhost
        if (environment.IsDevelopment())
        {
            AddOrigin("http://localhost:3000");
            AddOrigin("http://127.0.0.1:3000");
            AddOrigin("http://localhost:5173");
            AddOrigin("http://localhost:4200");
        }

        if (set.Count == 0)
        {
            throw new InvalidOperationException(
                "CORS: configure at least one origin in Cors:AllowedOrigins or Email:FrontendUrl. " +
                "Wildcard origin is not compatible with SignalR when the browser sends credentials.");
        }

        return set.ToArray();
    }
}