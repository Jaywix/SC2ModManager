using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            string zipPath = args[0];
            string installPath = args[1];
            string exeName = args[2];

            // Wait for main app to close
            Thread.Sleep(2000);

            // Extract to temp folder
            string tempExtract = Path.Combine(Path.GetTempPath(), "SC2_Update_Extract");

            if (Directory.Exists(tempExtract))
                Directory.Delete(tempExtract, true);

            ZipFile.ExtractToDirectory(zipPath, tempExtract);

            // Copy files over
            foreach (var file in Directory.GetFiles(tempExtract, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(tempExtract, file);
                string dest = Path.Combine(installPath, relative);

                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(file, dest, true);
            }

            // Launch updated app
            string exePath = Path.Combine(installPath, exeName);

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = installPath
            });
        }
        catch (Exception ex)
        {
            File.WriteAllText("update_error.txt", ex.ToString());
        }
    }
}