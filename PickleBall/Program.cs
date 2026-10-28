using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;               // don't advertise the server software
            options.Limits.MaxRequestBodySize = 2 * 1024 * 1024; // API accepts JSON only; no uploads
        });
        
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
        var secretKey = jwtSettings["SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey)
            || secretKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)
            || Encoding.UTF8.GetByteCount(secretKey) < 32)
        {
            // Fail fast: never run with a missing, placeholder or short signing key.
            throw new InvalidOperationException(
                "Jwt:SecretKey must be configured (environment variable Jwt__SecretKey) with at least 32 bytes of random data.");
        }

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
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
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
                OnTokenValidated = async context =>
                {
                    // A signed JWT stays valid until it expires; re-check the account so deactivated users
                    // and users whose role changed lose access immediately (cached briefly to spare the DB).
                    var services = context.HttpContext.RequestServices;
                    var config = services.GetRequiredService<IConfiguration>();
                    var userIdClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    var roleClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

                    if (!Guid.TryParse(userIdClaim, out var userId))
                    {
                        context.Fail("Invalid subject");
                        return;
                    }

                    var ttlSeconds = config.GetValue("Security:AuthStateCacheSeconds", 30);
                    var cache = services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                    var cacheKey = $"authstate:{userId}";

                    if (ttlSeconds <= 0 || !cache.TryGetValue(cacheKey, out AuthState? state) || state == null)
                    {
                        var db = services.GetRequiredService<ApplicationDbContext>();
                        var row = await db.Users.AsNoTracking()
                            .Where(u => u.Id == userId)
                            .Select(u => new { u.IsActive, u.Role })
                            .FirstOrDefaultAsync();

                        state = row == null
                            ? new AuthState(false, string.Empty)
                            : new AuthState(row.IsActive, row.Role.ToString());

                        if (ttlSeconds > 0)
                        {
                            cache.Set(cacheKey, state, TimeSpan.FromSeconds(ttlSeconds));
                        }
                    }

                    if (!state.IsActive || !string.Equals(state.Role, roleClaim, StringComparison.Ordinal))
                    {
                        context.Fail("Account is inactive or role changed");
                    }
                }
            };
        });

        builder.Services.AddAuthorization();
        builder.Services.AddSignalR();

        // Fly.io / reverse proxy: use X-Forwarded-Proto so Request.Scheme is https (JWT, cookies, SignalR)
        // Only trust forwarded headers from private/internal networks (the platform proxy), never from the
        // public internet; otherwise any client could spoof its IP (rate-limit bypass, audit-log poisoning).
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();

            var trustProxy = builder.Configuration.GetValue("Security:TrustProxyHeaders", true);
            if (trustProxy)
            {
                foreach (var cidr in new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "fc00::/7", "fdaa::/16" })
                {
                    var parts = cidr.Split('/');
                    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(
                        System.Net.IPAddress.Parse(parts[0]), int.Parse(parts[1])));
                }
                options.KnownProxies.Add(System.Net.IPAddress.Loopback);
                options.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);
            }

            foreach (var proxy in builder.Configuration.GetSection("Security:TrustedProxies").Get<string[]>() ?? Array.Empty<string>())
            {
                if (System.Net.IPAddress.TryParse(proxy, out var ip))
                {
                    options.KnownProxies.Add(ip);
                }
            }
        });

        ConfigureRateLimiting(builder);

        // Explicit DTO mapping (hand-written; replaces AutoMapper)
        builder.Services.AddSingleton<PickleBall.Application.Mapping.IMapper, PickleBall.Application.Mapping.AppMapper>();

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
                      .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                      .WithHeaders("Authorization", "Content-Type", "Accept", "X-Requested-With",
                                   "x-signalr-user-agent", "Cache-Control", "Pragma")
                      .AllowCredentials()
                      .WithExposedHeaders("X-Total-Count", "X-Page", "X-Page-Size", "X-Total-Pages");
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

        // Business-rule exceptions are thrown by our own code with user-facing messages. Exceptions that originate
        // in the framework / EF / drivers (connection strings, SQL, internals) must not be echoed back.
        static string SafeMessage(Exception ex, string fallback)
        {
            var assembly = ex.TargetSite?.DeclaringType?.Assembly.GetName().Name;
            return assembly != null && assembly.StartsWith("PickleBall", StringComparison.Ordinal)
                ? ex.Message
                : fallback;
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

        // Security headers + no-store for API responses (responses may contain personal / order data)
        var isDevelopment = app.Environment.IsDevelopment();
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                var isSwagger = context.Request.Path.StartsWithSegments("/swagger");
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
                headers["Cross-Origin-Resource-Policy"] = "same-site";
                if (!isSwagger)
                {
                    headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
                }
                if (!isDevelopment && context.Request.IsHttps)
                {
                    headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
                }
                if (!headers.ContainsKey("Cache-Control"))
                {
                    headers["Cache-Control"] = "no-store";
                    headers["Pragma"] = "no-cache";
                }
                return Task.CompletedTask;
            });

            await next();
        });

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
                logger.LogWarning("Unauthorized: {Message}", uex.Message);
                await WriteJsonErrorAsync(context, StatusCodes.Status401Unauthorized, SafeMessage(uex, "Unauthorized"), uex);
            }
            catch (KeyNotFoundException knf)
            {
                logger.LogWarning("Not found: {Message}", knf.Message);
                await WriteJsonErrorAsync(context, StatusCodes.Status404NotFound, SafeMessage(knf, "Not found"), knf);
            }
            catch (ArgumentException aex)
            {
                logger.LogWarning("Bad request: {Message}", aex.Message);
                await WriteJsonErrorAsync(context, StatusCodes.Status400BadRequest, SafeMessage(aex, "Invalid request"), aex);
            }
            catch (InvalidOperationException ioex)
            {
                logger.LogWarning(ioex, "Invalid operation: {Message}", ioex.Message);
                await WriteJsonErrorAsync(context, StatusCodes.Status400BadRequest, SafeMessage(ioex, "Invalid request"), ioex);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception. TraceId: {TraceId}", context.TraceIdentifier);
                // Never return exception text/stack to the client outside Development.
                await WriteJsonErrorAsync(context, StatusCodes.Status500InternalServerError,
                    "An error occurred while processing your request", ex);
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

        // Swagger exposes the full attack surface: Development only (opt-in elsewhere via Swagger:Enabled=true)
        if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", false))
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "PickleBall Shop API V1");
                c.RoutePrefix = "swagger"; // Swagger UI at /swagger
                c.DisplayRequestDuration();
            });
        }

        // Only redirect to HTTPS in development
        // In production (Fly.io), HTTPS is handled by the reverse proxy
        if (app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        // Rate limiting (after forwarded headers so the real client IP is used)
        if (app.Configuration.GetValue("Security:RateLimiting:Enabled", true))
        {
            app.UseRateLimiter();
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
                        if (context.Database.IsRelational())
                        {
                            await context.Database.MigrateAsync();
                        }
                        else
                        {
                            await context.Database.EnsureCreatedAsync();
                        }
                        await SeedData.SeedAsync(context, app.Configuration, logger);
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
    /// Per-IP fixed-window rate limits on abuse-prone endpoints (credential stuffing, enumeration, spam, paid AI usage).
    /// Policies are attached with [EnableRateLimiting("name")].
    /// </summary>
    private static void ConfigureRateLimiting(WebApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (ctx, ct) =>
            {
                if (ctx.Lease.TryGetMetadata(System.Threading.RateLimiting.MetadataName.RetryAfter, out var retryAfter))
                {
                    ctx.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }
                ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await ctx.HttpContext.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Bạn thao tác quá nhanh. Vui lòng thử lại sau."
                }, ct);
            };

            static string Key(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            void Fixed(string name, int permits, TimeSpan window) =>
                options.AddPolicy(name, c => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                    $"{name}:{Key(c)}",
                    _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permits,
                        Window = window,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            Fixed("login", 10, TimeSpan.FromMinutes(1));
            Fixed("register", 5, TimeSpan.FromMinutes(10));
            Fixed("password", 5, TimeSpan.FromMinutes(15));
            Fixed("voucher", 30, TimeSpan.FromMinutes(1));
            Fixed("guest-lookup", 20, TimeSpan.FromMinutes(1));
            Fixed("guest-order", 10, TimeSpan.FromMinutes(10));
            Fixed("chat", 15, TimeSpan.FromMinutes(1));
            Fixed("write", 20, TimeSpan.FromMinutes(10));
            Fixed("payment-confirm", 30, TimeSpan.FromMinutes(1));

            // Coarse global ceiling per client IP
            options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(c =>
                System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                    $"global:{Key(c)}",
                    _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 600,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });
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

            // Only exact http(s) origins: never "*", "null", wildcards or URLs with paths.
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                uri.AbsolutePath != "/" || trimmed.Contains('*'))
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

/// <summary>Cached subset of the user row used to re-validate JWTs.</summary>
internal sealed record AuthState(bool IsActive, string Role);
