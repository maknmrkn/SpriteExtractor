using System;
using System.Windows.Forms;
using System.Threading.Tasks;
using SpriteExtractor.Views;

namespace SpriteExtractor
{
    internal static class Program
    {
        /// <summary>
        /// Application entry point that configures global error handlers, visual settings, optional smoke-test modes, and starts the UI message loop.
        /// </summary>
        /// <remarks>
        /// - Installs handlers for UI thread, AppDomain, and unobserved task exceptions.
        /// - Enables visual styles and sets text rendering compatibility.
        /// - If the environment variable "SMOKE_TEST" is set to "1", runs a non-UI smoke test and exits.
        /// - If the environment variable "SMOKE_UI" is set to "1", starts the main form and runs its UI automation on shown, then exits after the message loop.
        /// - Otherwise starts the standard main form message loop.
        /// </remarks>
        [STAThread]
        static void Main()
        {
            // مدیریت خطاهای کنترل نشده
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, e) =>
                MessageBox.Show($"خطا در UI: {e.Exception.Message}", "خطای مدیریت‌نشده");
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                MessageBox.Show($"خطا: {(e.ExceptionObject as Exception)?.Message}", "خطای دامنه");

            // Log unobserved task exceptions to console so async fire-and-forget errors are visible
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                try
                {
                    Console.Error.WriteLine($"UnobservedTaskException: {e.Exception}");
                }
                catch { }
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Allow optional smoke-test modes via environment variables
            var smoke = Environment.GetEnvironmentVariable("SMOKE_TEST");
            if (!string.IsNullOrEmpty(smoke) && smoke == "1")
            {
                // Run non-UI smoke test to validate presenter thumbnail flow
                SmokeTest.Run();
                return;
            }

            var smokeUi = Environment.GetEnvironmentVariable("SMOKE_UI");
            if (!string.IsNullOrEmpty(smokeUi) && smokeUi == "1")
            {
                var mf = new MainForm();
                mf.Shown += async (s, e) => await mf.RunUiAutomationAsync();
                Application.Run(mf);
                return;
            }

            Application.Run(new MainForm());
        }
    }
}