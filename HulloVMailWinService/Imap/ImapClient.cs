using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Text;

namespace HulloVMailWinService.Imap {

  public class ImapClient : TextClient, IMailClient {
    private string _selectedMailbox;
    private int _tag = 0;
    private string[] _capability;

    private bool _idling;
    private Thread _idleEvents;
    const string IdleThreadAbort = "!AbortThread";

    public ImapClient(string host, string username, string password, AuthMethods method = AuthMethods.Login, int port = 143, bool secure = false) {
      Connect(host, port, secure);
      AuthMethod = method;
      Login(username, password);
    }

    public enum AuthMethods {
      Login, Crammd5
    }

    public AuthMethods AuthMethod { get; set; }

    private string GetTag() {
      _tag++;
      return string.Format("xm{0:000} ", _tag);
    }

    public bool Supports(string command) {
      return (_capability ?? Capability()).Contains(command, StringComparer.OrdinalIgnoreCase);
    }

    private EventHandler<MessageEventArgs> _newMessage;
    public event EventHandler<MessageEventArgs> NewMessage {
      add {
        _newMessage += value;
        IdleStart();
      }
      remove {
        _newMessage -= value;
        if (!HasEvents) IdleStop();
      }
    }

    private EventHandler<MessageEventArgs> _messageDeleted;
    public event EventHandler<MessageEventArgs> MessageDeleted {
      add {
        _messageDeleted += value;
        IdleStart();
      }
      remove {
        _messageDeleted -= value;
        if (!HasEvents) IdleStop();
      }
    }

    private void IdleStart() {
      if (string.IsNullOrEmpty(_selectedMailbox)) {
        SelectMailbox("Inbox");
      }
      _idling = true;
      if (!Supports("IDLE")) {
        throw new InvalidOperationException("This IMAP server does not support the IDLE command");
      }
      CheckMailboxSelected();
      IdleResume();
    }

    private void IdlePause() {
      CheckConnectionStatus();
      if (_idleEvents == null || !_idling) return;
      StopIdleEventsThread();

      _idleEvents = null;
      SendCommandGetResponse("DONE");
    }

    private void IdleResume() {
      if (_idleEvents != null || !_idling) return;

      IdleResumeCommand();

      if (_idleEvents == null) {
        _idleEvents = new Thread(WatchIdleQueue);
        _idleEvents.Name = "_IdleEvents";
        _idleEvents.Start();
      }
    }

    private void IdleResumeCommand() {
      var response = SendCommandGetResponse(GetTag() + "IDLE");
      //response = response.Substring(response.IndexOf(" ")).Trim();
      //if (!response.TrimStart().StartsWith("idling", StringComparison.OrdinalIgnoreCase))
      //    throw new Exception(response);
    }

    private bool HasEvents {
      get {
        return _messageDeleted != null || _newMessage != null;
      }
    }

    private void IdleStop() {
      _idling = false;
      IdlePause();
      if (_idleEvents != null) {
        StopIdleEventsThread();
        _idleEvents = null;
      }
    }

    private void StopIdleEventsThread() {
      _Responses.Add(IdleThreadAbort);  //this will abort the thread
      if (!_idleEvents.Join(2000))
        _idleEvents.Abort();
    }

    private void WatchIdleQueue() {
      try {
        string last = null;

        while (true) {
          string resp;
          if (!TryGetResponse(out resp, (int)TimeSpan.FromMinutes(20).TotalMilliseconds)) {   //send NOOP every 20 minutes
            Noop(false);        //call noop without aborting this Idle thread
            continue;
          }
          if (resp == IdleThreadAbort)       //string that tells us to close the thread
            return;

          var data = resp.Split(' ');
          if (data[0] == "*" && data.Length >= 3) {
            var e = new MessageEventArgs { Client = this, MessageCount = int.Parse(data[1]) };
            if (data[2].Is("EXISTS") && !last.Is("EXPUNGE") && e.MessageCount > 0) {
              ThreadPool.QueueUserWorkItem(callback => _newMessage.Fire(this, e));    //Fire the event on a separate thread
            } else if (data[2].Is("EXPUNGE")) {
              _messageDeleted.Fire(this, e);
            }
            last = data[2];
          }
        }
      } catch (ThreadAbortException) {
        Console.WriteLine("IdleEvent thread aborted");
      } catch (Exception ex) {
        Console.WriteLine(ex.Message);
      }
    }

