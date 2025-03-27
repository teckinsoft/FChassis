using Microsoft.Win32;
using System.Diagnostics;

namespace FChassis.Installer.Components;
public class DataFolderMapping : Component {
   override public void method () {
      Directory.CreateDirectory (MapPath);
      foreach (string file in Directory.GetFiles (Path.Combine (Application.StartupPath, "files", "map"))) {
         File.Copy (file, Path.Combine (MapPath, Path.GetFileName (file)), true);
      }

      Process.Start ("cmd.exe", "/c " + $"subst {MapDrive} \"{MapPath}\"")?.WaitForExit ();

      using (RegistryKey key = Registry.CurrentUser.CreateSubKey (@"Software\Microsoft\Windows\CurrentVersion\Run")) {
         key?.SetValue ("TIS_FChassisMapDrive", $"subst {MapDrive} \"{MapPath}\"");
      }
   }

   // #region Fields
   readonly string MapPath = @"C:\FluxSDK\map";
   readonly string MapDrive = "W:";
}