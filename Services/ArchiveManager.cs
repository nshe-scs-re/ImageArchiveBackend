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

        Jobs[jobId].JobId = jobId;

        Task.Run(async () =>
        {
            try
            {
                await this.ProcessArchiveRequest(jobId);
            }
            catch(Exception exception)
            {
                request.Status = ArchiveRequest.ArchiveStatus.Failed;
                request.AddError($"Processing failed: {exception.Message}");
            }
        });

        return jobId;
    }

    public ArchiveRequest GetJob(Guid jobId)
    {
        return Jobs.TryGetValue(jobId, out ArchiveRequest? request)
            ? request
            : throw new KeyNotFoundException($"No archive process found with ID: {jobId}");
    }

    public string GetFilePath(Guid jobId)
    {
        if(Jobs.TryGetValue(jobId, out ArchiveRequest? request))
        {
            if(request.Status is ArchiveRequest.ArchiveStatus.Completed)
            {
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Archives", $"{jobId}.zip");

                return File.Exists(filePath) ? filePath : throw new FileNotFoundException("The file does not exist.");
            }
            else
            {
                throw new InvalidOperationException("The archiving process has not completed."); //TODO: Move scope of determining process status outside of this function
            }
        }
        else
        {
            throw new KeyNotFoundException($"No archive process found with ID: {jobId}");
        }
    }

    public async Task ProcessArchiveRequest(Guid jobId)
    {
        //Console.WriteLine($"DEBUG [ArchiveManager.cs] [ProcessArchiveRequest]: Archiving process started...");

        Stopwatch stopwatch = Stopwatch.StartNew();

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

                    Parallel.ForEach(images, (image) =>
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

                            }
                            else
                            {
                                Console.WriteLine($"DEBUG [ArchiveManager.cs]: {image.FilePath} does not exist."); //TODO: Implement logging
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
                            Console.WriteLine($"DEBUG [ArchiveManager.cs]: Exception: {exception.Message}"); //TODO: Implement logging

                            request.AddError(exception.Message);
                        }
                    }
                }
            }

            stopwatch.Stop();

            Console.WriteLine($"DEBUG: Archiving complete. Elapsed Time: {stopwatch.Elapsed}");

            Jobs[jobId].Status = ArchiveRequest.ArchiveStatus.Completed;
        }
    }
}
