using System.Web.Routing;
using System.Web.Mvc;
using System.Collections.Generic;
using System.IO;
using System;
using System.Xml.Linq;
using System.Linq;

namespace StaticWebEpiserverPlugin.Routing
{
    public class StaticWebRouting
    {
        /// <summary>
        /// Gets all routes for all monitored pages being converted to static versions
        /// </summary>
        public static Dictionary<string, RouteBase> Routes { get; protected set; }

        public const string ConfigName = "StaticWeb.Routes.config";

        static StaticWebRouting()
        {
            Routes = new Dictionary<string, RouteBase>();
        }

        public static void Add(string pageUrl)
        {
            if (string.IsNullOrEmpty(pageUrl))
            {
                return;
            }

            pageUrl = pageUrl.Replace("\\", "/");
            pageUrl = pageUrl.Trim(new[] { '/' });

            RouteCollection tmpRoutes = new RouteCollection();
            try
            {
                tmpRoutes.IgnoreRoute(pageUrl);
            }
            catch (System.Exception)
            {
                // Ignore error

            }
            var route = tmpRoutes[0];

            // track our route
            try
            {
                Routes.Add(pageUrl, route);
            }
            catch (System.Exception)
            {
                // We are trying to add a route already existing, ignore this.
                return;
            }

            try
            {
                // Add route to be global route table
                RouteTable.Routes.Insert(0, route);
            }
            catch (System.Exception)
            {
                // Ignore error
            }
        }
        public static void Remove(string pageUrl)
        {
            if (string.IsNullOrEmpty(pageUrl))
            {
                return;
            }

            pageUrl = pageUrl.Replace("\\", "/");
            pageUrl = pageUrl.Trim(new[] { '/' });

            RouteBase route = null;
            try
            {
                route = Routes[pageUrl];
                Routes.Remove(pageUrl);
                RouteTable.Routes.Remove(route);
            }
            catch (System.Exception) { }
        }

        public static void SaveRoutes()
        {
            string fileName = null;
            try
            {
                var directory = new DirectoryInfo(AppContext.BaseDirectory);
                if (!directory.Exists)
                {
                    return;
                }

                fileName = directory.FullName + Path.DirectorySeparatorChar + ConfigName;

                var routes = Routes.Keys.ToList();
                XDocument document =
                      new XDocument(
                        new XElement("routes",
                            routes.Select(x => new XElement("route", new XAttribute("value", x)))
                          )
                      );

                document.Save(fileName);
            }
            catch (Exception)
            {
            }
        }

        public static void LoadRoutes()
        {
            // Register routes from stored version
            string configFileName = null;
            try
            {
                var directory = new DirectoryInfo(AppContext.BaseDirectory);
                if (!directory.Exists)
                {
                    return;
                }
                var directoryPath = directory.FullName + Path.DirectorySeparatorChar;

                configFileName = directoryPath + ConfigName;

                if (!File.Exists(configFileName))
                {
                    return;
                }

                var content = File.ReadAllText(configFileName, System.Text.Encoding.UTF8);

                XDocument document = XDocument.Parse(content);
                var routes = from datas in document.Root.Elements("route")
                            select (string)datas.Attribute("value");

                var configIsDirty = false;

                foreach (string route in routes)
                {
                    var routeFileName = directoryPath + route.Replace("/", "\\") + Path.DirectorySeparatorChar + "index.html";
                    if (File.Exists(routeFileName))
                    {
                        Add(route);
                    }
                    else
                    {
                        configIsDirty = true;
                    }
                }

                // IF one or more routes are no longer valid for some reason, adjust config file and store a backup
                if (configIsDirty)
                {
                    // We will save a backup with hash in filename to allow multiple backups if needed
                    File.Copy(configFileName, configFileName + ".bak");
                    SaveRoutes();
                }
            }
            catch (Exception)
            {
                // do nothing if we can't read this
            }
        }
    }
}