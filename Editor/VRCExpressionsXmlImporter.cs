using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Experimental.AssetImporters;
using VRC.SDK3.Avatars.ScriptableObjects;
using System.Xml.Linq;

using ValueType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType;
using ControlType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType;

namespace KLabs.VRC.SDK3.Avatars.ScriptableObjects
{
	[ScriptedImporter(1, "vrcexpxml")]
	public class VRCExpressionsXmlImporter : ScriptedImporter
	{
		[field: SerializeField]
		public List<NameMenuPair> XrefsMenu { get; set; } = new List<NameMenuPair>();

		[field: SerializeField]
		public string[] MenuLabels { get; private set; } = new string[0];

		public override void OnImportAsset(AssetImportContext ctx)
		{
			var vrcExps = ScriptableObject.CreateInstance<VRCExpressions>();
			ctx.AddObjectToAsset(nameof(VRCExpressions), vrcExps);
			ctx.SetMainObject(vrcExps);

			var vrcExpParams = ScriptableObject.CreateInstance<VRCExpressionParameters>();
			vrcExpParams.name = "Parameters";
			ctx.AddObjectToAsset("Parameters", vrcExpParams);
			vrcExps.Parameters = vrcExpParams;

			try
			{
				var doc = XDocument.Load(assetPath);
				var exps = doc.Element("VRCExpressions");
				var parameters = exps.Element("VRCExpressionParameters");

				var paramsDic = new Dictionary<string, VRCExpressionParameters.Parameter>();

				foreach (var item in parameters.Elements())
				{
					var p = ParametersElementParser.Parse(item);
					paramsDic.Add(p.name, p);
				}
				vrcExpParams.parameters = paramsDic.Values.ToArray();

				var menus = exps.Element("VRCExpressionsMenus");
				if (menus == null)
				{
					return;
				}

				var menusParser = new MenusElementParser();
				var assets = menusParser.Parse(menus);
				var hashset = menusParser.GetMenuHashSet();

				var a = XrefsMenu.Select(e => e.name).ToArray();

				foreach (var item in menusParser.subMenuControls)
				{
					if (GetExternalObjectMap().TryGetValue(new SourceAssetIdentifier(typeof(VRCExpressionsMenu), item.name), out var map))
					{
						item.control.subMenu = (VRCExpressionsMenu)map;
					}
				}

				foreach (var item in assets)
				{
					ctx.AddObjectToAsset(item.name, item);
				}
			}
			catch (System.Exception e)
			{
				Debug.LogError(e.Message, vrcExpParams);
			}
		}

		private static class ParametersElementParser
		{
			public static VRCExpressionParameters.Parameter Parse(XElement element)
			{
				var type = element.Attribute("value-type");
				var value = element.Attribute("default-value");
				var saved = element.Attribute("saved");

				var p = new VRCExpressionParameters.Parameter() { name = element.Name.LocalName };
				p.valueType = ParseType(type);
				p.defaultValue = ParseValue(value);
				p.saved = ParseSaved(saved);
				return p;
			}

			private static ValueType ParseType(XAttribute attribute)
			{
				switch (attribute.Value)
				{
					case "int":
						return ValueType.Int;
					case "float":
						return ValueType.Float;
					case "bool":
						return ValueType.Bool;
					default:
						throw new System.Exception();
				}
			}

			private static float ParseValue(XAttribute attribute)
			{
				if (float.TryParse(attribute.Value, out var value))
				{
					return value;
				}
				switch (attribute.Value)
				{
					case "true":
						return 1.0f;
					case "false":
						return 0.0f;
					default:
						throw new System.Exception();
				}
			}

			private static bool ParseSaved(XAttribute attribute)
			{
				switch (attribute.Value)
				{
					case "true":
						return true;
					case "false":
						return false;
					default:
						throw new System.Exception();
				}
			}
		}

		private class MenusElementParser
		{
			public Dictionary<string, VRCExpressionsMenu> assets;
			public List<NameControlPair> subMenuControls;

			public VRCExpressionsMenu[] Parse(XElement element)
			{
				var elements = element.Elements();
				assets = new Dictionary<string, VRCExpressionsMenu>(elements.Count());
				subMenuControls = new List<NameControlPair>();
				foreach (var item in elements)
				{
					ParseMenu(item);
					LinkSubMenus();
				}
				return assets.Values.ToArray();
			}

			public HashSet<string> GetMenuHashSet()
			{
				var hashset = new HashSet<string>();
				foreach (var item in assets.Keys)
				{
					hashset.Add(item);
				}
				foreach (var item in subMenuControls)
				{
					hashset.Add(item.name);
				}
				return hashset;
			}

