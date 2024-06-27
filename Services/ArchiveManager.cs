using ImageProjectBackend.Data;
using ImageProjectBackend.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;

namespace ImageProjectBackend.Services;

public class ArchiveManager(IServiceScopeFactory DbScopeFactory)
{
    private static readonly ConcurrentDictionary<Guid, ArchiveRequest> Jobs = new();

    public Guid StartArchive(ArchiveRequest request)
    {
        Guid jobId = Guid.NewGuid();

        while(!Jobs.TryAdd(jobId, request))
        {
            jobId = Guid.NewGuid();
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await this.ProcessArchiveRequest(jobId);
            }
            catch(Exception exception)
            {
                request.Status = ArchiveRequest.ArchiveStatus.Failed;
                request.AddError($"Processing failed: {exception.Message}");
                Console.WriteLine($"Error in ProcessArchiveRequest: {exception.Message}"); //TODO: Remove debug log
            }
        });

        Console.WriteLine($"Archive job started with Job ID: {jobId}"); //TODO: Remove debug log

        return jobId;
    }

    public ArchiveRequest GetJob(Guid jobId)
    {
        return Jobs.TryGetValue(jobId, out ArchiveRequest? request)
            ? request
            : throw new KeyNotFoundException($"No archive process found with ID: {jobId}");
    }

    public async Task ProcessArchiveRequest(Guid jobId)
    {
        Stopwatch stopwatch = Stopwatch.StartNew(); //TODO: Remove benchmarking code

        ArchiveRequest request = Jobs[jobId];

        Jobs[jobId].Status = ArchiveRequest.ArchiveStatus.Processing;

        using(IServiceScope DbScope = DbScopeFactory.CreateScope())
        {
            ImageDbContext dbContext = DbScope.ServiceProvider.GetRequiredService<ImageDbContext>();

            List<Image> images = await dbContext.Images
                .Where(i => i.DateTime >= request.StartDate && i.DateTime <= request.EndDate)
                .ToListAsync();

            string zipFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Archives", $"{jobId}.zip");

            ConcurrentBag<Exception> exceptions = new ConcurrentBag<Exception>();

            using(FileStream zipToOpen = new FileStream(zipFilePath, FileMode.Create))
            {
                using(ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                {
                    object archiveLock = new object();

                    _ = Parallel.ForEach(images, (image) =>
                    {
                        int year = image.DateTime.Year;
                        string month = $"{image.DateTime:MMM}";
                        string day = $"{image.DateTime:dd}";

                        try
                        {
                            if(File.Exists(image.FilePath))
                            {
                                lock(archiveLock)
                                {
                                    ZipArchiveEntry entry = archive.CreateEntry($"{year}/{month}/{day}/{day} {month} {year} {image.DateTime:hh.mmtt}.{Path.GetExtension(image.FilePath)}");

                                    using(FileStream fileStream = new FileStream(image.FilePath, FileMode.Open, FileAccess.Read))
                                    {
                                        using(Stream entryStream = entry.Open())
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
                        catch(Exception exception)
                        {
                            exceptions.Add(exception);
                        }
                    });

                    if(!exceptions.IsEmpty)
                    {
                        foreach(Exception exception in exceptions)
                        {
                            Console.WriteLine($"Exception: {exception.Message}"); //TODO: Implement logging

                            request.AddError(exception.Message);

                            request.Status = ArchiveRequest.ArchiveStatus.Failed; //TODO: Determine if better to handle here or in AddError()
                        }
                    }
                }
            }

            stopwatch.Stop(); //TODO: Remove benchmarking code

            Console.WriteLine($"Elapsed Time: {stopwatch.Elapsed}"); //TODO: Remove benchmarking code

            Jobs[jobId].Status = ArchiveRequest.ArchiveStatus.Completed;
        }
    }
}
