using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Mappy.Data
{

    internal static class Crypto
    {
        // App-scoped entropy; change GUID per app.
        private static readonly byte[] Entropy = new Guid("b8f1e2a9-2c7a-4d6d-9e7d-1c6d6d3f5c33").ToByteArray();

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return string.Empty;
            var bytes = Encoding.UTF8.GetBytes(plain);
            var protectedBytes = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        public static string Unprotect(string cipher)
        {
            if (string.IsNullOrEmpty(cipher)) return string.Empty;
            var protectedBytes = Convert.FromBase64String(cipher);
            var bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
    }


    [DataContract]
    public sealed class EmailAccount
    {
        [DataMember(Order = 1)] public string Host { get; set; }
        [DataMember(Order = 2)] public int Port { get; set; }
        [DataMember(Order = 3)] public bool UseSsl { get; set; }
        [DataMember(Order = 4)] public string Username { get; set; }

        // Stored encrypted; not user-editable directly.
        [DataMember(Order = 5)] public string EncryptedPassword { get; set; }

        [DataMember(Order = 6)] public List<string> Keywords { get; set; } = [];
        [DataMember(Order = 7)] public List<string> Folders { get; set; } = [];

        [JsonIgnore]
        public string Password
        {
            get => Crypto.Unprotect(EncryptedPassword);
            set => EncryptedPassword = Crypto.Protect(value ?? string.Empty);
        }

        public EmailAccount Clone()
            => new()
            {
                Host = Host,
                Port = Port,
                UseSsl = UseSsl,
                Username = Username,
                EncryptedPassword = EncryptedPassword,
                Keywords = [.. Keywords],
                Folders = [.. Folders]
            };
    }
    public sealed class EmailAccountStore
    {
        public static string GetPath(string file = "email_accounts.json")
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Mappy", file);
        }

        public List<EmailAccount> Accounts { get; set; } = [];

        private static JsonSerializerOptions JsonOpts => new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public async Task SaveAsync() => await SaveAsync(GetPath());

        public async Task SaveAsync(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var fs = File.Create(path);
            await JsonSerializer.SerializeAsync(fs, this, JsonOpts);
        }

        public static async Task<EmailAccountStore> LoadAsync() => await LoadAsync(GetPath());

        public static async Task<EmailAccountStore> LoadAsync(string path)
        {
            if (!File.Exists(path)) return new EmailAccountStore();
            await using var fs = File.OpenRead(path);
            var store = await JsonSerializer.DeserializeAsync<EmailAccountStore>(fs, JsonOpts);
            return store ?? new EmailAccountStore();
        }

        public void Upsert(EmailAccount account)
        {
            var idx = Accounts.FindIndex(a => a.Username == account.Username && a.Host == account.Host && a.Port == account.Port);
            if (idx >= 0) Accounts[idx] = account;
            else Accounts.Add(account);
        }

        public void Remove(EmailAccount account)
        {
            Accounts.RemoveAll(a => a.Username == account.Username && a.Host == account.Host && a.Port == account.Port);
        }
    }
}
