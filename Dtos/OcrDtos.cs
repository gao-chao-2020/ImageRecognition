namespace ImageRecognition.Dtos
{
    /// <summary>
    /// OCR 识别请求
    /// </summary>
    public class OcrRequest
    {
        /// <summary>
        /// 图片路径或 URL
        /// </summary>
        public string ImagePath { get; set; } = string.Empty;
    }

    /// <summary>
    /// OCR 识别结果
    /// </summary>
    public class OcrResponse
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 识别结果列表
        /// </summary>
        public List<OcrTextBlock> TextBlocks { get; set; } = new();

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// OCR 识别文本块
    /// </summary>
    public class OcrTextBlock
    {
        /// <summary>
        /// 识别到的文本
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// 文本框坐标
        /// </summary>
        public List<OcrPoint> BoxPoints { get; set; } = new();
    }

    /// <summary>
    /// 坐标点
    /// </summary>
    public class OcrPoint
    {
        /// <summary>
        /// X 坐标
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Y 坐标
        /// </summary>
        public int Y { get; set; }
    }
}
