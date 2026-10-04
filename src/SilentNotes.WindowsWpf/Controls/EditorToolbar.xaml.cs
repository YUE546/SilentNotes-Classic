using System.Windows;
using System.Windows.Controls;

namespace SilentNotes.WindowsWpf.Controls
{
    public partial class EditorToolbar : UserControl
    {
        public EditorToolbar()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler UndoClicked;
        public event RoutedEventHandler RedoClicked;
        public event RoutedEventHandler Heading1Clicked;
        public event RoutedEventHandler Heading2Clicked;
        public event RoutedEventHandler Heading3Clicked;
        public event RoutedEventHandler BoldClicked;
        public event RoutedEventHandler ItalicClicked;
        public event RoutedEventHandler UnderlineClicked;
        public event RoutedEventHandler StrikethroughClicked;
        public event RoutedEventHandler BulletsClicked;
        public event RoutedEventHandler NumberingClicked;
        public event RoutedEventHandler BlockquoteClicked;
        public event RoutedEventHandler CodeBlockClicked;
        public event RoutedEventHandler HorizontalRuleClicked;
        public event RoutedEventHandler LinkClicked;
        public event RoutedEventHandler PinnedChanged;

        public CheckBox PinnedCheckBoxControl => PinnedCheckBox;

        private void UndoButton_Click(object sender, RoutedEventArgs e) => UndoClicked?.Invoke(this, e);
        private void RedoButton_Click(object sender, RoutedEventArgs e) => RedoClicked?.Invoke(this, e);
        private void Heading1Button_Click(object sender, RoutedEventArgs e) => Heading1Clicked?.Invoke(this, e);
        private void Heading2Button_Click(object sender, RoutedEventArgs e) => Heading2Clicked?.Invoke(this, e);
        private void Heading3Button_Click(object sender, RoutedEventArgs e) => Heading3Clicked?.Invoke(this, e);
        private void BoldButton_Click(object sender, RoutedEventArgs e) => BoldClicked?.Invoke(this, e);
        private void ItalicButton_Click(object sender, RoutedEventArgs e) => ItalicClicked?.Invoke(this, e);
        private void UnderlineButton_Click(object sender, RoutedEventArgs e) => UnderlineClicked?.Invoke(this, e);
        private void StrikethroughButton_Click(object sender, RoutedEventArgs e) => StrikethroughClicked?.Invoke(this, e);
        private void BulletsButton_Click(object sender, RoutedEventArgs e) => BulletsClicked?.Invoke(this, e);
        private void NumberingButton_Click(object sender, RoutedEventArgs e) => NumberingClicked?.Invoke(this, e);
        private void BlockquoteButton_Click(object sender, RoutedEventArgs e) => BlockquoteClicked?.Invoke(this, e);
        private void CodeBlockButton_Click(object sender, RoutedEventArgs e) => CodeBlockClicked?.Invoke(this, e);
        private void HorizontalRuleButton_Click(object sender, RoutedEventArgs e) => HorizontalRuleClicked?.Invoke(this, e);
        private void LinkButton_Click(object sender, RoutedEventArgs e) => LinkClicked?.Invoke(this, e);
        private void PinnedCheckBox_Changed(object sender, RoutedEventArgs e) => PinnedChanged?.Invoke(this, e);
    }
}
