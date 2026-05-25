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

        private Label lblStatus;
        private Label lblSeparator;

        private Panel panelDisconnected;
        private Label lblInstructions;
        private Label lblDisconnectedObjectInfo;
        private Button btnConnect;

        private Panel panelConnected;
        private Label lblActiveProjectTitle;
        private Label lblActiveProject;
        private Label lblSelectedObjectTitle;
        private TextBox txtSelectedObjectInfo;
        private Label lblUdaName;
        private TextBox txtUdaName;
        private Label lblUdaValue;
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

            _uiRefreshTimer = new Timer();
            _uiRefreshTimer.Interval = 150;
            _uiRefreshTimer.Tick += OnUiRefreshTick;
            _uiRefreshTimer.Start();
        }

        private void OnUiRefreshTick(object sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new EventHandler(OnUiRefreshTick), sender, e);
                return;
            }

            if (string.IsNullOrEmpty(_controller.TeklaBinPath))
            {
                lblStatus.Text = $"Tekla Version: {_controller.DetectedVersion} | State: Tekla is not running!";
                lblStatus.ForeColor = Color.FromArgb(255, 102, 102);
            }
            else if (_controller.IsTeklaLinked)
            {
                lblStatus.Text = $"Tekla Version: {_controller.DetectedVersion} | State: Connected";
                lblStatus.ForeColor = Color.FromArgb(102, 255, 102);
            }
            else
            {
                lblStatus.Text = $"Tekla Version: {_controller.DetectedVersion} | State: Ready to Connect";
                lblStatus.ForeColor = Color.FromArgb(255, 153, 0);
            }

            string rawObjectInfo = _controller.SelectedObjectInfo ?? "";
            string formattedObjectInfo = rawObjectInfo;

            if (rawObjectInfo.StartsWith("Type: ") && rawObjectInfo.Contains("GUID:"))
            {
                formattedObjectInfo = rawObjectInfo.Replace("GUID:", Environment.NewLine + "GUID: ");
            }

            if (!_controller.IsTeklaLinked)
            {
                panelConnected.Visible = false;
                panelDisconnected.Visible = true;

                bool processMissing = string.IsNullOrEmpty(_controller.TeklaBinPath);
                btnConnect.Enabled = !processMissing;

                lblInstructions.Text = "Make sure Tekla Structures is open with a project loaded, then click below to link the application.";

                if (_controller.SelectedObjectInfo != "No object selected.")
                {
                    lblDisconnectedObjectInfo.Text = formattedObjectInfo;
                    lblDisconnectedObjectInfo.Visible = true;
                }
                else
                {
                    lblDisconnectedObjectInfo.Visible = false;
                }
            }
            else
            {
                panelDisconnected.Visible = false;
                panelConnected.Visible = true;

                lblActiveProject.Text = string.IsNullOrEmpty(_controller.ActiveProject) ? "No active project." : _controller.ActiveProject;

                txtSelectedObjectInfo.Text = formattedObjectInfo;

                bool hasActiveSelection = _controller.CurrentSelectedObject != null;
                btnSetUda.Enabled = hasActiveSelection;
            }
        }
       
        private void btnConnect_Click(object sender, EventArgs e)
        {
            _controller.ConnectToTekla();
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
            this.Size = new Size(720, 490);
            this.Text = "Tekla UDA Controller";
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;

            Font segoeUiFont = new Font("Segoe UI", 12F, FontStyle.Regular);
            this.Font = segoeUiFont;

            lblStatus = new Label { Location = new Point(15, 15), Size = new Size(670, 30), ForeColor = Color.White };
            this.Controls.Add(lblStatus);

            lblSeparator = new Label { Location = new Point(15, 45), Size = new Size(670, 2), BackColor = Color.Gray };
            this.Controls.Add(lblSeparator);

            panelDisconnected = new Panel { Location = new Point(15, 60), Size = new Size(670, 370), Visible = true };

            lblInstructions = new Label { Location = new Point(10, 10), Size = new Size(650, 50), ForeColor = Color.White };
            lblDisconnectedObjectInfo = new Label { Location = new Point(10, 65), Size = new Size(650, 50), ForeColor = Color.FromArgb(255, 102, 102), Visible = false };

            btnConnect = new Button { Location = new Point(10, 130), Size = new Size(650, 50), Text = "Connect to Tekla", BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnConnect.Click += btnConnect_Click;

            panelDisconnected.Controls.Add(lblInstructions);
            panelDisconnected.Controls.Add(lblDisconnectedObjectInfo);
            panelDisconnected.Controls.Add(btnConnect);
            this.Controls.Add(panelDisconnected);


            panelConnected = new Panel { Location = new Point(15, 60), Size = new Size(670, 370), Visible = false };

            lblActiveProjectTitle = new Label { Location = new Point(10, 0), Size = new Size(650, 25), Text = "Active Project:", ForeColor = Color.DarkGray };
            lblActiveProject = new Label { Location = new Point(10, 25), Size = new Size(650, 45), ForeColor = Color.White };

            lblSelectedObjectTitle = new Label { Location = new Point(10, 75), Size = new Size(650, 25), Text = "Selected Tekla Object:", ForeColor = Color.DarkGray };
            txtSelectedObjectInfo = new TextBox { Location = new Point(10, 100), Size = new Size(650, 60), Multiline = true, ReadOnly = true, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            lblUdaName = new Label { Location = new Point(10, 175), Size = new Size(650, 25), Text = "New UDA Name:", ForeColor = Color.White };
            txtUdaName = new TextBox { Location = new Point(10, 200), Size = new Size(650, 30), BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, MaxLength = 127 };

            lblUdaValue = new Label { Location = new Point(10, 240), Size = new Size(650, 25), Text = "UDA Value:", ForeColor = Color.White };
            txtUdaValue = new TextBox { Location = new Point(10, 265), Size = new Size(650, 30), BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, MaxLength = 127 };

            btnSetUda = new Button { Location = new Point(10, 315), Size = new Size(650, 50), Text = "Set UDA to Selected Object", BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnSetUda.Click += btnSetUda_Click;

            panelConnected.Controls.Add(lblActiveProjectTitle);
            panelConnected.Controls.Add(lblActiveProject);
            panelConnected.Controls.Add(lblSelectedObjectTitle);
            panelConnected.Controls.Add(txtSelectedObjectInfo);
            panelConnected.Controls.Add(lblUdaName);
            panelConnected.Controls.Add(txtUdaName);
            panelConnected.Controls.Add(lblUdaValue);
            panelConnected.Controls.Add(txtUdaValue);
            panelConnected.Controls.Add(btnSetUda);
            this.Controls.Add(panelConnected);
        }
    }
}