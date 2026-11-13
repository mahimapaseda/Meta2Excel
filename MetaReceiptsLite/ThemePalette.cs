using System.Drawing;

namespace MetaReceiptsLite
{
    internal sealed class ThemePalette
    {
        public bool IsDark { get; private set; }
        public Color Canvas { get; private set; }
        public Color Surface { get; private set; }
        public Color Elevated { get; private set; }
        public Color Border { get; private set; }
        public Color Text { get; private set; }
        public Color Muted { get; private set; }
        public Color Accent { get; private set; }
        public Color Selection { get; private set; }
        public Color DisabledText { get; private set; }
        public Color DisabledSurface { get; private set; }

        private ThemePalette(bool dark)
        {
            IsDark = dark;
            Canvas = dark ? Color.FromArgb(11, 15, 23) : Color.FromArgb(248, 250, 252);
            Surface = dark ? Color.FromArgb(17, 24, 39) : Color.White;
            Elevated = dark ? Color.FromArgb(22, 32, 50) : Color.FromArgb(241, 245, 249);
            Border = dark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240);
            Text = dark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);
            Muted = dark ? Color.FromArgb(203, 213, 225) : Color.FromArgb(71, 85, 105);
            Accent = dark ? Color.FromArgb(52, 211, 153) : Color.FromArgb(21, 128, 61);
            Selection = dark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(219, 234, 254);
            DisabledText = dark ? Color.FromArgb(203, 213, 225) : Color.FromArgb(71, 85, 105);
            DisabledSurface = dark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(241, 245, 249);
        }

        public static ThemePalette Light => new ThemePalette(false);
        public static ThemePalette Dark => new ThemePalette(true);
    }
}
