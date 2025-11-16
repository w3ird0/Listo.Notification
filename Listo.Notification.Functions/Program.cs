using Listo.Notification.Application.Interfaces;
using Listo.Notification.Application.Services;
using Listo.Notification.Infrastructure.Data;
using Listo.Notification.Infrastructure.Providers;
using Listo.Notification.Infrastructure.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // Database Context
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("NotificationDb"),
                sqlOptions => sqlOptions.EnableRetryOnFailure()));

        // Repositories
        services.AddScoped<INotificationRepository, NotificationRepository>();
        // TODO: Add TemplateRepository and PreferenceRepository when implemented
        // services.AddScoped<ITemplateRepository, TemplateRepository>();
        // services.AddScoped<IPreferenceRepository, PreferenceRepository>();

        // Application Services
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ITemplateRenderingService, TemplateRenderingService>();

        // HTTP Client Factory for FCM
        services.AddHttpClient("FCM");

        // Notification Providers
        services.Configure<TwilioOptions>(configuration.GetSection("Twilio"));
        services.Configure<SendGridOptions>(configuration.GetSection("SendGrid"));
        services.Configure<FcmOptions>(configuration.GetSection("FCM"));

        services.AddScoped<ISmsProvider, TwilioSmsProvider>();
        services.AddScoped<IEmailProvider, SendGridEmailProvider>();
        services.AddScoped<IPushProvider, FcmPushProvider>();

        // Application Insights is configured via host.json and local.settings.json
    })
    .Build();

host.Run();
