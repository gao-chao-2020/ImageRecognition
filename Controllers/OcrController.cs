using ImageRecognition.Dtos;
using ImageRecognition.Services;
using Microsoft.AspNetCore.Mvc;

namespace ImageRecognition.Controllers
{
    /// <summary>
    /// OCR 图像识别控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class OcrController : ControllerBase
    {
        private readonly IOcrService _ocrService;
        private readonly ILogger<OcrController> _logger;

        /// <summary>
        /// OCR 控制器
        /// </summary>
        public OcrController(IOcrService ocrService, ILogger<OcrController> logger)
        {
            _ocrService = ocrService;
            _logger = logger;
        }

        /// <summary>
        /// 识别图片中的文字
        /// </summary>
        /// <param name="request">OCR 请求</param>
        /// <returns>识别结果</returns>
        [HttpPost("recognize")]
        [ProducesResponseType(typeof(OcrResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(OcrResponse), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OcrResponse>> Recognize([FromBody] OcrRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ImagePath))
            {
                return BadRequest(new OcrResponse
                {
                    Success = false,
                    Error = "图片路径不能为空"
                });
            }

            var result = await _ocrService.RecognizeAsync(request.ImagePath);
            return Ok(result);
        }

        /// <summary>
        /// 上传并识别图片
        /// </summary>
        /// <param name="file">图片文件</param>
        /// <returns>识别结果</returns>
        [HttpPost("upload")]
        [ProducesResponseType(typeof(OcrResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(OcrResponse), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OcrResponse>> UploadAndRecognize(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new OcrResponse
                {
                    Success = false,
                    Error = "请上传图片文件"
                });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new OcrResponse
                {
                    Success = false,
                    Error = "不支持的图片格式，请上传 jpg、png、bmp 或 gif 格式的图片"
                });
            }

            try
            {
                var tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                var tempFilePath = Path.Combine(tempDir, Guid.NewGuid() + extension);
                await using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var result = await _ocrService.RecognizeAsync(tempFilePath);

                try
                {
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        System.IO.File.Delete(tempFilePath);
                    }
                }
                catch
                {
                    // 忽略删除临时文件的错误
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上传并识别图片失败");
                return StatusCode(500, new OcrResponse
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }
    }
}
