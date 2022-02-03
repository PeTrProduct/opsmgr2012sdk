namespace OpsMgrModuleSamples
{
    using Microsoft.EnterpriseManagement.HealthService;
    using System;
    using System.Diagnostics;
    using System.Xml;

    /// <summary>
    /// Implementation of a data item that adds a single extra string field
    /// called "SampleInfo" 
    /// </summary>
    public class SampleDataItem : DataItemBase
    {
        /// <summary>
        /// Data to be persisted and also posted on output stream
        /// The extra element this data item adds to base data.  The field is
        /// marked as readonly since data items must be invariant.
        /// </summary>
        private readonly string sampleInfo;

        /// <summary>
        /// The name of the XML element we store our data in.
        /// </summary>
        private const string SampleInfoElementName = "SampleInfo";

        /// <summary>
        /// Constructor to create a new instance of this data item.
        /// </summary>
        /// <param name="sampleInfo">The value to be used for the SampleInfo element</param>
        public SampleDataItem(string sampleInfo)
        {
            if (sampleInfo == null)
            {
                throw new ArgumentNullException("sampleInfo");
            }

            this.sampleInfo = sampleInfo;
        }

        /// <summary>
        /// Constructor for creating this data item from XML.  When the base
        /// class constructor returns the reader will be positioned at the
        /// start of the data for this object.
        /// </summary>
        /// <param name="reader">XmlReader containing the XML version of this data item.</param>
        public SampleDataItem(XmlReader reader)
            : base(reader)
        {
            // Read our portion of the XML document.  If the XML is invalid we
            // must throw MalformedDataItemException.  It doesn't apply for
            // this data item since we don't do additional validation but all
            // data must be validated and any failures must result in
            // MalformedDataItemException being thrown.  Known transient
            // failures must result in ModuleException being thrown.  Other
            // exception types may cause the host process to be taken down.
            try
            {
                this.sampleInfo = reader.ReadElementString(
                    SampleDataItem.SampleInfoElementName);
            }
            catch (XmlException xe)
            {
                throw new MalformedDataItemException(
                    "This should be localized text about invalid XML in production.", xe);
            }

            Debug.Assert(this.sampleInfo != null);
        }

        /// <summary>
        /// This is the name of the data item as specified in the MP.
        /// </summary>
        public override string DataItemTypeName
        {
            get
            {
                return "Microsoft.Mom.Samples.SampleData";
            }
        }

        /// <summary>
        /// Data to be persisted and also posted on output stream.  Since data
        /// items are invariant there is not set a accessor.
        /// </summary>
        public string SampleInfo
        {
            get
            {
                return this.sampleInfo;
            }
        }

        /// <summary>
        /// This writes out the portion of the XML that is specific to this
        /// derived data item.
        /// </summary>
        /// <param name="writer">XML stream to write to.</param>
        protected override void GenerateItemXml(XmlWriter writer)
        {
            // We just need to write out our portion of the XML document.  The
            // base class has handled writing out its portion before it called
            // into this method.
            writer.WriteElementString(
                SampleDataItem.SampleInfoElementName,
                this.SampleInfo);
        }
    }
}
