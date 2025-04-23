namespace FChassis.Installer.Components;
public class InstallFChassis : Component {
   override public void Install () { 
      Directory.CreateDirectory (this.fluxSDKBinPath);
      string sourcePath = Path.Combine (this.installExePath, "files", "bin");
      this.copyFiles (sourcePath, this.fluxSDKBinPath, Files.programFiles);
      InstallationPage.WriteLine ("Program files copied successfully!");

      sourcePath = Path.Combine (this.installExePath, "files", "lib");
      this.copyFiles (sourcePath, this.fluxSDKBinPath, Files.libraryFiles);
      InstallationPage.WriteLine ("Library files copied successfully!");
   }

   void copyFiles (string sourceDir, string targetDir, string[] files) {
      foreach (string file in files) {
         string fileName = Path.GetFileName (file);
         string destFile = Path.Combine (targetDir, fileName);
         string srcFile = Path.Combine (sourceDir, fileName);

         InstallationPage.WriteLine ($"Copying file '{fileName}'!");
         try {
            File.Copy (srcFile, destFile, true); // Overwrite if exists
         } catch (Exception /*ex*/) {
            InstallationPage.WriteLine ($"Copy file '{fileName}' failed!");
            return;
         }
      }
   }

   void createShortcut (string targetPath, string shortcutName) {
      string desktopPath = Environment.GetFolderPath (Environment.SpecialFolder.Desktop);
      string shortcutPath = Path.Combine (desktopPath, shortcutName);
      using (StreamWriter writer = new StreamWriter (shortcutPath)) {
         writer.WriteLine ("[InternetShortcut]");
         writer.WriteLine ("URL=file:///" + targetPath);
         writer.WriteLine ("IconIndex=0");
         writer.WriteLine ("IconFile=" + targetPath);
      }
   }
}