using Microsoft.Win32;
using System.Diagnostics;

namespace FChassis.Installer.Components;
public class DataFolderMapping : Component {
   override public void method () {
      Directory.CreateDirectory (mapPath);
      copysubDirectories__l (Path.Combine (Application.StartupPath, "files", "map"), this.mapPath, true);
      Process.Start ("cmd.exe", "/c " + $"subst {this.mapDrive} \"{this.mapPath}\"")?.WaitForExit ();

      using (RegistryKey key = Registry.CurrentUser.CreateSubKey (@"Software\Microsoft\Windows\CurrentVersion\Run")) {
         key?.SetValue ("TIS_FChassisMapDrive", $"subst {this.mapDrive} \"{this.mapPath}\""); }

      #region Local Funtion
      void copysubDirectories__l (string sourceDir, string destinationDir, bool recursive) {
         var dir = new DirectoryInfo (sourceDir);
         if (!dir.Exists)
            throw new DirectoryNotFoundException ($"Source directory not found: {dir.FullName}");

         DirectoryInfo[] dirs = dir.GetDirectories ();
         Directory.CreateDirectory (destinationDir);

         // Get the files in the source directory and copy to the destination directory
         foreach (FileInfo file in dir.GetFiles ()) {
            string targetFilePath = Path.Combine (destinationDir, file.Name);
            if (!File.Exists (targetFilePath))
               file.CopyTo (targetFilePath);
         }

         // If recursive and copying subdirectories, recursively call this method
         if (recursive) {
            foreach (DirectoryInfo subDir in dirs) {
               string newDestinationDir = Path.Combine (destinationDir, subDir.Name);
               copysubDirectories__l (subDir.FullName, newDestinationDir, true);
            }
         }
      }
      #endregion
   }

   // #region Fields
   readonly string mapPath = @"C:\FluxSDK\map";
   readonly string mapDrive = "W:";
}