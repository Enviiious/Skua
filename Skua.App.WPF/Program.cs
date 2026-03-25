using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows;

namespace Skua.App.WPF;

public static class Program
{
    private static readonly string CrashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "skua_crash.txt");

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            File.WriteAllText(CrashLogPath,
                $"[{DateTime.Now}] Skua Crash\r\n" +
                $"Message:    {ex.Message}\r\n" +
                $"Type:       {ex.GetType().FullName}\r\n" +
                $"StackTrace: {ex.StackTrace}\r\n" +
                (ex.InnerException != null
                    ? $"\r\nInner: {ex.InnerException.Message}\r\n{ex.InnerException.StackTrace}"
                    : ""));
        }
        catch { }
    }

    [STAThread]
    public static void Main()
    {
        try
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += new ResolveEventHandler(ResolveAssemblies);
            currentDomain.UnhandledException += CurrentDomain_UnhandledException;

            App app = new();
            app.InitializeComponent();
            app.Run();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            try { MessageBox.Show($"Fatal startup crash — see skua_crash.txt on your Desktop.\r\n\r\n{ex.Message}", "Skua Crash"); }
            catch { }
            throw;
        }
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Exception ex = (Exception)e.ExceptionObject;
        WriteCrashLog(ex);
        MessageBox.Show($"Application Crash.\r\nMessage: {ex.Message}\r\nStackTrace: {ex.StackTrace}\r\n\r\nFull log: {CrashLogPath}", "Application");
    }

    private static Assembly? ResolveAssemblies(object? sender, ResolveEventArgs args)
    {
        if (args.Name.Contains(".resources"))
            return null;

        Assembly? assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
        if (assembly != null)
            return assembly;

        string assemblyName = new AssemblyName(args.Name).Name + ".dll";
        string assemblyPath = Path.Combine(AppContext.BaseDirectory, "Assemblies", assemblyName);
        if (!File.Exists(assemblyPath))
        {
            assemblyPath = Path.Combine(AppContext.BaseDirectory, assemblyName);
            return File.Exists(assemblyPath) ? Assembly.LoadFrom(assemblyPath) : null;
        }
        return Assembly.LoadFrom(assemblyPath);
    }
}