using StaticWebEpiserverPlugin.Models;
using System;
using System.Configuration;
using System.IO;

namespace StaticWebEpiserverPlugin.Configuration
{
    public class SiteConfigurationElement : ConfigurationElement
    {
        protected bool? isValid = null;
        protected string _name = null;
        protected string _url = null;
        protected string _outputPath = null;
        protected string _resourceFolder = null;

        protected bool IsValid()
        {
            if (isValid.HasValue)
            {
                return isValid.Value;
            }

            isValid = (ValidateInputUrl()
                && ValidateOutputFolder()
            && ValidateResourceFolder()
            && ValidateMandatoryFields());

            return isValid.Value;
        }

        private bool ValidateMandatoryFields()
        {
            return !string.IsNullOrEmpty(OutputPath + ResourceFolder + Url);
        }

        public SiteConfigurationElement() { }

        [ConfigurationProperty("enabled", DefaultValue = true, IsRequired = false)]
        public bool Enabled
        {
            get
            {
                bool? config = (bool?)this["enabled"];
                if (config ?? true)
                {
                    return IsValid();
                }
                return false;
            }
        }

        [ConfigurationProperty("name", DefaultValue = "", IsKey = true, IsRequired = true)]
        public string Name
        {
            get { return _name ?? (string)this["name"]; }
            set { _name = value; }
        }

        [ConfigurationProperty("url", DefaultValue = "", IsRequired = true)]
        public string Url
        {
            get { return _url ?? (string)this["url"]; }
            set { _url = value; }
        }

        [ConfigurationProperty("outputPath", DefaultValue = "", IsRequired = true)]
        public string OutputPath
        {
            get { return _outputPath ?? (string)this["outputPath"]; }
            set { _outputPath = value; }
        }

        [ConfigurationProperty("resourceFolder", DefaultValue = "", IsRequired = false)]
        public string ResourceFolder
        {
            get { return _resourceFolder ?? (string)this["resourceFolder"]; }
            set { _resourceFolder = value; }
        }

        [ConfigurationProperty("useRouting", DefaultValue = false, IsRequired = false)]
        public bool UseRouting
        {
            get
            {
                bool? config = (bool?)this["useRouting"];
                return config ?? false;
            }
            set { this["useRouting"] = value; }
        }

        [ConfigurationProperty("removeObsoleteResources", DefaultValue = false, IsRequired = false)]
        public bool RemoveObsoleteResources
        {
            get
            {
                bool? config = (bool?)this["removeObsoleteResources"];
                return config ?? false;
            }
            set { this["removeObsoleteResources"] = value; }
        }

        [ConfigurationProperty("removeObsoletePages", DefaultValue = false, IsRequired = false)]
        public bool RemoveObsoletePages
        {
            get
            {
                bool? config = (bool?)this["removeObsoletePages"];
                return config ?? false;
            }
            set { this["removeObsoletePages"] = value; }
        }

        [ConfigurationProperty("maxDegreeOfParallelismForScheduledJob", DefaultValue = 1, IsRequired = false)]
        public int MaxDegreeOfParallelismForScheduledJob {
            get
            {
                int? config = (int?)this["maxDegreeOfParallelismForScheduledJob"];
                return config ?? 1;
            }
            set { this["maxDegreeOfParallelismForScheduledJob"] = value; }

        }

        [ConfigurationProperty("useTemporaryAttribute", DefaultValue = null, IsRequired = false)]
        public bool? UseTemporaryAttribute
        {
            get
            {
                bool? config = (bool?)this["useTemporaryAttribute"];
                return config;
            }
            set { this["useTemporaryAttribute"] = value; }

        }

        [ConfigurationProperty("generateOrderForScheduledJob", DefaultValue = GenerateOrderForScheduledJob.Default, IsRequired = false)]
        public GenerateOrderForScheduledJob GenerateOrderForScheduledJob
        {
            get { return (GenerateOrderForScheduledJob)this["generateOrderForScheduledJob"]; }
            set { this["generateOrderForScheduledJob"] = value; }
        }


