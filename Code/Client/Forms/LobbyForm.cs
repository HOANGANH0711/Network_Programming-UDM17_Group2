using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Client.Network;
using Shared.DTO;

namespace Client.Forms
{
    public partial class LobbyForm : Form
    {
        private readonly GameClientService service;
        private readonly string playerName;
        private readonly ComboBox cboBotDifficulty = new ComboBox();
        private readonly Button btnPlayBot = new Button();
        private readonly Button btnReturnGame = new Button();
        private readonly Label lblBot = new Label();
        private readonly Label lblMatchSetup = new Label();
        private readonly ComboBox cboTurnTime = new ComboBox();
        private readonly ComboBox cboMyMark = new ComboBox();
        private string currentGameId = string.Empty;

        public LobbyForm() : this(new GameClientService(), string.Empty)
        {
        }

        public LobbyForm(GameClientService service, string playerName)
        {
            this.service = service;
            this.playerName = playerName;

            InitializeComponent();
            ApplyModernStyle();
            Text = string.IsNullOrWhiteSpace(playerName) ? "Lobby" : "Lobby - " + playerName;

            lstPlayers.DisplayMember = nameof(UserDTO.UserName);

            this.service.OnPlayerListReceived += UpdatePlayersSafe;
            this.service.OnInviteReceived += ShowInviteSafe;
            this.service.OnGameStarted += OpenGameSafe;
            this.service.OnGameEnded += ShowGameEndedSafe;
            this.service.OnError += ShowErrorSafe;
            this.service.OnDisconnected += ShowDisconnectedSafe;
            btnPlayBot.Click += btnPlayBot_Click;
            btnReturnGame.Click += btnReturnGame_Click;
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (service.IsConnected)
                await SendSafeAsync(() => service.RequestPlayerListAsync());
        }

        private async void btnCreateRoom_Click(object sender, EventArgs e)
        {
            await SendSafeAsync(() => service.RequestPlayerListAsync());
        }

        private void btnHistory_Click(object sender, EventArgs e)
        {
            HistoryForm history = new HistoryForm(service, this);
            history.Show();
            Hide();
        }

        private async void btnJoinRoom_Click(object sender, EventArgs e)
        {
            if (lstPlayers.SelectedItem is not UserDTO selectedPlayer)
            {
                MessageBox.Show("Hay chon mot nguoi choi online de moi dau.");
                return;
            }

            if (selectedPlayer.UserName == playerName || selectedPlayer.UserID == service.CurrentUserId)
            {
                MessageBox.Show("Khong the moi chinh ban than.");
                return;
            }

            int turnSeconds = GetSelectedTurnSeconds();
            bool inviterPlaysX = (cboMyMark.SelectedItem?.ToString() ?? "X") == "X";

            await SendSafeAsync(() => service.InviteAsync(selectedPlayer, turnSeconds, inviterPlaysX));
            MessageBox.Show("Da gui loi moi den " + selectedPlayer.UserName);
        }

        private async void btnPlayBot_Click(object? sender, EventArgs e)
        {
            string difficulty = cboBotDifficulty.SelectedItem?.ToString() ?? "Easy";
            await SendSafeAsync(() => service.StartBotGameAsync(difficulty));
        }

        private async void btnReturnGame_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(currentGameId))
            {
                MessageBox.Show("Khong co van dau nao dang cho quay lai.");
                return;
            }