			private void ParseMenu(XElement element)
			{
				var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
				menu.name = element.Name.LocalName;
				foreach (var item in element.Elements())
				{
					var control = new VRCExpressionsMenu.Control();
					if (!System.Enum.TryParse<ControlType>(item.Name.LocalName, true, out var type))
					{
						continue;
					}
					control.name = item.Attribute("name").Value;
					var parameter = item.Attribute("parameter");
					control.parameter = parameter == null ? null : new VRCExpressionsMenu.Control.Parameter() { name = parameter.Value };
					if (control.parameter != null)
					{
						control.value = float.Parse(item.Attribute("value").Value);
					}
					control.type = type;
					switch (type)
					{
						case ControlType.Button:
							break;
						case ControlType.Toggle:
							break;
						case ControlType.SubMenu:
							subMenuControls.Add(new NameControlPair() { name = item.Attribute("asset").Value ?? string.Empty, control = control });
							break;
						case ControlType.TwoAxisPuppet:
							ParseTwoAxisPuppet(control, item);
							break;
						case ControlType.FourAxisPuppet:
							ParseFourAxisPuppet(control, item);
							break;
						case ControlType.RadialPuppet:
							ParseRadialPuppet(control, item);
							break;
						case ControlType.OneAxisPuppet:
							break;
						default:
							break;
					}

					menu.controls.Add(control);
				}
				assets.Add(menu.name, menu);
			}

			private void LinkSubMenus()
			{
				foreach (var item in subMenuControls)
				{
					if (assets.TryGetValue(item.name, out var menu))
					{
						item.control.subMenu = menu;
					}
				}
			}
		}

		private static void ParseTwoAxisPuppet(VRCExpressionsMenu.Control control, XElement element)
		{
			var parameters = new VRCExpressionsMenu.Control.Parameter[2];
			var labels = new VRCExpressionsMenu.Control.Label[4];
			var horizontal = element.Element("Horizontal");
			var vertical = element.Element("Vertical");
			if (horizontal != null)
			{
				parameters[0] = new VRCExpressionsMenu.Control.Parameter() { name = horizontal.Attribute("parameter")?.Value ?? string.Empty };
				labels[1] = new VRCExpressionsMenu.Control.Label() { name = horizontal.Attribute("right")?.Value ?? string.Empty };
				labels[3] = new VRCExpressionsMenu.Control.Label() { name = horizontal.Attribute("left")?.Value ?? string.Empty };
			}
			if (vertical != null)
			{
				parameters[1] = new VRCExpressionsMenu.Control.Parameter() { name = vertical.Attribute("parameter")?.Value ?? string.Empty };
				labels[0] = new VRCExpressionsMenu.Control.Label() { name = vertical.Attribute("up")?.Value ?? string.Empty };
				labels[2] = new VRCExpressionsMenu.Control.Label() { name = vertical.Attribute("down")?.Value ?? string.Empty };
			}
			control.subParameters = parameters;
			control.labels = labels;
		}

		private static void ParseFourAxisPuppet(VRCExpressionsMenu.Control control, XElement element)
		{
			var parameters = new VRCExpressionsMenu.Control.Parameter[4];
			var labels = new VRCExpressionsMenu.Control.Label[4];
			var up = element.Element("Up");
			var down = element.Element("Down");
			var right = element.Element("Right");
			var left = element.Element("Left");

			if (up != null)
			{
				var ret = ParseFourAxisPuppetItem(up);
				parameters[0] = ret.parameter;
				labels[0] = ret.label;
			}
			if (right != null)
			{
				var ret = ParseFourAxisPuppetItem(right);
				parameters[1] = ret.parameter;
				labels[1] = ret.label;
			}
			if (down != null)
			{
				var ret = ParseFourAxisPuppetItem(down);
				parameters[2] = ret.parameter;
				labels[2] = ret.label;
			}
			if (left != null)
			{
				var ret = ParseFourAxisPuppetItem(left);
				parameters[3] = ret.parameter;
				labels[3] = ret.label;
			}
			control.subParameters = parameters;
			control.labels = labels;
		}

		private static void ParseRadialPuppet(VRCExpressionsMenu.Control control, XElement element)
		{
			control.subParameters = new VRCExpressionsMenu.Control.Parameter[]
			{
				new VRCExpressionsMenu.Control.Parameter() { name = element.Attribute("rotation-parameter")?.Value ?? string.Empty }
			};
		}

		private static FourAxisPuppetPair ParseFourAxisPuppetItem(XElement element)
		{
			return new FourAxisPuppetPair()
			{
				parameter = new VRCExpressionsMenu.Control.Parameter() { name = element.Attribute("parameter")?.Value ?? string.Empty },
				label = new VRCExpressionsMenu.Control.Label() { name = element.Attribute("label")?.Value ?? string.Empty }
			};
		}

		private struct FourAxisPuppetPair
		{
			public VRCExpressionsMenu.Control.Parameter parameter;
			public VRCExpressionsMenu.Control.Label label;
		}

		[System.Serializable]
		public struct NameControlPair
		{
			[field: SerializeField]
			public string name { get; set; }

			[field: SerializeField]
			public VRCExpressionsMenu.Control control { get; set; }
		}

		[System.Serializable]
		public struct NameMenuPair
		{
			[field: SerializeField]
			public string name { get; set; }

			[field: SerializeField]
			public VRCExpressionsMenu menu { get; set; }
		}
	}
}
