using ClinicFlow.Api.Agent;
using ClinicFlow.API.Agent;
using ClinicFlow.API.Data;
using ClinicFlow.API.Models;
using ClinicFlow.API.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ClinicDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ClinicDb")));

builder.Services.AddScoped<SchedulingService>();

var aiOptions = builder.Configuration
    .GetSection(AzureOpenAIOptions.SectionName)
    .Get<AzureOpenAIOptions>() ?? new AzureOpenAIOptions();

if (!aiOptions.IsConfigured)
{
    throw new InvalidOperationException(
        "Azure OpenAI is not configured. Set AzureOpenAI:Endpoint, :ApiKey and " +
        ":DeploymentName via 'dotnet user-secrets set'. See README.");
}

builder.Services.AddSingleton(aiOptions);
builder.Services.AddScoped<ChatService>();

builder.Services.AddScoped(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: aiOptions.DeploymentName,
        endpoint: aiOptions.Endpoint,
        apiKey: aiOptions.ApiKey);

    var scheduling = sp.GetRequiredService<SchedulingService>();
    kernelBuilder.Plugins.AddFromObject(new SchedulingPlugin(scheduling), "Scheduling");

    return kernelBuilder.Build();
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
    await SeedData.InitialiseAsync(db);
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/chat", async (
    ChatRequest request,
    ChatService chat,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { error = "Message cannot be empty." });

    try
    {
        var response = await chat.SendAsync(request, ct);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Chat request failed");
        return Results.Ok(new ChatResponse(
            "Sorry, I'm having trouble right now. Could you try again in a moment?",
            request.History ?? []));
    }
});

app.MapGet("/api/dentists", async (ClinicDbContext db, CancellationToken ct) =>
{
    var dentists = await db.Dentists
        .Include(d => d.WorkingHours)
        .ToListAsync(ct);

    return Results.Ok(dentists.Select(d => new
    {
        d.Id,
        d.Name,
        d.Specialty,
        WorkingDays = d.WorkingHours
            .OrderBy(w => w.DayOfWeek)
            .Select(w => new
            {
                Day = w.DayOfWeek.ToString(),
                Start = w.StartTime.ToString("HH:mm"),
                End = w.EndTime.ToString("HH:mm"),
                Break = w.BreakStart is null ? null : $"{w.BreakStart:HH:mm}-{w.BreakEnd:HH:mm}"
            })
    }));
});

app.MapGet("/api/slots", async (
    string date,
    AppointmentType type,
    int? dentistId,
    SchedulingService scheduling,
    CancellationToken ct) =>
{
    if (!DateOnly.TryParse(date, out var parsed))
        return Results.BadRequest(new { error = "Date must be in yyyy-MM-dd format." });

    var slots = await scheduling.GetAvailableSlotsAsync(parsed, type, dentistId, ct);

    return Results.Ok(slots.Select(s => new
    {
        s.DentistId,
        s.DentistName,
        StartUtc = s.StartUtc,
        Time = s.StartUtc.ToString("HH:mm")
    }));
});

app.MapPost("/api/appointments", async (
    BookRequest request,
    SchedulingService scheduling,
    CancellationToken ct) =>
{
    var result = await scheduling.BookAsync(
        request.DentistId, request.PatientName, request.PhoneNumber,
        request.Type, request.StartUtc, ct);

    return result.Success
        ? Results.Ok(result)
        : Results.BadRequest(result);
});

app.MapGet("/api/appointments", async (
    string phone,
    SchedulingService scheduling,
    CancellationToken ct) =>
{
    var found = await scheduling.FindByPhoneAsync(phone, ct);
    return Results.Ok(found);
});

app.MapDelete("/api/appointments/{id:int}", async (
    int id,
    string phone,
    SchedulingService scheduling,
    CancellationToken ct) =>
{
    var result = await scheduling.CancelAsync(id, phone, ct);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.Run();

record BookRequest(
    int DentistId,
    string PatientName,
    string PhoneNumber,
    AppointmentType Type,
    DateTime StartUtc);