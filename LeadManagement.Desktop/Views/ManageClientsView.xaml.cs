using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LeadManagement.Desktop.Models;
using LeadManagement.Desktop.ViewModels;

namespace LeadManagement.Desktop.Views
{
    public partial class ManageClientsView : UserControl
    {
        public ManageClientsView()
        {
            InitializeComponent();
        }

        private async void ManageClientsView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ManageClientsViewModel vm)
            {
                await vm.LoadClientsAsync();
            }
        }

        // ----------------------------------------------------------------
        // Drag & Drop — Card-level drop (whole card is a drop target)
        // ----------------------------------------------------------------
        private async void ClientCard_Drop(object sender, DragEventArgs e)
        {
            if (sender is not Border border) return;
            if (border.Tag is not ClientModel client) return;
            if (DataContext is not ManageClientsViewModel vm) return;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files?.Length > 0)
                    await vm.HandleDroppedFileAsync(client, files[0]);
            }
        }

        // ----------------------------------------------------------------
        // Drag & Drop — DropZone panel inside expanded card
        // ----------------------------------------------------------------
        private async void ClientDropZone_Drop(object sender, DragEventArgs e)
        {
            if (sender is not FrameworkElement element) return;
            if (element.Tag is not ClientModel client) return;
            if (DataContext is not ManageClientsViewModel vm) return;

            ResetDropZoneStyle(element as Border);

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files?.Length > 0)
                    await vm.HandleDroppedFileAsync(client, files[0]);
            }
        }

        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is Border border && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x25, 0x63, 0xEB));
                border.Background  = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 0x25, 0x63, 0xEB));
                e.Effects = DragDropEffects.Copy;
            }
        }

        private void DropZone_DragLeave(object sender, DragEventArgs e)
        {
            ResetDropZoneStyle(sender as Border);
        }

        private static void ResetDropZoneStyle(Border? border)
        {
            if (border == null) return;
            border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x41, 0x55));
            border.Background  = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x0F, 0x17, 0x2A));
        }
    }
}

