using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MetaReceiptsLite
{
    internal sealed class ThemeButton : Button
    {
        private bool _hot;
        private bool _pressed;

        public Color DisabledTextColor { get; set; } = Color.FromArgb(148, 163, 184);
        public Color DisabledBackColor { get; set; } = Color.FromArgb(241, 245, 249);
        public Color ThemeBorderColor { get; set; } = Color.FromArgb(226, 232, 240);
        public int CornerRadius { get; set; } = 6;
        public bool IsPrimary { get; set; }

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

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color back = !Enabled ? DisabledBackColor : (_pressed ? FlatAppearance.MouseDownBackColor : (_hot ? FlatAppearance.MouseOverBackColor : BackColor));
            Color fore = Enabled ? ForeColor : DisabledTextColor;

            var drawRect = new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1);
            using (var path = CreateRoundedRectanglePath(drawRect, CornerRadius))
            {
                using (var brush = new SolidBrush(back))
                {
                    e.Graphics.FillPath(brush, path);
                }

                if (FlatAppearance.BorderSize > 0 && ThemeBorderColor != Color.Transparent)
                {
                    using (var pen = new Pen(ThemeBorderColor, 1f))
                    {
                        e.Graphics.DrawPath(pen, path);
                    }
                }

                if (Focused && ShowFocusCues && Enabled)
                {
                    var focusRect = Rectangle.Inflate(drawRect, -3, -3);
                    using (var focusPath = CreateRoundedRectanglePath(focusRect, Math.Max(2, CornerRadius - 2)))
                    using (var focusPen = new Pen(fore, 1f) { DashStyle = DashStyle.Dot })
                    {
                        e.Graphics.DrawPath(focusPen, focusPath);
                    }
                }
            }

            TextRenderer.DrawText(e.Graphics, Text, Font, bounds, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            int diameter = radius * 2;
            var arc = new Rectangle(rect.X, rect.Y, diameter, diameter);

            // top left
            path.AddArc(arc, 180, 90);

            // top right
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);

            // bottom right
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // bottom left
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
