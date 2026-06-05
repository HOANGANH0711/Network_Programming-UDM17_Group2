using Client.Network;
using Shared.DTO;
using Shared.Enums;

namespace Client.Forms
{
    public partial class HistoryForm : Form
    {
        private readonly GameClientService _service = GameClientService.Instance;
        private readonly ListBox _lstMoves = new ListBox();
        private List<HistoryItemDto> _items = new List<HistoryItemDto>();
        private bool _eventsAttached;

        public HistoryForm()
        {
            InitializeComponent();
            BuildUi();
            _service.OnHistoryReceived += RenderHistory;
            _eventsAttached = true;
            FormClosed += (_, _) => DetachEvents();
            _ = _service.SendAsync(CommandType.GET_HISTORY, "");
        }

        private void DetachEvents()
        {
            if (!_eventsAttached)
                return;

            _service.OnHistoryReceived -= RenderHistory;
            _eventsAttached = false;
        }

        private void BuildUi()
        {
            ClientSize = new Size(820, 520);
            BackColor = Color.FromArgb(21, 27, 36);
            lblTitle.Text = "Lich su dau";
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(28, 22);

            lstHistory.Location = new Point(28, 84);
            lstHistory.Size = new Size(360, 340);
            lstHistory.Font = new Font("Segoe UI", 11);
            lstHistory.SelectedIndexChanged += (_, _) => ShowMoves();

            _lstMoves.Location = new Point(420, 84);
            _lstMoves.Size = new Size(360, 340);
            _lstMoves.Font = new Font("Consolas", 11);
            Controls.Add(_lstMoves);

            btnBack.Location = new Point(28, 450);
            btnBack.Size = new Size(140, 36);
            btnBack.Text = "Quay lai lobby";
        }

        private void RenderHistory(List<HistoryItemDto> items)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => RenderHistory(items));
                return;
            }
            _items = items.OrderByDescending(i => i.PlayedAt).ToList();
            lstHistory.Items.Clear();
            foreach (var item in _items)
                lstHistory.Items.Add($"{item.PlayedAt:HH:mm dd/MM} | {item.Result} vs {item.OpponentName} | {item.Mode}");
        }

        private void ShowMoves()
        {
            _lstMoves.Items.Clear();
            if (lstHistory.SelectedIndex < 0 || lstHistory.SelectedIndex >= _items.Count)
                return;
            foreach (var move in _items[lstHistory.SelectedIndex].Moves)
                _lstMoves.Items.Add($"{move.Symbol} -> {(char)('A' + move.Col)}{move.Row + 1}");
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            DetachEvents();
            var lobby = new LobbyForm();
            lobby.Show();
            Close();
        }
    }
}
