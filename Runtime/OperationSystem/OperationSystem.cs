﻿using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace YooAsset
{
	internal class OperationSystem
	{
		private static readonly List<AsyncOperationBase> _operations = new List<AsyncOperationBase>(100);

		// 计时器相关
		private static Stopwatch _watch;	
		private static long _maxTimeSlice;
		private static long _frameTime;


		/// <summary>
		/// 初始化异步操作系统
		/// </summary>
		public static void Initialize(long maxTimeSlice)
		{
			_maxTimeSlice = maxTimeSlice;
			_watch = Stopwatch.StartNew();
		}

		/// <summary>
		/// 更新异步操作系统
		/// </summary>
		public static void Update()
		{
			_frameTime = _watch.ElapsedMilliseconds;

			for (int i = _operations.Count - 1; i >= 0; i--)
			{
				if (_watch.ElapsedMilliseconds - _frameTime >= _maxTimeSlice)
					return;

				_operations[i].Update();
				if (_operations[i].IsDone)
				{
					_operations[i].Finish();
					_operations.RemoveAt(i);
				}
			}
		}

		/// <summary>
		/// 开始处理异步操作类
		/// </summary>
		public static void ProcessOperaiton(AsyncOperationBase operationBase)
		{
			_operations.Add(operationBase);
			operationBase.Start();
		}
	}
}