using System.Drawing;
using System.Windows.Forms;

namespace MetaReceiptsLite
{
    internal sealed class ThemeButton : Button
    {
        private bool _hot;
        private bool _pressed;

        public Color DisabledTextColor { get; set; } = Color.FromArgb(71, 85, 105);
        public Color DisabledBackColor { get; set; } = Color.FromArgb(241, 245, 249);
        public Color ThemeBorderColor { get; set; } = Color.FromArgb(226, 232, 240);

        public ThemeButton()
        {
            FlatStyle = FlatStyle.Flat;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseEnter(System.EventArgs e) { _hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(System.EventArgs e) { _hot = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left && Enabled) _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(System.EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var bounds = ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            Color back = !Enabled ? DisabledBackColor : (_pressed ? FlatAppearance.MouseDownBackColor : (_hot ? FlatAppearance.MouseOverBackColor : BackColor));
            Color fore = Enabled ? ForeColor : DisabledTextColor;
            using (var brush = new SolidBrush(back)) e.Graphics.FillRectangle(brush, bounds);

            if (FlatAppearance.BorderSize > 0)
            {
                var border = bounds;
                border.Width--;
                border.Height--;
                using (var pen = new Pen(ThemeBorderColor)) e.Graphics.DrawRectangle(pen, border);
            }

            TextRenderer.DrawText(e.Graphics, Text, Font, bounds, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues && Enabled)
            {
                var focus = Rectangle.Inflate(bounds, -4, -4);
                ControlPaint.DrawFocusRectangle(e.Graphics, focus, fore, back);
            }
        }
    }
}
