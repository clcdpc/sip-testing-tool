using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sip_console.Configuration
{
    public class CustomSettings : ConfigurationSection
    {
        public static CustomSettings Settings { get; } = ConfigurationManager.GetSection("settings") as CustomSettings;

        [ConfigurationProperty("value_lookup_name", IsRequired = true)]
        public string ValueLookupName
        {
            get { return (string)this["value_lookup_name"]; }
            set { this["value_lookup_name"] = value; }
        }

        [ConfigurationProperty("sip_servers", IsDefaultCollection = false)]
        public ServerCollection SipServers
        {
            get { return (ServerCollection)base["sip_servers"]; }
        }
    }

    [ConfigurationCollection(typeof(ServerElement), AddItemName = "add", CollectionType = ConfigurationElementCollectionType.BasicMap)]
    public class ServerCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new ServerElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((ServerElement)element).Hostname;
        }
    }

    public class ServerElement : ConfigurationElement
    {
        [ConfigurationProperty("hostname", IsRequired = true)]
        public string Hostname
        {
            get { return (string)this["hostname"]; }
            set { this["hostname"] = value; }
        }

        [ConfigurationProperty("port", IsRequired = true)]
        public int Port
        {
            get { return (int)this["port"]; }
            set { this["port"] = value; }
        }

        [ConfigurationProperty("ao", IsRequired = true)]
        public int AO
        {
            get { return (int)this["ao"]; }
            set { this["ao"] = value; }
        }

        [ConfigurationProperty("username", IsRequired = true)]
        public string Username
        {
            get { return (string)this["username"]; }
            set { this["username"] = value; }
        }

        [ConfigurationProperty("password", IsRequired = true)]
        public string Password
        {
            get { return (string)this["password"]; }
            set { this["password"] = value; }
        }
    }
}
