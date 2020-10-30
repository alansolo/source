using SIntCrearItemMDMPlugin.SIntCrearItemMdmWS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Web;

namespace SIntCrearItemMDMPlugin
{
    internal class ServiceClientHelper
    {
        public static BPEL_CrearItemMdmClient GetClient(string endpointAddress)
        {
            //binding
            var binding = new BasicHttpBinding();
            binding.Name = "SoapBinding";

            //endpoint
            var endpoint = new EndpointAddress(endpointAddress);

            return new BPEL_CrearItemMdmClient(binding, endpoint);
        }
    }
}