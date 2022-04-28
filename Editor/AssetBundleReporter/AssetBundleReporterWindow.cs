#if UNITY_2019_4_OR_NEWER
using UnityEditor;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace YooAsset.Editor
{
	public class AssetBundleReporterWindow : EditorWindow
	{
		[MenuItem("YooAsset/AssetBundle Reporter", false, 103)]
		public static void ShowExample()
		{
			AssetBundleReporterWindow window = GetWindow<AssetBundleReporterWindow>("资源包报告工具", true, EditorDefine.DockedWindowTypes);
			window.minSize = new Vector2(800, 600);
		}

		/// <summary>
		/// 视图模式
		/// </summary>
		private enum EViewMode
		{
			/// <summary>
			/// 概览视图
			/// </summary>
			Summary,

			/// <summary>
			/// 资源对象视图
			/// </summary>
			AssetView,

			/// <summary>
			/// 资源包视图
			/// </summary>
			BundleView,
		}

		private ToolbarMenu _viewModeMenu;
		private SummaryReporterViewer _summaryViewer;
		private AssetListReporterViewer _assetListViewer;
		private BundleListReporterViewer _bundleListViewer;

		private EViewMode _viewMode;
		private string _searchKeyWord;
		private BuildReport _buildReport;


		public void CreateGUI()
		{
			VisualElement root = this.rootVisualElement;

			// 加载布局文件
			string rootPath = EditorTools.GetYooAssetPath();
			string uxml = $"{rootPath}/Editor/AssetBundleReporter/{nameof(AssetBundleReporterWindow)}.uxml";
			var visualAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxml);
			if (visualAsset == null)
			{
				Debug.LogError($"Not found {nameof(AssetBundleReporterWindow)}.uxml : {uxml}");
				return;
			}
			visualAsset.CloneTree(root);

			// 导入按钮
			var importBtn = root.Q<Button>("ImportButton");
			importBtn.clicked += ImportBtn_onClick;

			// 视图模式菜单
			_viewModeMenu = root.Q<ToolbarMenu>("ViewModeMenu");
			_viewModeMenu.menu.AppendAction(EViewMode.Summary.ToString(), ViewModeMenuAction0, ViewModeMenuFun0);
			_viewModeMenu.menu.AppendAction(EViewMode.AssetView.ToString(), ViewModeMenuAction1, ViewModeMenuFun1);
			_viewModeMenu.menu.AppendAction(EViewMode.BundleView.ToString(), ViewModeMenuAction2, ViewModeMenuFun2);

			// 搜索栏
			var searchField = root.Q<ToolbarSearchField>("SearchField");
			searchField.RegisterValueChangedCallback(OnSearchKeyWordChange);

			// 加载视图
			_summaryViewer = new SummaryReporterViewer();
			_summaryViewer.InitViewer();

			// 加载视图
			_assetListViewer = new AssetListReporterViewer();
			_assetListViewer.InitViewer();

			// 加载视图
			_bundleListViewer = new BundleListReporterViewer();
			_bundleListViewer.InitViewer();

			// 显示视图
			_viewMode = EViewMode.Summary;
			_viewModeMenu.text = EViewMode.Summary.ToString();
			_summaryViewer.AttachParent(root);
		}

		private void ImportBtn_onClick()
		{
			string selectFilePath = EditorUtility.OpenFilePanel("导入报告", EditorTools.GetProjectPath(), "json");
			if (string.IsNullOrEmpty(selectFilePath))
				return;

			string jsonData = FileUtility.ReadFile(selectFilePath);
			_buildReport = BuildReport.Deserialize(jsonData);
			_assetListViewer.FillViewData(_buildReport, _searchKeyWord);
			_bundleListViewer.FillViewData(_buildReport, _searchKeyWord);
			_summaryViewer.FillViewData(_buildReport);
		}
		private void OnSearchKeyWordChange(ChangeEvent<string> e)
		{
			_searchKeyWord = e.newValue;
			if(_buildReport != null)
			{
				_assetListViewer.FillViewData(_buildReport, _searchKeyWord);
				_bundleListViewer.FillViewData(_buildReport, _searchKeyWord);
			}
		}
		private void ViewModeMenuAction0(DropdownMenuAction action)
		{
			if (_viewMode != EViewMode.Summary)
			{
				_viewMode = EViewMode.Summary;
				VisualElement root = this.rootVisualElement;
				_viewModeMenu.text = EViewMode.Summary.ToString();
				_summaryViewer.AttachParent(root);
				_assetListViewer.DetachParent();
				_bundleListViewer.DetachParent();
			}
		}
		private void ViewModeMenuAction1(DropdownMenuAction action)
		{
			if (_viewMode != EViewMode.AssetView)
			{
				_viewMode = EViewMode.AssetView;
				VisualElement root = this.rootVisualElement;
				_viewModeMenu.text = EViewMode.AssetView.ToString();
				_summaryViewer.DetachParent();
				_assetListViewer.AttachParent(root);
				_bundleListViewer.DetachParent();
			}
		}
		private void ViewModeMenuAction2(DropdownMenuAction action)
		{
			if (_viewMode != EViewMode.BundleView)
			{
				_viewMode = EViewMode.BundleView;
				VisualElement root = this.rootVisualElement;
				_viewModeMenu.text = EViewMode.BundleView.ToString();
				_summaryViewer.DetachParent();
				_assetListViewer.DetachParent();
				_bundleListViewer.AttachParent(root);
			}
		}
		private DropdownMenuAction.Status ViewModeMenuFun0(DropdownMenuAction action)
		{
			if (_viewMode == EViewMode.Summary)
				return DropdownMenuAction.Status.Checked;
			else
				return DropdownMenuAction.Status.Normal;
		}
		private DropdownMenuAction.Status ViewModeMenuFun1(DropdownMenuAction action)
		{
			if (_viewMode == EViewMode.AssetView)
				return DropdownMenuAction.Status.Checked;
			else
				return DropdownMenuAction.Status.Normal;
		}
		private DropdownMenuAction.Status ViewModeMenuFun2(DropdownMenuAction action)
		{
			if (_viewMode == EViewMode.BundleView)
				return DropdownMenuAction.Status.Checked;
			else
				return DropdownMenuAction.Status.Normal;
		}
	}
}
#endif