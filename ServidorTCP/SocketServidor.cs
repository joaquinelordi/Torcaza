using System.Text;
using System.Net;
using System.Net.Sockets;
using Npgsql;
using Newtonsoft.Json;
using Entidades;
using NpgsqlTypes;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite;
using System.Globalization;

namespace ServidorTCP
{
    public class TcpServer
    {
        private readonly TcpListener _listener;
        private readonly string _connectionString;
        private int _port { get; set; }
        private string _ipAddress { get; set; }

        public event EventHandler<string> MensajeRecibido;
        public TcpServer(string ipAddress, int port)
        {
            _listener = new TcpListener(IPAddress.Parse(ipAddress), port);
            _connectionString = "Host=localhost;Database=pruebas_T1;Username=postgres;Password=Admin01";
        }

        public async Task StartAsync()
        {
            _listener.Start();
            Console.WriteLine("Servidor TCP iniciado...");

            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync();
                _ = Task.Run(async () => await HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (var stream = client.GetStream())
            {
                var buffer = new byte[1024];
                var messageBuilder = new StringBuilder();
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    var mensaje = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    messageBuilder.Append(mensaje);

                    // Verificar si el mensaje completo ha sido recibido
                    if (messageBuilder.ToString().EndsWith("\r\n"))
                    {
                        var mensajeCompleto = messageBuilder.ToString().TrimEnd('\r', '\n');
                        Console.WriteLine($"Mensaje recibido: {mensajeCompleto}");

                        // Invocar el evento de mensaje recibido
                        MensajeRecibido?.Invoke(this, mensajeCompleto);

                        // Enviar una respuesta al cliente
                        var response = Encoding.ASCII.GetBytes("Mensaje recibido");
                        await stream.WriteAsync(response, 0, response.Length);

                        // Procesar el mensaje recibido
                        ProcesarMensaje(mensajeCompleto);

                        // Limpiar el StringBuilder para el próximo mensaje
                        messageBuilder.Clear();
                    }
                }
            }
        }

        // ...

        private void ProcesarMensaje(string mensaje)
        {
            int accion = 0;
            Console.WriteLine($"ProcesarMensaje -> Inicio: {mensaje}");

            // Reemplazar la línea problemática con la siguiente
            var ubicacion = JsonConvert.DeserializeObject<Ubicacion>(mensaje); // Usar JsonConvert de Newtonsoft.Json

            //Aca la idea es usar un enum con los tipos de acciones disponibles, enviado en el objeto del mensaje 
            switch (accion)
            {
                case 0:
                    CargarUbicacion(ubicacion);
                    break;
            }
        }

        private void CargarUbicacion(Ubicacion ubicacion)
        {
            // La idea es que el mensaje sea un objeto JSON con la siguiente estructura
            // Uso UUID fijos para las pruebas inicialies, solo recibo la latitud y longitud en un principio
            var idRegistro = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
            var idDispositivo = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
            var ubiTimestamp = DateTime.Now; //ubicacion.Timestamp;
            var agenteID = Guid.Parse(ubicacion.IDAgente);

            // Convertimos latitud y longitud a double
            double latitud = double.Parse(ubicacion.Latitud, CultureInfo.InvariantCulture);
            double longitud = double.Parse(ubicacion.Longitud, CultureInfo.InvariantCulture);

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                using var command = new NpgsqlCommand("CALL cargar_ubicacion(@idRegistro, @idDispositivo, @latitud, @longitud, @ubiTimestamp, @agenteID)",connection);
                command.Parameters.AddWithValue("idRegistro", idRegistro);
                command.Parameters.AddWithValue("idDispositivo", idDispositivo);
                command.Parameters.AddWithValue("latitud", latitud);
                command.Parameters.AddWithValue("longitud", longitud);
                command.Parameters.AddWithValue("ubiTimestamp", ubiTimestamp);
                command.Parameters.AddWithValue("agenteID", agenteID);
                command.ExecuteNonQuery();
            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"Error de PostgreSQL: {ex.Message}");
                Console.WriteLine($"Detalles: {ex.InnerException?.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            catch (Exception ex)
            {
                Console.WriteLine($"Error al conectar a la base de datos: {ex.Message}");
                Console.WriteLine($"Detalles: {ex.InnerException?.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }


        public void Stop()
        {
            _listener.Stop();
        }
    }
}
