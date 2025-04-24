using Microsoft.Win32;
using System.Diagnostics;

namespace FChassis.Installer.Components;
public class DataFolderMapping : Component {
   override public void Install () {
      Directory.CreateDirectory (mapPath);
      copysubDirectories_ (Path.Combine (Application.StartupPath, "files", "map"), this.mapPath, true);
      Process.Start ("cmd.exe", "/c " + $"subst {this.mapDrive} \"{this.mapPath}\"")?.WaitForExit ();

      using (RegistryKey key = Registry.CurrentUser.CreateSubKey (@"Software\Microsoft\Windows\CurrentVersion\Run")) {
         key?.SetValue ("TIS_FChassisMapDrive", $"subst {this.mapDrive} \"{this.mapPath}\""); }

      #region local
      void copysubDirectories_ (string sourcePath, string destPath, bool recursive) {
         var dir = new DirectoryInfo (sourcePath);
         if (!dir.Exists)
            throw new DirectoryNotFoundException ($"Source directory not found: {dir.FullName}");

         DirectoryInfo[] dirs = dir.GetDirectories ();
         Directory.CreateDirectory (destPath);

         // Get the files in the source directory and copy to the destination directory
         foreach (FileInfo file in dir.GetFiles ()) {
            string targetFilePath = Path.Combine (destPath, file.Name);
            if (!File.Exists (targetFilePath))
               file.CopyTo (targetFilePath);
         }

         // If recursive and copying subdirectories, recursively call this method
         if (recursive) {
            foreach (DirectoryInfo subDir in dirs) {
               string newDestinationDir = Path.Combine (destPath, subDir.Name);
               copysubDirectories_ (subDir.FullName, newDestinationDir, true);
            }
         }
      }
      #endregion
   }

   // Fields
   public string mapPath = @"C:\FluxSDK\map";
   readonly string mapDrive = "W:";
}