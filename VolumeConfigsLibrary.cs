﻿//   VolumeConfigsLibrary.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System.Collections.Generic;
using System.Linq;

namespace AT_Utils
{
    public static class VolumeConfigsLibrary
    {
        public const string USER_FILE = "VolumeConfigs.user";

        private static SortedList<string, VolumeConfiguration> presets;

        private static SortedList<string, VolumeConfiguration> user_configs;
        public static string UserFile => CustomConfig.GameDataFolder("ConfigurableContainers", USER_FILE);

        /// <summary>
        ///     The library of tank configurations provided by mods.
        /// </summary>
        public static SortedList<string, VolumeConfiguration> PresetConfigs
        {
            get
            {
                if(presets != null)
                    return presets;
                var nodes = GameDatabase.Instance.GetConfigNodes(VolumeConfiguration.NODE_NAME);
                presets = new SortedList<string, VolumeConfiguration>(nodes.Length);
                foreach(var n in nodes)
                {
                    Utils.Debug("Parsing preset tank configuration:\n{}", n);
                    var cfg = ConfigNodeObject.FromConfig<VolumeConfiguration>(n);
                    if(!cfg.Valid)
                    {
                        var msg = $"ConfigurableContainers: configuration \"{cfg.name}\" is INVALID.";
                        Utils.Message(6, msg);
                        Utils.Error(msg);
                        continue;
                    }
                    try
                    {
                        presets.Add(cfg.name, cfg);
                    }
                    catch
                    {
                        Utils.Warning("SwitchableTankType: ignoring duplicate configuration of '{}' configuration. "
                                      + "Use ModuleManager to change the existing one.",
                            cfg.name);
                    }
                }
                Utils.Debug("Parsed presets: {}", presets);
                return presets;
            }
        }

        /// <summary>
        ///     The library of tank configurations saved by the user.
        /// </summary>
        /// <value>The user configs.</value>
        public static SortedList<string, VolumeConfiguration> UserConfigs
        {
            get
            {
                if(user_configs != null)
                    return user_configs;
                user_configs = new SortedList<string, VolumeConfiguration>();
                var node = CustomConfig.LoadNode(UserFile);
                Utils.Debug("Loading user configurations from:\n{}\n{}", UserFile, node);
                if(node == null)
                    return user_configs;
                foreach(var n in node.GetNodes(VolumeConfiguration.NODE_NAME))
                {
                    var cfg = ConfigNodeObject.FromConfig<VolumeConfiguration>(n);
                    if(!cfg.Valid)
                    {
                        var msg = $"ConfigurableContainers: configuration \"{cfg.name}\" is INVALID.";
                        Utils.Message(6, msg);
                        Utils.Error(msg);
                        continue;
                    }
                    if(SwitchableTankType.HaveTankType(cfg.name))
                        cfg.name += " [cfg]";
                    if(PresetConfigs.ContainsKey(cfg.name))
                        cfg.name += " [usr]";
                    add_unique(cfg, user_configs);
                }
                Utils.Debug("Parsed user presets: {}", user_configs);
                return user_configs;
            }
        }

        private static void add_unique(VolumeConfiguration cfg, IDictionary<string, VolumeConfiguration> db)
        {
            var index = 1;
            var basename = cfg.name;
            while(db.ContainsKey(cfg.name))
                cfg.name = string.Concat(basename, " ", index++);
            db.Add(cfg.name, cfg);
        }

        private static void save_user_configs()
        {
            var node = new ConfigNode();
            UserConfigs.ForEach(c => c.Value.SaveInto(node));
            if(CustomConfig.SaveNode(node, UserFile))
                return;
            Utils.Message("Unable to save tank configurations.");
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
            else
                UserConfigs.Add(cfg.name, cfg);
            save_user_configs();
        }

        public static bool RemoveConfig(string cfg_name)
        {
            if(!UserConfigs.Remove(cfg_name))
                return false;
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
                names.AddRange(
                    from cfg in PresetConfigs
                    where cfg.Value.ContainsTypes(exclude)
                    select cfg.Value.name);
                names.AddRange(
                    from cfg in UserConfigs
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
            if(string.IsNullOrEmpty(name))
                return null;
            if(PresetConfigs.TryGetValue(name, out var cfg))
                return cfg;
            if(UserConfigs.TryGetValue(name, out cfg))
                return cfg;
            return null;
        }

        public static string GetConfigInfo(string name, float volume_conversion = 1)
        {
            var config = GetConfig(name);
            return config == null ? string.Empty : config.Info(volume_conversion);
        }

        public static bool HaveUserConfig(string name)
        {
            return UserConfigs.ContainsKey(name);
        }
    }
}
