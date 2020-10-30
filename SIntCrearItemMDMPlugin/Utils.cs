using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Serialization;
using System.Xml.XPath;
using Xeno.Data;
using Xeno.Data.Common;
using Xeno.Data.Meta;
using Xeno.Prodika.Application;
using Xeno.Prodika.Reflection;
using Xeno.Prodika.Services.UOMService;

namespace SIntCrearItemMDMPlugin
{
    public class Utils
    {
        internal static string GetLatestIntegrationStatus(string specId, string worflowStatus)
        {
            string status = null;

            string sql = string.Format("SELECT status FROM SIntCrearItemMDMHistory WHERE specId = '{0}' AND workflowStatus = '{1}' ORDER BY transactionDate DESC", specId, worflowStatus);
            try
            {
                using (IDataReader reader = AppPlatformHelper.DataManager.newQuery().execute(sql))
                {
                    if (reader.Read())
                    {
                        var value = reader.GetValue(reader.GetOrdinal("status"));
                        status = value is System.DBNull ? null : value.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("GetLatestIntegrationStatus" + ex.ToString());
            }

            return status;
        }

        internal static int SaveIntegrationStatus(string specId, string worflowStatus, string status)
        {
            try
            {
                string sql = string.Format("INSERT INTO SIntCrearItemMDMHistory (pkid, specId, workflowStatus, transactionDate, status) VALUES ('{0}','{1}','{2}',TO_DATE('{3}', 'YYYY/MM/DD HH24:MI:SS'),{4})",
                    Guid.NewGuid(),
                    specId,
                    worflowStatus,
                    DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
                    string.IsNullOrWhiteSpace(status) ? "null" : "'" + status.Substring(0, 1) + "'");
                return AppPlatformHelper.DataManager.newQuery().executeNonQuery(sql);
            }
            catch (Exception ex)
            {
                Logger.Log("SaveIntegrationStatus" + ex.ToString());
                return 0;
            }
        }

        internal static ILegacyProfile GetLegacyProfile(string systemCode)
        {
            var manager = AppPlatformHelper.DataManager;
            using (IDataReader reader = manager.newQuery().execute(string.Format("select * from specLegacyProfile where SystemCode = '{0}'", (object)systemCode)))
            {
                if (reader.Read())
                    return manager.objectFromID(DataReaderHelper.GetPKIDInString(reader), (IDataRecord)reader) as ILegacyProfile;
            }
            return (ILegacyProfile)null;
        }

        internal static IBaseSpec AddLegacyProfile(IBaseSpec baseSpec, ILegacyProfiles legacyProfiles)
        {
            if (ReflectionHelper.HasProperty((object)baseSpec, "LegacyProfiles"))
            {
                IXDataObjectCollection propObject = ReflectionHelper.GetPropObject((object)baseSpec, "LegacyProfiles") as IXDataObjectCollection;
                if (propObject != null)
                {
                    propObject.Add((IXUniqueObject)legacyProfiles);
                    ReflectionHelper.SetPropObject(baseSpec, "LegacyProfiles", propObject);
                }
            }
            return baseSpec;
        }

        internal static decimal ConvertUOM(double fromValue, string fromUOMISO, string toUOMISO)
        {
            try
            {
                var uomService = GetService<IUOMService>();
                return (decimal)uomService.Convert(uomService.GetUOMByISOCode(fromUOMISO), uomService.GetUOMByISOCode(toUOMISO), fromValue);
            }
            catch (Exception ex)
            {
                Logger.Log("ConvertUOM: " + ex.ToString());
                return (decimal)fromValue;
            }
        }

        internal static string XmlSerialize(object obj)
        {
            try
            {
                XmlSerializer x = new XmlSerializer(obj.GetType());
                using (var sw = new StringWriter())
                {
                    x.Serialize(sw, obj);
                    return sw.ToString();
                }
            }
            catch (Exception)
            {
                return obj.ToString();
            }
        }

        internal static object ChangeType(object value, Type conversion)
        {
            var t = conversion;

            if (t.IsGenericType && t.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                if (value == null)
                {
                    return null;
                }

                t = Nullable.GetUnderlyingType(t);
            }

            return Convert.ChangeType(value, t);
        }

        internal static string GetEAValue(IExtendedAttributeBaseDO ea, string option = "")
        {
            if ((new string[] { "min", "max" }).Contains(option) && !(ea is IExtendedAttributeQuantitativeRangeDO))
            {
                return null;
            }
            if (ea is IExtendedAttributeTextDO)
            {
                if (option.Contains("regex:"))
                {
                    Match match = Regex.Match(((IExtendedAttributeTextDO)ea).AttributeText, option.Replace("regex:", string.Empty));
                    if (match.Success) return match.Value;
                }
                return ((IExtendedAttributeTextDO)ea).AttributeText;
            }
            else if (ea is IExtendedAttributeLongTextDO)
            {
                if (option.Contains("regex:"))
                {
                    Match match = Regex.Match(((IExtendedAttributeLongTextDO)ea).AttributeText, option.Replace("regex:", string.Empty));
                    if (match.Success) return match.Value;
                }
                return ((IExtendedAttributeLongTextDO)ea).AttributeText;
            }
            else if (ea is IExtendedAttributeNumericDO)
            {
                return ((IExtendedAttributeNumericDO)ea).AttributeValue.ToString();
            }
            else if (ea is IExtendedAttributeBooleanDO)
            {
                return ((IExtendedAttributeBooleanDO)ea).AttributeState == 1 ? "Y" : "N";
            }
            else if (ea is IExtendedAttributeDateDO)
            {
                return ((IExtendedAttributeDateDO)ea).AttributeDate.ToString();
            }
            else if (ea is IExtendedAttributeQualitativeDO)
            {
                if (option == "bool")
                {
                    return ((IExtendedAttributeQualitativeDO)ea).Qualities.Values.Count > 0 ? "Y" : "N";
                }
                foreach (IExtendedAttributeQualityDO q in ((IExtendedAttributeQualitativeDO)ea).Qualities.Values)
                {
                    if (option.Contains("regex:"))
                    {
                        Match match = Regex.Match(q.QualityML.Quality, option.Replace("regex:", string.Empty));
                        if (match.Success) return match.Value;
                    }
                    return q.QualityML.Quality;
                }
            }
            else if (ea is IExtendedAttributeQuantitativeRangeDO)
            {
                switch (option)
                {
                    case "min":
                        return ((IExtendedAttributeQuantitativeRangeDO)ea).Min == -1234567890 ? null : ((IExtendedAttributeQuantitativeRangeDO)ea).Min.ToString();
                    case "max":
                        return ((IExtendedAttributeQuantitativeRangeDO)ea).Max == -1234567890 ? null : ((IExtendedAttributeQuantitativeRangeDO)ea).Max.ToString();
                    case "target":
                        return ((IExtendedAttributeQuantitativeRangeDO)ea).Target == -1234567890 ? null : ((IExtendedAttributeQuantitativeRangeDO)ea).Target.ToString();
                    default:
                        return null;
                }
            }
            return null;
        }

        internal static object GetConfigurationValue(string xpath)
        {
            try
            {
                var prodikaHome = AppPlatformHelper.ApplicationManager.EnvironmentManager.GetEnvironmentVariable("PRODIKA_HOME");
                var fileName = prodikaHome + "/Config/Custom/SIntCrearItemMDMPlugin.config";

                var document = new XPathDocument(fileName);
                var navigator = document.CreateNavigator();

                //Returns object that can be casted to Boolean, Double, String, or XPathNodeIterator depending on evaluation result
                return navigator.Evaluate(xpath);
            }
            catch (Exception e)
            {
                Logger.Log("GetConfigurationValue: " + "xpath= " + xpath + " Exception= " + e.ToString());
                return null;
            }

        }

        internal static T GetService<T>()
        {
            return AppPlatformHelper.ServiceManager.GetServiceByType<T>();
        }
    }
}