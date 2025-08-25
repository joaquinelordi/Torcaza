using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Torcaza.Hubs
{
    public class AlertasHub : Hub
    {
        public async Task EnviarAlerta(string mensaje)
        {
            await Clients.All.SendAsync("RecibirAlerta", mensaje);
        }
    }
}