    protected override void OnDispose() {
      base.OnDispose();
      if (_idleEvents != null) {
        _idleEvents.Abort();
        _idleEvents = null;
      }
    }

    public void AppendMail(string mailbox, MailMessage email) {
      IdlePause();

      string flags = String.Empty;
      string size = (email.Body.Length - 1).ToString();
      if (email.RawFlags.Length > 0) {
        flags = string.Concat("(", string.Join(" ", email.Flags), ")");
      }
      string command = GetTag() + "APPEND " + mailbox.QuoteString() + " " + flags + " {" + size + "}";
      string response = SendCommandGetResponse(command);
      if (response.StartsWith("+")) {
        response = SendCommandGetResponse(email.Body);
      }
      IdleResume();
    }

    public void Noop() {
      Noop(true);
    }
    private void Noop(bool pauseIdle) {
      if (pauseIdle)
        IdlePause();
      else
        SendCommandGetResponse("DONE");

      var tag = GetTag();
      var response = SendCommandGetResponse(tag + "NOOP");
      while (!response.StartsWith(tag)) {
        //if (_IdleEvents != null && _IdleQueue != null)    //NK: I'm not sure how to deal with this... Add to the _Responses queue?
        //    _IdleQueue.Enqueue(response);
        response = GetResponse();
      }

      if (pauseIdle)
        IdleResume();
      else
        IdleResumeCommand();
    }

    public string[] Capability() {
      IdlePause();
      string command = GetTag() + "CAPABILITY";
      string response = SendCommandGetResponse(command);
      if (response.StartsWith("* CAPABILITY ")) response = response.Substring(13);
      _capability = response.Trim().Split(' ');
      GetResponse();
      IdleResume();
      return _capability;
    }

    public void Copy(string messageset, string destination) {
      CheckMailboxSelected();
      IdlePause();
      string prefix = null;
      if (messageset.StartsWith("UID ", StringComparison.OrdinalIgnoreCase)) {
        messageset = messageset.Substring(4);
        prefix = "UID ";
      }
      string command = string.Concat(GetTag(), prefix, "COPY ", messageset, " " + destination.QuoteString());
      SendCommandCheckOK(command);
      IdleResume();
    }

    public void CreateMailbox(string mailbox) {
      IdlePause();
      string command = GetTag() + "CREATE " + mailbox.QuoteString();
      SendCommandCheckOK(command);
      IdleResume();
    }

    public void DeleteMailbox(string mailbox) {
      IdlePause();
      string command = GetTag() + "DELETE " + mailbox.QuoteString();
      SendCommandCheckOK(command);
      IdleResume();
    }

    public Mailbox Examine(string mailbox) {
      IdlePause();

      Mailbox x = null;
      string tag = GetTag();
      string command = tag + "EXAMINE " + mailbox.QuoteString();
      string response = SendCommandGetResponse(command);
      if (response.StartsWith("*")) {
        x = new Mailbox(mailbox);
        while (response.StartsWith("*")) {
          Match m;
          m = Regex.Match(response, @"(\d+) EXISTS");
          if (m.Groups.Count > 1) { x.NumMsg = Convert.ToInt32(m.Groups[1].ToString()); }
          m = Regex.Match(response, @"(\d+) RECENT");
          if (m.Groups.Count > 1) x.NumNewMsg = Convert.ToInt32(m.Groups[1].ToString());
          m = Regex.Match(response, @"UNSEEN (\d+)");
          if (m.Groups.Count > 1) x.NumUnSeen = Convert.ToInt32(m.Groups[1].ToString());
          m = Regex.Match(response, @" FLAGS \((.*?)\)");
          if (m.Groups.Count > 1) x.SetFlags(m.Groups[1].ToString());
          response = GetResponse();
        }
        _selectedMailbox = mailbox;
      }
      IdleResume();
      return x;
    }

