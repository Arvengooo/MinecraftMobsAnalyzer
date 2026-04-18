using System.Windows;
using MinecraftMobsAnalyzer.ViewModels;

namespace MinecraftMobsAnalyzer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
