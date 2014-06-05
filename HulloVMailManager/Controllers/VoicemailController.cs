﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using HulloVMailManager.Imap;
using HulloVMailManager.Models;

namespace HulloVMailManager.Controllers
{
    public class VoicemailController : Controller
    {
        private readonly VoicemailDBContext _voicemailDB = new VoicemailDBContext();

        public ActionResult Index(string loadNew)
        {
            if (loadNew == "new" || loadNew == "all")
            {
            }

            var vmi = new List<Voicemail>();
            if (loadNew == "new")
            {
                vmi = (from v in _voicemailDB.Voicemails
                       where v.IsNew == true
                       orderby v.RecordedDate descending
                       select v).ToList();
            }
            else
            {
                vmi = (from v in _voicemailDB.Voicemails
                       orderby v.RecordedDate descending
                       select v).ToList();
            }

            return View(vmi);
        }

        private void GetVoicemailsFromEmail(bool getAll)
        {
            if (getAll)
            {
                // Clear the database
                foreach (var voicemail in _voicemailDB.Voicemails)
                {
                    _voicemailDB.Voicemails.Attach(voicemail);
                    _voicemailDB.Voicemails.Remove(voicemail);
                }
                _voicemailDB.SaveChanges();
            }

            using (var imap = new ImapClient("imap.gmail.com", "hullovmail@gmail.com", "strumpet",
                                                ImapClient.AuthMethods.Login, 993, true))
            {
                imap.SelectMailbox("HulloMail");
                Lazy<MailMessage>[] msgs = null;

                if (getAll)
                {
                    msgs = imap.SearchMessages(SearchCondition.Subject("voicemail").And(SearchCondition.SentSince(DateTime.Now.AddYears(-1))));
                }
                else
                {
                    // TODO: need to check if unseen works
                    msgs = imap.SearchMessages(SearchCondition.Subject("voicemail").And(SearchCondition.Unseen()));
                }

                var voicemails = new List<Voicemail>();
                for (int i = 0; i < msgs.Length; i++)
                {
                    // Check if message has any audio attachments
                    var audio = (from attachment in msgs[i].Value.Attachments
                                    //                                     where attachment.Filename.EndsWith(".mp3")
                                    where attachment.ContentType.Contains("mp3")
                                    select attachment);

                    if (audio.Count<Attachment>() > 0)
                    {
                        var voicemail = new Voicemail();
                        voicemail.From = (msgs[i].Value.ReplyTo.Count) == 0 ? msgs[i].Value.From.Address : msgs[i].Value.ReplyTo.FirstOrDefault().Address;
                        voicemail.RecordedDate = msgs[i].Value.Date;
                        voicemail.IsNew = !msgs[i].Value.Flags.HasFlag(Flags.Seen);
                        voicemail.FromDisplay = (msgs[i].Value.From.DisplayName.ToLower() == "hullomail") ? "Unknown" : msgs[i].Value.From.DisplayName;

                        voicemail.Message = audio.First<Attachment>().GetData();
                        voicemails.Add(voicemail);
                    }
                }

                voicemails.ForEach(v => _voicemailDB.Voicemails.Add(v));
                try
                {
                    _voicemailDB.SaveChanges();
                }
                catch (Exception)
                {
                    var temp = _voicemailDB.GetValidationErrors();
                    foreach (var message in temp)
                    {
                        var m = message.ToString();
                    }
                    throw;
                }
            }
        }

        public ActionResult Details(int id)
        {
            var vm = _voicemailDB.Voicemails.Find(id);
            if (vm == null)
            {
                return HttpNotFound();
            }
            return View(vm);
        }

        public ActionResult VoicemailDelete(int id)
        {
            var username = Request.QueryString["u"];
            var password = Request.QueryString["p"];

            if (username == "yos" && password == "strumpet")
            {
                var voicemail = _voicemailDB.Voicemails.Find(id);

                using (
                    var imap = new ImapClient("imap.gmail.com", "yosnoor@gmail.com", "5trump3t",
                                              ImapClient.AuthMethods.Login, 993, true))
                {
                    imap.SelectMailbox("HulloMail");
                    var message = imap.GetMessage(voicemail.EmailID);
                    imap.AddFlags(Flags.Deleted, new[] {message});
                }

                _voicemailDB.Voicemails.Remove(voicemail);
                _voicemailDB.SaveChanges();
            }

            return RedirectToAction("Index");
        }

        public ActionResult MarkRead(int id)
        {
            using (var imap = new ImapClient("imap.gmail.com", "yosnoor@gmail.com", "5trump3t",
                                          ImapClient.AuthMethods.Login, 993, true))
            {
                var voicemail = _voicemailDB.Voicemails.Find(id);

                imap.SelectMailbox("HulloMail");
                var message = imap.GetMessage(voicemail.EmailID);
                imap.AddFlags(Flags.Seen, new[]{message});

                voicemail.IsNew = false;
                _voicemailDB.SaveChanges();

            }

            return RedirectToAction("Index");
        }

        public FileContentResult DownloadMessage(int id)
        {
            var vm = _voicemailDB.Voicemails.Find(id);
            if (vm == null)
            {
                return null;
            }

            return File(vm.Message.ToArray(), "audio/mpeg3", string.Format("{0}.mp3", vm.ID.ToString()));
        }

        public ActionResult NotifyNewVoicemail(string sender)
        {
            var emailCount = 0;
            using (var imap = new ImapClient("imap.gmail.com", "yosnoor@gmail.com", "5trump3t", ImapClient.AuthMethods.Login, 993, true))
            {
                imap.SelectMailbox("HulloMail");

                var msgs =
                    imap.SearchMessages(
                        SearchCondition.Subject("voicemail").And(SearchCondition.Unseen()).And(
                            SearchCondition.SentSince(DateTime.Now.AddYears(-1))));
                emailCount = msgs.Length;
            }

            if (emailCount > 0)
            {
                // Send notification
            }

            return RedirectToAction("Index", "Home");
        }
    }
}