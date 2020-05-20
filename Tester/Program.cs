using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                //Create a new HttpClient (using System.Net.Http)
                using (var httpClient = new HttpClient())
                {
                    var clientId = "898a2f77-50c0-4f0f-b6fa-9db5a381043c"; //Replace with your own Client ID
                    var clientSecret = "6MMiJNWCpMrRpoZeYqz/Dk1kckuCCDt0XYi4hIOy"; //Replace with your own Client Secret

                    //Set the base address for the client
                    httpClient.BaseAddress = new Uri("https://api.truesign.com/v1/");

                    //Create the body with the client credentials
                    var json = JsonConvert.SerializeObject(new Dictionary<string, string>() { { "client_id", clientId }, { "client_secret", clientSecret } });
                    var body = new StringContent(json, Encoding.UTF8, "application/json");

                    //Make a POST call to the authentication endpoint to receive a JWT
                    var response = httpClient.PostAsync("auth", body).Result;

                    //Ensure the call did not error out. If it did error out, then this will throw an exception.
                    response.EnsureSuccessStatusCode();

                    //Read the JWT from the response.
                    var tokenStr = response.Content.ReadAsStringAsync().Result;
                    dynamic tokenObject = JsonConvert.DeserializeObject(tokenStr);
                    Console.WriteLine("Token: " + tokenObject.Token);
                    Console.WriteLine("Expires: " + tokenObject.Expires_UTC);

                   var envelopes = GetAllEnvelopes(tokenObject.Token.ToString());

                    foreach (var item in envelopes)
                    {
                        Console.WriteLine(item.Id);
                    }
                }
            }
            catch
            {
                throw;
            }

            Console.ReadLine();
        }

        public static JArray GetAllEnvelopes(string token)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    //Set the base address for the client
                    httpClient.BaseAddress = new Uri("https://api.truesign.com/v1/");

                    //add the authorization token to the httpClient's header
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    //Call the envelope endpoint and make sure the response is success.
                    var response = httpClient.GetAsync("envelope").Result;

                    //Ensure the call did not error out. If it did error out, then this will throw an exception.
                    response.EnsureSuccessStatusCode();

                    //Read the successful response and convert it to a list of TS_Envelope
                    var env_resp = response.Content.ReadAsStringAsync().Result;
                    dynamic envelopes = JsonConvert.DeserializeObject(env_resp);

                    return envelopes;
                }
            }
            catch (Exception ex)
            {
                //An error has occurred. Return null.
                //Handle exception
                return null;
            }
        }
    }
}
