using System;
using System.Collections.Generic;
using System.Text;

namespace TrueSign.Shared
{
    public class ApiToken
    {
        public string Token { get; set; }
        public DateTime Expires_UTC { get; set; }
    }

    public class Envelope_User
    {
        public string Email { get; set; }
        public string Name { get; set; }
    }
}
