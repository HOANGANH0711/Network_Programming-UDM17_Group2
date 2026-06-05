using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Client.Network;
using Shared.DTO;

namespace Client.Forms
{
    public partial class HistoryForm : Form
    {
        private readonly GameClientService? service;
        private readonly LobbyForm? lobbyForm;
        private readonly Label lblEmpty = new Label();
        private readonly Label lblDetailTitle = new Label();
        private readonly ListBox lstMoveDetail = new ListBox();
        private List<GameHistoryDTO> currentHistory = new List<GameHistoryDTO>();

        public HistoryForm()
        {
            InitializeComponent();
            ApplyModernStyle();
        }

        public HistoryForm(GameClientService service, LobbyForm lobbyForm)
        {
            this.service = service;
            this.lobbyForm = lobbyForm;
            InitializeComponent();
            ApplyModernStyle();

            this.service.OnHistoryReceived += UpdateHistorySafe;
            this.service.OnError += ShowErrorSafe;
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (service != null)
                await SendSafeAsync(() => service.RequestHistoryAsync());
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (service != null)
            {
                service.OnHistoryReceived -= UpdateHistorySafe;
                service.OnError -= ShowErrorSafe;
            }

            base.OnFormClosed(e);
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            lobbyForm?.Show();
            Close();
        }

        private void UpdateHistorySafe(List<GameHistoryDTO> history)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdateHistorySafe(history)));
                return;
            }

            currentHistory = history;
            lstHistory.Items.Clear();
            lstMoveDetail.Items.Clear();
            lblDetailTitle.Text = "Chi tiet nuoc di";

            foreach (GameHistoryDTO item in history)
                lstHistory.Items.Add(FormatHistory(item));

            lblEmpty.Visible = history.Count == 0;
        }

        private void lstHistory_SelectedIndexChanged(object? sender, EventArgs e)
        {
            int index = lstHistory.SelectedIndex;

            if (index < 0 || index >= currentHistory.Count)
                return;

            GameHistoryDTO game = currentHistory[index];
            lblDetailTitle.Text = "Nuoc di: " + game.Player1ID + " vs " + game.Player2ID;
            lstMoveDetail.Items.Clear();

            if (game.MoveLog.Count == 0)
            {
                lstMoveDetail.Items.Add("Tran nay chua co nuoc di nao.");
                return;
            }

            foreach (string move in game.MoveLog)
                lstMoveDetail.Items.Add(move);
        }

        private static string FormatHistory(GameHistoryDTO item)
        {
            string winner = string.IsNullOrWhiteSpace(item.WinnerID) ? "Hoa" : "Thang: " + item.WinnerID;
            return item.EndedAt.ToString("HH:mm dd/MM") + " | " +
                   item.Player1ID + " vs " + item.Player2ID + " | " +
                   winner;
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

        private void ApplyModernStyle()
        {
            BackColor = Color.FromArgb(12, 16, 24);
            ClientSize = new Size(980, 560);
            Font = new Font("Segoe UI", 10F);
            Text = "Lich su dau";

            lblTitle.AutoSize = false;
            lblTitle.Text = "Lich su tran dau";
            lblTitle.Font = new Font("Segoe UI Semibold", 26F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(36, 28);
            lblTitle.Size = new Size(460, 54);

            lstHistory.Location = new Point(40, 110);
            lstHistory.Size = new Size(520, 350);
            lstHistory.BackColor = Color.FromArgb(20, 26, 38);
            lstHistory.ForeColor = Color.FromArgb(230, 236, 246);
            lstHistory.BorderStyle = BorderStyle.None;
            lstHistory.Font = new Font("Consolas", 10F);
            lstHistory.SelectedIndexChanged += lstHistory_SelectedIndexChanged;

            lblDetailTitle.AutoSize = false;
            lblDetailTitle.Text = "Chi tiet nuoc di";
            lblDetailTitle.Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold);
            lblDetailTitle.ForeColor = Color.White;
            lblDetailTitle.Location = new Point(600, 110);
            lblDetailTitle.Size = new Size(320, 32);
            Controls.Add(lblDetailTitle);

            lstMoveDetail.Location = new Point(600, 154);
            lstMoveDetail.Size = new Size(330, 306);
            lstMoveDetail.BackColor = Color.FromArgb(20, 26, 38);
            lstMoveDetail.ForeColor = Color.FromArgb(230, 236, 246);
            lstMoveDetail.BorderStyle = BorderStyle.None;
            lstMoveDetail.Font = new Font("Consolas", 10F);
            Controls.Add(lstMoveDetail);

            lblEmpty.AutoSize = false;
            lblEmpty.Text = "Chua co tran dau nao duoc ghi nhan trong phien server nay.";
            lblEmpty.ForeColor = Color.FromArgb(180, 193, 215);
            lblEmpty.Font = new Font("Segoe UI", 11F);
            lblEmpty.Location = new Point(52, 126);
            lblEmpty.Size = new Size(500, 40);
            lblEmpty.Visible = true;
            Controls.Add(lblEmpty);
            lblEmpty.BringToFront();

            btnBack.Location = new Point(760, 486);
            btnBack.Size = new Size(170, 38);
            btnBack.Text = "Ve Lobby";
            btnBack.FlatStyle = FlatStyle.Flat;
            btnBack.FlatAppearance.BorderColor = Color.FromArgb(72, 84, 104);
            btnBack.BackColor = Color.FromArgb(31, 38, 52);
            btnBack.ForeColor = Color.FromArgb(230, 236, 246);
            btnBack.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        }
    }
}
