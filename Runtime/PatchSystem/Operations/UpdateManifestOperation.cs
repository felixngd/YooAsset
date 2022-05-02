﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace YooAsset
{
	/// <summary>
	/// 更新清单操作
	/// </summary>
	public abstract class UpdateManifestOperation : AsyncOperationBase
	{
	}

	/// <summary>
	/// 编辑器下模拟运行的更新清单操作
	/// </summary>
	internal sealed class EditorPlayModeUpdateManifestOperation : UpdateManifestOperation
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
	/// 离线模式的更新清单操作
	/// </summary>
	internal sealed class OfflinePlayModeUpdateManifestOperation : UpdateManifestOperation
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
	/// 网络模式的更新清单操作
	/// </summary>
	internal sealed class HostPlayModeUpdateManifestOperation : UpdateManifestOperation
	{
		private enum ESteps
		{
			None,
			LoadWebManifestHash,
			CheckWebManifestHash,
			LoadWebManifest,
			CheckWebManifest,
			InitVerifyingCache,
			UpdateVerifyingCache,
			Done,
		}

		private static int RequestCount = 0;
		private readonly HostPlayModeImpl _impl;
		private readonly int _resourceVersion;
		private readonly int _timeout;
		private ESteps _steps = ESteps.None;
		private UnityWebDataRequester _downloaderHash;
		private UnityWebDataRequester _downloaderManifest;
		private float _verifyTime;

		internal HostPlayModeUpdateManifestOperation(HostPlayModeImpl impl, int resourceVersion, int timeout)
		{
			_impl = impl;
			_resourceVersion = resourceVersion;
			_timeout = timeout;
		}
		internal override void Start()
		{
			RequestCount++;
			_steps = ESteps.LoadWebManifestHash;
		}
		internal override void Update()
		{
			if (_steps == ESteps.None || _steps == ESteps.Done)
				return;

			if (_steps == ESteps.LoadWebManifestHash)
			{
				string webURL = GetPatchManifestRequestURL(YooAssetSettingsData.GetPatchManifestHashFileName(_resourceVersion));
				YooLogger.Log($"Beginning to request patch manifest hash : {webURL}");
				_downloaderHash = new UnityWebDataRequester();
				_downloaderHash.SendRequest(webURL, _timeout);
				_steps = ESteps.CheckWebManifestHash;
			}

			if (_steps == ESteps.CheckWebManifestHash)
			{
				if (_downloaderHash.IsDone() == false)
					return;

				// Check error
				if (_downloaderHash.HasError())
				{
					_steps = ESteps.Done;
					Status = EOperationStatus.Failed;
					Error = _downloaderHash.GetError();
				}
				else
				{
					string webManifestHash = _downloaderHash.GetText();
					string cachedManifestHash = GetSandboxPatchManifestFileHash(_resourceVersion);

					// 如果补丁清单文件的哈希值相同
					if (cachedManifestHash == webManifestHash)
					{
						YooLogger.Log($"Patch manifest file hash is not change : {webManifestHash}");
						LoadSandboxPatchManifest(_resourceVersion);
						_steps = ESteps.InitVerifyingCache;
					}
					else
					{
						YooLogger.Log($"Patch manifest hash is change : {webManifestHash} -> {cachedManifestHash}");
						_steps = ESteps.LoadWebManifest;
					}
				}
				_downloaderHash.Dispose();
			}

			if (_steps == ESteps.LoadWebManifest)
			{
				string webURL = GetPatchManifestRequestURL(YooAssetSettingsData.GetPatchManifestFileName(_resourceVersion));
				YooLogger.Log($"Beginning to request patch manifest : {webURL}");
				_downloaderManifest = new UnityWebDataRequester();
				_downloaderManifest.SendRequest(webURL, _timeout);
				_steps = ESteps.CheckWebManifest;
			}

			if (_steps == ESteps.CheckWebManifest)
			{
				if (_downloaderManifest.IsDone() == false)
					return;

				// Check error
				if (_downloaderManifest.HasError())
				{
					_steps = ESteps.Done;
					Status = EOperationStatus.Failed;
					Error = _downloaderManifest.GetError();
				}
				else
				{
					// 解析补丁清单			
					if (ParseAndSaveRemotePatchManifest(_resourceVersion, _downloaderManifest.GetText()))
					{
						_steps = ESteps.InitVerifyingCache;
					}
					else
					{
						_steps = ESteps.Done;
						Status = EOperationStatus.Failed;
						Error = $"URL : {_downloaderManifest.URL} Error : remote patch manifest content is invalid";
					}
				}
				_downloaderManifest.Dispose();
			}

			if (_steps == ESteps.InitVerifyingCache)
			{
				InitVerifyingCache();
				_verifyTime = UnityEngine.Time.realtimeSinceStartup;
				_steps = ESteps.UpdateVerifyingCache;
			}

			if (_steps == ESteps.UpdateVerifyingCache)
			{
				if (UpdateVerifyingCache())
				{
					_steps = ESteps.Done;
					Status = EOperationStatus.Succeed;
					float costTime = UnityEngine.Time.realtimeSinceStartup - _verifyTime;
					YooLogger.Log($"Verify result : Success {_verifySuccessCount}, Fail {_verifyFailCount}, Elapsed time {costTime} seconds");
				}
			}
		}

		/// <summary>
		/// 获取补丁清单请求地址
		/// </summary>
		private string GetPatchManifestRequestURL(string fileName)
		{
			// 轮流返回请求地址
			if (RequestCount % 2 == 0)
				return _impl.GetPatchDownloadFallbackURL(fileName);
			else
				return _impl.GetPatchDownloadMainURL(fileName);
		}

		/// <summary>
		/// 解析并保存远端请求的补丁清单
		/// </summary>
		private bool ParseAndSaveRemotePatchManifest(int updateResourceVersion, string content)
		{
			try
			{
				_impl.LocalPatchManifest = PatchManifest.Deserialize(content);

				YooLogger.Log("Save remote patch manifest file.");
				string savePath = PathHelper.MakePersistentLoadPath(YooAssetSettingsData.GetPatchManifestFileName(updateResourceVersion));
				PatchManifest.Serialize(savePath, _impl.LocalPatchManifest);
				return true;
			}
			catch (Exception e)
			{
				YooLogger.Warning(e.ToString());
				return false;
			}
		}

		/// <summary>
		/// 加载沙盒内的补丁清单
		/// </summary>
		private void LoadSandboxPatchManifest(int updateResourceVersion)
		{
			YooLogger.Log("Load sandbox patch manifest file.");
			string filePath = PathHelper.MakePersistentLoadPath(YooAssetSettingsData.GetPatchManifestFileName(updateResourceVersion));
			string jsonData = File.ReadAllText(filePath);
			_impl.LocalPatchManifest = PatchManifest.Deserialize(jsonData);
		}

		/// <summary>
		/// 获取沙盒内补丁清单文件的哈希值
		/// 注意：如果沙盒内补丁清单文件不存在，返回空字符串
		/// </summary>
		private string GetSandboxPatchManifestFileHash(int updateResourceVersion)
		{
			string filePath = PathHelper.MakePersistentLoadPath(YooAssetSettingsData.GetPatchManifestFileName(updateResourceVersion));
			if (File.Exists(filePath))
				return HashUtility.FileMD5(filePath);
			else
				return string.Empty;
		}

		#region 多线程相关
		private class ThreadInfo
		{
			public bool Result = false;
			public string FilePath { private set; get; }
			public PatchBundle Bundle { private set; get; }
			public ThreadInfo(string filePath, PatchBundle bundle)
			{
				FilePath = filePath;
				Bundle = bundle;
			}
		}

		private readonly List<PatchBundle> _waitingList = new List<PatchBundle>(1000);
		private readonly List<PatchBundle> _verifyingList = new List<PatchBundle>(100);
		private readonly ThreadSyncContext _syncContext = new ThreadSyncContext();
		private int _verifyMaxNum = 32;
		private int _verifySuccessCount = 0;
		private int _verifyFailCount = 0;

		private void InitVerifyingCache()
		{
			// 遍历所有文件然后验证并缓存合法文件
			foreach (var patchBundle in _impl.LocalPatchManifest.BundleList)
			{
				// 忽略缓存文件
				if (DownloadSystem.ContainsVerifyFile(patchBundle.Hash))
					continue;

				// 忽略APP资源
				// 注意：如果是APP资源并且哈希值相同，则不需要下载
				if (_impl.AppPatchManifest.Bundles.TryGetValue(patchBundle.BundleName, out PatchBundle appPatchBundle))
				{
					if (appPatchBundle.IsBuildin && appPatchBundle.Hash == patchBundle.Hash)
						continue;
				}

				// 查看文件是否存在
				string filePath = SandboxHelper.MakeCacheFilePath(patchBundle.Hash);
				if (File.Exists(filePath) == false)
					continue;

				_waitingList.Add(patchBundle);
			}

			// 设置同时验证的最大数
			ThreadPool.GetMaxThreads(out int workerThreads, out int ioThreads);
			YooLogger.Log($"Work threads : {workerThreads}, IO threads : {ioThreads}");
			_verifyMaxNum = Math.Min(workerThreads, ioThreads);
		}
		private bool UpdateVerifyingCache()
		{
			_syncContext.Update();

			if (_waitingList.Count == 0 && _verifyingList.Count == 0)
				return true;

			if (_verifyingList.Count >= _verifyMaxNum)
				return false;

			for (int i = _waitingList.Count - 1; i >= 0; i--)
			{
				if (_verifyingList.Count >= _verifyMaxNum)
					break;

				var patchBundle = _waitingList[i];
				if (RunThread(patchBundle))
				{
					_waitingList.RemoveAt(i);
					_verifyingList.Add(patchBundle);
				}
				else
				{
					YooLogger.Warning("The thread pool is failed queued.");
					break;
				}
			}

			return false;
		}
		private bool RunThread(PatchBundle patchBundle)
		{
			string filePath = SandboxHelper.MakeCacheFilePath(patchBundle.Hash);
			ThreadInfo info = new ThreadInfo(filePath, patchBundle);
			return ThreadPool.QueueUserWorkItem(new WaitCallback(VerifyInThread), info);
		}
		private void VerifyInThread(object infoObj)
		{
			// 验证沙盒内的文件
			ThreadInfo info = (ThreadInfo)infoObj;
			try
			{
				info.Result = DownloadSystem.CheckContentIntegrity(info.FilePath, info.Bundle.SizeBytes, info.Bundle.CRC);
			}
			catch (Exception)
			{
				info.Result = false;
			}
			_syncContext.Post(VerifyCallback, info);
		}
		private void VerifyCallback(object obj)
		{
			ThreadInfo info = (ThreadInfo)obj;
			if (info.Result)
			{
				_verifySuccessCount++;
				DownloadSystem.CacheVerifyFile(info.Bundle.Hash, info.Bundle.BundleName);
			}
			else
			{
				_verifyFailCount++;
				YooLogger.Warning($"Failed to verify file : {info.FilePath}");
				if (File.Exists(info.FilePath))
					File.Delete(info.FilePath);
			}
			_verifyingList.Remove(info.Bundle);
		}
		#endregion
	}
}