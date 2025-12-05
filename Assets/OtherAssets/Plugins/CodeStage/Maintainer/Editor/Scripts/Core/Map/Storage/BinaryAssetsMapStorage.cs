#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Map.Storage
{
	using System;
	using System.IO;
	using EditorCommon.Tools;
	using UnityEditor;
	using UnityEngine;

	internal class BinaryAssetsMapStorage : IAssetsMapStorage
	{
		public AssetsMap Load(string path)
		{
			if (!File.Exists(path))
				return null;

			var fileSize = new FileInfo(path).Length;
			if (fileSize > 500000)
				EditorUtility.DisplayProgressBar("Loading Assets Map", "Please wait...", 0);

			AssetsMap result = null;

			try
			{
				using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
				using (var reader = new BinaryReader(stream))
				{
					result = AssetsMapBinarySerializer.Read(reader, out var versionMismatch);
					if (versionMismatch)
					{
						result = null;
					}
				}
			}
			catch (Exception)
			{
				Debug.Log(Maintainer.ConstructLog("Couldn't read assets map (more likely you've updated Maintainer recently).\nThis message is harmless unless repeating on every Maintainer run."));
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}

			return result;
		}

		public void Save(string path, AssetsMap map)
		{
			if (map.assets.Count > 10000)
			{
				EditorUtility.DisplayProgressBar("Saving Assets Map", "Please wait...", 0);
			}

			try
			{
				using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
				using (var writer = new BinaryWriter(stream))
				{
					AssetsMapBinarySerializer.Write(writer, map);
				}
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}

		public void Delete(string path)
		{
			CSFileTools.DeleteFile(path);
		}
	}
}
