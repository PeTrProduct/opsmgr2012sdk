// <copyright company="Microsoft Corporation" file="ProbeActionSample.cs">
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>
// This file implements a Sample ProbeAction Module that computes the size of a given folder and submits this dataItem to the next module
// </summary>


namespace OpsMgrModuleSamples
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.EnterpriseManagement.HealthService;
    using System.Xml;
    using System.IO;
    using System.Diagnostics;
    using System.Security;

    /// <summary>
    /// Class that implements the ProbeActionModule. MonitoringModule is ReadAction indicating a ProbeAction. 
    /// ModuleOutput attribute is true. This is typically true for ProbeActions as one would would expect a ProbeAction or a DataSource
    /// to submit a value; that is what a ProbeAction is used for.
    /// This ProbeAction in particular accepts a folder Name from config and computes the folder size everytime a trigger data item is 
    /// received, and posts the folder size as output
    /// The OutputDataType is SampleDataItem and that is the reason it inherits from ModuleBase templated with SampleDataItem
    /// The InputType is the Base Data Item type, since we don't really care about the content of the dataitem, and we need it only to trigger 
    /// our actions
    /// </summary>
    [MonitoringModule(ModuleType.ReadAction), ModuleOutput(true)]
    public class FolderSizeProbe : ModuleBase<SampleDataItem>
    {
        /// <summary>
        /// Object we use for doing locking to check to see if we have been
        /// shutdown or not.  This field is marked as readonly since we set it
        /// in the constructor and should never change it.
        /// </summary>
        private readonly object shutdownLock;

        /// <summary>
        /// User supplied folder. Again, this is marked readonly since we set it in the constructor and should never change it
        /// </summary>
        private readonly string folderName;

        #region XML Element Names

        /// <summary>
        /// The outer element of configuration XML to a module is always
        /// "Configuration".
        /// </summary>
        private const string ConfigurationElementName = "Configuration";

        /// <summary>
        /// Our own portion of the configuration about the Folder to size
        /// "FolderName".
        /// </summary>
        private const string FolderNameTag = "FolderName";

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
        public FolderSizeProbe(
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
            //      <FolderName>User-supplied foldername</FolderName>
            // </Configuration>

            try
            {
                configuration.MoveToContent();

                configuration.ReadStartElement(FolderSizeProbe.ConfigurationElementName);
                configuration.ReadStartElement(FolderSizeProbe.FolderNameTag);

                this.folderName = configuration.ReadString();

                configuration.ReadEndElement();
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

            Debug.Assert((this.folderName != null));

            if (!Directory.Exists(this.folderName))
            {
                throw new ModuleException(String.Format(System.Globalization.CultureInfo.CurrentUICulture, SampleResources.DirectoryDoesNotExistError, this.folderName));
            }
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
        /// This function is called by the Health Service, so it is a necessary method. Module should explicitly call RequestNextDataItem() 
        /// to receive data in this method
        /// </summary>
        public override void Start()
        {
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
        /// This is the function that is called when new data is available. Since this is a Rule workflow with a Scheduler, the dataitem 
        /// that is received is a fake trigger dataitem, so the function doesn't use it but the ConditionDetection that follows this module
        /// will use the dataItem
        /// </summary>
        /// <param name="dataItem">dataItem received</param>
        /// <param name="acknowledgedCallback">Optional. Callback to be invoked when the module accepts responsibility for the data item.</param>
        /// <param name="acknowledgedState">Parameter that must be passed in the call to acknowledgedCallback</param>
        /// <param name="completionCallback">Optional. Callback to be invoked when the module has completed processing this data batch.</param>
        /// <param name="completionState">Parameter that must be passed in the call to completionCallback</param>
        [InputStream(0)]
        //// It is critical have this attribute. For every input (number of inputs is defined in the MP), there should be a corresponding input method
        //// InputStream(0) says that this is the input method for Input-0. If we did not have this methodAttribute, Health Service would be unable to find
        //// an input method for port0. (ports are numbered from 0 to (MaxInput -1)). If there are 5 Inputs defined for the Module in the MP,
        //// then there should be a correponding method with InputStream(0), InputStream(1)...InputStream(4). Although it is possible to have a 
        //// single method handle all the inputs, it would not be able to tell which port the input came from since the port nunmber is not passed
        //// along with the data, although the method may able to tell which port the input came from based on the DataItem type
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

            // dataitem(s) were received. Since the dataitems are used only as a trigger, we don't really use the dataitem itself. 
            // We receive the trigger dataitem (from the Scheduler DataSource, in this case), then compute the folderSize of the 
            // given folder and output the folderSize 

            // Acquire the lock guarding against shutdown.
            lock (this.shutdownLock)
            {
                // If the module has been shutdown we should stop processing.
                if (this.shutdown)
                {
                    return;
                }

                long folderSize = 0;
                SampleDataItem outputDataItem = null;

                try
                {
                    folderSize = this.GetFolderSize(this.folderName);
                    outputDataItem = new SampleDataItem(String.Format(System.Globalization.CultureInfo.CurrentUICulture, "Folder size is '{0}' bytes", folderSize));
                }
                catch (SecurityException e)
                {
                    /* We catch all known exceptions except the IOException and rethrow ModuleException because the error is fatal.
                     * This will result in the Module being terminated by the HealthService */
                    throw new ModuleException(
                        String.Format(System.Globalization.CultureInfo.CurrentUICulture, SampleResources.GetFolderSizeError, this.folderName), e);
                }
                catch (NotSupportedException e)
                {
                    throw new ModuleException(
                        String.Format(System.Globalization.CultureInfo.CurrentUICulture, SampleResources.GetFolderSizeError, this.folderName), e);
                }
                catch (PathTooLongException e)
                {
                    throw new ModuleException(
                        String.Format(System.Globalization.CultureInfo.CurrentUICulture, SampleResources.GetFolderSizeError, this.folderName), e);
                }
                catch (DirectoryNotFoundException e)
                {
                    throw new ModuleException(
                        String.Format(System.Globalization.CultureInfo.CurrentUICulture, SampleResources.GetFolderSizeError, this.folderName), e);
                }
                catch (IOException e)
                {
                    // This may be transient, so we call NotifyError with Warning severity so that HealthService does not unload 
                    // the module
                    this.ModuleHost.NotifyError(ModuleErrorSeverity.Warning, e);
                }
                catch (UnauthorizedAccessException e)
                {
                    throw new ModuleException(
                        String.Format(System.Globalization.CultureInfo.CurrentUICulture, SampleResources.GetFolderSizeError, this.folderName), e);
                }
                catch (ArgumentNullException e)
                {
                    throw new ModuleException(
                        String.Format(System.Globalization.CultureInfo.CurrentUICulture, SampleResources.GetFolderSizeError, this.folderName), e);
                }
                catch (ArgumentException e)
                {
                    throw new ModuleException(
                        String.Format(System.Globalization.CultureInfo.CurrentUICulture, SampleResources.GetFolderSizeError, this.folderName), e);
                }

                // Since this is a trigger-driven module, there is no point in asking for an acknowledgement since
                // we will not be able to recreate the dataitem exactly in case the acknowledgement does not arrive
                // Since we are submitting time-dependent data, it does not make sense to resend the old data in case 
                // it's acknowledgement is not received, since the old data is now stale.

                // If an ack was requested on the data we received we want to acknowledge the data by calling the callback function
                if (acknowledgedCallback != null)
                {
                    acknowledgedCallback(acknowledgedState);
                }

                if (completionCallback != null)
                {
                    completionCallback(completionState);
                }

                if (outputDataItem != null)
                {
                    this.ModuleHost.PostOutputDataItem(outputDataItem);
                }

                // Now that we have sent back both the completion and ack we can request the next data item. 
                // It is essential to explicitly call RequestNextDataItem to receive the next dataitem
                this.ModuleHost.RequestNextDataItem();
            }
        }

        /// <summary>
        /// Helper function that returns the size of the given folder
        /// </summary>
        /// <param name="folderName">folder whose size is required</param>
        /// <returns>folder size in bytes as long</returns>
        private long GetFolderSize(string folderName)
        {
            // Total size of all the files in the directory (directly, and not located in the subdirectories)
            long totalFileSize = 0;

            // Total size of all the sub-directories, calculated by calling self recursively 
            long totalSubDirectorySize = 0;

            string[] allFiles = Directory.GetFiles(folderName);

            foreach (string fileName in allFiles)
            {
                if (File.Exists(fileName))
                {
                    FileInfo fileInfo = new FileInfo(fileName);

                    totalFileSize += fileInfo.Length;
                }
            }

            string[] alldirectories = Directory.GetDirectories(folderName);

            foreach (string directoryName in alldirectories)
            {
                long subDirectorySize = this.GetFolderSize(directoryName);
                totalSubDirectorySize += subDirectorySize;
            }

            return (totalFileSize + totalSubDirectorySize);
        }
    } 
}
