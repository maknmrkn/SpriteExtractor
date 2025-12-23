using System;
using System.Windows.Forms;
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

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}