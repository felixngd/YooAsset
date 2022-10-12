using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YooAsset
{
	public static partial class YooAssets
	{
		private static bool _isInitialize = false;
		private static readonly List<YooAssetPackage> _packages = new List<YooAssetPackage>();

		/// <summary>
		/// 初始化资源系统
		/// </summary>
		public static void Initialize()
		{
			if (_isInitialize)
				throw new Exception($"{nameof(YooAssets)} is initialized !");

			if (_isInitialize == false)
			{
				// 创建驱动器
				_isInitialize = true;
				UnityEngine.GameObject driverGo = new UnityEngine.GameObject($"[{nameof(YooAssets)}]");
				driverGo.AddComponent<YooAssetDriver>();
				UnityEngine.Object.DontDestroyOnLoad(driverGo);

#if DEBUG
				// 添加远程调试脚本
				driverGo.AddComponent<RemoteDebuggerInRuntime>();
#endif

				// 初始化异步系统
				OperationSystem.Initialize();
			}
		}

		/// <summary>
		/// 更新资源系统
		/// </summary>
		internal static void Update()
		{
			if (_isInitialize)
			{
				OperationSystem.Update();
				DownloadSystem.Update();

				foreach (var package in _packages)
				{
					package.UpdatePackage();
				}
			}
		}

		/// <summary>
		/// 销毁资源系统
		/// </summary>
		internal static void Destroy()
		{
			if (_isInitialize)
			{
				OperationSystem.DestroyAll();
				DownloadSystem.DestroyAll();
				CacheSystem.ClearAll();

				foreach (var package in _packages)
				{
					package.DestroyPackage();
				}
				_packages.Clear();

				_isInitialize = false;
				YooLogger.Log("YooAssets destroy all !");
			}
		}


		/// <summary>
		/// 创建资源包
		/// </summary>
		/// <param name="packageName">资源包名称</param>
		public static YooAssetPackage CreateAssetPackage(string packageName)
		{
			if (_isInitialize == false)
				throw new Exception($"{nameof(YooAssets)} not initialize !");

			if (string.IsNullOrEmpty(packageName))
				throw new Exception("PackageName is null or empty !");

			if (HasAssetPackage(packageName))
				throw new Exception($"Package {packageName} already existed !");

			YooAssetPackage component = new YooAssetPackage(packageName);
			_packages.Add(component);
			return component;
		}

		/// <summary>
		/// 获取资源包
		/// </summary>
		/// <param name="packageName">资源包名称</param>
		public static YooAssetPackage GetAssetPackage(string packageName)
		{
			if (_isInitialize == false)
				throw new Exception($"{nameof(YooAssets)} not initialize !");

			if (string.IsNullOrEmpty(packageName))
				throw new Exception("Package name is null or empty !");

			foreach (var package in _packages)
			{
				if (package.PackageName == packageName)
					return package;
			}

			YooLogger.Warning($"Not found asset package : {packageName}");
			return null;
		}

		/// <summary>
		/// 检测资源包是否存在
		/// </summary>
		/// <param name="packageName">资源包名称</param>
		public static bool HasAssetPackage(string packageName)
		{
			if (_isInitialize == false)
				throw new Exception($"{nameof(YooAssets)} not initialize !");

			foreach (var package in _packages)
			{
				if (package.PackageName == packageName)
					return true;
			}
			return false;
		}

		/// <summary>
		/// 开启一个异步操作
		/// </summary>
		/// <param name="operation">异步操作对象</param>
		public static void StartOperation(GameAsyncOperation operation)
		{
			OperationSystem.StartOperation(operation);
		}

		#region 系统参数
		/// <summary>
		/// 启用下载系统的断点续传功能的文件大小
		/// </summary>
		public static void SetDownloadSystemBreakpointResumeFileSize(int fileBytes)
		{
			DownloadSystem.BreakpointResumeFileSize = fileBytes;
		}

		/// <summary>
		/// 设置异步系统的每帧允许运行的最大时间切片（单位：毫秒）
		/// </summary>
		public static void SetOperationSystemMaxTimeSlice(long milliseconds)
		{
			if (milliseconds < 30)
			{
				milliseconds = 30;
				YooLogger.Warning($"MaxTimeSlice minimum value is 30 milliseconds.");
			}
			OperationSystem.MaxTimeSlice = milliseconds;
		}

		/// <summary>
		/// 设置缓存系统的已经缓存文件的校验等级
		/// </summary>
		public static void SetCacheSystemCachedFileVerifyLevel(EVerifyLevel verifyLevel)
		{
			CacheSystem.InitVerifyLevel = verifyLevel;
		}
		#endregion

		#region 沙盒相关
		/// <summary>
		/// 获取沙盒的根路径
		/// </summary>
		public static string GetSandboxRoot()
		{
			return PathHelper.MakePersistentRootPath();
		}

		/// <summary>
		/// 清空沙盒目录
		/// </summary>
		public static void ClearSandbox()
		{
			SandboxHelper.DeleteSandbox();
		}

		/// <summary>
		/// 清空所有的缓存文件
		/// </summary>
		public static void ClearAllCacheFiles()
		{
			SandboxHelper.DeleteCacheFolder();
		}
		#endregion

		#region 调试信息
		internal static DebugReport GetDebugReport()
		{
			DebugReport report = new DebugReport();
			report.FrameCount = Time.frameCount;

			foreach (var package in _packages)
			{
				var result = package.GetDebugReportInfos();
				report.ProviderInfos.AddRange(result);
			}

			// 重新排序
			report.ProviderInfos.Sort();
			return report;
		}
		#endregion
	}
}