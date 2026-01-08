using Entidades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ModuloAlertas;
using ServidorTCP;
using System.Configuration;
using System.Net;


var builder = WebApplication.CreateBuilder(args);

//Definicion de WebHost para usar Kestrel con http y https

if (builder.Environment.IsDevelopment())
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        
        options.ListenAnyIP(5133);

        options.ListenAnyIP(7089, listenOptions =>
        {
            listenOptions.UseHttps(https =>
            {
                https.ClientCertificateMode = ClientCertificateMode.NoCertificate;
                https.CheckCertificateRevocation = false;
            });
        });
    });
}
else
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Listen(IPAddress.Any, 8080);
        options.Listen(IPAddress.Any, 443, listenOptions =>
        {
            var certPath = builder.Configuration["Kestrel:Certificates:Default:Path"];
            var certPassword = builder.Configuration["Kestrel:Certificates:Default:Password"];
            listenOptions.UseHttps(certPath, certPassword);
        });
    });
}


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

// Notificador Telegram Bot clientes
builder.Services.AddSingleton<TelegramBot>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var token = configuration["TorcazaBot:ApiKey"];
    var conectionString = configuration["Database:ConnectionString"];
    return new TelegramBot(token, conectionString);
});

// Entity Framework
builder.Services.AddDbContext<AlertasDBContext>(options =>
{
    var configuration = builder.Configuration;
    var conectionString = configuration["Database:ConnectionString"];
    options.UseNpgsql(conectionString, npgsql => npgsql.UseNetTopologySuite());

});


// Servicio Servidor TCP
builder.Services.AddSingleton<TcpServer>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var conectionString = configuration["Database:ConnectionString"];
    var openCellID = sp.GetRequiredService<OpenCellID>();
    var handlerJWT = sp.GetRequiredService<HandlerJWT>();
    var telegramBot = sp.GetRequiredService<TelegramBot>();
    var notificadorTelegram = new NotificadorTelegram(telegramBot);
    var notificadorWEB = new NotificadorWEB();
    var listaNotificadores = new List<INotificador>
    {
        notificadorTelegram,
        notificadorWEB
    };
    var notificador = new Notificador(listaNotificadores);

    return new TcpServer(ipAddress, port, openCellID, handlerJWT, new AlertaCercoVirtual(conectionString, notificador));
});

// SignalR
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("SignalRDev", policy =>
    {
        policy
            .WithOrigins("http://localhost:5183", "https://localhost:5183")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // necesario si luego usás cookies o accessTokenFactory
    });
});

// servicio que arranca el bot de telegram
builder.Services.AddHostedService<TelegramBotHostedService>();

//Configuracion de autorizacion de conexiones
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("EncryptedOnly", policy =>
        policy.RequireAssertion(context =>
        {
            if (context.Resource is HttpContext httpContext &&
                httpContext.Connection.LocalPort == 8080)
            {
                return httpContext.Request.Headers.TryGetValue("X-Encrypted", out var value) &&
                       value == "true";
            }
            return true;
        }));
});



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

/*
app.UseCors("SignalR");
app.UseAuthorization();
// Mapea el endpoint de SignalR
app.MapHub<Torcaza.Hubs.AlertasHub>("/hub/Alertas").RequireAuthorization("EncryptedOnly");

// Esto siempre va ultimo
app.MapControllers().RequireAuthorization("EncryptedOnly");
*/

//PARA PRUEBAS, LUEGO SE DEBE CONFIGURAR CORS CORRECTAMENTE
app.UseRouting();
app.UseCors("SignalRDev");
// Hub SIN autorización (solo pruebas)
app.MapHub<Torcaza.Hubs.AlertasHub>("/hub/Alertas").RequireCors("SignalRDev");

// Controllers (si querés también dejarlos sin auth mientras probás)
app.MapControllers(); // .RequireAuthorization("EncryptedOnly");  <- COMENTALO EN PRUEBA

//Inicio servidor TCP
var tcpService = app.Services.GetRequiredService<TcpServer>();

Task.Run(() => tcpService.StartAsync());

app.Run();
