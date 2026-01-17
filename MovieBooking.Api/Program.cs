using Microsoft.EntityFrameworkCore;
using MovieBooking.Core.Interfaces;
using MovieBooking.Infrastructure.BackgroundJobs;
using MovieBooking.Infrastructure.Hubs;
using MovieBooking.Infrastructure.Data;
using MovieBooking.Infrastructure.Services;
using MovieBooking.Core.Entities;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSignalR();
// builder.Services.AddEndpointsApiExplorer();
// Swagger Removed due to .NET 10 incompatibility
// builder.Services.AddSwaggerGen();

// Setup DbContext (Default: SQLite for local dev. Switch to Npgsql for Prod)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=moviebooking.db"));
    // options.UseNpgsql(...)); // PRODUCTION

// Redis Configuration (Default: Mock for local dev)
// builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect("localhost"));
// builder.Services.AddSingleton<IDistributedLockService, RedisLockService>(); 
// builder.Services.AddStackExchangeRedisCache(options => options.Configuration = "localhost"); // For Idempotency in Prod

builder.Services.AddSingleton<IDistributedLockService, MockRedisLockService>(); // DEV
builder.Services.AddDistributedMemoryCache(); // For Idempotency in Dev

builder.Services.AddScoped<ISeatService, SeatService>();
builder.Services.AddHostedService<SeatCleanupHelper>(); // Fallback Polling (Works in Dev/Prod)
// builder.Services.AddHostedService<RedisKeyExpiredSubscriber>(); // Reactive (Requires Real Redis - Uncomment for Prod)

// Add CORS for the frontend simulation
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyMethod().AllowAnyHeader().AllowCredentials().SetIsOriginAllowed(_ => true));
});

builder.Services.AddSignalR();
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
// Swagger Removed
// if (app.Environment.IsDevelopment())
// {
//      // app.UseSwagger();
//      // app.UseSwaggerUI();
// }

app.UseCors("AllowAll");
app.UseMiddleware<MovieBooking.Api.Middleware.IdempotencyMiddleware>();
app.UseDefaultFiles(); // Enables index.html at root
app.UseStaticFiles(); // Serve index.html
app.UseAuthorization();
app.MapControllers();
app.MapHub<SeatHub>("/seatHub");


// Auto-migrate and Seed Data on Startup (for demo purposes)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated(); // Simple creation for SQLite (skip migrations for speed)
    
    // Seed one show and some seats if empty
    if (!context.Shows.Any())
    {
        var show = new Show
        {
            Id = Guid.NewGuid(),
            Title = "Inception",
            StartTime = DateTime.UtcNow.AddHours(2)
        };
        context.Shows.Add(show);
        
        // 5 Rows, 10 Seats each
        for (int r = 1; r <= 5; r++)
        {
            for (int n = 1; n <= 10; n++)
            {
                context.Seats.Add(new Seat
                {
                    Id = Guid.NewGuid(),
                    ShowId = show.Id,
                    Row = ((char)('A' + r - 1)).ToString(),
                    Number = n,
                    Status = SeatStatus.Available
                });
            }
        }
        context.SaveChanges();
    }
}

app.Run();
