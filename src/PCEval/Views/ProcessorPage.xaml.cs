using PCEval.ViewModels;

namespace PCEval.Views;

public partial class ProcessorPage : ContentPage
{
    private readonly ProcessorViewModel _vm;

    public ProcessorPage(ProcessorViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.IsLoading)
            _vm.LoadProcessorCommand.Execute(null);
    }
}
