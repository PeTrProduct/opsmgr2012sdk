using System;
using System.Collections.Generic;
using System.Text;

namespace OpsMgrModuleSamples
{
    class Constants
    {
        /// <summary>
        /// The outer element of configuration XML to a module is always
        /// "Configuration".
        /// </summary>
        public static string ConfigurationElementName   = "Configuration";
        public static string FileNameElementName        = "FileName";
        public static string StringInputElementName     = "StringInput";
        public static string TimerFrequencyInSeconds    = "TimerFrequency";
        public static string ManagedEntityId            = "MEId";
    }
}
