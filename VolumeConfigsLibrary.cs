//   VolumeConfigsLibrary.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Linq;
using System.Collections.Generic;
using AT_Utils;

namespace AT_Utils
{
    public class VolumeConfigsLibrary : CustomConfig
    {
        public const string USERFILE = "VolumeConfigs.user";
        public static string UserFile { get { return GameDataFolder("ConfigurableContainers", USERFILE); } }

        private static VolumeConfigsLibrary instance;

        private static VolumeConfigsLibrary Instance 
        { 
            get 
            { 
                if(instance == null) instance = new VolumeConfigsLibrary();
                return instance;
            }
        }

        /// <summary>
        /// The library of tank configurations provided by mods.
        /// </summary>
        public static SortedList<string, VolumeConfiguration> PresetConfigs 
        { 
            get
            {
                if(presets == null)
                {
                    var nodes = GameDatabase.Instance.GetConfigNodes(VolumeConfiguration.NODE_NAME);
                    presets = new SortedList<string, VolumeConfiguration>(nodes.Length);
                    foreach(ConfigNode n in nodes)
                    {
                        #if DEBUG
                        Utils.Log("Parsing preset tank configuration:\n{}", n);
                        #endif
                        var cfg = ConfigNodeObject.FromConfig<VolumeConfiguration>(n);
                        if(!cfg.Valid)
                        {
                            var msg = string.Format("ConfigurableContainers: configuration \"{0}\" is INVALID.", cfg.name);
                            Utils.Message(6, msg);
                            Utils.Log(msg);
                            continue;
                        }
                        try { presets.Add(cfg.name, cfg); }
                        catch
                        { 
                            Utils.Log("SwitchableTankType: ignoring duplicate configuration of '{}' configuration. " +
                                      "Use ModuleManager to change the existing one.", cfg.name); 
                        }
                    }
                }
                return presets;
            }
        }

        private static SortedList<string, VolumeConfiguration> presets;

        /// <summary>
        /// The library of tank configurations saved by the user.
        /// </summary>
        /// <value>The user configs.</value>
        public static SortedList<string, VolumeConfiguration> UserConfigs 
        {
            get
            {
                if(user_configs == null)
                {
                    user_configs = new SortedList<string, VolumeConfiguration>();
                    var node = LoadNode(UserFile);
                    #if DEBUG
                    Utils.Log("Loading user configurations from:\n{}\n{}", UserFile, node);
                    #endif
                    if(node != null)
                    {
                        foreach(var n in node.GetNodes(VolumeConfiguration.NODE_NAME))
                        {
                            var cfg = ConfigNodeObject.FromConfig<VolumeConfiguration>(n);
                            if(!cfg.Valid)
                            {
                                var msg = string.Format("ConfigurableContainers: configuration \"{0}\" is INVALID.", cfg.name);
                                Utils.Message(6, msg);
                                Utils.Log(msg);
                                continue;
                            }
                            else
                            {
                                if(SwitchableTankType.HaveTankType(cfg.name)) cfg.name += " [cfg]";
                                if(PresetConfigs.ContainsKey(cfg.name)) cfg.name += " [usr]";
                                add_unique(cfg, user_configs);
                            }
                        }
                    }
                }
                return user_configs;
            }
        }

        private static SortedList<string, VolumeConfiguration> user_configs;

        private static void add_unique(VolumeConfiguration cfg, IDictionary<string, VolumeConfiguration> db)
        {
            int index = 1;
            var basename = cfg.name;
            while(db.ContainsKey(cfg.name)) 
                cfg.name = string.Concat(basename, " ", index++);
            db.Add(cfg.name, cfg);
        }

        private static bool save_user_configs()
        {
            var node = new ConfigNode();
            UserConfigs.ForEach(c => c.Value.SaveInto(node));
            if(SaveNode(node, UserFile)) return true;
            Utils.Message("Unable to save tank configurations.");
            return false;
        }

        public static void AddConfig(VolumeConfiguration cfg)
        {
            add_unique(cfg, UserConfigs);
            save_user_configs();
        }

        public static void AddOrSave(VolumeConfiguration cfg)
        {
            if(UserConfigs.ContainsKey(cfg.name))
                UserConfigs[cfg.name] = cfg;
            else UserConfigs.Add(cfg.name, cfg);
            save_user_configs();
        }

        public static bool RemoveConfig(string cfg_name)
        { 
            if(!UserConfigs.Remove(cfg_name)) return false;
            save_user_configs();
            return true;
        }

        public static List<string> AllConfigNames(string[] include, string[] exclude)
        {
            var names = new List<string>();
            if(include != null && include.Length > 0)
                exclude = SwitchableTankType.TankTypeNames(null, include).ToArray();
            if(exclude != null && exclude.Length > 0)
            {
                names.AddRange(from cfg in PresetConfigs 
                               where cfg.Value.ContainsTypes(exclude)
                               select cfg.Value.name);
                names.AddRange(from cfg in UserConfigs 
                               where cfg.Value.ContainsTypes(exclude)
                               select cfg.Value.name);
            }
            else 
            {
                names.AddRange(PresetConfigs.Keys);
                names.AddRange(UserConfigs.Keys);
            }
            return names;
        }

        public static VolumeConfiguration GetConfig(string name)
        {
            if(string.IsNullOrEmpty(name)) return null;
            if(PresetConfigs.TryGetValue(name, out var cfg)) return cfg;
            if(UserConfigs.TryGetValue(name, out cfg)) return cfg;
            return null;
        }

        public static bool HaveUserConfig(string name)
        { return UserConfigs.ContainsKey(name); }
    }
}

