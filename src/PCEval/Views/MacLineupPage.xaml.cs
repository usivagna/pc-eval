using PCEval.ViewModels;

namespace PCEval.Views;

public partial class MacLineupPage : ContentPage
{
    private readonly MacLineupViewModel _vm;

    public MacLineupPage(MacLineupViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.IsLoading)
            _vm.LoadCommand.Execute(null);
    }
}
