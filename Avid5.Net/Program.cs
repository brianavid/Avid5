using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using NLog;
using NLog.Web;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

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
	Security.Initialize();
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