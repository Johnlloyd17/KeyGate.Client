using System.Runtime.InteropServices;
using KeyGate.Client.Services;
using Microsoft.UI.Windowing;

namespace KeyGate.Client.Platforms.Windows;

public class WindowsWindowManager : IWindowManager
{
    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    private readonly SystemTrayService _tray;
    private AppWindow? _appWindow;
    private nint _hwnd;
    private bool _isInTray;

    public bool IsInTray => _isInTray;

    public event Action? OnTrayRestoreRequested;
    public event Action? OnTrayLockAndExitRequested;

    public WindowsWindowManager(SystemTrayService tray)
    {
        _tray = tray;
        IWindowManager.Current = this;
        _tray.OnRestoreClicked += OnTrayRestore;
        _tray.OnLockAndExitClicked += OnTrayLockAndExit;
    }

    public void SetAppWindow(AppWindow appWindow, nint hwnd = 0)
    {
        _appWindow = appWindow;
        _hwnd = hwnd;
    }

    public void MinimizeToTray()
    {
        if (_isInTray) return;

        var hidden = false;

        if (_appWindow is not null)
        {
            try
            {
                _appWindow.Hide();
                hidden = true;
            }
            catch
            {
            }
        }

        if (!hidden && _hwnd != 0)
        {
            try
            {
                ShowWindow(_hwnd, SW_HIDE);
                hidden = true;
            }
            catch
            {
            }
        }

        if (!hidden) return;

        _isInTray = true;
        _tray.Show(tooltip: "KeyGate - Running in background");
    }

    public void RestoreFromTray()
    {
        if (!_isInTray) return;

        _tray.Hide();
        _isInTray = false;

        if (_hwnd != 0)
        {
            try
            {
                ShowWindow(_hwnd, SW_SHOW);
            }
            catch
            {
            }
        }

        if (_appWindow is null) return;

        try
        {
            _appWindow.Show();
            _appWindow.MoveInZOrderAtTop();
        }
        catch
        {
        }

        EnterFullScreen();
    }

    public void EnterFullScreen()
    {
        if (_appWindow is null) return;
        try
        {
            _appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }
        catch
        {
        }
    }

    public void SetOverlapped()
    {
        if (_appWindow is null) return;

        if (_appWindow.Presenter is not OverlappedPresenter)
        {
            _appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        }

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(false, false);
        }
    }

    private void OnTrayRestore()
    {
        OnTrayRestoreRequested?.Invoke();
    }

    private void OnTrayLockAndExit()
    {
        OnTrayLockAndExitRequested?.Invoke();
    }
}
