using System.Maui.Samples;
using System.Windows;

namespace System.Maui.WPF.Sample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            MainFrame.NavigationService.Navigate(new CometPage(MainFrame, new MainPage()));
        }
    }
}
