using McCrypt.PackData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace McCrypt.PackData
{
    public class PReader
    {
        private List<PEntry> entries = new List<PEntry>();
        private HashSet<string> hiddenUuids = new HashSet<String>();
        public PEntry[] GetDependancies(PEntry baseentry)
        {
            List<PEntry> dependancyList = new List<PEntry>();
            foreach (PEntry pentry in entries)
            {
                if (baseentry.DependsUUID.Contains<string>(pentry.Uuid))
                    dependancyList.Add(pentry);

                // check sub packs in world templates .. 
                if(pentry.ProductType == "world_templates")
                {
                    if (Directory.Exists(pentry.SubResourcePacks))
                    {
                        foreach (string resourcePack in Directory.GetDirectories(pentry.SubResourcePacks, "*", SearchOption.TopDirectoryOnly))
                        {
                            PEntry subPentry = new PEntry(Path.Combine(pentry.SubResourcePacks, resourcePack));
                            if (baseentry.DependsUUID.Contains<string>(subPentry.Uuid))
                                dependancyList.Add(subPentry);
                        }
                    }
                    if (Directory.Exists(pentry.SubBehaviourPacks))
                    {
                        foreach (string behaviourPack in Directory.GetDirectories(pentry.SubBehaviourPacks, "*", SearchOption.TopDirectoryOnly))
                        {
                            PEntry subPentry = new PEntry(Path.Combine(pentry.SubBehaviourPacks, behaviourPack));
                            if (baseentry.DependsUUID.Contains<string>(subPentry.Uuid))
                                dependancyList.Add(subPentry);
                        }
                    }

                }

            }
            return dependancyList.ToArray();
        }

        public PEntry[] PEntryList
        {
            get
            {
                List<PEntry> publicEntries = new List<PEntry>();
                foreach (PEntry entry in entries)
                {
                    if (!hiddenUuids.Contains(entry.Uuid))
                    {
                        publicEntries.Add(entry);
                    }
                }
                return publicEntries.ToArray();
            }
        }
        public PReader()
        {
            // search premium cache
            foreach (string searchFolder in Config.SearchFolders)
            {
                foreach (string searchModule in Config.SearchModules) 
                {
                    string moduleFolder = Path.Combine(searchFolder, searchModule);

                    if (Directory.Exists(moduleFolder))
                    {
                        foreach (string moduleItem in Directory.GetDirectories(moduleFolder, "*", SearchOption.TopDirectoryOnly))
                        {
                            PEntry entry = new PEntry(moduleItem);
                            if (entry.ProductType == "minecraftWorlds" && !Config.DecryptExistingWorlds) continue;
                            if (entry.ProductType != "minecraftWorlds" && !hiddenUuids.Contains(entry.Uuid)) foreach (string uuid in entry.DependsUUID) hiddenUuids.Add(uuid);
                            if (!entry.IsEncrypted) continue;

                            entries.Add(entry);
                        }
                    }
                }

            }
            if (Config.DecryptExistingWorlds)
            {
                foreach(string worldFolder in Config.WorldsFolders)
                {
                    foreach (string moduleItem in Directory.GetDirectories(worldFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        PEntry entry = new PEntry(moduleItem);

                        if (entry.ProductType != "minecraftWorlds" && !hiddenUuids.Contains(entry.Uuid)) foreach (string uuid in entry.DependsUUID) hiddenUuids.Add(uuid);
                        if (!entry.IsEncrypted) continue;

                        entries.Add(entry);
                    }

                }
            }
        }
    }
}
