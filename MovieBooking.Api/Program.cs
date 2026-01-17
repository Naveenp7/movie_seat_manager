using Microsoft.EntityFrameworkCore;
using MovieBooking.Core.Interfaces;
using MovieBooking.Infrastructure.BackgroundJobs;
using MovieBooking.Api.Hubs;
using MovieBooking.Infrastructure.Data;
using MovieBooking.Infrastructure.Services;
using MovieBooking.Core.Entities;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Setup DbContext with SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=moviebooking.db"));

// Register Services
builder.Services.AddScoped<ISeatService, SeatService>();
builder.Services.AddHostedService<SeatCleanupHelper>();

// Add CORS for the frontend simulation
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
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
