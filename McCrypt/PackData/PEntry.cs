using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace McCrypt.PackData
{
    public class PEntry
    {
        
        private string originalPath;
        public String FilePath
        {
            get
            {
                return originalPath;
            }
        }
        public String ManifestPath
        {
            get
            {
                return Path.Combine(originalPath, "manifest.json");
            }
        }
        public String WorldResourcePacksPath
        {
            get
            {
                return Path.Combine(this.FilePath, "world_resource_packs.json");
            }
        }

        public String WorldBehaviourPacksPath
        {
            get
            {
                return Path.Combine(this.FilePath, "world_behavior_packs.json");
            }
        }


        public String SubResourcePacks
        {
            get
            {
                return Path.Combine(this.FilePath, "resource_packs");
            }
        }

        public String SubBehaviourPacks
        {
            get
            {
                return Path.Combine(this.FilePath, "behavior_packs");
            }
        }

        public bool IsEncrypted
        {
            get
            {
                if (this.ProductType != "minecraftWorlds") return true;
                else return Marketplace.IsLevelEncrypted(this.FilePath);
            }
        }

        public bool HasDependancies
        {
            get
            {
                return DependsUUID.Length >= 1;
            }
        }
        public String[] DependsUUID
        {
            get
            {
                if (File.Exists(this.ManifestPath))
                {
                    return Manifest.ReadDependancyUuids(this.ManifestPath);
                }
                else if(this.Type == "minecraftWorlds")
                {
                    List<String> uuids = new List<String>();
                    if(File.Exists(WorldResourcePacksPath)) uuids.AddRange(Manifest.ReadWorldPackList(WorldResourcePacksPath));
                    if(File.Exists(WorldBehaviourPacksPath)) uuids.AddRange(Manifest.ReadWorldPackList(WorldBehaviourPacksPath));
                    return uuids.ToArray();
                }
                else
                {
                    return new String[0] { };
                }
            }
        }

        public String Name
        {
            get
            {
                if (File.Exists(this.ManifestPath))
                    return Manifest.ReadName(this.ManifestPath);
                else if (File.Exists(Path.Combine(FilePath, "levelname.txt")))
                    return File.ReadAllText(Path.Combine(FilePath, "levelname.txt"));
                else
                    return "Untitled";
            }
        }
        public String Type {
            get
            {
                if(File.Exists(this.ManifestPath))
                    return Manifest.ReadType(this.ManifestPath);
                else if (File.Exists(Path.Combine(FilePath, "levelname.txt")))
                    return "minecraftWorlds";
                else
                    return Path.GetFileName(Path.GetDirectoryName(this.FilePath));
            }
        }

        public String ProductType
        {
            get
            {
                if (File.Exists(this.ManifestPath))
                {
                    string ptype = Manifest.ReadProductType(this.ManifestPath);
                    if (ptype == null)
                    {
                        string type = Manifest.ReadType(this.ManifestPath);
                        switch (type)
                        {
                            case "resources":
                                return "resource_packs";
                            case "skin_pack":
                                return "skin_packs";
                            case "world_template":
                                return "world_templates";
                            case "data":
                                return "behaviour_packs";
                            case "persona_piece":
                                return "persona";
                        }
                    }
                    return ptype;
                }
                else if (File.Exists(Path.Combine(FilePath, "levelname.txt")))
                {
                    return "minecraftWorlds";
                }
                else if (this.HasDependancies)
                {
                    return "addon";
                }
                else
                {
                    return Path.GetFileName(Path.GetDirectoryName(this.FilePath));
                }
            }
        }
        public String Uuid
        {
            get
            {
                if(File.Exists(this.ManifestPath))
                {
                    return Manifest.ReadUUID(this.ManifestPath);
                }
                else
                {
                    return new Guid().ToString();
                }
            }
        }

        public PEntry(string path)
        {
            this.originalPath = path;
        }
    }
}
