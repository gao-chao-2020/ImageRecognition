# 图像识别服务

基于 RapidOcrNet (PP-OCRv5) 的 OCR 图像识别 API 服务。

## 功能

- 识别图片中的文字内容
- 支持本地文件路径和网络 URL
- 支持图片文件上传
- 返回识别到的文本块和位置信息

## API 接口

### 1. 识别图片 (POST /api/ocr/recognize)

**请求：**
```json
{
  "imagePath": "http://example.com/image.jpg"
}
```

**响应：**
```json
{
  "success": true,
  "textBlocks": [
    {
      "text": "识别到的文字",
      "boxPoints": [
        { "x": 10, "y": 20 },
        { "x": 100, "y": 20 },
        { "x": 100, "y": 40 },
        { "x": 10, "y": 40 }
      ]
    }
  ],
  "error": null
}
```

### 2. 上传并识别 (POST /api/ocr/upload)

**请求：** multipart/form-data，文件字段名 `file`

**响应：** 同上

## 运行

1. 将 OCR 模型文件放入 `models/v5/` 目录：
   - ch_PP-OCRv5_det_mobile.onnx
   - ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx
   - ch_PP-OCRv5_rec_mobile.onnx
   - ppocrv5_dict.txt

2. 运行服务：
```bash
dotnet run
```

3. 访问 Swagger UI：`http://localhost:5000/swagger`

## 依赖

- .NET 8
- RapidOcrNet 1.0.2 (PP-OCRv5)
- SkiaSharp