    public void Expunge() {
      CheckMailboxSelected();
      IdlePause();

      string tag = GetTag();
      string command = tag + "EXPUNGE";
      string response = SendCommandGetResponse(command);
      while (response.StartsWith("*")) {
        response = GetResponse();
      }
      IdleResume();
    }

    public void DeleteMessage(MailMessage msg) {
      DeleteMessage(msg.Uid);
    }

    public void DeleteMessage(string uid) {
      CheckMailboxSelected();
      Store("UID " + uid, true, "\\Seen \\Deleted");
    }

    public void MoveMessage(string uid, string folderName) {
      CheckMailboxSelected();
      Copy("UID " + uid, folderName);
      DeleteMessage(uid);
    }

    private void CheckMailboxSelected() {
      if (string.IsNullOrEmpty(_selectedMailbox))
        SelectMailbox("INBOX");
    }

    public MailMessage GetMessage(string uid, bool headersonly = false) {
      return GetMessage(uid, headersonly, true);
    }

    public MailMessage GetMessage(int index, bool headersonly = false) {
      return GetMessage(index, headersonly, true);
    }

    public MailMessage GetMessage(int index, bool headersonly, bool setseen) {
      return GetMessages(index, index, headersonly, setseen).FirstOrDefault();
    }

    public MailMessage GetMessage(string uid, bool headersonly, bool setseen) {
      return GetMessages(uid, uid, headersonly, setseen).FirstOrDefault();
    }

    public MailMessage[] GetMessages(string startUid, string endUid, bool headersonly = true, bool setseen = false) {
      return GetMessages(startUid, endUid, true, headersonly, setseen);
    }

    public MailMessage[] GetMessages(int startIndex, int endIndex, bool headersonly = true, bool setseen = false) {
      return GetMessages((startIndex + 1).ToString(), (endIndex + 1).ToString(), false, headersonly, setseen);
    }

    public MailMessage[] GetMessages(string start, string end, bool uid, bool headersonly, bool setseen) {
      CheckMailboxSelected();
      IdlePause();

      string headers;
      string setSeen;
      var UID = headers = setSeen = String.Empty;
      if (uid) UID = "UID ";
      if (headersonly) headers = "HEADER";
      if (setseen) setSeen = ".PEEK";
      string tag = GetTag();
      string command = tag + UID + "FETCH " + start + ":" + end + " (UID RFC822.SIZE FLAGS BODY" + setSeen + "[" + headers + "])";
      string response = SendCommandGetResponse(command);
      var x = new List<MailMessage>();
      string reg = @"\* \d+ FETCH.*?BODY.*?\{(\d+)\}";
      var m = Regex.Match(response, reg);

      while (m.Groups.Count > 1) {
        int bodyremaininglen = Convert.ToInt32(m.Groups[1].ToString());
        var mail = new MailMessage();

        var body = new StringBuilder();
        while (bodyremaininglen > 0) {
          var line = GetResponse();

          if (bodyremaininglen < line.Length) {
            body.Append(line, 0, bodyremaininglen);
            bodyremaininglen = 0;
          } else {
            body.Append(line).Append(Environment.NewLine);
            bodyremaininglen -= line.Length + 2;  //extra 2 for CRLF
          }
        }

        Match m2 = Regex.Match(response, @"UID (\d+)");
        mail.Uid = m2.Groups[1].ToString();
        m2 = Regex.Match(response, @"FLAGS \((.*?)\)");
        mail.SetFlags(m2.Groups[1].ToString());
        m2 = Regex.Match(response, @"RFC822\.SIZE (\d+)");
        mail.Size = Convert.ToInt32(m2.Groups[1].ToString());
        mail.Load(body.ToString(), headersonly);
        x.Add(mail);
        response = GetResponse(); // read last line terminated by )
        response = GetResponse();
        m = Regex.Match(response, reg);
      }

      IdleResume();
      return x.ToArray();
    }

