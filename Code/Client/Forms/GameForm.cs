using Client.Network;
using Shared.DTO;
using Shared.Enums;

namespace Client.Forms
{
    public partial class GameForm : Form
    {
        private const int SizeBoard = 15;
        private const int Cell = 36;
        private const int Offset = 34;
        private readonly GameClientService _service = GameClientService.Instance;
        private readonly ListBox _lstMoves = new ListBox();
        private readonly Label _lblInfo = new Label();
        private readonly Label _lblTimer = new Label();
        private readonly Button _btnResign = new Button();
        private readonly Button _btnDraw = new Button();
        private bool _drawPopupOpen;
        private bool _hasNewChat;
        private bool _eventsAttached;
        private bool _endMessageShown;
        private string _lastBoardKey = "";
        private string _lastErrorMessage = "";
        private DateTime _lastErrorAt = DateTime.MinValue;
        private bool _drawRequestPending;
        private bool _returningToLobbyAfterEnd;

        public GameForm()
        {
            InitializeComponent();
            BuildUi();
            WireEvents();
            if (_service.CurrentGame != null)
                RenderGame(_service.CurrentGame);
        }

        private void BuildUi()
        {
            ClientSize = new Size(1120, 720);
            BackColor = Color.FromArgb(20, 26, 35);
            lblTitle.Text = "Caro 15x15";
            lblTitle.Location = new Point(30, 14);
            lblTitle.ForeColor = Color.White;

            var oldBoard = pnlBoard;
            Controls.Remove(oldBoard);
            oldBoard.Dispose();
            pnlBoard = new SmoothBoardPanel();
            pnlBoard.Location = new Point(30, 70);
            pnlBoard.Size = new Size(Offset + Cell * SizeBoard + 10, Offset + Cell * SizeBoard + 10);
            pnlBoard.BackColor = Color.FromArgb(250, 248, 239);
            pnlBoard.Paint += panel1_Paint;
            pnlBoard.MouseClick += BoardClick;
            Controls.Add(pnlBoard);

            _lblInfo.Location = new Point(620, 70);
            _lblInfo.Size = new Size(460, 60);
            _lblInfo.ForeColor = Color.White;
            _lblInfo.Font = new Font("Segoe UI", 13, FontStyle.Bold);
            Controls.Add(_lblInfo);

            _lblTimer.Location = new Point(620, 132);
            _lblTimer.Size = new Size(220, 42);
            _lblTimer.ForeColor = Color.FromArgb(255, 215, 0);
            _lblTimer.Font = new Font("Segoe UI", 24, FontStyle.Bold);
            Controls.Add(_lblTimer);

            _lstMoves.Location = new Point(620, 190);
            _lstMoves.Size = new Size(220, 220);
            _lstMoves.Font = new Font("Consolas", 10);
            Controls.Add(_lstMoves);

            lstChat.Location = new Point(860, 190);
            lstChat.Size = new Size(220, 220);
            lstChat.Font = new Font("Segoe UI", 10);
            txtMessage.Location = new Point(860, 424);
            txtMessage.Size = new Size(150, 27);
            btnSend.Location = new Point(1018, 423);
            btnSend.Size = new Size(62, 30);
            btnSend.Text = "Gui";
            btnSend.Click += btnSend_Click;

            _btnDraw.Text = "Cau hoa";
            _btnDraw.Location = new Point(620, 430);
            _btnDraw.Size = new Size(105, 36);
            StyleActionButton(_btnDraw, Color.FromArgb(0, 122, 204));
            _btnDraw.Click += btnDraw_Click;
            Controls.Add(_btnDraw);

            _btnResign.Text = "Dau hang";
            _btnResign.Location = new Point(735, 430);
            _btnResign.Size = new Size(105, 36);
            StyleActionButton(_btnResign, Color.FromArgb(220, 53, 69));
            _btnResign.Click += btnResign_Click;
            Controls.Add(_btnResign);

            btnBack.Text = "Ve lobby";
            btnBack.Location = new Point(620, 486);
            btnBack.Size = new Size(220, 38);
        }

        private void WireEvents()
        {
            _service.OnGameState += RenderGame;
            _service.OnGameEnded += RenderGame;
            _service.OnChatReceived += ReceiveChat;
            _service.OnDrawMessage += HandleDrawMessage;
            _service.OnError += ShowError;
            _eventsAttached = true;
            FormClosed += (_, _) => DetachEvents();
        }

