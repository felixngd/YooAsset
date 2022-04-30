﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace YooAsset.Editor
{
	public class BuildAssetInfo
	{
		private string _mainBundleName;
		private string _shareBundleName;
		private readonly HashSet<string> _referenceBundleNames = new HashSet<string>();

		/// <summary>
		/// 收集器类型
		/// </summary>
		public ECollectorType CollectorType { private set; get; }

		/// <summary>
		/// 可寻址地址
		/// </summary>
		public string Address { private set; get; }

		/// <summary>
		/// 资源路径
		/// </summary>
		public string AssetPath { private set; get; }

		/// <summary>
		/// 是否为原生资源
		/// </summary>
		public bool IsRawAsset { private set; get; }

		/// <summary>
		/// 是否为着色器资源
		/// </summary>
		public bool IsShaderAsset { private set; get; }

		/// <summary>
		/// 资源分类标签列表
		/// </summary>
		public readonly List<string> AssetTags = new List<string>();

		/// <summary>
		/// 依赖的所有资源
		/// 注意：包括零依赖资源和冗余资源（资源包名无效）
		/// </summary>
		public List<BuildAssetInfo> AllDependAssetInfos { private set; get; }


		public BuildAssetInfo(ECollectorType collectorType, string mainBundleName, string address, string assetPath, bool isRawAsset)
		{
			_mainBundleName = mainBundleName;
			CollectorType = collectorType;
			Address = address;
			AssetPath = assetPath;
			IsRawAsset = isRawAsset;

			System.Type assetType = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(assetPath);
			if (assetType == typeof(UnityEngine.Shader))
				IsShaderAsset = true;
			else
				IsShaderAsset = false;
		}
		public BuildAssetInfo(ECollectorType collectorType, string assetPath)
		{
			CollectorType = collectorType;
			Address = string.Empty;
			AssetPath = assetPath;
			IsRawAsset = false;

			System.Type assetType = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(assetPath);
			if (assetType == typeof(UnityEngine.Shader))
				IsShaderAsset = true;
			else
				IsShaderAsset = false;
		}


		/// <summary>
		/// 设置所有依赖的资源
		/// </summary>
		public void SetAllDependAssetInfos(List<BuildAssetInfo> dependAssetInfos)
		{
			if (AllDependAssetInfos != null)
				throw new System.Exception("Should never get here !");

			AllDependAssetInfos = dependAssetInfos;
		}

		/// <summary>
		/// 添加资源分类标签
		/// </summary>
		public void AddAssetTags(List<string> tags)
		{
			foreach (var tag in tags)
			{
				if (AssetTags.Contains(tag) == false)
				{
					AssetTags.Add(tag);
				}
			}
		}

		/// <summary>
		/// 资源包名是否存在
		/// </summary>
		public bool HasBundleName()
		{
			string bundleName = GetBundleName();
			if (string.IsNullOrEmpty(bundleName))
				return false;
			else
				return true;
		}

		/// <summary>
		/// 获取资源包名称
		/// </summary>
		public string GetBundleName()
		{
			if (CollectorType == ECollectorType.None)
				return _shareBundleName;
			else
				return _mainBundleName;
		}

		/// <summary>
		/// 添加关联的资源包名称
		/// </summary>
		public void AddReferenceBundleName(string bundleName)
		{
			if (string.IsNullOrEmpty(bundleName))
				throw new Exception("Should never get here !");

			if (_referenceBundleNames.Contains(bundleName) == false)
				_referenceBundleNames.Add(bundleName);
		}

		/// <summary>
		/// 计算主资源或共享资源的完整包名
		/// </summary>
		public void CalculateFullBundleName()
		{
			if (CollectorType == ECollectorType.None)
			{
				if (IsRawAsset)
					throw new Exception("Should never get here !");

				if (AssetBundleGrouperSettingData.Setting.AutoCollectShaders)
				{
					if (IsShaderAsset)
					{
						string shareBundleName = $"{AssetBundleGrouperSettingData.Setting.ShadersBundleName}.{YooAssetSettingsData.Setting.AssetBundleFileVariant}";
						_shareBundleName = EditorTools.GetRegularPath(shareBundleName).ToLower();
						return;
					}
				}

				if (_referenceBundleNames.Count > 1)
				{
					var bundleNameList = _referenceBundleNames.ToList();
					bundleNameList.Sort();
					string combineName = string.Join("|", bundleNameList);
					var combineNameHash = HashUtility.StringSHA1(combineName);
					var shareBundleName = $"share_{combineNameHash}.{YooAssetSettingsData.Setting.AssetBundleFileVariant}";
					_shareBundleName = EditorTools.GetRegularPath(shareBundleName).ToLower();
				}
			}
			else
			{
				if (IsRawAsset)
				{
					string mainBundleName = $"{_mainBundleName}.{YooAssetSettingsData.Setting.RawFileVariant}";
					_mainBundleName = EditorTools.GetRegularPath(mainBundleName).ToLower();
				}
				else
				{
					string mainBundleName = $"{_mainBundleName}.{YooAssetSettingsData.Setting.AssetBundleFileVariant}";
					_mainBundleName = EditorTools.GetRegularPath(mainBundleName).ToLower(); ;
				}
			}
		}
	}
}