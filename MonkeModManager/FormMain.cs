﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Threading;
using MonkeModManager.Internals;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MonkeModManager.Internals.SimpleJSON;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Speech.Synthesis;
using System.Speech.Recognition;

namespace MonkeModManager
{
    public partial class FormMain : Form
    {
        Boolean sleeping = false;
        Point lastpoint;
        SpeechSynthesizer s = new SpeechSynthesizer();
        Choices list = new Choices();
        SpeechRecognitionEngine rec = new SpeechRecognitionEngine();
        private const string BaseEndpoint = "https://api.github.com/repos/";
        private const Int16 CurrentVersion = 5;
        private List<ReleaseInfo> releases;
        Dictionary<string, int> groups = new Dictionary<string, int>();
        private string InstallDirectory = @"";
        private bool modsDisabled = false;
        public bool isSteam = true;
        public bool platformDetected = false;

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn
        (
            int nLeftRect,     // x-coordinate of upper-left corner
            int nTopRect,      // y-coordinate of upper-left corner
            int nRightRect,    // x-coordinate of lower-right corner
            int nBottomRect,   // y-coordinate of lower-right corner
            int nWidthEllipse, // width of ellipse
            int nHeightEllipse // height of ellipse
        );

        public FormMain()
        {
            InitializeComponent();
            Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));
            MessageBox.Show("This Gatg Mod Manager Was Modified + Tweaked By RE3Z", "[NOTE]", MessageBoxButtons.OK, MessageBoxIcon.Information);
            list.Add(new String[] { "Close", "Minimize", "Scan For Viruses", "Mod Manager", "Open" });
            Grammar gr = new Grammar(new GrammarBuilder(list));
            try
            {
                rec.RequestRecognizerUpdate();
                rec.LoadGrammar(gr);
                rec.SpeechRecognized += rec_speechreconised;
                rec.SetInputToDefaultAudioDevice();
                rec.RecognizeAsync(RecognizeMode.Multiple);
            }
            catch (Exception) { }

