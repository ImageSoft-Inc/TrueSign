namespace TrueSignNextLibrary
{
    using System;
    using System.Text;
    using Hyland.Unity;
    using Hyland.Unity.CodeAnalysis;

    //Additional using statements
    using System.Net.Http;
    using System.Net.Http.Headers;
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// A library to connect to TrueSign Next.
    /// </summary>
    public class TrueSignNext
    {
        /// <summary>
        /// The base URL for the TrueSign API. Contact ImageSoft for the correct one.
        /// </summary>
        public static string _Url = "https://api.truesign.com/v1/";

        /// <summary>
        /// A reusable HttpClient to communicate with the TrueSign API
        /// </summary>
        private HttpClient _Http_Client = new HttpClient();
        private bool _Authenticated { get; set; }
        private string _ClientId { get; set; }
        private string _ClientSecret { get; set; }

        /// <summary>
        /// The private OnBase application object to store, retrieve documents and write to Diagnostics Console.
        /// </summary>
        private Application _App = null;

        /// <summary>
        /// Initiate the TrueSignNext class by passing an OnBase app object.
        /// Use this initializer when you do not need to connect to the TrueSign API.
        /// </summary>
        /// <param name="app">OnBase application object</param>
        public TrueSignNext(Application app)
        {
            _App = app;
        }

        /// <summary>
        /// Initiate the TrueSignNext class by passing an OnBase app object and the Workflow args object.
        /// We will retrieve the Client API credentials from the args.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="args"></param>
        public TrueSignNext(Application app, WorkflowEventArgs args)
        {
            _App = app;

            string clientId = string.Empty;
            string clientSecret = string.Empty;

            if (!args.SessionPropertyBag.TryGetValue("TrueSignClientId", out clientId))
                throw new Exception("No API Client ID (property bag: TrueSignClientId) was present.");
            else
                this._ClientId = clientId;

            if (!args.SessionPropertyBag.TryGetValue("TrueSignClientSecret", out clientSecret))
                throw new Exception("No API Client Secret (property bag: TrueSignClientSecret) was present.");
            else
                this._ClientSecret = clientSecret;
        }

        public TrueSignNext(Application app, string clientId, string clientSecret)
        {
            _App = app;
            _ClientId = clientId;
            _ClientSecret = clientSecret;
        }

        /// <summary>
        /// Call this method to attach a bearer token to the TrueSign object that you just initiated. 
        /// This will set the Http client's base URL and authorization header.
        /// </summary>
        /// <returns>true/false</returns>
        public bool Authenticate()
        {
            try
            {
                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("Calling the TrueSign API to get an authorization token."));

                _Http_Client = new HttpClient();

                //Set the base address for the client
                _Http_Client.BaseAddress = new Uri(_Url);

                if (string.IsNullOrEmpty(_ClientId) || string.IsNullOrEmpty(_ClientSecret))
                    throw new Exception("Client ID or/and Client Secret are missing.");

                //Create the body with the client credentials
                var json = JsonConvert.SerializeObject(new Dictionary<string, string>() { { "client_id", _ClientId }, { "client_secret", _ClientSecret } });
                var body = new StringContent(json, Encoding.UTF8, "application/json");

                //Make a POST call to the authentication endpoint to receive a JWT
                var response = _Http_Client.PostAsync("auth", body).Result;
                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, response.Content.ReadAsStringAsync().Result);

                //Ensure the call did not error out. If it did error out, then this will throw an exception.
                response.EnsureSuccessStatusCode();

                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, "TrueSign API token was successfully received.");

                //Read the JWT from the response and add it to the HttpClient's authentication header.
                var token_txt = response.Content.ReadAsStringAsync().Result;
                ApiToken apiToken = JsonConvert.DeserializeObject<ApiToken>(token_txt);

                _Http_Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken.Token.Replace("\"", ""));

                _Authenticated = true;

                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, "Finished Authenticating with the TrueSign API.");
                //If all lines above suceeded, return true.
                return true;
            }
            catch (Exception ex)
            {
                //An error has occurred. Write the exception to DC and return false.
                _App.Diagnostics.Write(ex);
                return false;
            }
        }

        /// <summary>
        /// Get all users for this envelope type. This should be called if you want to display a list of users to the user creating the envelope via an eform.
        /// Note: the required signer for an envelope must be a TrueSign user with access to that envelope.
        /// </summary>
        /// <returns>A list of TS_User objects</returns>
        public List<Envelope_User> GetUsers()
        {
            try
            {
                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("Getting all users for the authenticated envelope type."));

                //Check if the httpClient is authenticated
                if (!_Authenticated)
                    Authenticate();

                //Call the users endpoint and make sure the response is success.
                var response = _Http_Client.GetAsync("envelope/Users").Result;
                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, response.Content.ReadAsStringAsync().Result);

                //Ensure the call did not error out. If it did error out, then this will throw an exception.
                response.EnsureSuccessStatusCode();

                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("Successfully received all users."));

                //Read the successful response and convert it to a list of TS_Users
                var users_resp = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<List<Envelope_User>>(users_resp);
            }
            catch (Exception ex)
            {
                //An error has occurred. Write the exception to DC and return null.
                _App.Diagnostics.Write(ex);
                return null;
            }
        }

        /// <summary>
        /// Create a new TrueSign envelope. Title and Email are required.
        /// </summary>
        /// <param name="title">The title of the new envelope</param>
        /// <param name="documents">A list of TS_Document objects. Can be null. Use AddToEnvelope() later if null</param>
        /// <param name="clientData">An optional string with data for you to utilize when a signed envelope returns back to your system</param>
        /// <returns></returns>
        public Envelope CreateEnvelope(string title, List<Document_Dto> documents = default(List<Document_Dto>),
            string clientData = default(string), Contact contact = default(Contact))
        {
            try
            {
                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("Creating new envelope with title: {0}.", title));

                //Check if the httpClient is authenticated
                if (!_Authenticated)
                    Authenticate();

                if (string.IsNullOrEmpty(title))
                    throw new Exception("Envelope title cannot be empty.");

                //Create the content object needed to call the create endpoint of the API
                Envelope_Dto envelope = new Envelope_Dto()
                {
                    Title = title,
                    Client_Data = clientData,
                    Documents = documents,
                    Contact = contact
                };

                //JSON encode the content and call the API
                var json = JsonConvert.SerializeObject(envelope);
                var body = new StringContent(json, Encoding.UTF8, "application/json");

                var response = _Http_Client.PostAsync("envelope", body).Result;
                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, response.Content.ReadAsStringAsync().Result);

                //Ensure the call did not error out. If it did error out, then this will throw an exception.
                response.EnsureSuccessStatusCode();

                //Read the API's response into a TS_Envelope object and return it to the caller.
                var created_envelope = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<Envelope>(created_envelope);
            }
            catch (Exception ex)
            {
                //An error has occurred. Write the exception to DC and return null.
                _App.Diagnostics.Write(ex);
                return null;
            }
        }

        /// <summary>
        /// Add a list of OnBase documents to the TrueSign Envelope you created. 
        /// The document bytes will also be uploaded.
        /// </summary>
        /// <param name="id">The Id of the envelope</param>
        /// <param name="documents">A list of OnBase documents</param>
        /// <returns></returns>
        public List<TrueSignNextLibrary.Document> AddToEnvelope(Guid id, List<Hyland.Unity.Document> documents)
        {
            try
            {
                //Check if the httpClient is authenticated
                if (!_Authenticated)
                    Authenticate();

                //Iterate through the OnBase document objects and create TrueSign document objects
                var docs = new List<Document_Dto>();
                foreach (var document in documents)
                {
                    var doc = new Document_Dto()
                    {
                        Title = document.Name,
                        Client_Id = document.ID.ToString()
                    };

                    docs.Add(doc);
                }

                //Convert the TrueSign document list to JSON
                var json = JsonConvert.SerializeObject(docs);
                var body = new StringContent(json, Encoding.UTF8, "application/json");

                //Upload the JSON body the envelope
                var response = _Http_Client.PostAsync(string.Format("envelope/{0}/files", id), body).Result;
                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, response.Content.ReadAsStringAsync().Result);

                //Ensure the call did not error out. If it did error out, then this will throw an exception.
                response.EnsureSuccessStatusCode();

                //REad the response of the API and deserialize it to a list of TrueSign documents
                var docs_resp = response.Content.ReadAsStringAsync().Result;
                var new_docs = JsonConvert.DeserializeObject<List<TrueSignNextLibrary.Document>>(docs_resp);

                //For each document in the API response, upload the OnBase document bytes to the upload url returned by the API. 
                foreach (var item in new_docs)
                {
                    var ob_doc = documents.Find(x => x.ID == Int32.Parse(item.Client_Id));
                    Upload(ob_doc, item.Upload_Url);
                }

                //Return the list of the TS_Document created initially
                return new_docs;
            }
            catch (Exception ex)
            {
                //An error has occurred. Write the exception to DC and return null.
                _App.Diagnostics.Write(ex);
                return null;
            }
        }

        /// <summary>
        /// Add an OnBase document to the TrueSign Envelope you created. 
        /// The document bytes will also be uploaded.
        /// </summary>
        /// <param name="id">The Id of the envelope</param>
        /// <param name="document">An OnBase document object</param>
        /// <returns>A TS_Document object</returns>
        public TrueSignNextLibrary.Document AddToEnvelope(Guid id, Hyland.Unity.Document document)
        {
            try
            {
                //Check if the httpClient is authenticated
                if (!_Authenticated)
                    Authenticate();

                //Create a TS_Document object based on the OnBase document
                var doc = new Document_Dto()
                {
                    Title = document.Name,
                    Client_Id = document.ID.ToString()
                };

                //Create a list of TS_Document object since the API requires a list for this endpoint
                var documents = new List<Document_Dto>() { doc };

                //Convert the list of documents to JSON and call the API
                var json = JsonConvert.SerializeObject(documents);
                var body = new StringContent(json, Encoding.UTF8, "application/json");
                var response = _Http_Client.PostAsync(string.Format("envelope/{0}/files", id), body).Result;
                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, response.Content.ReadAsStringAsync().Result);

                //Ensure the call did not error out. If it did error out, then this will throw an exception.
                response.EnsureSuccessStatusCode();

                //Read the API's response and convert it to a list of TS_Document
                var docs_resp = response.Content.ReadAsStringAsync().Result;
                var docs = JsonConvert.DeserializeObject<List<Document>>(docs_resp);

                //Upload the OnBase document's bytes to the upload url returned by the API
                if (Upload(document, docs[0].Upload_Url))
                    return docs[0];
                else
                    return null;
            }
            catch (Exception ex)
            {
                //An error has occurred. Write the exception to DC and return null.
                _App.Diagnostics.Write(ex);
                return null;
            }
        }

        /// <summary>
        /// Add an external signer to the envelope.
        /// </summary>
        /// <param name="envelope_id"></param>
        /// <param name="dto"></param>
        /// <param name="access_code"></param>
        /// <returns></returns>
        public bool AddExternalSigner(Guid envelope_id, Signer_Dto dto, Access_Code access_code)
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

            if (!_Authenticated)
                Authenticate();

            _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose,
                    string.Format("Adding external signer with email {0} to envelope with ID {1}.", dto.Email, envelope_id.ToString()));

            var json = JsonConvert.SerializeObject(signer);
            var response = _Http_Client.PostAsync($"envelope/{envelope_id}/AddExternalSigner", new StringContent(json, Encoding.UTF8, "application/json")).Result;

            var response_text = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
                throw new Exception(response_text);

            return true;
        }

        /// <summary>
        /// Add an internal signer to the envelope.
        /// </summary>
        /// <param name="envelope_id"></param>
        /// <param name="email"></param>
        /// <returns></returns>
        public bool AddInternalSigner(Guid envelope_id, string email)
        {
            if (envelope_id == Guid.Empty)
                throw new Exception("An envelope ID is required to send this request");

            if (string.IsNullOrEmpty(email))
                throw new Exception("An email address is required for the internal signer");

            _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose,
                    string.Format("Adding internal signer with email {0} to envelope with ID {1}.", email, envelope_id.ToString()));

            Signer signer = new Signer()
            {
                Email = email
            };

            if (!_Authenticated)
                Authenticate();

            var json = JsonConvert.SerializeObject(signer);
            var response = _Http_Client.PostAsync($"envelope/{envelope_id}/AddInternalSigner", new StringContent(json, Encoding.UTF8, "application/json")).Result;

            var response_text = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
                throw new Exception(response_text);

            return true;
        }


        /// <summary>
        /// Close the envelope and make it available to the required signer. 
        /// You must close an envelope for the user to be able to sign it.
        /// </summary>
        /// <param name="id">The envelope ID</param>
        /// <returns>true/false</returns>
        public bool SendEnvelope(Guid id)
        {
            try
            {
                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose,
                    string.Format("Attempting to mark envelope with ID {0} as CLOSED...", id.ToString()));

                //Check if the httpClient is authenticated
                if (!_Authenticated)
                    Authenticate();

                //Call the close endpoint of the API
                var response = _Http_Client.GetAsync(string.Format("envelope/{0}/send", id)).Result;
                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, response.Content.ReadAsStringAsync().Result);

                //Ensure the call did not error out. If it did error out, then this will throw an exception.
                response.EnsureSuccessStatusCode();

                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose,
                    string.Format("Successfully marked envelope with ID {0} as CLOSED.", id.ToString()));

                return true;
            }
            catch (Exception ex)
            {
                //An error has occurred. Write the exception to DC and return false.
                _App.Diagnostics.Write(ex);
                return false;
            }
        }

        /// <summary>
        /// Get an envelope by it's ID
        /// </summary>
        /// <param name="id">The envelope ID</param>
        /// <returns>A TS Envelope object</returns>
        public TrueSignNextLibrary.Envelope GetEnvelope(Guid id)
        {
            try
            {
                //Check if the httpClient is authenticated
                if (!_Authenticated)
                    Authenticate();

                //Call the GET method of the API for the envelope
                var response = _Http_Client.GetAsync(string.Format("envelope/{0}", id)).Result;
                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, response.Content.ReadAsStringAsync().Result);

                //Ensure the call did not error out. If it did error out, then this will throw an exception.
                response.EnsureSuccessStatusCode();

                //Read the JSON response and coonvert it to a TS_Envelope object
                var envelope = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<TrueSignNextLibrary.Envelope>(envelope);
            }
            catch (Exception ex)
            {
                //An error has occurred. Write the exception to DC and return null.
                _App.Diagnostics.Write(ex);
                return null;
            }
        }

        /// <summary>
        /// Read the content of the document as a TrueSign envelope.
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        public TrueSignNextLibrary.Envelope ReadEnvelope(Hyland.Unity.Document document)
        {
            try
            {//Access the page data of the OnBase document (as PDF)
                using (PageData pd = _App.Core.Retrieval.Text.GetDocument(document.DefaultRenditionOfLatestRevision))
                {
                    using (Stream stream = pd.Stream)
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            var content = reader.ReadToEnd();
                            if (content == null)
                                throw new Exception("Unable to read TrueSign Download Document " + document.Name);

                            return JsonConvert.DeserializeObject<TrueSignNextLibrary.Envelope>(content);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                //An error has occurred. Write the exception to DC and return null.
                _App.Diagnostics.Write(ex);
                return null;
            }
        }

        /// <summary>
        /// Delete a TrueSign envelope
        /// </summary>
        /// <param name="id">The envelope ID</param>
        /// <returns>true/false</returns>
        public bool DeleteEnvelope(Guid id)
        {
            try
            {
                //Check if the httpClient is authenticated
                if (!_Authenticated)
                    Authenticate();

                //Call the delte endpoint of the API with the envelope ID
                var response = _Http_Client.DeleteAsync(string.Format("envelope/{0}", id)).Result;
                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, response.Content.ReadAsStringAsync().Result);

                //Ensure the call did not error out. If it did error out, then this will throw an exception.
                response.EnsureSuccessStatusCode();

                return true;
            }
            catch (Exception ex)
            {
                //An error has occurred. Write the exception to DC and return false.
                _App.Diagnostics.Write(ex);
                return false;
            }
        }

        /// <summary>
        /// Upload an OnBase document byte content to the UploadUrl returned by TrueSign API.
        /// </summary>
        /// <param name="doc">The OnBase document object</param>
        /// <param name="url">The Upload URL returned by TrueSign for the document</param>
        /// <returns>true/false</returns>
        public bool Upload(Hyland.Unity.Document doc, string url)
        {
            try
            {
                //Access the page data of the OnBase document (as PDF)
                using (PageData pd = _App.Core.Retrieval.PDF.GetDocument(doc.DefaultRenditionOfLatestRevision))
                {
                    using (Stream s = pd.Stream)
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            s.CopyTo(ms);
                            _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("Downloaded file content."));

                            //Post the stream of the document to the upload URL
                            using (var client = new HttpClient())
                            {
                                var content = new ByteArrayContent(ms.ToArray());
                                content.Headers.Add("x-ms-blob-type", "BlockBlob");
                                var response = client.PutAsync(url, content).Result;

                                //Ensure the call did not error out. If it did error out, then this will throw an exception.
                                response.EnsureSuccessStatusCode();
                            }

                            _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("Uploaded document to TrueSign API"));
                        }
                    }
                }

                //If everything went fine, then return true.
                return true;
            }
            catch (Exception ex)
            {
                //An error has occurred. Write the exception to DC and return false.
                _App.Diagnostics.Write(ex);
                return false;
            }
        }

        /// <summary>
        /// Download a file from the provided URL. 
        /// File will be downloaded in themp directory and a path will be returned.
        /// </summary>
        /// <param name="url">The file URL</param>
        /// <returns>Path of downloaded file</returns>
        public string DownloadFile(string url)
        {
            try
            {
                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("Downloading file with URL: {0}", url));
                //Create a new instance of a HttpClient
                using (var client = new HttpClient())
                {
                    //Get temp file path
                    var path = Path.GetTempFileName();
                    _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("File to be temporarly save at path: {0}", path));

                    //Download the byte content from the URL
                    var content = client.GetByteArrayAsync(new Uri(url)).Result;

                    //Write to the temp file
                    File.WriteAllBytes(path, content);


                    _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("Finished downloading file."));
                    //Return the temp file path
                    return path;
                }
            }
            catch (Exception ex)
            {
                //An error has occurred. Write the exception to DC and return null.
                _App.Diagnostics.Write(ex);
                return null;
            }
        }

        /// <summary>
        /// This method will download the documents of the envelope and will create a new revision of the corresponding OnBase document.
        /// </summary>
        /// <param name="envelope">The envelope object that contains the signed documents</param>
        /// <param name="signedOrStampedOnly">Download only documents that have had a signature or stamp added to them. Default: true</param>
        /// <param name="stampedKeywordName">The name of the keyword to set the Stamped (bool) value to. Default: Stamped</param>
        /// <param name="signedKeywordName">The name of the keyword to set the Signed (bool) value to. Default: Signed</param>
        /// <returns></returns>
        public bool DownloadEnvelopeDocs(Envelope envelope, bool signedOrStampedOnly = true, string stampedKeywordName = "Stamped", string signedKeywordName = "Signed")
        {
            try
            {
                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("Downloading documents for envelope with title: {0}", envelope.Content.Title));

                var docs = new List<TrueSignNextLibrary.Document>();
                if (signedOrStampedOnly)
                    //filter only aigned or stamped documents
                    docs = envelope.Content.Documents.FindAll(x => x.Signed || x.Stamped);
                else
                    //include all documents in the envelope
                    docs = envelope.Content.Documents;

                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("Envelope has {0} documents.", envelope.Content.Documents.Count));

                //Iterate through the document list
                foreach (var doc in docs)
                {
                    _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("Working with envelope document with title: {0} (ClientID: {1})", doc.Title, doc.Client_Id));

                    //Download the TrueSign document to a temp file
                    var path = DownloadFile(doc.Download_Url);
                    if (path != null)
                    {
                        _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("Creating a new OnBase revision for document with ID: {0}", doc.Client_Id));

                        //Create a new revision of the correcponding document in OnBase. Document will be PDF.
                        Storage storage = _App.Core.Storage;
                        Hyland.Unity.Document document = _App.Core.GetDocumentByID(long.Parse(doc.Client_Id));
                        FileType fileType = _App.Core.FileTypes.Find("PDF");
                        StoreRevisionProperties storeRevisionProperties = storage.CreateStoreRevisionProperties(document, fileType);
                        storeRevisionProperties.Comment = "Downloaded from TrueSign Next";

                        List<string> fileList = new List<string>();
                        fileList.Add(path);
                        Hyland.Unity.Document newDocument = storage.StoreNewRevision(fileList, storeRevisionProperties);

                        //If the new document creation was successful
                        if (newDocument != null)
                        {
                            //then set the signed and stamped keywords
                            KeywordModifier keyModifier = newDocument.CreateKeywordModifier();
                            KeywordType signedKeywordType = _App.Core.KeywordTypes.Find(signedKeywordName);
                            KeywordType stampedKeywordType = _App.Core.KeywordTypes.Find(stampedKeywordName);
                            if (signedKeywordType != null)
                            {
                                //Create keyword with value True/False
                                Keyword newKeyword = signedKeywordType.CreateKeyword(doc.Signed.ToString());

                                //Check if the document contains a Signed keyword type
                                var keyRec = newDocument.KeywordRecords.Find(signedKeywordType);
                                if (keyRec != null)
                                {
                                    //Retrieve keyword to update
                                    foreach (Keyword keyword in keyRec.Keywords.FindAll(signedKeywordType))
                                    {
                                        //Update the keyword in the keyword modifier object
                                        keyModifier.UpdateKeyword(keyword, newKeyword);
                                    }
                                }
                                else
                                    keyModifier.AddKeyword(newKeyword);

                            }
                            else
                                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("There was no keyword type found with name: {0}", signedKeywordName));

                            if (stampedKeywordType != null)
                            {
                                //Create keyword with value True/False
                                Keyword newKeyword = stampedKeywordType.CreateKeyword(doc.Stamped.ToString());

                                //Check if the document contains a Stamped keyword type
                                var keyRec = newDocument.KeywordRecords.Find(stampedKeywordType);
                                if (keyRec != null)
                                {
                                    //Retrieve keyword to update
                                    foreach (Keyword keyword in keyRec.Keywords.FindAll(stampedKeywordType))
                                    {
                                        //Update the keyword in the keyword modifier object
                                        keyModifier.UpdateKeyword(keyword, newKeyword);
                                    }
                                }
                                else
                                    keyModifier.AddKeyword(newKeyword);
                            }
                            else
                                _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("There was no keyword type found with name: {0}", stampedKeywordName));

                            //Apply keyword changes
                            keyModifier.ApplyChanges();

                            _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("Successfully created a new OnBase revision for document with ID: {0}. Deleting temp file...", doc.Client_Id));

                            //Delete temp file
                            File.Delete(path);

                            //Check if item needs to be moved from the Waiting Q to the Done Q
                            var waitingQ = _App.Workflow.Queues.Find(x => x.Name == "Waiting" && x.LifeCycle.Name == "TrueSign Next");
                            if (waitingQ != null)
                            {
                                var doneQ = _App.Workflow.Queues.Find(x => x.Name == "Done" && x.LifeCycle.Name == "TrueSign Next");
                                if (doneQ != null)
                                {
                                    try
                                    {
                                        //Try to transition doc - empty catch because the doc might not be in the queue
                                        waitingQ.TransitionDocument(doneQ, document);
                                    }
                                    catch { }

                                    _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("Successfully transitioned document with ID: {0} to the Done queue.", document.ID));
                                }
                            }
                        }
                        else
                        {
                            _App.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Error, string.Format("Failed to create a new OnBase revision for document with ID: {0}", doc.Client_Id));
                        }
                    }
                }

                //If everything went fine, then return true.
                return true;
            }
            catch (Exception ex)
            {
                //An error has occurred. Write the exception to DC and return false.
                _App.Diagnostics.Write(ex);
                return false;
            }
        }
    }

    /*
     * Hepler classes to transfer data to and from the API
     */

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

    public class Envelope_History
    {
        public DateTime DateTime_UTC { get; set; }
        public Envelope_History_Type History_Type { get; set; }
        public string Message { get; set; }
    }

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

    public class Signer
    {
        public string User_Id { get; set; }

        public Signer_Type Type { get; set; }
        public string First_Name { get; set; }
        public string Last_Name { get; set; }
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

    public enum Signer_Type
    {
        Internal,
        External
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

}