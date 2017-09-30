using Microsoft.VisualBasic.ApplicationServices;
using Squirrel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hotsapi.Uploader.Windows
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (!App.Debug) {
                using (var mgr = new UpdateManager("not needed here")) {
                    // Note, in most of these scenarios, the app exits after this method completes!
                    SquirrelAwareApp.HandleEvents(
                        onInitialInstall: v => mgr.CreateShortcutForThisExe(),
                        onAppUpdate: v => mgr.CreateShortcutForThisExe(),
                        onAppUninstall: v => {
                            mgr.RemoveShortcutForThisExe();
                            if (Directory.Exists(App.SettingsDir)) {
                                Directory.Delete(App.SettingsDir, true);
                            }
                        });
                }
            }

            if (!Directory.Exists(App.SettingsDir)) {
                Directory.CreateDirectory(App.SettingsDir);
            }

            // Move files from old locations
            if (File.Exists($@"{App.AppDir}\..\replays.xml") && !File.Exists($@"{App.SettingsDir}\replays.xml")) {
                File.Move($@"{App.AppDir}\..\replays.xml", $@"{App.SettingsDir}\replays.xml");
            }
            if (File.Exists($@"{App.AppDir}\..\last.config") && !File.Exists($@"{App.SettingsDir}\last.config")) {
                File.Move($@"{App.AppDir}\..\last.config", $@"{App.SettingsDir}\last.config");
            }

            SingleInstanceManager manager = new SingleInstanceManager();
            manager.Run(args);
        }
    }

    public class SingleInstanceManager : WindowsFormsApplicationBase
    {
        private App _application;

        public SingleInstanceManager()
        {
            IsSingleInstance = true;
            ShutdownStyle = ShutdownMode.AfterAllFormsClose;
        }

        protected override bool OnStartup(StartupEventArgs eventArgs)
        {
            _application = new App();
            _application.InitializeComponent();
            _application.Run();
            return false;
        }

        protected override void OnStartupNextInstance(StartupNextInstanceEventArgs eventArgs)
        {
            base.OnStartupNextInstance(eventArgs);
            _application.Activate();
        }
    }
}
