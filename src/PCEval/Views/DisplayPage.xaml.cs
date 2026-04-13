using PCEval.ViewModels;

namespace PCEval.Views;

public partial class DisplayPage : ContentPage
{
    private readonly DisplayViewModel _vm;

    public DisplayPage(DisplayViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.IsLoading)
            _vm.LoadDisplaysCommand.Execute(null);
    }
}
