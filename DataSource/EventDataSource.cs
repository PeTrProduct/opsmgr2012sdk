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
    using Microsoft.EnterpriseManagement.Mom.Modules.DataItems.Event;
    using System.Threading;
    using System.Collections.ObjectModel;
    using System.Xml.XPath;

    [MonitoringModule(ModuleType.DataSource)]
    
    [ModuleOutput(true)]

    class EventDataSource : ModuleBase<MOMEventDataItem>
    {
        ModuleHost<MOMEventDataItem> m_moduleHost;
        bool                         m_shutdown;
        Timer                        m_timer;
        int                          m_timerInterval;
        Guid                         m_managedEntityGuid;
        object                       m_shutdownLock;
        
        public EventDataSource(
            ModuleHost<MOMEventDataItem> moduleHost,
            XmlReader                    configuration,
            byte[]                       previousState
            ):base(moduleHost)
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
            m_shutdownLock = new object();

            LoadConfiguration(configuration);
                    
            m_moduleHost    = moduleHost;
            m_shutdown      = false;
        }

        private void LoadConfiguration(XmlReader configuration)
        {
            try
            {
                configuration.MoveToContent();

                configuration.ReadStartElement(
                    Constants.ConfigurationElementName);

                m_timerInterval = Convert.ToInt32(configuration.ReadElementString(Constants.TimerFrequencyInSeconds));
                m_managedEntityGuid = new Guid(configuration.ReadElementString(Constants.ManagedEntityId));

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
        }

        public override void Start()
        {
            lock (m_shutdownLock)
            {
                if (m_shutdown)
                {
                    return;
                }

                TimerCallback timerCallback = new TimerCallback(ProduceEvent);

                m_timer = new Timer(timerCallback,
                                    null,
                                    0,
                                    m_timerInterval * 1000);              
            }
        }

        public override void Shutdown()
        {
            lock (m_shutdownLock)
            {
                Debug.Assert(!m_shutdown);

                m_shutdown = true;

                m_timer.Dispose();
            }            
        }

        public void ProduceEvent(object state)       
        {
            if (m_shutdown)
            {
                return;
            }

            try
            {
                DateTime    time                = DateTime.Now.ToUniversalTime();
                string      eventOriginId       = Guid.NewGuid().ToString();
                string      eventPublisherID    = Guid.NewGuid().ToString();
                string      eventPublisherName = "EventDataSourceSample";
                string      chanel              = "Application";
                string      loggingComputer     = "computer name goes here";
                uint        eventNumber         = 1;
                uint        eventCategory       = 1;
                uint        eventLevel          = 1;
                string      userName            = "user name goes here";
                string      eventData           = "event data goes here";
                string      managedEntityId     = m_managedEntityGuid.ToString();
                string      ruleId              = m_managedEntityGuid.ToString();
                List<EventMessage> messagesList = new List<EventMessage>();
                ReadOnlyCollection<EventMessage> messages = new ReadOnlyCollection<EventMessage>(messagesList);

                MOMEventDataItem dataItem;

                dataItem = new MOMEventDataItem(time,
                                                eventOriginId,
                                                eventPublisherID,
                                                eventPublisherName,
                                                chanel,
                                                loggingComputer,
                                                eventNumber,
                                                eventCategory,
                                                eventLevel,
                                                userName,
                                                eventData,
                                                managedEntityId,
                                                ruleId,
                                                messages);

                m_moduleHost.PostOutputDataItem(dataItem, DataItemAckCallback, null);
            }
            catch (Exception error)
            {
                EventLog.WriteEntry("SampleEventModule", error.Message);
            }
        }

        private void DataItemAckCallback(object state)
        {
            //This method is called to ack the data item that is submitted in ProduceEvent()
        }
    }
}
