using McCrypt.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace McCrypt
{
    public class Config
    {
        private static List<string> searchFolders = new List<string>();
        private static List<string> searchModules = new List<string>();
        private static string minecraftWorlds;
        private static string optionsTxts;

        public static string LocalAppdata = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        public static string RoamingAppdata = Environment.GetEnvironmentVariable("APPDATA");
        public static string Temp = Environment.GetEnvironmentVariable("TEMP");

        public static string ApplicationFolder;
        public static string MinecraftFolder; 
        public static string CacheFolder;

        public static string PremiumCache;
        public static string ServerPackCache;
        public static string RealmsPremiumCache;

        public static string KeysDbPath;
        public static string OutFolder;

        public static bool CrackPacks;
        public static bool ZipPacks;
        public static bool MultiThread;
        public static bool DecryptExistingWorlds;

        public static string UsersFolder;


        public static string[] OptionsTxts
        { 
            get
            {
                return Directory.GetDirectories(UsersFolder).Select(o => optionsTxts.Replace("$USERDIR", o)).Where(o => File.Exists(o)).ToArray();
            } 
        }

        public static string[] WorldsFolders
        {
            get
            {
                return Directory.GetDirectories(UsersFolder).Select(o => minecraftWorlds.Replace("$USERDIR", o)).Where(o => Directory.Exists(o)).ToArray();
            }
        }

        public static string[] SearchFolders
        {
            get
            {
                return searchFolders.ToArray();
            }
        }

        public static string[] SearchModules
        {
            get
            {
                return searchModules.ToArray();
            }
        }

        private static void rebaseSearchFolders()
        {
            searchFolders.Clear();
            searchFolders.Add(ApplicationFolder);
            searchFolders.Add(PremiumCache);
            searchFolders.Add(ServerPackCache);
            searchFolders.Add(RealmsPremiumCache);

            searchModules.Clear();
            searchModules.Add("resource_packs");
            searchModules.Add("skin_packs");
            searchModules.Add("world_templates");
            searchModules.Add("persona");
            searchModules.Add("behavior_packs");
            searchModules.Add("resource");
            searchModules.Add("minecraftWorlds");
        }
        private static void rebaseLocalData()
        {
            PremiumCache = Path.Combine(MinecraftFolder, "premium_cache");
            ServerPackCache = Path.Combine(CacheFolder, "packcache");
            RealmsPremiumCache = Path.Combine(CacheFolder, "premiumcache");
            UsersFolder = Path.Combine(MinecraftFolder, "Users");
            minecraftWorlds = Path.Combine("$USERDIR", "games", "com.mojang", "minecraftWorlds"); 
            optionsTxts = Path.Combine("$USERDIR", "games", "com.mojang", "minecraftpe", "options.txt");

            rebaseSearchFolders();
        }
        private static void rebaseAll()
        {
            MinecraftFolder = Path.Combine(RoamingAppdata, "Minecraft Bedrock");
            CacheFolder = Path.Combine(Temp, "minecraftpe");

            rebaseLocalData();
        }
        private static string resolve(string str)
        {
            str = str.Trim();
            str = str.Replace("$LOCALAPPDATA", LocalAppdata);
            str = str.Replace("$APPDATA", RoamingAppdata);
            str = str.Replace("$TEMP", Temp);

            str = str.Replace("$MCDIR", MinecraftFolder);
            str = str.Replace("$CACHEDIR", CacheFolder);
            str = str.Replace("$EXECDIR", ApplicationFolder);
            str = str.Replace("$USERSDIR", UsersFolder);


            str = str.Replace("$PREMIUMCACHE", PremiumCache);
            str = str.Replace("$SERVERPACKCACHE", ServerPackCache);
            str = str.Replace("$REALMSPREMIUMCACHE", RealmsPremiumCache);


            str = str.Replace("$OUTFOLDER", OutFolder);
            return str;
        }

        public static void Init()
        {
            CrackPacks = true;
            ZipPacks = false;

            ApplicationFolder = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            KeysDbPath = Path.Combine(ApplicationFolder, "keys.db");
            MinecraftFolder = Path.Combine(RoamingAppdata, "Minecraft Bedrock");
            CacheFolder = Path.Combine(Temp, "minecraftpe");

            rebaseAll();
        }

        public static void ReadConfig(string configFile)
        {
            if (File.Exists(configFile))
            {
                string[] configLines = File.ReadAllLines(configFile);
                foreach(string line in configLines)
                {
                    if (line.Trim().StartsWith("#"))
                        continue;

                    if (!line.Contains(":"))
                        continue;

                    string[] keyvalpair = line.Trim().Split(':');
                    
                    if (keyvalpair.Length < 2)
                        continue;

                    switch(keyvalpair[0])
                    {
                        case "MinecraftFolder":
                            MinecraftFolder = resolve(keyvalpair[1]);
                            rebaseAll();
                            break;

                        case "CacheFolder":
                            CacheFolder = resolve(keyvalpair[1]);
                            rebaseLocalData();
                            break;
                        case "UsersFolder":
                            UsersFolder = resolve(keyvalpair[1]);
                            rebaseLocalData();
                            break;

                        case "PremiumCache":
                            PremiumCache = resolve(keyvalpair[1]);
                            rebaseSearchFolders();
                            break;
                        case "ServerPackCache":
                            ServerPackCache = resolve(keyvalpair[1]);
                            rebaseSearchFolders();
                            break;
                        case "RealmsPremiumCache":
                            RealmsPremiumCache = resolve(keyvalpair[1]);
                            rebaseSearchFolders();
                            break;

                        case "OptionsTxt":
                            optionsTxts = resolve(keyvalpair[1]);
                            break;

                        case "WorldsFolder":
                            minecraftWorlds = resolve(keyvalpair[1]);
                            break;

                        case "OutputFolder":
                            OutFolder = resolve(keyvalpair[1]);
                            break;

                        case "KeysDb":
                            KeysDbPath = resolve(keyvalpair[1]);
                            break;

                        case "AdditionalSearchDir":
                            searchFolders.Add(resolve(keyvalpair[1]));
                            break;
                        case "AdditionalModuleDir":
                            searchModules.Add(resolve(keyvalpair[1]));
                            break;

                        case "CrackThePacks":
                            CrackPacks = (resolve(keyvalpair[1]).ToLower() == "yes");
                            break;
                        case "ZipThePacks":
                            ZipPacks = (resolve(keyvalpair[1]).ToLower() == "yes");
                            break;
                        case "MultiThread":
                            MultiThread = (resolve(keyvalpair[1]).ToLower() == "yes");
                            break;
                        case "DecryptExistingWorlds":
                            DecryptExistingWorlds = (resolve(keyvalpair[1]).ToLower() == "yes");
                            break;
                    }
                }
            }
            else
            {
                File.WriteAllBytes(configFile, Resources.DefaultConfigFile);
                ReadConfig(configFile);
            }
        }

    }
}
