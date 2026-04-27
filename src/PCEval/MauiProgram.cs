using Microsoft.Extensions.Logging;
using PCEval.Services;
using PCEval.ViewModels;
using PCEval.Views;

namespace PCEval;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Services
        builder.Services.AddSingleton<IDisplayService, DisplayService>();
        builder.Services.AddSingleton<IProcessorService, ProcessorService>();
        builder.Services.AddSingleton<ISystemInfoService, SystemInfoService>();

        // ViewModels
        builder.Services.AddTransient<DisplayViewModel>();
        builder.Services.AddTransient<ProcessorViewModel>();
        builder.Services.AddTransient<RetinaCheckerViewModel>();
        builder.Services.AddTransient<MacLineupViewModel>();

        // Views / Pages
        builder.Services.AddTransient<DisplayPage>();
        builder.Services.AddTransient<ProcessorPage>();
        builder.Services.AddTransient<RetinaCheckerPage>();
        builder.Services.AddTransient<MacLineupPage>();

        // Shell and App (must be registered after pages so DI can construct them)
        builder.Services.AddTransient<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