            await SendSafeAsync(() => service.ReturnGameAsync(currentGameId));
        }

        private void UpdatePlayersSafe(List<UserDTO> players)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdatePlayersSafe(players)));
                return;
            }

            lstPlayers.Items.Clear();
            foreach (UserDTO player in players)
            {
                if (player.IsOnline && !player.IsInGame)
                    lstPlayers.Items.Add(player);
            }
        }

        private async void ShowInviteSafe(InviteDTO invite)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowInviteSafe(invite)));
                return;
            }

            string inviterMark = invite.InviterPlaysX ? "X" : "O";
            string myMark = invite.InviterPlaysX ? "O" : "X";
            DialogResult result = MessageBox.Show(
                invite.Inviter.UserName + " moi ban dau Caro.\n" +
                "Thoi gian moi luot: " + FormatSeconds(invite.TurnSeconds) + "\n" +
                invite.Inviter.UserName + " cam " + inviterMark + ", ban cam " + myMark + ".\n" +
                "Chap nhan?",
                "Loi moi choi",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            bool accepted = result == DialogResult.Yes;
            await SendSafeAsync(() => service.SendInviteResponseAsync(invite, accepted));
        }

        private void OpenGameSafe(GameDTO game)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OpenGameSafe(game)));
                return;
            }

            OpenGame(game);
        }

        private void OpenGame(GameDTO? game = null)
        {
            if (game != null)
                currentGameId = game.GameID ?? string.Empty;

            btnReturnGame.Visible = !string.IsNullOrWhiteSpace(currentGameId);
            btnPlayBot.Visible = string.IsNullOrWhiteSpace(currentGameId);
            btnReturnGame.BringToFront();
            GameForm gameForm = new GameForm(service, this, game);
            gameForm.Show();
            Hide();
        }

        public void ClearCurrentGame()
        {
            currentGameId = string.Empty;
            btnReturnGame.Visible = false;
            btnPlayBot.Visible = true;
        }

        private void ShowGameEndedSafe(string message)
        {
            if (IsDisposed || !Visible)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowGameEndedSafe(message)));
                return;
            }

            currentGameId = string.Empty;
            btnReturnGame.Visible = false;
            btnPlayBot.Visible = true;
            MessageBox.Show(message, "Ket thuc van dau");
            _ = SendSafeAsync(() => service.RequestPlayerListAsync());
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

        private void ShowDisconnectedSafe()
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(ShowDisconnectedSafe));
                return;
            }

            btnCreateRoom.Enabled = false;
            btnJoinRoom.Enabled = false;
            Text = "Lobby - mat ket noi";
        }

        private async Task SendSafeAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message, "Loi ket noi");
            }
        }

        private void ApplyModernStyle()
        {
            BackColor = Color.FromArgb(18, 22, 31);
            ClientSize = new Size(760, 520);
            Font = new Font("Segoe UI", 10F);

            lblTitle.AutoSize = false;
            lblTitle.Text = "Lobby Online";
            lblTitle.Font = new Font("Segoe UI Semibold", 24F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(36, 26);
            lblTitle.Size = new Size(360, 48);

            lstPlayers.Location = new Point(40, 96);
            lstPlayers.Size = new Size(460, 360);
            lstPlayers.BackColor = Color.FromArgb(25, 31, 43);
            lstPlayers.ForeColor = Color.FromArgb(230, 236, 246);
            lstPlayers.BorderStyle = BorderStyle.FixedSingle;
            lstPlayers.Font = new Font("Segoe UI", 12F);

            btnJoinRoom.Location = new Point(540, 110);
            btnJoinRoom.Size = new Size(170, 42);
            StylePrimaryButton(btnJoinRoom, "Moi dau");

            lblMatchSetup.Text = "Tuy chinh van";
            lblMatchSetup.AutoSize = false;
            lblMatchSetup.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            lblMatchSetup.ForeColor = Color.FromArgb(190, 205, 230);
            lblMatchSetup.Location = new Point(540, 166);
            lblMatchSetup.Size = new Size(170, 24);
            Controls.Add(lblMatchSetup);

            cboTurnTime.DropDownStyle = ComboBoxStyle.DropDownList;
            cboTurnTime.Items.AddRange(new object[] { "15 giay", "30 giay", "1 phut", "2 phut", "5 phut" });
            cboTurnTime.SelectedIndex = 1;
            cboTurnTime.Location = new Point(540, 194);
            cboTurnTime.Size = new Size(170, 30);
            cboTurnTime.BackColor = Color.FromArgb(245, 247, 251);
            cboTurnTime.Font = new Font("Segoe UI", 10F);
            Controls.Add(cboTurnTime);

            cboMyMark.DropDownStyle = ComboBoxStyle.DropDownList;
            cboMyMark.Items.AddRange(new object[] { "X", "O" });
            cboMyMark.SelectedIndex = 0;
            cboMyMark.Location = new Point(540, 234);
            cboMyMark.Size = new Size(170, 30);
            cboMyMark.BackColor = Color.FromArgb(245, 247, 251);
            cboMyMark.Font = new Font("Segoe UI", 10F);
            Controls.Add(cboMyMark);

            btnCreateRoom.Location = new Point(540, 284);
            btnCreateRoom.Size = new Size(170, 42);
            StyleSecondaryButton(btnCreateRoom, "Lam moi");

            btnHistory.Location = new Point(540, 336);
            btnHistory.Size = new Size(170, 42);
            StyleSecondaryButton(btnHistory, "Lich su");

            lblBot.Text = "Solo voi bot";
            lblBot.AutoSize = false;
            lblBot.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            lblBot.ForeColor = Color.FromArgb(190, 205, 230);
            lblBot.Location = new Point(540, 394);
            lblBot.Size = new Size(170, 24);
            Controls.Add(lblBot);

            cboBotDifficulty.DropDownStyle = ComboBoxStyle.DropDownList;
            cboBotDifficulty.Items.AddRange(new object[] { "Easy", "Medium", "Hard" });
            cboBotDifficulty.SelectedIndex = 0;
            cboBotDifficulty.Location = new Point(540, 424);
            cboBotDifficulty.Size = new Size(170, 30);
            cboBotDifficulty.BackColor = Color.FromArgb(245, 247, 251);
            cboBotDifficulty.Font = new Font("Segoe UI", 10F);
            Controls.Add(cboBotDifficulty);

            btnPlayBot.Location = new Point(540, 464);
            btnPlayBot.Size = new Size(170, 42);
            StylePrimaryButton(btnPlayBot, "Dau bot");
            Controls.Add(btnPlayBot);

            btnReturnGame.Location = new Point(540, 464);
            btnReturnGame.Size = new Size(170, 42);
            btnReturnGame.Visible = false;
            StyleSecondaryButton(btnReturnGame, "Quay lai van");
            Controls.Add(btnReturnGame);
            btnReturnGame.BringToFront();
        }

        private int GetSelectedTurnSeconds()
        {
            return cboTurnTime.SelectedIndex switch
            {
                0 => 15,
                1 => 30,
                2 => 60,
                3 => 120,
                4 => 300,
                _ => 30
            };
        }

        private static string FormatSeconds(int seconds)
        {
            if (seconds < 60)
                return seconds + " giay";

            return (seconds / 60) + " phut";
        }

        private static void StylePrimaryButton(Button button, string text)
        {
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(31, 126, 255);
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        }

        private static void StyleSecondaryButton(Button button, string text)
        {
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(72, 84, 104);
            button.BackColor = Color.FromArgb(31, 38, 52);
            button.ForeColor = Color.FromArgb(230, 236, 246);
            button.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        }
    }
}
