using ImageProjectBackend.Data;
using ImageProjectBackend.Models;
using ImageProjectBackend.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

//TODO: remove appsettings dependency and move secrets to GitHub secrets or env variables
builder.Configuration.AddJsonFile("Properties/appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"Properties/appsettings.{builder.Environment.EnvironmentName}.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

//Console.WriteLine($"DEBUG [Program.cs]: Connection string is: {builder.Configuration.GetConnectionString("ImageDb")}"); //TODO: Use GitHub secrets and/or environment variable

builder.Services.AddSqlServer<ImageDbContext>(builder.Configuration.GetConnectionString("ImageDb"));

builder.Services.AddScoped<ArchiveManager>();

builder.Services.AddAntiforgery(); //TODO: Configure antiforgery options

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        //TODO: Look into further configuration options for added security
        //builder.AllowAnyOrigin()
        builder.WithOrigins("10.176.244.111")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });

});

var app = builder.Build();

app.UseCors();

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapGet("/api/db-verify", async(ImageDbContext dbContext) =>
{
    try
    {
        var canConnect = await dbContext.Database.CanConnectAsync();
        if(canConnect)
        {
            Console.WriteLine($"DEBUG [Program.cs] [/db-verify]: Database connection succeded!");
            return Results.Ok("Database connection succeeded.");
        }
        else
        {
            Console.WriteLine($"DEBUG [Program.cs] [/db-verify]: Database connection failed.");
            return Results.Problem("Database connection failed.");
        }
    }
    catch(Exception ex)
    {
        Console.WriteLine($"DEBUG [Program.cs] [/db-verify]: Database connection failed - {ex.Message}");
        return Results.Problem("Database connection failed.");
    }
});

app.MapPost("/api/archive/start", async (ArchiveManager manager, HttpContext context) =>
{
    // TODO: try-catch wrapper

    ArchiveRequest? request = await context.Request.ReadFromJsonAsync<ArchiveRequest>();

    Console.WriteLine($"DEBUG [Program.cs] [/api/archive/start]: endpoint hit: request: {request.StartDate} {request.EndDate}");

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
    //TODO try-catch wrapper
    List<Image> images = await dbContext.Images.ToListAsync();

    //TODO: Implement null checking for images and return decision tree
    return Results.Ok(images);
});

app.MapGet("/api/images/filter", async (ImageDbContext dbContext, DateTime startDate, DateTime endDate) =>
{
    //TODO: try-catch wrapper
    List<Image> images = await dbContext.Images.Where(i => i.DateTime >= startDate && i.DateTime <= endDate).ToListAsync();

    //TODO: Implement null checking for images and return decision tree
    return Results.Ok(images);
});

app.MapGet("/api/images/paginated", async (ImageDbContext dbContext, DateTime startDate, DateTime endDate, int pageIndex, int pageSize) =>
{
    //TODO: try-catch wrapper
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
    try
    {
        var image = await dbContext.Images.FindAsync(id);
        if(image is null)
        {
            Console.WriteLine($"DEBUG [Program.cs] [/api/images/id]: image is null.");
            return Results.NotFound();
        }
        try
        {
            var fileStream = new FileStream(image.FilePath!, FileMode.Open, FileAccess.Read);
            string extension = Path.GetExtension(image.FilePath)!.ToLowerInvariant();
            string mimeType = extension switch
            {
                ".jpeg" or ".jpg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };

            return Results.File(fileStream, mimeType);
        }
        catch(Exception exception)
        {
            Console.WriteLine($"ERROR [Program.cs] [/api/images/id]: Exception message: {exception.Message}");
            return Results.Problem(exception.Message);
        }

    }
    catch(Exception exception)
    {
        Console.WriteLine($"ERROR [Program.cs] [/api/images/id]: Outer catch hit. Exception message: {exception.Message}");
        throw;
    }
});

app.Run();