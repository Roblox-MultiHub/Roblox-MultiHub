using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RobloxMultiHub
{
    // Accounts are always encrypted on disk:
    //   mode 'D' = Windows DPAPI (tied to your Windows user, no password to remember)
    //   mode 'P' = your own password (AES-256 + HMAC), portable between PCs
    public static class Store
    {
        public static string Dir;
        public static string SettingsPath;
        public static string AccountsPath;
        public static string BackupDir;
        public static AppSettings Settings = new AppSettings();
        public static List<Account> Accounts = new List<Account>();
        public static string Password;            // null = DPAPI mode
        public static bool Loaded;                // never overwrite the file until it loaded successfully
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RobloxMultiHub.v1");

        public static void Init() { Init(null); }

        // dataDir: used by the admin memory helper, which may run under another Windows account (UAC with
        // an administrator's password) and must still use YOUR data folder.
        public static void Init(string dataDir)
        {
            Dir = !string.IsNullOrEmpty(dataDir) ? dataDir : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RobloxMultiHub");
            Directory.CreateDirectory(Dir);
            BackupDir = Path.Combine(Dir, "Backups");
            SettingsPath = Path.Combine(Dir, "settings.json");
            AccountsPath = Path.Combine(Dir, "accounts.dat");
            Log.Init(Dir);
            LoadSettings();
            Log.TrimOld(Settings.LogHours);
        }

        public static void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    AppSettings s = Json.From<AppSettings>(File.ReadAllText(SettingsPath, Encoding.UTF8));
                    if (s != null) Settings = s;
                }
            }
            catch (Exception ex) { Log.Error("LoadSettings", ex); }
            if (Settings.Layout == null) Settings.Layout = new LayoutSettings();
            if (Settings.Groups == null) Settings.Groups = new List<string>();
            if (Settings.Recent == null) Settings.Recent = new List<GameEntry>();
            if (Settings.Pinned == null) Settings.Pinned = new List<PinnedRect>();
            if (Settings.Presets == null) Settings.Presets = new List<Preset>();
            if (Settings.Macro == null) Settings.Macro = new MacroSettings();
            if (Settings.Mem == null) Settings.Mem = new MemSettings();
            Settings.Mem.Threshold = Math.Max(30, Math.Min(98, Settings.Mem.Threshold));
            if (string.IsNullOrEmpty(Settings.AntiAfkAction)) Settings.AntiAfkAction = "Nudge";
            if (Settings.StartupPreset == null) Settings.StartupPreset = "";
            if (!Settings.LaunchDefaultsV2)
            {
                // Direct launches (RobloxPlayerBeta.exe with extra arguments) got human-verification checks;
                // the roblox-player: link, exactly like Roblox Account Manager, does not. Switch once.
                Settings.LaunchMethod = "Protocol";
                Settings.LaunchDefaultsV2 = true;
            }
        }

        public static void SaveSettings()
        {
            try { AtomicWrite(SettingsPath, Encoding.UTF8.GetBytes(Json.To(Settings))); }
            catch (Exception ex) { Log.Error("SaveSettings", ex); }
        }

        public static bool AccountsFileIsPasswordProtected()
        {
            if (!File.Exists(AccountsPath)) return false;
            byte[] b = File.ReadAllBytes(AccountsPath);
            return b.Length > 0 && b[0] == (byte)'P';
        }

        // Returns null on success, otherwise a human readable error.
        public static string LoadAccounts(string password)
        {
            try
            {
                if (!File.Exists(AccountsPath))
                {
                    Accounts = new List<Account>();
                    Password = password;
                    Loaded = true;
                    return null;
                }
                byte[] raw = File.ReadAllBytes(AccountsPath);
                string json = Decrypt(raw, password);
                AccountFile f = Json.From<AccountFile>(json);
                Accounts = (f != null && f.Accounts != null) ? f.Accounts : new List<Account>();
                Password = password;
                Loaded = true;
                Backup();
                return null;
            }
            catch (CryptographicException)
            {
                if (password != null) return "Wrong password (or the file is damaged).";
                return "This accounts file could not be decrypted. It was probably created by a different Windows user or PC.";
            }
            catch (Exception ex)
            {
                Log.Error("LoadAccounts", ex);
                return "Could not read the accounts file: " + ex.Message;
            }
        }

        public static void StartFresh()
        {
            try { if (File.Exists(AccountsPath)) File.Copy(AccountsPath, AccountsPath + ".old", true); }
            catch (Exception) { }
            Accounts = new List<Account>();
            Password = null;
            Loaded = true;
            SaveAccounts();
        }

        public static void SaveAccounts()
        {
            if (!Loaded) return;
            try
            {
                AccountFile f = new AccountFile();
                f.Accounts = Accounts;
                byte[] enc = Encrypt(Encoding.UTF8.GetBytes(Json.To(f)), Password);
                AtomicWrite(AccountsPath, enc);
            }
            catch (Exception ex) { Log.Error("SaveAccounts", ex); }
        }

        public static void Backup()
        {
            try
            {
                if (!File.Exists(AccountsPath)) return;
                Directory.CreateDirectory(BackupDir);
                string name = "accounts_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".dat";
                File.Copy(AccountsPath, Path.Combine(BackupDir, name), true);
                string[] all = Directory.GetFiles(BackupDir, "accounts_*.dat");
                Array.Sort(all);
                for (int i = 0; i < all.Length - 10; i++) File.Delete(all[i]);
            }
            catch (Exception) { }
        }

        static void AtomicWrite(string path, byte[] data)
        {
            string tmp = path + ".tmp";
            File.WriteAllBytes(tmp, data);
            File.Copy(tmp, path, true);
            File.Delete(tmp);
        }

        static byte[] Rand(int n)
        {
            byte[] b = new byte[n];
            using (RNGCryptoServiceProvider r = new RNGCryptoServiceProvider()) { r.GetBytes(b); }
            return b;
        }

        static byte[] Encrypt(byte[] data, string password)
        {
            if (password == null)
            {
                byte[] enc = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
                byte[] outD = new byte[enc.Length + 1];
                outD[0] = (byte)'D';
                Buffer.BlockCopy(enc, 0, outD, 1, enc.Length);
                return outD;
            }
            byte[] salt = Rand(16);
            byte[] iv = Rand(16);
            byte[] keys = new Rfc2898DeriveBytes(password, salt, 100000).GetBytes(64);
            byte[] encKey = new byte[32];
            byte[] macKey = new byte[32];
            Buffer.BlockCopy(keys, 0, encKey, 0, 32);
            Buffer.BlockCopy(keys, 32, macKey, 0, 32);
            byte[] cipher;
            using (Aes aes = Aes.Create())
            {
                aes.Key = encKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(data, 0, data.Length);
                        cs.FlushFinalBlock();
                    }
                    cipher = ms.ToArray();
                }
            }
            byte[] body = new byte[16 + 16 + cipher.Length];
            Buffer.BlockCopy(salt, 0, body, 0, 16);
            Buffer.BlockCopy(iv, 0, body, 16, 16);
            Buffer.BlockCopy(cipher, 0, body, 32, cipher.Length);
            byte[] mac;
            using (HMACSHA256 h = new HMACSHA256(macKey)) { mac = h.ComputeHash(body); }
            byte[] result = new byte[1 + 32 + body.Length];
            result[0] = (byte)'P';
            Buffer.BlockCopy(mac, 0, result, 1, 32);
            Buffer.BlockCopy(body, 0, result, 33, body.Length);
            return result;
        }

        static string Decrypt(byte[] raw, string password)
        {
            if (raw.Length < 2) throw new CryptographicException("empty");
            if (raw[0] == (byte)'D')
            {
                byte[] enc = new byte[raw.Length - 1];
                Buffer.BlockCopy(raw, 1, enc, 0, enc.Length);
                byte[] data = ProtectedData.Unprotect(enc, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            if (raw[0] == (byte)'P')
            {
                if (password == null) throw new CryptographicException("password needed");
                if (raw.Length < 1 + 32 + 32 + 16) throw new CryptographicException("short");
                byte[] mac = new byte[32];
                Buffer.BlockCopy(raw, 1, mac, 0, 32);
                byte[] body = new byte[raw.Length - 33];
                Buffer.BlockCopy(raw, 33, body, 0, body.Length);
                byte[] salt = new byte[16];
                byte[] iv = new byte[16];
                Buffer.BlockCopy(body, 0, salt, 0, 16);
                Buffer.BlockCopy(body, 16, iv, 0, 16);
                byte[] keys = new Rfc2898DeriveBytes(password, salt, 100000).GetBytes(64);
                byte[] encKey = new byte[32];
                byte[] macKey = new byte[32];
                Buffer.BlockCopy(keys, 0, encKey, 0, 32);
                Buffer.BlockCopy(keys, 32, macKey, 0, 32);
                byte[] check;
                using (HMACSHA256 h = new HMACSHA256(macKey)) { check = h.ComputeHash(body); }
                int diff = 0;
                for (int i = 0; i < 32; i++) diff |= mac[i] ^ check[i];
                if (diff != 0) throw new CryptographicException("bad mac");
                using (Aes aes = Aes.Create())
                {
                    aes.Key = encKey;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(body, 32, body.Length - 32);
                            cs.FlushFinalBlock();
                        }
                        return Encoding.UTF8.GetString(ms.ToArray());
                    }
                }
            }
            throw new CryptographicException("unknown format");
        }

        // ---- helpers used by the UI ----
        public static void RenumberOrder()
        {
            for (int i = 0; i < Accounts.Count; i++) Accounts[i].Order = i;
        }

        public static List<string> AllGroups()
        {
            List<string> g = new List<string>(Settings.Groups);
            foreach (Account a in Accounts)
                if (!string.IsNullOrEmpty(a.Group) && !g.Contains(a.Group)) g.Add(a.Group);
            g.Sort(StringComparer.OrdinalIgnoreCase);
            return g;
        }

        public static Account FindByCookie(string cookie)
        {
            return Accounts.FirstOrDefault(a => a.Cookie == cookie);
        }
    }
}
