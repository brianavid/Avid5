using NLog;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

logger.Info("Avid 5 Started");

Config.Initialize(app.Lifetime, app.Environment.ContentRootPath);
Receiver.Initialize();
Running.Initialize();
Spotify.Initialize();
Security.Initialize();
JRMC.LoadAndIndexAllAlbums(new string[] { "1", "2" }, DateTime.Now.Hour < 5);   //  Reload album data from JRMC when restarting between midnight and five (i.e. in the overnight restart)
VideoTV.Initialise();

app.Run();
