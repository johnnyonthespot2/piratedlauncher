using SevenZipExtractor;
using System;
using System.IO;
using System.Windows.Forms;

public class FileExtractor
{
    public bool ExtractArchive(string archivePath, string destinationDirectory)
    {
        try
        {
            Directory.CreateDirectory(destinationDirectory);

            using (ArchiveFile archiveFile = new ArchiveFile(archivePath))
            {
                archiveFile.Extract(destinationDirectory);
                return true;
            }
        }
        catch(Exception ex)
        {
            MessageBox.Show("There was an error extracting the files. Error was copied to your clipboard in case you want to send it on the discord server for help.", "PiratedLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Clipboard.SetText(ex.ToString());
            return false;
        }
    }
}