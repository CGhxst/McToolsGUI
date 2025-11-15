using McCrypt;
using McCrypt.PackData;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace McDecryptor
{
    public class FormMain : Form
    {
        private ListView listPacks;
        private Panel panelControls;
        private Button btnRefresh;
        private Button btnDecryptSelected;
        private Button btnDecryptAll;
        private CheckBox chkZip;
        private CheckBox chkCrack;
        private Button btnToggleView;
        private Button btnOpenOutput;
        private ProgressBar progress;
        private TextBox txtLog;
        private PEntry[] currentEntries = new PEntry[0];

        private ImageList imgList;
        private ImageList imgListLarge;
        private Dictionary<string, int> iconIndexByPath;
        private Dictionary<string, int> iconIndexLargeByPath;
        private int fallbackIndex = 0;
        private int fallbackLargeIndex = 0;
        private Dictionary<string, int> typeFallbackIndices = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
        private Dictionary<string, int> typeFallbackLargeIndices = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
        private bool gridMode = false;

        public FormMain()
        {
            Text = "McDecryptor";
            Width = 1000;
            Height = 600;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MaximumSize = new Size(1000, 600);
            MinimumSize = new Size(800, 500);

            // Set the window (Form) icon to the executable's embedded application icon so it matches the EXE
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            imgList = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(32, 32) };
            imgListLarge = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(64, 64) };
            iconIndexByPath = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            iconIndexLargeByPath = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            AddFallbackImage();
            AddTypeFallbackImages();
            AddFallbackImageLarge();
            AddTypeFallbackImagesLarge();

            listPacks = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = true,
                Dock = DockStyle.Top,
                Height = 230,
                SmallImageList = imgList,
                LargeImageList = imgListLarge
            };
            listPacks.Columns.Add("Type", 120);
            listPacks.Columns.Add("Product", 140);
            listPacks.Columns.Add("Name", 400);
            listPacks.Columns.Add("UUID", 250);

            panelControls = new Panel { Dock = DockStyle.Top, Height = 55 };

            btnRefresh = new Button { Text = "Refresh", Left = 10, Top = 10, Width = 80 };
            btnDecryptSelected = new Button { Text = "Decrypt Selected", Left = 100, Top = 10, Width = 130 };
            btnDecryptAll = new Button { Text = "Decrypt All", Left = 240, Top = 10, Width = 110 };
            chkZip = new CheckBox { Text = "Zip Packs", Left = 370, Top = 14, AutoSize = true, Checked = Config.ZipPacks };
            chkCrack = new CheckBox { Text = "Crack Packs", Left = 470, Top = 14, AutoSize = true, Checked = Config.CrackPacks };
            btnToggleView = new Button { Text = "Grid View", Left = 580, Top = 10, Width = 90 };
            btnOpenOutput = new Button { Text = "Open Output Folder", Left = 680, Top = 10, Width = 130 };
            progress = new ProgressBar { Dock = DockStyle.Bottom, Height = 18 };

            panelControls.Controls.AddRange(new Control[] { btnRefresh, btnDecryptSelected, btnDecryptAll, chkZip, chkCrack, btnToggleView, btnOpenOutput });

            txtLog = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                ReadOnly = true
            };

            Controls.Add(txtLog);
            Controls.Add(panelControls);
            Controls.Add(listPacks);
            Controls.Add(progress);

            btnRefresh.Click += (s, e) => LoadEntries();
            btnDecryptSelected.Click += (s, e) => DecryptSelected();
            btnDecryptAll.Click += (s, e) => DecryptAll();
            btnToggleView.Click += (s, e) => ToggleGridView();
            btnOpenOutput.Click += (s, e) => OpenOutputFolder();

            Shown += (s, e) => Initialize();
        }

        private void ToggleGridView()
        {
            gridMode = !gridMode;
            btnToggleView.Text = gridMode ? "List View" : "Grid View";
            RebuildItems();
        }

        private void RebuildItems()
        {
            if (currentEntries == null) return;
            listPacks.BeginUpdate();
            listPacks.Items.Clear();

            if (gridMode)
            {
                listPacks.View = View.LargeIcon;
                foreach (var entry in currentEntries)
                {
                    int imgIndex = GetIconIndexLarge(entry);
                    long sz = GetDirectorySize(entry.FilePath);
                    var item = new ListViewItem(entry.Name + " (" + FormatSize(sz) + ")", imgIndex) { Tag = entry };
                    listPacks.Items.Add(item);
                }
            }
            else
            {
                listPacks.View = View.Details;
                if (listPacks.Columns.Count != 5)
                {
                    listPacks.Columns.Clear();
                    listPacks.Columns.Add("Type", 120);
                    listPacks.Columns.Add("Product", 140);
                    listPacks.Columns.Add("Name", 350);
                    listPacks.Columns.Add("Size", 90);
                    listPacks.Columns.Add("UUID", 250);
                }
                foreach (var entry in currentEntries)
                {
                    int imgIndex = GetIconIndex(entry);
                    long sz = GetDirectorySize(entry.FilePath);
                    var item = new ListViewItem(new[] { entry.Type, entry.ProductType, entry.Name, FormatSize(sz), entry.Uuid }, imgIndex) { Tag = entry };
                    listPacks.Items.Add(item);
                }
            }
            listPacks.EndUpdate();
        }

        private void AddFallbackImage()
        {
            Image img;
            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "illager.ico");
            if (File.Exists(icoPath))
            {
                using (var icon = new Icon(icoPath, 32, 32)) img = icon.ToBitmap();
            }
            else
            {
                img = new Bitmap(32, 32);
                using (Graphics g = Graphics.FromImage(img)) { g.Clear(Color.DimGray); g.DrawRectangle(Pens.Black, 0, 0, 31, 31); }
            }
            imgList.Images.Add(img);
            fallbackIndex = 0;
        }
        private void AddFallbackImageLarge()
        {
            Image img;
            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "illager.ico");
            if (File.Exists(icoPath))
            {
                using (var icon = new Icon(icoPath, 64, 64)) img = icon.ToBitmap();
            }
            else
            {
                img = new Bitmap(64, 64);
                using (Graphics g = Graphics.FromImage(img)) { g.Clear(Color.DimGray); g.DrawRectangle(Pens.Black, 0, 0, 63, 63); }
            }
            imgListLarge.Images.Add(img);
            fallbackLargeIndex = 0;
        }

        private void AddTypeFallbackImages()
        {
            var mapping = new Dictionary<string, Color>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"resource_packs", Color.SteelBlue}, {"skin_packs", Color.DeepPink}, {"world_templates", Color.ForestGreen}, {"persona", Color.MediumPurple}, {"behavior_packs", Color.DarkOrange}, {"behaviour_packs", Color.DarkOrange}, {"minecraftWorlds", Color.SaddleBrown}, {"addon", Color.Teal}
            };
            foreach (var kv in mapping)
            {
                if (typeFallbackIndices.ContainsKey(kv.Key)) continue;
                Bitmap bmp = new Bitmap(32, 32);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(kv.Value);
                    g.DrawRectangle(Pens.Black, 0, 0, 31, 31);
                    string letter = kv.Key.Substring(0, 1).ToUpperInvariant();
                    using (Font f = new Font(FontFamily.GenericSansSerif, 12, FontStyle.Bold))
                    using (Brush b = new SolidBrush(Color.White))
                    {
                        var sz = g.MeasureString(letter, f);
                        g.DrawString(letter, f, b, (32 - sz.Width) / 2, (32 - sz.Height) / 2);
                    }
                }
                imgList.Images.Add(bmp);
                typeFallbackIndices[kv.Key] = imgList.Images.Count - 1;
            }
        }
        private void AddTypeFallbackImagesLarge()
        {
            var mapping = new Dictionary<string, Color>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"resource_packs", Color.SteelBlue}, {"skin_packs", Color.DeepPink}, {"world_templates", Color.ForestGreen}, {"persona", Color.MediumPurple}, {"behavior_packs", Color.DarkOrange}, {"behaviour_packs", Color.DarkOrange}, {"minecraftWorlds", Color.SaddleBrown}, {"addon", Color.Teal}
            };
            foreach (var kv in mapping)
            {
                if (typeFallbackLargeIndices.ContainsKey(kv.Key)) continue;
                Bitmap bmp = new Bitmap(64, 64);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(kv.Value);
                    g.DrawRectangle(Pens.Black, 0, 0, 63, 63);
                    string letter = kv.Key.Substring(0, 1).ToUpperInvariant();
                    using (Font f = new Font(FontFamily.GenericSansSerif, 22, FontStyle.Bold))
                    using (Brush b = new SolidBrush(Color.White))
                    {
                        var sz = g.MeasureString(letter, f);
                        g.DrawString(letter, f, b, (64 - sz.Width) / 2, (64 - sz.Height) / 2);
                    }
                }
                imgListLarge.Images.Add(bmp);
                typeFallbackLargeIndices[kv.Key] = imgListLarge.Images.Count - 1;
            }
        }

        private int GetIconIndex(PEntry entry)
        {
            string packIconPath = Path.Combine(entry.FilePath, "pack_icon.png");
            if (!File.Exists(packIconPath)) return typeFallbackIndices.TryGetValue(entry.ProductType, out int t) ? t : fallbackIndex;
            if (iconIndexByPath.TryGetValue(entry.FilePath, out int existing)) return existing;
            try
            {
                using (var original = Image.FromFile(packIconPath))
                {
                    Bitmap scaled = new Bitmap(32, 32);
                    using (Graphics g = Graphics.FromImage(scaled))
                    {
                        g.Clear(Color.Transparent);
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        float scale = Math.Min(32f / original.Width, 32f / original.Height);
                        int sw = (int)(original.Width * scale);
                        int sh = (int)(original.Height * scale);
                        int x = (32 - sw) / 2;
                        int y = (32 - sh) / 2;
                        g.DrawImage(original, new Rectangle(x, y, sw, sh));
                    }
                    imgList.Images.Add(scaled);
                    int idx = imgList.Images.Count - 1;
                    iconIndexByPath[entry.FilePath] = idx;
                    return idx;
                }
            }
            catch { return typeFallbackIndices.TryGetValue(entry.ProductType, out int t) ? t : fallbackIndex; }
        }
        private int GetIconIndexLarge(PEntry entry)
        {
            string packIconPath = Path.Combine(entry.FilePath, "pack_icon.png");
            if (!File.Exists(packIconPath)) return typeFallbackLargeIndices.TryGetValue(entry.ProductType, out int t) ? t : fallbackLargeIndex;
            if (iconIndexLargeByPath.TryGetValue(entry.FilePath, out int existing)) return existing;
            try
            {
                using (var original = Image.FromFile(packIconPath))
                {
                    Bitmap scaled = new Bitmap(64, 64);
                    using (Graphics g = Graphics.FromImage(scaled))
                    {
                        g.Clear(Color.Transparent);
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        float scale = Math.Min(64f / original.Width, 64f / original.Height);
                        int sw = (int)(original.Width * scale);
                        int sh = (int)(original.Height * scale);
                        int x = (64 - sw) / 2;
                        int y = (64 - sh) / 2;
                        g.DrawImage(original, new Rectangle(x, y, sw, sh));
                    }
                    imgListLarge.Images.Add(scaled);
                    int idx = imgListLarge.Images.Count - 1;
                    iconIndexLargeByPath[entry.FilePath] = idx;
                    return idx;
                }
            }
            catch { return typeFallbackLargeIndices.TryGetValue(entry.ProductType, out int t) ? t : fallbackLargeIndex; }
        }

        private void Log(string line)
        {
            if (InvokeRequired) { Invoke(new Action<string>(Log), line); return; }
            txtLog.AppendText(line + Environment.NewLine);
        }

        private void Initialize()
        {
            try
            {
                Log("Initializing...");
                Config.Init();
                Directory.SetCurrentDirectory(Config.ApplicationFolder);
                Config.ReadConfig("McDecryptor.cfg");
                chkZip.Checked = Config.ZipPacks;
                chkCrack.Checked = Config.CrackPacks;
                btnToggleView.Text = gridMode ? "List View" : "Grid View";

                McCrypt.Keys.KeyDbFile = Config.KeysDbPath;
                if (File.Exists(Config.KeysDbPath))
                {
                    Log("Reading key database...");
                    McCrypt.Keys.ReadKeysDb(Config.KeysDbPath);
                }

                foreach (string optionsTxt in Config.OptionsTxts)
                {
                    Log("Reading options.txt: " + optionsTxt);
                    McCrypt.Keys.ReadOptionsTxt(optionsTxt);
                    foreach (string entFile in Directory.GetFiles(Config.MinecraftFolder, "*.ent", SearchOption.TopDirectoryOnly))
                        McCrypt.Keys.ReadEntitlementFile(entFile);
                }

                LoadEntries();
            }
            catch (Exception ex) { Log("Init failed: " + ex.Message); }
        }

        private void LoadEntries()
        {
            try
            {
                listPacks.BeginUpdate();
                listPacks.Items.Clear();
                iconIndexByPath.Clear();
                iconIndexLargeByPath.Clear();
                currentEntries = new PReader().PEntryList;
                listPacks.EndUpdate();
                Log("Loaded " + currentEntries.Length + " entries.");
                RebuildItems();
            }
            catch (Exception ex)
            {
                listPacks.EndUpdate();
                Log("Load failed: " + ex.Message);
            }
        }

        private void DecryptAll() => DecryptEntries(currentEntries);
        private void DecryptSelected()
        {
            var sel = new List<PEntry>();
            foreach (ListViewItem li in listPacks.SelectedItems)
                if (li.Tag is PEntry pe) sel.Add(pe);
            DecryptEntries(sel.ToArray());
        }

        private void DecryptEntries(PEntry[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            Config.ZipPacks = chkZip.Checked;
            Config.CrackPacks = chkCrack.Checked;

            Log("Starting decryption of " + entries.Length + " item(s)...");
            var bw = new BackgroundWorker { WorkerReportsProgress = true };
            bw.DoWork += (s, e) =>
            {
                int idx = 0;
                foreach (var cEntry in entries)
                {
                    Log("Starting: " + cEntry.Name);
                    idx++;
                    int scaled = (int)Math.Min(70.0, (double)idx / entries.Length * 70.0);
                    bw.ReportProgress(scaled, cEntry.Name);
                    try { DecryptSingle(cEntry); Log("Decrypted: " + cEntry.Name); }
                    catch (Exception ex) { Log("Failed: " + cEntry.Name + " - " + ex.Message); }
                }
            };
            bw.ProgressChanged += (s, e) => progress.Value = Math.Min(70, Math.Max(0, e.ProgressPercentage));
            bw.RunWorkerCompleted += (s, e) => { progress.Value = 100; Log("Finished."); };
            bw.RunWorkerAsync();
        }

        private static string EscapeFilename(string filename) => filename
            .Replace("/", "_").Replace("\\", "_").Replace(":", "_").Replace("?", "_")
            .Replace("*", "_").Replace("<", "_").Replace(">", "_").Replace("|", "_").Replace("\"", "_");

        private void DecryptSingle(PEntry cEntry)
        {
            string outRoot = Config.OutFolder ?? Path.Combine(Config.ApplicationFolder, "output");
            string outFolder = Path.Combine(outRoot, cEntry.ProductType, EscapeFilename(cEntry.Name));
            int counter = 1;
            string ogOutFolder = outFolder;
            while (Directory.Exists(outFolder)) { outFolder = ogOutFolder + "_" + counter; counter++; }
            Directory.CreateDirectory(outFolder);
            var pReader = new PReader();
            try
            {
                if (cEntry.ProductType == "addon")
                {
                    string subDir = Path.Combine(outFolder, cEntry.Type);
                    Directory.CreateDirectory(subDir);
                    CopyDirectory(cEntry.FilePath, subDir);
                    DecryptPack(subDir, cEntry);
                    foreach (PEntry dependancy in pReader.GetDependancies(cEntry))
                    {
                        string newDir = Path.Combine(outFolder, dependancy.Type);
                        Directory.CreateDirectory(newDir);
                        CopyDirectory(dependancy.FilePath, newDir);
                        DecryptPack(newDir, dependancy);
                    }
                }
                else if (cEntry.ProductType == "minecraftWorlds")
                {
                    CopyDirectory(cEntry.FilePath, outFolder);
                    DecryptPack(outFolder, cEntry);
                    foreach (PEntry dependancy in pReader.GetDependancies(cEntry))
                    {
                        string newDir = Path.Combine(outFolder, dependancy.ProductType, Path.GetFileName(dependancy.FilePath));
                        CopyDirectory(dependancy.FilePath, newDir);
                        DecryptPack(newDir, dependancy);
                    }
                }
                else
                {
                    CopyDirectory(cEntry.FilePath, outFolder);
                    DecryptPack(outFolder, cEntry);
                }

                if (Config.ZipPacks)
                {
                    string ext;
                    if (cEntry.ProductType == "world_templates") ext = ".mctemplate";
                    else if (cEntry.ProductType == "minecraftWorlds") ext = ".mcworld";
                    else if (cEntry.ProductType == "addon") ext = ".mcaddon";
                    else if (cEntry.ProductType == "persona") ext = ".mcpersona";
                    else ext = ".mcpack";
                    string fname = outFolder + ext;
                    if (File.Exists(fname)) File.Delete(fname);
                    ZipFile.CreateFromDirectory(outFolder, fname, CompressionLevel.NoCompression, false);
                    Directory.Delete(outFolder, true);
                }
            }
            catch
            {
                Directory.Delete(outFolder, true);
                throw;
            }
        }

        private static void CopyFile(string src, string dst)
        {
            using (var fs = File.OpenRead(src))
            {
                using (var wfd = File.OpenWrite(dst))
                {
                    fs.CopyTo(wfd);
                }
            }
        }
        private static void CopyDirectory(string sourcePath, string targetPath)
        {
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            foreach (string newPath in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
                CopyFile(newPath, newPath.Replace(sourcePath, targetPath));
        }
        private static void DecryptPack(string filePath, PEntry entry)
        {
            string levelDatFile = Path.Combine(filePath, "level.dat");
            string skinsJsonFile = Path.Combine(filePath, "skins.json");
            string oldSchoolZipe = Path.Combine(filePath, "content.zipe");
            Marketplace.DecryptContents(filePath, entry.ProductType);
            SaveContentKey(filePath, entry);
            if (Config.CrackPacks)
            {
                if (File.Exists(oldSchoolZipe)) Marketplace.CrackZipe(oldSchoolZipe);
                if (File.Exists(levelDatFile)) Marketplace.CrackLevelDat(levelDatFile);
                if (File.Exists(skinsJsonFile)) Marketplace.CrackSkinsJson(skinsJsonFile);
            }
        }
        private static void SaveContentKey(string packPath, PEntry entry)
        {
            try
            {
                if (!File.Exists(entry.ManifestPath)) return;
                string uuid = Manifest.ReadUUID(entry.ManifestPath);
                byte[] key = McCrypt.Keys.LookupKey(uuid);
                if (key == null) return;
                if (entry.ProductType == "skin_packs" || entry.ProductType == "persona") return;
                File.WriteAllText(Path.Combine(packPath, "content.key"), Encoding.UTF8.GetString(key));
            }
            catch { }
        }

        private void OpenOutputFolder()
        {
            try
            {
                string root = Config.OutFolder;
                if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(Config.ApplicationFolder, "output");
                Directory.CreateDirectory(root);
                Process.Start(root);
            }
            catch (Exception ex) { Log("Failed to open output folder: " + ex.Message); }
        }

        private string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##} {1}", size, units[unit]);
        }
        private long GetDirectorySize(string path)
        {
            long total = 0;
            try
            {
                foreach (var f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    try { total += new FileInfo(f).Length; } catch { }
            }
            catch { }
            return total;
        }
    }
}
