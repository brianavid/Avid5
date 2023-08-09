using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using NLog;

namespace Avid.Cec.Controllers
{
	public class CecController : Controller
	{
		public static string? ClientPath { get; private set; }

		static Logger logger = LogManager.GetCurrentClassLogger();

		static IHostApplicationLifetime? _appLifetime;

		public static void Initialize(IHostApplicationLifetime appLifetime, string clientPath)
		{
			_appLifetime = appLifetime;
			ClientPath = clientPath;
		}

		static string  RunCECControlProcess(string command, bool wait)
		{
			if (string.IsNullOrEmpty(ClientPath) ||
				string.IsNullOrEmpty(command)) return "";

			using (Process myProcess = new Process())
			{
				logger.Info($"RunCECControlProcess: '{command}'");
				myProcess.StartInfo.FileName = ClientPath;
				myProcess.StartInfo.UseShellExecute = false;
				myProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				myProcess.StartInfo.CreateNoWindow = true;
				myProcess.StartInfo.Arguments = "-s -d 1";
				myProcess.StartInfo.UseShellExecute = false;
				myProcess.StartInfo.RedirectStandardInput = true;
				myProcess.StartInfo.RedirectStandardOutput = true;

				myProcess.Start();

				StreamWriter myStreamWriter = myProcess.StandardInput;
				myStreamWriter.Write(command);
				myStreamWriter.Close();

				string result = "";
				if (wait)
				{
					result = myProcess.StandardOutput.ReadToEnd().Trim();
					myProcess.WaitForExit();
					logger.Info($"  Result: {myProcess.ExitCode} '{result}'");
					result = myProcess.ExitCode.ToString() + ": " + result;
				}

				return result;
				
			}
		}

		public ContentResult Do(string parm)
		{
			RunCECControlProcess(parm, false);
			return Content("");
		}

		public ContentResult Get(string parm)
		{
			return Content(RunCECControlProcess(parm, true));
		}

		public ContentResult Probe()
		{
			return Content("OK");
		}

		public ContentResult Exit()
		{
			if (_appLifetime != null) _appLifetime.StopApplication();
			return Content("OK");
		}
	}
}