        protected bool ValidateInputUrl()
        {
            if (string.IsNullOrEmpty(Url))
            {
                //throw new ArgumentException("Missing value for 'StaticWeb:InputUrl'", "StaticWeb:InputUrl");
                return false;
            }

            try
            {
                // Try to parse as Uri to validate value
                var testUrl = new Uri(Url);
            }
            catch (Exception)
            {
                //throw new ArgumentException("Invalid value for 'StaticWeb:InputUrl'", "StaticWeb:InputUrl");
                return false;
            }

            if (Url.EndsWith("/"))
            {
                Url = Url.TrimEnd('/');
            }
            return true;
        }

        protected bool ValidateOutputFolder()
        {
            if (string.IsNullOrEmpty(OutputPath))
            {
                //throw new ArgumentException("Missing value for 'StaticWeb:OutputFolder'", "StaticWeb:OutputFolder");
                return false;
            }

            if (!OutputPath.EndsWith("\\"))
            {
                // Make sure it can be combined with _resourcePath
                OutputPath += "\\";
            }


            if (!Directory.Exists(OutputPath))
            {
                //throw new ArgumentException("Folder specified in 'StaticWeb:OutputFolder' doesn't exist", "StaticWeb:OutputFolder");
                return false;
            }

            try
            {
                var directory = new DirectoryInfo(OutputPath);
                var directoryName = directory.FullName;

                var fileName = directoryName + Path.DirectorySeparatorChar + ".staticweb-access-test";

                // verifying write access
                File.WriteAllText(fileName, "Verifying write access to folder");
                // verify modify access
                File.WriteAllText(fileName, "Verifying modify access to folder");
                // verify delete access
                File.Delete(fileName);

            }
            catch (UnauthorizedAccessException)
            {
                //throw new ArgumentException("Not sufficient permissions for folder specified in 'StaticWeb:OutputFolder'. Require read, write and modify permissions", "StaticWeb:OutputFolder");
                return false;
            }
            catch (Exception)
            {
                //throw new ArgumentException("Unknown error when testing write, edit and remove access to folder specified in 'StaticWeb:OutputFolder'", "StaticWeb:OutputFolder");
                return false;
            }
            return true;
        }

        protected bool ValidateResourceFolder()
        {
            if (string.IsNullOrEmpty(ResourceFolder))
            {
                ResourceFolder = "";
            }

            if (!Directory.Exists(OutputPath + ResourceFolder))
            {
                Directory.CreateDirectory(OutputPath + ResourceFolder);
            }

            try
            {
                var directory = new DirectoryInfo(OutputPath + ResourceFolder);
                var directoryName = directory.FullName;

                // Check if it looks like we are in a EpiServer application
                // - if TRUE, throw exception and tell them this is not allowed (resource folder is required to be a subfolder)
                // - if FALSE, continue as usual.
                var appDirectory = new DirectoryInfo(Environment.CurrentDirectory);
                if (directoryName == appDirectory.FullName)
                {
                    //throw new ArgumentException($"'StaticWeb:ResourceFolder' can't be the application folder (read: {appDirectory.FullName}). You can change this by setting 'StaticWeb:ResourceFolder", "StaticWeb:ResourceFolder");
                    return false;
                }
                appDirectory = new DirectoryInfo(AppContext.BaseDirectory);
                if (directoryName == appDirectory.FullName)
                {
                    //throw new ArgumentException($"'StaticWeb:ResourceFolder' can't be the application folder (read: {appDirectory.FullName}). You can change this by setting 'StaticWeb:ResourceFolder", "StaticWeb:ResourceFolder");
                    return false;
                }

                var fileName = directoryName + Path.DirectorySeparatorChar + ".staticweb-access-test";

                // verifying write access
                File.WriteAllText(fileName, "Verifying write access to folder");
                // verify modify access
                File.WriteAllText(fileName, "Verifying modify access to folder");
                // verify delete access
                File.Delete(fileName);

            }
            catch (UnauthorizedAccessException)
            {
                //throw new ArgumentException("Not sufficient permissions for folder specified in 'StaticWeb:ResourceFolder'. Require read, write and modify permissions", "StaticWeb:ResourceFolder");
                return false;
            }
            catch (Exception)
            {
                //throw new ArgumentException("Unknown error when testing write, edit and remove access to folder specified in 'StaticWeb:ResourceFolder'", "StaticWeb:ResourceFolder");
                return false;
            }
            return true;
        }
    }
}
