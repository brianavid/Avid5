﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Web;
using System.Runtime.InteropServices;
using NLog;

/// <summary>
/// A class of configuration values helds in a manually edited XML file
/// </summary>
public static class Config
{
    static Logger logger = LogManager.GetCurrentClassLogger();
    public static string ContentRootPath { get; private set; }

    static IHostApplicationLifetime _appLifetime;

    public static void Initialize(IHostApplicationLifetime appLifetime, string contentRootPath, string directoryPath = null)
    {
        _appLifetime = appLifetime;
        ContentRootPath = contentRootPath;
        if (directoryPath != null)
        {
            DirectoryPath = directoryPath;
        }
        else
        {
            DirectoryPath = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "/opt/avid" : @"C:\Avid.Net";
        }

        logger.Info($"DirectoryPath = {DirectoryPath}");
    }

    public static string DirectoryPath { get; private set; }

    public static string FilePath(string fileName)
    {
        return System.IO.Path.Combine(DirectoryPath, fileName);
    }

    static XDocument doc = null;

    /// <summary>
    /// The XML document
    /// </summary>
    static XDocument Doc
    {
        get
        {
            if (doc == null)
            {
                var configPath = FilePath("Avid5Config.xml");
                logger.Info($"Load {configPath}");
                doc = XDocument.Load(configPath);
            }
            return doc;
        }
    }

    /// <summary>
    /// The Receiver's IP address
    /// </summary>
    public static string ReceiverAddress
    {
        get
        {
            XElement elAddr = Doc.Root.Element("ReceiverAddress");
            return elAddr == null ? null : elAddr.Value;
        }
    }

    /// <summary>
    /// The Roku box's IP address
    /// </summary>
    public static string RokuAddress
    {
        get
        {
            XElement elAddr = Doc.Root.Element("RokuAddress");
            return elAddr == null ? null : elAddr.Value;
        }
    }

    /// <summary>
    /// The path to the directory in which JRMC recorded TV programmes are stored along with their sidecar XML files
    /// </summary>
    public static string RecordingsPath
    {
        get
        {
            XElement elAddr = Doc.Root.Element("Recordings");
            return elAddr == null ? null : elAddr.Value;
        }
    }

    /// <summary>
    /// The path to the directory in which old-system (DVBViewer)recorded TV programmes are stored
    /// </summary>
    public static string OldRecordingsPath
    {
        get
        {
            XElement elAddr = Doc.Root.Element("OldRecordings");
            return elAddr == null ? null : elAddr.Value;
        }
    }

    /// <summary>
    /// The path to the directory in which ripped DVDs are stored
    /// </summary>
    public static string DvdPath
    {
        get
        {
            XElement elAddr = Doc.Root.Element("DVD");
            return elAddr == null ? null : elAddr.Value;
        }
    }

    /// <summary>
    /// The path to the directory in which Video Files are stored
    /// </summary>
    public static string VideoPath
    {
        get
        {
            XElement elAddr = Doc.Root.Element("Video");
            return elAddr == null ? null : elAddr.Value;
        }
    }

	/// <summary>
	/// The local Spotify market
	/// </summary>
	public static string SpotifyMarket
	{
		get
		{
			XElement elAddr = Doc.Root.Element("SpotifyMarket");
			return elAddr == null ? null : elAddr.Value;
		}
	}

	/// <summary>
	/// The (optional) path to the CEC-client program for direct use within Avid, by-passing Avid-CEC
	/// </summary>
	public static string CECClientPath
	{
		get
		{
			XElement elAddr = Doc.Root.Element("CECClientPath");
			return elAddr == null ? null : elAddr.Value;
		}
	}

	public static void SaveValue(string name, string value)
    {
        try
        {
            System.IO.File.WriteAllText(FilePath(name) + ".dat", value);
        } catch 
        { 
        }
    }

    public static string ReadValue(string name)
    {
        try
        {
            return System.IO.File.ReadAllText(FilePath(name) + ".dat").Trim();
        }
        catch
        {
            return null;
        }
    }

    public static bool Restart { get; private set; }

	public static void StopApplication(bool restart = true)
	{
        Restart = restart;
		_appLifetime.StopApplication();
	}
}