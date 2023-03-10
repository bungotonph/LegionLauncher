using Newtonsoft.Json;
using SevenZip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Windows.Forms;

namespace GameUpdater
{
    public partial class MainForm : Form
    {
        private const string FileHashesPath = "file_hashes.json";
        private const string FileHashesUrl = "http://localhost/game/file_hashes.json";
        private const string VersionUrl = "http://localhost/game/version.json";
        private const string ArchiveUrl = "http://localhost/game/cabalmain.7z";
        private const string ArchiveFilename = "cabalmain.7z";
        private const string GameUrl = "http://localhost/game/";
        private const string GameDir = ".";
        private const string Password = "123";
        private readonly string ExtractPath = Path.Combine(Application.StartupPath, ".");
        private readonly string VersionFilePath = "version.json";
        private readonly Label lblServerStatus;
        private bool isDragging = false;
        private Point lastCursor;
        private Point lastForm;
        //private string CurrentVersion;
        //private string LatestVersion;

        public MainForm()
        {
            InitializeComponent();
            lblServerStatus = new Label
            {
                Text = "Offline",
                AutoSize = true,
                Location = new Point(85, 65),
                BackColor = Color.Transparent
            };

            // Add the Label control to the form's controls collection
            this.Controls.Add(lblServerStatus);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // Check important directories
            CheckMainDirectories();

            // Automatic update checking
            CheckForUpdates();

            // Update local file list from server
            UpdateLocalFileListFromServer();
        }

