﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace YooAsset
{
	/// <summary>
	/// 本地缓存文件验证器
	/// </summary>
	internal abstract class PatchCacheVerifier
	{
		public abstract bool InitVerifier(PatchManifest appPatchManifest, PatchManifest localPatchManifest, bool weaklyUpdate);
		public abstract bool UpdateVerifier();
		public abstract float GetVerifierProgress();

		public int VerifySuccessCount { protected set; get; } = 0;
		public int VerifyFailCount { protected set; get; } = 0;
	}

	/// <summary>
	/// 本地缓存文件验证器（线程版）
	/// </summary>
	internal class PatchCacheVerifierWithThread : PatchCacheVerifier
	{
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

		private readonly ThreadSyncContext _syncContext = new ThreadSyncContext();
		private readonly List<PatchBundle> _waitingList = new List<PatchBundle>(1000);
		private readonly List<PatchBundle> _verifyingList = new List<PatchBundle>(100);
		private int _verifyMaxNum;
		private int _verifyTotalCount;

		public override bool InitVerifier(PatchManifest appPatchManifest, PatchManifest localPatchManifest, bool weaklyUpdate)
		{
			// 遍历所有文件然后验证并缓存合法文件
			foreach (var patchBundle in localPatchManifest.BundleList)
			{
				// 忽略缓存文件
				if (DownloadSystem.ContainsVerifyFile(patchBundle.FileHash))
					continue;

				// 忽略APP资源
				// 注意：如果是APP资源并且哈希值相同，则不需要下载
				if (appPatchManifest.TryGetPatchBundle(patchBundle.BundleName, out PatchBundle appPatchBundle))
				{
					if (appPatchBundle.IsBuildin && appPatchBundle.FileHash == patchBundle.FileHash)
						continue;
				}

				// 注意：在弱联网模式下，我们需要验证指定资源版本的所有资源完整性
				if (weaklyUpdate)
				{
					string filePath = SandboxHelper.MakeCacheFilePath(patchBundle.FileName);
					if (File.Exists(filePath))
						_waitingList.Add(patchBundle);
					else
						return false;
				}
				else
				{
					string filePath = SandboxHelper.MakeCacheFilePath(patchBundle.FileName);
					if (File.Exists(filePath))
						_waitingList.Add(patchBundle);
				}
			}

			// 设置同时验证的最大数
			ThreadPool.GetMaxThreads(out int workerThreads, out int ioThreads);
			YooLogger.Log($"Work threads : {workerThreads}, IO threads : {ioThreads}");
			_verifyMaxNum = Math.Min(workerThreads, ioThreads);
			_verifyTotalCount = _waitingList.Count;
			if (_verifyMaxNum < 1)
				_verifyMaxNum = 1;
			return true;
		}
		public override bool UpdateVerifier()
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
				if (VerifyFile(patchBundle))
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
		public override float GetVerifierProgress()
		{
			if (_verifyTotalCount == 0)
				return 1f;
			return (float)(VerifySuccessCount + VerifyFailCount) / _verifyTotalCount;
		}

		private bool VerifyFile(PatchBundle patchBundle)
		{
			string filePath = SandboxHelper.MakeCacheFilePath(patchBundle.FileName);
			ThreadInfo info = new ThreadInfo(filePath, patchBundle);
			return ThreadPool.QueueUserWorkItem(new WaitCallback(VerifyInThread), info);
		}
		private void VerifyInThread(object infoObj)
		{
			ThreadInfo info = (ThreadInfo)infoObj;
			info.Result = DownloadSystem.CheckContentIntegrity(info.FilePath, info.Bundle.FileSize, info.Bundle.FileCRC);
			_syncContext.Post(VerifyCallback, info);
		}
		private void VerifyCallback(object obj)
		{
			ThreadInfo info = (ThreadInfo)obj;
			if (info.Result)
			{
				VerifySuccessCount++;
				DownloadSystem.CacheVerifyFile(info.Bundle.FileHash, info.Bundle.FileName);
			}
			else
			{
				VerifyFailCount++;

				// NOTE：不期望删除断点续传的资源文件
				/*
				YooLogger.Warning($"Failed to verify file : {info.FilePath}");
				if (File.Exists(info.FilePath))
					File.Delete(info.FilePath);
				*/
			}
			_verifyingList.Remove(info.Bundle);
		}
	}

	/// <summary>
	/// 本地缓存文件验证器（非线程版）
	/// </summary>
	internal class PatchCacheVerifierWithoutThread : PatchCacheVerifier
	{
		private readonly List<PatchBundle> _waitingList = new List<PatchBundle>(1000);
		private readonly List<PatchBundle> _verifyingList = new List<PatchBundle>(100);
		private int _verifyMaxNum;
		private int _verifyTotalCount;

		public override bool InitVerifier(PatchManifest appPatchManifest, PatchManifest localPatchManifest, bool weaklyUpdate)
		{
			// 遍历所有文件然后验证并缓存合法文件
			foreach (var patchBundle in localPatchManifest.BundleList)
			{
				// 忽略缓存文件
				if (DownloadSystem.ContainsVerifyFile(patchBundle.FileHash))
					continue;

				// 忽略APP资源
				// 注意：如果是APP资源并且哈希值相同，则不需要下载
				if (appPatchManifest.TryGetPatchBundle(patchBundle.BundleName, out PatchBundle appPatchBundle))
				{
					if (appPatchBundle.IsBuildin && appPatchBundle.FileHash == patchBundle.FileHash)
						continue;
				}

				// 注意：在弱联网模式下，我们需要验证指定资源版本的所有资源完整性
				if (weaklyUpdate)
				{
					string filePath = SandboxHelper.MakeCacheFilePath(patchBundle.FileName);
					if (File.Exists(filePath))
						_waitingList.Add(patchBundle);
					else
						return false;
				}
				else
				{
					string filePath = SandboxHelper.MakeCacheFilePath(patchBundle.FileName);
					if (File.Exists(filePath))
						_waitingList.Add(patchBundle);
				}
			}

			// 设置同时验证的最大数
			_verifyMaxNum = 32;
			_verifyTotalCount = _waitingList.Count;
			return true;
		}
		public override bool UpdateVerifier()
		{
			if (_waitingList.Count == 0 && _verifyingList.Count == 0)
				return true;

			for (int i = _waitingList.Count - 1; i >= 0; i--)
			{
				if (_verifyingList.Count >= _verifyMaxNum)
					break;

				var patchBundle = _waitingList[i];
				VerifyFile(patchBundle);
				_waitingList.RemoveAt(i);
				_verifyingList.Add(patchBundle);
			}

			_verifyingList.Clear();
			return false;
		}
		public override float GetVerifierProgress()
		{
			if (_verifyTotalCount == 0)
				return 1f;
			return (float)(VerifySuccessCount + VerifyFailCount) / _verifyTotalCount;
		}

		private void VerifyFile(PatchBundle patchBundle)
		{
			string filePath = SandboxHelper.MakeCacheFilePath(patchBundle.FileName);
			bool result = DownloadSystem.CheckContentIntegrity(filePath, patchBundle.FileSize, patchBundle.FileCRC);
			if (result)
			{
				VerifySuccessCount++;
				DownloadSystem.CacheVerifyFile(patchBundle.FileHash, patchBundle.FileName);
			}
			else
			{
				VerifyFailCount++;

				// NOTE：不期望删除断点续传的资源文件
				/*
				YooLogger.Warning($"Failed to verify file : {info.FilePath}");
				if (File.Exists(info.FilePath))
					File.Delete(info.FilePath);
				*/
			}
		}
	}
}