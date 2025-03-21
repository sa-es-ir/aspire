// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAzureCosmosDatabase("db");
builder.AddAzureCosmosContainer("entries");
builder.AddCosmosDbContext<TestCosmosContext>("db", configureDbContextOptions =>
{
    configureDbContextOptions.RequestTimeout = TimeSpan.FromSeconds(120);
});

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGet("/", async (Database db, Container container) =>
{
    // Add an entry to the database on each request.
    var newEntry = new Entry() { Id = Guid.NewGuid().ToString() };
    await container.CreateItemAsync(newEntry);

    var entries = new List<Entry>();
    var iterator = container.GetItemQueryIterator<Entry>(requestOptions: new QueryRequestOptions() { MaxItemCount = 5 });

    var batchCount = 0;
    while (iterator.HasMoreResults)
    {
        batchCount++;
        var batch = await iterator.ReadNextAsync();
        foreach (var entry in batch)
        {
            entries.Add(entry);
        }
    }

    return new
    {
        batchCount = batchCount,
        totalEntries = entries.Count,
        entries = entries
    };
});

app.MapGet("/ef", async (TestCosmosContext context) =>
{
    await context.Database.EnsureCreatedAsync();

    context.Entries.Add(new EntityFrameworkEntry());
    await context.SaveChangesAsync();

    return await context.Entries.ToListAsync();
});

app.Run();

public class Entry
{
    [JsonProperty("id")]
    public string? Id { get; set; }
}

public class TestCosmosContext(DbContextOptions<TestCosmosContext> options) : DbContext(options)
{
    public DbSet<EntityFrameworkEntry> Entries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EntityFrameworkEntry>()
            .HasPartitionKey(e => e.Id);
    }
}

public class EntityFrameworkEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
