using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace McCrypt
{
    public class Manifest
    {
        private struct signatureBlock
        {
            public string hash;
            public string path;
        }

        public static string SignManifestString(string manifestJson, string setPath)
        {
            signatureBlock signBlock = new signatureBlock();
            signBlock.path = setPath;
            signBlock.hash = Convert.ToBase64String(Crypto.Sha256(Encoding.UTF8.GetBytes(manifestJson)));

            List<signatureBlock> signatureData = new List<signatureBlock>();
            signatureData.Add(signBlock);

            string signatureJson = JsonConvert.SerializeObject(signatureData);
            return signatureJson;
        }

        public static void SignManifest(string basePath)
        {
            string manifestPath = Path.Combine(basePath, "manifest.json");

            signatureBlock signBlock = new signatureBlock();
            signBlock.path = manifestPath.Remove(0, basePath.Length + 1);
            signBlock.hash = Convert.ToBase64String(Crypto.Sha256(File.ReadAllBytes(manifestPath)));

            List<signatureBlock> signatureData = new List<signatureBlock>();
            signatureData.Add(signBlock);

            string signatureJson = JsonConvert.SerializeObject(signatureData);
            string signaturesJsonFile = Path.Combine(basePath, "signatures.json");
            File.WriteAllText(signaturesJsonFile, signatureJson);
        }

        public static string ReadType(string manifestFile)
        {
            string manifestStr = File.ReadAllText(manifestFile);
            dynamic manifestData = JsonConvert.DeserializeObject(manifestStr);
            if(manifestData.modules != null)
            {
                if(manifestData.modules.Count >= 1)
                {
                    return manifestData.modules[0].type;
                }
            }
            return Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(manifestFile)));
        }
        public static string ReadProductType(string manifestFile)
        {
            string manifestStr = File.ReadAllText(manifestFile);
            dynamic manifestData = JsonConvert.DeserializeObject(manifestStr);
            if (manifestData.metadata != null)
            {
                if (manifestData.metadata.product_type != null)
                {
                    return manifestData.metadata.product_type;
                }
            }
            return null;
        }

        public static string[] ReadWorldPackList(string packListFile)
        {
            List<string> depUuidList = new List<string>();
            string packStr = File.ReadAllText(packListFile);
            dynamic packListData = JsonConvert.DeserializeObject(packStr);

            for (int i = 0; i < packListData.Count; i++)
            {
                dynamic packData = packListData[i];
                if(packData.pack_id != null) depUuidList.Add(packData.pack_id.ToString());
            }

            return depUuidList.ToArray();
        }
        public static string[] ReadDependancyUuids(string manifestFile)
        {
            List<string> depUuidList = new List<string>();
            string manifestStr = File.ReadAllText(manifestFile);
            dynamic manifestData = JsonConvert.DeserializeObject(manifestStr);
            if (manifestData.dependencies != null)
            {
                for(int i = 0; i < manifestData.dependencies.Count; i++)
                {
                    dynamic manifestDependancy = manifestData.dependencies[i];
                    if(manifestDependancy.uuid != null)
                    {
                        depUuidList.Add(manifestDependancy.uuid.ToString());
                    }
                }
            }
            return depUuidList.ToArray();
        }
        public static string ReadName(string manifestFile)
        {
            string defaultName = Path.GetFileName(Path.GetDirectoryName(manifestFile));
            if (!File.Exists(manifestFile))
                return Utils.TrimName(defaultName);

            string manifestStr = File.ReadAllText(manifestFile);
            dynamic manifestData = JsonConvert.DeserializeObject(manifestStr);
            if (manifestData.header != null)
            {
                if (manifestData.header.name != null)
                {
                    string name = manifestData.header.name;
                    string englishLanguageFile = Path.Combine(Path.GetDirectoryName(manifestFile), "texts", "en_US.lang");

                    if (File.Exists(englishLanguageFile))
                    {
                        string[] lines = File.ReadAllLines(englishLanguageFile);
                        foreach (string line in lines)
                        {
                            if (!line.Contains('='))
                                continue;

                            string[] values = line.Split('=');

                            // How tf does this work??!!

                            if (values.Length <= 0)
                                continue;

                            if (values[0] == name)
                                return Utils.TrimName(values[1]);

                            if (values[0] == "pack.name")
                                return Utils.TrimName(values[1]);

                            if (values[0].Contains('.'))
                            {
                                string[] values2 = values[0].Split('.');
                                if (values2.Length <= 0)
                                    return Utils.TrimName(defaultName);

                                if (values[0].Split('.').Last() == name)
                                    return Utils.TrimName(values[1]);

                                if (values2[0] == "skinpack")
                                    return Utils.TrimName(values[1]);

                                if (values2[0] == "persona")
                                    return Utils.TrimName(values[1]);

                            }

                            if (values[0].Contains(name))
                                return Utils.TrimName(values[1]);
                        }
                        if (name.Contains("."))
                            return Utils.TrimName(defaultName);
                        else
                            return Utils.TrimName(name);
                    }
                    else
                        return Utils.TrimName(defaultName);

                }
            }

            return Utils.TrimName(defaultName);
        }
        public static string ReadUUID(string manifestPath)
        {
            dynamic manifest = JsonConvert.DeserializeObject(File.ReadAllText(manifestPath));
            return manifest.header.uuid.ToString();
        }

        public static void ChangeUUID(string manifestPath, string newUUID)
        {
            dynamic manifest = JsonConvert.DeserializeObject(File.ReadAllText(manifestPath));
            manifest.header.uuid = newUUID;
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));
        }
    }
}
