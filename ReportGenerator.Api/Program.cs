using Microsoft.OpenApi.Models;
using ReportGenerator.Core.Configuration;
using ReportGenerator.Api.Utilities;
using ReportGenerator.Api.Services;
using ReportGenerator.Core.Errors;

var builder = WebApplication.CreateBuilder(args);

// הוספת שירותי ליבה
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ReportGenerator API",
        Version = "v1",
        Description = "API להפקת דוחות PDF ו-Excel באמצעות תבניות HTML"
    });
});

// קריאת הגדרות מקובץ התצורה
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// רישום רק השירותים הנדרשים באמת
builder.Services.AddSingleton<IParameterConverter, ParameterConverter>();
builder.Services.AddSingleton<IFileSystemService, FileSystemService>();

// אתחול SimpleLogger פעם אחת בלבד
var connectionString = builder.Configuration.GetConnectionString("ConnectionString");
var logsFolder = Path.Combine(Path.GetTempPath(), "ReportLogs");
SimpleLogger.Initialize(connectionString, logsFolder);

var app = builder.Build();

// הפעלת Swagger בסביבת פיתוח ובסביבת ייצור
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ReportGenerator API v1"));

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// יצירת תיקיות בסיסיות בעת הפעלת האפליקציה
var fileSystemService = app.Services.GetRequiredService<IFileSystemService>();
var baseTemplatesFolder = app.Configuration["ReportSettings:BaseTemplatesFolder"];
var outputFolder = app.Configuration["ReportSettings:OutputFolder"];

if (!string.IsNullOrEmpty(baseTemplatesFolder))
{
    fileSystemService.EnsureDirectoryExists(baseTemplatesFolder);
}

if (!string.IsNullOrEmpty(outputFolder))
{
    fileSystemService.EnsureDirectoryExists(outputFolder);
}

app.Run();
