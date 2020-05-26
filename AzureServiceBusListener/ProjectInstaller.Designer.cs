namespace AzureServiceBusListener
{
    partial class ProjectInstaller
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.AzureServiceBusProcessInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this.AzureServiceBusListenerInstaller = new System.ServiceProcess.ServiceInstaller();
            // 
            // AzureServiceBusProcessInstaller
            // 
            this.AzureServiceBusProcessInstaller.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
            this.AzureServiceBusProcessInstaller.Password = null;
            this.AzureServiceBusProcessInstaller.Username = null;
            // 
            // AzureServiceBusListenerInstaller
            // 
            this.AzureServiceBusListenerInstaller.Description = "A sample service that connects to an Azure Service Bus queue and listens for new " +
    "messages.";
            this.AzureServiceBusListenerInstaller.DisplayName = "Azure Service Bus Listener";
            this.AzureServiceBusListenerInstaller.ServiceName = "AzureServiceBusListener";
            this.AzureServiceBusListenerInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.AzureServiceBusProcessInstaller,
            this.AzureServiceBusListenerInstaller});

        }

        #endregion

        private System.ServiceProcess.ServiceProcessInstaller AzureServiceBusProcessInstaller;
        private System.ServiceProcess.ServiceInstaller AzureServiceBusListenerInstaller;
    }
}