            s.SelectVoiceByHints(VoiceGender.Male);
        }

        public void say(String h)
        {
            s.Speak(h);
            sleeping = true;
        }

        private void rec_speechreconised(object sender, SpeechRecognizedEventArgs e)
        {
            String r = e.Result.Text;

            if (r == "Mod Manager")
            {
                sleeping = false;
            }

            if (sleeping == false && checkBox1.Checked == true)
            {
                if (r == "Close")
                {
                    say("Closing Window");
                    Environment.Exit(0);
                }

                if (r == "Minimize")
                {
                    say("Minimising Window");
                    WindowState = FormWindowState.Minimized;
                }

                if (r == "Scan For Viruses")
                {

                }

                if (r == "Open")
                {
                    say("Opening Mod Manager");
                    WindowState = FormWindowState.Normal;
                }
            }
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            foreach(Button btn in Controls.OfType<Button>())
            {
                btn.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, btn.Width, btn.Height, 13, 13));
            }

            LocationHandler();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            releases = new List<ReleaseInfo>();
            if (!File.Exists(Path.Combine(InstallDirectory, "winhttp.dll")))
            {
                if (File.Exists(Path.Combine(InstallDirectory, "mods.disable")))
                {
                    buttonToggleMods.Text = "Enable Mods";
                    modsDisabled = true;
                    buttonToggleMods.Enabled = true;
                }
                else
                {
                    buttonToggleMods.Enabled = false;
                }
            }
            else
            {
                buttonToggleMods.Enabled = true;
            }
            new Thread(() =>
            {
                LoadRequiredPlugins();
            }).Start();
        }

        #region ReleaseHandling

        private void LoadReleases()
        {
            var decodedMods = JSON.Parse(DownloadSite("https://raw.githubusercontent.com/BzzzThe18th/MonkeModInfo/master/modinfo.json"));
            var decodedGroups = JSON.Parse(DownloadSite("https://raw.githubusercontent.com/BzzzThe18th/MonkeModInfo/master/groupinfo.json"));

            var allMods = decodedMods.AsArray;
            var allGroups = decodedGroups.AsArray;

            for (int i = 0; i < allMods.Count; i++)
            {
                JSONNode current = allMods[i];
                ReleaseInfo release = new ReleaseInfo(current["name"], current["author"], current["version"], current["group"], current["download_url"], current["install_location"], current["git_path"], current["dependencies"].AsArray);
                releases.Add(release);
            }


            allGroups.Linq.OrderBy(x => x.Value["rank"]);
            for (int i = 0; i < allGroups.Count; i++)
            {
                JSONNode current = allGroups[i];
                if (releases.Any(x => x.Group == current["name"]))
                {
                    groups.Add(current["name"], groups.Count());
                }
            }
            groups.Add("Uncategorized", groups.Count());

            foreach (ReleaseInfo release in releases)
            {
                foreach (string dep in release.Dependencies)
                {
                    releases.Where(x => x.Name == dep).FirstOrDefault()?.Dependents.Add(release.Name);
                }
            }
        }

        private void LoadRequiredPlugins()
        {
            CheckVersion();
            UpdateStatus("Getting latest version info...");
            LoadReleases();
            this.Invoke((MethodInvoker)(() =>
            {//Invoke so we can call from current thread
             //Update checkbox's text
                Dictionary<string, int> includedGroups = new Dictionary<string, int>();

                for (int i = 0; i < groups.Count(); i++)
                {
                    var key = groups.First(x => x.Value == i).Key;
                    var value = listViewMods.Groups.Add(new ListViewGroup(key, HorizontalAlignment.Left));
                    groups[key] = value;
                }

                foreach (ReleaseInfo release in releases)
                {
                    ListViewItem item = new ListViewItem();
                    item.BackColor = Color.FromArgb(28,28,28);
                    item.ForeColor = Color.White;
                    item.Text = release.Name;
                    if (!String.IsNullOrEmpty(release.Version)) item.Text = $"{release.Name} - {release.Version}";
                    if (!String.IsNullOrEmpty(release.Tag)) { item.Text = string.Format("{0} - ({1})",release.Name, release.Tag); };
                    item.SubItems.Add(release.Author);
                    item.Tag = release;
                    if (release.Install)
                    {
                        listViewMods.Items.Add(item);
                    }
                    CheckDefaultMod(release, item);

                    if (release.Group == null || !groups.ContainsKey(release.Group))
                    {
                        item.Group = listViewMods.Groups[groups["Uncategorized"]];
                    }
                    else if (groups.ContainsKey(release.Group))
                    {
                        int index = groups[release.Group];
                        item.Group = listViewMods.Groups[index];
                    }
                }

                buttonInstall.Enabled = true;

            }));
           
            UpdateStatus("Release info updated!");

        }

        private void UpdateReleaseInfo(ref ReleaseInfo release)
        {
            Thread.Sleep(100); //So we don't get rate limited by github

            string releaseFormatted = BaseEndpoint + release.GitPath + "/releases";
            var rootNode = JSON.Parse(DownloadSite(releaseFormatted))[0];
            
            release.Version = rootNode["tag_name"];
            
            var assetsNode = rootNode["assets"];
            var downloadReleaseNode = assetsNode[release.ReleaseId];
            release.Link = downloadReleaseNode["browser_download_url"];
            
            var uploaderNode = downloadReleaseNode["uploader"];
            if (release.Author.Equals(String.Empty)) release.Author = uploaderNode["login"];
        }

        #endregion // ReleaseHandling

        #region Installation

        private void Install()
        {
            ChangeInstallButtonState(false);
            UpdateStatus("Starting install sequence...");
            foreach (ReleaseInfo release in releases)
            {
                if (release.Install)
                {
                    UpdateStatus(string.Format("Downloading...{0}", release.Name));
                    byte[] file = DownloadFile(release.Link);
                    UpdateStatus(string.Format("Installing...{0}", release.Name));
                    string fileName = Path.GetFileName(release.Link);
                    if (Path.GetExtension(fileName).Equals(".dll"))
                    {
                        string dir;
                        if (release.InstallLocation == null)
                        {
                            dir = Path.Combine(InstallDirectory, @"BepInEx\plugins", Regex.Replace(release.Name, @"\s+", string.Empty));
                            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        }
                        else
                        {
                            dir = Path.Combine(InstallDirectory, release.InstallLocation);
                        }
                        File.WriteAllBytes(Path.Combine(dir, fileName), file);

                        var dllFile = Path.Combine(InstallDirectory, @"BepInEx\plugins", fileName);
                        if (File.Exists(dllFile))
                        {
                            File.Delete(dllFile);
                        }
                    }
                    else
                    {
                        UnzipFile(file, (release.InstallLocation != null) ? Path.Combine(InstallDirectory, release.InstallLocation) : InstallDirectory);
                    }
                    UpdateStatus(string.Format("Installed {0}!", release.Name));
                }
            }
            UpdateStatus("Install complete!");
            ChangeInstallButtonState(true);

            this.Invoke((MethodInvoker)(() =>
            { //Invoke so we can call from any thread
                buttonToggleMods.Enabled = true;
            }));
        }

        #endregion // Installation

        #region UIEvents

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void buttonInstall_Click(object sender, EventArgs e)
        {
            new Thread(() =>
            {
                Install();
            }).Start();
        }

        private void buttonFolderBrowser_Click(object sender, EventArgs e)
        {
            using (var fileDialog = new OpenFileDialog())
            {
                fileDialog.FileName = "GorillaTag Executable";
                fileDialog.Filter = "Exe Files (.exe)|*.exe|All Files (*.*)|*.*";
                fileDialog.FilterIndex = 1;
                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    string path = fileDialog.FileName;
                    if (Path.GetFileName(path).Equals("Gorilla Tag.exe") | Path.GetFileName(path).Equals("GorillaTag.exe"))
                    {
                        InstallDirectory = Path.GetDirectoryName(path);
                        textBoxDirectory.Text = InstallDirectory;
                    }
                    else
                    {
                        MessageBox.Show("That's not the Gorilla Tag exectuable! please try again!", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                }

            }
        }

        private void listViewMods_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            ReleaseInfo release = (ReleaseInfo)e.Item.Tag;

            if (release.Dependencies.Count > 0)
            {
                foreach (ListViewItem item in listViewMods.Items)
                {
                    var plugin = (ReleaseInfo)item.Tag;

                    if (plugin.Name == release.Name) continue;

                    // if this depends on plugin
                    if (release.Dependencies.Contains(plugin.Name))
                    {
                        if (e.Item.Checked)
                        {
                            item.Checked = true;
                        }
                        else
                        {
                            release.Install = false;
                            if (releases.Count(x => plugin.Dependents.Contains(x.Name) && x.Install) <= 1)
                            {
                                item.Checked = false;
                            }
                        }
                    }
                }
            }

            // don't allow user to uncheck if a dependent is checked
            if (release.Dependents.Count > 0)
            {
                if (releases.Count(x => release.Dependents.Contains(x.Name) && x.Install) > 0)
                {
                    e.Item.Checked = true;
                }
            }

            if (release.Name.Contains("BepInEx") || release.Name.Contains("Utilla")) { e.Item.Checked = true; };
            release.Install = e.Item.Checked;
        }

         private void listViewMods_DoubleClick(object sender, EventArgs e)
        {
            OpenLinkFromRelease();
        }

        private void buttonModInfo_Click(object sender, EventArgs e)
        {
            OpenLinkFromRelease();
        }

        private void viewInfoToolStripMenuItem_Click(object sender, EventArgs e)
         {
             OpenLinkFromRelease();
         }

        private void clickQuit(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void clickMinimize(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }

        private void listViewMods_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {

        }

        private void buttonUninstallAll_Click(object sender, EventArgs e)
        {
            var confirmResult = MessageBox.Show(
                "You are about to delete all your mods (including any saved data in your plugins). This cannot be undone!\n\nAre you sure you wish to continue?",
                "Confirm Delete",
                MessageBoxButtons.YesNo);

            if (confirmResult == DialogResult.Yes)
            {
                UpdateStatus("Uninstalling all mods");

                var pluginsPath = Path.Combine(InstallDirectory, @"BepInEx\plugins");

                try
                {
                    foreach (var d in Directory.GetDirectories(pluginsPath))
                    {
                        Directory.Delete(d, true);
                    }

                    foreach (var f in Directory.GetFiles(pluginsPath))
                    {
                        File.Delete(f);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Something went wrong!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    UpdateStatus("Failed to uninstall mods.");
                    return;
                }

                UpdateStatus("All mods uninstalled successfully!");
            }
        }

        private void buttonBackupMods_Click(object sender, EventArgs e)
        {
            var pluginsPath = Path.Combine(InstallDirectory, @"BepInEx\plugins");

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = InstallDirectory,
                FileName = $"Mod Backup",
                Filter = "ZIP Folder (.zip)|*.zip",
                Title = "Save Mod Backup"
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK && saveFileDialog.FileName != "")
            {
                UpdateStatus("Backing up mods...");
                try
                {
                    if (File.Exists(saveFileDialog.FileName)) File.Delete(saveFileDialog.FileName);
                    ZipFile.CreateFromDirectory(pluginsPath, saveFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Something went wrong!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    UpdateStatus("Failed to back up mods.");
                    return;
                }
                UpdateStatus("Successfully backed up mods!");
            }


        }

        private void buttonRestoreMods_Click(object sender, EventArgs e)
        {
            using (var fileDialog = new OpenFileDialog())
            {
                fileDialog.InitialDirectory = InstallDirectory;
                fileDialog.FileName = "Mod Backup.zip";
                fileDialog.Filter = "ZIP Folder (.zip)|*.zip";
                fileDialog.FilterIndex = 1;
                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    if (!Path.GetExtension(fileDialog.FileName).Equals(".zip", StringComparison.InvariantCultureIgnoreCase))
                    {
                        MessageBox.Show("Invalid file!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        UpdateStatus("Failed to restore mods.");
                        return;
                    }
                    var pluginsPath = Path.Combine(InstallDirectory, @"BepInEx\plugins");
                    try
                    {
                        UpdateStatus("Restoring mods...");
                        using (var archive = ZipFile.OpenRead(fileDialog.FileName))
                        {
                            foreach (var entry in archive.Entries)
                            {
                                var directory = Path.Combine(InstallDirectory, @"BepInEx\plugins", Path.GetDirectoryName(entry.FullName));
                                if (!Directory.Exists(directory))
                                {
                                    Directory.CreateDirectory(directory);
                                }

                                entry.ExtractToFile(Path.Combine(pluginsPath, entry.FullName), true);
                            }
                        }
                        UpdateStatus("Successfully restored mods!");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Something went wrong!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        UpdateStatus("Failed to restore mods.");
                    }
                }
            }
        }

        #region Folders

        private void buttonOpenGameFolder_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(InstallDirectory))
                Process.Start(InstallDirectory);
        }

        private void buttonOpenConfigFolder_Click(object sender, EventArgs e)
        {
            var configDirectory = Path.Combine(InstallDirectory, @"BepInEx\config");
            if (Directory.Exists(configDirectory))
                Process.Start(configDirectory);
        }

        private void buttonOpenModsFolder_Click(object sender, EventArgs e)
        {
            var BepInExDirectory = Path.Combine(InstallDirectory, @"BepInEx\plugins");
            if (Directory.Exists(BepInExDirectory))
                Process.Start(BepInExDirectory);
        }

        #endregion // Folders

        #endregion // UIEvents

        #region Helpers

        private CookieContainer PermCookie;
        private string DownloadSite(string URL)
        {
            try
            {
                if (PermCookie == null) { PermCookie = new CookieContainer(); }
                HttpWebRequest RQuest = (HttpWebRequest)HttpWebRequest.Create(URL);
                RQuest.Method = "GET";
                RQuest.KeepAlive = true;
                RQuest.CookieContainer = PermCookie;
                RQuest.ContentType = "application/x-www-form-urlencoded";
                RQuest.Referer = "";
                RQuest.UserAgent = "Monke-Mod-Manager";
                RQuest.Proxy = null;
                HttpWebResponse Response = (HttpWebResponse)RQuest.GetResponse();
                StreamReader Sr = new StreamReader(Response.GetResponseStream());
                string Code = Sr.ReadToEnd();
                Sr.Close();
                return Code;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("403"))
                {
                    MessageBox.Show("Failed to update version info, GitHub has rate limited you, please check back in 15 - 30 minutes. If this problem persists, share this error to helpers in the modding discord:\n{ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    MessageBox.Show($"Failed to update version info, please check your internet connection. If this problem persists, share this error to helpers in the modding discord:\n{ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                Process.GetCurrentProcess().Kill();
                return null;
            }
        }

        private void UnzipFile(byte[] data, string directory)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                using (var unzip = new Unzip(ms))
                {
                    unzip.ExtractToDirectory(directory);
                }
            }
        }

        private byte[] DownloadFile(string url)
        {
            WebClient client = new WebClient();
            client.Proxy = null;
            return client.DownloadData(url);
        }

        private void UpdateStatus(string status)
        {
            string formattedText = string.Format("Status: {0}", status);
            this.Invoke((MethodInvoker)(() =>
            { //Invoke so we can call from any thread
                labelStatus.Text = formattedText;
            }));
        }
  
        private void NotFoundHandler()
        {
            bool found = false;
            while (found == false)
            {
                using (var fileDialog = new OpenFileDialog())
                {
                    fileDialog.FileName = "Gorilla Tag Executable";
                    fileDialog.Filter = "Exe Files (.exe)|*.exe|All Files (*.*)|*.*";
                    fileDialog.FilterIndex = 1;
                    if (fileDialog.ShowDialog() == DialogResult.OK)
                    {
                        string path = fileDialog.FileName;
                        if (Path.GetFileName(path).Equals("Gorilla Tag.exe") | Path.GetFileName(path).Equals("GorillaTag.exe"))
                        {
                            InstallDirectory = Path.GetDirectoryName(path);
                            textBoxDirectory.Text = InstallDirectory;
                            found = true;
                        }
                        else
                        {
                            MessageBox.Show("That's not the Gorilla Tag exectuable! please try again!", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        Process.GetCurrentProcess().Kill();
                    }
                }
            }
        }

        private void CheckVersion()
        {
            UpdateStatus("Checking for updates...");
            Int16 version = Convert.ToInt16(DownloadSite("https://raw.githubusercontent.com/BzzzThe18th/MonkeModManager/master/update.txt"));
            if (version > CurrentVersion)
            {
                this.Invoke((MethodInvoker)(() =>
                {
                    MessageBox.Show("Your version of the mod installer is outdated! Please download the new one!", "Update available!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    Process.Start("https://github.com/BzzzThe18th/MonkeModManager/releases/latest");
                    Process.GetCurrentProcess().Kill();
                    Environment.Exit(0);
                }));
            }
        }

        private void ChangeInstallButtonState(bool enabled)
        {
            this.Invoke((MethodInvoker)(() =>
                {
                    buttonInstall.Enabled = enabled;
                }));
        }

        private void OpenLinkFromRelease()
        {
            if (listViewMods.SelectedItems.Count > 0)
            {
                foreach (ListViewItem item in listViewMods.SelectedItems)
                {
                    ReleaseInfo release = (ReleaseInfo)item.Tag;
                    UpdateStatus($"Opening GitHub page for {release.Name}");
                    Process.Start(string.Format("https://github.com/{0}", release.GitPath));
                }
            }
            
        }

#endregion // Helpers

        #region Registry

        private void LocationHandler()
        {
            string steam = GetSteamLocation();
            if (steam != null)
            {
                if (Directory.Exists(steam))
                {
                    if (File.Exists(steam + @"\Gorilla Tag.exe") | File.Exists(steam + @"\GorillaTag.exe"))
                    {
                        textBoxDirectory.Text = steam;
                        InstallDirectory = steam;
                        platformDetected = true;
                        return;
                    }
                }
            }
            ShowErrorFindingDirectoryMessage();
        }
        private void ShowErrorFindingDirectoryMessage()
        {
            MessageBox.Show("We couldn't seem to find your Gorilla Tag installation, please press \"OK\" and point us to it", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            NotFoundHandler();
            this.TopMost = true;
        }
        private string GetSteamLocation()
        {
            string path = RegistryWOW6432.GetRegKey64(RegHive.HKEY_LOCAL_MACHINE, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1533390", @"InstallLocation");
            if (path != null)
            {
                path = path + @"\";
            }
            return path;
        }
        private void CheckDefaultMod(ReleaseInfo release, ListViewItem item)
        {
            if (release.Name.Contains("BepInEx") || release.Name.Contains("Utilla"))
            {
                item.Checked = true;
                item.ForeColor = Color.LightGray;
            }
            else
            {
                release.Install = false;
            }
        }
#endregion // Registry

        #region RegHelper
        enum RegSAM
        {
            QueryValue = 0x0001,
            SetValue = 0x0002,
            CreateSubKey = 0x0004,
            EnumerateSubKeys = 0x0008,
            Notify = 0x0010,
            CreateLink = 0x0020,
            WOW64_32Key = 0x0200,
            WOW64_64Key = 0x0100,
            WOW64_Res = 0x0300,
            Read = 0x00020019,
            Write = 0x00020006,
            Execute = 0x00020019,
            AllAccess = 0x000f003f
        }

        static class RegHive
        {
            public static UIntPtr HKEY_LOCAL_MACHINE = new UIntPtr(0x80000002u);
            public static UIntPtr HKEY_CURRENT_USER = new UIntPtr(0x80000001u);
        }

        static class RegistryWOW6432
        {
            [DllImport("Advapi32.dll")]
            static extern uint RegOpenKeyEx(UIntPtr hKey, string lpSubKey, uint ulOptions, int samDesired, out int phkResult);

            [DllImport("Advapi32.dll")]
            static extern uint RegCloseKey(int hKey);

            [DllImport("advapi32.dll", EntryPoint = "RegQueryValueEx")]
            public static extern int RegQueryValueEx(int hKey, string lpValueName, int lpReserved, ref uint lpType, System.Text.StringBuilder lpData, ref uint lpcbData);

            static public string GetRegKey64(UIntPtr inHive, String inKeyName, string inPropertyName)
            {
                return GetRegKey64(inHive, inKeyName, RegSAM.WOW64_64Key, inPropertyName);
            }

            static public string GetRegKey32(UIntPtr inHive, String inKeyName, string inPropertyName)
            {
                return GetRegKey64(inHive, inKeyName, RegSAM.WOW64_32Key, inPropertyName);
            }

            static public string GetRegKey64(UIntPtr inHive, String inKeyName, RegSAM in32or64key, string inPropertyName)
            {
                //UIntPtr HKEY_LOCAL_MACHINE = (UIntPtr)0x80000002;
                int hkey = 0;

                try
                {
                    uint lResult = RegOpenKeyEx(RegHive.HKEY_LOCAL_MACHINE, inKeyName, 0, (int)RegSAM.QueryValue | (int)in32or64key, out hkey);
                    if (0 != lResult) return null;
                    uint lpType = 0;
                    uint lpcbData = 1024;
                    StringBuilder AgeBuffer = new StringBuilder(1024);
                    RegQueryValueEx(hkey, inPropertyName, 0, ref lpType, AgeBuffer, ref lpcbData);
                    string Age = AgeBuffer.ToString();
                    return Age;
                }
                finally
                {
                    if (0 != hkey) RegCloseKey(hkey);
                }
            }
        }

        #endregion // RegHelper

        private void buttonToggleMods_Click(object sender, EventArgs e)
        {
            if (modsDisabled)
            {
                if (File.Exists(Path.Combine(InstallDirectory, "mods.disable")))
                {
                    File.Move(Path.Combine(InstallDirectory, "mods.disable"), Path.Combine(InstallDirectory, "winhttp.dll"));
                    buttonToggleMods.Text = "Disable Mods";
                    buttonToggleMods.BackColor = Color.FromArgb(120, 0, 0);
                    modsDisabled = false;
                    UpdateStatus("Enabled mods!");
                }
            }
            else
            {
                if (File.Exists(Path.Combine(InstallDirectory, "winhttp.dll")))
                {
                    File.Move(Path.Combine(InstallDirectory, "winhttp.dll"), Path.Combine(InstallDirectory, "mods.disable"));
                    buttonToggleMods.Text = "Enable Mods";
                    buttonToggleMods.BackColor = Color.FromArgb(0, 120, 0);
                    modsDisabled = true;
                    UpdateStatus("Disabled mods!");
                }
            }
        }
        private void button1_Click_1(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }

        private void panel1_MouseDown(object sender, MouseEventArgs e)
        {
            lastpoint = new Point(e.X, e.Y);
        }

        private void panel1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.Left += e.X - lastpoint.X;
                this.Top += e.Y - lastpoint.Y;
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            AboutUs aboutUs = new AboutUs();
            aboutUs.Show();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            Process.Start("");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            DllInjector dllInjector = new DllInjector();
            dllInjector.Show();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            C_CodeGenerator codeGenerator = new C_CodeGenerator();
            codeGenerator.Show();
        }

        private void button7_Click(object sender, EventArgs e)
        {

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked == true)
            {
                checkBox1.Text = "AI: Enabled";
                checkBox1.BackColor = Color.Green;
            }

            if (checkBox1.Checked == false)
            {
                checkBox1.Text = "AI: Disabled";
                checkBox1.BackColor = Color.Red;
            }
        }
    }
}