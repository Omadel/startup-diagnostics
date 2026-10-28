using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Finds where an asset, or the objects of a scene, reference another asset: a component and its field, a prefab
    ///     instance, or a property of a material. The references of each asset are collected once.
    /// </summary>
    internal sealed class ReferenceFinder
    {
        private static readonly Type[] _skippedTypes = { typeof(Mesh), typeof(Texture), typeof(AnimationClip), typeof(AudioClip), typeof(Shader), typeof(Avatar), typeof(Transform), typeof(Font) };
        private static readonly string[] _prefabLinks = { "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject", "m_Modification", "m_SourcePrefab" };

        private readonly string _scenePath;
        private readonly Func<IEnumerable<Object>> _sceneObjects;
        private readonly Dictionary<string, Dictionary<string, string>> _references = new();

        /// <summary>
        ///     A finder for the chains of a scene, whose objects are those of the scene opened as a preview scene.
        /// </summary>
        public ReferenceFinder(string scenePath, Func<IEnumerable<Object>> sceneObjects)
        {
            _scenePath = scenePath;
            _sceneObjects = sceneObjects;
        }

        /// <summary>
        ///     Where the owner references the target, such as <c>Dog › DogTestController._breeds[1]</c>, or null when not
        ///     found.
        /// </summary>
        public string Describe(string owner, string target)
        {
            if (!_references.TryGetValue(owner, out var references))
            {
                references = Collect(owner, owner == _scenePath ? _sceneObjects() : AssetDatabase.LoadAllAssetsAtPath(owner));
                _references.Add(owner, references);
            }

            return references.TryGetValue(target, out var where) ? where : null;
        }

        /// <summary>
        ///     The path of an object in the hierarchy of its scene or prefab.
        /// </summary>
        public static string GetHierarchyPath(Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = $"{parent.name}/{path}";
            }

            return path;
        }

        private static Dictionary<string, string> Collect(string owner, IEnumerable<Object> objects)
        {
            var references = new Dictionary<string, string>();
            foreach (var asset in objects)
            {
                if (asset == null || IsSkipped(asset)) continue;

                switch (asset)
                {
                    case GameObject gameObject:
                        CollectPrefabInstance(gameObject, references);
                        break;
                    case Material material:
                        CollectMaterial(material, references);
                        break;
                    default:
                        CollectProperties(owner, asset, references);
                        break;
                }
            }

            return references;
        }

        private static void CollectPrefabInstance(GameObject gameObject, Dictionary<string, string> references)
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(gameObject)) return;

            Add(references, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject), $"{GetHierarchyPath(gameObject.transform)}, prefab instance");
        }

        private static void CollectMaterial(Material material, Dictionary<string, string> references)
        {
            foreach (var property in material.GetTexturePropertyNames())
            {
                var texture = material.GetTexture(property);
                if (texture != null)
                {
                    Add(references, AssetDatabase.GetAssetPath(texture), $"{material.name} › {property}");
                }
            }

            if (material.shader != null)
            {
                Add(references, AssetDatabase.GetAssetPath(material.shader), $"{material.name} › shader");
            }
        }

        /// <summary>
        ///     Collects the object references of the serialized properties, without the links of Unity between the objects
        ///     of a prefab and their source, which <see cref="CollectPrefabInstance" /> describes as prefab instances.
        /// </summary>
        private static void CollectProperties(string owner, Object asset, Dictionary<string, string> references)
        {
            var property = new SerializedObject(asset).GetIterator();
            var enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = property.depth > 0 || Array.IndexOf(_prefabLinks, property.name) < 0;
                if (!enterChildren || property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null) continue;

                var path = AssetDatabase.GetAssetPath(property.objectReferenceValue);
                if (path != owner)
                {
                    Add(references, path, $"{DescribeObject(asset)}.{ReferenceChains.FormatPropertyPath(property.propertyPath)}");
                }
            }
        }

        private static string DescribeObject(Object asset)
        {
            if (asset is Component component) return $"{GetHierarchyPath(component.transform)} › {component.GetType().Name}";

            return string.IsNullOrEmpty(asset.name) ? asset.GetType().Name : $"{asset.GetType().Name} {asset.name}";
        }

        private static bool IsSkipped(Object asset)
        {
            foreach (var type in _skippedTypes)
            {
                if (type.IsInstanceOfType(asset)) return true;
            }

            return false;
        }

        private static void Add(Dictionary<string, string> references, string path, string where)
        {
            if (!string.IsNullOrEmpty(path) && !references.ContainsKey(path))
            {
                references.Add(path, where);
            }
        }
    }
}