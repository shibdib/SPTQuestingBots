﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Aki.Common.Http;
using Newtonsoft.Json;
using SPTQuestingBots.Configuration;
using SPTQuestingBots.Models;

namespace SPTQuestingBots.Controllers
{
    public static class ConfigController
    {
        public static Configuration.ModConfig Config { get; private set; } = null;
        public static Dictionary<string, Configuration.ScavRaidSettingsConfig> ScavRaidSettings = null;
        public static string LoggingPath { get; private set; } = null;

        public static Configuration.ModConfig GetConfig()
        {
            string errorMessage = "!!!!! Cannot retrieve config.json data from the server. The mod will not work properly! !!!!!";
            string json = RequestHandler.GetJson("/QuestingBots/GetConfig");

            if (!TryDeserializeObject(json, errorMessage, out Configuration.ModConfig _config))
            {
                return null;
            }
            Config = _config;

            return Config;
        }

        public static void AdjustPMCConversionChances(float factor, bool verify)
        {
            RequestHandler.GetJson("/QuestingBots/AdjustPMCConversionChances/" + factor + "/" + verify.ToString());
        }

        public static void AdjustPScavChance(float timeRemainingFactor, bool preventPScav)
        {
            double factor = preventPScav ? 0 : InterpolateForFirstCol(Config.AdjustPScavChance.ChanceVsTimeRemainingFraction, timeRemainingFactor);

            RequestHandler.GetJson("/QuestingBots/AdjustPScavChance/" + factor);
        }

        public static void ReportError(string errorMessage)
        {
            RequestHandler.GetJson("/QuestingBots/ReportError/" + errorMessage);
        }

        public static string GetLoggingPath()
        {
            if (LoggingPath != null)
            {
                return LoggingPath;
            }

            string errorMessage = "Cannot retrieve logging path from the server. Falling back to using the current directory.";
            string json = RequestHandler.GetJson("/QuestingBots/GetLoggingPath");

            if (TryDeserializeObject(json, errorMessage, out Configuration.LoggingPath _path))
            {
                LoggingPath = _path.Path;
            }
            else
            {
                LoggingPath = Assembly.GetExecutingAssembly().Location;
            }

            return LoggingPath;
        }

        public static Dictionary<string, Configuration.ScavRaidSettingsConfig> GetScavRaidSettings()
        {
            if (ScavRaidSettings != null)
            {
                return ScavRaidSettings;
            }

            string errorMessage = "Cannot read scav-raid settings.";
            string json = RequestHandler.GetJson("/QuestingBots/GetScavRaidSettings");

            TryDeserializeObject(json, errorMessage, out Configuration.ScavRaidSettingsResponse _response);
            ScavRaidSettings = _response.Maps;

            return ScavRaidSettings;
        }

        public static RawQuestClass[] GetAllQuestTemplates()
        {
            string errorMessage = "Cannot read quest templates.";
            string json = RequestHandler.GetJson("/QuestingBots/GetAllQuestTemplates");

            TryDeserializeObject(json, errorMessage, out Configuration.QuestDataConfig _templates);
            return _templates.Templates;
        }

        public static IEnumerable<Quest> GetCustomQuests(string locationID)
        {
            Quest[] standardQuests = new Quest[0];
            string filename = GetLoggingPath() + "..\\quests\\standard\\" + locationID + ".json";
            if (File.Exists(filename))
            {
                string errorMessage = "Cannot read standard quests for " + locationID;
                try
                {
                    string json = File.ReadAllText(filename);
                    TryDeserializeObject(json, errorMessage, out standardQuests);
                }
                catch (Exception e)
                {
                    LoggingController.LogError(e.Message);
                    LoggingController.LogError(e.StackTrace);
                    LoggingController.LogErrorToServerConsole(errorMessage);
                }
            }

            Quest[] customQuests = new Quest[0];
            filename = GetLoggingPath() + "..\\quests\\custom\\" + locationID + ".json";
            if (File.Exists(filename))
            {
                string errorMessage = "Cannot read custom quests for " + locationID;
                try
                {
                    string json = File.ReadAllText(filename);
                    TryDeserializeObject(json, errorMessage, out customQuests);
                }
                catch (Exception e)
                {
                    LoggingController.LogError(e.Message);
                    LoggingController.LogError(e.StackTrace);
                    LoggingController.LogErrorToServerConsole(errorMessage);
                }
            }

            return standardQuests.Concat(customQuests);
        }

        public static bool TryDeserializeObject<T>(string json, string errorMessage, out T obj)
        {
            try
            {
                if (json.Length == 0)
                {
                    throw new InvalidCastException("Could deserialize an empty string to an object of type " + typeof(T).FullName);
                }

                if (!json.StartsWith("["))
                {
                    ServerResponseError serverResponse = JsonConvert.DeserializeObject<ServerResponseError>(json);
                    if (serverResponse?.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        throw new System.Net.WebException("Could not retrieve configuration settings from the server. Response: " + serverResponse.StatusCode.ToString());
                    }
                }

                obj = JsonConvert.DeserializeObject<T>(json, GClass1340.SerializerSettings);

                return true;
            }
            catch (Exception e)
            {
                LoggingController.LogError(e.Message);
                LoggingController.LogError(e.StackTrace);
                LoggingController.LogErrorToServerConsole(errorMessage);
            }

            obj = default(T);
            if (obj == null)
            {
                obj = (T)Activator.CreateInstance(typeof(T));
            }

            return false;
        }

        public static double InterpolateForFirstCol(double[][] array, double value)
        {
            if (array.Length == 1)
            {
                return array.Last()[1];
            }

            if (value <= array[0][0])
            {
                return array[0][1];
            }

            for (int i = 1; i < array.Length; i++)
            {
                if (array[i][0] >= value)
                {
                    if (array[i][0] - array[i - 1][0] == 0)
                    {
                        return array[i][1];
                    }

                    return array[i - 1][1] + (value - array[i - 1][0]) * (array[i][1] - array[i - 1][1]) / (array[i][0] - array[i - 1][0]);
                }
            }

            return array.Last()[1];
        }
    }
}
