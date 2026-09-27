using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HttpOverrides;
using NLog;
using NLog.Web;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Option A: If NPM is on a known static LAN IP (e.g., 192.168.1.50), trust it explicitly:
    // options.KnownProxies.Add(IPAddress.Parse("192.168.1.50"));

    // Option B: For homelab/internal setups, clear the defaults to trust the reverse proxy hop:
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// MUST be placed at the very top of the pipeline, before any routing, auth, or IP checks
app.UseForwardedHeaders();

var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

// Configure the HTTP request pipeline.
app.UseExceptionHandler("/Home/Error");
// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
app.UseHsts();

//app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

logger.Info("-----------------------------------------------------------");
logger.Info("Avid 5 Started");

bool initialisedSuccessfully = false;
try
{
	Config.Initialize(app.Lifetime, app.Environment.ContentRootPath, args.Length == 0 ? null : args[0]);
    Receiver.Initialize();
    Screen.Initialise();
    JRMC.Initialise();
    Running.Initialize();
	Spotify.Initialize();
	JRMC.LoadAndIndexAllAlbums(new string[] { "1", "2" }, DateTime.Now.Hour < 5);   //  Reload album data from JRMC when restarting between midnight and five (i.e. in the overnight restart)
	VideoTV.Initialise();
	JRMC.CloseScreen();

	logger.Info($"Avid 5 Initialised (build {Config.GetBuildDate(Assembly.GetExecutingAssembly())} UTC)");
	initialisedSuccessfully = true;

    app.Start();

	var server = app.Services.GetService<IServer>();
	var addressFeature = server.Features.Get<IServerAddressesFeature>();

	foreach (var address in addressFeature.Addresses)
	{
		Console.WriteLine("Kestrel is listening on address: " + address);
	}

	app.WaitForShutdown();
	Running.Stop();
	logger.Info($"Avid 5 Shutdown restart={Config.Restart}");
	Environment.Exit(Config.Restart ? 0 : 1);
}
catch (Exception ex)
{
    logger.Info(ex, $"Avid 5 Exception: {ex.Message}");
    Console.WriteLine($"Avid 5 Exception: {ex.Message}");
    Environment.Exit(initialisedSuccessfully ? 0 : 1);
}