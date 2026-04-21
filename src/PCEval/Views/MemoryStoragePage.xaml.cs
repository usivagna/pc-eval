using PCEval.ViewModels;

namespace PCEval.Views;

public partial class MemoryStoragePage : ContentPage
{
    private readonly MemoryStorageViewModel _vm;

    public MemoryStoragePage(MemoryStorageViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.IsLoading)
            _vm.LoadMemoryStorageCommand.Execute(null);
    }
}
