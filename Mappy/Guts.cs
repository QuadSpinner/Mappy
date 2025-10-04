using System.IO;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;

namespace Mappy
{
    internal static class Guts
    {
        internal static string baseFolder = @"SavedPDFs"; // Base folder for saving PDFs.

        private static SearchQuery BuildKeywordQuery(string[] keywords)
        {
            if (keywords == null || keywords.Length == 0)
                return SearchQuery.All;

            SearchQuery query = SearchQuery.SubjectContains(keywords[0]);
            for (int i = 1; i < keywords.Length; i++)
                query = query.Or(SearchQuery.SubjectContains(keywords[i]));

            return query;
        }

        internal static async Task GetMessages(string host, int port, bool useSsl, string username, string password, int days = 3, string[] keywords = null, params string[] folders)
        {
            bool ensureKeywords = keywords != null;
            DateTime period = DateTime.Now.AddDays(-days);
            RaiseNotify($"Connecting...{username}", true);

            using var client = new ImapClient();
            await client.ConnectAsync(host, port, useSsl);
            await client.AuthenticateAsync(username, password);

            List<IMailFolder> mailFolders = [client.Inbox];

            if (folders != null)
            {
                foreach (string t in folders)
                {
                    IMailFolder f = await client.GetFolderAsync(t);
                    if (f != null)
                    {
                        RaiseNotify($"Added {f.Name}", true);
                        mailFolders.Add(f);
                    }
                }
            }

            foreach (IMailFolder folder in mailFolders)
            {
                await folder.OpenAsync(FolderAccess.ReadOnly);

                IList<UniqueId> uids = await folder.SearchAsync(SearchQuery.DeliveredAfter(period).And(BuildKeywordQuery(keywords)));

                //IList<UniqueId> uids = await folder.SearchAsync(SearchQuery.DeliveredAfter(period)
                //    .And(SearchQuery.SubjectContains("invoice")
                //        .Or(SearchQuery.SubjectContains("hdfc"))
                //        .Or(SearchQuery.SubjectContains("adobe"))
                //        .Or(SearchQuery.SubjectContains("receipt"))
                //        .Or(SearchQuery.SubjectContains("remittance"))
                //        .Or(SearchQuery.SubjectContains("billing statement"))
                //        .Or(SearchQuery.SubjectContains("order"))
                //    ));

                // Retrieve emails delivered after one month ago.

                RaiseNotify($"{uids.Count} total for folder {folder.Name}.", true);

                foreach (var uid in uids)
                {
                    var message = await folder.GetMessageAsync(uid);

                    if (string.IsNullOrEmpty(message.Subject) || !message.Attachments.Any())
                        continue;

                    DateTime emailDate = message.Date.LocalDateTime;
                    // Determine sender folder.
                    string senderFolder = "Unknown";
                    if (message.From.Mailboxes.Any())
                    {
                        senderFolder = message.From.Mailboxes.First().Address.Split("@").Last();

                        if (message.ReplyTo.Any())
                        {
                            senderFolder = message.ReplyTo.Mailboxes.First().Address.Split("@").Last();
                        }
                    }

                    string monthFolder = emailDate.ToString("yyyy-MM");
                    string fullFolderPath = Path.Combine(baseFolder, monthFolder);

                    // Process PDF attachments.
                    await ParseAttachments(ensureKeywords, keywords, message, emailDate, senderFolder, fullFolderPath);
                }
            }

            await client.DisconnectAsync(true);
        }

        private static async Task ParseAttachments(bool ensureKeywords, string[] keywords, MimeMessage message, DateTime emailDate, string senderFolder, string fullFolderPath)
        {
            foreach (var attachment in message.Attachments)
            {
                if (attachment is not MimePart part)
                    continue;

                if (part.FileName.ToLower().Contains(".pdf")
                    || (part.ContentType.MediaType.Equals("application", StringComparison.OrdinalIgnoreCase)
                        && part.ContentType.MediaSubtype.Equals("pdf", StringComparison.OrdinalIgnoreCase)))
                {
                    string originalFileName = string.IsNullOrEmpty(part.FileName)
                        ? $"{emailDate:yyyy-MM-dd}_attachment{(ensureKeywords ? "_d" : "")}.pdf"
                        : part.FileName;

                    originalFileName = Sanitize(originalFileName);

                    string fileName = Sanitize($"{emailDate:yyyy-MM-dd}_{senderFolder}_{originalFileName}");

                    if (ensureKeywords)
                    {
                        try
                        {
                            string temp = Path.GetTempFileName();
                            await using var stream = File.Create(temp);
                            await part.Content.DecodeToAsync(stream);
                            stream.Close();

                            if (!ContainsKeywords(temp, keywords))
                            {
                                try
                                {
                                    File.Delete(temp);
                                }
                                catch (Exception)
                                {
                                }

                                continue;
                            }

                            try
                            {
                                File.Delete(temp);
                            }
                            catch (Exception)
                            {
                            }
                        }
                        catch (Exception ex)
                        {
                            RaiseNotify("!!! ERROR: " + ex.Message, true);
                            RaiseNotify($"!!! File was: {fileName}", true);
                            continue;
                        }
                    }

                    //RaiseNotify($"{message.Date:MM-dd}: {message.Subject} -- ");
                    string filePath = Path.Combine(fullFolderPath, fileName);
                    RaiseNotify($"{filePath}...");
                    if (!File.Exists(filePath))
                    {
                        Directory.CreateDirectory(fullFolderPath);
                        await using var stream = File.Create(filePath);
                        await part.Content.DecodeToAsync(stream);
                        RaiseNotify("done!", true);
                    }
                    else
                    {
                        RaiseNotify("exists!", true);
                    }
                }
            }
        }

        private static string Sanitize(string s)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Concat(s.Select(ch => invalid.Contains(ch) ? '_' : ch));
        }

        private static bool ContainsKeywords(string pdfPath, string[] keywords)
        {
            using PdfReader reader = new(pdfPath);
            using PdfDocument pdfDoc = new(reader);
            for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
            {
                string text = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i));
                if (keywords.Any(k => text.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                    return true;
            }

            return false;
        }

        public delegate void NotifyHandler(string text, bool line);

        public static event NotifyHandler Notify;

        private static void RaiseNotify(string text, bool line = false) => Notify?.Invoke(text, line);
    }
}