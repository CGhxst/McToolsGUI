using McCrypt;
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace McEncryptor
{
    public class FormEncryptor : Form
    {
        private TextBox txtPackPath;
        private Button btnBrowse;
        private Button btnEncrypt;
        private TextBox txtLog;
        private TextBox txtUuid;
        private TextBox txtContentKey;
        private Button btnGenerateKey;
        private CheckBox chkAutoKeyLookup;
        private Label lblUuid;
        private Label lblContentKey;
        private Label lblPackPath;
        private FolderBrowserDialog folderDialog;

        public FormEncryptor()
        {
            Text = "McEncryptor";
            Width = 700;
            Height = 500;
            FormBorderStyle = FormBorderStyle.Sizable; // allow resize
            MaximizeBox = true;
            MaximumSize = new System.Drawing.Size(700, 500); // cap
            MinimumSize = new System.Drawing.Size(600, 420); // allow some shrink

            // Set the window (Form) icon to the executable's embedded application icon so the window shows the same illager icon without requiring an external .ico file.
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            lblPackPath = new Label { Text = "Pack Folder:", Left = 10, Top = 15, Width = 90 };
            txtPackPath = new TextBox { Left = 110, Top = 12, Width = 420 };
            btnBrowse = new Button { Text = "Browse", Left = 540, Top = 10, Width = 80 };
            btnEncrypt = new Button { Text = "Encrypt", Left = 540, Top = 45, Width = 80 };

            lblUuid = new Label { Text = "UUID:", Left = 10, Top = 55, Width = 90 };
            txtUuid = new TextBox { Left = 110, Top = 52, Width = 420, ReadOnly = true };

            lblContentKey = new Label { Text = "Content Key:", Left = 10, Top = 95, Width = 90 };
            txtContentKey = new TextBox { Left = 110, Top = 92, Width = 420 };
            btnGenerateKey = new Button { Text = "Generate", Left = 540, Top = 90, Width = 80 };

            chkAutoKeyLookup = new CheckBox { Text = "Auto lookup key (keys.db)", Left = 110, Top = 125, Width = 220, Checked = true };

            txtLog = new TextBox { Left = 10, Top = 160, Width = 650, Height = 290, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true };

            folderDialog = new FolderBrowserDialog();

            Controls.Add(lblPackPath);
            Controls.Add(txtPackPath);
            Controls.Add(btnBrowse);
            Controls.Add(btnEncrypt);
            Controls.Add(lblUuid);
            Controls.Add(txtUuid);
            Controls.Add(lblContentKey);
            Controls.Add(txtContentKey);
            Controls.Add(btnGenerateKey);
            Controls.Add(chkAutoKeyLookup);
            Controls.Add(txtLog);

            btnBrowse.Click += (s, e) => BrowseFolder();
            btnGenerateKey.Click += (s, e) => GenerateKey();
            btnEncrypt.Click += (s, e) => EncryptPack();
            // Removed txtPackPath.Leave auto-load to prevent premature validation
        }

        private void Log(string message)
        {
            txtLog.AppendText(message + Environment.NewLine);
        }

        private void BrowseFolder()
        {
            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                txtPackPath.Text = folderDialog.SelectedPath;
                LoadPackInfo();
            }
        }

        private void LoadPackInfo()
        {
            string path = txtPackPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                // Do not log anything for empty path
                return;
            }
            if (!Directory.Exists(path))
            {
                Log("Folder does not exist.");
                return;
            }

            string manifestPath = Path.Combine(path, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                Log("manifest.json not found in folder.");
                return;
            }

            try
            {
                string uuid = Manifest.ReadUUID(manifestPath);
                txtUuid.Text = uuid;
                Log("UUID: " + uuid);

                if (chkAutoKeyLookup.Checked)
                {
                    // load keys.db if present in exe directory
                    string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                    string keysDb = Path.Combine(exeDir, "keys.db");
                    if (File.Exists(keysDb))
                    {
                        McCrypt.Keys.KeyDbFile = keysDb;
                        if (McCrypt.Keys.LookupKey(uuid) == null)
                        {
                            Log("Key for UUID not cached. Using fallback or manual key.");
                        }
                        else
                        {
                            string keyStr = Encoding.UTF8.GetString(McCrypt.Keys.LookupKey(uuid));
                            txtContentKey.Text = keyStr;
                            Log("Found key in cache.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Failed to read manifest: " + ex.Message);
            }
        }

        private void GenerateKey()
        {
            string key = McCrypt.Keys.GenerateKey();
            txtContentKey.Text = key;
            Log("Generated new content key.");
        }

        private void EncryptPack()
        {
            string path = txtPackPath.Text.Trim();
            if (!Directory.Exists(path))
            {
                Log("Invalid pack path.");
                return;
            }
            string manifestPath = Path.Combine(path, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                Log("manifest.json missing.");
                return;
            }
            string uuid = txtUuid.Text.Trim();
            if (string.IsNullOrWhiteSpace(uuid))
            {
                Log("UUID not loaded.");
                return;
            }

            string contentKey = txtContentKey.Text.Trim();
            if (string.IsNullOrWhiteSpace(contentKey))
            {
                // fallback default key
                contentKey = "s5s5ejuDru4uchuF2drUFuthaspAbepE";
                txtContentKey.Text = contentKey;
                Log("Using fallback content key.");
            }

            try
            {
                Manifest.SignManifest(path);
                Marketplace.EncryptContents(path, uuid, contentKey);
                Log("Encryption complete.");
            }
            catch (Exception ex)
            {
                Log("Encryption failed: " + ex.Message);
            }
        }
    }
}
