using KeyGate.Client.ViewModels;

namespace KeyGate.Client.Views;

public partial class LockScreenPage : ContentPage
{
    private readonly LockScreenViewModel _viewModel;

    public LockScreenPage(LockScreenViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }

    private void OnKeyEntryTextChanged(object? sender, TextChangedEventArgs e)
    {
        _viewModel.NotifyActivityCommand.Execute(null);
    }
}
