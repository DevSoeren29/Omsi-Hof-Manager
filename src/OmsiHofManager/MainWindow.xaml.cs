using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OmsiHofManager.Models;
using OmsiHofManager.Services;
using OmsiHofManager.ViewModels;

namespace OmsiHofManager;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;
        if (_settings.WindowMaximized)
            WindowState = WindowState.Maximized;

        _vm = new MainViewModel(_settings);
        DataContext = _vm;
        Loaded += (_, _) => _vm.Initialize();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _settings.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
        }
        _vm.SaveSettings();
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            _vm.ImportPaths(paths);
    }

    private void HofList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && _vm.RemoveHofsCommand.CanExecute(HofList.SelectedItems))
        {
            _vm.RemoveHofsCommand.Execute(HofList.SelectedItems);
            e.Handled = true;
        }
    }

    private void ShowHof_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.DataContext is HofFile hof)
            _vm.ShowHofInExplorer(hof);
    }

    private void OpenBusFolder_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.DataContext is BusFolder bus)
            _vm.OpenBusFolder(bus);
    }
}
