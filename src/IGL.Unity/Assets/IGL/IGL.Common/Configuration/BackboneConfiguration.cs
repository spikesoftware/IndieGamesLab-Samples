using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IGL.Configuration
{
    public class BackboneConfiguration
    {
        public string ServiceNamespace { get; set; }
        public string IssuerName { get; set; }
        public string IssuerSecret { get; set; }

        public string GetServiceMessagesAddress(string queue)
        {
            return string.Format("https://{0}.{1}/{2}/messages", ServiceNamespace, SBHostName, queue);
        }

        public string GetServiceSubscriptionsAddress(string queue, string subscription)
        {
            return string.Format("https://{0}.{1}/{2}/subscriptions/{3}/messages/head?timeout=60", ServiceNamespace, SBHostName, queue, subscription);
        }

        public string GetACSEndpoint()
        {
            return "https://" + ServiceNamespace + "-sb." + ACSHostName + "/WRAPv0.9/";
        }

        public string GetRealm()
        {
            return "http://" + ServiceNamespace + "." + SBHostName + "/";
        }
        
        internal const string ACSHostName = "accesscontrol.windows.net";
        internal const string SBHostName = "servicebus.windows.net";
    }
}
