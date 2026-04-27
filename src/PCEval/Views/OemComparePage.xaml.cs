using PCEval.ViewModels;

namespace PCEval.Views;

public partial class OemComparePage : ContentPage
{
    public OemComparePage(OemCompareViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
