using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml.XPath;
using Xeno.Data;
using Xeno.Prodika.Application;
using Xeno.Prodika.Services;
using Xeno.Web.UI.Providers.Security;

namespace SIntCrearItemMDMPlugin
{
    public class SIntCrearItemMDMEvaluator : ICustomSecurityEvaluator
    {
        public ISpecificationService SpecService
        {
            get
            {
                return AppPlatformHelper.ServiceManager.GetServiceByType<ISpecificationService>();
            }
        }

        public bool Evaluate()
        {
            try
            {
                if (SpecService.Current == null)
                {
                    return false;
                }
                else
                {
                    var result = true;
                    var configValue = (string)Utils.GetConfigurationValue("string(/configuration/constants/status/text())");
                    var validStatus = new List<string>();
                    foreach (var status in configValue.Split(','))
                    {
                        validStatus.Add(status.Trim());
                    }
                    result = result && validStatus.Contains(((IBaseSpec)SpecService.Current).SpecSummary.WorkflowStatus.Status);

                    result = result && Utils.GetLatestIntegrationStatus(SpecService.Current.PKID, ((IBaseSpec)SpecService.Current).SpecSummary.WorkflowStatus.Status) != "S";

                    return result;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("SIntCrearItemMDMEvaluator: " + ex.ToString());
                return false;
            }
        }
    }
}