using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MetaReceiptsLite
{
    public sealed class DailySpendPoint
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public decimal Cumulative { get; set; }
    }

    public static class DailySpendChart
    {
        public static byte[] RenderPng(string currencyCode, string currencySymbol, IList<DailySpendPoint> points, int width = 900, int height = 360)
        {
            if (width < 200) width = 200;
            if (height < 120) height = 120;
            if (points == null) points = new List<DailySpendPoint>();

            using (var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            using (var g = Graphics.FromImage(bmp))
            using (var bgBrush = new SolidBrush(Color.White))
            using (var axisPen = new Pen(Color.FromArgb(80, 80, 80), 1f))
            using (var gridPen = new Pen(Color.FromArgb(220, 220, 220), 1f))
            using (var barBrush = new SolidBrush(Color.FromArgb(31, 78, 120)))
            using (var linePen = new Pen(Color.FromArgb(192, 80, 77), 2f))
            using (var titleFont = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Bold))
            using (var labelFont = new Font(FontFamily.GenericSansSerif, 8f, FontStyle.Regular))
            using (var textBrush = new SolidBrush(Color.FromArgb(40, 40, 40)) )
            {
                g.SmoothingMode = SmoothingMode.None; // cheaper on old GPUs
                g.Clear(Color.White);
                g.FillRectangle(bgBrush, 0, 0, width, height);

                string title = "Daily Spend (" + (currencyCode ?? "") + ") — billed by payment date";
                g.DrawString(title, titleFont, textBrush, 10, 8);

                int left = 56;
                int right = width - 16;
                int top = 36;
                int bottom = height - 40;
                int plotW = Math.Max(1, right - left);
                int plotH = Math.Max(1, bottom - top);

                g.DrawRectangle(axisPen, left, top, plotW, plotH);

                if (points.Count == 0)
                {
                    g.DrawString("No data", labelFont, textBrush, left + 8, top + 8);
                    return ToPngBytes(bmp);
                }

                decimal maxAmt = points.Max(p => p.Amount);
                decimal maxCum = points.Max(p => p.Cumulative);
                decimal maxY = Math.Max(maxAmt, maxCum);
                if (maxY <= 0) maxY = 1m;

                // Grid + Y labels
                for (int i = 0; i <= 4; i++)
                {
                    float y = top + plotH * (1f - i / 4f);
                    g.DrawLine(gridPen, left, y, right, y);
                    decimal val = maxY * i / 4m;
                    string label = (currencySymbol ?? "") + val.ToString("0.##", CultureInfo.InvariantCulture);
                    g.DrawString(label, labelFont, textBrush, 4, y - 6);
                }

                int n = points.Count;
                float slot = plotW / (float)Math.Max(n, 1);
                float barW = Math.Max(4f, slot * 0.55f);

                var linePts = new PointF[n];
                for (int i = 0; i < n; i++)
                {
                    var p = points[i];
                    float cx = left + slot * i + slot * 0.5f;
                    float barH = (float)((double)(p.Amount / maxY) * plotH);
                    float by = bottom - barH;
                    g.FillRectangle(barBrush, cx - barW / 2f, by, barW, barH);

                    float ly = bottom - (float)((double)(p.Cumulative / maxY) * plotH);
                    linePts[i] = new PointF(cx, ly);

                    string dx = p.Date.ToString("dd MMM", CultureInfo.GetCultureInfo("en-US"));
                    var sz = g.MeasureString(dx, labelFont);
                    g.DrawString(dx, labelFont, textBrush, cx - sz.Width / 2f, bottom + 4);
                }

                if (n == 1)
                {
                    using (var pointBrush = new SolidBrush(Color.FromArgb(192, 80, 77)))
                        g.FillEllipse(pointBrush, linePts[0].X - 3, linePts[0].Y - 3, 6, 6);
                }
                else
                    g.DrawLines(linePen, linePts);

                // Legend
                g.FillRectangle(barBrush, right - 160, 10, 12, 10);
                g.DrawString("Amount", labelFont, textBrush, right - 144, 8);
                g.DrawLine(linePen, right - 80, 15, right - 60, 15);
                g.DrawString("Cumulative", labelFont, textBrush, right - 56, 8);

                return ToPngBytes(bmp);
            }
        }

        private static byte[] ToPngBytes(Bitmap bmp)
        {
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }
}
