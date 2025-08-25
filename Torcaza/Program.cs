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
builder.Services.AddHttpClient("OpenCellID");
builder.Services.AddSingleton<OpenCellID>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<OpenCellID>>();
    var apiKey = builder.Configuration["OpenCellID:ApiKey"];
    return new OpenCellID(sp.GetRequiredService<IHttpClientFactory>(), logger, apiKey);
});

// httoClient para App Cliente en browser
builder.Services.AddHttpClient("AppCliente");
builder.Services.AddSingleton<CapaComunicacionAppCliente>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("AppCliente");
    var urlBrowser = sp.GetRequiredService<IConfiguration>()["AppCliente:BaseUrl"] ?? "http://localhost:5183";
    httpClient.BaseAddress = new Uri($"{urlBrowser}");
    return new CapaComunicacionAppCliente(httpClient, urlBrowser);
});

// Servicio Servidor TCP
builder.Services.AddSingleton<TcpServer>(sp =>
{
    var openCellID = sp.GetRequiredService<OpenCellID>();
    var handlerJWT = sp.GetRequiredService<HandlerJWT>();
    return new TcpServer(ipAddress, port, openCellID, handlerJWT);
});

// SignalR
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://localhost:7089")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Notificador Telegram Bot clientes
builder.Services.AddSingleton<NotificadorTelegramBot>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var token = configuration["TorcazaBot:ApiKey"];
    return new NotificadorTelegramBot(token);
});

// servicio que arranca el bot
builder.Services.AddHostedService<TelegramBotHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

//app.UseAuthorization();


// Mapea el endpoint de SignalR
app.UseCors();
app.MapHub<Torcaza.Hubs.AlertasHub>("/hub/Alertas");


// Esto siempre va ultimo
app.MapControllers();
//Inicio servidor TCP
var tcpService = app.Services.GetRequiredService<TcpServer>();

Task.Run(() => tcpService.StartAsync());

app.Run();
