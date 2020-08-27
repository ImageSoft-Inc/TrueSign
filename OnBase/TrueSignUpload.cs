namespace TrueSignNextUpload
{
    using System;
    using System.Text;
    using Hyland.Unity;
    using Hyland.Unity.CodeAnalysis;
    using Hyland.Unity.Workflow;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using TrueSignNextLibrary;

    /// <summary>
    /// TrueSign Next Upload
    /// </summary>
    public class TrueSignNextUpload : Hyland.Unity.IWorkflowScript
    {
        #region IWorkflowScript
        /// <summary>
        /// Implementation of <see cref="IWorkflowScript.OnWorkflowScriptExecute" />.
        /// <seealso cref="IWorkflowScript" />
        /// </summary>
        /// <param name="app"></param>
        /// <param name="args"></param>
        public void OnWorkflowScriptExecute(Hyland.Unity.Application app, Hyland.Unity.WorkflowEventArgs args)
        {
            bool result = true;
            try
            {
                Guid Envelope_Id = Guid.Empty;
                string propEnvId = "", title = "", notifySigner = "", overrideDeliveryMethod = "", singleEmail = "";
                string[] multipleEmails = new string[10], designers = new string[10];
                bool notify = true, design = false;

                //Initiate a new TrueSign object. The clint API creds must have been set
                //on Session Property bags before the script was called.
                //TrueSignClientID - holds the Guid for the API client ID
                //TrueSignClientSecret - holds a string for the API client secret
                TrueSignNext TrueSign = new TrueSignNext(app, args);

                //Check if the envelope was already created (in a batch situation)
                if (!args.SessionPropertyBag.TryGetValue("TrueSignEnvelopeId", out propEnvId))
                {
                    //You are here because this is a new envelope. Get the title set in a prop bag
                    args.SessionPropertyBag.TryGetValue("TrueSignEnvelopeTitle", out title);

                    args.SessionPropertyBag.TryGetValue("TrueSignOverrideDeliveryMethod", out overrideDeliveryMethod);

                    //If the title was empty, we will set a default one
                    if (string.IsNullOrEmpty(title))
                        title = "OnBase Envelope " + DateTime.Now.ToString("g");

                    //Create a contact object that will be on external envelopes. 
                    //This info will appear on the email sent to the required signer. NOT REQUIRED
                    var contact = new Contact();
                    contact.First_Name = "";
                    contact.Last_Name = "";
                    contact.Title = "ImageSoft County Court";
                    contact.Email = "help@court.gov";
                    contact.Phone = "(313) 555 - 5555";

                    //Create the actual envelope
                    var env = TrueSign.CreateEnvelope(title, null, null, contact, overrideDeliveryMethod);
                    if (env == null)
                        throw new Exception("Failed to create a new envelope");

                    //Set the envelope ID to a property bag.
                    Envelope_Id = env.Id;
                    args.SessionPropertyBag.Set("TrueSignEnvelopeId", Envelope_Id.ToString());
                }
                else
                {
                    //You are here because an envelope has already been created and this is the 1+ document in the batch.
                    //You could also be here because you forgot to clear the properties.
                    Envelope_Id = Guid.Parse(propEnvId);

                    app.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Info, "Envelope had already been created, adding files...");
                }

                //Upload the current document to the envelope
                var doc = TrueSign.AddToEnvelope(Envelope_Id, args.Document);
                if (doc == null)
                    throw new Exception(string.Format("Failed to add document to envelope. Doc ID: {0}", args.Document.ID));

                //Call the function to save all notes in a prop bag.
                //This only works when the envelope has one signer and a TrueSignNoteId prop has been set.
                SaveAnchorsToProp(app, args, doc.Id);

                //If this is the last document in the batch
                if (args.BatchDocumentsRemaining == 0)
                {
                    List<Signer> signers = new List<Signer>();

                    //Check if envelope has multiple or single signer
                    if (args.SessionPropertyBag.TryGetValue("TrueSignSignerEmail", out singleEmail))
                        signers.Add(GetSigner(app, args));
                    else if (args.SessionPropertyBag.TryGetValue("TrueSignSignerEmail", out multipleEmails))
                        signers.AddRange(GetSigners(app, args));
                    else
                        app.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Info, "No property bag for signer email found...");

                    foreach (Signer signer in signers)
                    {
                        //Check if this envelope is meant for an external signer
                        if (signer.Type == Signer_Type.External)
                        {
                            //Add the signer to the envelope.
                            TrueSign.AddExternalSigner(Envelope_Id, signer);
                        }
                        else
                        {
                            args.SessionPropertyBag.TryGetValue("TrueSignNotifySigner", out notifySigner);

                            if (!string.IsNullOrEmpty(notifySigner))
                                notify = bool.Parse(notifySigner);

                            //This is an internal signer so all we need is their email address
                            TrueSign.AddInternalSigner(Envelope_Id, signer, notify);
                        }
                    }

                    args.SessionPropertyBag.TryGetValue("TrueSignDesign", out design);
                    if (!design)
                        //Close the envelope and mark it ready for the signer to sign.
                        TrueSign.SendEnvelope(Envelope_Id);
                    else
                    {
                        args.SessionPropertyBag.TryGetValue("TrueSignDesigners", out designers);
                        var designerList = new List<string>();
                        for (int i = 0; i < designers.Length; i++)
                            if (!string.IsNullOrEmpty(designers[i]))
                                designerList.Add(designers[i]);

                        TrueSign.SetDesigners(Envelope_Id, designerList);
                    }
                }
            }
            catch (Exception ex)
            {
                app.Diagnostics.Write(ex);
                result = false;
                args.PropertyBag.Set("error", ex.Message);
            }
            args.ScriptResult = result;
        }
        #endregion

        private void SaveAnchorsToProp(Hyland.Unity.Application app, Hyland.Unity.WorkflowEventArgs args, Guid docId)
        {
            try
            {
                string email = ""; string[] multipleEmails = new string[10]; long noteId = 0; bool singleSigner = false;

                if (args.SessionPropertyBag.TryGetValue("TrueSignSignerNoteId", out noteId))
                {
                    if (args.SessionPropertyBag.TryGetValue("TrueSignSignerEmail", out email))
                        singleSigner = true;
                    else if (args.SessionPropertyBag.TryGetValue("TrueSignSignerEmail", out multipleEmails))
                        singleSigner = SignerArrayOnlyOne(multipleEmails);

                    if (singleSigner)
                    {
                        if (args.Document.Notes.Count > 0 && noteId > 0)
                        {
                            string noteJson;
                            args.SessionPropertyBag.TryGetValue("TrueSignAnchors", out noteJson);

                            List<Anchor> anchors = string.IsNullOrEmpty(noteJson) ? new List<Anchor>() : JsonConvert.DeserializeObject<List<Anchor>>(noteJson);
                            foreach (var note in args.Document.Notes.FindAll(x => x.NoteType.ID == noteId))
                            {
                                var anchor = new Anchor();
                                anchor.Id = Guid.NewGuid();
                                anchor.Comment = note.Text;
                                anchor.Page = (int)note.PageNumber;
                                anchor.Required = true;
                                anchor.Height = note.Size.Height * 72 / 96;
                                anchor.Width = note.Size.Width * 72 / 96;
                                anchor.X = note.Position.X * 72 / 96;
                                anchor.Y = note.Position.Y * 72 / 96;
                                anchor.Doc_Id = docId;
                                anchor.Client_Data = note.ID.ToString();
                                anchor.Type = Anchor_Type.SignHere;
                                anchors.Add(anchor);
                            }

                            args.SessionPropertyBag.Set("TrueSignAnchors", JsonConvert.SerializeObject(anchors));
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Get the signer information from the property bags. Also add anchors, if any from the SaveAnchorsToProp function
        /// </summary>
        /// <param name="app"></param>
        /// <param name="args"></param>
        /// <returns>A single signer object</returns>
        private Signer GetSigner(Hyland.Unity.Application app, Hyland.Unity.WorkflowEventArgs args)
        {
            Signer signer = new Signer();
            try
            {
                app.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Info, "Adding a single signer to the envelope...");

                string email = "", first = "", last = "", codeDesc = "", codeVal = "";
                bool external = false;

                //Get all the signer info from prop bags
                if (!args.SessionPropertyBag.TryGetValue("TrueSignSignerEmail", out email))
                    throw new Exception("An email must be provided for the signer.");

                args.SessionPropertyBag.TryGetValue("TrueSignFirstName", out first);
                args.SessionPropertyBag.TryGetValue("TrueSignLastName", out last);
                args.SessionPropertyBag.TryGetValue("TrueSignExternal", out external);
                args.SessionPropertyBag.TryGetValue("TrueSignCodeDesc", out codeDesc);
                args.SessionPropertyBag.TryGetValue("TrueSignCodeVal", out codeVal);

                signer.Email = email;
                signer.First_Name = first == null ? "" : first; //not needed for internal signer
                signer.Last_Name = last == null ? "" : last; //not needed for internal signer
                signer.Type = external == true ? Signer_Type.External : Signer_Type.Internal;

                if (signer.Type == Signer_Type.External)
                {
                    if (!string.IsNullOrEmpty(codeDesc) && !string.IsNullOrEmpty(codeVal))
                    {
                        //If the signer is an external user, we are requiring them to enter a code they would know before being able to sign. 
                        //Only the code description will be known to the signer (they will see it in the email notification). 
                        //The value itself should be something that the signer already knows (like a date of birth, last 4 of SSN, or a case number) -- NOT REQUIRED
                        Access_Code code = new Access_Code();
                        code.Description = codeDesc;
                        code.Value = codeVal;
                        signer.Code = code;
                    }
                }

                string noteJson = "";
                if (args.SessionPropertyBag.TryGetValue("TrueSignAnchors", out noteJson))
                {
                    List<Anchor> anchors = string.IsNullOrEmpty(noteJson) ? new List<Anchor>() : JsonConvert.DeserializeObject<List<Anchor>>(noteJson);
                    signer.Anchors = anchors;
                }
            }
            catch
            {
                throw;
            }

            return signer;
        }

        /// <summary>
        /// Get all signers for this envelope from the propery bags. No anchors are possible.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="args"></param>
        /// <returns>A list of signer objects</returns>
        private List<Signer> GetSigners(Hyland.Unity.Application app, Hyland.Unity.WorkflowEventArgs args)
        {
            List<Signer> signers = new List<Signer>();
            try
            {
                app.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Info, "Adding multiple signers to the envelope...");
                string[] email = new string[10], first = new string[10], last = new string[10],
                        codeDesc = new string[10], codeVal = new string[10];
                bool[] external = new bool[10];
                bool singleSigner = false;

                //Get all the signer info from prop bags
                if (!args.SessionPropertyBag.TryGetValue("TrueSignSignerEmail", out email))
                    throw new Exception("An email must be provided for the signer.");

                singleSigner = SignerArrayOnlyOne(email);

                args.SessionPropertyBag.TryGetValue("TrueSignFirstName", out first);
                args.SessionPropertyBag.TryGetValue("TrueSignLastName", out last);
                args.SessionPropertyBag.TryGetValue("TrueSignExternal", out external);
                args.SessionPropertyBag.TryGetValue("TrueSignCodeDesc", out codeDesc);
                args.SessionPropertyBag.TryGetValue("TrueSignCodeVal", out codeVal);

                if (singleSigner)
                {
                    app.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Info, "This property bag has a single signer...");
                    //Create the signer data transfer object
                    Signer signer = new Signer();
                    signer.Email = email[0];
                    signer.First_Name = first == null ? "" : first[0] ?? ""; //not needed for internal signer
                    signer.Last_Name = last == null ? "" : last[0] ?? ""; //not needed for internal signer
                    signer.Type = external == null ? Signer_Type.Internal : external[0] == true ? Signer_Type.External : Signer_Type.Internal;

                    if (signer.Type == Signer_Type.External)
                    {
                        if (!string.IsNullOrEmpty(codeDesc[0]) && !string.IsNullOrEmpty(codeVal[0]))
                        {
                            //If the signer is an external user, we are requiring them to enter a code they would know before being able to sign. 
                            //Only the code description will be known to the signer (they will see it in the email notification). 
                            //The value itself should be something that the signer already knows (like a date of birth, last 4 of SSN, or a case number) -- NOT REQUIRED
                            Access_Code code = new Access_Code();
                            code.Description = codeDesc[0];
                            code.Value = codeVal[0];
                            signer.Code = code;
                        }
                    }

                    string noteJson = "";
                    if (args.SessionPropertyBag.TryGetValue("TrueSignAnchors", out noteJson))
                    {
                        List<Anchor> anchors = string.IsNullOrEmpty(noteJson) ? new List<Anchor>() : JsonConvert.DeserializeObject<List<Anchor>>(noteJson);
                        signer.Anchors = anchors;
                    }

                    signers.Add(signer);
                }
                else
                {
                    for (int i = 0; i < email.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(email[i]))
                        {
                            //Create the signer data transfer object
                            Signer signer = new Signer();
                            signer.Email = email[i];
                            signer.First_Name = first == null ? "" : first[i] ?? ""; //not needed for internal signer
                            signer.Last_Name = last == null ? "" : last[i] ?? ""; //not needed for internal signer
                            signer.Type = external == null ? Signer_Type.Internal : external[i] == true ? Signer_Type.External : Signer_Type.Internal;

                            if (signer.Type == Signer_Type.External)
                            {
                                if (!string.IsNullOrEmpty(codeDesc[i]) && !string.IsNullOrEmpty(codeVal[i]))
                                {
                                    //If the signer is an external user, we are requiring them to enter a code they would know before being able to sign. 
                                    //Only the code description will be known to the signer (they will see it in the email notification). 
                                    //The value itself should be something that the signer already knows (like a date of birth, last 4 of SSN, or a case number) -- NOT REQUIRED
                                    Access_Code code = new Access_Code();
                                    code.Description = codeDesc[i];
                                    code.Value = codeVal[i];
                                    signer.Code = code;
                                }
                            }

                            signers.Add(signer);
                        }
                    }
                }
            }
            catch
            {
                throw;
            }

            return signers;
        }

        private bool SignerArrayOnlyOne(string[] email)
        {
            List<string> signerEmails = new List<string>(email);
            signerEmails.RemoveAll(x => string.IsNullOrEmpty(x));
            if (signerEmails.Count == 1)
                return true;
            else
                return false;
        }
    }
}