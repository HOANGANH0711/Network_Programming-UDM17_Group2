using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Client.Network;

namespace Client.Forms
{
    public partial class LoginForm : Form
    {
        public LoginForm()
        {
            InitializeComponent();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void txtServerIP_TextChanged(object sender, EventArgs e)
        {

        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                var service = new GameClientService();
                service.Connect(txtServerIP.Text, 5000);

                MessageBox.Show("Kết nối server thành công!");

                LobbyForm lobby = new LobbyForm(service);
                lobby.Show();
                this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không kết nối được server! " + ex.Message);
            }
        }
    }
}