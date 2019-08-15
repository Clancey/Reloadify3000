﻿using Comet.Samples;
using System.Windows;

namespace Comet.WPF.Sample
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
