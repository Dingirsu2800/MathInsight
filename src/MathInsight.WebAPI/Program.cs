using MassTransit;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MathInsight.Modules.Identity_Access;
using MathInsight.Modules.QuestionBank;
using MathInsight.Modules.Testing;
using MathInsight.Modules.TestGen;
using MathInsight.Modules.Grading_Analytics;
using MathInsight.Modules.Recommender;
using MathInsight.Modules.Learning_Lecture;
using MathInsight.Modules.Gamification;
using MathInsight.Modules.Notification_Report;
// using MathInsight.Modules.Grading_Analytics.Consumers;
// using MathInsight.Modules.Recommender.Consumers;
using MathInsight.Modules.Grading_Analytics.Handlers;
using System.IdentityModel.Tokens.Jwt;
using MathInsight.Modules.Identity_Access.Services.Auth;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Ocr;
using MathInsight.Shared.Results;
using MathInsight.Shared.Storage;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
const string CorsPolicyName = "MathInsightCors";

// 1. Add MediatR (In-process Event Bus)
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GradeSubmittedSessionHandler).Assembly); // Registers Grading Handlers
});

// 2. Add MassTransit.
// Local development uses the in-memory transport by default so the API can run without RabbitMQ.
// Set RabbitMQ:Enabled=true in environment/Azure configuration when a real broker is available.
var rabbitMqEnabled = builder.Configuration.GetValue<bool>("RabbitMQ:Enabled");
builder.Services.AddMassTransit(x =>
{
    // Register asynchronous consumers
    // x.AddConsumer<TestSubmittedConsumer>();
    // x.AddConsumer<GradeCalculatedConsumer>();

    if (rabbitMqEnabled)
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost", "/", h =>
            {
                h.Username(builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest");
                h.Password(builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest");
            });

            // Auto-configure endpoints based on consumers
            cfg.ConfigureEndpoints(context);
        });
    }
    else
    {
        x.UsingInMemory((context, cfg) =>
        {
            cfg.ConfigureEndpoints(context);
        });
    }
});

