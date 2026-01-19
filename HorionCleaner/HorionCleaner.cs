// HorionCleaner.cs  (C# 7.3 / WinForms)
// UI: Dark + Red, rounded panels, RIGHT side inner frame, Accordion (Cleaner + Gaming) CLOSED by default
// Left bottom: ONLY safe toggle + Scan/Clean/Stop
// Bottom panel: Gaming Mode (power plan + safe tweaks) - smaller, no "open settings" buttons
// Features: Scan/Clean safe, DarkProgressBar, Recycle Bin API,
//           Gaming mode (power plan + safe tweaks), Animated toggle + Animated accordion sections,
//           thread-safe logging, C#7.3 compatible.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HorionCleaner
{
    public partial class HorionCleaner : Form
    {
        // ===== Theme =====
        private static readonly Color C_BG0 = Color.FromArgb(0, 0, 0);
        private static readonly Color C_BG1 = Color.FromArgb(12, 12, 12);

        private static readonly Color C_FRAME = Color.FromArgb(20, 20, 20);
        private static readonly Color C_FRAME_BORDER = Color.FromArgb(55, 55, 55);

        private static readonly Color C_PANEL_L = Color.FromArgb(22, 22, 22);
        private static readonly Color C_PANEL_R = Color.FromArgb(28, 28, 28);

        private static readonly Color C_BORDER = Color.FromArgb(70, 70, 70);
        private static readonly Color C_TEXT = Color.FromArgb(235, 235, 245);
        private static readonly Color C_TEXT_DIM = Color.FromArgb(170, 235, 235, 245);

        private static readonly Color C_RED = Color.FromArgb(200, 40, 40);
        private static readonly Color C_RED_DARK = Color.FromArgb(180, 35, 35);

        private const int CORNER_RADIUS = 28;

        private bool _uiReady;

        // ===== Controls =====
        private RoundedPanel _left;
        private RoundedPanel _gamingPanel;
        private RoundedPanel _right;

        private Label _title;
        private Label _subtitle;
        private Label _summary;

        private AccordionSection _secCleaner;
        private AccordionSection _secGaming;

        private CheckedListBox _listCleaner;
        private CheckedListBox _listGaming;

        private ToggleSwitch _toggleSafe;
        private Label _safeLabel;

        private RoundedButton _btnScan;
        private RoundedButton _btnClean;
        private RoundedButton _btnStop;

        // Gaming Mode panel
        private Label _gamingTitle;
        private ComboBox _comboPower;
        private RoundedButton _btnApplyGaming;
        private RoundedButton _btnBalanced;

        private DarkProgressBar _progress;
        private DataGridView _grid;
        private TextBox _log;

        // ===== Runtime =====
        private CancellationTokenSource _cts;
        private List<ScanItem> _currentItems = new List<ScanItem>();

        // ===== Targets =====
        private readonly List<CleanTarget> _cleanTargets = new List<CleanTarget>();

        // ===== Gaming labels (accordion list) =====
        private const string G_PRIO_FN = "Priorità HIGH a Fortnite (se aperto)";
        private const string G_KILL_CHROME = "Chiudi Chrome/Edge";
        private const string G_KILL_DISCORD = "Chiudi Discord";
        private const string G_SHADER = "Pulisci shader cache (Avanzato)";

        public HorionCleaner()
        {
            InitializeComponent();

            // ===== Window =====
            ClientSize = new Size(1100, 620);
            MinimumSize = new Size(1100, 620);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.Black;
            DoubleBuffered = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            ApplyRoundedRegion();

            // Drag form
            MouseDown += delegate (object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    NativeDrag.ReleaseCapture();
                    NativeDrag.SendMessage(Handle, NativeDrag.WM_NCLBUTTONDOWN, NativeDrag.HTCAPTION, 0);
                }
            };

            KeyPreview = true;
            KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) Close();
            };

            BuildCleanerTargets();
            CreateControls();
            LayoutControls();

            _uiReady = true;
            SetIdleState();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRoundedRegion();
            if (!_uiReady) return;
            LayoutControls();
        }

        private void ApplyRoundedRegion()
        {
            if (ClientSize.Width <= 2 || ClientSize.Height <= 2) return;

            Rectangle r = new Rectangle(0, 0, ClientSize.Width + 1, ClientSize.Height + 1);
            using (GraphicsPath path = CreateRoundedRect(r, CORNER_RADIUS))
            {
                if (Region != null) Region.Dispose();
                Region = new Region(path);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var br = new LinearGradientBrush(ClientRectangle, C_BG0, C_BG1, LinearGradientMode.Vertical))
                g.FillRectangle(br, ClientRectangle);
        }

        // ============================================================
        // Build targets
        // ============================================================
        private void BuildCleanerTargets()
        {
            _cleanTargets.Clear();

            _cleanTargets.Add(new CleanTarget(
                "Temp utente (%TEMP%)",
                delegate { return new[] { Path.GetTempPath() }; },
                true, false, CleanKind.FilesAny));

            _cleanTargets.Add(new CleanTarget(
                "Temp Windows (C:\\Windows\\Temp) (Admin)",
                delegate { return new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp") }; },
                false, true, CleanKind.FilesAny));

            _cleanTargets.Add(new CleanTarget(
                "Prefetch (C:\\Windows\\Prefetch) (consigliato OFF)",
                delegate { return new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch") }; },
                false, true, CleanKind.FilesAny));

            _cleanTargets.Add(new CleanTarget(
                "Thumbcache Explorer (thumbcache_*.db)",
                delegate
                {
                    return new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "Microsoft", "Windows", "Explorer")
                    };
                },
                false, false, CleanKind.Thumbcache));

            _cleanTargets.Add(new CleanTarget(
                "Cache Internet (INetCache)",
                delegate
                {
                    return new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "Microsoft", "Windows", "INetCache")
                    };
                },
                false, false, CleanKind.FilesAny));

            _cleanTargets.Add(new CleanTarget(
                "Recent (AppData\\...\\Recent)",
                delegate
                {
                    return new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "Microsoft", "Windows", "Recent")
                    };
                },
                false, false, CleanKind.FilesAny));

            _cleanTargets.Add(new CleanTarget(
                "Spool stampanti (C:\\Windows\\System32\\spool\\PRINTERS) (Admin)",
                delegate
                {
                    return new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                            "System32", "spool", "PRINTERS")
                    };
                },
                false, true, CleanKind.FilesAny));

            _cleanTargets.Add(new CleanTarget(
                "Windows Logs (C:\\Windows\\Logs) (Admin)",
                delegate { return new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs") }; },
                false, true, CleanKind.FilesAny));

            _cleanTargets.Add(new CleanTarget(
                "Svuota Cestino (Recycle Bin) (consigliato OFF)",
                delegate { return new string[0]; },
                false, false, CleanKind.RecycleBin));
        }

        // ============================================================
        // Create UI
        // ============================================================
        private void CreateControls()
        {
            Controls.Clear();

            _left = new RoundedPanel
            {
                BackColor = C_PANEL_L,
                BorderColor = C_BORDER,
                Radius = 22
            };

            _gamingPanel = new RoundedPanel
            {
                BackColor = C_PANEL_L,
                BorderColor = C_BORDER,
                Radius = 22
            };

            _right = new RoundedPanel
            {
                BackColor = C_PANEL_R,
                BorderColor = C_BORDER,
                Radius = 22,
                DrawInnerFrame = true,
                InnerFill = C_FRAME,
                InnerBorder = C_FRAME_BORDER,
                InnerPadding = 12
            };

            _title = new Label
            {
                Text = "HORION CLEANER",
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI Semibold", 22f, FontStyle.Bold, GraphicsUnit.Pixel),
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _subtitle = new Label
            {
                Text = "v0   |   BetaTest Version",
                ForeColor = C_TEXT_DIM,
                Font = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel),
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _listCleaner = new CheckedListBox
            {
                BackColor = Color.FromArgb(18, 18, 18),
                ForeColor = C_TEXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point),
                IntegralHeight = false
            };

            _listGaming = new CheckedListBox
            {
                BackColor = Color.FromArgb(18, 18, 18),
                ForeColor = C_TEXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point),
                IntegralHeight = false
            };

            FillCleanerList();
            FillGamingList();

            _secCleaner = new AccordionSection
            {
                Title = "CLEANER",
                BackColor = Color.Transparent,
                ForeColor = C_TEXT,
                HeaderBack = Color.FromArgb(18, 18, 18),
                HeaderBorder = Color.FromArgb(60, 60, 60),
                Radius = 16
            };
            _secCleaner.Content.Controls.Add(_listCleaner);
            _secCleaner.SetExpanded(false, animate: false); // CLOSED by default

            _secGaming = new AccordionSection
            {
                Title = "GAMING (safe)",
                BackColor = Color.Transparent,
                ForeColor = C_TEXT,
                HeaderBack = Color.FromArgb(18, 18, 18),
                HeaderBorder = Color.FromArgb(60, 60, 60),
                Radius = 16
            };
            _secGaming.Content.Controls.Add(_listGaming);
            _secGaming.SetExpanded(false, animate: false); // CLOSED by default

            _toggleSafe = new ToggleSwitch { OnColor = C_RED, OffColor = Color.FromArgb(70, 70, 70) };
            _toggleSafe.SetChecked(true, false);

            _safeLabel = new Label
            {
                Text = "Modalità sicura (salta file recenti / in uso)",
                ForeColor = C_TEXT_DIM,
                Font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Pixel),
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _btnScan = new RoundedButton
            {
                Text = "ANALIZZA",
                BackColor = C_RED_DARK,
                ForeColor = Color.White,
                Radius = 20
            };
            _btnScan.Click += async delegate { await RunCleanerAsync(false); };

            _btnClean = new RoundedButton
            {
                Text = "PULISCI",
                BackColor = C_RED,
                ForeColor = Color.White,
                Radius = 20
            };
            _btnClean.Click += async delegate { await RunCleanerAsync(true); };

            _btnStop = new RoundedButton
            {
                Text = "STOP",
                BackColor = Color.FromArgb(90, 90, 90),
                ForeColor = Color.White,
                Radius = 20
            };
            _btnStop.Click += delegate { if (_cts != null) _cts.Cancel(); };

            // Gaming Mode panel (smaller)
            _gamingTitle = new Label
            {
                Text = "GAMING MODE (power plan + azioni safe)",
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold, GraphicsUnit.Pixel),
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _comboPower = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(18, 18, 18),
                ForeColor = C_TEXT,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f)
            };
            _comboPower.Items.Add("Non cambiare Power Plan");
            _comboPower.Items.Add("Bilanciato");
            _comboPower.Items.Add("Prestazioni elevate");
            _comboPower.Items.Add("Ultimate Performance (se disponibile)");
            _comboPower.SelectedIndex = 0;

            _btnApplyGaming = new RoundedButton
            {
                Text = "APPLICA GAMING MODE",
                BackColor = C_RED,
                ForeColor = Color.White,
                Radius = 20
            };
            _btnApplyGaming.Click += async delegate { await ApplyGamingModeAsync(); };

            _btnBalanced = new RoundedButton
            {
                Text = "RIPRISTINA BILANCIATO",
                BackColor = Color.FromArgb(90, 90, 90),
                ForeColor = Color.White,
                Radius = 20
            };
            _btnBalanced.Click += delegate { SetPowerPlanBalanced(); };

            _progress = new DarkProgressBar
            {
                TrackColor = Color.FromArgb(22, 22, 22),
                TrackBorder = Color.FromArgb(60, 60, 60),
                FillColor = C_RED,
                Radius = 10
            };

            _grid = BuildGrid();

            _log = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.FromArgb(14, 14, 14),
                ForeColor = C_TEXT_DIM,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10f)
            };

            // Add controls to panels
            _left.Controls.AddRange(new Control[]
            {
                _title, _subtitle,
                _secCleaner, _secGaming,
                _toggleSafe, _safeLabel,
                _btnScan, _btnClean, _btnStop,
                _summary
            });

            _gamingPanel.Controls.AddRange(new Control[]
            {
                _gamingTitle, _comboPower,
                _btnApplyGaming, _btnBalanced
            });

            _right.Controls.AddRange(new Control[] { _progress, _grid, _log });

            Controls.Add(_left);
            Controls.Add(_gamingPanel);
            Controls.Add(_right);
        }

        private void FillCleanerList()
        {
            _listCleaner.Items.Clear();
            for (int i = 0; i < _cleanTargets.Count; i++)
                _listCleaner.Items.Add(_cleanTargets[i].Name, _cleanTargets[i].DefaultChecked);
        }

        private void FillGamingList()
        {
            _listGaming.Items.Clear();
            _listGaming.Items.Add(G_PRIO_FN, false);
            _listGaming.Items.Add(G_KILL_CHROME, false);
            _listGaming.Items.Add(G_KILL_DISCORD, false);
            _listGaming.Items.Add(G_SHADER, false);
        }

        private DataGridView BuildGrid()
        {
            DataGridView grid = new DataGridView
            {
                BackgroundColor = Color.FromArgb(16, 16, 16),
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(40, 40, 40),
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = 30,
                EnableHeadersVisualStyles = false
            };

            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 25, 25);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = C_TEXT;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);

            grid.DefaultCellStyle.BackColor = Color.FromArgb(16, 16, 16);
            grid.DefaultCellStyle.ForeColor = C_TEXT;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 45, 45);
            grid.DefaultCellStyle.SelectionForeColor = C_TEXT;

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColPath", HeaderText = "Percorso" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColSize", HeaderText = "Dimensione", FillWeight = 25 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColStatus", HeaderText = "Stato", FillWeight = 20 });

            return grid;
        }

        // ============================================================
        // Layout
        // ============================================================
        private void LayoutControls()
        {
            if (_left == null || _right == null || _gamingPanel == null) return;

            int pad = 18;
            int leftW = 390;

            // Smaller gaming panel
            int gapPanels = 10;
            int gamingPanelH = 175; // smaller now

            int leftH = ClientSize.Height - (pad + gapPanels + gamingPanelH + pad);
            if (leftH < 340) leftH = 340;

            _left.Bounds = new Rectangle(pad, pad, leftW, leftH);
            _gamingPanel.Bounds = new Rectangle(pad, pad + leftH + gapPanels, leftW, gamingPanelH);

            _right.Bounds = new Rectangle(
                pad + leftW + pad,
                pad,
                ClientSize.Width - (pad * 3 + leftW),
                ClientSize.Height - pad * 2
            );

            // LEFT header
            _title.Location = new Point(22, 18);
            _subtitle.Location = new Point(24, 52);

            // Accordions (closed by default, close together)
            int accX = 22;
            int accW = _left.Width - 44;
            int accY = 88;
            int gap = 6; // closer

            // Reserve space ONLY for toggle + 3 buttons + summary
            int bottomReserve = 170;

            int available = _left.Height - accY - bottomReserve;
            if (available < 120) available = 120;

            // When closed, these heights are just headers (40). We still give a "slot"
            // so that if user expands, it has space without going outside.
            int cleanerSlot = (int)(available * 0.55);
            int gamingSlot = available - cleanerSlot - gap;

            if (cleanerSlot < 60) cleanerSlot = 60;
            if (gamingSlot < 60) gamingSlot = 60;

            _secCleaner.Bounds = new Rectangle(accX, accY, accW, cleanerSlot);
            _secGaming.Bounds = new Rectangle(accX, accY + cleanerSlot + gap, accW, gamingSlot);

            _listCleaner.Dock = DockStyle.Fill;
            _listGaming.Dock = DockStyle.Fill;

            // Bottom: ONLY safe toggle + buttons
            int afterAccY = _secGaming.Bottom + 10;

            _toggleSafe.Bounds = new Rectangle(22, afterAccY, 54, 26);
            _safeLabel.Location = new Point(84, afterAccY + 2);

            int btnY = afterAccY + 40;
            int btnH = 46;
            int btnGap = 12;
            int btnW = (_left.Width - 44 - btnGap) / 2;

            _btnScan.Bounds = new Rectangle(22, btnY, btnW, btnH);
            _btnClean.Bounds = new Rectangle(22 + btnW + btnGap, btnY, btnW, btnH);

            _btnStop.Bounds = new Rectangle(22, btnY + btnH + 12, _left.Width - 44, 44);


            // GAMING PANEL (smaller)
            _gamingTitle.Location = new Point(22, 14);
            _comboPower.Bounds = new Rectangle(22, 40, _gamingPanel.Width - 44, 28);

            _btnApplyGaming.Bounds = new Rectangle(22, 74, _gamingPanel.Width - 44, 46);
            _btnBalanced.Bounds = new Rectangle(22, 126, _gamingPanel.Width - 44, 42);

            // RIGHT layout
            _progress.Bounds = new Rectangle(26, 26, _right.Width - 52, 16);

            int gridTop = 56;
            int logH = 170;

            int gridH = _right.Height - gridTop - logH - 32;
            if (gridH < 120) gridH = 120;

            _grid.Bounds = new Rectangle(26, gridTop, _right.Width - 52, gridH);
            _log.Bounds = new Rectangle(26, _right.Height - logH - 18, _right.Width - 52, logH);

            // force correct height when collapsed (prevents half-open glitch)
            if (!_secCleaner.Expanded) _secCleaner.Height = 40; // HEADER_H
            if (!_secGaming.Expanded) _secGaming.Height = 40; // HEADER_H

        }

        // ============================================================
        // UI States + Logging
        // ============================================================
        private void SetIdleState()
        {
            _btnScan.Enabled = true;
            _btnClean.Enabled = true;
            _btnStop.Enabled = false;

            _progress.StopMarquee();
            _progress.Minimum = 0;
            _progress.Maximum = 1;
            _progress.Value = 0;
        }

        private void SetBusyState()
        {
            _btnScan.Enabled = false;
            _btnClean.Enabled = false;
            _btnStop.Enabled = true;

            _progress.Minimum = 0;
            _progress.Maximum = 1;
            _progress.Value = 0;
            _progress.StartMarquee(28);
        }

        private void Log(string msg)
        {
            if (_log.InvokeRequired)
            {
                _log.BeginInvoke(new Action<string>(Log), msg);
                return;
            }
            _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine);
        }

        private void UpdateSummary(string msg)
        {
            if (_summary == null) return; // ✅ evita crash

            if (_summary.InvokeRequired)
            {
                _summary.BeginInvoke(new Action<string>(UpdateSummary), msg);
                return;
            }

            _summary.Text = msg ?? "";
        }


        private void ClearGrid()
        {
            if (_grid.InvokeRequired)
            {
                _grid.BeginInvoke(new Action(ClearGrid));
                return;
            }
            _grid.Rows.Clear();
        }

        private void AddGridRow(ScanItem item)
        {
            if (_grid.InvokeRequired)
            {
                _grid.BeginInvoke(new Action<ScanItem>(AddGridRow), item);
                return;
            }
            _grid.Rows.Add(item.Path, FormatBytes(item.SizeBytes), item.Status);
        }

        // ============================================================
        // Read checks (accordion lists)
        // ============================================================
        private List<string> GetSelectedCleanerTargets()
        {
            List<string> selected = new List<string>();
            foreach (object o in _listCleaner.CheckedItems)
            {
                string s = (o ?? "").ToString();
                if (string.IsNullOrWhiteSpace(s)) continue;
                selected.Add(s);
            }
            return selected;
        }

        private bool IsGamingChecked(string label)
        {
            for (int i = 0; i < _listGaming.Items.Count; i++)
            {
                if (string.Equals(_listGaming.Items[i].ToString(), label, StringComparison.OrdinalIgnoreCase))
                    return _listGaming.GetItemChecked(i);
            }
            return false;
        }

        // ============================================================
        // Cleaner flow
        // ============================================================
        private async Task RunCleanerAsync(bool clean)
        {
            List<string> selectedCleaner = GetSelectedCleanerTargets();
            if (selectedCleaner.Count == 0)
            {
                MessageBox.Show("Seleziona almeno un elemento nella sezione CLEANER.", "Horion Cleaner",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool safeMode = _toggleSafe.Checked;
            TimeSpan safeAge = safeMode ? TimeSpan.FromHours(12) : TimeSpan.Zero;

            SetBusyState();
            ClearGrid();
            _log.Clear();
            _currentItems.Clear();

            if (_cts != null) _cts.Dispose();
            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            try
            {
                Log(clean ? "Avvio pulizia..." : "Avvio analisi...");
                UpdateSummary("In corso...");

                List<ScanItem> items = await Task.Run(() => Scan(selectedCleaner, safeAge, token), token);
                _currentItems = items;
                long PrintTotalBytes = items.Sum(i => i.SizeBytes);

                string totalFormatted = FormatBytes(PrintTotalBytes);

                Log("Dimensione totale trovata: " + totalFormatted);


                _progress.StopMarquee();
                _progress.Minimum = 0;
                _progress.Maximum = 1;
                _progress.Value = 0;

                long totalBytes = items.Sum(i => i.SizeBytes);
                UpdateSummary("Trovati " + items.Count + " file - " + FormatBytes(totalBytes));

                int showMax = 2500;
                for (int i = 0; i < items.Count && i < showMax; i++)
                    AddGridRow(items[i]);

                if (items.Count > showMax)
                    Log("Nota: mostrati " + showMax + " elementi su " + items.Count + " (per prestazioni UI).");

                if (!clean)
                {
                    Log("Analisi completata.");
                    return;
                }

                _progress.Minimum = 0;
                _progress.Maximum = Math.Max(1, items.Count);
                _progress.Value = 0;

                int done = 0;
                long freed = 0;
                int deleted = 0;
                int skipped = 0;

                foreach (ScanItem it in items)
                {
                    token.ThrowIfCancellationRequested();

                    if (it.Kind == CleanKind.RecycleBin)
                    {
                        done++;
                        _progress.Value = Math.Min(_progress.Maximum, done);
                        continue;
                    }

                    string status;
                    bool ok = TryDelete(it.Path, out status);
                    it.Status = status;

                    if (ok) { freed += it.SizeBytes; deleted++; }
                    else skipped++;

                    done++;
                    if (done % 25 == 0 || done == items.Count)
                    {
                        int v = Math.Min(_progress.Maximum, done);
                        if (_progress.InvokeRequired) _progress.BeginInvoke(new Action<int>(val => _progress.Value = val), v);
                        else _progress.Value = v;
                    }
                }

                if (selectedCleaner.Contains("Svuota Cestino (Recycle Bin) (consigliato OFF)"))
                {
                    Log("Svuoto cestino...");
                    string rb;
                    TryEmptyRecycleBin(out rb);
                    Log(rb);
                }

                UpdateSummary("Pulizia: eliminati " + deleted + " - saltati " + skipped + " - liberati " + FormatBytes(freed));
                Log("Pulizia completata.");
            }
            catch (OperationCanceledException)
            {
                UpdateSummary("Operazione annullata.");
                Log("Operazione annullata dall'utente.");
            }
            catch (Exception ex)
            {
                UpdateSummary("Errore.");
                Log("ERRORE: " + ex.Message);
                MessageBox.Show(ex.ToString(), "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetIdleState();
            }
        }

        private List<ScanItem> Scan(List<string> selected, TimeSpan safeAge, CancellationToken token)
        {
            List<ScanItem> items = new List<ScanItem>();
            bool admin = IsAdmin();
            DateTime minDate = safeAge == TimeSpan.Zero ? DateTime.MaxValue : DateTime.Now - safeAge;

            foreach (CleanTarget t in _cleanTargets)
            {
                token.ThrowIfCancellationRequested();
                if (!selected.Contains(t.Name)) continue;

                if (t.RequiresAdmin && !admin)
                {
                    Log("Skip (non admin): " + t.Name);
                    continue;
                }

                if (t.Kind == CleanKind.RecycleBin)
                {
                    items.Add(new ScanItem { Path = "[RecycleBin]", SizeBytes = 0, Status = "Selected", Kind = CleanKind.RecycleBin });
                    continue;
                }

                foreach (string root in t.GetRoots())
                {
                    token.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(root)) continue;

                    try
                    {
                        if (!Directory.Exists(root))
                        {
                            Log("Non esiste: " + root);
                            continue;
                        }

                        Log("Scansione: " + t.Name + " -> " + root);

                        if (t.Kind == CleanKind.Thumbcache)
                        {
                            foreach (string f in Directory.EnumerateFiles(root, "thumbcache_*.db", SearchOption.TopDirectoryOnly))
                            {
                                token.ThrowIfCancellationRequested();
                                AddFileIfEligible(f, minDate, items, t.Kind);
                            }
                        }
                        else
                        {
                            foreach (string f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                            {
                                token.ThrowIfCancellationRequested();
                                AddFileIfEligible(f, minDate, items, t.Kind);
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Log("Accesso negato: " + root);
                    }
                    catch (Exception ex)
                    {
                        Log("Errore su " + root + ": " + ex.Message);
                    }
                }
            }

            return items;
        }

        private void AddFileIfEligible(string file, DateTime minDate, List<ScanItem> items, CleanKind kind)
        {
            try
            {
                FileInfo fi = new FileInfo(file);

                if (minDate != DateTime.MaxValue && fi.LastWriteTime > minDate) return;

                long size = fi.Length;
                if (size <= 0) return;

                items.Add(new ScanItem { Path = file, SizeBytes = size, Status = "Found", Kind = kind });
            }
            catch { }
        }

        private bool TryDelete(string file, out string status)
        {
            try
            {
                if (!File.Exists(file))
                {
                    status = "Missing";
                    return false;
                }

                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);

                status = "Deleted";
                return true;
            }
            catch (UnauthorizedAccessException) { status = "Denied"; return false; }
            catch (IOException) { status = "Locked"; return false; }
            catch { status = "Error"; return false; }
        }

        private static string FormatBytes(long bytes)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB" };
            double b = bytes;
            int i = 0;
            while (b >= 1024 && i < suf.Length - 1) { b /= 1024; i++; }
            return string.Format("{0:0.##} {1}", b, suf[i]);
        }

        private bool IsAdmin()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = "session",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process proc = Process.Start(psi))
                {
                    proc.WaitForExit(1500);
                    return proc.ExitCode == 0;
                }
            }
            catch { return false; }
        }

        // ===== Recycle bin API =====
        private bool TryEmptyRecycleBin(out string status)
        {
            try
            {
                int res = SHEmptyRecycleBin(Handle, null,
                    SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);

                status = (res == 0) ? "Cestino svuotato." : ("Impossibile svuotare cestino (code " + res + ").");
                return res == 0;
            }
            catch (Exception ex)
            {
                status = "Errore cestino: " + ex.Message;
                return false;
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        // ============================================================
        // Gaming mode
        // ============================================================
        private async Task ApplyGamingModeAsync()
        {
            try
            {
                _btnApplyGaming.Enabled = false;
                Log("Gaming Mode: applicazione...");

                int powerChoice = _comboPower.SelectedIndex;
                bool prioFortnite = IsGamingChecked(G_PRIO_FN);
                bool killChrome = IsGamingChecked(G_KILL_CHROME);
                bool killDiscord = IsGamingChecked(G_KILL_DISCORD);
                bool shader = IsGamingChecked(G_SHADER);

                await Task.Run(() =>
                {
                    switch (powerChoice)
                    {
                        case 1: SetPowerPlanBalanced(); break;
                        case 2: SetPowerPlanHighPerformance(); break;
                        case 3: SetPowerPlanUltimateIfAvailable(); break;
                    }

                    if (killChrome)
                    {
                        TryKillProcessByName("chrome");
                        TryKillProcessByName("msedge");
                    }
                    if (killDiscord)
                        TryKillProcessByName("discord");

                    if (prioFortnite)
                    {
                        bool ok = TrySetPriorityHigh("FortniteClient-Win64-Shipping");
                        Log(ok ? "Priorità HIGH impostata su Fortnite (se aperto)." : "Fortnite non trovato (priorità non impostata).");
                    }

                    if (shader)
                        CleanShaderCachesDefault();
                });

                Log("Gaming Mode: completato.");
                MessageBox.Show("Gaming Mode applicata (azioni safe e reversibili).", "Horion Cleaner",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("Gaming Mode error: " + ex.Message);
                MessageBox.Show(ex.ToString(), "Errore Gaming Mode", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnApplyGaming.Enabled = true;
            }
        }

        private void TryKillProcessByName(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                string n = name.Trim();

                Process[] ps = Process.GetProcessesByName(n);
                foreach (Process p in ps)
                {
                    try
                    {
                        if (p.SessionId == 0) continue;
                        p.Kill();
                        Log("Chiuso: " + n);
                    }
                    catch { Log("Non posso chiudere: " + n); }
                }
            }
            catch { }
        }

        private bool TrySetPriorityHigh(string processName)
        {
            try
            {
                Process[] ps = Process.GetProcessesByName(processName);
                if (ps.Length == 0) return false;

                foreach (Process p in ps)
                {
                    try { p.PriorityClass = ProcessPriorityClass.High; } catch { }
                }
                return true;
            }
            catch { return false; }
        }

        // ============================================================
        // Power plans
        // ============================================================
        private void SetPowerPlanBalanced()
        {
            RunPowerCfg("-setactive 381b4222-f694-41f0-9685-ff5bb260df2e", "Power Plan: Bilanciato");
        }

        private void SetPowerPlanHighPerformance()
        {
            RunPowerCfg("-setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", "Power Plan: Prestazioni elevate");
        }

        private void SetPowerPlanUltimateIfAvailable()
        {
            string ultimate = "e9a42b02-d5df-448d-aa00-03f14749eb61";

            if (!IsPowerSchemePresent(ultimate))
                RunPowerCfg("-duplicatescheme " + ultimate, "Power Plan: tentativo di creare Ultimate Performance");

            if (IsPowerSchemePresent(ultimate))
                RunPowerCfg("-setactive " + ultimate, "Power Plan: Ultimate Performance");
            else
                Log("Ultimate Performance non disponibile (o richiede admin).");
        }

        private bool IsPowerSchemePresent(string guid)
        {
            try
            {
                string outp = RunPowerCfgCapture("-list");
                return outp != null && outp.ToLowerInvariant().Contains(guid.ToLowerInvariant());
            }
            catch { return false; }
        }

        private void RunPowerCfg(string args, string okMsg)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(2500);
                    if (p.ExitCode == 0) Log(okMsg);
                    else Log("powercfg error (" + p.ExitCode + "): " + args);
                }
            }
            catch (Exception ex)
            {
                Log("powercfg exception: " + ex.Message);
            }
        }

        private string RunPowerCfgCapture(string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process p = Process.Start(psi))
            {
                string s = p.StandardOutput.ReadToEnd();
                p.WaitForExit(2500);
                return s ?? "";
            }
        }

        // ============================================================
        // Shader cache cleaning
        // ============================================================
        private void CleanShaderCachesDefault()
        {
            try
            {
                CleanDirectorySafe(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA", "DXCache"));
                CleanDirectorySafe(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA", "GLCache"));
                CleanDirectorySafe(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache"));

                Log("Shader cache pulita (se presente).");
            }
            catch (Exception ex)
            {
                Log("Shader cache error: " + ex.Message);
            }
        }

        private void CleanDirectorySafe(string dir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dir)) return;
                if (!Directory.Exists(dir)) return;

                foreach (string f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(f, FileAttributes.Normal);
                        File.Delete(f);
                    }
                    catch { }
                }
            }
            catch { }
        }

        // ============================================================
        // Models
        // ============================================================
        private enum CleanKind { FilesAny, Thumbcache, RecycleBin }

        private sealed class CleanTarget
        {
            public string Name { get; private set; }
            public Func<IEnumerable<string>> GetRoots { get; private set; }
            public bool DefaultChecked { get; private set; }
            public bool RequiresAdmin { get; private set; }
            public CleanKind Kind { get; private set; }

            public CleanTarget(string name, Func<IEnumerable<string>> getRoots, bool def, bool requiresAdmin, CleanKind kind)
            {
                Name = name;
                GetRoots = getRoots;
                DefaultChecked = def;
                RequiresAdmin = requiresAdmin;
                Kind = kind;
            }
        }

        private sealed class ScanItem
        {
            public string Path { get; set; }
            public long SizeBytes { get; set; }
            public string Status { get; set; }
            public CleanKind Kind { get; set; }
        }

        // ============================================================
        // Helpers
        // ============================================================
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

        // ============================================================
        // Custom controls
        // ============================================================
        private sealed class DarkProgressBar : Control
        {
            public int Minimum { get; set; } = 0;

            private int _maximum = 100;
            public int Maximum { get { return _maximum; } set { _maximum = Math.Max(1, value); Invalidate(); } }

            private int _value = 0;
            public int Value { get { return _value; } set { _value = Clamp(value, Minimum, Maximum); Invalidate(); } }

            public bool Marquee { get; private set; }

            public Color TrackColor { get; set; } = Color.FromArgb(28, 28, 28);
            public Color TrackBorder { get; set; } = Color.FromArgb(60, 60, 60);
            public Color FillColor { get; set; } = Color.FromArgb(200, 40, 40);
            public int Radius { get; set; } = 10;

            private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();
            private float _marqueePos = -0.25f;
            private float _marqueeSpeed = 0.015f;

            public DarkProgressBar()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw, true);

                Height = 16;

                _timer.Interval = 15;
                _timer.Tick += (s, e) =>
                {
                    _marqueePos += _marqueeSpeed;
                    if (_marqueePos > 1.25f) _marqueePos = -0.25f;
                    Invalidate();
                };
            }

            public void StartMarquee(int speed = 28)
            {
                Marquee = true;
                _marqueeSpeed = 0.006f + (speed / 1000f);
                _timer.Start();
                Invalidate();
            }

            public void StopMarquee()
            {
                Marquee = false;
                _timer.Stop();
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle r = ClientRectangle;
                r.Inflate(-1, -1);

                using (GraphicsPath path = RoundedRect(r, Radius))
                using (SolidBrush track = new SolidBrush(TrackColor))
                using (Pen border = new Pen(TrackBorder, 1.5f))
                {
                    g.FillPath(track, path);
                    g.DrawPath(border, path);
                }

                Rectangle inner = r;
                inner.Inflate(-2, -2);
                if (inner.Width <= 2 || inner.Height <= 2) return;

                if (Marquee)
                {
                    float bandW = inner.Width * 0.35f;
                    float x = inner.Left + (inner.Width * _marqueePos) - bandW * 0.5f;
                    RectangleF band = new RectangleF(x, inner.Top, bandW, inner.Height);

                    using (GraphicsPath clip = RoundedRect(inner, Math.Max(2, Radius - 2)))
                    {
                        Region old = g.Clip;
                        g.SetClip(clip);

                        using (LinearGradientBrush br = new LinearGradientBrush(
                            band, Color.FromArgb(0, FillColor), Color.FromArgb(220, FillColor),
                            LinearGradientMode.Horizontal))
                        {
                            ColorBlend cb = new ColorBlend();
                            cb.Colors = new[]
                            {
                                Color.FromArgb(0, FillColor),
                                Color.FromArgb(220, FillColor),
                                Color.FromArgb(0, FillColor)
                            };
                            cb.Positions = new[] { 0f, 0.5f, 1f };
                            br.InterpolationColors = cb;

                            g.FillRectangle(br, band);
                        }

                        g.Clip = old;
                    }
                    return;
                }

                float t = (Maximum <= Minimum) ? 0f : (Value - Minimum) / (float)(Maximum - Minimum);
                if (t < 0f) t = 0f;
                if (t > 1f) t = 1f;

                int fillW = (int)(inner.Width * t);
                if (fillW <= 0) return;

                Rectangle fill = new Rectangle(inner.Left, inner.Top, fillW, inner.Height);

                using (GraphicsPath fillPath = RoundedRect(fill, Math.Max(2, Radius - 2)))
                using (SolidBrush brFill = new SolidBrush(FillColor))
                    g.FillPath(brFill, fillPath);
            }

            private static int Clamp(int v, int min, int max)
            {
                if (v < min) return min;
                if (v > max) return max;
                return v;
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
        }

        private class RoundedPanel : Panel
        {
            public int Radius { get; set; }
            public Color BorderColor { get; set; }

            public bool DrawInnerFrame { get; set; }
            public Color InnerFill { get; set; }
            public Color InnerBorder { get; set; }
            public int InnerPadding { get; set; }

            public RoundedPanel()
            {
                DoubleBuffered = true;
                Radius = 18;
                BorderColor = Color.FromArgb(70, 70, 70);

                DrawInnerFrame = false;
                InnerFill = Color.FromArgb(20, 20, 20);
                InnerBorder = Color.FromArgb(55, 55, 55);
                InnerPadding = 10;
            }

            private void UpdateRegion()
            {
                if (Width <= 2 || Height <= 2) return;

                // Se c’è l'inner frame e vuoi clippare sui bordi interni, usa inner
                Rectangle clipRect = ClientRectangle;

                if (DrawInnerFrame)
                {
                    clipRect = ClientRectangle;
                    clipRect.Inflate(-InnerPadding, -InnerPadding);
                    if (clipRect.Width < 2 || clipRect.Height < 2)
                        clipRect = ClientRectangle;
                }

                using (GraphicsPath p = RoundedPath(clipRect, DrawInnerFrame ? Math.Max(10, Radius - 6) : Radius))
                {
                    if (Region != null) Region.Dispose();
                    Region = new Region(p);
                }
            }


            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle r = ClientRectangle;
                r.Inflate(-1, -1);

                using (GraphicsPath path = RoundedPath(r, Radius))
                using (SolidBrush fill = new SolidBrush(BackColor))
                using (Pen border = new Pen(BorderColor, 1.5f))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(border, path);
                }

                if (DrawInnerFrame)
                {
                    Rectangle inner = ClientRectangle;
                    inner.Inflate(-InnerPadding, -InnerPadding);

                    using (GraphicsPath p2 = RoundedPath(inner, Math.Max(10, Radius - 6)))
                    using (SolidBrush f2 = new SolidBrush(InnerFill))
                    using (Pen b2 = new Pen(InnerBorder, 2f))
                    {
                        e.Graphics.FillPath(f2, p2);
                        e.Graphics.DrawPath(b2, p2);
                    }
                }

                base.OnPaint(e);
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                UpdateRegion();
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                UpdateRegion();
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

        private class RoundedButton : Button
        {
            public int Radius { get; set; } = 22;

            public RoundedButton()
            {
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                Cursor = Cursors.Hand;
                Font = new Font("Segoe UI Semibold", 11f);
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

        private class ToggleSwitch : Control
        {
            public bool Checked { get; private set; }
            public Color OnColor { get; set; }
            public Color OffColor { get; set; }

            private readonly System.Windows.Forms.Timer _anim = new System.Windows.Forms.Timer();
            private float _t;
            private float _targetT;

            public event EventHandler CheckedChanged;

            public ToggleSwitch()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer, true);

                Cursor = Cursors.Hand;
                Size = new Size(54, 26);

                OnColor = Color.FromArgb(200, 40, 40);
                OffColor = Color.FromArgb(70, 70, 70);

                _anim.Interval = 15;
                _anim.Tick += (s, e) =>
                {
                    _t = Lerp(_t, _targetT, 0.25f);
                    if (Math.Abs(_t - _targetT) < 0.01f)
                    {
                        _t = _targetT;
                        _anim.Stop();
                    }
                    Invalidate();
                };

                MouseDown += (s, e) =>
                {
                    if (e.Button != MouseButtons.Left) return;
                    SetChecked(!Checked, true);
                };
            }

            public void SetChecked(bool value, bool animate)
            {
                Checked = value;
                _targetT = Checked ? 1f : 0f;

                if (!animate)
                {
                    _t = _targetT;
                    Invalidate();
                }
                else
                {
                    _anim.Start();
                }

                if (CheckedChanged != null)
                    CheckedChanged(this, EventArgs.Empty);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle r = ClientRectangle;
                r.Inflate(-1, -1);

                Color track = Blend(OffColor, OnColor, _t);

                using (GraphicsPath path = RoundedRect(r, r.Height / 2))
                using (SolidBrush br = new SolidBrush(track))
                    e.Graphics.FillPath(br, path);

                int knob = r.Height - 6;
                int minX = r.Left + 3;
                int maxX = r.Right - knob - 3;
                int x = (int)(minX + (maxX - minX) * EaseOut(_t));

                Rectangle k = new Rectangle(x, r.Top + 3, knob, knob);

                using (SolidBrush br = new SolidBrush(Color.White))
                    e.Graphics.FillEllipse(br, k);
            }

            private static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
            private static float EaseOut(float t)
            {
                float p = 1f - t;
                return 1f - p * p * p;
            }

            private static Color Blend(Color a, Color b, float t)
            {
                if (t < 0f) t = 0f; if (t > 1f) t = 1f;
                int r = (int)(a.R + (b.R - a.R) * t);
                int g = (int)(a.G + (b.G - a.G) * t);
                int bl = (int)(a.B + (b.B - a.B) * t);
                return Color.FromArgb(r, g, bl);
            }

            private static GraphicsPath RoundedRect(Rectangle r, int radius)
            {
                int rr = Math.Max(0, radius);
                int d = rr * 2;

                GraphicsPath path = new GraphicsPath();
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }
        }

        private class AccordionSection : Control
        {
            public event EventHandler Toggled;

            public string Title { get; set; } = "Section";
            public bool Expanded { get; private set; }

            public Color HeaderBack { get; set; } = Color.FromArgb(18, 18, 18);
            public Color HeaderBorder { get; set; } = Color.FromArgb(60, 60, 60);
            public int Radius { get; set; } = 16;

            public Panel Content { get; } = new Panel { BackColor = Color.Transparent };

            private readonly System.Windows.Forms.Timer _anim = new System.Windows.Forms.Timer();
            private int _targetHeight;
            private int _currentHeight;
            private int _contentFullHeight;

            private const int HEADER_H = 40;
            private const int PAD = 10;

            public AccordionSection()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw |
                         ControlStyles.SupportsTransparentBackColor, true);

                BackColor = Color.Transparent;

                Content.Visible = true;
                Controls.Add(Content);

                _anim.Interval = 15;
                _anim.Tick += (s, e) =>
                {
                    _currentHeight = (int)Lerp(_currentHeight, _targetHeight, 0.22f);
                    if (Math.Abs(_currentHeight - _targetHeight) <= 1)
                    {
                        _currentHeight = _targetHeight;
                        _anim.Stop();
                    }
                    Height = _currentHeight;
                    Invalidate();
                };

                MouseDown += (s, e) =>
                {
                    if (e.Button != MouseButtons.Left) return;
                    if (e.Y <= HEADER_H) Toggle(true);
                };
            }

            public void SetExpanded(bool expanded, bool animate)
            {
                Expanded = expanded;
                RecomputeHeights();

                _targetHeight = Expanded ? (HEADER_H + PAD + _contentFullHeight + PAD) : HEADER_H;
                if (!animate)
                {
                    _currentHeight = _targetHeight;
                    Height = _currentHeight;
                    LayoutInner();
                    Invalidate();
                }
                else
                {
                    if (_currentHeight <= 0) _currentHeight = Height <= 0 ? HEADER_H : Height;
                    _anim.Start();
                }
            }

            public void Toggle(bool animate)
            {
                SetExpanded(!Expanded, animate);
                if (Toggled != null) Toggled(this, EventArgs.Empty);
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                LayoutInner();
            }

            private void RecomputeHeights()
            {
                _contentFullHeight = 0;
                if (Content.Controls.Count == 0)
                {
                    _contentFullHeight = 80;
                }
                else
                {
                    _contentFullHeight = Math.Max(120, Content.Height);
                }

                if (Expanded && Height > HEADER_H + 30)
                {
                    int derived = Height - HEADER_H - PAD - PAD;
                    if (derived > 80) _contentFullHeight = derived;
                }
            }

            private void LayoutInner()
            {
                int w = Width;
                int h = Height;

                int contentH = Math.Max(0, h - HEADER_H - PAD - PAD);
                Content.Bounds = new Rectangle(PAD, HEADER_H + PAD, Math.Max(10, w - PAD * 2), contentH);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle header = new Rectangle(0, 0, Width, HEADER_H);
                header.Inflate(-1, -1);

                using (GraphicsPath hp = RoundedRect(header, Radius))
                using (SolidBrush hb = new SolidBrush(HeaderBack))
                using (Pen p = new Pen(HeaderBorder, 1.5f))
                {
                    e.Graphics.FillPath(hb, hp);
                    e.Graphics.DrawPath(p, hp);
                }

                string arrow = Expanded ? "▾" : "▸";
                using (Font f = new Font("Segoe UI Semibold", 11f, FontStyle.Bold))
                using (SolidBrush br = new SolidBrush(ForeColor))
                {
                    e.Graphics.DrawString(arrow, f, br, new PointF(12, 10));
                    e.Graphics.DrawString(Title, f, br, new PointF(30, 10));
                }
            }

            private static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

            private static GraphicsPath RoundedRect(Rectangle r, int radius)
            {
                int rr = Math.Max(0, radius);
                int d = rr * 2;

                GraphicsPath path = new GraphicsPath();
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

            [DllImport("user32.dll")]
            public static extern bool ReleaseCapture();

            [DllImport("user32.dll")]
            public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        }
    }
}