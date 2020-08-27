namespace TrueSignNextDownload
{
    using System;
    using System.Text;
    using Hyland.Unity;
    using Hyland.Unity.CodeAnalysis;
    using Hyland.Unity.Workflow;
    using System.Collections.Generic;

    //Additional using statements
    using TrueSignNextLibrary; //Add reference -> Unity Script Libraries -> TrueSignNext Library

    /// <summary>
    /// TrueSign Next Download
    /// </summary>
    public class TrueSignNextDownload : Hyland.Unity.IWorkflowScript
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
            var result = true;
            try
            {
                app.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, "Starting Download Envelope Script");

                //Check if this document is of type TrueSign Completed Envelope
                if (args.Document.DocumentType.Name.ToUpper() == "TRUESIGN COMPLETED ENVELOPE")
                {
                    //Initiate a new TrueSign object
                    TrueSignNext TrueSign = new TrueSignNext(app);

                    //Read the JSON of this document as a TrueSign envelope
                    var envelope = TrueSign.ReadEnvelope(args.Document);
                    if (envelope == null)
                        throw new Exception("Unable to read OnBase doc as a TrueSign Envelope");

                    //Add all doc ids to a property bag
                    AddDocsProperty(app, args, envelope);

                    //Check if the envelope was rejected
                    if (envelope.Status == Envelope_Status.Rejected)
                    {
                        AddRejectionProperty(app, args, envelope);
                    }
                    else
                    {
                        app.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("Downloading envelope with ID: {0}", envelope.Id));

                        //Download and create a new revision of the document
                        var success = TrueSign.DownloadEnvelopeDocs(envelope);
                        if (!success)
                            throw new Exception("Unable to download envelope docs from the TrueSign API");
                    }
                }
                else
                {
                    string Envelope_Id = "";
                    if (args.SessionPropertyBag.TryGetValue("TrueSignEnvelopeId", out Envelope_Id))
                    {
                        app.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, string.Format("Downloading envelope with ID: {0}", Envelope_Id));

                        //Initialize a new TrueSign object. We will retrieve the API creds from the prop bags
                        //TrueSignClientId
                        //TrueSignClientSecret
                        TrueSignNext TrueSign = new TrueSignNext(app, args);

                        //Get the envelope from the API
                        var envelope = TrueSign.GetEnvelope(Guid.Parse(Envelope_Id));
                        if (envelope == null)
                            throw new Exception(string.Format("Unable to get envelope with ID {0} from TrueSign API", Envelope_Id));

                        //Add all doc ids to a property bag
                        AddDocsProperty(app, args, envelope);

                        //Check if the envelope was rejected
                        if (envelope.Status == Envelope_Status.Rejected)
                        {
                            AddRejectionProperty(app, args, envelope);
                        }
                        else
                        {
                            //Download and create a new revision of the document
                            var success = TrueSign.DownloadEnvelopeDocs(envelope, false);
                            if (!success)
                                throw new Exception("Unable to download envelope docs from the TrueSign API");
                        }
                    }
                    else
                        throw new Exception("TrueSignEnvelopeId property bag not found.");
                }
            }
            catch (Exception ex)
            {
                app.Diagnostics.Write(ex);
                result = false;
                args.PropertyBag.Set("error", ex.ToString());
            }

            args.ScriptResult = result;
        }
        #endregion

        private void AddRejectionProperty(Application app, Hyland.Unity.WorkflowEventArgs args, TrueSignNextLibrary.Envelope envelope)
        {
            app.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose,
             string.Format("Envelope with ID: {0} has been rejected.", envelope.Id));

            //Find which signer rejected the envelope
            var rejected_signer = envelope.Content.Signers.FindLast(x => x.Rejected);

            //Add the rejected note to a property bag
            var note = string.Format("Envelope was rejected by signer {0} with the following reason: {1}",
             rejected_signer.First_Name + " " + rejected_signer.Last_Name, rejected_signer.Reject_Reason);

            app.Diagnostics.WriteIf(Diagnostics.DiagnosticsLevel.Verbose, note);

            args.SessionPropertyBag.Set("TrueSignEnvelopeRejected", true);
            args.SessionPropertyBag.Set("TrueSignEnvelopeRejectedReason", note);
        }

        private void AddDocsProperty(Application app, Hyland.Unity.WorkflowEventArgs args, TrueSignNextLibrary.Envelope envelope)
        {
            List<long> docHandles = new List<long>();
            envelope.Content.Documents.ForEach(x => docHandles.Add(long.Parse(x.Client_Data)));

            args.SessionPropertyBag.Set("TrueSignEnvelopeDocs", docHandles.ToArray());
            args.SessionPropertyBag.Set("TrueSignEnvelopeId", envelope.Id.ToString());
            args.SessionPropertyBag.Set("TrueSignEnvelopeDocHandle", args.Document.ID.ToString());
        }
    }
}