using HulloVMailWinService;
using HulloVMailWinService.Imap;
using HulloVMailWinService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HulloVMailWinService
{
    class Program
    {
        static void Main(string[] args)
        {
            var emailCount = 0;
            using (var imap = new ImapClient("imap.gmail.com", "yosnoor@gmail.com", "plpzcarxfnvsglau", ImapClient.AuthMethods.Login, 993, true))
            {
                imap.SelectMailbox("HulloMail");

                Lazy<MailMessage>[] msgs = null;

                if (args.Count() == 1 && args[0] == "all")
                {
                    // Check all voicemails read or not
                    msgs = imap.SearchMessages(
                        SearchCondition.Subject("voicemail").
                        And(SearchCondition.SentSince(DateTime.Now.AddYears(-1))));
                }
                else
                {
                    // Only check for UNREAD voicemails
                    msgs = imap.SearchMessages(
                                SearchCondition.Subject("voicemail").
                                And(SearchCondition.Unseen()).
                                And(SearchCondition.SentSince(DateTime.Now.AddYears(-1))));
                }

                emailCount = msgs.Length;

                if (msgs.Length > 0)
                {
                    // There are new voicemails
                    // Save voicemails to storage

                    // Loop through each email and check for audio attachments
                    var voicemails = new List<Voicemail>();
                    foreach (var msg in msgs)
                    {
                        var audio = (from attachment in msg.Value.Attachments
                                     where attachment.ContentType.Contains("mp3")
                                     select attachment);

                        if (audio.Count<Attachment>() > 0)
                        {
                            var voicemail = new Voicemail();
                            voicemail.From = (msg.Value.ReplyTo.Count) == 0 ? msg.Value.From.Address : msg.Value.ReplyTo.FirstOrDefault().Address;
                            voicemail.RecordedDate = msg.Value.Date;
                            voicemail.IsNew = !msg.Value.Flags.HasFlag(Flags.Seen);
                            voicemail.FromDisplay = (msg.Value.From.DisplayName.ToLower() == "hullomail") ? "Unknown" : msg.Value.From.DisplayName;

                            voicemail.Message = audio.First<Attachment>().GetData();
                            voicemails.Add(voicemail);
                        }
                    }

                    var voicemailDB = new VoicemailDBContext();
                    voicemails.ForEach(v => voicemailDB.Voicemails.Add(v));
                    try
                    {
                        voicemailDB.SaveChanges();
                    }
                    catch (Exception)
                    {
                        var temp = voicemailDB.GetValidationErrors();
                        foreach (var message in temp)
                        {
                            var m = message.ToString();
                        }
                        throw;
                    }
                }
            }
        }
    }
}
