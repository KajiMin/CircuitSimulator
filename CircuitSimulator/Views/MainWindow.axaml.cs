using Avalonia.Controls;
using CircuitSimulator.ViewModels;

namespace CircuitSimulator.Views {
    public partial class MainWindow: Window {
        readonly MainWindowViewModel mwvm;

        public MainWindow() {
            InitializeComponent();
            mwvm = new MainWindowViewModel();
            DataContext = mwvm;
            mwvm.AddWindow(this);
        }

        public void DTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e) {
            mwvm.DTapped(sender, e);
        }

        public void Update() => mwvm.Update();
    }
}