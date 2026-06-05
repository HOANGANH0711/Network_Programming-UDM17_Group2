using System;
using System.Drawing;
using System.Windows.Forms;
using Client.Network;

namespace Client.Forms
{
    public partial class GameForm : Form
    {
        private GameClientService _service;

        public GameForm(GameClientService service)
        {
            InitializeComponent();
            _service = service;
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            LobbyForm lobby = new LobbyForm(_service);

            lobby.Show();

            this.Hide();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            MessageBox.Show(txtMessage.Text);

            txtMessage.Clear();
        }
    }
}