// 3. Add Real-time SignalR hubs
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? new[] { "http://localhost:5173" };

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// 4. Register Domain Modules (Composition Root)
var cloudinaryOptions = new CloudinaryOptions
{
    CloudName = builder.Configuration["Cloudinary:CloudName"] ?? string.Empty,
    ApiKey = builder.Configuration["Cloudinary:ApiKey"] ?? string.Empty,
    ApiSecret = builder.Configuration["Cloudinary:ApiSecret"] ?? string.Empty
};
builder.Services.AddSingleton(cloudinaryOptions);
builder.Services.AddHttpClient<IImageStorage, CloudinaryImageStorage>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddQuestionBankModule(builder.Configuration);
builder.Services.AddTestingModule(builder.Configuration);
builder.Services.AddTestGenModule(builder.Configuration);
builder.Services.AddGradingModule(builder.Configuration);
builder.Services.AddRecommenderModule(builder.Configuration);
builder.Services.AddLearningModule(builder.Configuration);
builder.Services.AddGamificationModule(builder.Configuration);
builder.Services.AddNotificationModule(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
// Swashbuckle renders [FromForm] IFormFile parameters (e.g. teacher registration) as a
// multipart/form-data file upload automatically.
builder.Services.AddSwaggerGen(options =>
{
    // Swashbuckle 7.x throws a SwaggerGeneratorException when an action binds a *top-level*
    // [FromForm] IFormFile parameter (QuestionBank's QuestionsController.UploadQuestionImage and
    // ExtractQuestionOcrDraft). That failure aborts generation of the entire document, so no
    // endpoint shows up in Swagger. The exception is raised while reading the action's parameters,
    // which happens *before* any IOperationFilter/IDocumentFilter runs — so a filter cannot repair
    // it. DocInclusionPredicate is evaluated before an operation is generated, so we use it to skip
    // just those broken operations and let the rest of the document (auth endpoints included)
    // generate. This lives in the WebAPI composition root and does not touch the QuestionBank
    // module. Auth's register/teacher binds a [FromForm] form-model (not a bare IFormFile), so it
    // is unaffected and still renders as a multipart file upload.
    options.DocInclusionPredicate((docName, apiDescription) =>
    {
        // Preserve Swashbuckle's default document/group matching for everything else.
        if (apiDescription.GroupName != null && apiDescription.GroupName != docName)
            return false;

        if (apiDescription.ActionDescriptor is ControllerActionDescriptor descriptor)
        {
            var bindsTopLevelFormFile = descriptor.MethodInfo
                .GetParameters()
                .Any(parameter =>
                    parameter.GetCustomAttribute<FromFormAttribute>() != null &&
                    (typeof(IFormFile).IsAssignableFrom(parameter.ParameterType) ||
                     typeof(IFormFileCollection).IsAssignableFrom(parameter.ParameterType)));

            if (bindsTopLevelFormFile)
                return false;
        }

        return true;
    });

    // JWT Bearer auth for Swagger UI: renders the Authorize button so a token issued by
    // /api/v1/auth/login can be pasted and sent on [Authorize]-protected endpoints
    // (e.g. /api/v1/accounts/profile). Documentation only — this does not affect how tokens
    // are validated at runtime; that is the AddJwtBearer configuration below.
    const string bearerSchemeId = "Bearer";

    options.AddSecurityDefinition(bearerSchemeId, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,

        // Type=Http with Scheme="bearer" makes Swagger UI send "Authorization: Bearer {token}"
        // on its own, so only the raw token is pasted into the Authorize dialog.
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the access token returned by /api/v1/auth/login. Do not include the \"Bearer \" prefix."
    });

    // Applied document-wide: every operation offers the token. Anonymous endpoints (login,
    // register, confirm-email, logout) simply ignore the header, so this costs nothing and
    // avoids an operation filter that would have to re-derive each action's [Authorize] state.
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = bearerSchemeId
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, cancellationToken) =>
    {
        var policyName = context.HttpContext.GetEndpoint()?
            .Metadata
            .GetMetadata<EnableRateLimitingAttribute>()?
            .PolicyName;
        var error = policyName == QuestionOcrRateLimit.PolicyName
            ? QuestionBankErrors.OcrRateLimitExceeded
            : ApplicationErrors.RateLimitExceeded;

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ApiErrorResponse(error),
            cancellationToken);
    };

    options.AddPolicy(QuestionOcrRateLimit.PolicyName, httpContext =>
    {
        var partitionKey = httpContext.User.FindFirst("account_id")?.Value
            ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

//5 .Jwt
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],

            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSigningKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),

            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            // Diagnostics only — neither handler below changes whether a token is accepted.
            // Fires when token validation throws: bad signature, expired, wrong issuer/audience.
            OnAuthenticationFailed = context =>
            {
                context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JWTAuth")
                    .LogError(context.Exception, "JWT auth failed");

                return Task.CompletedTask;
            },

            // OnAuthenticationFailed does NOT cover a context.Fail() raised inside
            // OnTokenValidated (the blacklist / session checks below) — that path sets a failed
            // AuthenticateResult without throwing, so it reaches the client as a bare 401
            // invalid_token with nothing logged. OnChallenge runs for every 401 and carries the
            // failure, so it is the only place those reasons become visible.
            OnChallenge = context =>
            {
                context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JWTAuth")
                    .LogError(
                        context.AuthenticateFailure,
                        "JWT challenge → 401. error={Error}; description={Description}; failure={Failure}",
                        context.Error,
                        context.ErrorDescription,
                        context.AuthenticateFailure?.Message ?? "(none — no token, or rejected by OnTokenValidated)");

                return Task.CompletedTask;
            },

            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (principal is null)
                {
                    context.Fail("Missing principal.");
                    return;
                }

                var tokenId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value
                    ?? principal.FindFirst("jti")?.Value;

                if (string.IsNullOrWhiteSpace(tokenId))
                {
                    context.Fail("Missing jti claim.");
                    return;
                }

                var authSessionService = context.HttpContext.RequestServices
                    .GetRequiredService<IAuthSessionService>();

                // Revocation (BR-10, BR-15): logout, password change/reset, deactivation, and
                // permission changes all blacklist the outstanding jti. This is the only
                // per-request session check — BR-02 (Student single-session) is enforced at login
                // by RevokeAllSessionsAsync, which deletes the previous refresh token; the
                // superseded access token then dies on its own within 15 minutes (DD-02).
                var isBlacklisted = await authSessionService.IsTokenBlacklistedAsync(tokenId);
                if (isBlacklisted)
                {
                    context.Fail("Token has been revoked.");
                }
            }
        };
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicyName);

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

// Map SignalR Hub for realtime notifications
// app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
