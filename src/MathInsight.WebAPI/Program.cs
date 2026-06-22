using MassTransit;
using MathInsight.Modules.Identity_Access;
using MathInsight.Modules.Grading_Analytics.Consumers;
using MathInsight.Modules.Recommender.Consumers;
using MathInsight.Modules.Grading_Analytics.Handlers;

var builder = WebApplication.CreateBuilder(args);

// 1. Add MediatR (In-process Event Bus)
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(TestSubmittedHandler).Assembly); // Registers Grading Handlers
});

// 2. Add MassTransit + RabbitMQ (Out-of-process Message Broker)
builder.Services.AddMassTransit(x =>
{
    // Register asynchronous consumers
    x.AddConsumer<TestSubmittedConsumer>();
    x.AddConsumer<GradeCalculatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost", "/", h => {
            h.Username(builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest");
            h.Password(builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest");
        });

        // Auto-configure endpoints based on consumers
        cfg.ConfigureEndpoints(context);
    });
});

// 3. Add Real-time SignalR hubs
builder.Services.AddSignalR();

// 4. Register Domain Modules (Composition Root)
builder.Services.AddIdentityModule(builder.Configuration);
// builder.Services.AddQuestionBankModule(builder.Configuration);
// builder.Services.AddTestingModule(builder.Configuration);
// builder.Services.AddGradingModule(builder.Configuration);
// builder.Services.AddRecommenderModule(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

// Map SignalR Hub for realtime notifications
// app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();