using Mappy.Data;
using Mappy.Logic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Mappy
{
    public partial class MainWindow
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
                txtKeywords.Text = File.ReadAllText(kw);
            }

            string dw = EmailAccountStore.GetPath("destination.txt");
            txtDestination.Text = File.Exists(dw) ? File.ReadAllText(dw) : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Mappy";

            string tw = EmailAccountStore.GetPath("days.txt");

            txtDays.Text = File.Exists(tw) ? File.ReadAllText(tw) : "30";
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            File.WriteAllText(EmailAccountStore.GetPath("keywords.txt"), txtKeywords.Text);
            File.WriteAllText(EmailAccountStore.GetPath("destination.txt"), txtDestination.Text);
            File.WriteAllText(EmailAccountStore.GetPath($"Log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"), txtConsole.Text);
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
            int numDays = int.TryParse(txtDays.Text, out var days) && days > 0 ? days : 3;
            string[] globalKeywords = txtKeywords.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (Directory.Exists(txtDestination.Text))
            {
                try
                {
                    Directory.CreateDirectory(txtDestination.Text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error creating folder", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            Guts.baseFolder = txtDestination.Text;

            Mouse.OverrideCursor = Cursors.Wait;
            btnSync.IsEnabled = false;
            WriteLine("");
            WriteLine("");
            WriteLine($"New sync started at {DateTime.Now.ToLongTimeString()}");
            Task.Run(async () =>
            {
                SetStatus("Loading accounts...");
                var store = await EmailAccountStore.LoadAsync();

                var accounts = store.Accounts;

                bool error = false;
                WriteLine($"{accounts.Count} accounts found.");

                foreach (var account in accounts)
                {
                    try
                    {
                        SetStatus(account.Username);
                        var localKeywords = account.Keywords is { Count: > 0 } ? new List<string>(account.Keywords) : new List<string>();
                        localKeywords.AddRange(globalKeywords);

                        WriteLine("");
                        WriteLine("");
                        WriteLine($"Syncing account: {account.Username}");
                        await Guts.GetMessages(
                            account.Host, account.Port, account.UseSsl,
                            account.Username, account.Password, numDays,
                            localKeywords.ToArray(), account.Folders?.ToArray() ?? []);
                    }
                    catch (Exception ex)
                    {
                        WriteLine($"❌ Error with account {account.Username} at {account.Host}: {ex.Message}");
                        error = true;
                    }
                }

                WriteLine($"\nSync finished at {DateTime.Now.ToLongTimeString()}");
                WriteLine(error ? "\n\nSome operations may not have completed." : "\n\n✔️ All operations completed.");

                await Dispatcher.BeginInvoke(DispatcherPriority.Render,
                    new Action(() =>
                    {
                        Mouse.OverrideCursor = null;
                        btnSync.IsEnabled = true;
                        SetStatus();

                        if (chkOpenWhenFinished.IsChecked == true)
                        {
                            Process.Start("explorer.exe", txtDestination.Text);
                        }
                    }));
            });
        }

        private void Write(string text)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Render,
                new Action(() =>
                {
                    if (txtConsole == null) return;

                    txtConsole.AppendText(text);
                    txtConsole.ScrollToEnd();
                }));
        }

        private void WriteLine(string text)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Render,
                new Action(() =>
                {
                    if (txtConsole == null) return;

                    txtConsole.AppendText(text + Environment.NewLine);
                    txtConsole.ScrollToEnd();
                }));
        }

        private void SetStatus(string text = "") => Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => Title = string.IsNullOrEmpty(text) ? $"QuadSpinner Mappy" : $"QuadSpinner Mappy - {text}"));

        private void BtnOpenFolder_OnClick(object sender, RoutedEventArgs e) => Process.Start("explorer.exe", txtDestination.Text);
    }
}