using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AnalysisService.Controllers
{
    [ApiController]
    [Route("api/analysis")]
    public class AnalysisController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        // БД отчетов. В реальности - EF Core + Postgres.
        private static readonly ConcurrentBag<AnalysisReport> _reports = new();

        public AnalysisController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("check")]
        public async Task<IActionResult> Analyze([FromBody] AnalysisRequest request)
        {
            try
            {
                // 1. Межсервисное взаимодействие: Получаем файл из Storage
                var client = _httpClientFactory.CreateClient();
                // Имя сервиса в Docker сети будет "filestorage"
                var storageUrl = "http://filestorage:8080/api/files"; 
                
                // Сначала получим метаданные, чтобы узнать TaskId и StudentName
                var metaResponse = await client.GetAsync($"{storageUrl}/{request.FileId}/meta");
                if (!metaResponse.IsSuccessStatusCode) return BadRequest("File metadata not found in Storage");
                
                var metaJson = await metaResponse.Content.ReadAsStringAsync();
                var metadata = JsonSerializer.Deserialize<FileMetadataDto>(metaJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Получаем сам файл для хэширования
                var fileResponse = await client.GetAsync($"{storageUrl}/{request.FileId}");
                if (!fileResponse.IsSuccessStatusCode) return BadRequest("File content not found");
                
                var fileBytes = await fileResponse.Content.ReadAsByteArrayAsync();
                
                // 2. Логика Антиплагиата
                string fileHash = ComputeMd5(fileBytes);
                
                // Ищем совпадения: Тот же TaskId, Тот же Хэш, НО Другой Студент, и дата загрузки раньше текущей
                var isPlagiarism = _reports.Any(r => 
                    r.TaskId == metadata.TaskId && 
                    r.FileHash == fileHash && 
                    r.StudentName != metadata.StudentName);

                // 3. Бонус: Облако слов (QuickChart API)
                string wordCloudUrl = await GenerateWordCloud(fileBytes);

                var report = new AnalysisReport
                {
                    ReportId = Guid.NewGuid(),
                    FileId = request.FileId,
                    TaskId = metadata.TaskId,
                    StudentName = metadata.StudentName,
                    FileHash = fileHash,
                    IsPlagiarism = isPlagiarism,
                    AnalyzedAt = DateTime.UtcNow,
                    WordCloudUrl = wordCloudUrl
                };

                _reports.Add(report);

                return Ok(report);
            }
            catch (HttpRequestException)
            {
                // Обработка ошибки падения зависимого сервиса
                return StatusCode(503, "File Storage Service is unavailable");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("reports/{taskId}")]
        public IActionResult GetReports(string taskId)
        {
            var results = _reports.Where(r => r.TaskId == taskId).ToList();
            return Ok(results);
        }

        private string ComputeMd5(byte[] data)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(data);
            return Convert.ToBase64String(hash);
        }

        private async Task<string> GenerateWordCloud(byte[] fileData)
        {
            try 
            {
                // Упрощенно: считаем, что файл текстовый. 
                // В реальности нужен парсер (pdf/docx -> txt).
                var text = Encoding.UTF8.GetString(fileData);
                
                // Обрезаем, если слишком длинный (ограничение URL)
                if (text.Length > 1000) text = text.Substring(0, 1000);

                // QuickChart API Format
                var qcConfig = new 
                {
                    format = "png",
                    width = 500,
                    height = 500,
                    fontScale = 15,
                    scale = "linear",
                    removeStopwords = true,
                    minWordLength = 4,
                    text = text
                };

                // В реальном случае лучше использовать POST запрос к QuickChart, 
                // но здесь вернем ссылку на генерацию
                return $"https://quickchart.io/wordcloud?text={Uri.EscapeDataString(text)}";
            }
            catch
            {
                return "Error generating cloud";
            }
        }
    }

    public class AnalysisRequest { public Guid FileId { get; set; } }
    
    public class FileMetadataDto 
    { 
        public string StudentName { get; set; } 
        public string TaskId { get; set; } 
    }

    public class AnalysisReport
    {
        public Guid ReportId { get; set; }
        public Guid FileId { get; set; }
        public string TaskId { get; set; }
        public string StudentName { get; set; }
        public string FileHash { get; set; }
        public bool IsPlagiarism { get; set; }
        public string WordCloudUrl { get; set; }
        public DateTime AnalyzedAt { get; set; }
    }
}