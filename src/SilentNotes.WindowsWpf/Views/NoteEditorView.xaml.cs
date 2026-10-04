using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SilentNotes.WindowsWpf.Views
{
    public partial class NoteEditorView : UserControl
    {
        public NoteEditorView()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler TagsLostFocus;
        public event EventHandler TagsTagAdded;
        public event EventHandler DeleteTagRequested;

        public string TagsText
        {
            get { return TagsTextBox.Text; }
            set { TagsTextBox.Text = value; }
        }

        public bool IsTagsReadOnly
        {
            get { return TagsTextBox.IsReadOnly; }
            set { TagsTextBox.IsReadOnly = value; }
        }

        public bool IsDeleteMode
        {
            get { return DeleteCheckBox.IsChecked == true; }
            set { DeleteCheckBox.IsChecked = value; }
        }

        public void SetTagSuggestions(IEnumerable<string> suggestions)
        {
            TagsTextBox.Suggestions = suggestions;
        }

        public void FocusTagInput()
        {
            TagsTextBox.FocusInput();
        }

        private void TagsTextBox_TagAdded(object sender, EventArgs e)
        {
            if (DeleteCheckBox.IsChecked == true)
                DeleteTagRequested?.Invoke(this, EventArgs.Empty);
            else
                TagsTagAdded?.Invoke(this, e);
        }

        private void TagsTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TagsLostFocus?.Invoke(this, e);
        }

        private void DeleteCheckBox_Changed(object sender, RoutedEventArgs e)
        {
        }
    }
}
