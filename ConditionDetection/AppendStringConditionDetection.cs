// <copyright company="Microsoft Corporation" file="ConditionDetectionSample.cs">
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>
// This file implements a Sample Condition Detection Module that appends a string to everydata Item that it receives and passes it on.
// </summary>

namespace OpsMgrModuleSamples
{
    using Microsoft.EnterpriseManagement.HealthService;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Xml;

    /// <summary>
    /// Sample ConditionDetection module class. This is used in a Rule, and has one input and one output, both of SampleData type
    /// On receiving a dataitem, it appends the string that was passed in through config to the dataItem that came in and outputs it
    /// </summary>
    //// MonitoringModule attribute tells the Health Service what kind of Module it is, In this case it is a ConditionDetectionModule
    [MonitoringModule(ModuleType.Condition)]

    // This tells the Health Service that the Module has Output. A Module can have only a single Output. The Health Service verifies what is specified in the
    // MP (whether there is output or not) with what is specified here
    [ModuleOutput(true)]
    public sealed class AppendStringConditionDetection : ModuleBase<SampleDataItem>
    {
        /// <summary>
        /// The string we are adding to each data item that comes through.
        /// This field is readonly since we get the value from configuration
        /// in the constructor
        /// </summary>
        private readonly string stringToAppend;

        /// <summary>
        /// Object we use for doing locking to check to see if we have been
        /// shutdown or not.  This field is marked as readonly since we set it
        /// in the constructor and should never change it.
        /// </summary>
        private readonly object shutdownLock;

        #region XML Element Names

        /// <summary>
        /// The outer element of configuration XML to a module is always
        /// "Configuration".
        /// </summary>
        private const string ConfigurationElementName = "Configuration";

        /// <summary>
        /// Our own portion of configuration has the element name
        /// "StringToAppend".
        /// </summary>
        private const string StringToAppendElementName = "StringToAppend";

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
        public AppendStringConditionDetection(
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
            //      <StringToAppend>User supplied string to append</StringToAppend>
            // </Configuration>

            try
            {
                configuration.MoveToContent();

                configuration.ReadStartElement(AppendStringConditionDetection.ConfigurationElementName);
                configuration.ReadStartElement(AppendStringConditionDetection.StringToAppendElementName);

                this.stringToAppend = configuration.ReadString();

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
                throw new ModuleException("For production code this should be a localized string about invalid XML.", xe);
            }

            // Sanity check that we don't have a NULL string here

            Debug.Assert(this.stringToAppend != null);
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

            // If an ack was requested on the data we received we want to
            // request an ack on the data we post.  If there was no ack on the
            // input data it doesn't make sense to request an ack on the
            // output.
            bool ackNeeded = acknowledgedCallback != null;

            // Acquire the lock guarding against shutdown.
            lock (this.shutdownLock)
            {
                // If the module has been shutdown we should stop processing.
                if (this.shutdown)
                {
                    return;
                }

                // Create the list we will use for storing output
                List<SampleDataItem> outputDataItems = new List<SampleDataItem>();

                bool corruptLogicalSet = false;

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
                        SampleDataItem outputDataItem = new SampleDataItem(
                            dataItem.SampleInfo + this.stringToAppend);

                        outputDataItems.Add(outputDataItem);
                    }
                    catch (MalformedDataItemException)
                    {
                        // The usual case for this is a coding bug.  On a debug
                        // build we want to go to the debugger to investigate.
                        Debug.Assert(false);

                        // On malformed data we need to drop the data item.  If
                        // the batch is a logical set we drop the entire batch
                        // since we shouldn't break up the set.
                        if (logicalSet)
                        {
                            corruptLogicalSet = true;
                            this.ModuleHost.NotifyDroppedMalformedDataItems(dataItems.Length);

                            // We are going to drop the whole batch so we can
                            // stop processing.
                            break;
                        }
                        else
                        {
                            // Not a set we can drop out this individual item only.

                            // Notify the host that we dropped a data item.
                            this.ModuleHost.NotifyDroppedMalformedDataItems(1);
                        }
                    }
                } 

                // If we ended up with no data items due to corruption or any
                // items in a logical set were corrupted we will have no
                // output.  We need to give any acks and then request the next
                // data item.
                if (outputDataItems.Count == 0 || corruptLogicalSet)
                {
                    if (ackNeeded)
                    {
                        acknowledgedCallback(acknowledgedState);
                        completionCallback(completionState);
                    }

                    this.ModuleHost.RequestNextDataItem();

                    return;
                }

                if (ackNeeded)
                {
                    // We want to forward on the acknowledgement on input data
                    // to the next module.  We create an anonymous delegate to
                    // process handling the ack.
                    DataItemAcknowledgementCallback ackDelegate = delegate(object ackState)
                    {
                        // We set this parameter to null when calling
                        // PostOutputDataItems so we expect this parameter to be null here.
                        Debug.Assert(ackState == null);

                        lock (this.shutdownLock)
                        {
                            // If we have been shutdown stop processing.
                            if (this.shutdown)
                            {
                                return;
                            }

                            // Send the ack and completion back for the input.
                            acknowledgedCallback(acknowledgedState);
                            completionCallback(completionState);

                            // Know that we have sent back both the completion and
                            // ack we can request the next data item.
                            this.ModuleHost.RequestNextDataItem();
                        }
                    };

                    this.ModuleHost.PostOutputDataItems(outputDataItems.ToArray(), logicalSet, ackDelegate, null);
                }
                else
                {
                    // No ack was requested on input.  We can post the output
                    // and then immediately request the next data items
                    this.ModuleHost.PostOutputDataItems(outputDataItems.ToArray(), logicalSet);

                    this.ModuleHost.RequestNextDataItem();
                }
            }
        }
    }
}
