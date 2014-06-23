using System;
using System.Collections;
using System.Net;
using System.Xml;
using Umbraco.Core.Configuration;
using Umbraco.Core.Logging;
using Umbraco.Core.Publishing;
using Umbraco.Core.Sync;

namespace Umbraco.Web.Scheduling
{
    //TODO: No scheduled task (i.e. URL) would be secured, so if people are actually using these each task
    // would need to be a publicly available task (URL) which isn't really very good :(

    internal class ScheduledTasks
    {
        private static readonly Hashtable ScheduledTaskTimes = new Hashtable();
        private static bool _isPublishingRunning = false;

        public void Start(object sender)
        {
            //NOTE: sender will be the umbraco ApplicationContext

            if (_isPublishingRunning) return;

            _isPublishingRunning = true;
            
            try
            {
                ProcessTasks();
            }
            catch (Exception ee)
            {
                LogHelper.Error<ScheduledTasks>("Error executing scheduled task", ee);
            }
            finally
            {
                _isPublishingRunning = false;
            }
        }

        private static void ProcessTasks()
        {


            var scheduledTasks = UmbracoSettings.ScheduledTasks;
            if (scheduledTasks != null)
            {
                var tasks = scheduledTasks.SelectNodes("./task");
                if (tasks == null) return;

                foreach (XmlNode task in tasks)
                {
                    var runTask = false;
                    if (ScheduledTaskTimes.ContainsKey(task.Attributes.GetNamedItem("alias").Value) == false)
                    {
                        runTask = true;
                        ScheduledTaskTimes.Add(task.Attributes.GetNamedItem("alias").Value, DateTime.Now);
                    }
                    // Add 1 second to timespan to compensate for differencies in timer
                    else if (new TimeSpan(
                        DateTime.Now.Ticks - ((DateTime)ScheduledTaskTimes[task.Attributes.GetNamedItem("alias").Value]).Ticks).TotalSeconds + 1
                             >= int.Parse(task.Attributes.GetNamedItem("interval").Value))
                    {
                        runTask = true;
                        ScheduledTaskTimes[task.Attributes.GetNamedItem("alias").Value] = DateTime.Now;
                    }

                    if (runTask)
                    {
                        bool taskResult = GetTaskByHttp(task.Attributes.GetNamedItem("url").Value);
                        if (bool.Parse(task.Attributes.GetNamedItem("log").Value))
                            LogHelper.Info<ScheduledTasks>(string.Format("{0} has been called with response: {1}", task.Attributes.GetNamedItem("alias").Value, taskResult));
                    }
                }
            }
        }

        private static bool GetTaskByHttp(string url)
        {
            var myHttpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            
            try
            {
                using (var response = (HttpWebResponse)myHttpWebRequest.GetResponse())
                {
                    return response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error<ScheduledTasks>("An error occurred calling web task for url: " + url, ex);
            }            

            return false;
        }
    }
}