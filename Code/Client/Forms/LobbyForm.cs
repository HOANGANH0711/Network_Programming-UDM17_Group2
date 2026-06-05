using Client.Network;
using Shared.DTO;
using Shared.Enums;

namespace Client.Forms
{
    public partial class LobbyForm : Form
    {
        private readonly GameClientService _service = GameClientService.Instance;
        private readonly ComboBox _cboTime = new ComboBox();
        private readonly ComboBox _cboSymbol = new ComboBox();
        private readonly ComboBox _cboBot = new ComboBox();
        private readonly Button _btnInvite = new Button();
        private readonly Button _btnBot = new Button();
        private readonly Button _btnReturnGame = new Button();
        private List<UserDTO> _players = new List<UserDTO>();
        private bool _eventsAttached;

        public LobbyForm()
        {
            InitializeComponent();
            BuildUi();
            WireEvents();
            _ = _service.SendAsync(CommandType.GET_PLAYER_LIST, "");
        }

        private void BuildUi()
        {
            ClientSize = new Size(820, 540);
            BackColor = Color.FromArgb(21, 27, 36);
            lblTitle.Text = "Lobby Caro";
            lblTitle.Location = new Point(32, 24);
            lblTitle.ForeColor = Color.White;

            lstPlayers.Location = new Point(32, 86);
            lstPlayers.Size = new Size(410, 360);
            lstPlayers.Font = new Font("Segoe UI", 12);
            lstPlayers.BackColor = Color.FromArgb(246, 248, 251);

            var panel = new Panel { Location = new Point(482, 86), Size = new Size(292, 360), BackColor = Color.FromArgb(31, 39, 51) };
            Controls.Add(panel);

            var lblSetup = new Label { Text = "Tuy chinh van dau", ForeColor = Color.White, Font = new Font("Segoe UI", 14, FontStyle.Bold), Location = new Point(18, 18), AutoSize = true };
            panel.Controls.Add(lblSetup);

            panel.Controls.Add(new Label { Text = "Thoi gian moi luot", ForeColor = Color.White, Location = new Point(20, 68), AutoSize = true });
            _cboTime.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboTime.Items.AddRange(new object[] { "15s", "30s", "1 phut", "2 phut", "5 phut" });
            _cboTime.SelectedIndex = 1;
            _cboTime.Location = new Point(20, 92);
            _cboTime.Size = new Size(250, 30);
            panel.Controls.Add(_cboTime);

            panel.Controls.Add(new Label { Text = "Quan cua ban", ForeColor = Color.White, Location = new Point(20, 136), AutoSize = true });
            _cboSymbol.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboSymbol.Items.AddRange(new object[] { "X", "O" });
            _cboSymbol.SelectedIndex = 0;
            _cboSymbol.Location = new Point(20, 160);
            _cboSymbol.Size = new Size(250, 30);
            panel.Controls.Add(_cboSymbol);

            _btnInvite.Text = "Moi dau";
            _btnInvite.Location = new Point(20, 210);
            _btnInvite.Size = new Size(250, 38);
            StylePrimary(_btnInvite);
            _btnInvite.Click += btnJoinRoom_Click;
            panel.Controls.Add(_btnInvite);

            _cboBot.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboBot.Items.AddRange(new object[] { "Easy", "Medium", "Hard" });
            _cboBot.SelectedIndex = 0;
            _cboBot.Location = new Point(20, 266);
            _cboBot.Size = new Size(120, 30);
            panel.Controls.Add(_cboBot);

            _btnBot.Text = "Solo bot";
            _btnBot.Location = new Point(150, 264);
            _btnBot.Size = new Size(120, 34);
            StylePrimary(_btnBot);
            _btnBot.Click += async (_, _) => await StartBotAsync();
            panel.Controls.Add(_btnBot);

            btnCreateRoom.Visible = false;
            btnJoinRoom.Visible = false;
            btnHistory.Location = new Point(32, 466);
            btnHistory.Size = new Size(130, 36);
            btnHistory.Text = "Lich su";
            btnHistory.Font = new Font("Segoe UI", 10, FontStyle.Bold);

            _btnReturnGame.Text = "Quay lai van dang dau";
            _btnReturnGame.Location = new Point(182, 466);
            _btnReturnGame.Size = new Size(190, 36);
            _btnReturnGame.Visible = _service.CurrentGame is { IsGameOver: false };
            _btnReturnGame.Click += (_, _) => OpenGame();
            StylePrimary(_btnReturnGame);
            Controls.Add(_btnReturnGame);
        }

