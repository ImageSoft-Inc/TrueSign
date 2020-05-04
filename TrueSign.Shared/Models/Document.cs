using System;
using System.Collections.Generic;
using System.Text;

namespace TrueSign.Library
{
    public class Document
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Client_Id { get; set; }
        public string Upload_Url { get; set; }
        public string Download_Url { get; set; }
        public bool Signed { get; set; }
        public bool Stamped { get; set; }
        public List<Document_History> History { get; set; }
    }

    public class Document_Dto
    {
        public string Title { get; set; }
        public string Client_Id { get; set; }
    }

    public class Document_History
    {
        public DateTime Date_Time_UTC { get; set; }
        public string Message { get; set; }
        public string Email { get; set; }
    }
}