        private void DetachEvents()
        {
            if (!_eventsAttached)
                return;

            _service.OnGameState -= RenderGame;
            _service.OnGameEnded -= RenderGame;
            _service.OnChatReceived -= ReceiveChat;
            _service.OnDrawMessage -= HandleDrawMessage;
            _service.OnError -= ShowError;
            _eventsAttached = false;
        }

        private async void BoardClick(object? sender, MouseEventArgs e)
        {
            var game = _service.CurrentGame;
            if (game == null || game.IsGameOver)
                return;
            if (e.X < Offset || e.Y < Offset || e.X >= Offset + Cell * SizeBoard || e.Y >= Offset + Cell * SizeBoard)
                return;
            var col = (e.X - Offset) / Cell;
            var row = (e.Y - Offset) / Cell;
            if (row < 0 || row >= SizeBoard || col < 0 || col >= SizeBoard)
                return;
            if (game.CurrentTurnID != _service.PlayerId)
            {
                MessageBox.Show("Chua toi luot cua ban.");
                return;
            }
            await _service.SendAsync(CommandType.MAKE_MOVE, new MoveDTO { GameID = game.GameID, PlayerID = _service.PlayerId, Row = row, Col = col });
        }

        private void panel1_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var gridPen = new Pen(Color.FromArgb(75, 82, 94), 1);
            using var font = new Font("Segoe UI", 9, FontStyle.Bold);
            using var brush = new SolidBrush(Color.FromArgb(55, 65, 81));

            for (var i = 0; i <= SizeBoard; i++)
            {
                var p = Offset + i * Cell;
                g.DrawLine(gridPen, Offset, p, Offset + SizeBoard * Cell, p);
                g.DrawLine(gridPen, p, Offset, p, Offset + SizeBoard * Cell);
                if (i < SizeBoard)
                {
                    var center = Offset + i * Cell + Cell / 2;
                    g.DrawString((i + 1).ToString(), font, brush, 8, center - 8);
                    g.DrawString(((char)('A' + i)).ToString(), font, brush, center - 5, 8);
                }
            }

            var board = _service.CurrentGame?.Board;
            if (board == null)
                return;

            for (var r = 0; r < SizeBoard; r++)
                for (var c = 0; c < SizeBoard; c++)
                {
                    var value = board[r][c];
                    if (value == 0)
                        continue;
                    var x = Offset + c * Cell + Cell / 2;
                    var y = Offset + r * Cell + Cell / 2;
                    if (value == 1)
                    {
                        using var pen = new Pen(Color.FromArgb(220, 53, 69), 3);
                        g.DrawLine(pen, x - 11, y - 11, x + 11, y + 11);
                        g.DrawLine(pen, x + 11, y - 11, x - 11, y + 11);
                    }
                    else
                    {
                        using var pen = new Pen(Color.FromArgb(0, 122, 204), 3);
                        g.DrawEllipse(pen, x - 13, y - 13, 26, 26);
                    }
                }
        }

