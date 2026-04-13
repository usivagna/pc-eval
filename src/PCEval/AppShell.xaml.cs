using PCEval.Views;

namespace PCEval;

public partial class AppShell : Shell
{
    /// <summary>
    /// Pages are resolved via DI and assigned directly so no parameterless
    /// page constructor is required (fixing the ContentTemplate activator issue).
    /// </summary>
    public AppShell(DisplayPage displayPage, ProcessorPage processorPage,
                    RetinaCheckerPage retinaCheckerPage)
    {
        InitializeComponent();
        DisplayContent.Content  = displayPage;
        ProcessorContent.Content = processorPage;
        RetinaContent.Content   = retinaCheckerPage;
    }
}
