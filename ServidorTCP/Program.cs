using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Entidades;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServidorTCP;

namespace ServidorTCP
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                       .ConfigureServices((context, services) =>
                       {
                           // Configuración de servicios
                           services.AddHttpClient();
                           services.AddSingleton<OpenCellID>(sp =>
                           {
                               var logger = sp.GetRequiredService<ILogger<OpenCellID>>();
                               var apiKey = context.Configuration["OpenCellID:ApiKey"];
                               return new OpenCellID(sp.GetRequiredService<IHttpClientFactory>(), logger, apiKey);
                           });

                           //Handler JWT
                           services.AddSingleton<HandlerJWT>();

                           services.AddSingleton<TcpServer>(sp =>
                           {
                               var openCellID = sp.GetRequiredService<OpenCellID>();
                               var handlerJWT = sp.GetRequiredService<HandlerJWT>();
                               var ipAddress = sp.GetRequiredService<IConfiguration>()["appSettings:TCP_IP_ADDRESS"] ?? "127.0.0.1";
                               var port = int.Parse(sp.GetRequiredService<IConfiguration>()["appSettings:TCP_PORT"] ?? "123");
                               return new TcpServer(ipAddress, port, openCellID, handlerJWT);
                           });
                       })
                       .Build();

            // Obtener el servicio TcpServer e iniciar el servidor
            var server = host.Services.GetRequiredService<TcpServer>();
            await server.StartAsync();
        }
    }
}

