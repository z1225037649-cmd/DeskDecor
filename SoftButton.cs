using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DeskDecor
{
    internal sealed class SoftButton : Button
    {
        private bool _hovered, _pressed;
        public bool Primary { get; set; }
        public SoftButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
        }
        protected override void OnMouseEnter(System.EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(System.EventArgs e) { _hovered = _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5F, 0.5F, Width - 1, Height - 1);
            using (var path = new GraphicsPath())
            {
                const float d = 14;
                path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                Color fill = Primary ? Color.FromArgb(_pressed ? 17 : _hovered ? 49 : 33, 48, 61)
                    : _pressed ? Color.FromArgb(229, 234, 239) : _hovered ? Color.FromArgb(246, 248, 250) : Color.White;
                using (var brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, path);
                using (var border = new Pen(Primary ? fill : Color.FromArgb(222, 227, 233))) e.Graphics.DrawPath(border, path);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
                !Enabled ? SystemColors.GrayText : Primary ? Color.White : Color.FromArgb(48, 57, 68),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5));
        }
    }
}
