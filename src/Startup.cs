namespace TelegramStickerPorter;

public class Startup : AppStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddLoggingSetup();
        services.AddConsoleFormatter();
        services.AddControllers()
                .AddInjectWithUnifyResult();
        services.AddSchedule(options =>
        {
            options.LogEnabled = true;
            options.AddJob(App.EffectiveTypes.ScanToBuilders());
        });
        services.AddTelegram();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();

        app.UseInject(string.Empty);

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}