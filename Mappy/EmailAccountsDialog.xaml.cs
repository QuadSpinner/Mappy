using System.Windows;
using System.Windows.Controls;
using Mappy.Data;

namespace Mappy
{
    public partial class EmailAccountsDialog : Window
    {

        public EmailAccountsDialogVM VM { get; } = new();

        public EmailAccountsDialog()
        {
            InitializeComponent();
            DataContext = VM;


            Loaded += async (_, __) =>
            {
                var store = await EmailAccountStore.LoadAsync();
                VM.LoadFromStore(store);

                // Ensure there is always something to edit without clicking "Add"
                if (VM.Selected == null)
                    VM.AddNew();
            };
        }
        private void AccountsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            pwd.Password = VM.Selected?.PasswordPlain ?? string.Empty;
        }


        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox pb && VM.Selected != null)
                VM.Selected.PasswordPlain = pb.Password;
        }

        private void OnAdd(object sender, RoutedEventArgs e) => VM.AddNew();

        private void OnDelete(object sender, RoutedEventArgs e) => VM.DeleteSelected();

        private async void OnSave(object sender, RoutedEventArgs e)
        {
            // Commit PasswordBox content
            if (VM.Selected != null) VM.Selected.PasswordPlain = pwd.Password;

            // Filter empties
            var store = VM.ToStore();
            store.Accounts = store.Accounts
                .Where(a => !string.IsNullOrWhiteSpace(a.Host)
                            && a.Port is >= 1 and <= 65535
                            && !string.IsNullOrWhiteSpace(a.Username))
                .ToList();

            if (store.Accounts.Count == 0)
            {
                MessageBox.Show("Nothing to save. Add at least one valid account.");
                return;
            }

            await store.SaveAsync();
        }

    }
}
