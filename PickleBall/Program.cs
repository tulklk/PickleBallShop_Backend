using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
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
using PickleBall.Services;

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
            var pgConn = NpgsqlConnectionStringHelper.ForEfCore(connectionString);
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(pgConn, o => o.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null)));
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

                    if (!string.IsNullOrEmpty(accessToken) &&
                        (path.StartsWithSegments("/hubs/support-chat") ||
                         path.StartsWithSegments("/hubs/admin-notifications")))
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
                    logger.LogDebug("Token validated for user: {UserId}",
                        context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
                    return Task.CompletedTask;
                }
            };
        });

        builder.Services.AddAuthorization();
        builder.Services.AddSignalR();

        // Fly.io / reverse proxy: use X-Forwarded-Proto so Request.Scheme is https (JWT, cookies, SignalR)
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        // Configure AutoMapper
        builder.Services.AddAutoMapper(typeof(MappingProfile));

        // Configure FluentValidation
        builder.Services.AddFluentValidationAutoValidation();
        builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

        builder.Services.AddMemoryCache();
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                new[] { "application/json" });
        });

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
        builder.Services.AddSingleton<AdminOrderNotificationPublisher>();
        builder.Services.AddSingleton<IAdminOrderNotificationPublisher>(sp => sp.GetRequiredService<AdminOrderNotificationPublisher>());
        builder.Services.AddSingleton<IAdminSupportMessageNotificationPublisher>(sp => sp.GetRequiredService<AdminOrderNotificationPublisher>());
        
        builder.Services.AddHttpClient("Gemini", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<IGeminiChatService, GeminiChatService>();

        var app = builder.Build();

        static string? FormatDevExceptionDetail(Exception ex)
        {
            var inner = ex;
            var parts = new List<string> { inner.Message };
            while (inner.InnerException != null)
            {
                inner = inner.InnerException;
                parts.Add(inner.Message);
            }
            return string.Join(" | ", parts);
        }

        async Task WriteJsonErrorAsync(HttpContext context, int statusCode, string message, Exception? ex = null)
        {
            if (context.Response.HasStarted) return;
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            string? errorDetail = null;
            if (ex != null && app.Environment.IsDevelopment())
                errorDetail = FormatDevExceptionDetail(ex);

            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message,
                traceId = context.TraceIdentifier,
                error = errorDetail
            });
        }

        // Configure the HTTP request pipeline
        // Must be first so UseAuthentication / SignalR see correct Scheme (https) behind Fly.io proxy
        app.UseForwardedHeaders();

        app.UseResponseCompression();

        // CORS must be very early in the pipeline, before authentication and error handling
        // This ensures preflight OPTIONS requests are handled correctly
        app.UseCors("AllowConfiguredOrigins");

        // Global exception handling + request timing for hot API paths
        app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            var sw = Stopwatch.StartNew();
            try
            {
                await next();
            }
            catch (UnauthorizedAccessException uex)
            {
                logger.LogWarning(uex, "Unauthorized: {Message}", uex.Message);
                await WriteJsonErrorAsync(context, StatusCodes.Status401Unauthorized, uex.Message, uex);
            }
            catch (KeyNotFoundException knf)
            {
                logger.LogWarning(knf, "Not found: {Message}", knf.Message);
                await WriteJsonErrorAsync(context, StatusCodes.Status404NotFound, knf.Message, knf);
            }
            catch (ArgumentException aex)
            {
                logger.LogWarning(aex, "Bad request: {Message}", aex.Message);
                await WriteJsonErrorAsync(context, StatusCodes.Status400BadRequest, aex.Message, aex);
            }
            catch (InvalidOperationException ioex)
            {
                logger.LogWarning(ioex, "Invalid operation: {Message}", ioex.Message);
                await WriteJsonErrorAsync(context, StatusCodes.Status400BadRequest, ioex.Message, ioex);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception. TraceId: {TraceId}", context.TraceIdentifier);
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "application/json";
                    string? errorDetail = app.Environment.IsDevelopment() ? FormatDevExceptionDetail(ex) : null;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        success = false,
                        message = "An error occurred while processing your request",
                        traceId = context.TraceIdentifier,
                        error = errorDetail
                    });
                }
            }
            finally
            {
                sw.Stop();
                var path = context.Request.Path.Value ?? "";
                if (path.StartsWith("/api/Products", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/api/Categories", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/api/News", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/api/Home", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation(
                        "HTTP {Method} {Path} completed in {ElapsedMs}ms with status {StatusCode}",
                        context.Request.Method,
                        path,
                        sw.ElapsedMilliseconds,
                        context.Response.StatusCode);
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
        app.MapHub<AdminNotificationsHub>("/hubs/admin-notifications");

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
        // Production: set Fly.io secret FRONTEND_URL=https://your-app.vercel.app (preview deploys need their URL added too)
        AddOrigin(Environment.GetEnvironmentVariable("FRONTEND_URL"));
        AddOrigin(configuration["PublicFrontendUrl"]);

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