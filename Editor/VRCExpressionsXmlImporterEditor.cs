#if UNITY_EDITOR
using System.Linq;
using System.Collections.Generic;
using UnityEditor.Experimental.AssetImporters;
using UnityEditor;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace KLabs.VRC.SDK3.Avatars.ScriptableObjects
{
	[CustomEditor(typeof(VRCExpressionsXmlImporter))]
	public class VRCExpressionsXmlImporterEditor : ScriptedImporterEditor
	{
		private KeyValuePair<string, VRCExpressionsMenu>[] subAssets = new KeyValuePair<string, VRCExpressionsMenu>[0];

		public override void OnInspectorGUI()
		{
			var target = (VRCExpressionsXmlImporter)this.target;
			for (int i = 0; i < subAssets.Length; i++)
			{
				var item = subAssets[i];
				var changeValue = EditorGUILayout.ObjectField(item.Key, item.Value, typeof(VRCExpressionsMenu), false) as VRCExpressionsMenu;
				if (changeValue != item.Value)
				{
					var pair = new VRCExpressionsXmlImporter.NameMenuPair() { name = item.Key, menu = changeValue };
					var index = target.XrefsMenu.FindIndex(e => e.name == item.Key);
					if (index == -1)
					{
						target.XrefsMenu.Add(pair);
					}
					else
					{
						target.XrefsMenu[index] = pair;
					}
					subAssets[i] = new KeyValuePair<string, VRCExpressionsMenu>(item.Key, changeValue);
				}
			}
			ApplyRevertGUI();
		}

		protected override void Apply()
		{
			var target = (VRCExpressionsXmlImporter)this.target;
			foreach (var item in target.XrefsMenu)
			{
				if (item.menu == null)
				{
					target.RemoveRemap(new AssetImporter.SourceAssetIdentifier(typeof(VRCExpressionsMenu), item.name));
				}
				else
				{
					target.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(VRCExpressionsMenu), item.name), item.menu);
				}
			}
			target.XrefsMenu = target.XrefsMenu.Where(e => e.menu != null).ToList();
			base.Apply();

			AssetDatabase.WriteImportSettingsIfDirty(target.assetPath);
			AssetDatabase.ImportAsset(target.assetPath, ImportAssetOptions.ForceUpdate);
		}

		public override void OnEnable()
		{
			base.OnEnable();
			var target = (VRCExpressionsXmlImporter)this.target;
			var tempAssets = AssetDatabase.LoadAllAssetsAtPath(target.assetPath);
			var view = new Dictionary<string, VRCExpressionsMenu>();
			foreach (var item in target.MenuLabels)
			{
				view[item] = null;
			}
			foreach (var item in tempAssets)
			{
				if (item is VRCExpressionsMenu && AssetDatabase.IsSubAsset(item))
				{
					view[item.name] = null;
				}
			}
			var keys = view.Keys.ToArray();
			foreach (var key in keys)
			{
				var p = target.XrefsMenu.Find(e => e.name == key);
				view[key] = p.name != null ? p.menu : null;
			}
			subAssets = view.ToArray();
		}

		public override void OnDisable()
		{
			base.OnDisable();
		}
	}
}
#endif
