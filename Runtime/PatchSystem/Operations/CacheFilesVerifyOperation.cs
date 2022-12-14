﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace YooAsset
{
	/// <summary>
	/// 本地缓存文件验证
	/// </summary>
	internal abstract class CacheFilesVerifyOperation : AsyncOperationBase
	{
		public List<VerifyInfo> VerifySuccessList { protected set; get; }
		public List<VerifyInfo> VerifyFailList { protected set; get; }
	}

	/// <summary>
	/// 本地缓存文件验证（线程版）
	/// </summary>
	internal class CacheFilesVerifyWithThreadOperation : CacheFilesVerifyOperation
	{
		private enum ESteps
		{
			None,
			InitVerify,
			PrepareVerify,
			UpdateVerify,
			Done,
		}

		private readonly PatchManifest _patchManifest;
		private readonly IQueryServices _queryServices;
		private ESteps _steps = ESteps.None;

		private readonly ThreadSyncContext _syncContext = new ThreadSyncContext();
		private List<VerifyInfo> _waitingList;
		private List<VerifyInfo> _verifyingList;
		private int _verifyMaxNum;
		private int _verifyTotalCount;

		public CacheFilesVerifyWithThreadOperation(PatchManifest patchManifest, IQueryServices queryServices)
		{
			_patchManifest = patchManifest;
			_queryServices = queryServices;
		}
		internal override void Start()
		{
			_steps = ESteps.InitVerify;
		}
		internal override void Update()
		{
			if (_steps == ESteps.None || _steps == ESteps.Done)
				return;

			if (_steps == ESteps.InitVerify)
			{
				int bundleCount = _patchManifest.BundleList.Count;
				VerifySuccessList = new List<VerifyInfo>(bundleCount);
				VerifyFailList = new List<VerifyInfo>(bundleCount);

				// 设置同时验证的最大数
				ThreadPool.GetMaxThreads(out int workerThreads, out int ioThreads);
				YooLogger.Log($"Work threads : {workerThreads}, IO threads : {ioThreads}");
				_verifyMaxNum = Math.Min(workerThreads, ioThreads);
				_verifyTotalCount = bundleCount;
				if (_verifyMaxNum < 1)
					_verifyMaxNum = 1;

				_waitingList = new List<VerifyInfo>(bundleCount);
				_verifyingList = new List<VerifyInfo>(_verifyMaxNum);
				_steps = ESteps.PrepareVerify;
			}

			if (_steps == ESteps.PrepareVerify)
			{
				foreach (var patchBundle in _patchManifest.BundleList)
				{
					if (CacheSystem.IsCached(patchBundle))
						continue;

					bool isBuildinFile = IsBuildinFile(patchBundle);
					VerifyInfo verifyInfo = new VerifyInfo(isBuildinFile, patchBundle);
					_waitingList.Add(verifyInfo);
				}
				_steps = ESteps.UpdateVerify;
			}

			if (_steps == ESteps.UpdateVerify)
			{
				_syncContext.Update();

				Progress = GetVerifierProgress();
				if (_waitingList.Count == 0 && _verifyingList.Count == 0)
				{
					_steps = ESteps.Done;
					Status = EOperationStatus.Succeed;
				}

				for (int i = _waitingList.Count - 1; i >= 0; i--)
				{
					if (OperationSystem.IsBusy)
						break;

					if (_verifyingList.Count >= _verifyMaxNum)
						break;

					var verifyIno = _waitingList[i];
					if (VerifyFileWithThread(verifyIno))
					{
						_waitingList.RemoveAt(i);
						_verifyingList.Add(verifyIno);
					}
					else
					{
						YooLogger.Warning("The thread pool is failed queued.");
						break;
					}
				}
			}
		}

		private float GetVerifierProgress()
		{
			if (_verifyTotalCount == 0)
				return 1f;
			return (float)(VerifySuccessList.Count + VerifyFailList.Count) / _verifyTotalCount;
		}
		private bool IsBuildinFile(PatchBundle patchBundle)
		{
			if (_queryServices == null)
				return true;

			return _queryServices.QueryStreamingAssets(patchBundle.FileName);
		}
		private bool VerifyFileWithThread(VerifyInfo verifyInfo)
		{
			return ThreadPool.QueueUserWorkItem(new WaitCallback(VerifyInThread), verifyInfo);
		}
		private void VerifyInThread(object infoObj)
		{
			VerifyInfo verifyInfo = (VerifyInfo)infoObj;
			verifyInfo.Result = CacheSystem.VerifyBundle(verifyInfo.VerifyBundle, CacheSystem.InitVerifyLevel);
			_syncContext.Post(VerifyCallback, verifyInfo);
		}
		private void VerifyCallback(object obj)
		{
			VerifyInfo verifyIno = (VerifyInfo)obj;
			if (verifyIno.Result == EVerifyResult.Succeed)
			{
				VerifySuccessList.Add(verifyIno);
				CacheSystem.CacheBundle(verifyIno.VerifyBundle);
			}
			else
			{
				VerifyFailList.Add(verifyIno);

				// 删除验证失败的缓存文件
				if (File.Exists(verifyIno.VerifyBundle.CachedFilePath))
				{
					YooLogger.Warning($"Delete verify failed bundle file : {verifyIno.VerifyBundle.CachedFilePath}");
					File.Delete(verifyIno.VerifyBundle.CachedFilePath);
				}
			}
			_verifyingList.Remove(verifyIno);
		}
	}

	/// <summary>
	/// 本地缓存文件验证（非线程版）
	/// </summary>
	internal class CacheFilesVerifyWithoutThreadOperation : CacheFilesVerifyOperation
	{
		private enum ESteps
		{
			None,
			InitVerify,
			PrepareVerify,
			UpdateVerify,
			Done,
		}

		private readonly PatchManifest _patchManifest;
		private readonly IQueryServices _queryServices;
		private ESteps _steps = ESteps.None;

		private List<VerifyInfo> _waitingList;
		private List<VerifyInfo> _verifyingList;
		private int _verifyMaxNum;
		private int _verifyTotalCount;

		public CacheFilesVerifyWithoutThreadOperation(PatchManifest patchManifest, IQueryServices queryServices)
		{
			_patchManifest = patchManifest;
			_queryServices = queryServices;
		}
		internal override void Start()
		{
			_steps = ESteps.InitVerify;
		}
		internal override void Update()
		{
			if (_steps == ESteps.None || _steps == ESteps.Done)
				return;

			if (_steps == ESteps.InitVerify)
			{
				int bundleCount = _patchManifest.BundleList.Count;
				VerifySuccessList = new List<VerifyInfo>(bundleCount);
				VerifyFailList = new List<VerifyInfo>(bundleCount);

				// 设置同时验证的最大数
				_verifyMaxNum = bundleCount;
				_verifyTotalCount = _waitingList.Count;

				_waitingList = new List<VerifyInfo>(bundleCount);
				_verifyingList = new List<VerifyInfo>(_verifyMaxNum);
				_steps = ESteps.PrepareVerify;
			}

			if (_steps == ESteps.PrepareVerify)
			{
				foreach (var patchBundle in _patchManifest.BundleList)
				{
					if (CacheSystem.IsCached(patchBundle))
						continue;

					bool isBuildinFile = IsBuildinFile(patchBundle);
					VerifyInfo verifyInfo = new VerifyInfo(isBuildinFile, patchBundle);
					_waitingList.Add(verifyInfo);
				}
				_steps = ESteps.UpdateVerify;
			}

			if (_steps == ESteps.UpdateVerify)
			{
				Progress = GetVerifierProgress();
				if (_waitingList.Count == 0 && _verifyingList.Count == 0)
				{
					_steps = ESteps.Done;
					Status = EOperationStatus.Succeed;
				}

				for (int i = _waitingList.Count - 1; i >= 0; i--)
				{
					if (OperationSystem.IsBusy)
						break;

					if (_verifyingList.Count >= _verifyMaxNum)
						break;

					var verifyIno = _waitingList[i];
					VerifyFileWithoutThread(verifyIno);
					_waitingList.RemoveAt(i);
					_verifyingList.Add(verifyIno);
				}

				_verifyingList.Clear();
			}
		}

		private float GetVerifierProgress()
		{
			if (_verifyTotalCount == 0)
				return 1f;
			return (float)(VerifySuccessList.Count + VerifyFailList.Count) / _verifyTotalCount;
		}
		private bool IsBuildinFile(PatchBundle patchBundle)
		{
			if (_queryServices == null)
				return true;

			return _queryServices.QueryStreamingAssets(patchBundle.FileName);
		}
		private void VerifyFileWithoutThread(VerifyInfo verifyIno)
		{
			var verifyResult = CacheSystem.VerifyAndCacheLocalBundleFile(verifyIno.VerifyBundle, CacheSystem.InitVerifyLevel);
			if (verifyResult == EVerifyResult.Succeed)
			{
				VerifySuccessList.Add(verifyIno);
			}
			else
			{
				VerifyFailList.Add(verifyIno);

				// 删除验证失败的缓存文件
				if (File.Exists(verifyIno.VerifyBundle.CachedFilePath))
				{
					YooLogger.Warning($"Delete verify failed bundle file : {verifyIno.VerifyBundle.CachedFilePath}");
					File.Delete(verifyIno.VerifyBundle.CachedFilePath);
				}
			}
		}
	}
}