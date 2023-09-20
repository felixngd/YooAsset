#if UNITY_2019_4_OR_NEWER
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace YooAsset.Editor
{
	internal abstract class BuildPipelineViewerBase
	{
		private const int StyleWidth = 400;

		protected readonly string PackageName;
		protected readonly BuildTarget BuildTarget;
		protected readonly EBuildPipeline BuildPipeline;
		protected TemplateContainer Root;

		private TextField _buildOutputField;
		private TextField _buildVersionField;
		private PopupField<Enum> _buildModeField;
		private PopupField<Type> _encryptionField;
		private EnumField _compressionField;
		private EnumField _outputNameStyleField;
		private EnumField _copyBuildinFileOptionField;
		private TextField _copyBuildinFileTagsField;

		public BuildPipelineViewerBase(string packageName, EBuildPipeline buildPipeline, BuildTarget buildTarget, VisualElement parent)
		{
			PackageName = packageName;
			BuildTarget = buildTarget;
			BuildPipeline = buildPipeline;

			CreateView(parent);
			RefreshView();
		}
		private void CreateView(VisualElement parent)
		{
			// ���ز����ļ�
			var visualAsset = UxmlLoader.LoadWindowUXML<BuildPipelineViewerBase>();
			if (visualAsset == null)
				return;

			Root = visualAsset.CloneTree();
			Root.style.flexGrow = 1f;
			parent.Add(Root);

			// ���Ŀ¼
			string defaultOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
			_buildOutputField = Root.Q<TextField>("BuildOutput");
			_buildOutputField.SetValueWithoutNotify(defaultOutputRoot);
			_buildOutputField.SetEnabled(false);

			// �����汾
			_buildVersionField = Root.Q<TextField>("BuildVersion");
			_buildVersionField.style.width = StyleWidth;
			_buildVersionField.SetValueWithoutNotify(GetDefaultPackageVersion());

			// ����ģʽ
			{
				var buildModeContainer = Root.Q("BuildModeContainer");
				var buildMode = AssetBundleBuilderSetting.GetPackageBuildMode(PackageName, BuildPipeline);
				var buildModeList = GetSupportBuildModes();
				int defaultIndex = buildModeList.FindIndex(x => x.Equals(buildMode));
				_buildModeField = new PopupField<Enum>(buildModeList, defaultIndex);
				_buildModeField.label = "BuildMode";
				_buildModeField.style.width = StyleWidth;
				_buildModeField.RegisterValueChangedCallback(evt =>
				{
					AssetBundleBuilderSetting.SetPackageBuildMode(PackageName, BuildPipeline, (EBuildMode)_buildModeField.value);
				});
				buildModeContainer.Add(_buildModeField);
			}

			// ���ܷ���
			{
				var encryptionContainer = Root.Q("EncryptionContainer");
				var encryptionClassTypes = EditorTools.GetAssignableTypes(typeof(IEncryptionServices));
				if (encryptionClassTypes.Count > 0)
				{
					var encyptionClassName = AssetBundleBuilderSetting.GetPackageEncyptionClassName(PackageName, BuildPipeline);
					int defaultIndex = encryptionClassTypes.FindIndex(x => x.FullName.Equals(encyptionClassName));
					_encryptionField = new PopupField<Type>(encryptionClassTypes, defaultIndex);
					_encryptionField.label = "Encryption";
					_encryptionField.style.width = StyleWidth;
					_encryptionField.RegisterValueChangedCallback(evt =>
					{
						AssetBundleBuilderSetting.SetPackageEncyptionClassName(PackageName, BuildPipeline, _encryptionField.value.FullName);
					});
					encryptionContainer.Add(_encryptionField);
				}
				else
				{
					_encryptionField = new PopupField<Type>();
					_encryptionField.label = "Encryption";
					_encryptionField.style.width = StyleWidth;
					encryptionContainer.Add(_encryptionField);
				}
			}

			// ѹ����ʽѡ��
			var compressOption = AssetBundleBuilderSetting.GetPackageCompressOption(PackageName, BuildPipeline);
			_compressionField = Root.Q<EnumField>("Compression");
			_compressionField.Init(compressOption);
			_compressionField.SetValueWithoutNotify(compressOption);
			_compressionField.style.width = StyleWidth;
			_compressionField.RegisterValueChangedCallback(evt =>
			{
				AssetBundleBuilderSetting.SetPackageCompressOption(PackageName, BuildPipeline, (ECompressOption)_compressionField.value);
			});

			// ����ļ�������ʽ
			var fileNameStyle = AssetBundleBuilderSetting.GetPackageFileNameStyle(PackageName, BuildPipeline);
			_outputNameStyleField = Root.Q<EnumField>("FileNameStyle");
			_outputNameStyleField.Init(fileNameStyle);
			_outputNameStyleField.SetValueWithoutNotify(fileNameStyle);
			_outputNameStyleField.style.width = StyleWidth;
			_outputNameStyleField.RegisterValueChangedCallback(evt =>
			{
				AssetBundleBuilderSetting.SetPackageFileNameStyle(PackageName, BuildPipeline, (EFileNameStyle)_outputNameStyleField.value);
			});

			// �װ��ļ�����ѡ��
			var buildinFileCopyOption = AssetBundleBuilderSetting.GetPackageBuildinFileCopyOption(PackageName, BuildPipeline);
			_copyBuildinFileOptionField = Root.Q<EnumField>("CopyBuildinFileOption");
			_copyBuildinFileOptionField.Init(buildinFileCopyOption);
			_copyBuildinFileOptionField.SetValueWithoutNotify(buildinFileCopyOption);
			_copyBuildinFileOptionField.style.width = StyleWidth;
			_copyBuildinFileOptionField.RegisterValueChangedCallback(evt =>
			{
				AssetBundleBuilderSetting.SetPackageBuildinFileCopyOption(PackageName, BuildPipeline, (EBuildinFileCopyOption)_copyBuildinFileOptionField.value);
				RefreshView();
			});

			// �װ��ļ���������
			var buildinFileCopyParams = AssetBundleBuilderSetting.GetPackageBuildinFileCopyParams(PackageName, BuildPipeline);
			_copyBuildinFileTagsField = Root.Q<TextField>("CopyBuildinFileTags");
			_copyBuildinFileTagsField.SetValueWithoutNotify(buildinFileCopyParams);
			_copyBuildinFileTagsField.RegisterValueChangedCallback(evt =>
			{
				AssetBundleBuilderSetting.SetPackageBuildinFileCopyParams(PackageName, BuildPipeline, _copyBuildinFileTagsField.value);
			});

			// ������ť
			var buildButton = Root.Q<Button>("Build");
			buildButton.clicked += BuildButton_clicked;
		}
		private void RefreshView()
		{
			var buildinFileCopyOption = AssetBundleBuilderSetting.GetPackageBuildinFileCopyOption(PackageName, BuildPipeline);
			bool tagsFiledVisible = buildinFileCopyOption == EBuildinFileCopyOption.ClearAndCopyByTags || buildinFileCopyOption == EBuildinFileCopyOption.OnlyCopyByTags;
			_copyBuildinFileTagsField.visible = tagsFiledVisible;
		}
		private void BuildButton_clicked()
		{
			var buildMode = AssetBundleBuilderSetting.GetPackageBuildMode(PackageName, BuildPipeline);
			if (EditorUtility.DisplayDialog("��ʾ", $"ͨ������ģʽ��{buildMode}����������", "Yes", "No"))
			{
				EditorTools.ClearUnityConsole();
				EditorApplication.delayCall += ExecuteBuild;
			}
			else
			{
				Debug.LogWarning("[Build] ����Ѿ�ȡ��");
			}
		}

		/// <summary>
		/// ִ�й�������
		/// </summary>
		protected abstract void ExecuteBuild();

		/// <summary>
		/// ��ȡ��������֧�ֵĹ���ģʽ����
		/// </summary>
		protected abstract List<Enum> GetSupportBuildModes();

		/// <summary>
		/// ��ȡ�����汾
		/// </summary>
		protected string GetPackageVersion()
		{
			return _buildVersionField.value;
		}

		/// <summary>
		/// ����������ʵ��
		/// </summary>
		protected IEncryptionServices CreateEncryptionInstance()
		{
			var encyptionClassName = AssetBundleBuilderSetting.GetPackageEncyptionClassName(PackageName, BuildPipeline);
			var encryptionClassTypes = EditorTools.GetAssignableTypes(typeof(IEncryptionServices));
			var classType = encryptionClassTypes.Find(x => x.FullName.Equals(encyptionClassName));
			if (classType != null)
				return (IEncryptionServices)Activator.CreateInstance(classType);
			else
				return null;
		}

		private string GetDefaultPackageVersion()
		{
			int totalMinutes = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
			return DateTime.Now.ToString("yyyy-MM-dd") + "-" + totalMinutes;
		}
	}
}
#endif