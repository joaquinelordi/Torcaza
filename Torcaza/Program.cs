using ServidorTCP;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//Servicio Servidor TCP
var tcpServerService = new TcpServer("127.0.0.1", 1234);
builder.Services.AddSingleton(tcpServerService);


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