    public Quota GetQuota(string mailbox) {
      if (!Supports("NAMESPACE"))
        new Exception("This command is not supported by the server!");
      IdlePause();

      Quota quota = null;
      string command = GetTag() + "GETQUOTAROOT " + mailbox.QuoteString();
      string response = SendCommandGetResponse(command);
      string reg = "\\* QUOTA (.*?) \\((.*?) (.*?) (.*?)\\)";
      while (response.StartsWith("*")) {
        Match m = Regex.Match(response, reg);
        if (m.Groups.Count > 1) {
          quota = new Quota(m.Groups[1].ToString(),
                              m.Groups[2].ToString(),
                              Int32.Parse(m.Groups[3].ToString()),
                              Int32.Parse(m.Groups[4].ToString())
                          );
          break;
        }
        response = GetResponse();
      }

      IdleResume();
      return quota;
    }

    public Mailbox[] ListMailboxes(string reference, string pattern) {
      IdlePause();

      var x = new List<Mailbox>();
      string command = GetTag() + "LIST " + reference.QuoteString() + " " + pattern.QuoteString();
      string reg = "\\* LIST \\(([^\\)]*)\\) \\\"([^\\\"]+)\\\" \\\"?([^\\\"]+)\\\"?";
      string response = SendCommandGetResponse(command);
      Match m = Regex.Match(response, reg);
      while (m.Groups.Count > 1) {
        Mailbox mailbox = new Mailbox(m.Groups[3].ToString());
        x.Add(mailbox);
        response = GetResponse();
        m = Regex.Match(response, reg);
      }
      IdleResume();
      return x.ToArray();
    }

    public Mailbox[] ListSuscribesMailboxes(string reference, string pattern) {
      IdlePause();

      var x = new List<Mailbox>();
      string command = GetTag() + "LSUB " + reference.QuoteString() + " " + pattern.QuoteString();
      string reg = "\\* LSUB \\(([^\\)]*)\\) \\\"([^\\\"]+)\\\" \\\"([^\\\"]+)\\\"";
      string response = SendCommandGetResponse(command);
      Match m = Regex.Match(response, reg);
      while (m.Groups.Count > 1) {
        Mailbox mailbox = new Mailbox(m.Groups[3].ToString());
        x.Add(mailbox);
        response = GetResponse();
        m = Regex.Match(response, reg);
      }
      IdleResume();
      return x.ToArray();
    }

    internal override void OnLogin(string login, string password) {
      string command;
      string result;
      string tag = GetTag();

        switch (AuthMethod) {
        case AuthMethods.Crammd5:
          command = tag + "AUTHENTICATE CRAM-MD5";
          result = SendCommandGetResponse(command);
          // retrieve server key
          var key = result.Replace("+ ", "");
          key = System.Text.Encoding.Default.GetString(Convert.FromBase64String(key));
          // calcul hash
          using (HMACMD5 kMd5 = new HMACMD5(System.Text.Encoding.ASCII.GetBytes(password))) {
            byte[] hash1 = kMd5.ComputeHash(System.Text.Encoding.ASCII.GetBytes(key));
            key = BitConverter.ToString(hash1).ToLower().Replace("-", "");
            result = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(login + " " + key));
            result = SendCommandGetResponse(result);
          }
          break;

        case AuthMethods.Login:
          command = tag + "LOGIN " + login + " " + password;
          result = SendCommandGetResponse(command);
          break;

        default:
          throw new NotSupportedException();
      }

      if (result.StartsWith("* CAPABILITY ")) {
        _capability = result.Substring(13).Trim().Split(' ');
        result = GetResponse();
      }

      if (!result.StartsWith(tag + "OK")) {
        throw new Exception(result);
      }
    }

    internal override void OnLogout() {
      SendCommand(GetTag() + "LOGOUT");
    }

