using ImageProjectBackend.Data;
using ImageProjectBackend.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;

namespace ImageProjectBackend.Services;

public class ArchiveManager(ImageDbContext dbContext)
{
    private static readonly ConcurrentDictionary<Guid, ArchiveRequest> Jobs = new();

    public async Task<Guid> StartArchive(ArchiveRequest request)
    {
        var jobId = Guid.NewGuid();

        while(!Jobs.TryAdd(jobId, request))
            jobId = Guid.NewGuid();

        await Task.Run(() => ProcessArchiveRequest(jobId, request));

        return jobId;
    }

    public async Task ProcessArchiveRequest(Guid jobId, ArchiveRequest request) 
    {
        Stopwatch stopwatch = Stopwatch.StartNew(); //TODO: Remove benchmarking code

        var images = await dbContext.Images
            .Where(i => i.DateTime >= request.StartDate && i.DateTime <= request.EndDate)
            .ToListAsync();

        var zipFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Archives", $"{jobId}.zip");

        var exceptions = new ConcurrentBag<Exception>();

        using (FileStream zipToOpen = new FileStream(zipFilePath, FileMode.Create))
        {
            using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
            {
                object archiveLock = new object();

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
                                ZipArchiveEntry entry = archive.CreateEntry($"{year}/{month}/{day}/{day} {month} {year} {image.DateTime:hh.mmtt}.{Path.GetExtension(image.FilePath)}");

                                using (FileStream fileStream = new FileStream(image.FilePath, FileMode.Open, FileAccess.Read))
                                {
                                    using (Stream entryStream = entry.Open())
                                    {
                                        fileStream.CopyTo(entryStream);
                                    }
                                }
                            }
                            Console.WriteLine($"{image.FilePath} added to zip."); //TODO: Remove debug statement
                        }
                        else
                        {
                            Console.WriteLine($"{image.FilePath} does not exist."); //TODO: Implement logging
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
                    throw new Exception("There were errors during file processing. Check logs for details."); //TODO: Implement logging
                }
            }
        }

        stopwatch.Stop(); //TODO: Remove benchmarking code

        Console.WriteLine($"Elapsed Time: {stopwatch.Elapsed}"); //TODO: Remove benchmarking code

        Jobs[jobId].IsCompleted = true;
    }
}
