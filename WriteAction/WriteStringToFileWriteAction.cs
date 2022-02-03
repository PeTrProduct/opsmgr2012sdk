namespace OpsMgrModuleSamples
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.EnterpriseManagement.HealthService;
    using System.Xml;
    using System.Diagnostics;
    using System.IO;
    using System.Security;

    /// <summary>
    /// Sample WriteAction module class. This is used in a Task, and has one input and one output, both of BaseData type
    /// On receiving a dataitem, it appends the string that was passed in through config to the fileName also pased in through the config
    /// </summary>
    [MonitoringModule(ModuleType.WriteAction)]
    //// MonitoringModule attribute tells the Health Service what kind of Module it is, In this case it is a WriteActionModule
    [ModuleOutput(true)]

    // This tells the Health Service that the Module has Output. A Module can have only a single Output. The Health Service verifies what is specified in the
    // MP (whether there is output or not) with what is specified here
    public sealed class WriteStringToFileWriteAction : ModuleBase<SampleDataItem>
    {
        #region Private Fields
        
        /// <summary>
        /// User supplied File Name that the module writes the string input to
        /// </summary>
        private readonly string fileName;

        /// <summary>
        /// User supplied string that will be written to the File
        /// </summary>
        private readonly string stringInput;

        /// <summary>
        /// Object we use for doing locking to check to see if we have been
        /// shutdown or not.  This field is marked as readonly since we set it
        /// in the constructor and should never change it.
        /// </summary>
        private readonly object shutdownLock;

        #endregion

        /// <summary>
        /// Boolean value tracking if the module has been shutdown or not.
        /// </summary>
        private bool shutdown;

        /// <summary>
        /// Constructor of the Module is called when the Module is initialized with the moduleHost pointer and the Xml configuration
        /// along with the previous state, if any. If this is a stateful module, whatever state the module stored last would be handed 
        /// back to the module. This is just a byte array.
        /// </summary>
        /// <param name="moduleHost">The host object provides the services the
        /// module needs to interact with the Health Service.</param>
        /// <param name="configuration">XML reader giving the configuration of this module.</param>
        /// <param name="previousState">Previous state of the module.  This must be null since this module never calls SaveState</param>
        public WriteStringToFileWriteAction(
            ModuleHost<SampleDataItem> moduleHost,
            XmlReader configuration,
            byte[] previousState)
            : base(moduleHost)
        {
            // Verify parameters given by the Health Service.
            if (moduleHost == null)
            {
                throw new ArgumentNullException("moduleHost");
            }

            if (null == configuration)
            {
                throw new ArgumentNullException("configuration");
            }

            if (previousState != null)
            {
                // Since this module never calls SaveState this value should be null.
                throw new ArgumentOutOfRangeException("previousState");
            }

            // Create the shutdown block
            this.shutdownLock = new object();

            // Config blob that we expect is of the form:
            // <Configuration>
            //      <FileName>User supplied FileName</FileName>
            //      <StringInput>User supplied string to write to file</StringInput>
            // </Configuration>

            try
            {
                configuration.MoveToContent();

                configuration.ReadStartElement(
                    Constants.ConfigurationElementName);

                this.fileName = configuration.ReadElementString(
                    Constants.FileNameElementName);

                this.stringInput = configuration.ReadElementString(
                    Constants.StringInputElementName);

                configuration.ReadEndElement();
            }
            catch (XmlException xe)
            {
                /* It is important to catch all known exceptions.  From the
                 * module constructor if there is just a generic error then it
                 * should be wrapped as the inner exception to ModuleException
                 * and thrown.  It is also fine to have more detailed events 
                 * and use NotifyError with an event id.
                 */
                throw new ModuleException(SampleResources.XmlError, xe);
            }

            Debug.Assert((this.fileName != null) && (this.stringInput != null));
        }

        /// <summary>
        /// Do any cleanup that you have to do in this method
        /// </summary>
        public override void Shutdown()
        {
            lock (this.shutdownLock)
            {
                Debug.Assert(!this.shutdown);

                this.shutdown = true;
            }
        }

        /// <summary>
        /// This function is called by the Health Service, so it is a necessary method. You should explicitly call RequestNextDataItem() to
        /// receive data in this method
        /// </summary>
        public override void Start()
        {
            // since Shutdown, OnNewDataItems and Start acquire the lock before doing any action, only one of them can execute code in the code
            // segment inside the lock { ... } code block
            lock (this.shutdownLock)
            {
                if (this.shutdown)
                {
                    return;
                }

                // Request the first data batch.
                this.ModuleHost.RequestNextDataItem();
            }
        }

        /// <summary>
        /// This is the function that is called when new data is available. We
        /// read the data items and append the configured string to it and output it
        /// immediately
        /// </summary>
        /// <param name="dataItem">datum received</param>
        /// <param name="acknowledgedCallback">Optional. Callback to be invoked when the module accepts responsibility for the data item.</param>
        /// <param name="acknowledgedState">Parameter that must be passed in the call to acknowledgedCallback</param>
        /// <param name="completionCallback">Optional. Callback to be invoked when the module has completed processing this data batch.</param>
        /// <param name="completionState">Parameter that must be passed in the call to completionCallback</param>
        //// It is critical have this attribute. For every input (number of inputs is defined in the MP), there should be a corresponding input method
        //// InputStream(0) says that this is the input method for Input-0. If we did not have this methodAttribute, Health Service would be unable to find
        //// an input method for port0. (ports are numbered from 0 to (MaxInput -1)). If there are 5 Inputs defined for the Module in the MP,
        //// then there should be a correponding method with InputStream(0), InputStream(1)...InputStream(4). Although it is possible to have a 
        //// single method handle all the inputs, it would not be able to tell which port the input came from since the port nunmber is not passed
        //// along with the data, although the method may able to tell which port the input came from based on the DataItem type
        [InputStream(0)]
        public void OnNewDataItems(
            DataItemBase dataItem,
            DataItemAcknowledgementCallback acknowledgedCallback,
            object acknowledgedState,
            DataItemProcessingCompleteCallback completionCallback,
            object completionState)
        {
            // Either both delegates are null or neither should be.
            if ((acknowledgedCallback == null && completionCallback != null) ||
                (acknowledgedCallback != null && completionCallback == null))
            {
                throw new ArgumentOutOfRangeException(
                    "acknowledgedCallback, completionCallback",
                    SampleResources.AckError);
            }

            // dataitem(s) were received. Since this is used in a task, a fake dataitem was posted to the module. 
            // We ignore the dataitem and write the User specified string to the User specified file
            SampleDataItem outputDataItem = null;
            
              // Acquire the lock guarding against shutdown.
            lock (this.shutdownLock)
            {
                try
                {
                    // Open the file to append to it.
                    using (Stream fs = new FileStream(
                        this.fileName,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.Read))
                    {
                        using (TextWriter writer = new StreamWriter(fs, Encoding.UTF8))
                        {
                            writer.WriteLine(this.stringInput);
                        }
                    }

                    // The output contains just a string indicating successful completion of the action
                    outputDataItem = new SampleDataItem(String.Format(System.Globalization.CultureInfo.CurrentUICulture, "Wrote {0} to file {1} successfully", this.stringInput, this.fileName));
                }
                catch (UnauthorizedAccessException e)
                {
                    /* We catch all known exceptions except the IOException and rethrow ModuleException because the error is fatal.
                     * This will result in the Module being terminated by the HealthService */
                    throw new ModuleException(String.Format(System.Globalization.CultureInfo.CurrentUICulture, SampleResources.FileWriteError, this.fileName), e);
                }
                catch (ArgumentException e)
                {
                    throw new ModuleException(String.Format(System.Globalization.CultureInfo.CurrentUICulture, SampleResources.FileWriteError, this.fileName), e);
                }
                catch (NotSupportedException e)
                {
                    throw new ModuleException(String.Format(System.Globalization.CultureInfo.CurrentUICulture, SampleResources.FileWriteError, this.fileName), e);
                }
                catch (IOException e)
                {
                    // An IO exception could be transient.  We call the HealthService to report a non-fatal error
                    // We call NotifyError with ModuleErrorSeverity.Warning so that the HealthService will log an event, yet not terminate 
                    // the Module
                    this.ModuleHost.NotifyError(ModuleErrorSeverity.Warning, e);
                }
                catch (SecurityException e)
                {
                    throw new ModuleException(String.Format(System.Globalization.CultureInfo.CurrentUICulture, SampleResources.FileWriteError, this.fileName), e);
                }

                if (outputDataItem != null)
                {
                    // We don't want an ack because we cannot recreate the dataItem in case the ack does not arrive.
                    this.ModuleHost.PostOutputDataItem(outputDataItem);
                }

                // If an ack was requested on the data we received we want to acknowledgedge the receipt of the dataitem by calling 
                // the callback function
                if (acknowledgedCallback != null)
                {
                    acknowledgedCallback(acknowledgedState);
                }

                if (completionCallback != null)
                {
                    completionCallback(completionState);
                }

                // Now that we have sent back both the completion and ack we can request the next data item. 
                // It is essential to explicitly call RequestNextDataItem to receive the next dataitem
                this.ModuleHost.RequestNextDataItem();
            }
        }
    }
}
