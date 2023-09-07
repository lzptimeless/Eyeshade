using Eyeshade.Log;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

        private MainWindow? m_window;
        private readonly ILogWrapper m_logWrapper;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            m_logWrapper = new NLogWrapper("log.txt");
            UnhandledException += App_UnhandledException;

            this.InitializeComponent();
        }

        public Window? MainWindow => m_window;

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            m_logWrapper.Error(e.Exception, "App crashed.");
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow(m_logWrapper);
            var cmd = Environment.GetCommandLineArgs();

            if (args.Arguments?.Contains("--start-with-system") == true || // Unpackaged mode
                cmd?.Contains("--start-with-system") == true || // Unpackaged mode
                args.UWPLaunchActivatedEventArgs.Kind == ActivationKind.StartupTask) // Packaged mode
            {
                m_window.ShowHide();
            }
            else
            {
                m_window.Activate();
            }
        }
    }
}
