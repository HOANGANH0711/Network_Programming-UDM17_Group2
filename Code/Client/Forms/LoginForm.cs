using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Client.Network;

namespace Client.Forms
{
    public partial class LoginForm : Form
    {
        private const int ServerPort = 5000;

        public LoginForm()
        {
            InitializeComponent();
            ApplyModernStyle();
        }

        private void label1_Click(object sender, EventArgs e)
        {
        }

        private void txtServerIP_TextChanged(object sender, EventArgs e)
        {
        }

        private async void btnConnect_Click(object sender, EventArgs e)
        {
            string playerName = textBox1.Text.Trim();
            string serverIp = "127.0.0.1";

            if (string.IsNullOrWhiteSpace(playerName))
            {
                MessageBox.Show("Vui long nhap ten nguoi choi.");
                textBox1.Focus();
                return;
            }

            btnConnect.Enabled = false;
            btnConnect.Text = "DANG KET NOI";

            try
            {
                GameClientService service = new GameClientService();
                service.OnError += ShowErrorSafe;

                await service.ConnectAsync(serverIp, ServerPort);
                await service.LoginAsync(playerName);

                LobbyForm lobby = new LobbyForm(service, playerName);
                lobby.FormClosed += (_, _) => Close();
                lobby.Show();
                Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Khong ket noi duoc server: " + GetMessage(ex));
                btnConnect.Enabled = true;
                btnConnect.Text = "Ket noi";
            }
        }

        private void ShowErrorSafe(string message)
        {
            if (IsDisposed || !Visible)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowErrorSafe(message)));
                return;
            }

            MessageBox.Show(message, "Loi server");
        }

        private static string GetMessage(Exception ex)
        {
            return ex.InnerException?.Message ?? ex.Message;
        }

        private void ApplyModernStyle()
        {
            BackColor = Color.FromArgb(18, 22, 31);
            ClientSize = new Size(420, 330);
            Font = new Font("Segoe UI", 10F);
            ForeColor = Color.FromArgb(230, 236, 246);

            lblTitle.AutoSize = false;
            lblTitle.Text = "UDM17 Caro";
            lblTitle.Font = new Font("Segoe UI Semibold", 26F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblTitle.Location = new Point(28, 28);
            lblTitle.Size = new Size(364, 54);

            lblName.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            lblName.ForeColor = Color.FromArgb(190, 205, 230);
            lblName.Location = new Point(58, 112);
            lblName.Size = new Size(300, 24);
            lblName.Text = "Ten nguoi choi";

            textBox1.Location = new Point(58, 140);
            textBox1.Size = new Size(304, 30);
            textBox1.Font = new Font("Segoe UI", 11F);
            textBox1.BackColor = Color.FromArgb(245, 247, 251);

            lblIP.Visible = false;
            txtServerIP.Visible = false;
            txtServerIP.Text = "127.0.0.1";

            btnConnect.Location = new Point(58, 224);
            btnConnect.Size = new Size(304, 42);
            btnConnect.Text = "Ket noi";
            btnConnect.FlatStyle = FlatStyle.Flat;
            btnConnect.FlatAppearance.BorderSize = 0;
            btnConnect.BackColor = Color.FromArgb(31, 126, 255);
            btnConnect.ForeColor = Color.White;
            btnConnect.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        }
    }
}
