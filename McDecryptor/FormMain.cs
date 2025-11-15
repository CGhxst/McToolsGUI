using McCrypt;
using McCrypt.PackData;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private ProgressBar progress;
        private TextBox txtLog;
        private PEntry[] currentEntries = new PEntry[0];

        // Image handling
        private ImageList imgList; // holds icons
        private Dictionary<string, int> iconIndexByPath; // cache path->image index
        private int fallbackIndex = 0;
        private Dictionary<string, int> typeFallbackIndices = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);

        public FormMain()
        {
            Text = "McDecryptor";
            Width = 1000;
            Height = 600;
            FormBorderStyle = FormBorderStyle.Sizable; // allow resize
            MaximizeBox = true; // keep maximize button behavior constrained by MaximumSize
            MaximumSize = new Size(1000, 600); // cap enlargement
            MinimumSize = new Size(800, 500); // allow shrinking but keep usability

            // Initialize image list
            imgList = new ImageList();
            imgList.ColorDepth = ColorDepth.Depth32Bit;
            imgList.ImageSize = new Size(32, 32);
            iconIndexByPath = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            AddFallbackImage();
            AddTypeFallbackImages();

            // Pack list
            listPacks = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = true,
                Dock = DockStyle.Top,
                Height = 230,
                SmallImageList = imgList // attach images
            };
            listPacks.Columns.Add("Type", 120);
            listPacks.Columns.Add("Product", 140);
            listPacks.Columns.Add("Name", 400);
            listPacks.Columns.Add("UUID", 250);

            // Control panel
            panelControls = new Panel
            {
                Dock = DockStyle.Top,
                Height = 55
            };

            btnRefresh = new Button { Text = "Refresh", Left = 10, Top = 10, Width = 80 };            
            btnDecryptSelected = new Button { Text = "Decrypt Selected", Left = 100, Top = 10, Width = 130 };
            btnDecryptAll = new Button { Text = "Decrypt All", Left = 240, Top = 10, Width = 110 };
            chkZip = new CheckBox { Text = "Zip Packs", Left = 370, Top = 14, AutoSize = true, Checked = Config.ZipPacks };
            chkCrack = new CheckBox { Text = "Crack Packs", Left = 470, Top = 14, AutoSize = true, Checked = Config.CrackPacks };
            progress = new ProgressBar { Left = 600, Top = 15, Width = 360, Height = 22 };

            panelControls.Controls.Add(btnRefresh);
            panelControls.Controls.Add(btnDecryptSelected);
            panelControls.Controls.Add(btnDecryptAll);
            panelControls.Controls.Add(chkZip);
            panelControls.Controls.Add(chkCrack);
            panelControls.Controls.Add(progress);

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

            btnRefresh.Click += (s, e) => LoadEntries();
            btnDecryptSelected.Click += (s, e) => DecryptSelected();
            btnDecryptAll.Click += (s, e) => DecryptAll();

            Shown += (s, e) => Initialize();
        }

        private void AddFallbackImage()
        {
            try
            {
                Image fallback;
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "illager.ico");
                if (File.Exists(icoPath))
                {
                    using (var icon = new Icon(icoPath, 32, 32))
                    {
                        fallback = icon.ToBitmap();
                    }
                }
                else
                {
                    // simple gray square
                    Bitmap bmp = new Bitmap(32, 32);
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.DimGray);
                        g.DrawRectangle(Pens.Black, 0, 0, 31, 31);
                    }
                    fallback = bmp;
                }
                imgList.Images.Add(fallback); // index 0
                fallbackIndex = 0;
            }
            catch
            {
                // ensure we always have at least one image
                Bitmap bmp = new Bitmap(32, 32);
                imgList.Images.Add(bmp);
                fallbackIndex = 0;
            }
        }
        private void AddTypeFallbackImages()
        {
            // Define colors per product type
            var mapping = new Dictionary<string, Color>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"resource_packs", Color.SteelBlue},
                {"skin_packs", Color.DeepPink},
                {"world_templates", Color.ForestGreen},
                {"persona", Color.MediumPurple},
                {"behavior_packs", Color.DarkOrange},
                {"behaviour_packs", Color.DarkOrange}, // spelling variant
                {"minecraftWorlds", Color.SaddleBrown},
                {"addon", Color.Teal}
            };
            foreach (var kv in mapping)
            {
                if (typeFallbackIndices.ContainsKey(kv.Key)) continue;
                Bitmap bmp = new Bitmap(32, 32);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(kv.Value);
                    g.DrawRectangle(Pens.Black, 0, 0, 31, 31);
                    // optional letter overlay
                    string letter = kv.Key.Substring(0, 1).ToUpperInvariant();
                    using (Font f = new Font(FontFamily.GenericSansSerif, 12, FontStyle.Bold))
                    using (Brush b = new SolidBrush(Color.White))
                    {
                        var sz = g.MeasureString(letter, f);
                        g.DrawString(letter, f, b, (32 - sz.Width)/2, (32 - sz.Height)/2);
                    }
                }
                imgList.Images.Add(bmp);
                typeFallbackIndices[kv.Key] = imgList.Images.Count - 1;
            }
        }

        private int GetIconIndex(PEntry entry)
        {
            string packIconPath = Path.Combine(entry.FilePath, "pack_icon.png");
            if (!File.Exists(packIconPath))
            {
                // choose type-specific fallback if available
                if (typeFallbackIndices.TryGetValue(entry.ProductType, out int tIdx))
                    return tIdx;
                return fallbackIndex;
            }
            // Use entry.FilePath as key
            if (iconIndexByPath.TryGetValue(entry.FilePath, out int existing))
                return existing;
            try
            {
                using (var original = Image.FromFile(packIconPath))
                {
                    Bitmap scaled = new Bitmap(32, 32);
                    using (Graphics g = Graphics.FromImage(scaled))
                    {
                        g.Clear(Color.Transparent);
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        int w = original.Width;
                        int h = original.Height;
                        float scale = Math.Min(32f / w, 32f / h);
                        int sw = (int)(w * scale);
                        int sh = (int)(h * scale);
                        int x = (32 - sw) / 2;
                        int y = (32 - sh) / 2;
                        g.DrawImage(original, new Rectangle(x, y, sw, sh));
                    }
                    imgList.Images.Add(scaled);
                    int newIndex = imgList.Images.Count - 1;
                    iconIndexByPath[entry.FilePath] = newIndex;
                    return newIndex;
                }
            }
            catch
            {
                if (typeFallbackIndices.TryGetValue(entry.ProductType, out int tIdx))
                    return tIdx;
                return fallbackIndex;
            }
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
                iconIndexByPath.Clear(); // reset cache except fallback
                PReader reader = new PReader();
                currentEntries = reader.PEntryList;
                foreach (var entry in currentEntries)
                {
                    int imgIndex = GetIconIndex(entry);
                    var item = new ListViewItem(new[] { entry.Type, entry.ProductType, entry.Name, entry.Uuid }, imgIndex);
                    item.Tag = entry;
                    listPacks.Items.Add(item);
                }
                listPacks.EndUpdate();
                Log("Loaded " + currentEntries.Length + " entries.");
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

            BackgroundWorker bw = new BackgroundWorker { WorkerReportsProgress = true };
            bw.DoWork += (s, e) =>
            {
                int idx = 0;
                foreach (var cEntry in entries)
                {
                    idx++;
                    bw.ReportProgress((int)(idx * 100.0 / entries.Length), cEntry.Name);
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
            bw.ProgressChanged += (s, e) => progress.Value = Math.Min(100, Math.Max(0, e.ProgressPercentage));
            bw.RunWorkerCompleted += (s, e) => { progress.Value = 0; Log("Finished."); };
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
            while (Directory.Exists(outFolder))
            {
                outFolder = ogOutFolder + "_" + counter.ToString();
                counter++;
            }
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
            catch
            {
                Directory.Delete(outFolder, true);
                throw;
            }
        }

        private static void CopyFile(string src, string dst)
        {
            using (FileStream fs = File.OpenRead(src))
            {
                using (FileStream wfd = File.OpenWrite(dst))
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
    }
}
