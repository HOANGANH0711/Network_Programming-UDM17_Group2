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
namespace Client.Forms
{
    public partial class LoginForm : Form
    {
        private System.Net.Sockets.Socket? socket;

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
                if (socket == null)
                    socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);

                socket.Connect(txtServerIP.Text, 5000);

                if (socket.Connected)
                {
                    byte[] data = Encoding.UTF8.GetBytes("Hello Server");
                    socket.Send(data);

                    MessageBox.Show("Kết nối server thành công!");

                    LobbyForm lobby = new LobbyForm();
                    lobby.Show();
                    this.Hide();
                }
                else
                {
                    MessageBox.Show("Không kết nối được server!");
                }
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                MessageBox.Show("Không kết nối được server! " + ex.Message);
            }
        }
    }
}