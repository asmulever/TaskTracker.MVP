using TaskTracker.Application.Abstractions;
using TaskTracker.Application.Services;
using TaskTracker.Infrastructure.DependencyInjection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddInfrastructure();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TaskTracker API v1");
        options.RoutePrefix = string.Empty;
    });
}

app.UseCors("Frontend");
app.UseAuthorization();
app.MapControllers();

var frontendPath = Path.GetFullPath(
    Path.Combine(app.Environment.ContentRootPath, "..", "..", "frontend", "task-tracker-ui"));

if (Directory.Exists(frontendPath))
{
    var frontendFiles = new PhysicalFileProvider(frontendPath);

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = frontendFiles
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = frontendFiles
    });

    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = frontendFiles
    });
}

app.Run();
