using ImageProjectBackend.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSqlServer<ImageDbContext>(builder.Configuration.GetConnectionString("ImageDatabase"));

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/api/images", async (ImageDbContext dbContext) =>
{
    var images = await dbContext.Images.ToListAsync();
    return Results.Ok(images);
});

app.MapGet("/api/images/filter", async (ImageDbContext dbContext, DateTime startDate, DateTime endDate) => 
{
    var images = await dbContext.Images.Where(i => i.DateTime >= startDate && i.DateTime <= endDate).ToListAsync();

    return Results.Ok(images);
});

app.Run();