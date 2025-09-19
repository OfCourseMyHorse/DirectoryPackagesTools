using System;

using Avalonia;
using Avalonia.Dialogs;

namespace DirectoryPackagesTools
{
    internal class Program
    {
        public static readonly log4net.ILog _Log = log4net.LogManager.GetLogger(typeof(App));

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            _Log.Info("Application start");
            log4net.LogManager.Flush(1000);

            #if !SUPRESSTRYCATCH

            #if !DEBUG
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            #endif

            try {
            #endif

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            #if !SUPRESSTRYCATCH
            } catch(Exception ex)
            {
                _Log.Fatal(ex.Message, ex);
            }
            #endif

            _Log.Info("Application shutdown");
            log4net.LogManager.Shutdown();            
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                _Log.Fatal(ex.Message, ex);
            }
            else
            {
                _Log.Fatal("Unhandled Exception");
            }

            
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                        .UsePlatformDetect()                        
                        .WithInterFont()
                        .LogToTrace();
        }
    }
}
