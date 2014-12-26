//-----------------------------------------------------------------------------------------------
// Dado.Compass v0.2.0, Copyright 2014 roydukkey, 2014-12-27 (Sat, 27 December 2014).
// Released under the GPL Version 3 license (https://raw.githubusercontent.com/roydukkey/Dado.Compass/master/LICENSE).
//-----------------------------------------------------------------------------------------------

namespace Dado.Compass
{
	using System;
	using System.Web;
	using System.Diagnostics;
	using System.IO;
	using System.Text.RegularExpressions;

	/// <summary>
	///		Providers a handler for request-time compilation of compass files.
	/// </summary>
	public class SingleFileHandler : IHttpHandler
	{
		#region Fields

		private string _sassPath;
		private string _cachePath;

		#endregion Fields

		#region Public Properties

		/// <summary>
		///		Gets a value indicating whether another request can use the IHttpHandler instance.
		/// </summary>
		bool IHttpHandler.IsReusable
		{
			get { return false; }
		}

		#endregion Public Properties

		#region Public Methods

		/// <summary>
		///		Enables processing of HTTP Web requests by a custom HttpHandler that implements the IHttpHandler interface.
		/// </summary>
		/// <param name="context">An HttpContext object that provides references to the intrinsic server objects (for example, Request, Response, Session, and Server) used to service HTTP requests.</param>
		void IHttpHandler.ProcessRequest(HttpContext context)
		{
			// Gather Compass Configuration Variables
			ReadConfiguration(context);

			string baseDir = Path.GetDirectoryName(context.Request.Path).Replace(@"\", "/");
			string fileName = Path.GetFileName(context.Request.Path);
			string command = String.Format(@"compass compile ""{0}"" --trace --debug-info --css-dir ""{1}""",
				(baseDir + "/" + fileName).Trim(new char[] { '\\', '/' }),
				_cachePath.Trim(new char[] { '\\', '/' })
			);

			ProcessStartInfo psi = new ProcessStartInfo()
			{
				FileName = "cmd.exe",
				WorkingDirectory = context.Server.MapPath("~/"),
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
				Arguments = "/c " + command
			};

			using (Process process = new Process { StartInfo = psi }) {
				bool hasError = false;

				// use text/css so line breaks and any other whitespace formatting is preserved
				context.Response.ContentType = "text/css";

				process.ErrorDataReceived += (sender, e) => {
					hasError = e.Data != null;
					RecordDataReceived(e, context);
				};
				process.OutputDataReceived += (sender, e) => {
					RecordDataReceived(e, context);
				};

				// start the process and start reading the standard and error outputs
				process.Start();

				// Report Compilation Results
				{
					OpenRecord(context);

					// Report Command
					context.Response.Write(
@"//	" + command + @"
//
");

					// Process and Report Error
					process.BeginErrorReadLine();

					// Process and Report Output
					process.BeginOutputReadLine();

					// wait for the process to exit
					process.WaitForExit();

					CloseRecord(context);
				}

				// an exit code other than 0 generally means an error
				if (process.ExitCode != 0) {
					context.Response.StatusCode = 500;
				}
				// Report Compiled CSS
				else if (!hasError) {
					string modifyBasePath = baseDir;
					if (!String.IsNullOrEmpty(_sassPath))
						modifyBasePath = modifyBasePath.Replace(_sassPath, "");

					using (StreamReader sr = new StreamReader(
						context.Server.MapPath(
							"~/" + _cachePath + "/" + modifyBasePath + "/" + Path.ChangeExtension(fileName, "css")
						)
					)) {
						context.Response.Write(sr.ReadToEnd());
					}
				}
			}
		}

		#endregion Public Methods

		#region Private Methods

		/// <summary>
		///		Creates an open CSS comment block into the response stream to record Compass compile details.
		/// </summary>
		/// <param name="context">An HttpContext object that provides references to the intrinsic server objects (for example, Request, Response, Session, and Server) used to service HTTP requests.</param>
		private void OpenRecord(HttpContext context)
		{
			context.Response.Write(
@"/*-----------------------------------------------------------------------------------------------
// Dado.Compass v0.2.0, Copyright 2014 roydukkey, 2014-12-27 (Sat, 27 December 2014).
// Released under the GPL Version 3 license (https://raw.githubusercontent.com/roydukkey/Dado.Compass/master/LICENSE).
//-----------------------------------------------------------------------------------------------
//
");
		}

		/// <summary>
		///		Closes the CSS comment block for Commpass compile details.
		/// </summary>
		/// <param name="context"></param>
		private void CloseRecord(HttpContext context)
		{
			context.Response.Write(
@"//
//---------------------------------------------------------------------------------------------*/

");
		}

		/// <summary>
		///		Respond the data recieved by Commpass compile.
		/// </summary>
		/// <param name="e">A DataReceivedEventArgs that contains the event data.</param>
		/// <param name="context">An HttpContext object that provides references to the intrinsic server objects (for example, Request, Response, Session, and Server) used to service HTTP requests.</param>
		private void RecordDataReceived(DataReceivedEventArgs e, HttpContext context)
		{
			// sometimes a random event is received with null data, not sure why - I prefer to leave it out
			if (e.Data != null) {
				context.Response.Write(
@"//	" + e.Data + @"
");
			}
		}

		/// <summary>
		///		Reads configuration from root config.rb
		/// </summary>
		/// <param name="context"></param>
		private void ReadConfiguration(HttpContext context)
		{
			using (StreamReader sr = new StreamReader(
				context.Server.MapPath("~/config.rb")
			)) {
				string config = sr.ReadToEnd();

				// Get SASS Diretory
				Match match = Regex.Match(config, @"sass_dir\s*=\s*(?:""(.*)""|'(.*)')", RegexOptions.IgnoreCase);
				if (match.Success) {
					_sassPath = (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value).Trim(new char[] { '/', '\\' });
				}

				// Get Cache Diretory
				match = Regex.Match(config, @"cache_dir\s*=\s*(?:""(.*)""|'(.*)')", RegexOptions.IgnoreCase);
				if (match.Success) {
					_cachePath = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
				}

			}
		}

		#endregion Private Methods
	}
}