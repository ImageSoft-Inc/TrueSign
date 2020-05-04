using System;
using System.Collections.Generic;
using System.Text;

namespace TrueSign.Library
{
    public class Signer
    {
        public string User_Id { get; set; }

        public Signer_Type Type { get; set; }
        public string First_Name { get; set; }
        public string Last_Name { get; set; }
        public string Full_Name { get { return $"{First_Name} {Last_Name}"; } }
        public string Email { get; set; }

        public Access_Code Code { get; set; }

        public bool Completed { get; set; }
        public bool Rejected { get; set; }
        public string Reject_Reason { get; set; }
    }

    public class Signer_Dto
    {
        public string First_Name { get; set; }
        public string Last_Name { get; set; }
        public string Email { get; set; }
    }

    public class Access_Code
    {
        public string Description { get; set; }
        public string Value { get; set; }
    }

    public enum Signer_Type
    {
        Internal,
        External
    }
}
