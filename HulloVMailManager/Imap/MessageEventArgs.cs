using System;

namespace HulloVMailManager.Imap {
    public class MessageEventArgs : EventArgs {
        public int MessageCount { get; set; }
        internal ImapClient Client { get; set; }
    }
}
