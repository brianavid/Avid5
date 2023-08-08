using NLog;
using NLog.Web;

using Avid.Cec.Controllers;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting.Server;

var builder = WebApplication.CreateBuilder();

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Cec}/{action=Get}/{parm?}");

var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

if (args.Length != 0)
{
	if (System.IO.File.Exists(args[0]))
	{
		logger.Info($"Avid.CEC Started: '{args[0]}'");
		CecController.Initialize(app.Lifetime, args[0]);
		app.Start();

		var server = app.Services?.GetService<IServer>();
		var addressFeature = server?.Features.Get<IServerAddressesFeature>();

		if (addressFeature != null)
		{
			foreach (var address in addressFeature.Addresses)
			{
				logger.Info("Kestrel is listening on address: " + address);
			}
		}

		app.WaitForShutdown();
		logger.Info($"Avid.CEC stopped");
	}
	else
	{
		logger.Info($"Avid.CEC can't access: '{args[0]}'");
	}
}
else
{
	logger.Info($"Avid.CEC no EXE file specified");
}

