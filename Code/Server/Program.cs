using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Shared.Models;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            TcpListener server = new TcpListener(IPAddress.Any, 8888);

            server.Start();

            Console.WriteLine("Server dang chay...");

            TcpClient client = server.AcceptTcpClient();

            Console.WriteLine("Client da ket noi!");

            StreamReader reader = new StreamReader(client.GetStream());

            StreamWriter writer = new StreamWriter(client.GetStream());

            writer.AutoFlush = true;

            while (true)
            {
                string? json = reader.ReadLine();

                if (!string.IsNullOrEmpty(json))
                {
                    Packet? packet = JsonSerializer.Deserialize<Packet>(json);

                    if (packet is not null)
                    {
                        Console.WriteLine("Command: " + packet.Command);

                        Console.WriteLine("Data: " + packet.Data);

                        writer.WriteLine("Login success");
                    }
                }
            }
        }
    }
}