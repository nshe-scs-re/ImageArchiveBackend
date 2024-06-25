using ImageProjectBackend.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSqlServer<ImageDbContext>(builder.Configuration.GetConnectionString("ImageDatabase"));

builder.Services.AddAntiforgery();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapGet("/api/images", async (ImageDbContext dbContext) =>
{
    var images = await dbContext.Images.ToListAsync();

    //TODO: Implement null checking for images and return decision tree
    return Results.Ok(images);
});

app.MapGet("/api/images/filter", async (ImageDbContext dbContext, DateTime startDate, DateTime endDate) => 
{
    var images = await dbContext.Images.Where(i => i.DateTime >= startDate && i.DateTime <= endDate).ToListAsync();

    //TODO: Implement null checking for images and return decision tree
    return Results.Ok(images);
});

//TODO: Potentially change from GET to POST
app.MapGet("api/images/zip", async (ImageDbContext dbContext, DateTime startDate, DateTime endDate) =>
{
    //TODO: Remove benchmarking code
    Stopwatch stopwatch = Stopwatch.StartNew();
    
    var images = await dbContext.Images
        .Where(i => i.DateTime >= startDate && i.DateTime <= endDate)
        .ToListAsync();

    var zipFilePath = ""; //TODO: Get from configuration

    var exceptions = new ConcurrentBag<Exception>();

    using (FileStream zipToOpen = new FileStream(zipFilePath, FileMode.Create))
    {
        using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
        {
            Object archiveLock = new Object();

            Parallel.ForEach(images, (image) =>
            {
                var year = image.DateTime.Year;
                var month = $"{image.DateTime:MMM}";
                var day = $"{image.DateTime:dd}";

                try
                {
                    if (File.Exists(image.FilePath))
                    {
                        lock (archiveLock)
                        {
                            //TODO: Get natural file extension for image
                            ZipArchiveEntry entry = archive.CreateEntry($"{year}/{month}/{day}/{day} {month} {year} {image.DateTime:hh.mmtt}.jpg");

                            using (FileStream fileStream = new FileStream(image.FilePath, FileMode.Open, FileAccess.Read))
                            {
                                using (Stream entryStream = entry.Open())
                                {
                                    fileStream.CopyTo(entryStream);
                                }
                            }
                        }
                        Console.WriteLine($"{image.FilePath} added to zip."); //TODO: Remove
                    }
                    else
                    {
                        Console.WriteLine($"{image.FilePath} does not exist.");
                    }
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            });

            if (!exceptions.IsEmpty)
            {
                foreach (var exception in exceptions)
                {
                    Console.WriteLine($"Exception: {exception.Message}");
                }
                return Results.Problem("There were errors during file processing. Check logs for details.");
            }
        }
    }

    stopwatch.Stop();

    Console.WriteLine($"Elapsed Time: {stopwatch.Elapsed}");

    return Results.File(zipFilePath, "application/zip", "images.zip");
});

app.Run();