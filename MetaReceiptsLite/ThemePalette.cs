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
        public Color BorderSubtle { get; private set; }
        public Color Text { get; private set; }
        public Color Muted { get; private set; }
        public Color Subtext { get; private set; }
        public Color Accent { get; private set; }
        public Color AccentHover { get; private set; }
        public Color AccentFore { get; private set; }
        public Color Selection { get; private set; }
        public Color SelectionText { get; private set; }
        public Color DisabledText { get; private set; }
        public Color DisabledSurface { get; private set; }
        public Color BadgeBack { get; private set; }
        public Color BadgeText { get; private set; }

        private ThemePalette(bool dark)
        {
            IsDark = dark;
            if (dark)
            {
                Canvas = Color.FromArgb(15, 23, 42);          // Deep slate #0F172A
                Surface = Color.FromArgb(30, 41, 59);         // Slate #1E293B
                Elevated = Color.FromArgb(51, 65, 85);        // Slate #334155
                Border = Color.FromArgb(71, 85, 105);         // Subtle slate border #475569
                BorderSubtle = Color.FromArgb(51, 65, 85);
                Text = Color.FromArgb(248, 250, 252);         // Off-white #F8FAFC
                Muted = Color.FromArgb(203, 213, 225);        // Soft grey-blue #CBD5E1
                Subtext = Color.FromArgb(148, 163, 184);      // Dim grey #94A3B8
                Accent = Color.FromArgb(16, 185, 129);        // Emerald #10B981
                AccentHover = Color.FromArgb(5, 150, 105);    // Emerald dark #059669
                AccentFore = Color.FromArgb(15, 23, 42);
                Selection = Color.FromArgb(14, 116, 144);     // Deep cyan-blue #0E7490
                SelectionText = Color.White;
                DisabledText = Color.FromArgb(100, 116, 139);
                DisabledSurface = Color.FromArgb(24, 32, 47);
                BadgeBack = Color.FromArgb(22, 101, 52);       // Forest green badge
                BadgeText = Color.FromArgb(187, 247, 208);
            }
            else
            {
                Canvas = Color.FromArgb(248, 250, 252);       // Clean canvas #F8FAFC
                Surface = Color.White;
                Elevated = Color.FromArgb(241, 245, 249);      // #F1F5F9
                Border = Color.FromArgb(226, 232, 240);        // #E2E8F0
                BorderSubtle = Color.FromArgb(241, 245, 249);
                Text = Color.FromArgb(15, 23, 42);            // Slate navy #0F172A
                Muted = Color.FromArgb(71, 85, 105);          // #475569
                Subtext = Color.FromArgb(100, 116, 139);      // #64748B
                Accent = Color.FromArgb(21, 128, 61);         // Forest green #15803D
                AccentHover = Color.FromArgb(16, 105, 50);
                AccentFore = Color.White;
                Selection = Color.FromArgb(224, 242, 254);     // Sky light blue #E0F2FE
                SelectionText = Color.FromArgb(12, 74, 110);   // Deep sky blue #0C4A6E
                DisabledText = Color.FromArgb(148, 163, 184);
                DisabledSurface = Color.FromArgb(241, 245, 249);
                BadgeBack = Color.FromArgb(220, 252, 231);     // Mint badge #DCFCE7
                BadgeText = Color.FromArgb(22, 101, 52);       // Forest green text
            }
        }

        public static ThemePalette Light => new ThemePalette(false);
        public static ThemePalette Dark => new ThemePalette(true);
    }
}
