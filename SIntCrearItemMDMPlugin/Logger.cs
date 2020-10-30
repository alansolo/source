using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Xeno.Prodika.Application;
using Xeno.Prodika.Common.Logging;

namespace SIntCrearItemMDMPlugin
{
    internal class Logger
    {
        internal static void Log(string logEntry)
        {
            //var prodikaHome = AppPlatformHelper.ApplicationManager.EnvironmentManager.GetEnvironmentVariable("PRODIKA_HOME");
            //var logFolder = prodikaHome + "/logs/";
            //Directory.CreateDirectory(logFolder);
            //FileLogger logger = new FileLogger(logFolder + "SIntCrearItemMDMPlugin.log", true);
            //try
            //{
            //    using (logger)
            //    {
            //        logger.WriteEntryInfo(logEntry);
            //    }
            //}
            //catch (Exception e)
            //{
            //    throw new Exception(e.Message);
            //}
        }
    }
}