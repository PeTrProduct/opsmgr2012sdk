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
    /// Second Sample WriteAction module class. This is used in a Rule, and has one input and no output.Since it has no output,
    /// the ModuleOutput attribute is false
    /// On receiving a dataitem, it appends the string that was passed in through config to the fileName also pased in through the config
    /// </summary>
    ////MonitoringModule attribute tells the Engine what kind of Module it is, In this case it is a WriteActionModule
    [MonitoringModule(ModuleType.WriteAction)]
    //// This tells the Engine that the Module has Output. A Module can only have a single Output. Engine verifies what is specified in the
    //// MP (whether there is output or not) with what is specified here
    [ModuleOutput(false)]
    public class WriteDataItemToFileWriteAction : ModuleBase<DataItemBase>
    {
        /// <summary>
        /// User supplied File Name that the module writes the dataitems to
        /// </summary>
        private readonly string fileName;

        /// <summary>
        /// Object we use for doing locking to check to see if we have been
        /// shutdown or not.  This field is marked as readonly since we set it
        /// in the constructor and should never change it.
        /// </summary>
        private readonly object shutdownLock;

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
        public WriteDataItemToFileWriteAction(
            ModuleHost<DataItemBase> moduleHost,
            XmlReader configuration,
            byte[] previousState)
            : base(moduleHost)
        {
            // Verify parameters given by the Health Service.
            if (moduleHost == null)
            {
                throw new ArgumentNullException("moduleHost");
            }

            if (configuration == null)
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
            //      <FileName>User supplied file name</FileName>
            // </Configuration>

            try
            {
                configuration.MoveToContent();

                configuration.ReadStartElement(
                    Constants.ConfigurationElementName);

                this.fileName = configuration.ReadElementString(
                    Constants.FileNameElementName);

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

            Debug.Assert(this.fileName != null);
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
        /// read the data items and write them to the user-specified file
        /// immediately
        /// </summary>
        /// <param name="dataItems">data received</param>
        /// <param name="logicalSet">Is the data batch a logical set</param>
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
            SampleDataItem[] dataItems,
            bool logicalSet,
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

            // Acquire the lock guarding against shutdown.
            lock (this.shutdownLock)
            {
                // If the module has been shutdown we should stop processing.
                if (this.shutdown)
                {
                    return;
                }

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
                            // Loop through all input data items processing them
                            foreach (SampleDataItem dataItem in dataItems)
                            {
                                // When processing the input data items as a best practice
                                // we should always check for MalformedDataItemException on
                                // any access to the input data.  For this sample we know
                                // that get_SampleInfo won't throw but the catch block is
                                // put in to show how we would handle it if it did.

                                try
                                {
                                    writer.WriteLine("{0}", dataItem.SampleInfo);
                                }
                                catch (MalformedDataItemException)
                                {
                                    // The usual case for this is a coding bug.  On a debug
                                    // build we want to go to the debugger to investigate.
                                    Debug.Assert(false);

                                    // The data item is invalid.  We will just drop it and
                                    // keep going processing the rest of the data.
                                    this.ModuleHost.NotifyDroppedMalformedDataItems(1);
                                }
                            }
                        }
                    }
                }
                catch (UnauthorizedAccessException e)
                {
                    /* We catch all known exceptions except the IOException and rethrow ModuleException because the error is fatal.
                     * This will result in the Module being terminated by the HealthService */
                    throw new ModuleException(String.Format(System.Globalization.CultureInfo.CurrentUICulture, SampleResources.FileWriteError, this.fileName), e);
                }
                catch (ArgumentException e)
                {
                    this.ModuleHost.NotifyError(ModuleErrorSeverity.FatalError, e);
                    return;
                }
                catch (NotSupportedException e)
                {
                    this.ModuleHost.NotifyError(ModuleErrorSeverity.FatalError, e);
                    return;
                }
                catch (IOException e)
                {
                    // An IO exception could be transient.  We will report that
                    // as an error that we couldn't process this data batch but
                    // we might be able to resume from.
                    this.ModuleHost.NotifyError(ModuleErrorSeverity.DataLoss, e);
                }
                catch (SecurityException e)
                {
                    this.ModuleHost.NotifyError(ModuleErrorSeverity.FatalError, e);
                    return;
                }

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
