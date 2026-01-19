using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HorionCleaner
{
    public partial class LoginForm : Form
    {
        private RoundedTextBox txtUser;
        private RoundedTextBox txtPass;
        private RoundedButton btnLogin;

        private const int CORNER_RADIUS = 28;

        public event Action LoginSuccess;

        // ===== Credenziali (hardcoded per ora) =====
        private const string VALID_USERNAME = "admin";
        private const string VALID_PASSWORD = "admin";

        public LoginForm()
        {
            InitializeComponent();

            // ===== Window =====
            ClientSize = new Size(900, 500);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.Black;
            DoubleBuffered = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            ApplyRoundedRegion();

            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            // Drag del form (senza bordi)
            MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    NativeDrag.ReleaseCapture();
                    NativeDrag.SendMessage(Handle, NativeDrag.WM_NCLBUTTONDOWN, NativeDrag.HTCAPTION, 0);
                }
            };

            CreateControls();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRoundedRegion();
        }

        private void ApplyRoundedRegion()
        {
            if (ClientSize.Width <= 2 || ClientSize.Height <= 2) return;

            Rectangle r = new Rectangle(0, 0, Width + 1, Height + 1);
            using (GraphicsPath path = RoundedRect(r, CORNER_RADIUS))
            {
                Region?.Dispose();
                Region = new Region(path);
            }
        }

        private void CreateControls()
        {
            int boxWidth = 360;
            int boxHeight = 48;

            int x = (ClientSize.Width - boxWidth) / 2;
            int y = (ClientSize.Height / 2) - 70;

            txtUser = new RoundedTextBox
            {
                Bounds = new Rectangle(x, y, boxWidth, boxHeight),
                Placeholder = "Username",
                UsePasswordMask = false
            };

            txtPass = new RoundedTextBox
            {
                Bounds = new Rectangle(x, y + 62, boxWidth, boxHeight),
                Placeholder = "Password",
                UsePasswordMask = true
            };

            btnLogin = new RoundedButton
            {
                Bounds = new Rectangle(x, y + 135, boxWidth, 50),
                Text = "LOGIN",
                BackColor = Color.FromArgb(200, 40, 40),
                ForeColor = Color.White
            };

            btnLogin.Click += (s, e) =>
            {
                string user = txtUser.Value?.Trim() ?? "";
                string pass = txtPass.Value ?? "";

                if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
                {
                    MessageBox.Show("Inserisci username e password.", "Errore",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (user == VALID_USERNAME && pass == VALID_PASSWORD)
                {
                    // ✅ Non aprire qui il form principale.
                    // Lascia che Program.cs (ApplicationContext) gestisca il flusso.
                    LoginSuccess?.Invoke();
                    return;
                }

                MessageBox.Show("Credenziali non valide.", "Login fallito",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            AcceptButton = btnLogin;

            Controls.Add(txtUser);
            Controls.Add(txtPass);
            Controls.Add(btnLogin);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (LinearGradientBrush br = new LinearGradientBrush(
                ClientRectangle,
                Color.FromArgb(0, 0, 0),
                Color.FromArgb(12, 12, 12),
                LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(br, ClientRectangle);
            }

            using (Font titleFont = new Font("Segoe UI Semibold", 24f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(235, 235, 245)))
            {
                string title = "Login";
                SizeF size = e.Graphics.MeasureString(title, titleFont);

                float x = (ClientSize.Width - size.Width) / 2f;
                float y = ClientSize.Height / 2f - 125f;

                e.Graphics.DrawString(title, titleFont, titleBrush, x, y);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
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

        // ===================== Custom Controls =====================

        private class RoundedTextBox : UserControl
        {
            private readonly TextBox _tb = new TextBox();
            private readonly Label _ph = new Label();

            public string Placeholder
            {
                get => _ph.Text;
                set { _ph.Text = value ?? ""; UpdatePlaceholder(); }
            }

            public bool UsePasswordMask
            {
                get => _tb.UseSystemPasswordChar;
                set => _tb.UseSystemPasswordChar = value;
            }

            public string Value
            {
                get => _tb.Text;
                set { _tb.Text = value ?? ""; UpdatePlaceholder(); }
            }

            private bool _focused;

            public RoundedTextBox()
            {
                DoubleBuffered = true;
                BackColor = Color.Transparent;
                Height = 48;

                _tb.BorderStyle = BorderStyle.None;
                _tb.Font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point);
                _tb.ForeColor = Color.White; // ✅ testo utente bianco
                _tb.BackColor = Color.FromArgb(45, 45, 45);
                _tb.UseSystemPasswordChar = false;
                _tb.Cursor = Cursors.IBeam;

                _ph.AutoSize = false;
                _ph.TextAlign = ContentAlignment.MiddleLeft;
                _ph.Font = _tb.Font;
                _ph.ForeColor = Color.FromArgb(160, 255, 255, 255); // ✅ placeholder grigio chiaro
                _ph.BackColor = Color.Transparent;
                _ph.Cursor = Cursors.IBeam;

                _ph.MouseDown += (s, e) => _tb.Focus();

                Controls.Add(_tb);
                Controls.Add(_ph);

                _tb.GotFocus += (s, e) =>
                {
                    _focused = true;
                    _tb.BackColor = Color.FromArgb(55, 55, 55);
                    UpdatePlaceholder();
                    Invalidate();
                };

                _tb.LostFocus += (s, e) =>
                {
                    _focused = false;
                    _tb.BackColor = Color.FromArgb(45, 45, 45);
                    UpdatePlaceholder();
                    Invalidate();
                };

                _tb.TextChanged += (s, e) =>
                {
                    UpdatePlaceholder();
                    Invalidate();
                };

                Resize += (s, e) => LayoutInner();
                LayoutInner();
                UpdatePlaceholder();
            }

            private void LayoutInner()
            {
                int padX = 16;

                _tb.Width = Math.Max(10, Width - padX * 2);
                _tb.Location = new Point(padX, (Height - _tb.Font.Height) / 2);

                _ph.Bounds = new Rectangle(padX, 0, Math.Max(10, Width - padX * 2), Height);

                _tb.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                _ph.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            }

            private void UpdatePlaceholder()
            {
                bool show = string.IsNullOrEmpty(_tb.Text) && !_focused;
                _ph.Visible = show;
                _ph.BringToFront();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle r = ClientRectangle;
                r.Inflate(-1, -1);

                int radius = 18;

                using (GraphicsPath path = MakeRoundedPath(r, radius))
                using (SolidBrush fill = new SolidBrush(_focused ? Color.FromArgb(55, 55, 55) : Color.FromArgb(45, 45, 45)))
                using (Pen border = new Pen(Color.FromArgb(70, 70, 70), 1.5f))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(border, path);
                }
            }

            private static GraphicsPath MakeRoundedPath(Rectangle r, int radius)
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
        }

        private class RoundedButton : Button
        {
            public int Radius { get; set; } = 22;

            public RoundedButton()
            {
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                Cursor = Cursors.Hand;
                Font = new Font("Segoe UI Semibold", 12f);
                DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs pevent)
            {
                pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle r = ClientRectangle;
                r.Inflate(-1, -1);

                using (GraphicsPath path = RoundedPath(r, Radius))
                using (SolidBrush br = new SolidBrush(BackColor))
                {
                    pevent.Graphics.Clear(Parent.BackColor);
                    pevent.Graphics.FillPath(br, path);

                    TextRenderer.DrawText(
                        pevent.Graphics,
                        Text,
                        Font,
                        ClientRectangle,
                        ForeColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    );
                }
            }

            private static GraphicsPath RoundedPath(Rectangle r, int radius)
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
        }

        private static class NativeDrag
        {
            public const int WM_NCLBUTTONDOWN = 0xA1;
            public const int HTCAPTION = 0x2;

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool ReleaseCapture();

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        }
    }
}