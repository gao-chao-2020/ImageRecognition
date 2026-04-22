using Serilog;
using Serilog.Events;
using Serilog.Context;
using ImageRecognition.Services;

// 配置 Serilog - 输出到控制台和文件
var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "log-.txt");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}",
        flushToDiskInterval: TimeSpan.FromSeconds(1),
        shared: true)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // 使用 Serilog
    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Image Recognition API", Version = "v1" });
    });

    // Register OCR service - Singleton 因为 OCR 引擎初始化开销大
    builder.Services.AddSingleton<IOcrService, OcrService>();

    // Configure CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    var app = builder.Build();

    // Configure Swagger - always enable for local development
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.RoutePrefix = string.Empty;
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Image Recognition API v1");
    });

    app.UseHttpsRedirection();
    app.UseCors("AllowAll");
    app.UseAuthorization();

    // CorrelationId 中间件 - 为每个请求生成唯一 ID
    app.Use(async (context, next) =>
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N")[..12]; // 简短 ID
            context.Response.Headers["X-Correlation-Id"] = correlationId;
        }

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next();
        }
    });

    app.MapControllers();

    // 日志查看器页面
    app.MapGet("/logs", async context =>
    {
        var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogsViewer.html");
        if (File.Exists(htmlPath))
        {
            var html = await File.ReadAllTextAsync(htmlPath);
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(html);
        }
        else
        {
            await context.Response.WriteAsync("LogsViewer.html not found");
        }
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "应用程序启动失败");
}
finally
{
    Log.CloseAndFlush();
}
