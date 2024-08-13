using ImageProjectBackend.Data;
using ImageProjectBackend.Models;
using ImageProjectBackend.Services;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);


builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: false, reloadOnChange: true);

Console.WriteLine(builder.Configuration.GetConnectionString("ImageDb"));
builder.Services.AddSqlServer<ImageDbContext>(builder.Configuration.GetConnectionString("ImageDb"));

builder.Services.AddScoped<ArchiveManager>();

builder.Services.AddAntiforgery();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        _ = builder.WithOrigins("https://localhost:7233")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

WebApplication app = builder.Build();

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseCors();

app.MapPost("/api/archive/start", async (ArchiveManager manager, HttpContext context) =>
{
    ArchiveRequest? request = await context.Request.ReadFromJsonAsync<ArchiveRequest>();

    Console.WriteLine($"DEBUG: /api/archive/start hit: request: {request.StartDate} {request.EndDate}");

    if(request == null)
    {
        return Results.BadRequest();
    }

    Guid jobId = manager.StartArchive(request);

    if(jobId == Guid.Empty)
    {
        return Results.Problem("Error creating new archive job.");
    }

    ArchiveRequest job = manager.GetJob(jobId);

    return Results.Ok(job); //TODO: 'Created' response?
});

app.MapGet("/api/archive/status/{jobId}", (ArchiveManager manager, Guid jobId) =>
{
    try
    {
        ArchiveRequest job = manager.GetJob(jobId);

        return Results.Ok(job);
    }
    catch(KeyNotFoundException exception)
    {
        return Results.NotFound(exception.Message);
    }
    catch(Exception exception)
    {
        return Results.Problem(exception.Message);
    }
});

app.MapGet("/api/archive/download/{jobId}", (ArchiveManager manager, Guid jobId) =>
{
    try
    {
        string filePath = manager.GetFilePath(jobId);

        FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

        return Results.File(fileStream, "application/zip", $"{jobId}.zip");
    }
    catch(Exception exception)
    {
        return Results.Problem(exception.Message);
    }
});

app.MapGet("/api/images", async (ImageDbContext dbContext) =>
{
    List<Image> images = await dbContext.Images.ToListAsync();

    //TODO: Implement null checking for images and return decision tree
    return Results.Ok(images);
});

app.MapGet("/api/images/filter", async (ImageDbContext dbContext, DateTime startDate, DateTime endDate) =>
{
    List<Image> images = await dbContext.Images.Where(i => i.DateTime >= startDate && i.DateTime <= endDate).ToListAsync();

    //TODO: Implement null checking for images and return decision tree
    return Results.Ok(images);
});

app.MapGet("/api/images/paginated", async (ImageDbContext dbContext, DateTime startDate, DateTime endDate, int pageIndex, int pageSize) =>
{
    var images = await dbContext.Images
        .Where(i => i.DateTime >= startDate && i.DateTime <= endDate)
        .OrderBy(i => i.Id)
        .Skip(pageIndex * pageSize)
        .Take(pageSize)
        //.Select(i => new {i.Id}) //return just Id
        .ToListAsync();

    return Results.Ok(images);
});

app.MapGet("/api/images/{id}", async (ImageDbContext dbContext, long id) =>
{
    var image = await dbContext.Images.FindAsync(id);

    if(image is null)
    {
        return Results.NotFound();
    }

    var fileStream = new FileStream(image.FilePath, FileMode.Open, FileAccess.Read);

    return Results.File(fileStream, "image/jpeg"); //TODO: Other image extensions
});

app.Run();