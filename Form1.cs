using System;
using System.Drawing;
using System.Windows.Forms;
using TeklaUniversalUdaController;

namespace teklaUDA4._8Win
{
    public partial class Form1 : Form
    {
        private TeklaController _controller = new TeklaController();
        private Timer _uiRefreshTimer;

        private int _autoConnectTickCounter = 0;
        private const int AutoConnectEveryNTicks = 10;

        private Label lblProjectName;
        private Label lblAttribute;
        private TextBox txtUdaName;
        private Label lblValue;
        private TextBox txtUdaValue;
        private Button btnSetUda;

        public Form1()
        {
            InitializeComponentManual();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Console.WriteLine("Initializing...");
            _controller.Initialize();
            TryAutoConnect();

            _uiRefreshTimer = new Timer();
            _uiRefreshTimer.Interval = 150;
            _uiRefreshTimer.Tick += OnUiRefreshTick;
            _uiRefreshTimer.Start();
        }

        private void TryAutoConnect()
        {
            if (string.IsNullOrEmpty(_controller.TeklaBinPath))
                _controller.DetectTekla();

            if (!_controller.IsTeklaLinked && !string.IsNullOrEmpty(_controller.TeklaBinPath))
                _controller.ConnectToTekla();
        }

        private void OnUiRefreshTick(object sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new EventHandler(OnUiRefreshTick), sender, e);
                return;
            }

            if (!_controller.IsTeklaLinked)
            {
                _autoConnectTickCounter++;
                if (_autoConnectTickCounter >= AutoConnectEveryNTicks)
                {
                    _autoConnectTickCounter = 0;
                    TryAutoConnect();
                }
            }

            if (_controller.IsTeklaLinked)
            {
                lblProjectName.Text = string.IsNullOrEmpty(_controller.ActiveProject)
                    ? "No active project."
                    : _controller.ActiveProject;
            }
            else if (!string.IsNullOrEmpty(_controller.TeklaBinPath))
            {
                lblProjectName.Text = "Opening model...";
            }
            else
            {
                lblProjectName.Text = "Waiting for Tekla Structures...";
            }

            bool hasActiveSelection = _controller.IsTeklaLinked && _controller.CurrentSelectedObject != null;
            btnSetUda.Enabled = hasActiveSelection;
        }

        private void btnSetUda_Click(object sender, EventArgs e)
        {
            string udaKey = txtUdaName.Text.Trim();
            string udaVal = txtUdaValue.Text.Trim();
            _controller.CommitUDA(udaKey, udaVal);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _uiRefreshTimer?.Stop();

            if (_controller.EventsInstance != null)
            {
                try
                {
                    _controller.EventsInstance.GetType().GetMethod("UnRegister").Invoke(_controller.EventsInstance, null);
                }
                catch { }
            }
        }

        private void InitializeComponentManual()
        {
            this.Size = new Size(380, 220);
            this.Text = "UDA Assigner TS2022";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.BackColor = SystemColors.Control;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;

            int labelX = 12;
            int fieldX = 90;
            int fieldW = 255;
            int row1 = 14;
            int row2 = 48;
            int row3 = 80;
            int row4 = 118;

            lblProjectName = new Label
            {
                Location = new Point(labelX, row1),
                Size = new Size(fieldW + fieldX - labelX, 22),
                Text = "Waiting for Tekla Structures...",
                ForeColor = SystemColors.ControlText,
                AutoEllipsis = true
            };
            this.Controls.Add(lblProjectName);

            Label lblAttributeCaption = new Label
            {
                Location = new Point(labelX, row2 + 3),
                Size = new Size(75, 20),
                Text = "Attribute"
            };
            this.Controls.Add(lblAttributeCaption);

            txtUdaName = new TextBox
            {
                Location = new Point(fieldX, row2),
                Size = new Size(fieldW, 22),
                MaxLength = 127
            };
            this.Controls.Add(txtUdaName);

            Label lblValueCaption = new Label
            {
                Location = new Point(labelX, row3 + 3),
                Size = new Size(75, 20),
                Text = "Value"
            };
            this.Controls.Add(lblValueCaption);

            txtUdaValue = new TextBox
            {
                Location = new Point(fieldX, row3),
                Size = new Size(fieldW, 22),
                MaxLength = 127
            };
            this.Controls.Add(txtUdaValue);

            btnSetUda = new Button
            {
                Location = new Point(fieldX, row4),
                Size = new Size(fieldW, 26),
                Text = "Assign attribute values",
                UseVisualStyleBackColor = true
            };
            btnSetUda.Click += btnSetUda_Click;
            this.Controls.Add(btnSetUda);
        }
    }
}