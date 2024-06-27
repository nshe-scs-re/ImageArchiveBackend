using ImageProjectBackend.Data;
using ImageProjectBackend.Models;
using ImageProjectBackend.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSqlServer<ImageDbContext>(builder.Configuration.GetConnectionString("ImageDatabase"));

builder.Services.AddScoped<ArchiveManager>();

builder.Services.AddAntiforgery();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.WithOrigins("https://localhost:7233")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseCors();

app.MapPost("/api/archive/start", async (ArchiveManager manager, HttpContext context) =>
{
    var request = await context.Request.ReadFromJsonAsync<ArchiveRequest>();

    if (request == null)
    {
        return Results.BadRequest();
    }

    var jobId = manager.StartArchive(request);

    if (jobId == Guid.Empty)
    {
        return Results.Problem("Error creating new archive job.");
    }

    var result = new
    {
        JobId = jobId,
        //TODO: Potentially return more information
    };

    return Results.Ok(result);
});

app.MapGet("/api/archive/status/{jobId}", (ArchiveManager manager, Guid jobId) =>
{
    try
    {
        var job = manager.GetJob(jobId);

        return Results.Ok(job.Status.ToString());
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(exception.Message);
    }
    catch (Exception exception)
    {
        return Results.Problem(exception.Message);
    }
});

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

app.Run();