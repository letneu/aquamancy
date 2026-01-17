using Aquamancy.Data;
using Aquamancy.IData;
using Aquamancy.ILogic;
using Aquamancy.Logic;
using Aquamancy.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Read the connection string from appsettings.json (required)
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Default' in configuration. Add it to appsettings.json.");
}

// Register connection factory and repositories
builder.Services.AddSingleton<IDbConnectionFactory>(new MariaDbConnectionFactory(connectionString));
builder.Services.AddScoped<IProbeRepository, ProbeRepository>();
builder.Services.AddScoped<ITemperatureRepository, TemperatureRepository>();

builder.Services.AddHttpClient();

builder.Services.AddScoped<IDiscordNotifierLogic, DiscordNotifierLogic>();
builder.Services.AddScoped<ITemperatureReadingLogic, TemperatureReadingLogic>();
builder.Services.AddSingleton<IErrorTriggerLogic, ErrorTriggerLogic>();

builder.Services.AddHostedService<DiscordNotifierService>();
builder.Services.AddHostedService<DeadManSwitchService>();
var app = builder.Build();

// Ensure tables exist at startup and seed test data when empty
using (var scope = app.Services.CreateScope())
{
    var provRepo = scope.ServiceProvider.GetRequiredService<IProbeRepository>();
    var tempRepo = scope.ServiceProvider.GetRequiredService<ITemperatureRepository>();
    provRepo.EnsureTableExistsAsync().GetAwaiter().GetResult();
    tempRepo.EnsureTableExistsAsync().GetAwaiter().GetResult();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
