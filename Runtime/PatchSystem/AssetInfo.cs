﻿using System.IO;

namespace YooAsset
{
	public class AssetInfo
	{
		private readonly PatchAsset _patchAsset;
		private string _providerGUID;

		/// <summary>
		/// 资源提供者唯一标识符
		/// </summary>
		internal string ProviderGUID
		{
			get
			{
				if (string.IsNullOrEmpty(_providerGUID) == false)
					return _providerGUID;

				if (AssetType == null)
					_providerGUID = $"{AssetPath}[null]";
				else
					_providerGUID = $"{AssetPath}[{AssetType.Name}]";
				return _providerGUID;
			}
		}

		/// <summary>
		/// 资源对象名称
		/// </summary>
		public string AssetName { private set; get; }

		/// <summary>
		/// 资源路径
		/// </summary>
		public string AssetPath { private set; get; }

		/// <summary>
		/// 资源类型
		/// </summary>
		public System.Type AssetType { private set; get; }

		/// <summary>
		/// 身份是否无效
		/// </summary>
		public bool IsInvalid
		{
			get
			{
				return _patchAsset == null;
			}
		}

		/// <summary>
		/// 错误信息
		/// </summary>
		public string Error { private set; get; }


		private AssetInfo()
		{
		}
		internal AssetInfo(PatchAsset patchAsset, System.Type assetType)
		{
			if (patchAsset == null)
				throw new System.Exception("Should never get here !");

			_patchAsset = patchAsset;
			AssetType = assetType;
			AssetPath = patchAsset.AssetPath;
			AssetName = Path.GetFileName(patchAsset.AssetPath);
			Error = string.Empty;
		}
		internal AssetInfo(PatchAsset patchAsset)
		{
			if (patchAsset == null)
				throw new System.Exception("Should never get here !");

			_patchAsset = patchAsset;
			AssetType = null;
			AssetPath = patchAsset.AssetPath;
			AssetName = Path.GetFileName(patchAsset.AssetPath);
			Error = string.Empty;
		}
		internal AssetInfo(string error)
		{
			_patchAsset = null;
			AssetType = null;
			AssetPath = string.Empty;
			AssetName = string.Empty;
			Error = error;
		}
	}
}