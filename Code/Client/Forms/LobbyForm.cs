using System;
using System.Windows.Forms;
using Client.Network;

namespace Client.Forms
{
    public partial class LobbyForm : Form
    {
        public LobbyForm()
        {
            InitializeComponent();
        }

        private void btnCreateRoom_Click(object sender, EventArgs e)
        {
            GameForm game = new GameForm();
            game.Show();
            this.Hide();
        }

        private void btnHistory_Click(object sender, EventArgs e)
        {
            HistoryForm history = new HistoryForm();
            history.Show();
            this.Hide();
        }

        private void btnJoinRoom_Click(object sender, EventArgs e)
        {
            GameClientService service = new GameClientService();

            service.Connect("127.0.0.1", 8888);

            service.TestSend();

            GameForm game = new GameForm();
            game.Show();
            this.Hide();
        }
    }
}