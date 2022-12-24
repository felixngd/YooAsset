﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YooAsset
{
	/// <summary>
	/// 请求远端包裹的最新版本
	/// </summary>
	public abstract class UpdatePackageVersionOperation : AsyncOperationBase
	{
		/// <summary>
		/// 当前最新的包裹版本
		/// </summary>
		public string PackageVersion { protected set; get; }
	}

	/// <summary>
	/// 编辑器下模拟模式的请求远端包裹的最新版本
	/// </summary>
	internal sealed class EditorPlayModeUpdatePackageVersionOperation : UpdatePackageVersionOperation
	{
		internal override void Start()
		{
			Status = EOperationStatus.Succeed;
		}
		internal override void Update()
		{
		}
	}

	/// <summary>
	/// 离线模式的请求远端包裹的最新版本
	/// </summary>
	internal sealed class OfflinePlayModeUpdatePackageVersionOperation : UpdatePackageVersionOperation
	{
		internal override void Start()
		{
			Status = EOperationStatus.Succeed;
		}
		internal override void Update()
		{
		}
	}

	/// <summary>
	/// 联机模式的请求远端包裹的最新版本
	/// </summary>
	internal sealed class HostPlayModeUpdatePackageVersionOperation : UpdatePackageVersionOperation
	{
		private enum ESteps
		{
			None,
			DownloadPackageVersion,
			Done,
		}

		private static int RequestCount = 0;
		private readonly HostPlayModeImpl _impl;
		private readonly string _packageName;
		private readonly bool _appendTimeTicks;
		private readonly int _timeout;
		private UnityWebDataRequester _downloader;
		private ESteps _steps = ESteps.None;

		internal HostPlayModeUpdatePackageVersionOperation(HostPlayModeImpl impl, string packageName, bool appendTimeTicks, int timeout)
		{
			_impl = impl;
			_packageName = packageName;
			_appendTimeTicks = appendTimeTicks;
			_timeout = timeout;
		}
		internal override void Start()
		{
			RequestCount++;
			_steps = ESteps.DownloadPackageVersion;
		}
		internal override void Update()
		{
			if (_steps == ESteps.None || _steps == ESteps.Done)
				return;

			if (_steps == ESteps.DownloadPackageVersion)
			{
				if (_downloader == null)
				{
					string fileName = YooAssetSettingsData.GetPackageVersionFileName(_packageName);
					string webURL = GetPackageVersionRequestURL(fileName);
					YooLogger.Log($"Beginning to request package version : {webURL}");
					_downloader = new UnityWebDataRequester();
					_downloader.SendRequest(webURL, _timeout);
				}

				Progress = _downloader.Progress();
				if (_downloader.IsDone() == false)
					return;

				if (_downloader.HasError())
				{
					_steps = ESteps.Done;
					Status = EOperationStatus.Failed;
					Error = _downloader.GetError();
				}
				else
				{
					PackageVersion = _downloader.GetText();
					if (string.IsNullOrEmpty(PackageVersion))
					{
						_steps = ESteps.Done;
						Status = EOperationStatus.Failed;
						Error = $"Package version is empty : {_downloader.URL}";
					}
					else
					{
						_steps = ESteps.Done;
						Status = EOperationStatus.Succeed;
					}
				}

				_downloader.Dispose();
			}
		}

		private string GetPackageVersionRequestURL(string fileName)
		{
			string url;

			// 轮流返回请求地址
			if (RequestCount % 2 == 0)
				url = _impl.GetPatchDownloadFallbackURL(fileName);
			else
				url = _impl.GetPatchDownloadMainURL(fileName);

			// 在URL末尾添加时间戳
			if (_appendTimeTicks)
				return $"{url}?{System.DateTime.UtcNow.Ticks}";
			else
				return url;
		}
	}
}