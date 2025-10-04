using Mappy.Data;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Mappy
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Guts.Notify += Guts_Notify;
            Closing += MainWindow_Closing;
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            string kw = EmailAccountStore.GetPath("keywords.txt");
            if (File.Exists(kw))
            {
                k.Text = File.ReadAllText(kw);
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            File.WriteAllText(EmailAccountStore.GetPath("keywords.txt"), k.Text);
        }

        private void Guts_Notify(string text, bool line)
        {
            if (line)
            {
                WriteLine(text);
            }
            else
            {
                Write(text);
            }
        }

        private void ShowAccountsDialog(object sender, RoutedEventArgs e)
        {
            var dlg = new EmailAccountsDialog { Owner = this };
            dlg.ShowDialog();
        }

        private void RunSync(object sender, RoutedEventArgs e)
        {
            btnSync.IsEnabled = false;
            int numDays = int.TryParse(txtDays.Text, out var days) && days > 0 ? days : 3;
            string[] globalKeywords = k.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            Mouse.OverrideCursor = Cursors.Wait;

            Task.Run(async () =>
            {
                var store = await EmailAccountStore.LoadAsync();

                var accounts = store.Accounts;

                foreach (var account in accounts)
                {
                    var localKeywords = account.Keywords is { Count: > 0 } ? new List<string>(account.Keywords) : new List<string>();
                    localKeywords.AddRange(globalKeywords);

                    WriteLine("");
                    WriteLine("");
                    WriteLine($"Syncing account: {account.Username} at {account.Host}");
                    await Guts.GetMessages(
                        account.Host, account.Port, account.UseSsl,
                        account.Username, account.Password, numDays,
                        localKeywords.ToArray(), account.Folders?.ToArray() ?? []);
                }

                WriteLine($"\n\nAll operations completed.");

                await Dispatcher.BeginInvoke(DispatcherPriority.Render,
                    new Action(() =>
                    {
                        Mouse.OverrideCursor = null;
                        btnSync.IsEnabled = true;
                    }));
            });
        }

        private void Write(string text)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Render,
                new Action(() =>
                {
                    if (t == null) return;

                    t.AppendText(text);
                    t.ScrollToEnd();
                }));
        }

        private void WriteLine(string text)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Render,
                new Action(() =>
                {
                    if (t == null) return;

                    t.AppendText(text + Environment.NewLine);
                    t.ScrollToEnd();
                }));
        }
    }
}