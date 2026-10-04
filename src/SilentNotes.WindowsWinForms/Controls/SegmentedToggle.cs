using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SilentNotes.WindowsWinForms.Controls
{
    /// <summary>
    /// Segmented toggle (active notes / recycle bin): a recessed rounded track with
    /// the selected segment drawn as an inset pill. Custom-painted instead of two
    /// buttons so selection reads as one control; double-buffered, no animations.
    /// Colors come from WinFormsThemeService via the color properties.
    /// </summary>
    public class SegmentedToggle : Control
    {
        private readonly string[] _labels;
        private int _selectedIndex;
        private int _hoverIndex = -1;

        public SegmentedToggle(string[] labels)
        {
            _labels = (string[])labels.Clone();
            // Opaque painting only: transparent BackColor makes WinForms simulate
            // the parent background under every repaint, which defeats double
            // buffering and flickers (same lesson as the notes list). The theme
            // service sets BackColor to the sidebar color instead.
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (value < 0 || value >= _labels.Length || value == _selectedIndex)
                    return;
                _selectedIndex = value;
                Invalidate();
            }
        }

        /// <summary>Raised when the user picks a segment; not raised by SelectedIndex assignment.</summary>
        public event EventHandler SelectedIndexChanged;

        public Color TrackFill { get; set; }
        public Color TrackBorder { get; set; }
        public Color SegmentFill { get; set; }
        public Color SegmentText { get; set; }
        public Color TrackText { get; set; }
        public Color TrackTextHover { get; set; }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int index = HitTest(e.Location);
            if (index != _hoverIndex)
            {
                _hoverIndex = index;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                Invalidate();
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            MouseEventArgs me = e as MouseEventArgs;
            int index = HitTest(me != null ? me.Location : PointToClient(Cursor.Position));
            Pick(index);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right)
                return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left)
                Pick(0);
            else if (e.KeyCode == Keys.Right)
                Pick(_labels.Length - 1);
        }

        private void Pick(int index)
        {
            if (index < 0 || index == _selectedIndex)
                return;
            _selectedIndex = index;
            Invalidate();
            var handler = SelectedIndexChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private int HitTest(Point p)
        {
            if (ClientSize.Width <= 0)
                return -1;
            int index = p.X * _labels.Length / ClientSize.Width;
            if (index < 0 || index >= _labels.Length)
                return -1;
            return index;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
                return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            using (GraphicsPath path = RoundedRect(track, 8))
            {
                using (var brush = new SolidBrush(TrackFill))
                    g.FillPath(brush, path);
                using (var pen = new Pen(TrackBorder))
                    g.DrawPath(pen, path);
            }

            for (int i = 0; i < _labels.Length; i++)
            {
                int x1 = i * ClientSize.Width / _labels.Length;
                int x2 = (i + 1) * ClientSize.Width / _labels.Length;
                Rectangle cell = new Rectangle(x1, 0, x2 - x1, ClientSize.Height);
                bool selected = i == _selectedIndex;
                if (selected)
                {
                    Rectangle pill = new Rectangle(cell.X + 2, 2, cell.Width - 4, cell.Height - 4);
                    using (GraphicsPath pillPath = RoundedRect(pill, 6))
                    using (var brush = new SolidBrush(SegmentFill))
                        g.FillPath(brush, pillPath);
                }
                Color textColor = selected ? SegmentText : (i == _hoverIndex ? TrackTextHover : TrackText);
                TextRenderer.DrawText(g, _labels[i], Font, cell, textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
