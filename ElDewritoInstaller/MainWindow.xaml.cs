using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

using Ionic.Zip;
using Newtonsoft.Json;

namespace ElDewritoInstaller
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region BackgroundWorkers
        private static BackgroundWorker Download;
        private static BackgroundWorker HashCheck;
        private static BackgroundWorker Install;
        #endregion

        #region Variables
        WebClient webClient = new WebClient();
        ProcessStartInfo startProcess = new ProcessStartInfo();
        string dewLoc = Path.Combine(Directory.GetCurrentDirectory(), JSON("name"));
        string filePath = Path.Combine(Directory.GetCurrentDirectory(), JSON("filename"));
        string fileHash = JSON("hash");
        string url = JSON("url");
        #endregion

        #region Main
        public MainWindow()
        {
            InitializeComponent();

            if (File.Exists(filePath))
            { this.btnStartDownload.IsEnabled = false; this.btnStartHashCheck.IsEnabled = true; this.btnStartInstall.IsEnabled = false; }
            else
            { this.btnStartDownload.IsEnabled = true; this.btnStartHashCheck.IsEnabled = false; this.btnStartInstall.IsEnabled = false; }
        }
        #endregion

        #region Button classes
        private void btnStartDownload_Click(object sender, EventArgs e)
        {
            Download = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(Download_RunWorkerCompleted);
            webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(Download_ProgressChanged);
            webClient.DownloadFileAsync(new Uri(url), filePath);

            this.btnStartDownload.IsEnabled = false;
            this.btnStartHashCheck.IsEnabled = false;
            this.btnStartInstall.IsEnabled = false;
        }

        private void btnStartHashCheck_Click(object sender, EventArgs e)
        {
            HashCheck = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            HashCheck.DoWork += HashCheck_DoWork;
            HashCheck.ProgressChanged += HashCheck_ProgressChanged;
            HashCheck.RunWorkerCompleted += HashCheck_RunWorkerCompleted;

            HashCheck.RunWorkerAsync(filePath);
            this.btnStartDownload.IsEnabled = false;
            this.btnStartHashCheck.IsEnabled = false;
            this.btnStartInstall.IsEnabled = false;
        }

        private void btnStartInstall_Click(object sender, RoutedEventArgs e)
        {
            Install = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            Install.DoWork += Install_DoWork;
            Install.ProgressChanged += Install_ProgressChanged;
            Install.RunWorkerCompleted += Install_RunWorkerCompleted;

            Install.RunWorkerAsync(filePath);
            this.btnStartDownload.IsEnabled = false;
            this.btnStartHashCheck.IsEnabled = false;
            this.btnStartInstall.IsEnabled = false;
        }
        #endregion

        #region Download classes
        private void Download_ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            string progressPercentage = Convert.ToString(e.ProgressPercentage);
            string receivedMB = ConvertBytesToMegaGigabytes(e.BytesReceived, "MB").ToString("0.00");
            string receivedGB = ConvertBytesToMegaGigabytes(e.BytesReceived, "GB").ToString("0.00");
            string receiveMB = ConvertBytesToMegaGigabytes(e.TotalBytesToReceive, "MB").ToString("0");
            string receiveGB = ConvertBytesToMegaGigabytes(e.TotalBytesToReceive, "GB").ToString("0.00");

            progressBar.Value = e.ProgressPercentage;
            if (ConvertBytesToMegaGigabytes(e.BytesReceived, "MB") < 1024)
            {
                progressBarText.Text = String.Format("{0}% {1}/{2}MB", progressPercentage, receivedMB, receiveMB);
            }
            else
            {
                progressBarText.Text = String.Format("{0}% {1}/{2}GB", progressPercentage, receivedGB, receiveGB);
            }
        }

        private void Download_RunWorkerCompleted(object sender, AsyncCompletedEventArgs e)
        {
            progressBar.Value = 0;
            progressBarText.Text = "Download Complete";
            this.btnStartHashCheck.IsEnabled = true;
        }
        #endregion

        #region HashCheck classes
        private void HashCheck_DoWork(object sender, DoWorkEventArgs e)
        {
            byte[] buffer;
            int bytesRead;
            long size;
            long totalBytesRead = 0;

            using (Stream file = File.OpenRead(filePath))
            {
                size = file.Length;

                using (HashAlgorithm hasher = SHA256.Create())
                {
                    do
                    {
                        buffer = new byte[4096];

                        bytesRead = file.Read(buffer, 0, buffer.Length);

                        totalBytesRead += bytesRead;

                        hasher.TransformBlock(buffer, 0, bytesRead, null, 0);

                        HashCheck.ReportProgress((int)((double)totalBytesRead / size * 100));

                    }
                    while (bytesRead != 0);

                    hasher.TransformFinalBlock(buffer, 0, 0);

                    e.Result = MakeHashString(hasher.Hash);
                }
            }
        }

        private void HashCheck_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string progressPercentage = Convert.ToString(e.ProgressPercentage);

            progressBar.Value = e.ProgressPercentage;
            progressBarText.Text = String.Format("{0}%", progressPercentage);
        }

        private void HashCheck_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result.ToString() == fileHash)
            {
                progressBar.Value = 0;
                progressBarText.Text = "Hash Check Complete";
                this.btnStartHashCheck.IsEnabled = false;
                this.btnStartInstall.IsEnabled = true;
            }
            else
            {
                progressBar.Value = 0;
                progressBarText.Text = "Hash Check Fail";
                this.btnStartDownload.IsEnabled = true;
                this.btnStartHashCheck.IsEnabled = false;
            }
        }
        #endregion

        #region Install classes
        private void Install_DoWork(object sender, DoWorkEventArgs e)
        {
            using (ZipFile zip = ZipFile.Read(filePath))
            {
                zip.ExtractProgress +=
                    new EventHandler<ExtractProgressEventArgs>(zip_ExtractProgress);
                foreach (ZipEntry file in zip)
                {
                    file.Extract(dewLoc, ExtractExistingFileAction.OverwriteSilently);
                }
            }
            Process.Start(Path.Combine(dewLoc, "tpi\\vc2012_redist\\vcredist_x86.exe"));
            Process.Start(Path.Combine(dewLoc, "tpi\\directx\\directx_june2010.exe"));
        }

        private void Install_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string progressPercentage = Convert.ToString(e.ProgressPercentage);

            progressBar.Value = e.ProgressPercentage;
            progressBarText.Text = String.Format("{0}%", progressPercentage);
        }

        private void Install_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            progressBar.Value = 0;
            progressBarText.Text = "Install Complete";
            this.btnStartDownload.IsEnabled = true;
            this.btnStartHashCheck.IsEnabled = true;
            this.btnStartInstall.IsEnabled = true;
        }
        #endregion

        #region Helper classes
        static string JSON(string Option)
        {
            System.Net.WebClient wc = new System.Net.WebClient();
            dynamic dew = JsonConvert.DeserializeObject(wc.DownloadString("http://thetwist84.github.io/HaloOnlineModManager/game/base.json"));
            
            string name = dew["base"].Name;
            string version = dew["base"].Version;
            string filename = dew["base"].Filename;
            string hash = dew["base"].Hash;
            string url = dew["base"].Url;

            if (Option == "name")
                return name;
            if (Option == "version")
                return version;
            if (Option == "filename")
                return filename;
            if (Option == "hash")
                return hash;
            if (Option == "url")
                return url;
            return null;
        }

        static double ConvertBytesToMegaGigabytes(long bytes, string Option)
        {
            if (Option == "MB")
                return (bytes / 1024f) / 1024f;
            if (Option == "GB")
                return ((bytes / 1024f) / 1024f) / 1024.0;
            return 0;
        }

        private static string MakeHashString(byte[] hashBytes)
        {
            StringBuilder hash = new StringBuilder(64);

            foreach (byte b in hashBytes)
                hash.Append(b.ToString("X2").ToUpper());

            return hash.ToString();
        }

        void zip_ExtractProgress(object sender, ExtractProgressEventArgs e)
        {
            if (e.TotalBytesToTransfer > 0)
            {
                Install.ReportProgress(Convert.ToInt32(100 * e.BytesTransferred / e.TotalBytesToTransfer));
            }
        }
        #endregion
    }
}
