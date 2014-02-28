using System.Data.Entity;
using HulloVMailService;
using HulloVMailService.Imap;
using HulloVMailService.Models;
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
//            using (var imap = new ImapClient("imap.gmail.com", "yosnoor@gmail.com", "plpzcarxfnvsglau", ImapClient.AuthMethods.Login, 993, true))
            using (var imap = new ImapClient("imap.gmail.com", "hullovmail@gmail.com", "strumpet", ImapClient.AuthMethods.Login, 993, true))
            {
                imap.SelectMailbox("HulloMail");

                Lazy<MailMessage>[] msgs = null;
                if (args.Length > 0 && args[0] == "1")
                {
                    // Get ALL voicemails
                    msgs =
                        imap.SearchMessages(
                            SearchCondition.Subject("voicemail").And(SearchCondition.SentSince(DateTime.Now.AddYears(-2))));
                }
                else
                {
                    // Get only NEW (UNREAD) voicemails
                    msgs =
                        imap.SearchMessages(
                            SearchCondition.Subject("voicemail").And(SearchCondition.Unseen()).And(
                                SearchCondition.SentSince(DateTime.Now.AddYears(-2))));
                }

                if (msgs.Length > 0)
                {
                    // There are new voicemails
                    // Save voicemails to storage

                    // Loop through each email and check for audio attachments
                    var voicemails = new List<Voicemail>();
                    var voicemailDB = new VoicemailDBContext();
                    foreach (var msg in msgs)
                    {
                        var audio = (from attachment in msg.Value.Attachments
                                     where attachment.ContentType.Contains("mp3")
                                     select attachment);

                        if (audio.Count<Attachment>() > 0)
                        {
                            // Check if it already exists on the DB
                            var From = (msg.Value.ReplyTo.Count) == 0
                                ? msg.Value.From.Address
                                : msg.Value.ReplyTo.FirstOrDefault().Address;
                            if (
                                !voicemailDB.Voicemails.Select(v => v.From == From && v.RecordedDate == msg.Value.Date)
                                    .Any())
                            {
                                var voicemail = new Voicemail();
                                voicemail.From = From;
                                voicemail.RecordedDate = msg.Value.Date;
                                voicemail.IsNew = !msg.Value.Flags.HasFlag(Flags.Seen);
                                voicemail.FromDisplay = (msg.Value.From.DisplayName.ToLower() == "hullomail") ? "Unknown" : msg.Value.From.DisplayName;

                                voicemail.Message = audio.First<Attachment>().GetData();
                                voicemails.Add(voicemail);
                            }
                        }
                    }

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
