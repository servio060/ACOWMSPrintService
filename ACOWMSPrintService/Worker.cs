using System.Diagnostics;
namespace ACOWMSPrintService;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

            try
            {
                WebAPI api = new WebAPI();
                var tasks = await api.GetTasks() ?? [];

                foreach (var task in tasks)
                {
                    await ProcessTask(api, task, stoppingToken);
                }
                if (tasks.Count() > 0)
                {
                    await api.UpdatePrintedTasks(tasks);
                }
                api.Dispose();
                
            }catch(Exception ex)
            {
                logger.LogError(ex, "Error while processing print tasks");
            }

            await Task.Delay(100000, stoppingToken);
        }
    }

    private async Task ProcessTask(WebAPI api, PrintTask task, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Processing task {TaskId}", task.Id);
            //WebAPI webAPI = new WebAPI();
            var pdfBytes = await api.GetDocumentFromAPI(task.reportName ?? "", task.parameters ?? "");

            if (pdfBytes.Length == 0)
                throw new Exception("Documento no obtenido");

            string tempFile = Path.Combine(Path.GetTempPath(),$"{Guid.NewGuid()}.pdf");
            await File.WriteAllBytesAsync(tempFile, pdfBytes);

            PrintFile(tempFile, task.printerName ?? "");
            File.Delete(tempFile);
            //webAPI.Dispose();
        }catch(Exception ex)
        {
            logger.LogError("Error printing task {TaskId}: {message}", task.Id, ex.Message);
        }
        
    }

    private static void PrintFile(string filePath, string printerName)
    {

        try
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"PDF file not found: {filePath}");
            //Path.GetFullPath 

            //var sumatraPath = AppContext.BaseDirectory + @"\SumatraPDF.exe"; // Adjust as needed
            var sumatraPath = Path.Combine(AppContext.BaseDirectory, "SumatraPDF", "SumatraPDF.exe");
            if (!File.Exists(sumatraPath))
                throw new FileNotFoundException($"SumatraPDF not found at: {sumatraPath}");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = sumatraPath,
                    Arguments = $"-print-to \"{printerName}\" -silent \"{filePath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true // Capture error output
                }
            };

            process.Start();
            
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                string error = process.StandardError.ReadToEnd();
                throw new Exception($"SumatraPDF failed with exit code {process.ExitCode}: {error}");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to print PDF: {ex.Message}", ex);
        }
    }
}
