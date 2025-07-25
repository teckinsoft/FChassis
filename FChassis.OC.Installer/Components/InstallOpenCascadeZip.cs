using System.IO.Compression;

namespace FChassis.Installer.Components;
public class InstallOpenCascadeZip : Component {
   override public void Install () {
      // Run the unzip in a background task
      _ = InstallAsync ();
   }
   private async Task InstallAsync () {
      try {
         await Task.Run (() => unZip ());  // heavy operation off the UI thread
                                           // After extraction finishes, continue
         addBinPath (targetFolder);
      } catch (Exception ex) {
         InstallationPage.WriteLine ($"Error: {ex.Message}"); }
   }

   private string targetFolder = "";

   void unZip () {
      // Get a temp folder path
      string tempFolder = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());
      Directory.CreateDirectory (tempFolder);

      string installPath = @$"{installExePath}\files";
      string zipSetupFilePath = @$"{installPath}\{zipFilename}";

      string _3rdPartyPath = Path.Combine (fluxSDKPath, "bin\\3rdParty");
      if (!Directory.Exists (_3rdPartyPath)) // Make sure destination exists
         Directory.CreateDirectory (_3rdPartyPath);

      string shortPath = ellipsizePath (zipSetupFilePath, 60);
      InstallationPage.WriteLine ($"Extracting {shortPath} to: {_3rdPartyPath}...");
      InstallationPage.WriteLine ("Please wait ...");

      ZipFile.ExtractToDirectory (zipSetupFilePath, _3rdPartyPath);

      // Rename folder
      string folderName = Path.GetFileNameWithoutExtension (zipFilename); // e.g. "3rdparty-vc14-64"
      string extractedFolder = Path.Combine (_3rdPartyPath, folderName);
      targetFolder = Path.Combine (_3rdPartyPath, "open-cascade");

      // If target already exists, delete or handle
      if (Directory.Exists (targetFolder))
         Directory.Delete (targetFolder, true);

      Directory.Move (extractedFolder, targetFolder);
      InstallationPage.WriteLine ($"Extracted {shortPath} to: {targetFolder}");
   }
   
   void addBinPath (string targetFolder) {
      InstallationPage.WriteLine ($"Updating Path for Open Cascade library");
      string systemPath = Environment.GetEnvironmentVariable ("PATH", EnvironmentVariableTarget.Machine) ?? ""; // Get current system PATH

      // Iterate bin folders
      bool modified = false;
      foreach (var dir in Directory.GetDirectories (targetFolder, "bin", SearchOption.AllDirectories)) {
         if (!systemPath.Contains (dir, StringComparison.OrdinalIgnoreCase)) {
            systemPath += Path.PathSeparator + dir;
            modified = true;
            string shortDir = ellipsizePath (dir, 60);
            InstallationPage.WriteLine ($"Adding folder: {shortDir}");
         }
         else
            InstallationPage.WriteLine ($"Already in PATH: {dir}");
      }

      // Save back to system variables
      if (modified)
         Environment.SetEnvironmentVariable ("PATH", systemPath, EnvironmentVariableTarget.Machine);
      
      InstallationPage.WriteLine (modified ? "System PATH updated. (Restart apps or sign out/in to take effect)"
                                           : "No PATH required, already exist.");
   }

   #region Helper Methods
   string ellipsizePath (string path, int maxLength) {
      if (string.IsNullOrEmpty (path)) return path;
      if (path.Length <= maxLength) return path;

      // Keep start and end parts
      int keep = (maxLength - 3) / 2; // 3 chars for "..."
      string start = path.Substring (0, keep);
      string end = path.Substring (path.Length - keep);

      return start + "..." + end;
   }

   void copyDirectory (string sourceDir, string destDir, bool overwrite) {
      // Create all directories
      foreach (string dirPath in Directory.GetDirectories (sourceDir, "*", SearchOption.AllDirectories)) {
         string newDir = dirPath.Replace (sourceDir, destDir);
         if (!Directory.Exists (newDir))
            Directory.CreateDirectory (newDir);
      }

      // Copy all files
      foreach (string filePath in Directory.GetFiles (sourceDir, "*.*", SearchOption.AllDirectories)) {
         string newFile = filePath.Replace (sourceDir, destDir);
         Directory.CreateDirectory (Path.GetDirectoryName (newFile)!);
         File.Copy (filePath, newFile, overwrite);
      }
   }
   #endregion 

   readonly string zipFilename = @"3rdparty-vc14-64.zip"; // Path to your ZIP file
}