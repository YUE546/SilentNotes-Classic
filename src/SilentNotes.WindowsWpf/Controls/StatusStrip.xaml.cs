using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SilentNotes.WindowsWpf.Controls
{
    public partial class StatusStrip : UserControl
    {
        public StatusStrip()
        {
            InitializeComponent();
        }

        public void SetMessage(string message, bool isError = false)
        {
            StatusText.Text = string.Format("{0}  {1:T}", message, DateTime.Now);
            StatusText.Foreground = isError
                ? (Brush)FindResource("SilentNotesDangerBrush")
                : (Brush)FindResource("SilentNotesSecondaryTextBrush");
        }
    }
}
