using PCEval.ViewModels;

namespace PCEval.Views;

public partial class RetinaCheckerPage : ContentPage
{
    public RetinaCheckerPage(RetinaCheckerViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
