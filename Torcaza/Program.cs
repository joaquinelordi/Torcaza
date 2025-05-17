using ServidorTCP;
using Microsoft.Extensions.Configuration;
using System.Configuration;
using System.Net;
using Entidades;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//Levanto parametros desde el AppSettings
string ipAddress;
int port;
try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("Properties/appsettings.json", optional: false, reloadOnChange: true)
        .Build();

    ipAddress = configuration["appSettings:TCP_IP_ADDRESS"] ?? "127.0.0.1";
    port = int.Parse(configuration["appSettings:TCP_PORT"] ?? "123");
}
catch (Exception ex)
{
    Console.WriteLine($"Error al leer appsettings.json: {ex.Message}");
    ipAddress = "127.0.0.1";
    port = 123;
}

//Handler JWT
builder.Services.AddSingleton<HandlerJWT>();

// httpClient para OpenCellID
builder.Services.AddHttpClient();
builder.Services.AddSingleton<OpenCellID>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<OpenCellID>>();
    var apiKey = builder.Configuration["OpenCellID:ApiKey"];
    return new OpenCellID(sp.GetRequiredService<IHttpClientFactory>(), logger, apiKey);
});

// Servicio Servidor TCP
builder.Services.AddSingleton<TcpServer>(sp =>
{
    var openCellID = sp.GetRequiredService<OpenCellID>();
    var handlerJWT = sp.GetRequiredService<HandlerJWT>();
    return new TcpServer(ipAddress, port, openCellID, handlerJWT);
});



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

//app.UseAuthorization();

app.MapControllers();

//Inicio servidor TCP
var tcpService = app.Services.GetRequiredService<TcpServer>();

Task.Run(() => tcpService.StartAsync());

app.Run();
