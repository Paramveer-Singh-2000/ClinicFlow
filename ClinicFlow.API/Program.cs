using ClinicFlow.API.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddDbContext<ClinicDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ClinicDb")));


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
    await SeedData.InitialiseAsync(db);
}
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
}
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
