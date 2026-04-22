using ImageRecognition.Dtos;

namespace ImageRecognition.Services
{
    /// <summary>
    /// OCR 图像识别服务接口
    /// </summary>
    public interface IOcrService
    {
        /// <summary>
        /// 识别图片中的文字
        /// </summary>
        /// <param name="imagePath">图片路径或 URL</param>
        /// <returns>识别结果</returns>
        Task<OcrResponse> RecognizeAsync(string imagePath);
    }
}
