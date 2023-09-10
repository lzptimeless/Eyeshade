﻿using Eyeshade.Log;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Eyeshade.FuncModule
{
    public class EyeshadeUserConfig
    {
        private readonly string ConfigFileName = "user-config.xml";
        private readonly ILogWrapper? _logger;
        private readonly string _configFilePath;

        public EyeshadeUserConfig(ILogWrapper? logger)
        {
            _logger = logger;
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

            Load();
        }

        public TimeSpan WorkTime { get; set; } = TimeSpan.FromMinutes(45);
        public TimeSpan RestingTime { get; set; } = TimeSpan.FromMinutes(4);
        public int RingerVolume { get; set; } = 100;
        public EyeshadeTrayPopupShowModes TrayPopupShowMode { get; set; }
        public EyeshadeTrayPopupCloseModes TrayPopupCloseMode { get; set; }

        public void Load()
        {
            try
            {
                XDocument xdoc = XDocument.Load(_configFilePath);
                if (xdoc.Root == null) return;

                foreach (var itemNode in xdoc.Root.Elements())
                {
                    switch (itemNode.Name.LocalName)
                    {
                        case nameof(WorkTime):
                            {
                                if (TimeSpan.TryParse(itemNode.Value, out TimeSpan value) && value.TotalMinutes >= 1)
                                {
                                    WorkTime = value;
                                }
                            }
                            break;
                        case nameof(RestingTime):
                            {
                                if (TimeSpan.TryParse(itemNode.Value, out TimeSpan value) && value.TotalMinutes >= 1)
                                {
                                    RestingTime = value;
                                }
                            }
                            break;
                        case nameof(RingerVolume):
                            {
                                if (int.TryParse(itemNode.Value, out int value))
                                {
                                    RingerVolume = value;
                                }
                            }
                            break;
                        case nameof(TrayPopupShowMode):
                            {
                                if (Enum.TryParse(itemNode.Value, out EyeshadeTrayPopupShowModes value))
                                {
                                    TrayPopupShowMode = value;
                                }
                            }
                            break;
                        case nameof(TrayPopupCloseMode):
                            {
                                if (Enum.TryParse(itemNode.Value, out EyeshadeTrayPopupCloseModes value))
                                {
                                    TrayPopupCloseMode = value;
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn(ex, $"Load {ConfigFileName} failed.");
            }
        }

        public void Save()
        {
            try
            {
                XDocument xdoc = new XDocument();
                xdoc.Add(new XElement("UserConfig",
                    new XElement(nameof(WorkTime), WorkTime.ToString()),
                    new XElement(nameof(RestingTime), RestingTime.ToString()),
                    new XElement(nameof(RingerVolume), RingerVolume.ToString()),
                    new XElement(nameof(TrayPopupShowMode), TrayPopupShowMode.ToString()),
                    new XElement(nameof(TrayPopupCloseMode), TrayPopupCloseMode.ToString())
                ));

                xdoc.Save(_configFilePath);
            }
            catch (Exception ex)
            {
                _logger?.Warn(ex, $"Save {ConfigFileName} failed.");
            }
        }
    }
}