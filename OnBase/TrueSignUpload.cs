namespace TrueSignNextUpload
{
    using System;
    using System.Text;
    using Hyland.Unity;
    using Hyland.Unity.CodeAnalysis;
    using Hyland.Unity.Workflow;
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
                string propEnvId = "", title = "", email = "", first = "", last = "", codeDesc = "", codeVal = "", notifySigner = "";
                bool external = false, notify = true;

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

                    //If the title was empty, we will set a default one
                    if (string.IsNullOrEmpty(title))
                        title = "OnBase Envelope " + DateTime.Now.ToString("hh:mm");

                    //Create a contact object that will be on external envelopes. 
                    //This info will appear on the email sent to the required signer. NOT REQUIRED
                    var contact = new Contact();
                    contact.First_Name = "";
                    contact.Last_Name = "";
                    contact.Title = "ImageSoft County Court";
                    contact.Email = "help@court.gov";
                    contact.Phone = "(313) 555 - 5555";

                    //Create the actual envelope
                    var env = TrueSign.CreateEnvelope(title, null, null, contact);
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

                //If this is the last document in the batch
                if (args.BatchDocumentsRemaining == 0)
                {
                    //Get all the signer info from prop bags
                    args.SessionPropertyBag.TryGetValue("TrueSignSignerEmail", out email);
                    args.SessionPropertyBag.TryGetValue("TrueSignFirstName", out first);
                    args.SessionPropertyBag.TryGetValue("TrueSignLastName", out last);
                    args.SessionPropertyBag.TryGetValue("TrueSignExternal", out external);
                    args.SessionPropertyBag.TryGetValue("TrueSignCodeDesc", out codeDesc);
                    args.SessionPropertyBag.TryGetValue("TrueSignCodeVal", out codeVal);

                    //If no email prop bag was found, set the signer to the current user.
                    if (string.IsNullOrEmpty(email))
                    {
                        email = app.CurrentUser.EmailAddress;
                        external = false;
                    }

                    //Create the signer data transfer object
                    Signer_Dto signer = new Signer_Dto();
                    signer.Email = email;
                    signer.First_Name = first; //not needed for internal signer
                    signer.Last_Name = last; //not needed for internal signer

                    //Check if this envelope is meant for an external signer
                    if (external)
                    {
                        //If the signer is an external user, we are requiring them to enter a code they would know before being able to sign. 
                        //Only the code description will be known to the signer (they will see it in the email notification). 
                        //The value itself should be something that the signer already knows (like a date of birth, last 4 of SSN, or a case number)
                        // NOT REQUIRED
                        if (!string.IsNullOrEmpty(codeDesc))
                        {
                            Access_Code code = new Access_Code();
                            code.Description = codeDesc;
                            code.Value = codeVal;

                            //Add the signer to the envelope - with an access code.
                            TrueSign.AddExternalSigner(Envelope_Id, signer, code);
                        }
                        else
                            //Add the signer to the envelope - without an access code
                            TrueSign.AddExternalSigner(Envelope_Id, signer, null);
                    }
                    else
                    {
                        args.SessionPropertyBag.TryGetValue("TrueSignNotifySigner", out notifySigner);

                        if (!string.IsNullOrEmpty(notifySigner))
                            notify = bool.Parse(notifySigner);

                        //This is an internal signer so all we need is their email address
                        TrueSign.AddInternalSigner(Envelope_Id, signer.Email, notify);
                    }

                    //Close the envelope and mark it ready for the signer to sign.
                    TrueSign.SendEnvelope(Envelope_Id);
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
    }
}