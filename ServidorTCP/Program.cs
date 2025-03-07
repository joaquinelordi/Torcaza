using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ServidorTCP;

namespace ServidorTCP
{ 
    static class Program
    {
        static async Task Main()
        {
            var server = new TcpServer("127.0.0.1", 123);
            await server.StartAsync();
        }
    }
}
