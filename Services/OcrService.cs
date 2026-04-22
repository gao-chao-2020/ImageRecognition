using ImageRecognition.Dtos;
using Microsoft.Extensions.Logging;
using RapidOcrNet;
using SkiaSharp;

namespace ImageRecognition.Services
{
    /// <summary>
    /// OCR 图像识别服务实现（使用 RapidOcrNet - PP-OCRv5）
    /// </summary>
    public class OcrService : IOcrService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _modelsDir;
        private readonly ILogger<OcrService> _logger;
        private RapidOcr? _ocrEngine;
        private bool _disposed;
        private bool _initialized;
        private readonly object _lock = new();

        /// <summary>
        /// 初始化 OCR 服务
        /// </summary>
        /// <param name="logger">日志服务</param>
        public OcrService(ILogger<OcrService> logger)
        {
            _logger = logger;
            _modelsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "v5");
            _httpClient = new HttpClient();
        }

        private void InitializeEngine()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    _logger.LogInformation("OCR 模型目录：{ModelsDir}", _modelsDir);
                    _logger.LogInformation("应用程序目录：{BaseDirectory}", AppDomain.CurrentDomain.BaseDirectory);

                    if (!Directory.Exists(_modelsDir))
                    {
                        _logger.LogError("OCR 模型目录不存在：{ModelsDir}", _modelsDir);
                        throw new DirectoryNotFoundException($"OCR 模型目录不存在：{_modelsDir}");
                    }

                    var detModel = Path.Combine(_modelsDir, "ch_PP-OCRv5_det_mobile.onnx");
                    var clsModel = Path.Combine(_modelsDir, "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx");
                    var recModel = Path.Combine(_modelsDir, "ch_PP-OCRv5_rec_mobile.onnx");
                    var keysFile = Path.Combine(_modelsDir, "ppocrv5_dict.txt");

                    CheckModelFile(detModel);
                    CheckModelFile(clsModel);
                    CheckModelFile(recModel);
                    CheckModelFile(keysFile);

                    _logger.LogInformation("开始创建 RapidOcr 实例...");
                    var ocrEngine = new RapidOcr();
                    _logger.LogInformation("开始初始化模型...");
                    ocrEngine.InitModels(detModel, clsModel, recModel, keysFile);
                    _ocrEngine = ocrEngine;
                    _initialized = true;
                    _logger.LogInformation("RapidOcrNet OCR 引擎初始化成功");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RapidOcrNet 引擎初始化失败");
                    // 递归打印所有内部异常
                    var innerEx = ex.InnerException;
                    int depth = 1;
                    while (innerEx != null)
                    {
                        _logger.LogError(innerEx, "内部异常 [{Depth}]", depth);
                        innerEx = innerEx.InnerException;
                        depth++;
                    }
                    throw;
                }
            }
        }

        private void CheckModelFile(string path)
        {
            if (!File.Exists(path))
            {
                _logger.LogError("模型文件不存在：{Path}", path);
                throw new FileNotFoundException($"模型文件不存在：{path}");
            }
        }

        /// <summary>
        /// 识别图片中的文字
        /// </summary>
        /// <param name="imagePath">图片路径或 URL</param>
        /// <returns>识别结果</returns>
        public async Task<OcrResponse> RecognizeAsync(string imagePath)
        {
            var response = new OcrResponse();
            string? tempFilePath = null;
            bool isTempFile = false;

            try
            {
                // 如果是 HTTP URL，下载到临时文件；否则直接使用本地文件路径
                if (imagePath.StartsWith("http://") || imagePath.StartsWith("https://"))
                {
                    _logger.LogInformation("OCR 识别图片 URL: {ImageUrl}", imagePath);
                    tempFilePath = await DownloadImageToTempFileAsync(imagePath);
                    isTempFile = true;
                }
                else
                {
                    _logger.LogInformation("OCR 识别本地图片：{ImagePath}", imagePath);
                    tempFilePath = imagePath;
                    isTempFile = false;
                }

                if (string.IsNullOrEmpty(tempFilePath) || !File.Exists(tempFilePath))
                {
                    _logger.LogError("图片文件不存在：{ImagePath}", imagePath);
                    response.Success = false;
                    response.Error = "图片文件不存在";
                    return response;
                }

                try
                {
                    var ocrResult = RecognizeWithRapidOcr(tempFilePath);
                    response.Success = true;
                    response.TextBlocks = ConvertOcrResult(ocrResult);
                    _logger.LogInformation("OCR 识别完成，识别到 {Count} 个文本块", response.TextBlocks.Count);
                }
                finally
                {
                    // 只删除下载的临时文件，不删除原始文件
                    if (isTempFile && File.Exists(tempFilePath))
                    {
                        try
                        {
                            File.Delete(tempFilePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug("删除临时文件失败：{Message}", ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR 识别失败");
                response.Success = false;
                response.Error = ex.Message;
            }

            return response;
        }

        /// <summary>
        /// 从 URL 下载图片到临时文件
        /// </summary>
        /// <param name="imageUrl">图片 URL</param>
        /// <returns>临时文件路径，失败返回 null</returns>
        private async Task<string?> DownloadImageToTempFileAsync(string imageUrl)
        {
            try
            {
                var tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                var tempFilePath = Path.Combine(tempDir, Guid.NewGuid() + ".jpg");
                var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
                await File.WriteAllBytesAsync(tempFilePath, imageBytes);
                _logger.LogInformation("图片已下载到临时文件：{TempFilePath}", tempFilePath);
                return tempFilePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "下载图片失败：{ImageUrl}", imageUrl);
                return null;
            }
        }

        /// <summary>
        /// 使用 RapidOcrNet 识别图片
        /// </summary>
        private OcrResult RecognizeWithRapidOcr(string imagePath)
        {
            try
            {
                if (!_initialized)
                {
                    InitializeEngine();
                }

                if (_ocrEngine == null || _disposed)
                {
                    _logger.LogError("RapidOcrNet 引擎未初始化或已释放");
                    throw new InvalidOperationException("OCR 引擎未初始化");
                }

                if (!File.Exists(imagePath))
                {
                    _logger.LogError("图片文件不存在：{ImagePath}", imagePath);
                    throw new FileNotFoundException($"图片文件不存在：{imagePath}");
                }

                using var bitmap = SKBitmap.Decode(imagePath);
                var result = _ocrEngine.Detect(bitmap, RapidOcrOptions.Default);
                _logger.LogInformation("RapidOcrNet 识别完成，TextBlocks: {Count}", result.TextBlocks.Length);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RapidOcrNet 识别失败");
                throw;
            }
        }

        private List<OcrTextBlock> ConvertOcrResult(OcrResult ocrResult)
        {
            var blocks = new List<OcrTextBlock>();

            foreach (var textBlock in ocrResult.TextBlocks)
            {
                var block = new OcrTextBlock
                {
                    Text = string.Join("", textBlock.Chars ?? Array.Empty<string>()),
                    BoxPoints = textBlock.BoxPoints.Select(p => new OcrPoint
                    {
                        X = (int)p.X,
                        Y = (int)p.Y
                    }).ToList()
                };
                blocks.Add(block);
            }

            return blocks;
        }

        /// <summary>
        /// 释放 OCR 引擎资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_ocrEngine != null && _initialized)
                {
                    try
                    {
                        _ocrEngine.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Dispose OCR 引擎失败：{Message}", ex.Message);
                    }
                    _ocrEngine = null;
                }
                _disposed = true;
            }
        }
    }
}
