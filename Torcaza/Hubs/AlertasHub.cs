using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Torcaza.Hubs
{
    public class AlertasHub : Hub
    {
        public async Task EnviarAlerta(string mensaje)
        {
            Console.WriteLine($"EnviarAlerta invoked: {{mensaje}}", mensaje);
            await Clients.All.SendAsync("RecibirAlerta", mensaje);
        }

        public override Task OnConnectedAsync()
        {
            Console.WriteLine("Cliente conectado: {connectionId}", Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine("Cliente desconectado: {connectionId} ex:{exception}", Context.ConnectionId, exception?.Message);
            return base.OnDisconnectedAsync(exception);
        }
    }
}
