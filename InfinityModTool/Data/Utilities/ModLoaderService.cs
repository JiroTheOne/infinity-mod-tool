﻿using Microsoft.Extensions.Configuration;
using LitJson;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Controller;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;

namespace InfinityModTool.Data.Utilities
{
	public class ModLoaderService
	{
#if DEBUG
		const string MOD_PATH = "..\\..\\..\\Mods";
		const string CHARACTER_ID_NAMES = "..\\..\\..\\character_ids.json";
#else
		const string MOD_PATH = "Mods";
		const string CHARACTER_ID_NAMES = "character_ids.json";
#endif

		private class ModPathInfo
		{
			public string executionPath;
			public string modPath;
			public string extractBasePath;
			public string modCachePath;
		}

		public static ListOption[] GetIDNameListOptions()
		{
			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var idNamePath = Path.Combine(executionPath, CHARACTER_ID_NAMES);

			var fileData = File.ReadAllText(idNamePath);
			var idNames = JsonMapper.ToObject<IDNames>(fileData);

			return idNames.CharacterIDs.Select(id => new ListOption(id.ID, id.DisplayName)).ToArray();
		}

		public static BaseModConfiguration[] LoadMods(double currentVersion)
		{
			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var modPath = Path.Combine(executionPath, MOD_PATH);
			var extractBasePath = Path.Combine(Global.APP_DATA_FOLDER, "Temp");
			var modCachePath = Path.Combine(Global.APP_DATA_FOLDER, "ModCache");

			var pathInfo = new ModPathInfo()
			{
				executionPath = executionPath,
				modPath = modPath,
				extractBasePath = extractBasePath,
				modCachePath = modCachePath
			};
			
			if (!Directory.Exists(modPath))
				Directory.CreateDirectory(modPath);

			DeleteAndRecreateFolder(extractBasePath);
			DeleteAndRecreateFolder(modCachePath);

			var mods = new List<BaseModConfiguration>();

			foreach (var file in Directory.GetFiles(modPath))
			{
				var fileInfo = new FileInfo(file);

				if (TryLoadV1Mod(fileInfo, pathInfo, out var modData))
					mods.Add(modData);
			}

			return mods.ToArray();
		}

		private static bool TryLoadV1Mod(FileInfo fileInfo, ModPathInfo pathInfo, out BaseModConfiguration modData)
		{
			modData = null;

			if (fileInfo.Extension == ".zip")
			{
				var extractFolder = Path.Combine(pathInfo.extractBasePath, fileInfo.Name);
				System.IO.Compression.ZipFile.ExtractToDirectory(fileInfo.FullName, extractFolder);

				var configPath = Path.Combine(extractFolder, "config.json");
				
				if (!File.Exists(configPath))
					return false;
				
				var fileData = File.ReadAllText(configPath);
				modData = JsonMapper.ToObject<BaseModConfiguration>(fileData);
				
				if (string.IsNullOrEmpty(modData.ModID) || modData.Version > 1)
					return false;
				
				var destinationPath = Path.Combine(pathInfo.modCachePath, modData.ModID);

				// Delete the directory and re-save cache the mod data
				if (Directory.Exists(destinationPath))
					Directory.Delete(destinationPath, true);
				
				modData = LoadModData(modData, extractFolder);
				Directory.Move(extractFolder, destinationPath);
				
				modData.ModCachePath = destinationPath;

				// Temporary work around since we can't appear to load images from %APPDATA%
				var imageBytes = File.ReadAllBytes(Path.Join(modData.ModCachePath, modData.DisplayImage));
				modData.DisplayImageBase64 = "data:image/png;base64," + Convert.ToBase64String(imageBytes);

				return true;
			}

			return false;
		}

		static BaseModConfiguration LoadModData(BaseModConfiguration configuration, string extractPath)
		{
			switch (configuration.ModCategory)
			{
				case "Character":
					return LoadCharacterModData(configuration, extractPath);
				default:
					return configuration;
			}
		}

		static CharacterModConfiguration LoadCharacterModData(BaseModConfiguration configuration, string extractPath)
		{
			var configPath = Path.Combine(extractPath, "config.json");
			var presentationPath = Path.Combine(extractPath, "presentation.json");

			var modData = JsonMapper.ToObject<CharacterModConfiguration>(File.ReadAllText(configPath));
			var presentationData = JsonMapper.ToObject<CharacterData>(File.ReadAllText(presentationPath));

			modData.PresentationData = presentationData;
			return modData;
		}
		
		static void DeleteAndRecreateFolder(string path)
		{
			// Ensure that we delete this data if it's been left behind
			if (Directory.Exists(path))
				Directory.Delete(path, true);

			Directory.CreateDirectory(path);
		}
	}
}
