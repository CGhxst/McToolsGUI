using McCrypt;
using McCrypt.PackData;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics; // added for Process.Start
using System.Drawing; // added for images
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
        private Button btnToggleView; // replaced checkbox with button
        private Button btnOpenOutput; // new button to open output folder
        private ProgressBar progress;
        private TextBox txtLog;
        private PEntry[] currentEntries = new PEntry[0];

        // Image handling
        private ImageList imgList; // small (details view)
        private ImageList imgListLarge; // large (grid view)
        private Dictionary<string, int> iconIndexByPath; // small cache
        private Dictionary<string, int> iconIndexLargeByPath; // large cache
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
            FormBorderStyle = FormBorderStyle.Sizable; // allow resize
            MaximizeBox = true; // keep maximize button behavior constrained by MaximumSize
            MaximumSize = new Size(1000, 600); // cap enlargement
            MinimumSize = new Size(800, 500); // allow shrinking but keep usability

            // Initialize image lists
            imgList = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(32, 32) };
            imgListLarge = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(64, 64) };
            iconIndexByPath = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            iconIndexLargeByPath = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            AddFallbackImage();
            AddTypeFallbackImages();
            AddFallbackImageLarge();
            AddTypeFallbackImagesLarge();

            // Pack list
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

            // Control panel
            panelControls = new Panel { Dock = DockStyle.Top, Height = 55 };

            btnRefresh = new Button { Text = "Refresh", Left = 10, Top = 10, Width = 80 };
            btnDecryptSelected = new Button { Text = "Decrypt Selected", Left = 100, Top = 10, Width = 130 };
            btnDecryptAll = new Button { Text = "Decrypt All", Left = 240, Top = 10, Width = 110 };
            chkZip = new CheckBox { Text = "Zip Packs", Left = 370, Top = 14, AutoSize = true, Checked = Config.ZipPacks };
            chkCrack = new CheckBox { Text = "Crack Packs", Left = 470, Top = 14, AutoSize = true, Checked = Config.CrackPacks };
            btnToggleView = new Button { Text = "Grid View", Left = 580, Top = 10, Width = 90 }; // new toggle button
            btnOpenOutput = new Button { Text = "Open Output Folder", Left = 680, Top = 10, Width = 130 }; // updated text
            // Remove progress from control panel positioning
            progress = new ProgressBar { Dock = DockStyle.Bottom, Height = 18 }; // dock at bottom below log

            panelControls.Controls.Add(btnRefresh);
            panelControls.Controls.Add(btnDecryptSelected);
            panelControls.Controls.Add(btnDecryptAll);
            panelControls.Controls.Add(chkZip);
            panelControls.Controls.Add(chkCrack);
            panelControls.Controls.Add(btnToggleView);
            panelControls.Controls.Add(btnOpenOutput);
            // panelControls.Controls.Add(progress); // removed

            // Log box
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
            Controls.Add(progress); // added progress here after txtLog

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
                    string nameWithSize = entry.Name + " (" + FormatSize(sz) + ")";
                    var item = new ListViewItem(nameWithSize, imgIndex) { Tag = entry };
                    listPacks.Items.Add(item);
                }
            }
            else
            {
                listPacks.View = View.Details;
                // ensure columns count >=5 with Size column inserted if missing
                if (listPacks.Columns.Count == 0)
                {
                    listPacks.Columns.Add("Type", 120);
                    listPacks.Columns.Add("Product", 140);
                    listPacks.Columns.Add("Name", 350);
                    listPacks.Columns.Add("Size", 90);
                    listPacks.Columns.Add("UUID", 250);
                }
                else if (listPacks.Columns.Count == 4) // previously without Size
                {
                    // Recreate columns to include Size
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
                    string sizeStr = FormatSize(sz);
                    var item = new ListViewItem(new[] { entry.Type, entry.ProductType, entry.Name, sizeStr, entry.Uuid }, imgIndex) { Tag = entry };
                    listPacks.Items.Add(item);
                }
            }
            listPacks.EndUpdate();
        }

        private void AddFallbackImage()
        {
            try
            {
                Image fallback;
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "illager.ico");
                if (File.Exists(icoPath))
                {
                    using (var icon = new Icon(icoPath, 32, 32)) fallback = icon.ToBitmap();
                }
                else
                {
                    Bitmap bmp = new Bitmap(32, 32);
                    using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.DimGray); g.DrawRectangle(Pens.Black, 0, 0, 31, 31); }
                    fallback = bmp;
                }
                imgList.Images.Add(fallback);
                fallbackIndex = 0;
            }
            catch { Bitmap bmp = new Bitmap(32, 32); imgList.Images.Add(bmp); fallbackIndex = 0; }
        }
        private void AddFallbackImageLarge()
        {
            try
            {
                Image fallback;
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "illager.ico");
                if (File.Exists(icoPath))
                {
                    using (var icon = new Icon(icoPath, 64, 64)) fallback = icon.ToBitmap();
                }
                else
                {
                    Bitmap bmp = new Bitmap(64, 64);
                    using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.DimGray); g.DrawRectangle(Pens.Black, 0, 0, 63, 63); }
                    fallback = bmp;
                }
                imgListLarge.Images.Add(fallback);
                fallbackLargeIndex = 0;
            }
            catch { Bitmap bmp = new Bitmap(64, 64); imgListLarge.Images.Add(bmp); fallbackLargeIndex = 0; }
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
            if (!File.Exists(packIconPath))
            {
                if (typeFallbackIndices.TryGetValue(entry.ProductType, out int tIdx)) return tIdx;
                return fallbackIndex;
            }
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
                        int w = original.Width; int h = original.Height; float scale = Math.Min(32f / w, 32f / h);
                        int sw = (int)(w * scale); int sh = (int)(h * scale);
                        int x = (32 - sw) / 2; int y = (32 - sh) / 2;
                        g.DrawImage(original, new Rectangle(x, y, sw, sh));
                    }
                    imgList.Images.Add(scaled);
                    int newIndex = imgList.Images.Count - 1;
                    iconIndexByPath[entry.FilePath] = newIndex;
                    return newIndex;
                }
            }
            catch { if (typeFallbackIndices.TryGetValue(entry.ProductType, out int tIdx)) return tIdx; return fallbackIndex; }
        }
        private int GetIconIndexLarge(PEntry entry)
        {
            string packIconPath = Path.Combine(entry.FilePath, "pack_icon.png");
            if (!File.Exists(packIconPath))
            {
                if (typeFallbackLargeIndices.TryGetValue(entry.ProductType, out int tIdx)) return tIdx;
                return fallbackLargeIndex;
            }
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
                        int w = original.Width; int h = original.Height; float scale = Math.Min(64f / w, 64f / h);
                        int sw = (int)(w * scale); int sh = (int)(h * scale);
                        int x = (64 - sw) / 2; int y = (64 - sh) / 2;
                        g.DrawImage(original, new Rectangle(x, y, sw, sh));
                    }
                    imgListLarge.Images.Add(scaled);
                    int newIndex = imgListLarge.Images.Count - 1;
                    iconIndexLargeByPath[entry.FilePath] = newIndex;
                    return newIndex;
                }
            }
            catch { if (typeFallbackLargeIndices.TryGetValue(entry.ProductType, out int tIdx)) return tIdx; return fallbackLargeIndex; }
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
                    string[] entFiles = Directory.GetFiles(Config.MinecraftFolder, "*.ent", SearchOption.TopDirectoryOnly);
                    foreach (string entFile in entFiles)
                    {
                        Log("Reading entitlement: " + Path.GetFileName(entFile));
                        McCrypt.Keys.ReadEntitlementFile(entFile);
                    }
                }

                LoadEntries();
            }
            catch (Exception ex)
            {
                Log("Init failed: " + ex.Message);
            }
        }

        private void LoadEntries()
        {
            try
            {
                listPacks.BeginUpdate();
                listPacks.Items.Clear();
                iconIndexByPath.Clear();
                iconIndexLargeByPath.Clear();
                PReader reader = new PReader();
                currentEntries = reader.PEntryList;
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
            List<PEntry> sel = new List<PEntry>();
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
            BackgroundWorker bw = new BackgroundWorker { WorkerReportsProgress = true };
            bw.DoWork += (s, e) =>
            {
                int idx = 0;
                foreach (var cEntry in entries)
                {
                    Log("Starting: " + cEntry.Name);
                    idx++;
                    // Scale progress to max 70% before completion
                    double raw = (double)idx / entries.Length; // 0..1
                    int scaled = (int)Math.Min(70.0, raw * 70.0); // cap at 70
                    bw.ReportProgress(scaled, cEntry.Name);
                    try
                    {
                        DecryptSingle(cEntry);
                        Log("Decrypted: " + cEntry.Name);
                    }
                    catch (Exception ex)
                    {
                        Log("Failed: " + cEntry.Name + " - " + ex.Message);
                    }
                }
            };
            bw.ProgressChanged += (s, e) =>
            {
                // Do not allow worker to set 100% prematurely
                progress.Value = Math.Min(70, Math.Max(0, e.ProgressPercentage));
            };
            bw.RunWorkerCompleted += (s, e) =>
            {
                // Jump to 100% only when fully finished
                progress.Value = 100;
                Log("Finished.");
            };
            bw.RunWorkerAsync();
        }

        private static string EscapeFilename(string filename)
        {
            return filename.Replace("/", "_").Replace("\\", "_").Replace(":", "_").Replace("?", "_").Replace("*", "_").Replace("<", "_").Replace(">", "_").Replace("|", "_").Replace("\"", "_");
        }

        private void DecryptSingle(PEntry cEntry)
        {
            string outRoot = Config.OutFolder ?? Path.Combine(Config.ApplicationFolder, "output");
            string outFolder = Path.Combine(outRoot, cEntry.ProductType, EscapeFilename(cEntry.Name));
            int counter = 1;
            string ogOutFolder = outFolder;
            while (Directory.Exists(outFolder)) { outFolder = ogOutFolder + "_" + counter.ToString(); counter++; }
            Directory.CreateDirectory(outFolder);
            PReader pReader = new PReader();
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
                    string ext = "";
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
            catch { Directory.Delete(outFolder, true); throw; }
        }

        private static void CopyFile(string src, string dst)
        {
            using (FileStream fs = File.OpenRead(src))
            using (FileStream wfd = File.OpenWrite(dst))
            { fs.CopyTo(wfd); }
        }
        private static void CopyDirectory(string sourcePath, string targetPath)
        {
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            foreach (string newPath in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories)) CopyFile(newPath, newPath.Replace(sourcePath, targetPath));
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
                if (string.IsNullOrWhiteSpace(root))
                    root = Path.Combine(Config.ApplicationFolder, "output");
                Directory.CreateDirectory(root);
                Process.Start(root);
            }
            catch (Exception ex)
            {
                Log("Failed to open output folder: " + ex.Message);
            }
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
                // Sum files
                string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                foreach (var f in files)
                {
                    try { total += new FileInfo(f).Length; } catch { }
                }
            }
            catch { }
            return total;
        }
    }
}
