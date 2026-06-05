using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using Client.Network;
using Shared.DTO;

namespace Client.Forms
{
    public partial class GameForm : Form
    {
        private const int BoardSize = 15;
        private const int AxisMargin = 30;
        private readonly GameClientService service;
        private readonly LobbyForm? lobbyForm;
        private readonly int[,] board = new int[BoardSize, BoardSize];
        private readonly Label lblStatus = new Label();
        private readonly Label lblPlayers = new Label();
        private readonly Label lblMoveTitle = new Label();
        private readonly Panel pnlSide = new Panel();
        private readonly Panel pnlChat = new Panel();
        private readonly ListBox lstMoves = new ListBox();
        private readonly Button btnChatBubble = new Button();
        private readonly Button btnDraw = new Button();
        private readonly Button btnSurrender = new Button();
        private string gameId = string.Empty;
        private string player1Id = string.Empty;
        private string player2Id = string.Empty;
        private string currentTurnId = string.Empty;
        private int remainingSeconds;
        private bool gameOver;
        private bool chatOpen;
        private bool hasUnreadChat;

        public GameForm() : this(new GameClientService(), null, null)
        {
        }

        public GameForm(GameClientService service, LobbyForm? lobbyForm, GameDTO? game)
        {
            this.service = service;
            this.lobbyForm = lobbyForm;

            InitializeComponent();
            ApplyModernStyle();

            pnlBoard.MouseClick += pnlBoard_MouseClick;
            btnSend.Click += btnSend_Click;

            this.service.OnMoveMade += ApplyMoveSafe;
            this.service.OnChatReceived += AddChatSafe;
            this.service.OnDrawRequested += ShowDrawRequestSafe;
            this.service.OnTimerUpdated += UpdateTimerSafe;
            this.service.OnGameEnded += ShowGameEndedSafe;
            this.service.OnError += ShowErrorSafe;
            this.service.OnSuccess += AddSystemMessageSafe;

            if (game != null)
                LoadGame(game);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            service.OnMoveMade -= ApplyMoveSafe;
            service.OnChatReceived -= AddChatSafe;
            service.OnDrawRequested -= ShowDrawRequestSafe;
            service.OnTimerUpdated -= UpdateTimerSafe;
            service.OnGameEnded -= ShowGameEndedSafe;
            service.OnError -= ShowErrorSafe;
            service.OnSuccess -= AddSystemMessageSafe;
            base.OnFormClosed(e);
        }

        private void ApplyModernStyle()
        {
            BackColor = Color.FromArgb(12, 16, 24);
            ClientSize = new Size(1120, 760);
            Text = "UDM17 Caro";
            Font = new Font("Segoe UI", 10F);

            lblTitle.AutoSize = false;
            lblTitle.Text = "UDM17 Caro";
            lblTitle.Font = new Font("Segoe UI Semibold", 26F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(34, 20);
            lblTitle.Size = new Size(360, 50);

            lblPlayers.AutoSize = false;
            lblPlayers.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblPlayers.ForeColor = Color.FromArgb(180, 193, 215);
            lblPlayers.Location = new Point(38, 72);
            lblPlayers.Size = new Size(660, 24);
            Controls.Add(lblPlayers);

            lblStatus.AutoSize = false;
            lblStatus.Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold);
            lblStatus.ForeColor = Color.FromArgb(255, 209, 102);
            lblStatus.Location = new Point(782, 34);
            lblStatus.Size = new Size(300, 34);
            lblStatus.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(lblStatus);

            pnlBoard.Location = new Point(38, 116);
            pnlBoard.Size = new Size(630, 630);
            pnlBoard.BackColor = Color.FromArgb(248, 250, 253);
            pnlBoard.BorderStyle = BorderStyle.None;

            pnlSide.Location = new Point(704, 116);
            pnlSide.Size = new Size(378, 600);
            pnlSide.BackColor = Color.FromArgb(20, 26, 38);
            pnlSide.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(pnlSide);
            pnlSide.SendToBack();

            lblMoveTitle.Text = "Bien ban nuoc di";
            lblMoveTitle.AutoSize = false;
            lblMoveTitle.Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold);
            lblMoveTitle.ForeColor = Color.White;
            lblMoveTitle.Location = new Point(728, 136);
            lblMoveTitle.Size = new Size(260, 32);
            Controls.Add(lblMoveTitle);

            lstMoves.Location = new Point(728, 178);
            lstMoves.Size = new Size(330, 330);
            lstMoves.BackColor = Color.FromArgb(13, 18, 27);
            lstMoves.ForeColor = Color.FromArgb(230, 236, 246);
            lstMoves.BorderStyle = BorderStyle.None;
            lstMoves.Font = new Font("Consolas", 10F);
            Controls.Add(lstMoves);