        private void CheckMainDirectories()
        {
            string GameData = "data";
            string GameLang = GameData + "\\" + "language";
            string GameEng = GameLang + "\\" + "english";
            // Check if the game directory exists, if not, create it
            if (!Directory.Exists(GameData) || !Directory.Exists(GameLang) || !Directory.Exists(GameEng))
            {
                var result = MessageBox.Show($"There are missing directories. Would you like to create them now?",
                            "Missing Directories", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (result == DialogResult.Yes)
                {
                    Directory.CreateDirectory(GameData);
                    Directory.CreateDirectory(GameLang);
                    Directory.CreateDirectory(GameEng);
                }
                else
                {
                    MessageBox.Show($"You cannot run the game without those important directories!",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(1);
                }
            }
        }

        private void CheckForUpdates()
        {
            try
            {
                // Set the label's text to "Checking..."
                lblServerStatus.Text = "Checking...";
                lblServerStatus.BackColor = Color.Transparent;

                using (var client = new WebClient())
                {
                    // Download the latest version info
                    var latestVersionJson = client.DownloadString(VersionUrl);
                    var latestVersionInfo = JsonConvert.DeserializeObject<VersionInfo>(latestVersionJson);

                    // Set the label's text to "Online" if no exceptions are caught
                    lblServerStatus.Text = "Online";
                    lblServerStatus.ForeColor = Color.Green;
                    lblServerStatus.BackColor = Color.Transparent;

                    // Load the local version info
                    VersionInfo currentVersionInfo;
                    using (var file = File.OpenText(VersionFilePath))
                    {
                        var serializer = new JsonSerializer();
                        currentVersionInfo = (VersionInfo)serializer.Deserialize(file, typeof(VersionInfo));
                    }

                    // Compare versions and prompt to update if necessary
                    if (currentVersionInfo == null || currentVersionInfo.LatestVersion != latestVersionInfo.LatestVersion)
                    {
                        var result = MessageBox.Show($"An update is available (Version {latestVersionInfo.LatestVersion}). Would you like to download and install it now?",
                            "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                        if (result == DialogResult.Yes)
                        {
                            // Download and extract game archive
                            DownloadAndExtractGameArchive();
                            currentVersionInfo = latestVersionInfo;

                            // Save the updated version info to local file
                            using (var file = File.CreateText(VersionFilePath))
                            {
                                var serializer = new JsonSerializer();
                                serializer.Serialize(file, currentVersionInfo);
                            }
                        }
                    }
                    else
                    {
                        progressBar.Value = 100;
                        LabelVersionLatest.Text = $"You have the latest version ({currentVersionInfo.LatestVersion}).";
                        BTNStart.Enabled = true;
                        BTNCheckFiles.Enabled = false;
                        //_ = MessageBox.Show($"You have the latest version (version {currentVersionInfo.LatestVersion}).",
                        //        "No Update Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                // Set the label's text to "Offline" if an exception is caught
                lblServerStatus.Text = "Offline";
                lblServerStatus.ForeColor = Color.Red;
                lblServerStatus.BackColor = Color.Transparent;
                MessageBox.Show($"Failed to check for updates: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                webBrowser1.Visible = false;
            }
        }

        private void DownloadAndExtractGameArchive()
        {
            try
            {
                // Create a temporary file to store the downloaded archive
                string tempFilePath = Path.Combine(Path.GetTempPath(), ArchiveFilename);

                // Download the archive
                using (var client = new WebClient())
                {
                    client.DownloadFile(ArchiveUrl, tempFilePath);
                }

                // Extract the archive
                SevenZipBase.SetLibraryPath(Path.Combine(Application.StartupPath, "7z.dll"));
                SevenZipExtractor extractor = new SevenZipExtractor(tempFilePath, Password);

                extractor.Extracting += (sender, e) =>
                {
                    progressBar.Invoke((MethodInvoker)delegate
                    {
                        int value = (int)((double)e.PercentDone / 100 * 100);
                        progressBar.Value = value;
                        // Change the color of the progress bar based on the value
                        Color color = Color.FromArgb(255, value, 0, 255 - value);
                        //progressBar.SetState(2);
                        //progressBar.SetState(1);
                        //progressBar.SetState(3);
                        //progressBar.SetState(2);
                        //progressBar.SetState(1);
                        //progressBar.SetState(3);
                        progressBar.BackColor = color;
                    });

                    //progressBar.Invoke((MethodInvoker)delegate { progressBar.Value = (int)((double)e.PercentDone / 100 * 100); });
                    LabelVersionLatest.Invoke((MethodInvoker)delegate { LabelVersionLatest.Text = "Extracting game files... " + e.PercentDone.ToString() + "%"; });
                };

                extractor.ExtractArchive(ExtractPath);

                // Delete the temporary archive file
                File.Delete(tempFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                webBrowser1.Visible = false;
            }
        }

        private void UpdateLocalFileListFromServer()
        {
            try
            {
                using (var client = new WebClient())
                {
                    // Download the file hashes JSON from the server
                    var fileHashesJson = client.DownloadString(FileHashesUrl);

                    // Deserialize the JSON array of file hashes into a List<FileHash>
                    var fileHashes = JsonConvert.DeserializeObject<List<FileHash>>(fileHashesJson);

                    // Loop through each file hash and check if it exists locally and if it has been modified
                    foreach (var fileHash in fileHashes)
                    {
                        var filePath = Path.Combine(GameDir, fileHash.Path);

                        if (File.Exists(filePath))
                        {
                            var localFileHash = HashGenerator.GenerateHash(filePath);

                            if (localFileHash != fileHash.Hash)
                            {
                                // File has been modified, replace with new version
                                File.Delete(filePath);
                                client.DownloadFile(Path.Combine(GameUrl, fileHash.Path), filePath);
                            }
                        }
                        else
                        {
                            // File does not exist, download it
                            client.DownloadFile(Path.Combine(GameUrl, fileHash.Path), filePath);
                        }
                    }

                    // Write updated file hashes to local file
                    var updatedFileHashesJson = JsonConvert.SerializeObject(fileHashes, Formatting.Indented);
                    var tempFileHashesPath = FileHashesPath + ".tmp"; // use a temp file to avoid file locking issues
                    File.WriteAllText(tempFileHashesPath, updatedFileHashesJson);
                    File.Replace(tempFileHashesPath, FileHashesPath, null); // replace original file with temp file
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update local file list from server: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                webBrowser1.Visible = false;
                BTNStart.Enabled = false;
                BTNCheckFiles.Enabled = true;
            }
        }

        private void BTNCheckFiles_Click(object sender, EventArgs e)
        {
            // Update local file list from server
            UpdateLocalFileListFromServer();

            try
            {
                // Set the label's text to "Checking..."
                lblServerStatus.Text = "Checking...";
                lblServerStatus.BackColor = Color.Transparent;

                BTNStart.Enabled = false;

                using (var client = new WebClient())
                {
                    // Download the latest version info
                    var latestVersionJson = client.DownloadString(VersionUrl);
                    var latestVersionInfo = JsonConvert.DeserializeObject<VersionInfo>(latestVersionJson);

                    // Set the label's text to "Online" if no exceptions are caught
                    lblServerStatus.Text = "Online";
                    lblServerStatus.ForeColor = Color.Green;
                    lblServerStatus.BackColor = Color.Transparent;

                    // Load the local version info
                    VersionInfo currentVersionInfo;
                    using (var file = File.OpenText(VersionFilePath))
                    {
                        var serializer = new JsonSerializer();
                        currentVersionInfo = (VersionInfo)serializer.Deserialize(file, typeof(VersionInfo));
                    }

                    // Compare versions and prompt to update if necessary
                    if (currentVersionInfo == null || currentVersionInfo.LatestVersion != latestVersionInfo.LatestVersion)
                    {
                        var result = MessageBox.Show($"An update is available (version {latestVersionInfo.LatestVersion}). Would you like to download and install it now?",
                            "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                        if (result == DialogResult.Yes)
                        {
                            // Download and extract game archive
                            DownloadAndExtractGameArchive();
                            currentVersionInfo = latestVersionInfo;

                            // Save the updated version info to local file
                            using (var file = File.CreateText(VersionFilePath))
                            {
                                var serializer = new JsonSerializer();
                                serializer.Serialize(file, currentVersionInfo);
                            }
                        }
                    }
                    else
                    {
                        progressBar.Value = 100;
                        LabelVersionLatest.Text = $"You have the latest version ({currentVersionInfo.LatestVersion}).";
                        BTNStart.Enabled = true;
                        BTNCheckFiles.Enabled = false;
                        //_ = MessageBox.Show($"You have the latest version (version {currentVersionInfo.LatestVersion}).",
                        //        "No Update Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                // Set the label's text to "Online" if no exceptions are caught
                lblServerStatus.Text = "Offline";
                lblServerStatus.ForeColor = Color.Red;
                lblServerStatus.BackColor = Color.Transparent;
                MessageBox.Show($"Failed to check for updates: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                webBrowser1.Visible = false;
            }
        }
        private void LaunchGame()
        {
            try
            {
                var process = new Process();
                process.StartInfo.FileName = "cabalmain.exe";
                process.StartInfo.Arguments = "sbajo";
                process.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch game: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BTNStart_Click(object sender, MouseEventArgs e)
        {
            BTNStart.BackgroundImage = Properties.Resources.start_hover;
            BTNStart.BackColor = Color.Transparent;
            LaunchGame();
            Environment.Exit(0);
        }

        private void BTNStart_MouseEnter(object sender, EventArgs e)
        {
            BTNStart.BackgroundImage = Properties.Resources.start_hover;
        }

        private void BTNStart_MouseLeave(object sender, EventArgs e)
        {
            BTNStart.BackgroundImage = Properties.Resources.start_normal;
        }
        private void BTNStart_MouseOver(object sender, EventArgs e)
        {
            BTNStart.BackgroundImage = Properties.Resources.start_hover;
            BTNStart.Cursor = Cursors.Hand;
        }

        private void BTNStart_BackColor(object sender, EventArgs e)
        {
            BTNStart.BackColor = Color.Transparent;
        }

        private void BTNCF_MouseOver(object sender, EventArgs e)
        {
            BTNCheckFiles.BackgroundImage = Properties.Resources.cf_hover;
        }
        private void BTNCF_MouseEnter(object sender, EventArgs e)
        {
            BTNCheckFiles.BackgroundImage = Properties.Resources.cf_normal;
        }

        private void BTNClose_Click(object sender, EventArgs e)
        {
            Environment.Exit(1);
        }
        private void BTNClose_MouseLeave(object sender, EventArgs e)
        {
            BTNClose.BackgroundImage = Properties.Resources.close_hover;
        }
        private void BTNClose_MouseEnter(object sender, EventArgs e)
        {
            BTNClose.BackgroundImage = Properties.Resources.close_normal;
        }

        private void Form_MouseDown(object sender, MouseEventArgs e)
        {
            isDragging = true;
            lastCursor = Cursor.Position;
            lastForm = this.Location;
        }

        private void Form_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                int xDiff = Cursor.Position.X - lastCursor.X;
                int yDiff = Cursor.Position.Y - lastCursor.Y;

                this.Location = new Point(lastForm.X + xDiff, lastForm.Y + yDiff);
            }
        }

        private void Form_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
        }

    }

    public class VersionInfo
    {
        public string LatestVersion { get; set; }
    }
    public class FileHash
    {
        public string Path { get; set; }
        public string Hash { get; set; }
    }

}
