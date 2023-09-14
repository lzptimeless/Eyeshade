using Eyeshade.Log;
using Eyeshade.SingleInstance;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Windows.ApplicationModel.WindowsAppRuntime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Eyeshade
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
#if IS_NON_PACKAGED
        public static readonly bool IsPackaged = false;
#else
        public static readonly bool IsPackaged = true;
#endif

        private MainWindow? _window;
        private readonly ILogWrapper _logWrapper;
        private readonly SingleInstanceFeature? _singleInstanceFeature;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            _logWrapper = new NLogWrapper("log.txt");
            _singleInstanceFeature = new SingleInstanceFeature();
            UnhandledException += App_UnhandledException;

            // Initialize does a status check, and if the status is not Ok it will attempt to get
            // the WindowsAppRuntime into a good state by deploying packages. Unlike a simple
            // status check, Initialize can sometimes take several seconds to deploy the packages.
            var deployResult = DeploymentManager.Initialize();
            if (deployResult.Status != DeploymentStatus.Ok)
            {
                _logWrapper.Error(deployResult.ExtendedError, $"WindowsAppRuntime init failed: {deployResult.Status}");
                _logWrapper.Flush();
            }

            this.InitializeComponent();
        }

        public Window? MainWindow => _window;
        internal SingleInstanceFeature? SingleInstanceFeature => _singleInstanceFeature;

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            _logWrapper.Error(e.Exception, "App crashed.");
            _logWrapper.Flush();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            var cmdLine = GetCmdLine(args);
            if (this.GetSingleInstanceFeature()?.Register() == false)
            {
                this.GetSingleInstanceFeature()?.Active(cmdLine);
                Exit();
                return;
            }

            _window = new MainWindow(_logWrapper);
            if (cmdLine?.Contains("--start-with-system") == true || // Unpackaged mode
                args.UWPLaunchActivatedEventArgs.Kind == ActivationKind.StartupTask) // Packaged mode
            {
                _window.ShowHide();
            }
            else
            {
                _window.ShowNormal();
            }
        }

        private string GetCmdLine(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            StringBuilder cmdLine = new StringBuilder();
            var envArgs = Environment.GetCommandLineArgs();
            if (envArgs != null && envArgs.Length > 0)
            {
                cmdLine.Append(string.Join(' ', envArgs));
            }

            if (!string.IsNullOrEmpty(args.Arguments))
            {
                cmdLine.Append(' ');
                cmdLine.Append(args.Arguments);
            }

            if (!string.IsNullOrEmpty(args.UWPLaunchActivatedEventArgs?.Arguments))
            {
                cmdLine.Append(' ');
                cmdLine.Append(args.UWPLaunchActivatedEventArgs.Arguments);
            }

            return cmdLine.ToString();
        }
    }

    public static class ApplicationExtension
    {
        internal static SingleInstanceFeature? GetSingleInstanceFeature(this Application app)
        {
            return ((App)app).SingleInstanceFeature;
        }

        internal static Window? GetMainWindow(this Application app)
        {
            return ((App)app).MainWindow;
        }
    }
}
