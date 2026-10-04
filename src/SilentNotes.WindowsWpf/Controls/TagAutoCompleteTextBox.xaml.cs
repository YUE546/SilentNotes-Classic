using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SilentNotes.WindowsWpf.Controls
{
    public partial class TagAutoCompleteTextBox : UserControl
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(TagAutoCompleteTextBox),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty SuggestionsProperty =
            DependencyProperty.Register(nameof(Suggestions), typeof(IEnumerable<string>), typeof(TagAutoCompleteTextBox),
                new PropertyMetadata(Enumerable.Empty<string>()));

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(TagAutoCompleteTextBox),
                new PropertyMetadata(false, OnIsReadOnlyChanged));

        public TagAutoCompleteTextBox()
        {
            InitializeComponent();
        }

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public IEnumerable<string> Suggestions
        {
            get { return (IEnumerable<string>)GetValue(SuggestionsProperty); }
            set { SetValue(SuggestionsProperty, value); }
        }

        public bool IsReadOnly
        {
            get { return (bool)GetValue(IsReadOnlyProperty); }
            set { SetValue(IsReadOnlyProperty, value); }
        }

        public event EventHandler TagAdded;

        private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (TagAutoCompleteTextBox)d;
            control.InputBox.IsReadOnly = (bool)e.NewValue;
        }

        public void FocusInput()
        {
            InputBox.Focus();
        }

        private void InputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            UpdateSuggestions();
        }

        private void InputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SuggestionPopup.IsOpen = false;
        }

        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Text = InputBox.Text;
            UpdateSuggestions();
        }

        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab && SuggestionPopup.IsOpen && SuggestionList.Items.Count > 0)
            {
                string selected = SuggestionList.Items[0] as string;
                if (!string.IsNullOrEmpty(selected))
                {
                    InputBox.Text = selected;
                    InputBox.CaretIndex = InputBox.Text.Length;
                }
                SuggestionPopup.IsOpen = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                SuggestionPopup.IsOpen = false;
                Text = InputBox.Text;
                TagAdded?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        }

        private void SuggestionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selected = SuggestionList.SelectedItem as string;
            if (selected != null)
            {
                InputBox.Text = selected;
                InputBox.CaretIndex = InputBox.Text.Length;
                SuggestionPopup.IsOpen = false;
                InputBox.Focus();
            }
        }

        private void UpdateSuggestions()
        {
            if (!InputBox.IsFocused)
            {
                SuggestionPopup.IsOpen = false;
                return;
            }

            string currentTag = (InputBox.Text ?? string.Empty).Trim();
            var allSuggestions = Suggestions ?? Enumerable.Empty<string>();

            List<string> filtered;
            if (string.IsNullOrEmpty(currentTag))
                filtered = allSuggestions.ToList();
            else
                filtered = allSuggestions.Where(s => s.IndexOf(currentTag, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();

            SuggestionList.ItemsSource = filtered;
            SuggestionPopup.IsOpen = filtered.Count > 0;
        }
    }
}
