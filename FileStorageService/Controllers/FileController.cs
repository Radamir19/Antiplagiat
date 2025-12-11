using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace FileStorageService.Controllers
{
    [ApiController]
    [Route("api/files")]
    public class FileController : ControllerBase
    {
        // В реальном проекте используйте БД (Postgres/SQLite). Для демо - in-memory.
        private static readonly ConcurrentDictionary<Guid, FileMetadata> _db = new();
        private readonly string _uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");

        public FileController()
        {
            if (!Directory.Exists(_uploadDir)) Directory.CreateDirectory(_uploadDir);
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] UploadRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest("File is empty");

            var fileId = Guid.NewGuid();
            var filePath = Path.Combine(_uploadDir, fileId.ToString() + Path.GetExtension(request.File.FileName));

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }

            var metadata = new FileMetadata
            {
                Id = fileId,
                StudentName = request.StudentName,
                TaskId = request.TaskId,
                OriginalName = request.File.FileName,
                FilePath = filePath,
                UploadedAt = DateTime.UtcNow
            };

            _db.TryAdd(fileId, metadata);

            return Ok(new { FileId = fileId, Message = "File uploaded successfully" });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetFile(Guid id)
        {
            if (!_db.TryGetValue(id, out var metadata))
                return NotFound("File not found");

            if (!System.IO.File.Exists(metadata.FilePath))
                return NotFound("File on disk missing");

            var bytes = await System.IO.File.ReadAllBytesAsync(metadata.FilePath);
            return File(bytes, "application/octet-stream", metadata.OriginalName);
        }
        
        // Эндпоинт для получения метаданных (нужен Анализатору для проверки автора)
        [HttpGet("{id}/meta")]
        public IActionResult GetMeta(Guid id)
        {
            if (!_db.TryGetValue(id, out var metadata)) return NotFound();
            return Ok(metadata);
        }
    }

    public class UploadRequest
    {
        public string StudentName { get; set; }
        public string TaskId { get; set; } // string чтобы не мучиться с Guid парсингом в демо
        public IFormFile File { get; set; }
    }

    public class FileMetadata
    {
        public Guid Id { get; set; }
        public string StudentName { get; set; }
        public string TaskId { get; set; }
        public string OriginalName { get; set; }
        public string FilePath { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}