using System;
using System.Windows.Forms;
using System.Threading.Tasks;
using SpriteExtractor.Views;

namespace SpriteExtractor
{
    internal static class Program
    {
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