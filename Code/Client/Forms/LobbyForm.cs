using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Client.Network;
using Shared.DTO;

namespace Client.Forms
{
    public partial class LobbyForm : Form
    {
        private GameClientService _service;

        public LobbyForm(GameClientService service)
        {
            InitializeComponent();
            _service = service;

            _service.OnPlayerListReceived += OnPlayerListReceived;
            _service.OnInviteReveived += OnInviteReceived;
            _service.OnGameStarted += OnGameStarted;
        }

        private void OnPlayerListReceived(List<UserDTO> players)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnPlayerListReceived(players)));
                return;
            }
            lstPlayers.Items.Clear();
            foreach (var p in players)
                lstPlayers.Items.Add(p.UserName);
        }

        private void OnInviteReceived(UserDTO inviter)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnInviteReceived(inviter)));
                return;
            }
            var result = MessageBox.Show(
                $"{inviter.UserName} mời bạn chơi. Chấp nhận?",
                "Lời mời",
                MessageBoxButtons.YesNo
            );
            _service.SendInviteResponse(result == DialogResult.Yes);
        }

        private void OnGameStarted(GameDTO game)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnGameStarted(game)));
                return;
            }
            GameForm gameForm = new GameForm(_service);
            gameForm.Show();
            this.Hide();
        }

        private void btnCreateRoom_Click(object sender, EventArgs e)
        {
            GameForm game = new GameForm(_service);
            game.Show();
            this.Hide();
        }

        private void btnHistory_Click(object sender, EventArgs e)
        {
            HistoryForm history = new HistoryForm(_service);
            history.Show();
            this.Hide();
        }

        private void btnJoinRoom_Click(object sender, EventArgs e)
        {
            if (lstPlayers.SelectedItem == null)
            {
                MessageBox.Show("Chọn người chơi muốn mời trước!");
                return;
            }
            _service.SendInvite(lstPlayers.SelectedItem.ToString() ?? "");
        }
    }
}