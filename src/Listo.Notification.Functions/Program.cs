using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Application Insights is configured via host.json and local.settings.json
        // TODO: Register repositories, services, and providers
        // services.AddScoped<INotificationRepository, NotificationRepository>();
        // services.AddScoped<INotificationService, NotificationService>();
    })
    .Build();

host.Run();
