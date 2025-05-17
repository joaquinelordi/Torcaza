using Entidades;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;

namespace ServidorTCP
{
    public class OpenCellID
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenCellID> _logger;
        private readonly string _apiKey;

        public OpenCellID(IHttpClientFactory httpClientFactory, ILogger<OpenCellID> logger, string apiKey)
        {
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
            _apiKey = apiKey;
        }

        public void GetCellInfo(ref InfoCell infocell)
        {
            // conversion explicita para hacer el GET de la api OpenCellID 
            int mcc = (int)infocell.Mcc;
            int mnc = (int)infocell.Mnc;
            int lac = int.Parse(infocell.Lac);
            int cellId = int.Parse(infocell.Cellid);

            var openCellID = GetCellInfoAsync(mcc, mnc, lac, cellId);

            if (openCellID != null)
            {
                infocell.Lat = openCellID.Result.Lat;
                infocell.Lon = openCellID.Result.Lon;
                infocell.AverageSignalStrength = openCellID.Result.AverageSignalStrength;
                infocell.Range = openCellID.Result.Range;
                _logger.LogDebug("Coordenadas obtenidas: Lat: {Lat}, Lon: {Lon}", infocell.Lat, infocell.Lon);
            }
            else
            {
                _logger.LogError("No se pudo obtener información de la celda.");
            }
        }
        public async Task<InfoCell> GetCellInfoAsync(int mcc, int mnc, int lac, int cellId)
        {
            var url = $"https://opencellid.org/cell/get?key={_apiKey}&mcc={mcc}&mnc={mnc}&lac={lac}&cellid={cellId}&format=json";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            try
            {
                var response = await _httpClient.GetAsync(url, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error al obtener información de la celda: {StatusCode}", response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var cellInfo = JsonConvert.DeserializeObject<InfoCell>(content);

                // Depuración: Asegurar que la respuesta contiene lat y lon correctos
                _logger.LogInformation("Respuesta API: {Json}", JsonConvert.SerializeObject(cellInfo, Formatting.Indented));

                if (cellInfo?.Lat == null || cellInfo?.Lon == null)
                {
                    _logger.LogError("La respuesta de la API no contiene coordenadas válidas.");
                    return null;
                }

                return cellInfo;
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("La solicitud a la API de OpenCellID se canceló debido a un timeout.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error inesperado: {Message}", ex.Message);
                return null;
            }
        }
    }
}
