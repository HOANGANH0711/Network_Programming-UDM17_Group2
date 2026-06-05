using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client.Forms
{
    public partial class HistoryForm : Form
    {
        private Client.Network.GameClientService _service;

        public HistoryForm(Client.Network.GameClientService service)
        {
            InitializeComponent();
            _service = service;
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            LobbyForm lobby = new LobbyForm(_service);
            lobby.Show();
            this.Hide();
        }
    }
}