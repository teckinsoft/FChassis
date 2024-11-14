using System.Globalization;
using System.Windows;
using System.Windows.Threading;
namespace FChassis;

public partial class App : Application {
   protected override void OnStartup (StartupEventArgs e) {
      base.OnStartup (e);
      // Set the current culture to "en-US"
      CultureInfo.CurrentCulture = new CultureInfo ("en-US");
      CultureInfo.CurrentUICulture = new CultureInfo ("en-US");

      // Handle UI thread exceptions
      Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;

      // Handle non-UI thread exceptions
      AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

      // Handle TaskScheduler exceptions
      TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
      OnAppStart ();
   }

   void TaskScheduler_UnobservedTaskException (object sender, UnobservedTaskExceptionEventArgs e) {
      OnExitHandler ();
      
      // Mark the exception as handled
      e.SetObserved (); 
   }

   private void CurrentDomain_UnhandledException (object sender, UnhandledExceptionEventArgs e) {
      OnExitHandler ();
   }

   void Current_DispatcherUnhandledException (object sender, DispatcherUnhandledExceptionEventArgs e) {
      OnExitHandler ();
      e.Handled = true; 
   }
   void OnExitHandler () =>BeforeExit (); 
   protected override void OnExit (ExitEventArgs e) {
      OnExitHandler ();
      base.OnExit (e);
   }

   public void BeforeExit () { /* => SettingServices.It.SaveSettings ();*/ }

   public void OnAppStart () => SettingServices.It.LoadSettings ();
}

