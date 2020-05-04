using System;
using System.Collections.Generic;
using System.Text;

namespace TrueSign.Library
{
    public class Envelope
    {
        public Guid Id { get; set; }
        public Guid Type_Id { get; set; }
        public Envelope_Content Content { get; set; }
        public Envelope_Status Status { get; set; }
    }

    public class Envelope_Dto
    {
        public string Title { get; set; }
        public Contact Contact { get; set; }
        public string Client_Data { get; set; }
        public List<Document_Dto> Documents { get; set; }
    }

    public class Contact
    {
        public string First_Name { get; set; }
        public string Last_Name { get; set; }
        public string Title { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
    }

    public class Envelope_Content
    {
        public int API_Version { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public List<Signer> Signers { get; set; }
        public Contact Contact { get; set; }
        public DateTime Created_On_UTC { get; set; }
        public string Client_Data { get; set; }
        public List<Document> Documents { get; set; }
        public List<Envelope_History> History { get; set; }
    }

    public enum Envelope_Status
    {
        /// <summary>
        /// Envelope is created and ready to receive files.
        /// </summary>
        Created,

        /// <summary>
        /// Envelope has received all files, has been 
        /// closed and is ready to be signed.
        /// </summary>        
        ReadyToSign,

        /// <summary>
        /// All files have been signed for this envelope.
        /// </summary>
        Signed,

        /// <summary>
        /// If Async Env Type, this is the status to mark the 
        /// envelope to send a message to the service bus.
        /// </summary>
        ReadyToNotify,

        /// <summary>
        /// A message was sent to the service bus (when async), 
        /// or the file was downloaded via the API (when sync).
        /// </summary>
        Completed,

        /// <summary>
        /// The envelope has been deleted.
        /// </summary>
        Deleted,

        /// <summary>
        /// This envelope has been rejected by the signer.
        /// </summary>
        Rejected
    }

    public class Envelope_History
    {
        public DateTime DateTime_UTC { get; set; }
        public Envelope_History_Type History_Type { get; set; }
        public string Message { get; set; }
    }

    public enum Envelope_History_Type
    {
        Created,
        FileAdded,
        FileRemoved,
        Closed,
        MarkDeleted,
        Deleted,
        Viewed,
        Completed,
        ClientNotified,
        Downloaded,
        Other,
        Rejected
    }
}