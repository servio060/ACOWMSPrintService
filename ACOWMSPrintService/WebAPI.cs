using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace ACOWMSPrintService;

public class WebAPI
{
    private IConfigurationRoot builder { get; } = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
    private string? BaseURL;
    private string? Token;
    private string? Sucursal;
    private HttpClientHandler handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };
    private HttpClient _http;
    public WebAPI()
    {
        this.BaseURL = builder.GetSection("ConnectionStrings").GetValue<string>("WebAPI") ?? string.Empty;
        this.Token = builder.GetSection("ConnectionStrings").GetValue<string>("Token") ?? string.Empty;
        this.Sucursal = builder.GetValue<string>("IdSucursal") ?? string.Empty;
        this._http = new HttpClient(this.handler);
        this._http.DefaultRequestHeaders.Add("TOKEN", this.Token);
        this._http.Timeout = TimeSpan.FromMinutes(5);
    }
    public async void Dispose()
    {
        this._http.Dispose();
    }
    public async Task<List<PrintTask>> GetTasks()
    {
        List<PrintTask> printTasks = new List<PrintTask>();
        try
        {
            string url = this.BaseURL + $"/Reports/GetPrintTasks/?sucursal={this.Sucursal}";
            
            HttpRequestMessage reqMessage = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await this._http.SendAsync(reqMessage);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var responseObject = JObject.Parse(responseContent);

                var error = responseObject.ContainsKey("Error") ? responseObject["Error"]?.Value<string>() : "";

                if (!string.IsNullOrEmpty(error))
                    throw new Exception($"Error interno de API: {error}");
                
                //var values = responseObject["PrintTasks"]?.Value<string>() ?? "";

                printTasks = JsonConvert.DeserializeObject<List<PrintTask>>(responseObject["PrintTasks"]?.ToString() ?? "") ?? printTasks;
                return printTasks;
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error en solicitud a API: {responseContent}");
            }
        }catch(Exception e)
        {
            Console.WriteLine($"Fallo al obtener tareas de impresion: {e.Message}");
            return printTasks;
        }
    }

    public async Task<bool> UpdatePrintedTasks(List<PrintTask> printTasks)
    {
        try
        {
            string url = this.BaseURL + "/Reports/UpdatePrintTasks/";
            this._http.DefaultRequestHeaders.Accept.Clear();
            this._http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            StringContent content = new StringContent(JsonConvert.SerializeObject(printTasks), Encoding.UTF8, "application/json");

            HttpRequestMessage reqMessage = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };

            var response = await this._http.SendAsync(reqMessage);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Respuesta ok al actualizar: {responseContent}");
                return true;
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error en solicitud a API: {responseContent}");
            }
        }catch (Exception e)
        {
            Console.WriteLine($"Fallo al actualizar tareas de impresion: {e.Message}");
            return false;
        }
    }

    public async Task<byte[]> GetDocumentFromAPI(string reportName, string parameters)
    {
        try
        {
            string url = this.BaseURL + $"/Reports/GetReport/?reportName={reportName}";
            this._http.DefaultRequestHeaders.Accept.Clear();
            this._http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var obj = JsonConvert.DeserializeObject<List<ParameterField>>(parameters);

            StringContent content = new StringContent(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json");

            HttpRequestMessage reqMessage = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };

            var response = await this._http.SendAsync(reqMessage);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsByteArrayAsync();
                
                return responseContent;
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error en solicitud a API: {responseContent}");
            }
        }catch(Exception e)
        {
            Console.WriteLine($"Fallo al obtener pdf: {e.Message}");
            return new byte[0];
        }
    }
}

public class PrintTask
{
    public int Id { get; set; }
    public string? reportName { get; set; }
    public string? parameters { get; set; }
    public string? printerName { get; set; }
    public string? printed { get; set; }
}

public class ParameterField
{
    public string ParameterFieldName { get; set; }
    public string ParameterValue { get; set; }
}