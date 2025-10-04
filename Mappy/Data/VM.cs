using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

// EmailAccountViewModels.cs

namespace Mappy.Data
{
    public sealed class EmailAccountVM(EmailAccount model) : INotifyPropertyChanged
    {
        public EmailAccount Model => model;

        public string Host
        { get => model.Host; set { model.Host = value; OnPropertyChanged(); } }

        public int Port
        { get => model.Port; set { model.Port = value; OnPropertyChanged(); } }

        public bool UseSsl
        { get => model.UseSsl; set { model.UseSsl = value; OnPropertyChanged(); } }

        public string Username
        { get => model.Username; set { model.Username = value; OnPropertyChanged(); } }

        // Plain password for editing; writes-through to encrypted storage.
        private string _passwordPlain;

        public string PasswordPlain
        {
            get => _passwordPlain ??= model.Password;
            set { _passwordPlain = value; model.Password = value; OnPropertyChanged(); }
        }

        public string KeywordsCsv
        {
            get => string.Join(", ", model.Keywords);
            set { model.Keywords = (value ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList(); OnPropertyChanged(); }
        }

        public string FoldersCsv
        {
            get => string.Join(", ", model.Folders);
            set { model.Folders = (value ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList(); OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class EmailAccountsDialogVM : INotifyPropertyChanged
    {
        public ObservableCollection<EmailAccountVM> Accounts { get; } = [];
        private EmailAccountVM _selected;

        public EmailAccountVM Selected
        { get => _selected; set { _selected = value; OnPropertyChanged(); } }

        public void LoadFromStore(EmailAccountStore store)
        {
            Accounts.Clear();
            foreach (var a in store.Accounts) Accounts.Add(new EmailAccountVM(a));
            Selected = Accounts.FirstOrDefault();
        }

        public EmailAccountStore ToStore()
        {
            var store = new EmailAccountStore();
            store.Accounts.AddRange(Accounts.Select(a => a.Model.Clone()));
            return store;
        }

        public void AddNew()
        {
            var vm = new EmailAccountVM(new EmailAccount { UseSsl = true, Port = 993 });
            Accounts.Add(vm);
            Selected = vm;
        }

        public void DeleteSelected()
        {
            if (Selected == null) return;
            var idx = Accounts.IndexOf(Selected);
            Accounts.Remove(Selected);
            Selected = Accounts.Count == 0 ? null : Accounts[Math.Min(idx, Accounts.Count - 1)];
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}