            btnDraw.Location = new Point(728, 504);
            btnDraw.Size = new Size(330, 42);
            btnDraw.Click += btnDraw_Click;
            StyleAccentButton(btnDraw, "Cau hoa voi doi thu");
            Controls.Add(btnDraw);
            btnDraw.BringToFront();

            btnSurrender.Location = new Point(728, 560);
            btnSurrender.Size = new Size(330, 42);
            btnSurrender.Click += btnSurrender_Click;
            StyleDangerButton(btnSurrender, "Dau hang");
            Controls.Add(btnSurrender);
            btnSurrender.BringToFront();

            btnBack.Location = new Point(728, 616);
            btnBack.Size = new Size(330, 42);
            StyleSecondaryButton(btnBack, "Ve Lobby");
            btnBack.BringToFront();

            btnChatBubble.Location = new Point(1000, 650);
            btnChatBubble.Size = new Size(82, 54);
            btnChatBubble.Click += btnChatBubble_Click;
            StylePrimaryButton(btnChatBubble, "Chat");
            Controls.Add(btnChatBubble);
            btnChatBubble.BringToFront();

            BuildChatPanel();
        }

        private void BuildChatPanel()
        {
            pnlChat.Location = new Point(704, 216);
            pnlChat.Size = new Size(378, 420);
            pnlChat.BackColor = Color.FromArgb(20, 26, 38);
            pnlChat.BorderStyle = BorderStyle.FixedSingle;
            pnlChat.Visible = false;
            Controls.Add(pnlChat);
            pnlChat.BringToFront();

            Label chatHeader = new Label
            {
                Text = "Chat trong tran",
                AutoSize = false,
                Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(18, 16),
                Size = new Size(240, 30)
            };
            pnlChat.Controls.Add(chatHeader);

            Button btnCloseChat = new Button
            {
                Location = new Point(322, 14),
                Size = new Size(36, 30)
            };
            StyleSecondaryButton(btnCloseChat, "X");
            btnCloseChat.Click += (_, _) => ToggleChat(false);
            pnlChat.Controls.Add(btnCloseChat);

            lstChat.Location = new Point(18, 58);
            lstChat.Size = new Size(340, 270);
            lstChat.BackColor = Color.FromArgb(13, 18, 27);
            lstChat.ForeColor = Color.FromArgb(230, 236, 246);
            lstChat.BorderStyle = BorderStyle.None;
            lstChat.Font = new Font("Segoe UI", 10F);
            pnlChat.Controls.Add(lstChat);

            txtMessage.Location = new Point(18, 346);
            txtMessage.Size = new Size(260, 30);
            txtMessage.BackColor = Color.FromArgb(245, 247, 251);
            txtMessage.Font = new Font("Segoe UI", 10F);
            txtMessage.KeyDown += txtMessage_KeyDown;
            pnlChat.Controls.Add(txtMessage);

            btnSend.Location = new Point(286, 344);
            btnSend.Size = new Size(72, 34);
            StylePrimaryButton(btnSend, "Gui");
            pnlChat.Controls.Add(btnSend);
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

        private static void StyleAccentButton(Button button, string text)
        {
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(255, 193, 79);
            button.ForeColor = Color.FromArgb(18, 22, 31);
            button.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        }

        private static void StyleDangerButton(Button button, string text)
        {
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(239, 92, 83);
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        }

        private void LoadGame(GameDTO game)
        {
            gameId = game.GameID ?? string.Empty;
            player1Id = game.Player1ID ?? string.Empty;
            player2Id = game.Player2ID ?? string.Empty;
            currentTurnId = game.CurrentTurnID ?? player1Id;
            remainingSeconds = game.TimeRemaining;
            gameOver = game.IsGameOver;

            if (game.Board != null)
            {
                for (int row = 0; row < BoardSize && row < game.Board.Length; row++)
                {
                    if (game.Board[row] == null)
                        continue;

                    for (int col = 0; col < BoardSize && col < game.Board[row].Length; col++)
                        board[row, col] = game.Board[row][col];
                }
            }

            UpdateStatus();
            pnlBoard.Invalidate();
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {
            int cellSize = GetCellSize();
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.FromArgb(248, 250, 253));

            using Pen gridPen = new Pen(Color.FromArgb(184, 193, 208), 1);
            using Brush axisBrush = new SolidBrush(Color.FromArgb(83, 96, 120));
            using Font axisFont = new Font("Segoe UI Semibold", 8F, FontStyle.Bold);

            for (int i = 0; i < BoardSize; i++)
            {
                string label = (i + 1).ToString();
                SizeF topSize = graphics.MeasureString(label, axisFont);
                graphics.DrawString(label, axisFont, axisBrush, AxisMargin + i * cellSize + (cellSize - topSize.Width) / 2, 8);
                graphics.DrawString(label, axisFont, axisBrush, 8, AxisMargin + i * cellSize + 10);
            }

            for (int i = 0; i <= BoardSize; i++)
            {
                int pos = AxisMargin + i * cellSize;
                graphics.DrawLine(gridPen, pos, AxisMargin, pos, AxisMargin + BoardSize * cellSize);
                graphics.DrawLine(gridPen, AxisMargin, pos, AxisMargin + BoardSize * cellSize, pos);
            }

            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    if (board[row, col] == 1)
                        DrawX(graphics, row, col, cellSize);
                    else if (board[row, col] == 2)
                        DrawO(graphics, row, col, cellSize);
                }
            }
        }

        private async void pnlBoard_MouseClick(object? sender, MouseEventArgs e)
        {
            if (gameOver)
            {
                MessageBox.Show("Van dau da ket thuc.");
                return;
            }

            if (currentTurnId != service.CurrentUserId)
            {
                MessageBox.Show("Chua toi luot cua ban.");
                return;
            }

            if (e.X < AxisMargin || e.Y < AxisMargin)
                return;

            int cellSize = GetCellSize();
            int row = (e.Y - AxisMargin) / cellSize;
            int col = (e.X - AxisMargin) / cellSize;

            if (row < 0 || row >= BoardSize || col < 0 || col >= BoardSize)
                return;

            if (board[row, col] != 0)
                return;

            await SendSafeAsync(() => service.SendMoveAsync(row, col, gameId));
        }

        private void DrawX(Graphics graphics, int row, int col, int cellSize)
        {
            int padding = 9;
            Rectangle rect = new Rectangle(
                AxisMargin + col * cellSize + padding,
                AxisMargin + row * cellSize + padding,
                cellSize - padding * 2,
                cellSize - padding * 2);

            using Pen pen = new Pen(Color.FromArgb(31, 126, 255), 4)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawLine(pen, rect.Left, rect.Top, rect.Right, rect.Bottom);
            graphics.DrawLine(pen, rect.Right, rect.Top, rect.Left, rect.Bottom);
        }

        private void DrawO(Graphics graphics, int row, int col, int cellSize)
        {
            int padding = 8;
            Rectangle rect = new Rectangle(
                AxisMargin + col * cellSize + padding,
                AxisMargin + row * cellSize + padding,
                cellSize - padding * 2,
                cellSize - padding * 2);

            using Pen pen = new Pen(Color.FromArgb(239, 92, 83), 4);
            graphics.DrawEllipse(pen, rect);
        }

        private int GetCellSize()
        {
            return Math.Max(1, (Math.Min(pnlBoard.Width, pnlBoard.Height) - AxisMargin - 4) / BoardSize);
        }

        private void ApplyMoveSafe(MoveDTO move)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ApplyMoveSafe(move)));
                return;
            }

            ApplyMove(move);
        }

        private void ApplyMove(MoveDTO move)
        {
            if (move.GameID != gameId)
                return;

            if (move.Row < 0 || move.Row >= BoardSize || move.Col < 0 || move.Col >= BoardSize)
                return;

            int mark = move.PlayerID == player1Id ? 1 : 2;
            board[move.Row, move.Col] = mark;
            currentTurnId = move.PlayerID == player1Id ? player2Id : player1Id;

            AddMoveLog(move.PlayerID + " danh o (" + (move.Row + 1) + ", " + (move.Col + 1) + ")");
            UpdateStatus();
            pnlBoard.Invalidate();
        }

        private void UpdateStatus()
        {
            string myMark = service.CurrentUserId == player1Id ? "X" : "O";
            string opponent = service.CurrentUserId == player1Id ? player2Id : player1Id;

            lblPlayers.Text = "Ban: " + service.CurrentUserId + " (" + myMark + ")    Doi thu: " + opponent;

            if (gameOver)
                lblStatus.Text = "Da ket thuc";
            else if (currentTurnId == service.CurrentUserId)
                lblStatus.Text = "Den luot ban - " + FormatTime(remainingSeconds);
            else
                lblStatus.Text = "Cho doi thu - " + FormatTime(remainingSeconds);
        }

        private async void btnBack_Click(object sender, EventArgs e)
        {
            if (gameOver)
            {
                lobbyForm?.Show();
                Close();
                return;
            }

            DialogResult result = MessageBox.Show(
                "Ban se ve lobby tam thoi. Dong ho 30 giay cua luot hien tai van tiep tuc chay.",
                "Ve Lobby tam thoi",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                await SendSafeAsync(() => service.LeaveGameTemporarilyAsync(gameId));
                lobbyForm?.Show();
                Close();
            }
        }

        private async void btnSurrender_Click(object? sender, EventArgs e)
        {
            if (gameOver)
                return;

            DialogResult result = MessageBox.Show(
                "Ban chac chan muon dau hang va ket thuc van dau?",
                "Dau hang",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
                await SendSafeAsync(() => service.SurrenderAsync(gameId));
        }

        private async void btnSend_Click(object? sender, EventArgs e)
        {
            string message = txtMessage.Text.Trim();

            if (string.IsNullOrWhiteSpace(message))
                return;

            txtMessage.Clear();

            await SendSafeAsync(() => service.SendChatAsync(gameId, message));
        }

        private void txtMessage_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true;
            btnSend_Click(sender, e);
        }

        private async void btnDraw_Click(object? sender, EventArgs e)
        {
            if (gameOver)
            {
                MessageBox.Show("Van dau da ket thuc.");
                return;
            }

            DialogResult result = MessageBox.Show(
                "Ban muon gui loi cau hoa den doi thu?",
                "Cau hoa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
                await SendSafeAsync(() => service.RequestDrawAsync(gameId));
        }

        private void AddChatSafe(ChatMessageDTO chat)
        {
            if (IsDisposed || chat.GameID != gameId)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => AddChatSafe(chat)));
                return;
            }

            string sender = chat.SenderID == service.CurrentUserId ? "Toi" : chat.SenderName;
            lstChat.Items.Add("[" + chat.SentAt.ToString("HH:mm") + "] " + sender + ": " + chat.Message);
            lstChat.TopIndex = Math.Max(0, lstChat.Items.Count - 1);

            if (!chatOpen && chat.SenderID != service.CurrentUserId)
            {
                hasUnreadChat = true;
                UpdateChatBubble();
            }
        }

        private void UpdateTimerSafe(TimerUpdateDTO timer)
        {
            if (IsDisposed || timer.GameID != gameId)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdateTimerSafe(timer)));
                return;
            }

            currentTurnId = timer.CurrentTurnID;
            remainingSeconds = timer.RemainingSeconds;
            UpdateStatus();
        }

        private async void ShowDrawRequestSafe(DrawOfferDTO offer)
        {
            if (IsDisposed || offer.GameID != gameId)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowDrawRequestSafe(offer)));
                return;
            }

            DialogResult result = MessageBox.Show(
                offer.FromPlayerID + " muon cau hoa. Ban chap nhan?",
                "Cau hoa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            await SendSafeAsync(() => service.RespondDrawAsync(gameId, offer.FromPlayerID, result == DialogResult.Yes));
        }

        private void AddSystemMessageSafe(string message)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => AddSystemMessageSafe(message)));
                return;
            }

            AddMoveLog(message);
        }

        private void AddMoveLog(string message)
        {
            lstMoves.Items.Add((lstMoves.Items.Count + 1).ToString("00") + ". " + message);
            lstMoves.TopIndex = Math.Max(0, lstMoves.Items.Count - 1);
        }

        private void btnChatBubble_Click(object? sender, EventArgs e)
        {
            ToggleChat(!chatOpen);
        }

        private void ToggleChat(bool open)
        {
            chatOpen = open;
            pnlChat.Visible = open;
            pnlChat.BringToFront();

            if (open)
            {
                hasUnreadChat = false;
                txtMessage.Focus();
            }

            UpdateChatBubble();
        }

        private void UpdateChatBubble()
        {
            btnChatBubble.Text = hasUnreadChat ? "Chat !" : "Chat";
            btnChatBubble.BackColor = hasUnreadChat ? Color.FromArgb(239, 92, 83) : Color.FromArgb(31, 126, 255);
        }

        private void ShowGameEndedSafe(string message)
        {
            if (IsDisposed)
                return;

            if (gameOver)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowGameEndedSafe(message)));
                return;
            }

            gameOver = true;
            UpdateStatus();
            MessageBox.Show(message, "Ket thuc van dau");
            lobbyForm?.ClearCurrentGame();
            lobbyForm?.Show();
            Close();
        }

        private static string FormatTime(int seconds)
        {
            if (seconds < 0)
                seconds = 0;

            return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        }

        private void ShowErrorSafe(string message)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowErrorSafe(message)));
                return;
            }

            MessageBox.Show(message, "Loi server");
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
    }
}
