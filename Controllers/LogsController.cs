using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;

namespace ImageRecognition.Controllers
{
    /// <summary>
    /// 日志查看控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class LogsController : ControllerBase
    {
        /// <summary>
        /// 获取日志文件列表
        /// </summary>
        [HttpGet("list")]
        public ActionResult<List<string>> GetLogFiles()
        {
            var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logsDir))
            {
                return Ok(new List<string>());
            }

            var files = Directory.GetFiles(logsDir, "log-*.txt")
                .Select(Path.GetFileName)
                .OrderByDescending(f => f)
                .ToList();

            return Ok(files);
        }

        /// <summary>
        /// 获取日志文件内容
        /// </summary>
        /// <param name="file">文件名</param>
        /// <param name="errorsOnly">是否只看错误日志</param>
        /// <param name="correlationId">按 CorrelationId 筛选</param>
        [HttpGet("content")]
        public ActionResult<LogContentResponse> GetLogContent(string? file, bool errorsOnly = false, string? correlationId = null)
        {
            if (string.IsNullOrEmpty(file))
            {
                return Ok(new LogContentResponse { File = "", Lines = Array.Empty<string>(), Size = 0 });
            }

            var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            var filePath = Path.Combine(logsDir, file);

            if (!System.IO.File.Exists(filePath))
            {
                return Ok(new LogContentResponse { File = file, Lines = Array.Empty<string>(), Size = 0, Error = "文件不存在" });
            }

            var fileInfo = new FileInfo(filePath);

            // 使用 FileShare 读取，避免文件被锁定
            var lines = new List<string>();
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fileStream))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line != null)
                        lines.Add(line);
                }
            }

            // 分组：每条日志 + 其异常堆栈
            var grouped = new List<LogEntry>();
            LogEntry? currentEntry = null;
            foreach (var line in lines)
            {
                // 匹配日志格式：2026-04-16 09:03:39.229 +08:00 [INF] [0bf0aa8890db] 消息
                // 或者：2026-04-16 09:06:30.676 +08:00 [INF] [] Now listening
                var match = System.Text.RegularExpressions.Regex.Match(line, @"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \+\d{2}:\d{2}) \[(\w+)\] \[([a-f0-9]{12}|\s*)\] (.*)$");
                if (match.Success)
                {
                    var corrId = match.Groups[3].Value.Trim();
                    currentEntry = new LogEntry
                    {
                        HeaderLine = line,
                        Timestamp = match.Groups[1].Value,
                        Level = match.Groups[2].Value,
                        CorrelationId = corrId,
                        ExceptionLines = new List<string>()
                    };
                    grouped.Add(currentEntry);
                }
                else if (currentEntry != null && !string.IsNullOrWhiteSpace(line))
                {
                    // 异常堆栈行
                    currentEntry.ExceptionLines.Add(line);
                }
            }

            // 按 CorrelationId 筛选（分组后筛选，保留异常堆栈）
            if (!string.IsNullOrEmpty(correlationId))
            {
                grouped = grouped.Where(e => e.CorrelationId == correlationId).ToList();
            }

            // 只看错误日志时，过滤掉非错误级别的日志
            if (errorsOnly)
            {
                grouped = grouped.Where(e => e.Level == "ERR" || e.Level == "FTL").ToList();
            }

            // 按时间倒序（最新的在前）
            grouped.Sort((a, b) =>
            {
                // 时间戳格式：2026-04-16 09:08:50.870 +08:00
                // 直接字符串比较即可，格式是固定的
                return string.Compare(b.Timestamp, a.Timestamp, StringComparison.Ordinal);
            });

            // 扁平化返回
            var resultLines = new List<string>();
            foreach (var entry in grouped)
            {
                resultLines.Add(entry.HeaderLine);
                resultLines.AddRange(entry.ExceptionLines);
            }

            return Ok(new LogContentResponse
            {
                File = file,
                Lines = resultLines.TakeLast(2000).ToArray(),
                Size = fileInfo.Length,
                CorrelationId = correlationId
            });
        }
    }

    /// <summary>
    /// 日志条目（包含异常堆栈）
    /// </summary>
    public class LogEntry
    {
        public string HeaderLine { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public List<string> ExceptionLines { get; set; } = new();
    }

    /// <summary>
    /// 日志内容响应
    /// </summary>
    public class LogContentResponse
    {
        /// <summary>
        /// 文件名
        /// </summary>
        public string File { get; set; } = string.Empty;

        /// <summary>
        /// 日志行
        /// </summary>
        public string[] Lines { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// 当前筛选的 CorrelationId
        /// </summary>
        public string? CorrelationId { get; set; }
    }
}
