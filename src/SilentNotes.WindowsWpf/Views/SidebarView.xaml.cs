using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SilentNotes.WindowsWpf.Views
{
    public partial class SidebarView : UserControl
    {
        public SidebarView()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler NewNoteClicked;
        public event RoutedEventHandler NewChecklistClicked;
        public event RoutedEventHandler DeleteNoteClicked;
        public event RoutedEventHandler RestoreNoteClicked;
        public event RoutedEventHandler PermanentDeleteNoteClicked;
        public event RoutedEventHandler EmptyRecycleBinClicked;
        public event RoutedEventHandler ActiveNotesClicked;
        public event RoutedEventHandler RecycleBinClicked;
        public event TextChangedEventHandler SearchTextChanged;
        public event EventHandler TagSelectionChanged;
        public event SelectionChangedEventHandler NoteSelectionChanged;

        public string SelectedTag { get; private set; }

        public void SetModeButtons(bool isRecycleBinMode)
        {
            ActiveModeButtons.Visibility = isRecycleBinMode ? Visibility.Collapsed : Visibility.Visible;
            RecycleBinModeButtons.Visibility = isRecycleBinMode ? Visibility.Visible : Visibility.Collapsed;
        }

        public void PopulateTagPanel(List<string> tags, string selectedTag)
        {
            TagPanel.Children.Clear();
            SelectedTag = selectedTag;

            // "All" button with icon
            var allBtn = CreateTagButton("全部", null, selectedTag == null);
            allBtn.Click += TagButton_Click;
            TagPanel.Children.Add(allBtn);

            foreach (string tag in tags)
            {
                var btn = CreateTagButton(tag, tag, string.Equals(tag, selectedTag, StringComparison.InvariantCultureIgnoreCase));
                btn.Click += TagButton_Click;
                TagPanel.Children.Add(btn);
            }
        }

        private Button CreateTagButton(string text, string tag, bool isSelected)
        {
            var btn = new Button
            {
                Content = text,
                Style = (Style)FindResource("TagFilterButtonStyle"),
                Tag = tag,
                FontSize = 11,
                Foreground = isSelected
                    ? (Brush)FindResource("SilentNotesPrimaryBrush")
                    : (Brush)FindResource("SilentNotesTextBrush"),
            };
            if (isSelected)
            {
                btn.BorderBrush = (Brush)FindResource("SilentNotesPrimaryBrush");
                btn.Background = (Brush)FindResource("SilentNotesAccentSoftBrush");
            }
            return btn;
        }

        private void TagButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;

            SelectedTag = btn.Tag as string;

            // Update visual states
            foreach (UIElement child in TagPanel.Children)
            {
                Button b = child as Button;
                if (b != null)
                {
                    bool isSel = b == btn;
                    b.Foreground = isSel
                        ? (Brush)FindResource("SilentNotesPrimaryBrush")
                        : (Brush)FindResource("SilentNotesTextBrush");
                    b.BorderBrush = isSel
                        ? (Brush)FindResource("SilentNotesPrimaryBrush")
                        : (Brush)FindResource("SilentNotesBorderBrush");
                    b.Background = isSel
                        ? (Brush)FindResource("SilentNotesAccentSoftBrush")
                        : (Brush)FindResource("SilentNotesPaperBrush");
                }
            }

            TagSelectionChanged?.Invoke(this, new EventArgs());
        }

        private void NewNoteButton_Click(object sender, RoutedEventArgs e)
        {
            NewNoteClicked?.Invoke(this, e);
        }

        private void NewChecklistButton_Click(object sender, RoutedEventArgs e)
        {
            NewChecklistClicked?.Invoke(this, e);
        }

        private void DeleteNoteButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteNoteClicked?.Invoke(this, e);
        }

        private void RestoreNoteButton_Click(object sender, RoutedEventArgs e)
        {
            RestoreNoteClicked?.Invoke(this, e);
        }

        private void PermanentDeleteNoteButton_Click(object sender, RoutedEventArgs e)
        {
            PermanentDeleteNoteClicked?.Invoke(this, e);
        }

        private void EmptyRecycleBinButton_Click(object sender, RoutedEventArgs e)
        {
            EmptyRecycleBinClicked?.Invoke(this, e);
        }

        private void ActiveNotesButton_Click(object sender, RoutedEventArgs e)
        {
            ActiveNotesClicked?.Invoke(this, e);
        }

        private void RecycleBinButton_Click(object sender, RoutedEventArgs e)
        {
            RecycleBinClicked?.Invoke(this, e);
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchTextChanged?.Invoke(this, e);
        }

        private void NotesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            NoteSelectionChanged?.Invoke(this, e);
        }
    }
}
