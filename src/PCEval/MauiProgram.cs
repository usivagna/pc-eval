using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
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
            })
            .ConfigureLifecycleEvents(events =>
            {
#if WINDOWS
                events.AddWindows(windows => windows.OnWindowCreated(window =>
                {
                    // Apply Mica backdrop on the Activated event so it runs after
                    // the WinUI window content tree is fully set up. Assigning a
                    // SystemBackdrop too early causes a WinUI failfast on some hosts.
                    window.Activated += static (sender, _) =>
                    {
                        if (sender is Microsoft.UI.Xaml.Window w &&
                            w.SystemBackdrop is null &&
                            Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
                        {
                            try
                            {
                                w.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
                                {
                                    Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base
                                };
                            }
                            catch
                            {
                                // Backdrop assignment failed — ignore and keep default.
                            }
                        }
                    };
                }));
#endif
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
