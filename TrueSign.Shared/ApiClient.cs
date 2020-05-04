using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using TrueSign.Library;

namespace TrueSign.Shared
{
    public class ApiClient : IDisposable
    {
        private bool disposed = false;

        private static string _ApiUrl = "https://api.truesign.com/latest/";

        private string _ClientID { get; set; }
        private string _ClientSecret { get; set; }
        private HttpClient _HttpClient { get; set; }


        public ApiToken _ApiToken { get; set; }
        public bool _Authorized { get; set; }


        public ApiClient()
        {
            _HttpClient = new HttpClient();
            _HttpClient.BaseAddress = new Uri(_ApiUrl);
        }

        public ApiClient(string clientId, string secret)
        {
            if (string.IsNullOrEmpty(clientId))
                throw new Exception("An API client ID must be provided to connect to TrueSign");

            if (string.IsNullOrEmpty(secret))
                throw new Exception("An API client secret must be provided to connect to TrueSign");

            _ClientID = clientId;
            _ClientSecret = secret;

            _HttpClient = new HttpClient();
            _HttpClient.BaseAddress = new Uri(_ApiUrl);

            if(!Authenticate().Result)
                throw new Exception("Unable to authenticate");
        }



        public async Task<bool> Authenticate()
        {
            var json = JsonConvert.SerializeObject(new Dictionary<string, string>() { { "client_id", _ClientID }, { "client_secret", _ClientSecret } });
            var response = await _HttpClient.PostAsync("auth", new StringContent(json, Encoding.UTF8, "application/json"));

            var response_text = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(response_text);

            this._ApiToken = JsonConvert.DeserializeObject<ApiToken>(response_text);
            this._HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _ApiToken.Token);
            this._Authorized = true;

            return true;
        }

        public async Task<Envelope> CreateEnvelope(Envelope_Dto dto)
        {
            if (dto == null)
                throw new Exception("A new envelope object is required");

            if (string.IsNullOrEmpty(dto.Title))
                throw new Exception("A title is required for the new envelope");

            if (!_Authorized)
              await Authenticate();

            var json = JsonConvert.SerializeObject(dto);
            var response = await _HttpClient.PostAsync("envelope", new StringContent(json, Encoding.UTF8, "application/json"));

            var response_text = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(response_text);

            return JsonConvert.DeserializeObject<Envelope>(response_text);
        }

        public async Task<List<Document>> AddFilesToEnvelope(Guid envelope_id, List<Document_Dto> docs)
        {
            if (envelope_id == Guid.Empty)
                throw new Exception("An envelope ID is required to send this request");

            if (docs.Count == 0)
                throw new Exception("A list of documents is required for this request");

            docs.ForEach(a =>
            {
                if (string.IsNullOrEmpty(a.Title))
                    throw new Exception("A document must contain a title");
            });

            if (!_Authorized)
                await Authenticate();

            var json = JsonConvert.SerializeObject(docs);
            var response = await _HttpClient.PostAsync($"envelope/{envelope_id}/Files", new StringContent(json, Encoding.UTF8, "application/json"));

            var response_text = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(response_text);

            return JsonConvert.DeserializeObject<List<Document>>(response_text);
        }

        public async Task<bool> AddExternalSigner(Guid envelope_id, Signer_Dto dto, Access_Code access_code)
        {
            if (envelope_id == Guid.Empty)
                throw new Exception("An envelope ID is required to send this request");

            if (dto == null)
                throw new Exception("A new envelope object is required");

            if (string.IsNullOrEmpty(dto.Email))
                throw new Exception("An email address is required for the external signer");

            if (string.IsNullOrEmpty(dto.First_Name))
                throw new Exception("A first name is required for the external signer");

            if (string.IsNullOrEmpty(dto.Last_Name))
                throw new Exception("A last name is required for the external signer");

            Signer signer = new Signer()
            {
                Email = dto.Email,
                First_Name = dto.First_Name,
                Last_Name = dto.Last_Name,
                Type = Signer_Type.External
            };

            if (access_code != null)
            {
                if (string.IsNullOrEmpty(access_code.Description))
                    throw new Exception("A description is required for the access code");
                if (string.IsNullOrEmpty(access_code.Value))
                    throw new Exception("A value is required for the access code");

                signer.Code = access_code;
            }

            if (!_Authorized)
                await Authenticate();

            var json = JsonConvert.SerializeObject(signer);
            var response = await _HttpClient.PostAsync($"envelope/{envelope_id}/AddExternalSigner", new StringContent(json, Encoding.UTF8, "application/json"));

            var response_text = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(response_text);

            return true;
        }

        public async Task<bool> AddInternalSigner(Guid envelope_id, string email)
        {
            if (envelope_id == Guid.Empty)
                throw new Exception("An envelope ID is required to send this request");

            if (string.IsNullOrEmpty(email))
                throw new Exception("An email address is required for the internal signer");

            Signer signer = new Signer()
            {
                Email = email
            };

            if (!_Authorized)
                await Authenticate();

            var json = JsonConvert.SerializeObject(signer);
            var response = await _HttpClient.PostAsync($"envelope/{envelope_id}/AddInternalSigner", new StringContent(json, Encoding.UTF8, "application/json"));

            var response_text = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(response_text);

            return true;
        }

        public async Task<Envelope> CloseEnvelope(Guid envelope_id)
        {
            if (envelope_id == Guid.Empty)
                throw new Exception("An envelope ID is required to send a close request");

            if (!_Authorized)
                await Authenticate();

            var response = await _HttpClient.GetAsync($"envelope/{envelope_id}/Close");
            var response_text = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(response_text);

            return JsonConvert.DeserializeObject<Envelope>(response_text);
        }

        public async Task<bool> DeleteEnvelope(Guid envelope_id)
        {
            if (envelope_id == Guid.Empty)
                throw new Exception("An envelope ID is required to send a delete request");

            if (!_Authorized)
                await Authenticate();

            var response = await _HttpClient.DeleteAsync($"envelope/{envelope_id}");
            var response_text = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(response_text);

            return true;
        }

        public async Task<List<Envelope_History>> GetEnvelopeHistory(Guid envelope_id)
        {
            if (envelope_id == Guid.Empty)
                throw new Exception("An envelope ID is required to send this request");

            if (!_Authorized)
                await Authenticate();

            var response = await _HttpClient.GetAsync($"envelope/{envelope_id}/History");
            var response_text = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(response_text);

            return JsonConvert.DeserializeObject<List<Envelope_History>>(response_text);
        }

        public async Task<List<Envelope_User>> GetUsers()
        {
            if (!_Authorized)
                await Authenticate();

            var response = await _HttpClient.GetAsync("envelope/Users");
            var response_text = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(response_text);

            return JsonConvert.DeserializeObject<List<Envelope_User>>(response_text);
        }
               


        ~ApiClient()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    _HttpClient.Dispose();

                    this._ApiToken = null;
                }

                this._ClientID = string.Empty;
                this._ClientSecret = string.Empty;
                this._Authorized = false;
            }

            disposed = true;
        }
    }
}
