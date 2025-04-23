using System.Diagnostics;

namespace FChassis.Installer;
public class Component () {
   virtual public void Install () { }

   #region Protected Implementations
   protected async Task installDependSetupFromURL (string url, string name,
                                                   string installPath, string downloadFileName) {
      string randomTempFolderPath = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
      Directory.CreateDirectory (randomTempFolderPath); // Create Temp Folder

      var progressIndicator = new Progress<double> (percentage =>
                                 { Console.WriteLine ($"Downloaded: {percentage:F2}%"); });

      string downloadFilePath = Path.Combine (randomTempFolderPath, downloadFileName);
      await this.downloadLargeFileWithProgressAsync (url, downloadFilePath, progressIndicator);
      
      this.installDependSetup (downloadFilePath, installPath, name);

      Directory.Delete (randomTempFolderPath, true);  // Delete Temp Folder
   }

   public async Task downloadLargeFileWithProgressAsync (string url, string downloadFilePath,
                                                         IProgress<double> progress = null!) {
      using (var client = new HttpClient ())
      using (var response = await client.GetAsync (url, HttpCompletionOption.ResponseHeadersRead)) {
         response.EnsureSuccessStatusCode ();

         await using var contentStream = await response.Content.ReadAsStreamAsync ();
         await using var fileStream = new FileStream (downloadFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

         var totalBytes = response.Content.Headers.ContentLength ?? -1L;
         var totalReadBytes = 0L;
         var buffer = new byte[81920];  // 80 KB chunk size
         int readBytes;

         while ((readBytes = await contentStream.ReadAsync (buffer.AsMemory (0, buffer.Length))) > 0) {
            await fileStream.WriteAsync (buffer.AsMemory (0, readBytes));
            totalReadBytes += readBytes;

            if (progress != null) {
               if (totalBytes != -1) {
                  double percentage = (double)totalReadBytes / totalBytes * 100;
                  progress.Report (percentage);
               } else 
                  progress.Report (-1); // -1 indicates unknown size                  
            }
         }
      }
   }

   protected void installDependSetup (string filePath, string installPath, string name) {
      Process process = Process.Start (filePath, $"/silent /verysilent /suppressmsgboxes /norestart /dir={installPath}");
      process.WaitForExit ();

      string success = process.ExitCode == 0 ?"Completed" :"Failed";
      MessageBox.Show ($"{name} Installation {success}!");
   }
   #endregion Protected Implementations

   #region Fields
   public bool selected = true;
   public ComponentPage page = null!;

   protected string fluxSDKPath = @"C:\FluxSDK";
   protected string fluxSDKBinPath => Path.Combine (this.fluxSDKPath, "bin");
   //protected string installExePath = Environment.CurrentDirectory;
   protected string installExePath = @"P:\TeckInSoft\FChassis-Installation\FChassis-Installer";
   #endregion Fields
}