﻿using System.Windows;
using System.Windows.Controls;
using WPFControl.ProgramControl.ViewModels;

namespace WPFControl.ProgramControl.Views
{
    /// <summary>
    /// RunInfoView.xaml 的交互逻辑
    /// </summary>
    public partial class LogInfoView : UserControl
    {
        public LogInfoView()
        {
            InitializeComponent();
            IsVisibleChanged += OnIsVisibleChanged;
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue == false) return;
            if (DataContext is LogInfoViewModel viewModel && viewModel.ItemsSource != null && viewModel.ItemsSource.Count > 0)
            {
                var lastItem = viewModel.ItemsSource[viewModel.ItemsSource.Count - 1];
                View_DataGrid.ScrollIntoView(lastItem);
            }
        }
    }
}