    public Namespaces Namespace() {
      if (!Supports("NAMESPACE"))
        throw new NotSupportedException("This command is not supported by the server!");
      IdlePause();

      string command = GetTag() + "NAMESPACE";
      string response = SendCommandGetResponse(command);

      if (!response.StartsWith("* NAMESPACE")) {
        throw new Exception("Unknow server response !");
      }

      response = response.Substring(12);
      var n = new Namespaces();
      //[TODO] be sure to parse correctly namespace when not all namespaces are present. NIL character
      const string reg = @"\((.*?)\) \((.*?)\) \((.*?)\)$";
      var m = Regex.Match(response, reg);
      if (m.Groups.Count != 4) throw new Exception("En error occure, this command is not fully supported !");
      const string reg2 = "\\(\\\"(.*?)\\\" \\\"(.*?)\\\"\\)";
      var m2 = Regex.Match(m.Groups[1].ToString(), reg2);
      while (m2.Groups.Count > 1) {
        n.ServerNamespace.Add(new Namespace(m2.Groups[1].Value, m2.Groups[2].Value));
        m2 = m2.NextMatch();
      }
      m2 = Regex.Match(m.Groups[2].ToString(), reg2);
      while (m2.Groups.Count > 1) {
        n.UserNamespace.Add(new Namespace(m2.Groups[1].Value, m2.Groups[2].Value));
        m2 = m2.NextMatch();
      }
      m2 = Regex.Match(m.Groups[3].ToString(), reg2);
      while (m2.Groups.Count > 1) {
        n.SharedNamespace.Add(new Namespace(m2.Groups[1].Value, m2.Groups[2].Value));
        m2 = m2.NextMatch();
      }
      GetResponse();
      IdleResume();
      return n;
    }

    public int GetMessageCount() {
      CheckMailboxSelected();
      return GetMessageCount(null);
    }
    public int GetMessageCount(string mailbox) {
      IdlePause();

      var command = GetTag() + "STATUS " + Utilities.QuoteString(mailbox ?? _selectedMailbox) + " (MESSAGES)";
      var response = SendCommandGetResponse(command);
      const string reg = @"\* STATUS.*MESSAGES (\d+)";
      var result = 0;
      while (response.StartsWith("*")) {
        var m = Regex.Match(response, reg);
        if (m.Groups.Count > 1) result = Convert.ToInt32(m.Groups[1].ToString());
        response = GetResponse();
        m = Regex.Match(response, reg);
      }
      IdleResume();
      return result;
    }

    public void RenameMailbox(string frommailbox, string tomailbox) {
      IdlePause();

      var command = GetTag() + "RENAME " + frommailbox.QuoteString() + " " + tomailbox.QuoteString();
      SendCommandCheckOK(command);
      IdleResume();
    }

    public string[] Search(SearchCondition criteria, bool uid = true) {
      return Search(criteria.ToString(), uid);
    }

    public string[] Search(string criteria, bool uid = true) {
      CheckMailboxSelected();

      var isuid = uid ? "UID " : "";
      var tag = GetTag();
      var command = tag + isuid + "SEARCH " + criteria;
      var response = SendCommandGetResponse(command);

      if (!response.StartsWith("* SEARCH", StringComparison.InvariantCultureIgnoreCase) && !IsResultOK(response)) {
        throw new Exception(response);
      }

      string temp;
      while (!(temp = GetResponse()).StartsWith(tag)) {
        response += Environment.NewLine + temp;
      }

      var m = Regex.Match(response, @"^\* SEARCH (.*)");
      return m.Groups[1].Value.Trim().Split(' ').Where(x => !string.IsNullOrEmpty(x)).ToArray();
    }

    public Lazy<MailMessage>[] SearchMessages(SearchCondition criteria, bool headersonly = false) {
      return Search(criteria, true)
          .Select(x => new Lazy<MailMessage>(() => GetMessage(x, headersonly)))
          .ToArray();
    }

