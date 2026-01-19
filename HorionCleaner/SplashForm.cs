using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HorionCleaner
{
    public partial class SplashForm : Form
    {
        public event Action SplashFinished;
        private bool _finished;

        private readonly Timer _timer = new Timer();
        private readonly Stopwatch _sw = new Stopwatch();

        private const float T_BG_FADE = 0.30f;
        private const float T_ICON_ZOOM = 0.45f;
        private const float T_SPLIT = 0.75f;
        private const float T_TEXT_FADE = 0.30f;
        private const float T_HOLD = 0.20f;
        private const float T_FADE_OUT = 0.25f;

        private float Total => T_BG_FADE + T_ICON_ZOOM + T_SPLIT + T_HOLD + T_FADE_OUT;

        private const int CORNER_RADIUS = 28;

        public SplashForm()
        {
            InitializeComponent();

            ClientSize = new Size(900, 500);
            BackColor = Color.Black;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            UpdateRoundedRegion();

            MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                }
            };

            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            _timer.Interval = 16;
            _timer.Tick += (s, e) => Invalidate();

            Shown += (s, e) =>
            {
                _sw.Restart();
                _timer.Start();
            };
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRoundedRegion();
        }

        private void UpdateRoundedRegion()
        {
            if (ClientSize.Width <= 2 || ClientSize.Height <= 2) return;

            // +1 per ridurre artefatti sui bordi
            Rectangle r = new Rectangle(0, 0, ClientSize.Width + 1, ClientSize.Height + 1);
            using (GraphicsPath path = CreateRoundedRect(r, CORNER_RADIUS))
            {
                Region?.Dispose();
                Region = new Region(path);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float t = (float)_sw.Elapsed.TotalSeconds;

            // ✅ Fine intro: emetti evento UNA volta e basta
            if (!_finished && t > Total + 0.05f)
            {
                _finished = true;
                _timer.Stop();
                SplashFinished?.Invoke();
                return;
            }

            float W = ClientSize.Width;
            float H = ClientSize.Height;
            float cx = W * 0.50f;
            float cy = H * 0.50f;

            float pBg = Clamp01(t / T_BG_FADE);
            float bgAlpha = SmoothStep(pBg);

            float pZoom = Clamp01((t - 0.00f) / T_ICON_ZOOM);
            float iconAlpha = SmoothStep(pZoom);
            float iconScale = Lerp(0.18f, 1.00f, EaseOutCubic(pZoom));

            float tSplitStart = T_ICON_ZOOM;
            float pSplit = Clamp01((t - tSplitStart) / T_SPLIT);
            float split = EaseInOutCubic(pSplit);

            float tFadeStart = T_BG_FADE + T_ICON_ZOOM + T_SPLIT + T_HOLD;
            float pFade = Clamp01((t - tFadeStart) / T_FADE_OUT);
            float outAlpha = 1f - SmoothStep(pFade);

            float A_BG = bgAlpha * outAlpha;
            float A_ICON = iconAlpha * outAlpha;

            DrawBackground(g, A_BG);

            float iconBase = Math.Min(W, H) * 0.22f;
            float iconSize = iconBase * iconScale;

            float iconMoveX = 110f * split;
            float textMoveX = -60f * split;

            RectangleF iconRect = new RectangleF(
                (cx + iconMoveX) - iconSize / 2f,
                cy - iconSize / 2f,
                iconSize,
                iconSize
            );

            string top = "HORION";
            string bottom = "CLEANER";

            using (Font fTop = new Font("Segoe UI Semibold", 60f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font fBottom = new Font("Segoe UI", 18f, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                SizeF topSize = g.MeasureString(top, fTop);

                float textStartX = cx - (topSize.Width / 2f);
                float textX = textStartX + textMoveX;
                float textTopY = cy - topSize.Height * 0.52f;

                float pText = Clamp01((t - tSplitStart) / T_TEXT_FADE);
                float textAlpha = A_BG * SmoothStep(pText);

                if (textAlpha > 0f)
                {
                    float w = iconRect.Width;
                    float stroke = Math.Max(2f, w * 0.07f);

                    float iconVisualLeft = iconRect.Left + w * 0.20f - stroke * 0.5f;
                    float revealEdgeX = iconVisualLeft + 2f;

                    Region oldClip = g.Clip;
                    g.SetClip(new RectangleF(0, 0, revealEdgeX, H));
                    DrawVapeLiteText(g, top, bottom, fTop, fBottom, textX, textTopY, textAlpha);
                    g.Clip = oldClip;
                }
            }

            DrawTrashIcon(g, iconRect, ColorFromAlpha(A_ICON, 166, 33, 0));
            DrawIconGlow(g, iconRect, A_ICON);
        }

        private void DrawBackground(Graphics g, float alpha01)
        {
            alpha01 = Clamp01(alpha01);
            int a = (int)(255 * alpha01);

            Rectangle r = ClientRectangle;

            using (LinearGradientBrush br = new LinearGradientBrush(
                r,
                Color.FromArgb(a, 0, 0, 0),
                Color.FromArgb(a, 10, 10, 10),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(br, r);
            }
        }

        private void DrawVapeLiteText(Graphics g, string top, string bottom,
                                     Font fTop, Font fBottom,
                                     float x, float y, float alpha01)
        {
            float a01 = Clamp01(alpha01);
            int a = (int)(255 * a01);

            using (SolidBrush brTop = new SolidBrush(Color.FromArgb(a, 235, 235, 245)))
            using (SolidBrush brBottom = new SolidBrush(Color.FromArgb((int)(200 * a01), 235, 235, 245)))
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb((int)(120 * a01), 0, 0, 0)))
            {
                g.DrawString(top, fTop, shadow, x + 2, y + 2);
                g.DrawString(top, fTop, brTop, x, y);

                SizeF topSize = g.MeasureString(top, fTop);
                SizeF botSize = g.MeasureString(bottom, fBottom);

                float bx = x + (topSize.Width - botSize.Width) / 2f;
                float by = y + topSize.Height * 0.78f;

                g.DrawString(bottom, fBottom, brBottom, bx, by);
            }
        }

        private void DrawTrashIcon(Graphics g, RectangleF bounds, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float w = bounds.Width;
            float h = bounds.Height;

            float stroke = Math.Max(2f, w * 0.07f);
            using (Pen pen = new Pen(color, stroke))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                RectangleF body = new RectangleF(
                    bounds.X + w * 0.28f,
                    bounds.Y + h * 0.32f,
                    w * 0.44f,
                    h * 0.52f
                );

                using (GraphicsPath bodyPath = RoundedRect(body, w * 0.10f))
                    g.DrawPath(pen, bodyPath);

                for (int i = 1; i <= 2; i++)
                {
                    float x = body.Left + body.Width * i / 3f;
                    g.DrawLine(pen, x, body.Top + h * 0.06f, x, body.Bottom - h * 0.06f);
                }

                float lidY = bounds.Y + h * 0.28f;
                g.DrawLine(pen, bounds.X + w * 0.20f, lidY, bounds.X + w * 0.80f, lidY);

                g.DrawLine(pen, bounds.X + w * 0.28f, lidY, bounds.X + w * 0.32f, bounds.Y + h * 0.32f);
                g.DrawLine(pen, bounds.X + w * 0.72f, lidY, bounds.X + w * 0.68f, bounds.Y + h * 0.32f);

                RectangleF handle = new RectangleF(
                    bounds.X + w * 0.40f,
                    bounds.Y + h * 0.17f,
                    w * 0.20f,
                    h * 0.18f
                );
                g.DrawArc(pen, handle, 180, 180);
            }
        }

        private void DrawIconGlow(Graphics g, RectangleF iconRect, float alpha01)
        {
            alpha01 = Clamp01(alpha01);
            if (alpha01 <= 0f) return;

            float baseA = 0.12f * alpha01;

            for (int i = 1; i <= 4; i++)
            {
                float inflate = i * (iconRect.Width * 0.05f);
                RectangleF r = RectangleF.Inflate(iconRect, inflate, inflate);

                Color c = ColorFromAlpha(baseA / i, 166, 33, 0);
                DrawTrashIcon(g, r, c);
            }
        }

        private GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = radius * 2f;
            GraphicsPath path = new GraphicsPath();

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            return path;
        }

        private static GraphicsPath CreateRoundedRect(Rectangle r, int radius)
        {
            int rr = Math.Max(0, radius);
            int maxR = Math.Min(r.Width, r.Height) / 2;
            if (rr > maxR) rr = maxR;

            int d = rr * 2;
            GraphicsPath path = new GraphicsPath();

            if (rr <= 0)
            {
                path.AddRectangle(r);
                path.CloseFigure();
                return path;
            }

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            return path;
        }

        private Color ColorFromAlpha(float a01, int r, int g, int b)
        {
            int a = (int)(255 * Clamp01(a01));
            return Color.FromArgb(a, r, g, b);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        private static float Lerp(float a, float b, float t)
        {
            t = Clamp01(t);
            return a + (b - a) * t;
        }

        private static float SmoothStep(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float EaseOutCubic(float t)
        {
            t = Clamp01(t);
            float p = 1f - t;
            return 1f - p * p * p;
        }

        private static float EaseInOutCubic(float t)
        {
            t = Clamp01(t);
            if (t < 0.5f) return 4f * t * t * t;
            return 1f - (float)Math.Pow(-2f * t + 2f, 3f) / 2f;
        }

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    }
}