        private void WireEvents()
        {
            _service.OnPlayerListReceived += RenderPlayers;
            _service.OnInviteReceived += ReceiveInvite;
            _service.OnGameStarted += GameStarted;
            _service.OnGameEnded += GameEnded;
            _eventsAttached = true;
            FormClosed += (_, _) => DetachEvents();
        }

        private void DetachEvents()
        {
            if (!_eventsAttached)
                return;

            _service.OnPlayerListReceived -= RenderPlayers;
            _service.OnInviteReceived -= ReceiveInvite;
            _service.OnGameStarted -= GameStarted;
            _service.OnGameEnded -= GameEnded;
            _eventsAttached = false;
        }

        private void RenderPlayers(List<UserDTO> players)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => RenderPlayers(players));
                return;
            }
            _players = players.Where(p => p.UserID != _service.PlayerId).ToList();
            lstPlayers.Items.Clear();
            foreach (var player in _players)
                lstPlayers.Items.Add($"{player.UserName} - {(player.IsInGame ? "Dang dau" : "San sang")}");
            RefreshReturnButton();
        }

        private async void btnJoinRoom_Click(object? sender, EventArgs e)
        {
            if (lstPlayers.SelectedIndex < 0 || lstPlayers.SelectedIndex >= _players.Count)
            {
                MessageBox.Show("Chon mot nguoi choi online de moi dau.");
                return;
            }

            var target = _players[lstPlayers.SelectedIndex];
            if (target.IsInGame)
            {
                MessageBox.Show("Nguoi choi nay dang trong tran.");
                return;
            }

            await _service.SendAsync(CommandType.INVITE, new InviteDto
            {
                ToPlayerId = target.UserID,
                TurnSeconds = SelectedSeconds(),
                InviterSymbol = _cboSymbol.Text
            });
        }

        private void ReceiveInvite(InviteDto invite)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ReceiveInvite(invite));
                return;
            }
            var text = $"{invite.FromPlayerName} moi dau\nThoi gian: {invite.TurnSeconds}s/luot\nBan cam: {invite.InviteeSymbol}";
            var accepted = MessageBox.Show(text, "Loi moi dau", MessageBoxButtons.YesNo) == DialogResult.Yes;
            _ = _service.SendAsync(CommandType.INVITE_RESPONSE, new InviteResponseDto
            {
                FromPlayerId = invite.FromPlayerId,
                ToPlayerId = invite.ToPlayerId,
                Accepted = accepted
            });
        }

        private void GameStarted(GameStateDto game)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => GameStarted(game));
                return;
            }
            OpenGame();
        }

        private void GameEnded(GameStateDto game)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => GameEnded(game));
                return;
            }
            RefreshReturnButton();
        }

        private async Task StartBotAsync()
        {
            await _service.SendAsync(CommandType.START_BOT_GAME, new BotGameRequestDto
            {
                Difficulty = _cboBot.Text,
                TurnSeconds = SelectedSeconds(),
                PlayerSymbol = _cboSymbol.Text
            });
        }

        private int SelectedSeconds()
        {
            return _cboTime.SelectedIndex switch
            {
                0 => 15,
                1 => 30,
                2 => 60,
                3 => 120,
                4 => 300,
                _ => 30
            };
        }

        private void OpenGame()
        {
            DetachEvents();
            var game = new GameForm();
            game.Show();
            Close();
        }

        private void RefreshReturnButton()
        {
            _btnReturnGame.Visible = _service.CurrentGame is { IsGameOver: false };
        }

        private void btnCreateRoom_Click(object sender, EventArgs e) { }

        private void btnHistory_Click(object sender, EventArgs e)
        {
            DetachEvents();
            var history = new HistoryForm();
            history.Show();
            Close();
        }

        private static void StylePrimary(Button button)
        {
            button.BackColor = Color.FromArgb(0, 122, 204);
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        }
    }
}