    public Mailbox SelectMailbox(string mailbox) {
      IdlePause();

      Mailbox x = null;
      var tag = GetTag();
      var command = tag + "SELECT " + mailbox.QuoteString();
      var response = SendCommandGetResponse(command);
      if (response.StartsWith("*")) {
        x = new Mailbox(mailbox);
        while (response.StartsWith("*")) {
          Match m;
          m = Regex.Match(response, @"(\d+) EXISTS");
          if (m.Groups.Count > 1) { x.NumMsg = Convert.ToInt32(m.Groups[1].ToString()); }
          m = Regex.Match(response, @"(\d+) RECENT");
          if (m.Groups.Count > 1) x.NumNewMsg = Convert.ToInt32(m.Groups[1].ToString());
          m = Regex.Match(response, @"UNSEEN (\d+)");
          if (m.Groups.Count > 1) x.NumUnSeen = Convert.ToInt32(m.Groups[1].ToString());
          m = Regex.Match(response, @" FLAGS \((.*?)\)");
          if (m.Groups.Count > 1) x.SetFlags(m.Groups[1].ToString());
          response = GetResponse();
        }
        if (IsResultOK(response)) {
          x.IsWritable = Regex.IsMatch(response, "READ.WRITE", RegexOptions.IgnoreCase);
        }
        _selectedMailbox = mailbox;
      } else {
        throw new Exception(response);
      }
      IdleResume();
      return x;
    }

    public void SetFlags(Flags flags, params MailMessage[] msgs) {
      SetFlags(string.Join(" ", flags.ToString().Split(',').Select(x => "\\" + x.Trim())), msgs);
    }

    public void SetFlags(string flags, params MailMessage[] msgs) {
      Store("UID " + string.Join(" ", msgs.Select(x => x.Uid)), true, flags);
      foreach (var msg in msgs) {
        msg.SetFlags(flags);
      }
    }

    public void AddFlags(Flags flags, params MailMessage[] msgs) {
      AddFlags(string.Join(" ", flags.ToString().Split(',').Select(x => "\\" + x.Trim())), msgs);
    }

    public void AddFlags(string flags, params MailMessage[] msgs) {
      Store("UID " + string.Join(" ", msgs.Select(x => x.Uid)), false, flags);
      foreach (var msg in msgs) {
        msg.SetFlags(string.Join(" ", msg.Flags) + flags);
      }
    }

    public void SetUnread(string uid)
    {
        IdlePause();
        var command = GetTag() + "UID STORE " + uid + @" -FLAGS (\Seen)";
        var response = SendCommandGetResponse(command);
        while (response.StartsWith("*"))
        {
            response = GetResponse();
        }
        IdleResume();
    }

    public void Store(string messageset, bool replace, string flags) {
      CheckMailboxSelected();
      IdlePause();
      string prefix = null;
      if (messageset.StartsWith("UID ", StringComparison.OrdinalIgnoreCase)) {
        messageset = messageset.Substring(4);
        prefix = "UID ";
      }

      var command = string.Concat(GetTag(), prefix, "STORE ", messageset, " ", replace ? "+" : "", "FLAGS.SILENT (" + flags + ")");
      var response = SendCommandGetResponse(command);
      while (response.StartsWith("*")) {
        response = GetResponse();
      }
      CheckResultOK(response);
      IdleResume();
    }

    public void SuscribeMailbox(string mailbox) {
      IdlePause();

      var command = GetTag() + "SUBSCRIBE " + mailbox.QuoteString();
      SendCommandCheckOK(command);
      IdleResume();
    }

    public void UnSuscribeMailbox(string mailbox) {
      IdlePause();

      var command = GetTag() + "UNSUBSCRIBE " + mailbox.QuoteString();
      SendCommandCheckOK(command);
      IdleResume();
    }

    internal override void CheckResultOK(string response) {
      if (!IsResultOK(response)) {
        response = response.Substring(response.IndexOf(" ")).Trim();
        throw new Exception(response);
      }
    }

    internal bool IsResultOK(string response) {
      response = response.Substring(response.IndexOf(" ")).Trim();
      return response.ToUpper().StartsWith("OK");
    }
  }
}