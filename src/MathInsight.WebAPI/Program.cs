using MassTransit;
using MathInsight.Modules.Identity_Access;
using MathInsight.Modules.QuestionBank;
using MathInsight.Modules.Testing;
using MathInsight.Modules.TestGen;
using MathInsight.Modules.Grading_Analytics;
using MathInsight.Modules.Recommender;
using MathInsight.Modules.Learning_Lecture;
using MathInsight.Modules.Gamification;
using MathInsight.Modules.Notification_Report;
using MathInsight.Modules.Grading_Analytics.Consumers;
using MathInsight.Modules.Recommender.Consumers;
using MathInsight.Modules.Grading_Analytics.Handlers;

var builder = WebApplication.CreateBuilder(args);
const string CorsPolicyName = "MathInsightCors";

// 1. Add MediatR (In-process Event Bus)
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(TestSubmittedHandler).Assembly); // Registers Grading Handlers
});

// 2. Add MassTransit.
// Local development uses the in-memory transport by default so the API can run without RabbitMQ.
// Set RabbitMQ:Enabled=true in environment/Azure configuration when a real broker is available.
var rabbitMqEnabled = builder.Configuration.GetValue<bool>("RabbitMQ:Enabled");
builder.Services.AddMassTransit(x =>
{
    // Register asynchronous consumers
    x.AddConsumer<TestSubmittedConsumer>();
    x.AddConsumer<GradeCalculatedConsumer>();

    if (rabbitMqEnabled)
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost", "/", h => {
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

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors(CorsPolicyName);
app.UseAuthorization();

app.MapControllers();

// Map SignalR Hub for realtime notifications
// app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
