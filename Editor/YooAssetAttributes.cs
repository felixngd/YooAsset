using System;
using System.Reflection;

namespace YooAsset.Editor
{
    /// <summary>
    /// �༭����ʾ����
    /// </summary>
    internal sealed class EditorShowAttribute : Attribute 
    {
        public string Name;
        public EditorShowAttribute(string name)
        {
            this.Name = name;
        }
    }

    internal static class YooAssetAttributes
    {
        /// <summary>
        /// ��ȡ Type ����
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="type"></param>
        /// <returns>����Ϊ��</returns>
        internal static T GetAttribute<T>(Type type) where T : Attribute
        {
            return (T)type.GetCustomAttribute(typeof(T), false);
        }

        /// <summary>
        /// ��ȡ MethodInfo ����
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="methodInfo"></param>
        /// <returns>����Ϊ��</returns>
        internal static T GetAttribute<T>(MethodInfo methodInfo) where T : Attribute
        {
            return (T)methodInfo.GetCustomAttribute(typeof(T), false);
        }

        /// <summary>
        /// ��ȡ FieldInfo ����
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="field"></param>
        /// <returns>����Ϊ��</returns>
        internal static T GetAttribute<T>(FieldInfo field) where T : Attribute
        {
            return (T)field.GetCustomAttribute(typeof(T), false);
        }


    }
}