        private void RenderGame(GameStateDto game)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => RenderGame(game));
                return;
            }
            var boardChanged = HasBoardChanged(game);
            _lblInfo.Text = $"{game.PlayerXName} (X) vs {game.PlayerOName} (O)\nBan cam {game.YourSymbol}. Luot: {game.CurrentSymbol}";
            _lblTimer.Text = game.IsGameOver ? game.ResultText : $"{game.TimeRemaining}s";
            if (boardChanged)
            {
                _lstMoves.BeginUpdate();
                _lstMoves.Items.Clear();
                foreach (var move in game.Moves)
                    _lstMoves.Items.Add($"{move.Symbol}: {(char)('A' + move.Col)}{move.Row + 1}");
                _lstMoves.EndUpdate();
                pnlBoard.Invalidate();
            }
            _btnDraw.Enabled = !game.IsGameOver && !game.IsBotGame && !_drawRequestPending;
            _btnResign.Enabled = !game.IsGameOver;
            if (game.IsGameOver && !_endMessageShown)
            {
                _endMessageShown = true;
                MessageBox.Show(game.ResultText, "Ket thuc van dau");
                ReturnToLobby();
            }
        }

        private bool HasBoardChanged(GameStateDto game)
        {
            var key = string.Join("", game.Board.SelectMany(row => row));
            if (key == _lastBoardKey)
                return false;

            _lastBoardKey = key;
            return true;
        }

        private static void StyleActionButton(Button button, Color color)
        {
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.White;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(color);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(color);
            button.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            button.UseVisualStyleBackColor = false;
        }

        private async void btnDraw_Click(object? sender, EventArgs e)
        {
            var game = _service.CurrentGame;
            if (game == null || game.IsGameOver)
                return;
            if (game.IsBotGame)
            {
                MessageBox.Show("Bot khong ho tro cau hoa. Ban co the bam Dau hang de ket thuc van.");
                return;
            }

            _drawRequestPending = true;
            _btnDraw.Enabled = false;
            await _service.SendAsync(CommandType.DRAW_REQUEST, game.GameID);
            MessageBox.Show("Da gui yeu cau cau hoa cho doi thu.");
        }

        private async void btnResign_Click(object? sender, EventArgs e)
        {
            var game = _service.CurrentGame;
            if (game == null || game.IsGameOver)
                return;
            if (MessageBox.Show("Ban chac chan muon dau hang?", "Dau hang", MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;

            _btnResign.Enabled = false;
            await _service.SendAsync(CommandType.RESIGN, game.GameID);
        }

        private async void btnSend_Click(object? sender, EventArgs e)
        {
            var message = txtMessage.Text.Trim();
            var game = _service.CurrentGame;
            if (string.IsNullOrWhiteSpace(message) || game == null)
                return;

            await _service.SendAsync(CommandType.GAME_CHAT, new ChatDto
            {
                GameID = game.GameID,
                FromPlayerID = _service.PlayerId,
                FromPlayerName = _service.PlayerName,
                Message = message
            });
            txtMessage.Clear();
        }

        private void ReceiveChat(ChatDto chat)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ReceiveChat(chat));
                return;
            }
            var mine = chat.FromPlayerID == _service.PlayerId;
            lstChat.Items.Add($"{(mine ? "Ban" : chat.FromPlayerName)}: {chat.Message}");
            if (!mine)
            {
                _hasNewChat = true;
                Text = "Game Caro - co tin nhan moi";
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (_hasNewChat)
            {
                _hasNewChat = false;
                Text = "Game Caro";
            }
        }

        private void HandleDrawMessage(CommandType command, string senderId)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => HandleDrawMessage(command, senderId));
                return;
            }
            if (senderId == _service.PlayerId)
                return;

            if (command == CommandType.DRAW_REQUEST)
            {
                if (_drawPopupOpen)
                    return;
                _drawPopupOpen = true;
                var accepted = MessageBox.Show("Doi thu muon cau hoa. Ban chap nhan?", "Cau hoa", MessageBoxButtons.YesNo) == DialogResult.Yes;
                _drawPopupOpen = false;
                _ = _service.SendAsync(accepted ? CommandType.DRAW_ACCEPT : CommandType.DRAW_DECLINE, _service.CurrentGame?.GameID ?? "");
            }
            else if (command == CommandType.DRAW_DECLINE)
            {
                _drawRequestPending = false;
                _btnDraw.Enabled = _service.CurrentGame is { IsGameOver: false, IsBotGame: false };
                MessageBox.Show("Doi thu tu choi cau hoa.");
            }
        }

        private async void btnBack_Click(object? sender, EventArgs e)
        {
            await _service.SendAsync(CommandType.RETURN_TO_LOBBY, "");
            ReturnToLobby();
        }

        private void ReturnToLobby()
        {
            if (_returningToLobbyAfterEnd)
                return;

            _returningToLobbyAfterEnd = true;
            DetachEvents();
            var lobby = new LobbyForm();
            lobby.Show();
            Close();
        }

        private void ShowError(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ShowError(message));
                return;
            }
            if (message == _lastErrorMessage && (DateTime.Now - _lastErrorAt).TotalMilliseconds < 800)
                return;

            _lastErrorMessage = message;
            _lastErrorAt = DateTime.Now;
            MessageBox.Show(message);
        }

        private sealed class SmoothBoardPanel : Panel
        {
            public SmoothBoardPanel()
            {
                SetStyle(ControlStyles.UserPaint |
                         ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw, true);
                DoubleBuffered = true;
                UpdateStyles();
            }
        }
    }
}
