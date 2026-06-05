using Client.Network;

namespace Client.Forms
{
    public partial class LoginForm : Form
    {
        private readonly GameClientService _service = GameClientService.Instance;

        public LoginForm()
        {
            InitializeComponent();
            BuildUi();
            _service.OnLoginSuccess += OpenLobby;
            _service.OnError += ShowError;
            FormClosed += (_, _) => DetachEvents();
        }

        private void DetachEvents()
        {
            _service.OnLoginSuccess -= OpenLobby;
            _service.OnError -= ShowError;
        }

        private void BuildUi()
        {
            Text = "Dang nhap Caro";
            ClientSize = new Size(460, 420);
            BackColor = Color.FromArgb(18, 24, 33);
            lblTitle.Text = "Caro Online";
            lblTitle.Font = new Font("Segoe UI", 28, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(112, 42);
            lblTitle.AutoSize = true;

            lblName.Text = "Ten nguoi choi";
            lblName.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            lblName.Location = new Point(72, 126);
            textBox1.Name = "txtPlayerName";
            textBox1.Text = Environment.UserName;
            textBox1.Font = new Font("Segoe UI", 13);
            textBox1.Location = new Point(72, 154);
            textBox1.Size = new Size(316, 31);

            lblIP.Text = "IP server";
            lblIP.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            lblIP.ForeColor = Color.White;
            lblIP.Location = new Point(72, 202);
            txtServerIP.Font = new Font("Segoe UI", 13);
            txtServerIP.Location = new Point(72, 230);
            txtServerIP.Size = new Size(316, 31);
            txtServerIP.Text = "127.0.0.1";

            btnConnect.Text = "Ket noi";
            btnConnect.Font = new Font("Segoe UI", 13, FontStyle.Bold);
            btnConnect.BackColor = Color.FromArgb(0, 122, 204);
            btnConnect.ForeColor = Color.White;
            btnConnect.FlatStyle = FlatStyle.Flat;
            btnConnect.Location = new Point(72, 302);
            btnConnect.Size = new Size(316, 48);
        }

        private async void btnConnect_Click(object sender, EventArgs e)
        {
            var name = textBox1.Text.Trim();
            var ip = txtServerIP.Text.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(ip))
            {
                MessageBox.Show("Nhap ten nguoi choi va IP server.");
                return;
            }

            btnConnect.Enabled = false;
            btnConnect.Text = "Dang ket noi...";
            try
            {
                await _service.ConnectAndLoginAsync(ip, 5000, name);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Khong ket noi duoc server: " + ex.Message);
                btnConnect.Enabled = true;
                btnConnect.Text = "Ket noi";
            }
        }

        private void OpenLobby()
        {
            if (InvokeRequired)
            {
                BeginInvoke(OpenLobby);
                return;
            }

            DetachEvents();
            var lobby = new LobbyForm();
            lobby.Show();
            Hide();
        }

        private void ShowError(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ShowError(message));
                return;
            }
            MessageBox.Show(message);
        }

        private void label1_Click(object sender, EventArgs e) { }
        private void txtServerIP_TextChanged(object sender, EventArgs e) { }
    }
}
